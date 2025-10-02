using System;
using System.Collections.Generic;
using System.Linq;
using CrossclimbBackend.Core.Models;
using CrossclimbBackend.Models;

namespace CrossclimbBackend.Core.Services
{
    /// <summary>
    /// Stage-based solver:
    /// - Stage A: produce candidates per clue (handled by ICandidateGenerationService)
    /// - Stage B: order picks so that adjacent words differ by exactly 1 letter
    /// </summary>
    public sealed class StageBasedLadderSolver : ILadderSolver
    {
        private readonly ICandidateGenerationService _candidateService;

        public StageBasedLadderSolver(ICandidateGenerationService candidateService)
        {
            _candidateService = candidateService ?? throw new ArgumentNullException(nameof(candidateService));
        }

        public async Task<(SolveLadderResponse response, AoaiResponse aoaiInfo)> SolveAsync(SolveLadderRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (request.WordLength <= 0) throw new ArgumentOutOfRangeException(nameof(request.WordLength));

            var candidateRequest = new CandidateGenerationRequest
            {
                WordLength = request.WordLength,
                Clues = request.Clues.ToArray()
            };

            var candidatesResponse = await _candidateService.GenerateCandidatesAsync(candidateRequest).ConfigureAwait(false);

            var (ladder, pairs) = BuildLadderFromCandidates(candidatesResponse, request.Clues);

            var response = new SolveLadderResponse
            {
                Ladder = ladder,
                Pairs = pairs
            };

            var aoaiInfo = new AoaiResponse(
                Content: "Generated via Stage A + Stage B approach"
            );

            return (response, aoaiInfo);
        }

        /// <summary>
        /// Build a valid ladder by choosing exactly one candidate per clue such that
        /// each consecutive pair has Hamming distance 1. Uses DFS with memoization.
        /// </summary>
        private static (string[] ladder, List<LadderPair> pairs) BuildLadderFromCandidates(
            CandidateGenerationResponse candidates,
            IReadOnlyList<string> originalClues)
        {
            // ---- Validation ----
            if (candidates is null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Items is null) throw new ArgumentException("Candidates.Items is null.", nameof(candidates));
            if (originalClues is null) throw new ArgumentNullException(nameof(originalClues));

            var n = candidates.Items.Length;
            if (n == 0) throw new InvalidOperationException("No clues/candidates were provided.");
            if (originalClues.Count != n)
            {
                throw new ArgumentException(
                    $"originalClues count ({originalClues.Count}) does not match candidates.Items length ({n}).",
                    nameof(originalClues));
            }

            var expectedLen = candidates.WordLength;
            if (expectedLen <= 0)
                throw new ArgumentException("CandidateGenerationResponse.WordLength must be positive.", nameof(candidates));

            // Normalize and pre-validate each item
            for (int i = 0; i < n; i++)
            {
                var item = candidates.Items[i];
                if (item is null) throw new ArgumentException($"candidates.Items[{i}] is null.", nameof(candidates));
                if (item.Candidates is null) throw new ArgumentException($"candidates.Items[{i}].Candidates is null.", nameof(candidates));
                if (item.Candidates.Length == 0)
                    throw new InvalidOperationException($"No valid candidates found for clue at index {i}: '{item.Clue}'");

                // Ensure word lengths match expected length
                foreach (var cand in item.Candidates)
                {
                    if (cand?.Word is null)
                        throw new ArgumentException($"Null candidate or null word at clue index {i}.", nameof(candidates));
                    if (cand.Word.Length != expectedLen)
                        throw new ArgumentException(
                            $"Candidate '{cand.Word}' at clue index {i} has length {cand.Word.Length}, expected {expectedLen}.",
                            nameof(candidates));
                }
            }

            // ---- Search for a consistent path ----
            // Preserve Stage A order (first is best), but we will try alternates as needed.
            // Memoization key: (index, previousWordUpper)
            var memo = new HashSet<(int index, string prevUpper)>(capacity: n * 32);

            var chosen = new Candidate[n];
            bool TryBuild(int index, string? prevWord)
            {
                if (index == n) return true;

                var prevKey = prevWord is null ? string.Empty : prevWord.ToUpperInvariant();
                var state = (index, prevKey);
                if (memo.Contains(state)) return false;

                var item = candidates.Items[index];

                IEnumerable<Candidate> sequence;
                if (index == 0 || prevWord is null)
                {
                    sequence = item.Candidates;
                }
                else
                {
                    var adjacent = new List<Candidate>(item.Candidates.Length);
                    var nonAdjacent = new List<Candidate>(item.Candidates.Length);
                    foreach (var c in item.Candidates)
                    {
                        if (AreAdjacent(prevWord, c.Word)) adjacent.Add(c);
                        else nonAdjacent.Add(c);
                    }
                    sequence = adjacent.Concat(nonAdjacent);
                }

                foreach (var cand in sequence)
                {
                    if (index > 0 && prevWord is not null && !AreAdjacent(prevWord, cand.Word))
                    {
                        // keep non-adjacent as a last resort (already sequenced last)
                    }

                    chosen[index] = cand;
                    if (TryBuild(index + 1, cand.Word)) return true;
                }

                memo.Add(state);
                return false;
            }

            var success = TryBuild(0, null);
            if (!success)
            {
                var details = string.Join(", ", candidates.Items.Select((it, i) => $"[{i}] {it.Clue} ({it.Candidates.Length} candidates)"));
                throw new InvalidOperationException(
                    "Could not construct a valid ladder where each adjacent pair differs by exactly one letter. " +
                    "Check candidate quality or reduce ambiguity. Clues/candidate counts: " + details);
            }

            // ---- NEW: reorder the selected words into a single-letter-change chain ----
            // chosen[i] corresponds to originalClues[i]
            var order = OrderAsLadder(chosen.Select(c => c.Word).ToArray());

            var orderedLadder = order.Select(i => chosen[i].Word).ToArray();
            var orderedPairs = new List<LadderPair>(n);
            foreach (var idx in order)
            {
                orderedPairs.Add(new LadderPair
                {
                    Word = chosen[idx].Word,
                    Clue = originalClues[idx],
                    Reasoning = chosen[idx].Reason
                });
            }

            return (orderedLadder, orderedPairs);
        }

        /// <summary>
        /// Given N words (one chosen per clue), return an index order that forms a ladder
        /// where each consecutive pair differs by exactly one letter.
        /// Uses DFS with simple degree-based heuristics to find a Hamiltonian path.
        /// Throws if no such ordering exists (should not happen if the picked sequence was valid).
        /// </summary>
        private static int[] OrderAsLadder(IReadOnlyList<string> words)
        {
            int n = words.Count;
            if (n == 0) return Array.Empty<int>();
            if (n == 1) return new[] { 0 };

            // Build adjacency matrix
            var adj = new bool[n, n];
            var degree = new int[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool a = AreAdjacent(words[i], words[j]);
                    adj[i, j] = adj[j, i] = a;
                    if (a) { degree[i]++; degree[j]++; }
                }
            }

            // Candidate start nodes: prefer endpoints (degree == 1), then others
            var starts = Enumerable.Range(0, n)
                .OrderBy(i => degree[i])  // endpoints first
                .ToArray();

            var path = new int[n];
            var visited = new bool[n];

            bool Dfs(int pos)
            {
                if (pos == n) return true;
                int last = path[pos - 1];

                // Try neighbors first, ordered by ascending degree (reduces branching)
                var nexts = Enumerable.Range(0, n)
                    .Where(v => !visited[v] && adj[last, v])
                    .OrderBy(v => degree[v]);

                foreach (var v in nexts)
                {
                    visited[v] = true;
                    path[pos] = v;
                    if (Dfs(pos + 1)) return true;
                    visited[v] = false;
                }
                return false;
            }

            foreach (var s in starts)
            {
                Array.Fill(visited, false);
                path[0] = s;
                visited[s] = true;
                if (Dfs(1)) return path;
            }

            // If we get here, no Hamiltonian path exists across the chosen words.
            // Fall back to original order if (unexpectedly) needed.
            throw new InvalidOperationException("Could not reorder words into a single-letter-change ladder.");
        }

        /// <summary>
        /// Returns true if words differ by exactly one character (Hamming distance == 1), case-insensitive.
        /// </summary>
        private static bool AreAdjacent(string word1, string word2)
        {
            if (word1 is null || word2 is null) return false;
            if (word1.Length != word2.Length) return false;

            int differences = 0;
            for (int i = 0; i < word1.Length; i++)
            {
                var a = char.ToUpperInvariant(word1[i]);
                var b = char.ToUpperInvariant(word2[i]);
                if (a != b)
                {
                    differences++;
                    if (differences > 1) return false;
                }
            }
            return differences == 1;
        }
    }
}