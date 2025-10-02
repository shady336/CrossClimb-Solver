using CrossclimbBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrossclimbBackend.Core.Services
{
    public class CandidateValidationResult
    {
        public bool IsValid { get; set; }
        public CandidateGenerationResponse? ValidatedResponse { get; set; }
        public List<ValidationViolation> Violations { get; set; } = new();
    }

    public interface ICandidateValidator
    {
        CandidateValidationResult ValidateResponse(
            string rawResponse, 
            CandidateGenerationRequest originalRequest);

        bool IsValidWord(string word, int expectedLength);
    }

    public class CandidateValidator : ICandidateValidator
    {
        public CandidateValidationResult ValidateResponse(
            string rawResponse, 
            CandidateGenerationRequest originalRequest)
        {
            var result = new CandidateValidationResult();

            CandidateGenerationResponse? parsedResponse;
            try
            {
                parsedResponse = JsonSerializer.Deserialize<CandidateGenerationResponse>(rawResponse);
                if (parsedResponse == null)
                {
                    result.Violations.Add(new ValidationViolation
                    {
                        ClueIndex = -1,
                        Reason = "Failed to parse JSON response"
                    });
                    return result;
                }
            }
            catch (JsonException ex)
            {
                result.Violations.Add(new ValidationViolation
                {
                    ClueIndex = -1,
                    Reason = $"JSON parsing error: {ex.Message}"
                });
                return result;
            }

            // Perform structural checks; if critical structural mismatches are present, abort.
            var schemaViolations = ValidateSchema(parsedResponse, originalRequest);
            result.Violations.AddRange(schemaViolations);

            if (parsedResponse.WordLength != originalRequest.WordLength || parsedResponse.Items.Length != originalRequest.Clues.Length)
            {
                // Cannot reliably map response to request clues -> treat as fatal
                result.IsValid = false;
                result.ValidatedResponse = null;
                return result;
            }

            // Normalize words and remove any candidates that don't meet word-level constraints.
            var normalizedResponse = RemoveInvalidWords(parsedResponse);

            var dedupedResponse = DeduplicateWithinClues(normalizedResponse);

            // Check for minimum viable output and record as informational violations but do not block.
            var viabilityViolations = CheckMinimumViableOutput(dedupedResponse);
            result.Violations.AddRange(viabilityViolations);

            // Instead of rejecting the whole response due to per-candidate issues, return the cleaned response.
            result.IsValid = true;
            result.ValidatedResponse = dedupedResponse;

            return result;
        }

        public bool IsValidWord(string word, int expectedLength)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            if (word.Length != expectedLength)
                return false;

            if (CandidateGenerationDefaults.StrictRegex)
            {
                var pattern = $"^[A-Z]{{{expectedLength}}}$";
                return Regex.IsMatch(word, pattern);
            }

            var normalized = word.Trim().ToUpperInvariant();
            var pattern2 = $"^[A-Z]{{{expectedLength}}}$";
            return Regex.IsMatch(normalized, pattern2);
        }

        private List<ValidationViolation> ValidateSchema(
            CandidateGenerationResponse response, 
            CandidateGenerationRequest originalRequest)
        {
            var violations = new List<ValidationViolation>();

            if (response.WordLength != originalRequest.WordLength)
            {
                violations.Add(new ValidationViolation
                {
                    ClueIndex = -1,
                    Reason = $"WordLength mismatch: expected {originalRequest.WordLength}, got {response.WordLength}"
                });
            }

            if (response.Items.Length != originalRequest.Clues.Length)
            {
                violations.Add(new ValidationViolation
                {
                    ClueIndex = -1,
                    Reason = $"Items count mismatch: expected {originalRequest.Clues.Length}, got {response.Items.Length}"
                });
            }

            for (int i = 0; i < Math.Min(response.Items.Length, originalRequest.Clues.Length); i++)
            {
                var item = response.Items[i];
                
                if (item.Candidates.Length < CandidateGenerationDefaults.CandidatesMin)
                {
                    violations.Add(new ValidationViolation
                    {
                        ClueIndex = i,
                        Reason = $"Too few candidates: {item.Candidates.Length} (minimum {CandidateGenerationDefaults.CandidatesMin})"
                    });
                }
                else if (item.Candidates.Length > CandidateGenerationDefaults.CandidatesMax)
                {
                    violations.Add(new ValidationViolation
                    {
                        ClueIndex = i,
                        Reason = $"Too many candidates: {item.Candidates.Length} (maximum {CandidateGenerationDefaults.CandidatesMax})"
                    });
                }

                foreach (var candidate in item.Candidates)
                {
                    if (candidate.Reason.Length > CandidateGenerationDefaults.ReasonsMaxLength)
                    {
                        violations.Add(new ValidationViolation
                        {
                            ClueIndex = i,
                            Reason = $"Reason too long: {candidate.Reason.Length} chars (max {CandidateGenerationDefaults.ReasonsMaxLength})"
                        });
                    }
                }
            }

            return violations;
        }

        private CandidateGenerationResponse RemoveInvalidWords(
            CandidateGenerationResponse response)
        {
            var normalized = new CandidateGenerationResponse
            {
                WordLength = response.WordLength,
                Items = new ClueWithCandidates[response.Items.Length]
            };

            for (int i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                var normalizedCandidates = new List<Candidate>();

                foreach (var candidate in item.Candidates)
                {
                    var normalizedWord = candidate.Word.Trim().ToUpperInvariant();

                    if (IsValidWord(normalizedWord, response.WordLength))
                    {
                        normalizedCandidates.Add(new Candidate
                        {
                            Word = normalizedWord,
                            Reason = candidate.Reason.Trim()
                        });
                    }
                    else
                    {
                        // Simply skip invalid words; do not record violations here.
                    }
                }

                normalized.Items[i] = new ClueWithCandidates
                {
                    Clue = item.Clue,
                    Candidates = normalizedCandidates.ToArray()
                };
            }

            return normalized;
        }

        private CandidateGenerationResponse DeduplicateWithinClues(CandidateGenerationResponse response)
        {
            var deduplicated = new CandidateGenerationResponse
            {
                WordLength = response.WordLength,
                Items = new ClueWithCandidates[response.Items.Length]
            };

            for (int i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                var seenWords = new HashSet<string>();
                var uniqueCandidates = new List<Candidate>();

                foreach (var candidate in item.Candidates)
                {
                    if (seenWords.Add(candidate.Word))
                    {
                        uniqueCandidates.Add(candidate);
                    }
                }

                deduplicated.Items[i] = new ClueWithCandidates
                {
                    Clue = item.Clue,
                    Candidates = uniqueCandidates.ToArray()
                };
            }

            return deduplicated;
        }

        private List<ValidationViolation> CheckMinimumViableOutput(
            CandidateGenerationResponse response)
        {
            var violations = new List<ValidationViolation>();

            for (int i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                if (item.Candidates.Length < CandidateGenerationDefaults.CandidatesMin)
                {
                    violations.Add(new ValidationViolation
                    {
                        ClueIndex = i,
                        Reason = $"Only {item.Candidates.Length} valid candidates after filtering (minimum {CandidateGenerationDefaults.CandidatesMin})"
                    });
                }
            }

            return violations;
        }
    }
}