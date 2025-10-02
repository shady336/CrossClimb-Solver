using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CrossclimbBackend.Utils
{
    public static class CorsHelper
    {
        /// <summary>
        /// Adds CORS headers to the HTTP response
        /// </summary>
        /// <param name="response">The HTTP response to add headers to</param>
        public static void AddCorsHeaders(HttpResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            response.Headers.Add("Access-Control-Max-Age", "86400"); // 24 hours
        }

        /// <summary>
        /// Creates an OkObjectResult with CORS headers
        /// </summary>
        /// <param name="value">The response object</param>
        /// <param name="response">The HTTP response to add headers to</param>
        /// <returns>OkObjectResult with CORS headers added</returns>
        public static IActionResult CreateOkResponseWithCors(object value, HttpResponse response)
        {
            AddCorsHeaders(response);
            return new OkObjectResult(value);
        }

        /// <summary>
        /// Creates a BadRequestObjectResult with CORS headers
        /// </summary>
        /// <param name="value">The error response object</param>
        /// <param name="response">The HTTP response to add headers to</param>
        /// <returns>BadRequestObjectResult with CORS headers added</returns>
        public static IActionResult CreateBadRequestResponseWithCors(object value, HttpResponse response)
        {
            AddCorsHeaders(response);
            return new BadRequestObjectResult(value);
        }

        /// <summary>
        /// Creates an ObjectResult with specified status code and CORS headers
        /// </summary>
        /// <param name="value">The response object</param>
        /// <param name="statusCode">The HTTP status code</param>
        /// <param name="response">The HTTP response to add headers to</param>
        /// <returns>ObjectResult with CORS headers added</returns>
        public static IActionResult CreateResponseWithCors(object value, int statusCode, HttpResponse response)
        {
            AddCorsHeaders(response);
            return new ObjectResult(value) { StatusCode = statusCode };
        }

        /// <summary>
        /// Handles OPTIONS preflight request
        /// </summary>
        /// <param name="response">The HTTP response</param>
        /// <returns>OkResult with CORS headers</returns>
        public static IActionResult HandleOptionsRequest(HttpResponse response)
        {
            AddCorsHeaders(response);
            return new OkResult();
        }
    }
}