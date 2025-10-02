using System.Net.Http;
using System.Text;
using System.Text.Json;
using CrossclimbBackend.Core.Models;

namespace CrossclimbBackend.Core.Services
{
    public class AoaiService : IAoaiService
    {
        private readonly HttpClient _http;
        private readonly string _key;
        private readonly string _deployment;

        public AoaiService()
        {
            var endpoint = Environment.GetEnvironmentVariable("AOAI_ENDPOINT") ?? throw new InvalidOperationException("AOAI_ENDPOINT not set");
            _key = Environment.GetEnvironmentVariable("AOAI_API_KEY") ?? throw new InvalidOperationException("AOAI_API_KEY not set");
            _deployment = Environment.GetEnvironmentVariable("AOAI_DEPLOYMENT") ?? throw new InvalidOperationException("AOAI_DEPLOYMENT not set");

            _http = new HttpClient { BaseAddress = new Uri(endpoint) };
        }

        public async Task<AoaiResponse> GetChatCompletionAsync(string systemMessage, string userMessage, float temperature = 0.7f, float topP = 0.95f)
        {
            var body = new
            {
                messages = new[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                temperature,
                top_p = topP
            };
            
            var json = await SendWithRetriesAsync(async () =>
            {
                var r = new HttpRequestMessage(HttpMethod.Post, $"/openai/deployments/{_deployment}/chat/completions?api-version=2025-01-01-preview");
                r.Headers.Add("api-key", _key);
                r.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                return await _http.SendAsync(r);
            });

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            int promptTokens = 0, completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var p)) promptTokens = p.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var c)) completionTokens = c.GetInt32();
            }

            return new AoaiResponse(content);
        }

        private async Task<string> SendWithRetriesAsync(Func<Task<HttpResponseMessage>> sendFunc)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                HttpResponseMessage res = null!;
                try
                {
                    res = await sendFunc();
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt) * 100);
                    await Task.Delay(delayMs);
                    continue;
                }

                var content = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                    return content;

                var status = (int)res.StatusCode;
                if ((status == 429 || (status >= 500 && status <= 599)) && attempt < maxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt) * 200);
                    await Task.Delay(delayMs);
                    continue;
                }

                throw new InvalidOperationException($"AOAI error: {res.StatusCode}: {content}");
            }

            throw new InvalidOperationException("AOAI transient failures after retries");
        }
    }
}