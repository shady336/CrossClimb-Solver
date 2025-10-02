using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Services;
using CrossclimbBackend.Utils;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CrossclimbBackend.Functions
{
    public class SolveEndsFunction
    {
        private readonly IAoaiService _aoai;
        private readonly ILogger<SolveEndsFunction> _logger;

        public SolveEndsFunction(IAoaiService aoai, ILogger<SolveEndsFunction> logger)
        {
            _aoai = aoai;
            _logger = logger;
        }

        [FunctionName("SolveEnds")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "solve/ends")] HttpRequest req)
        {
            var requestId = Guid.NewGuid().ToString();
            try
            {
                if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    return CorsHelper.HandleOptionsRequest(req.HttpContext.Response);
                }

                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<SolveEndsRequest>(body);
                if (request == null)
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "Invalid JSON body", requestId }, req.HttpContext.Response);

                // Validate input
                if (request.WordLength <= 0)
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "wordLength must be > 0", requestId }, req.HttpContext.Response);

                if (string.IsNullOrWhiteSpace(request.NeighborWord))
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "neighborWord must be provided", requestId }, req.HttpContext.Response);

                if (request.NeighborWord.Length != request.WordLength)
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "neighborWord length must match wordLength", requestId }, req.HttpContext.Response);

                if (string.IsNullOrWhiteSpace(request.Clue))
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "clue must be provided", requestId }, req.HttpContext.Response);

                var normalizedNeighbor = request.NeighborWord.Trim().ToUpperInvariant();
                var clueWords = request.Clue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim().ToUpper().TrimEnd('.', ',', '!', '?', ';', ':', '"', '\'', ')', '('))
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .ToHashSet();

                const int maxAttempts = 15;
                SolveCandidate? chosen = null;
                List<SolveCandidate> lastCandidates = new();
                var previouslySeen = new HashSet<string>();

                for (int attempt = 1; attempt <= maxAttempts && chosen == null; attempt++)
                {
                    string previouslySeenSection = previouslySeen.Count > 0
                        ? $"\nALSO IMPORTANT: Do NOT output any of these previously attempted / rejected words (they either failed Hamming distance or were duplicates): {string.Join(", ", previouslySeen.OrderBy(x => x))}. If all good candidates are used, produce NEW different ones." : string.Empty;

                    var system = $@"You are an expert word puzzle assistant.
Generate between 10 and 20 DISTINCT candidate words that satisfy ALL rules:
1. Each candidate MUST be a real common English word.
2. Length MUST be exactly {request.WordLength} letters.
3. Must NOT be any word that appears literally in the clue text.
4. Each candidate should plausibly answer the clue.
5. DO NOT attempt to enforce Hamming distance yourself; just generate strong semantic candidates.
6. All answers MUST be unique within this list.
{previouslySeenSection}
Return ONLY JSON array like:
[
  {{ ""answer"": ""WORD1"", ""reasoning"": ""why it fits clue (no hamming mention)"" }},
  {{ ""answer"": ""WORD2"", ""reasoning"": ""why it fits clue (no hamming mention)"" }}
]
No extra text, no markdown.";

                    var user = $@"CLUE: ""{request.Clue}""
NEIGHBOR WORD: ""{normalizedNeighbor}""
Provide candidate answers (do NOT include the neighbor word unless it is valid semantically and not in clue).";

                    var aoaiResp = await _aoai.GetChatCompletionAsync(system, user, temperature: 0.9f, topP: 0.85f);
                    var raw = aoaiResp.Content ?? "[]";
                    _logger.LogInformation("SolveEnds attempt {Attempt}/{MaxAttempts}, raw LLM: {Raw}", attempt, maxAttempts, raw);

                    var candidates = ParseCandidates(raw, request.WordLength, clueWords);

                    // Remove any already previously seen answers (cross-attempt uniqueness)
                    candidates = candidates.Where(c => !previouslySeen.Contains(c.Answer)).ToList();

                    // Track all candidates we received this attempt (even if filtered later) to avoid repeats in future attempts
                    foreach (var c in candidates)
                        previouslySeen.Add(c.Answer);

                    lastCandidates = candidates;
                    _logger.LogInformation("Parsed {Count} NEW candidate answers (attempt {Attempt}); PreviouslySeen total={Seen}", candidates.Count, attempt, previouslySeen.Count);

                    // Filter algorithmically by exact Hamming distance = 1
                    var valid = candidates
                        .Where(c => Words.Hamming(c.Answer, normalizedNeighbor) == 1)
                        .ToList();

                    if (valid.Count > 0)
                    {
                        chosen = valid.First();
                        _logger.LogInformation("Chosen answer '{Answer}' on attempt {Attempt}", chosen.Answer, attempt);
                        break;
                    }

                    _logger.LogWarning("Attempt {Attempt} produced no valid Hamming distance=1 answer. Retrying...", attempt);
                }

                if (chosen == null)
                {
                    req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                    return CorsHelper.CreateResponseWithCors(new {
                        error = "No candidate with Hamming distance = 1 after retries",
                        neighborWord = normalizedNeighbor,
                        attempts = maxAttempts,
                        previouslyTried = previouslySeen.OrderBy(x => x).ToArray(),
                        requestId
                    }, StatusCodes.Status409Conflict, req.HttpContext.Response);
                }

                var result = new
                {
                    clue = request.Clue,
                    answer = chosen.Answer,
                    reasoning = chosen.Reasoning,
                    neighborWord = normalizedNeighbor,
                    hammingDistance = Words.Hamming(chosen.Answer, normalizedNeighbor)
                };

                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateOkResponseWithCors(result, req.HttpContext.Response);
            }
            catch (InvalidOperationException iox)
            {
                _logger.LogError(iox, "AOAI configuration error");
                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateResponseWithCors(new { error = "AOAI configuration error", requestId }, StatusCodes.Status503ServiceUnavailable, req.HttpContext.Response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in SolveEnds");
                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateResponseWithCors(new { error = "Internal server error", requestId }, StatusCodes.Status503ServiceUnavailable, req.HttpContext.Response);
            }
        }

        private sealed class SolveCandidate
        {
            public string Answer { get; set; }
            public string Reasoning { get; set; }
        }

        private List<SolveCandidate> ParseCandidates(string json, int expectedLength, HashSet<string> forbiddenClueWords)
        {
            var list = new List<SolveCandidate>();
            try
            {
                // Attempt to locate JSON array if wrapper text is present
                int firstBracket = json.IndexOf('[');
                int lastBracket = json.LastIndexOf(']');
                if (firstBracket >= 0 && lastBracket > firstBracket)
                {
                    json = json.Substring(firstBracket, lastBracket - firstBracket + 1);
                }

                var token = JToken.Parse(json);
                if (token.Type == JTokenType.Array)
                {
                    foreach (var el in token)
                    {
                        if (el.Type != JTokenType.Object) continue;
                        var answer = el["answer"]?.ToString()?.Trim().ToUpperInvariant();
                        var reasoning = el["reasoning"]?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(answer)) continue;
                        if (answer.Length != expectedLength) continue;
                        if (!answer.All(char.IsLetter)) continue;
                        if (forbiddenClueWords.Contains(answer)) continue; // exclude clue word reuse
                        reasoning ??= "No reasoning";
                        if (list.Any(c => c.Answer == answer)) continue; // dedupe
                        list.Add(new SolveCandidate { Answer = answer, Reasoning = reasoning });
                    }
                }
                else if (token.Type == JTokenType.Object)
                {
                    // Single object fallback
                    var answer = token["answer"]?.ToString()?.Trim().ToUpperInvariant();
                    var reasoning = token["reasoning"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(answer) && answer.Length == expectedLength && answer.All(char.IsLetter) && !forbiddenClueWords.Contains(answer))
                    {
                        list.Add(new SolveCandidate { Answer = answer, Reasoning = reasoning ?? "No reasoning" });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse candidate list JSON");
            }
            return list;
        }
    }
}
