using System;
using System.Linq;
using CrossclimbBackend.Models;
using CrossclimbBackend.Utils;

namespace CrossclimbBackend.Core.Services
{
    public interface IValidationService
    {
        string? ValidateRequest(SolveLadderRequest? request);
        string? ValidateResponse(SolveLadderResponse response, SolveLadderRequest request);
    }

    public class ValidationService : IValidationService
    {
        public string? ValidateRequest(SolveLadderRequest? request)
        {
            if (request == null)
                return "Invalid JSON body";

            if (request.WordLength <= 0)
                return "wordLength must be > 0";

            if (request.Clues == null || request.Clues.Count == 0)
                return "At least one clue is required";

            if (request.Clues.Any(c => string.IsNullOrWhiteSpace(c)))
                return "Each clue must be a non-empty string";

            if (request.Clues.Any(c => c.Length > 140))
                return "Each clue must have length <= 140 characters";

            return null; // Valid
        }

        public string? ValidateResponse(SolveLadderResponse response, SolveLadderRequest request)
        {
            if (response?.Ladder == null || response.Pairs == null)
                return "Invalid response structure";

            // Normalize case
            for (int i = 0; i < response.Ladder.Length; i++)
                response.Ladder[i] = response.Ladder[i].ToUpperInvariant();
            
            foreach (var pair in response.Pairs)
                pair.Word = pair.Word.ToUpperInvariant();

            // Check counts
            if (response.Ladder.Length != request.Clues.Count)
                return "Ladder length must equal number of clues";

            if (response.Pairs.Count != request.Clues.Count)
                return "Pairs count must equal clues count";

            // Check Hamming distance
            for (int i = 0; i < response.Ladder.Length - 1; i++)
            {
                if (Words.Hamming(response.Ladder[i], response.Ladder[i + 1]) != 1)
                    return "Adjacent words must differ by exactly 1 letter";
            }

            // Check uniqueness
            if (response.Ladder.Distinct().Count() != response.Ladder.Length)
                return "Duplicate words in ladder";

            // Validate pairs
            foreach (var pair in response.Pairs)
            {
                if (!response.Ladder.Contains(pair.Word))
                    return $"Pair word {pair.Word} not in ladder";

                if (!request.Clues.Any(c => string.Equals(c, pair.Clue, StringComparison.OrdinalIgnoreCase)))
                    return $"Pair clue '{pair.Clue}' doesn't match input";

                if (string.IsNullOrWhiteSpace(pair.Reasoning))
                    return "Each pair must include reasoning";
            }

            return null; // Valid
        }
    }
}