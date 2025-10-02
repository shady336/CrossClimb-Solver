using System;
using System.IO;
using System.Linq;
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
    public class TestFunction
    {
        private readonly ILogger<TestFunction> _logger;

        public TestFunction(ILogger<TestFunction> logger)
        {
            _logger = logger;
        }

        [FunctionName("Test")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "test")] HttpRequest req)
        {
            var requestId = Guid.NewGuid().ToString();
            
            try
            {
                // Handle OPTIONS request for CORS preflight
                if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    return CorsHelper.HandleOptionsRequest(req.HttpContext.Response);
                }

                // Handle GET request - return health check
                if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    var healthResponse = new
                    {
                        status = "healthy",
                        message = "Test endpoint is running",
                        timestamp = DateTime.UtcNow,
                        requestId = requestId
                    };

                    req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                    return CorsHelper.CreateOkResponseWithCors(healthResponse, req.HttpContext.Response);
                }

                // Handle POST request - parse SolveLadderRequest and return dummy response
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<SolveLadderRequest>(body);

                if (request == null)
                {
                    return CorsHelper.CreateBadRequestResponseWithCors(new { error = "Invalid JSON body", requestId }, req.HttpContext.Response);
                }

                // Return dummy success response
                var dummyResponse = new
                {
                    status = "success",
                    message = "Success and here is the request you just sent",
                    receivedRequest = new
                    {
                        wordLength = request.WordLength,
                        clues = request.Clues,
                        numberOfClues = request.Clues?.Count ?? 0
                    },
                    dummyLadder = GenerateDummyLadder(request),
                    requestId = requestId
                };

                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateOkResponseWithCors(dummyResponse, req.HttpContext.Response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test function failed");
                req.HttpContext.Response.Headers.Add("X-Request-Id", requestId);
                return CorsHelper.CreateResponseWithCors(new
                {
                    status = "error",
                    message = ex.Message,
                    requestId = requestId
                }, StatusCodes.Status500InternalServerError, req.HttpContext.Response);
            }
        }

        private object GenerateDummyLadder(SolveLadderRequest request)
        {
            if (request.Clues == null || request.Clues.Count == 0)
            {
                return new { ladder = new string[0], pairs = new object[0] };
            }

            // Generate dummy words of the correct length
            var dummyWords = new string[request.Clues.Count];
            for (int i = 0; i < request.Clues.Count; i++)
            {
                dummyWords[i] = new string('A', request.WordLength);
            }

            var dummyPairs = request.Clues.Select((clue, index) => new
            {
                word = dummyWords[index],
                clue = clue,
                reasoning = $"Dummy reasoning for clue {index + 1}"
            }).ToArray();

            return new
            {
                ladder = dummyWords,
                pairs = dummyPairs
            };
        }
    }
}