using CrossclimbBackend.Core.Models;

namespace CrossclimbBackend.Core.Services
{
    public interface IAoaiService
    {
        Task<AoaiResponse> GetChatCompletionAsync(string systemMessage, string userMessage, float temperature = 0.2f, float topP = 0.3f);
    }
}