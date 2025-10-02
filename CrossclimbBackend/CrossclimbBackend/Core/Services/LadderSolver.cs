using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Models;

namespace CrossclimbBackend.Core.Services
{

    public class LadderSolver : ILadderSolver
    {
        private readonly IAoaiService _aoai;
        private readonly IPromptBuilder _promptBuilder;
        private readonly IValidationService _validator;
        private readonly ILogger<LadderSolver> _logger;

        public LadderSolver(
            IAoaiService aoai,
            IPromptBuilder promptBuilder,
            IValidationService validator,
            ILogger<LadderSolver> logger)
        {
            _aoai = aoai;
            _promptBuilder = promptBuilder;
            _validator = validator;
            _logger = logger;
        }

        public async Task<(SolveLadderResponse response, AoaiResponse aoaiInfo)> SolveAsync(SolveLadderRequest request)
        {
            var (systemPrompt, userPrompt) = _promptBuilder.BuildPrompts(request);
            var aoaiResponse = await _aoai.GetChatCompletionAsync(systemPrompt, userPrompt, 0.7f, 0.95f);

            SolveLadderResponse? response;
            try
            {
                response = JsonConvert.DeserializeObject<SolveLadderResponse>(aoaiResponse.Content);
                if (response == null) throw new JsonException("Null response");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse response, attempting repair");
                response = await AttemptRepairAsync(aoaiResponse.Content, request);
            }

            var validationError = _validator.ValidateResponse(response, request);
            if (validationError != null)
                throw new InvalidOperationException($"Response validation failed: {validationError}");

            return (response, aoaiResponse);
        }

        private async Task<SolveLadderResponse> AttemptRepairAsync(string failedResponse, SolveLadderRequest request)
        {
            var repairSystem = "Fix the JSON to match expected format and constraints. Output JSON only.";
            var repairUser = _promptBuilder.BuildRepairPrompt(failedResponse, request);
            
            var repairResponse = await _aoai.GetChatCompletionAsync(repairSystem, repairUser, 0.0f, 0.0f);
            var repaired = JsonConvert.DeserializeObject<SolveLadderResponse>(repairResponse.Content);
            
            return repaired ?? throw new JsonException("Repair attempt failed");
        }
    }
}