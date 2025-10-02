using CrossclimbBackend.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrossclimbBackend.Core.Services
{
    public class CandidateWordCleaner : ICandidateWordCleaner
    {
        public CandidateGenerationResponse CleanInvalidWords(
            CandidateGenerationResponse parsedResponse)
        {
            var cleanedResponse = RemoveInvalidWords(parsedResponse);
            var dedupedResponse = DeduplicateWithinClues(cleanedResponse);

            return dedupedResponse;
        }

        private CandidateGenerationResponse RemoveInvalidWords(
            CandidateGenerationResponse response)
        {
            var cleaned = new CandidateGenerationResponse
            {
                WordLength = response.WordLength,
                Items = new ClueWithCandidates[response.Items.Length]
            };

            for (int i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                var validCandidates = new List<Candidate>();

                foreach (var candidate in item.Candidates)
                {
                    var normalizedWord = candidate.Word.Trim().ToUpperInvariant();

                    if (IsValidWord(normalizedWord, response.WordLength))
                    {
                        validCandidates.Add(new Candidate
                        {
                            Word = normalizedWord,
                            Reason = candidate.Reason.Trim()
                        });
                    }
                }

                cleaned.Items[i] = new ClueWithCandidates
                {
                    Clue = item.Clue,
                    Candidates = validCandidates.ToArray()
                };
            }

            return cleaned;
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

        private bool IsValidWord(string word, int expectedLength)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            if (word.Length != expectedLength)
                return false;

            var pattern = $"^[A-Z]{{{expectedLength}}}$";
            return Regex.IsMatch(word, pattern);
        }
    }
}