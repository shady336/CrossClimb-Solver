using System.ComponentModel.DataAnnotations;

namespace CrossclimbBackend.Models
{
    public sealed class SolveEndsRequest
    {
        [Required]
        public int WordLength { get; set; }

        [Required]
        public string NeighborWord { get; set; } 

        [Required]
        public string Clue { get; set; }
    }
}