namespace CrossclimbBackend.Models
{
    /// <summary>
    /// Represents a validation violation for a specific clue
    /// </summary>
    public sealed class ValidationViolation
    {
        /// <summary>
        /// Zero-based index of the clue that had validation issues
        /// </summary>
        public int ClueIndex { get; set; }

        /// <summary>
        /// Description of the validation failure
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error response for validation failures (422 Unprocessable Entity)
    /// </summary>
    public sealed class ValidationErrorResponse
    {
        /// <summary>
        /// Status indicator
        /// </summary>
        public string Status { get; set; } = "error";

        /// <summary>
        /// Error code
        /// </summary>
        public string Code { get; set; } = "VALIDATION_FAILED";

        /// <summary>
        /// Detailed validation error information
        /// </summary>
        public ValidationErrorDetails Details { get; set; } = new();
    }

    /// <summary>
    /// Details about validation failures
    /// </summary>
    public sealed class ValidationErrorDetails
    {
        /// <summary>
        /// The word length that was requested
        /// </summary>
        public int WordLength { get; set; }

        /// <summary>
        /// List of validation violations per clue
        /// </summary>
        public ValidationViolation[] Violations { get; set; } = Array.Empty<ValidationViolation>();
    }

    /// <summary>
    /// Generic error response for other error types
    /// </summary>
    public sealed class ErrorResponse
    {
        /// <summary>
        /// Status indicator
        /// </summary>
        public string Status { get; set; } = "error";

        /// <summary>
        /// Error code
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Request ID for tracing
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the request can be safely retried
        /// </summary>
        public bool Retryable { get; set; } = false;
    }
}