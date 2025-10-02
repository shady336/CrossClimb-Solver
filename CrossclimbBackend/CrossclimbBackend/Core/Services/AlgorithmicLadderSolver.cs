using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Models;

namespace CrossclimbBackend.Core.Services;

/// <summary>
/// Algorithmic ladder solver that uses LLM calls for candidate generation and graph-based algorithms for ladder construction.
/// It proceeds in 3 phases per attempt:
/// 1. Candidate generation for every clue using direct HTTP calls to Azure OpenAI.
/// 2. Ladder construction using algorithmic backtracking approach choosing one candidate per clue while enforcing hamming distance = 1 between adjacent words.
/// 3. Validation; retries up to 5 attempts.
///
/// The final structure is mapped into SolveLadderResponse so rest of pipeline remains unchanged.
/// </summary>
public class AlgorithmicLadderSolver : ILadderSolver
{
    private readonly IValidationService _validator;
    private readonly ILogger<AlgorithmicLadderSolver> _logger;
    private readonly IAoaiService _aoaiService;

    public AlgorithmicLadderSolver(IValidationService validator, ILogger<AlgorithmicLadderSolver> logger, IAoaiService aoaiService)
    {
        _validator = validator;
        _logger = logger;
        _aoaiService = aoaiService;
    }

    public async Task<(SolveLadderResponse response, AoaiResponse aoaiInfo)> SolveAsync(SolveLadderRequest request)
    {
        // Orchestrate attempts
        const int maxAttempts = 5;
        SolveLadderResponse? best = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var (response, reason) = await RunSingleAttemptAsync(request, attempt, maxAttempts);
            var validationError = _validator.ValidateResponse(response, request);
            if (validationError == null)
            {
                _logger.LogInformation("Algorithmic ladder success on attempt {Attempt}", attempt);
                best = response;
                break;
            }
            else
            {
                _logger.LogWarning("Attempt {Attempt} failed validation: {Error}. Reason: {Reason}", attempt, validationError, reason);
                best = response; // keep last for debugging if all fail
            }
        }

        if (best == null)
            throw new InvalidOperationException("Failed to generate ladder");

        // We cannot easily get token counts without deeper instrumentation; set 0.
        var aoaiInfo = new AoaiResponse("ALGORITHMIC_SOLVER");
        return (best, aoaiInfo);
    }

    private async Task<(SolveLadderResponse response, string reasoning)> RunSingleAttemptAsync(SolveLadderRequest request, int attempt, int maxAttempts)
    {
        // 1. Generate candidates for each clue via LLM
        var candidateSets = new List<List<(string word, string reasoning)>>();
        
        for (int i = 0; i < request.Clues.Count; i++)
        {
            var words = await GenerateCandidatesAsync(request.Clues[i], request.WordLength);
            candidateSets.Add(words);
        }

        // 2. Use algorithmic approach to construct ladder from candidate sets
        var (ladder, pairs) = BuildLadderAlgorithmically(candidateSets, request.Clues, request.WordLength);

        var response = new SolveLadderResponse
        {
            Ladder = ladder.ToArray(),
            Pairs = pairs
        };

        var attemptReasoning = $"Attempt {attempt}/{maxAttempts}: generated {candidateSets.Sum(s => s.Count)} candidates across {candidateSets.Count} clues, then used algorithmic approach to construct ladder.";
        return (response, attemptReasoning);
    }

    private (List<string> ladder, List<LadderPair> pairs) BuildLadderAlgorithmically(List<List<(string word, string reasoning)>> candidateSets, List<string> clues, int wordLength)
    {
        _logger.LogInformation("Building ladder algorithmically with {SetCount} candidate sets", candidateSets.Count);
        
        // If only one clue, return that candidate
        if (candidateSets.Count == 1)
        {
            if (candidateSets[0].Count > 0)
            {
                var candidate = candidateSets[0][0];
                return (new List<string> { candidate.word }, 
                       new List<LadderPair> { new LadderPair { Word = candidate.word, Clue = clues[0], Reasoning = candidate.reasoning } });
            }
            else
            {
                var fallback = RandomWord(wordLength);
                return (new List<string> { fallback }, 
                       new List<LadderPair> { new LadderPair { Word = fallback, Clue = clues[0], Reasoning = "Fallback word" } });
            }
        }

        // Try to find a valid ladder using backtracking
        var result = FindLadderPath(candidateSets, clues);
        
        if (result.ladder.Count > 0)
        {
            _logger.LogInformation("Successfully built algorithmic ladder with {Count} words: {Words}", 
                result.ladder.Count, string.Join(" -> ", result.ladder));
            return result;
        }
        
        // Fallback if no valid ladder found
        _logger.LogWarning("No valid ladder found algorithmically, using fallback");
        return BuildFallbackLadderFromCandidates(candidateSets, clues, wordLength);
    }

    private (List<string> ladder, List<LadderPair> pairs) FindLadderPath(List<List<(string word, string reasoning)>> candidateSets, List<string> clues)
    {
        var numSets = candidateSets.Count;
        
        // Create a mapping from word to its set index and reasoning
        var wordToSetInfo = new Dictionary<string, List<(int setIndex, string reasoning)>>();
        
        for (int setIndex = 0; setIndex < numSets; setIndex++)
        {
            foreach (var (word, reasoning) in candidateSets[setIndex])
            {
                if (!wordToSetInfo.ContainsKey(word))
                {
                    wordToSetInfo[word] = new List<(int, string)>();
                }
                wordToSetInfo[word].Add((setIndex, reasoning));
            }
        }
        
        // Build adjacency graph (words that differ by exactly 1 letter)
        var allWords = wordToSetInfo.Keys.ToList();
        var adjacencyGraph = new Dictionary<string, List<string>>();
        
        foreach (var word in allWords)
        {
            adjacencyGraph[word] = new List<string>();
        }
        
        for (int i = 0; i < allWords.Count; i++)
        {
            for (int j = i + 1; j < allWords.Count; j++)
            {
                if (Hamming(allWords[i], allWords[j]) == 1)
                {
                    adjacencyGraph[allWords[i]].Add(allWords[j]);
                    adjacencyGraph[allWords[j]].Add(allWords[i]);
                }
            }
        }
        
        // Try DFS from each word to find a valid ladder
        foreach (var startWord in allWords)
        {
            var path = new List<string>();
            var usedSets = new HashSet<int>();
            
            if (DfsLadderSearch(startWord, adjacencyGraph, wordToSetInfo, usedSets, path, numSets))
            {
                // Build the pairs with correct clue mapping
                var pairs = new List<LadderPair>();
                foreach (var word in path)
                {
                    var setInfoList = wordToSetInfo[word];
                    // Find which set this word came from (prefer the first available)
                    var setInfo = setInfoList[0];
                    pairs.Add(new LadderPair
                    {
                        Word = word,
                        Clue = clues[setInfo.setIndex],
                        Reasoning = setInfo.reasoning
                    });
                }
                
                return (path, pairs);
            }
        }
        
        // No valid ladder found
        return (new List<string>(), new List<LadderPair>());
    }

    private bool DfsLadderSearch(string currentWord, Dictionary<string, List<string>> adjacencyGraph, 
        Dictionary<string, List<(int setIndex, string reasoning)>> wordToSetInfo, 
        HashSet<int> usedSets, List<string> path, int targetLength)
    {
        // Add current word to path
        path.Add(currentWord);
        
        // Mark sets as used for this word
        var currentWordSets = wordToSetInfo[currentWord].Select(info => info.setIndex).ToList();
        var newlyUsedSets = new List<int>();
        
        // Choose one set for this word (prefer unused sets)
        int chosenSet = -1;
        foreach (var setIndex in currentWordSets)
        {
            if (!usedSets.Contains(setIndex))
            {
                chosenSet = setIndex;
                break;
            }
        }
        
        // If no unused set available, we can't use this word
        if (chosenSet == -1)
        {
            path.RemoveAt(path.Count - 1);
            return false;
        }
        
        usedSets.Add(chosenSet);
        newlyUsedSets.Add(chosenSet);
        
        // Check if we've reached the target length
        if (path.Count == targetLength)
        {
            return true; // Found a valid complete ladder
        }
        
        // Continue DFS to adjacent words
        foreach (var nextWord in adjacencyGraph[currentWord])
        {
            // Check if this word comes from an unused set
            var nextWordSets = wordToSetInfo[nextWord].Select(info => info.setIndex).ToList();
            bool hasUnusedSet = nextWordSets.Any(setIndex => !usedSets.Contains(setIndex));
            
            if (hasUnusedSet && !path.Contains(nextWord))
            {
                if (DfsLadderSearch(nextWord, adjacencyGraph, wordToSetInfo, usedSets, path, targetLength))
                {
                    return true;
                }
            }
        }
        
        // Backtrack
        foreach (var setIndex in newlyUsedSets)
        {
            usedSets.Remove(setIndex);
        }
        path.RemoveAt(path.Count - 1);
        
        return false;
    }

    private async Task<List<(string word, string reasoning)>> GenerateCandidatesAsync(string clue, int wordLength)
    {
        try
        {
            var systemMessage = "You are a helpful assistant that generates candidate words for crossword-style clues. Always respond with valid JSON only, no other text.";
            
            var userMessage = $"Generate between 5 and 10 plausible ENGLISH UPPERCASE candidate words of exactly {wordLength} letters that could answer this crossword-style clue: \"{clue}\"\n\nReturn ONLY a JSON array of objects with word and reasoning fields. Words must be exactly {wordLength} letters, letters A-Z only.\n\nFormat: [{{\"word\":\"HOUSE\",\"reasoning\":\"A building where people live\"}}, ...]";

            var aoaiResponse = await _aoaiService.GetChatCompletionAsync(systemMessage, userMessage, 0.7f, 0.95f);
            var content = aoaiResponse.Content ?? "[]";
            
            _logger.LogInformation("Raw LLM response for clue '{Clue}' (wordLength={WordLength}): {Content}", clue, wordLength, content);
            
            // Clean up the content to extract JSON
            var cleanContent = ExtractJsonFromContent(content);
            
            _logger.LogInformation("Cleaned JSON content: {CleanContent}", cleanContent);
            
            // Parse minimal JSON
            var list = new List<(string,string)>();
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(cleanContent);
                if (json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var el in json.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("word", out var w) && el.TryGetProperty("reasoning", out var r))
                        {
                            var word = (w.GetString() ?? string.Empty).Trim().ToUpperInvariant();
                            var reasoning = r.GetString() ?? string.Empty;
                            
                            _logger.LogDebug("Parsed word: '{Word}' (length={Length}, expected={ExpectedLength}), reasoning: '{Reasoning}'", 
                                word, word.Length, wordLength, reasoning);
                            

                            if (word.Length == wordLength && word.All(char.IsLetter))
                            {
                                list.Add((word, reasoning));
                                _logger.LogDebug("Word '{Word}' accepted", word);
                            }
                            else
                            {
                                _logger.LogWarning("Word '{Word}' rejected - length: {ActualLength}, expected: {ExpectedLength}, all letters: {AllLetters}", 
                                    word, word.Length, wordLength, word.All(char.IsLetter));
                            }
                        }
                        else
                        {
                            _logger.LogWarning("JSON element missing 'word' or 'reasoning' property: {Element}", el.ToString());
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("JSON root element is not an array: {ValueKind}", json.RootElement.ValueKind);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Candidate JSON parse failed for content: {Content}", cleanContent);
            }

            // Fallback minimal diversity if empty
            if (list.Count == 0)
            {
                _logger.LogWarning("No valid candidates parsed from LLM response, using fallback for clue: {Clue}", clue);
                list.Add((RandomWord(wordLength), "Fallback synthetic candidate"));
                list.Add((RandomWord(wordLength), "Fallback synthetic candidate"));
            }
            
            _logger.LogInformation("Generated {Count} candidates for clue '{Clue}': {Words}", 
                list.Count, clue, string.Join(", ", list.Select(x => x.Item1)));
            
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate candidates, using fallback");
            return new List<(string, string)>
            {
                (RandomWord(wordLength), "Fallback due to generation error"),
                (RandomWord(wordLength), "Fallback due to generation error")
            };
        }
    }

    private (List<string> ladder, List<LadderPair> pairs) BuildFallbackLadderFromCandidates(List<List<(string word, string reasoning)>> candidateSets, List<string> clues, int wordLength)
    {
        _logger.LogInformation("Building fallback ladder using first candidate from each set");
        
        var ladder = new List<string>();
        var pairs = new List<LadderPair>();
        
        for (int i = 0; i < candidateSets.Count; i++)
        {
            if (candidateSets[i].Count > 0)
            {
                var candidate = candidateSets[i][0];
                ladder.Add(candidate.word);
                pairs.Add(new LadderPair
                {
                    Word = candidate.word,
                    Clue = clues[i],
                    Reasoning = candidate.reasoning
                });
            }
            else
            {
                var fallbackWord = RandomWord(wordLength);
                ladder.Add(fallbackWord);
                pairs.Add(new LadderPair
                {
                    Word = fallbackWord,
                    Clue = clues[i],
                    Reasoning = "Fallback word due to no candidates"
                });
            }
        }
        
        _logger.LogInformation("Fallback ladder built with {Count} words", ladder.Count);
        return (ladder, pairs);
    }

    private string ExtractJsonFromContent(string content)
    {
        // Remove common prefixes/suffixes that LLMs might add
        content = content.Trim();
        
        // Look for JSON array start and end
        var startIndex = content.IndexOf('[');
        var endIndex = content.LastIndexOf(']');
        
        if (startIndex >= 0 && endIndex > startIndex)
        {
            var arrayContent = content.Substring(startIndex, endIndex - startIndex + 1);
            _logger.LogDebug("Extracted JSON array: {JsonArray}", arrayContent);
            return arrayContent;
        }
        
        // Last resort: try to extract anything that looks like JSON
        var objectStartIndex = content.IndexOf('{');
        if (objectStartIndex >= 0)
        {
            var remaining = content.Substring(objectStartIndex).Trim();
            _logger.LogWarning("Attempting to use partial JSON: {PartialJson}", remaining);
            return remaining;
        }
        
        _logger.LogWarning("No valid JSON found in content, returning original: {Content}", content);
        return content;
    }

    private static int Hamming(string a, string b)
    {
        if (a.Length != b.Length) return int.MaxValue;
        int d = 0; 
        for (int i = 0; i < a.Length; i++) 
            if (a[i] != b[i]) 
                d++; 
        return d;
    }

    private static string RandomWord(int length)
    {
        var rand = new Random();
        var chars = new char[length];
        for (int i = 0; i < length; i++) 
            chars[i] = (char)('A' + rand.Next(26));
        return new string(chars);
    }
}