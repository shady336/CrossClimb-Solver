using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Services;
using CrossclimbBackend.Utils;

namespace CrossclimbBackend.Functions
{
    public class SolveLadderFunction
    {
        private readonly IValidationService _validator;
        private readonly ILadderSolver _solver;
        private readonly ILogger<SolveLadderFunction> _logger;

        public SolveLadderFunction(
            IValidationService validator,
            ILadderSolver solver,
            ILogger<SolveLadderFunction> logger)
        {
            _validator = validator;
            _solver = solver;
            _logger = logger;
        }

        [FunctionName("SolveLadder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "solve/ladder")] HttpRequest req)
        {
            var requestId = Guid.NewGuid().ToString();
            
            try
            {
                // Handle OPTIONS request for CORS preflight
                if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    return CorsHelper.HandleOptionsRequest(req.HttpContext.Response);
                }
                // Parse and validate request
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<SolveLadderRequest>(body);
                
                var validationError = _validator.ValidateRequest(request);
                if (validationError != null)
                {
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = validationError, requestId }, req.HttpContext.Response);
                }

                // Solve ladder
                var (response, aoaiInfo) = await _solver.SolveAsync(request!);

                // Build success response
                var result = new
                {
                    ladder = response.Ladder,
                    pairs = response.Pairs,
                };

                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateOkResponseWithCors(result, req.HttpContext.Response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation or configuration error");
                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateResponseWithCors(new { error = ex.Message, requestId }, StatusCodes.Status409Conflict, req.HttpContext.Response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error");
                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateResponseWithCors(new { error = "Internal server error", requestId }, StatusCodes.Status503ServiceUnavailable, req.HttpContext.Response);
            }
        }
    }
}