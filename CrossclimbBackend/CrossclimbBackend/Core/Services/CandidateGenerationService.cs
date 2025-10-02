using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Models;
using System.Text.Json;

namespace CrossclimbBackend.Core.Services
{
    public class CandidateGenerationService : ICandidateGenerationService
    {
        private readonly IAoaiService _aoaiService;
        private readonly ICandidatePromptBuilder _promptBuilder;
        private readonly ICandidateWordCleaner _wordCleaner;

        public CandidateGenerationService(
            IAoaiService aoaiService,
            ICandidatePromptBuilder promptBuilder,
            ICandidateWordCleaner wordCleaner)
        {
            _aoaiService = aoaiService;
            _promptBuilder = promptBuilder;
            _wordCleaner = wordCleaner;
        }

        public async Task<CandidateGenerationResponse> GenerateCandidatesAsync(
            CandidateGenerationRequest request)
        {
            var (systemMessage, userMessage) = _promptBuilder.BuildPrompts(request);

            var aoaiResponse = await _aoaiService.GetChatCompletionAsync(
                systemMessage,
                userMessage,
                CandidateGenerationDefaults.Temperature,
                topP: 0.95f);

            var parsedResponse = JsonSerializer.Deserialize<CandidateGenerationResponse>(aoaiResponse.Content);

            if (parsedResponse == null)
            {
                throw new InvalidOperationException();
            }

            return _wordCleaner.CleanInvalidWords(parsedResponse);
        }
    }
}