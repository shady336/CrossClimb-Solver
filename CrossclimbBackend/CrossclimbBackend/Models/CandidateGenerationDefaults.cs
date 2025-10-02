namespace CrossclimbBackend.Models
{
    public static class CandidateGenerationDefaults
    {
        public const float Temperature = 0.7f;
        public const int MaxTokens = 4000;
        public const bool UseJsonMode = true;
        public const int TimeoutMs = 30000;
        public const int MaxRetryAttempts = 3;
        public const int RetryBaseDelayMs = 1000;
        public const bool StrictRegex = true;
        public const int ReasonsMaxLength = 80;
        public const int CandidatesMin = 3;
        public const int CandidatesMax = 6;
    }
}
