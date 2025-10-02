using System.ComponentModel.DataAnnotations;

namespace CrossclimbBackend.Models
{
    /// <summary>
    /// Request model for Stage A candidate generation
    /// </summary>
    public sealed class CandidateGenerationRequest
    {
        /// <summary>
        /// The exact length of words to generate (N ≥ 3)
        /// </summary>
        [Required]
        [Range(3, int.MaxValue, ErrorMessage = "Word length must be at least 3")]
        public int WordLength { get; set; }

        /// <summary>
        /// Array of clues to generate candidates for (size ≥ 1)
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one clue must be provided")]
        public string[] Clues { get; set; } = Array.Empty<string>();
    }
}