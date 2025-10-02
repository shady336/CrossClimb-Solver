using System.Linq;
using CrossclimbBackend.Models;
using Newtonsoft.Json;

namespace CrossclimbBackend.Core.Services
{
    public interface IPromptBuilder
    {
        (string system, string user) BuildPrompts(SolveLadderRequest request);
        string BuildRepairPrompt(string failedResponse, SolveLadderRequest request);
    }

    public class PromptBuilder : IPromptBuilder
    {
        public (string system, string user) BuildPrompts(SolveLadderRequest request)
        {
            var system = $"You are a Cross Climb Puzzle Solver. You receive a list of clues (clues) and an integer (wordLength) representing the length of all words in the puzzle. Your task is to produce a word ladder that solves the puzzle. Follow these rules strictly:\r\n\r\nFor each clue, generate plausible English words of length wordLength. Words must be uppercase A–Z only, with no spaces or punctuation.\r\n\r\nUse the generated words as a pool. Construct a word ladder where:\r\n\r\nLadder length equals the number of clues.\r\n\r\nEach consecutive word differs exactly by one letter (Hamming distance = 1).\r\n\r\nAll words are unique.\r\n\r\nWords can appear in any order, not necessarily matching the original clue order.\r\n\r\nEach clue contributes exactly one word to the ladder; no clue’s candidates appear more than once.\r\n\r\nValidate the ladder: correct length, all words match wordLength, consecutive words differ by one letter, no duplicates, and exactly one word per clue is used.\r\n\r\nIf the ladder is invalid, retry using alternative candidate words. Make a maximum of 5 attempts.\r\n\r\nUse step-by-step reasoning internally, briefly justifying why each word fits its clue and ladder constraints. Prioritize correctness of the ladder over unusual words.\r\n\r\nReturn a JSON object in this format:\r\n\r\n{{\r\n  \"ladder\": [\"WORD1\", \"WORD2\", \"WORD3\", \"...\"],\r\n  \"validated\": true,\r\n  \"attempts\": 1\r\n}}\r\n\r\n\r\nladder: the ordered list of words forming a valid ladder (empty if validation fails).\r\n\r\nvalidated: true if ladder passed validation, false otherwise.\r\n\r\nattempts: number of attempts made (max 5).\r\n\r\nAll candidate generation and ladder construction must rely solely on your internal knowledge; do not use external APIs. Retry intelligently with alternative valid English words if the first attempt fails. Ensure consecutive words always differ by exactly one letter and exactly one word from each clue is used in the ladder.";

            var userPrompt = JsonConvert.SerializeObject(new
            {
                wordLength = request.WordLength,
                attempts = 10,
                clues = request.Clues.Select((c, i) => new { index = i, text = c })
            });

            return (system, userPrompt);
        }

        public string BuildRepairPrompt(string failedResponse, SolveLadderRequest request)
        {
            return JsonConvert.SerializeObject(new 
            { 
                error = "Fix this invalid JSON to match the expected format and constraints.",
                failed = failedResponse, 
                constraints = new {
                    wordCount = request.Clues.Count,
                    wordLength = request.WordLength,
                    clues = request.Clues
                }
            });
        }
    }
}