using CrossclimbBackend.Models;

namespace CrossclimbBackend.Core.Services
{
    /// <summary>
    /// Stage A candidate generation service interface
    /// Generates 3-6 candidate words per clue according to the design document specifications
    /// </summary>
    public interface ICandidateGenerationService
    {
        /// <summary>
        /// Generates candidate words for the provided clues
        /// </summary>
        /// <param name="request">Request containing word length and clues</param>
        /// <returns>Response containing candidate words for each clue</returns>
        /// <exception cref="ArgumentException">Thrown when request validation fails</exception>
        /// <exception cref="InvalidOperationException">Thrown when LLM response validation fails after retries</exception>
        /// <exception cref="TimeoutException">Thrown when request exceeds timeout limits</exception>
        /// <exception cref="HttpRequestException">Thrown when AOAI service is unavailable</exception>
        Task<CandidateGenerationResponse> GenerateCandidatesAsync(
            CandidateGenerationRequest request);
    }
}