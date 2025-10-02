using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using CrossclimbBackend.Core.Services;

[assembly: FunctionsStartup(typeof(CrossclimbBackend.Startup))]

namespace CrossclimbBackend
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Core services - minimal implementation
            builder.Services.AddSingleton<IAoaiService, AoaiService>();
            builder.Services.AddScoped<IValidationService, ValidationService>();
            builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
            // Replace existing ladder solver with algorithmic implementation
            // Uses LLM for candidate generation and algorithmic backtracking for ladder construction
            builder.Services.AddScoped<ILadderSolver, AlgorithmicLadderSolver>();

            // Stage A Candidate Generation services
            builder.Services.AddScoped<ICandidateGenerationService, CandidateGenerationService>();
            builder.Services.AddScoped<ICandidatePromptBuilder, CandidatePromptBuilder>();
            builder.Services.AddScoped<ICandidateWordCleaner, CandidateWordCleaner>();

            // Use Stage A + Stage B based ladder solver
            //builder.Services.AddScoped<ILadderSolver, StageBasedLadderSolver>();
        }
    }
}