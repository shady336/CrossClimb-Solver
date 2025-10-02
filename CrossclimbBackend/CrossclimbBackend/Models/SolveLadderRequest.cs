using System.ComponentModel.DataAnnotations;

namespace CrossclimbBackend.Models
{
    public sealed class SolveLadderRequest
    {
        [Required]
        public int WordLength { get; set; }

        [Required]
        public List<string> Clues { get; set; } 
    }
}