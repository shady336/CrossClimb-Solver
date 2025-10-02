using CrossclimbBackend.Models;

namespace CrossclimbBackend.Core.Services
{
    public interface ICandidateWordCleaner
    {
        CandidateGenerationResponse CleanInvalidWords(
            CandidateGenerationResponse parsedResponse);
    }
}