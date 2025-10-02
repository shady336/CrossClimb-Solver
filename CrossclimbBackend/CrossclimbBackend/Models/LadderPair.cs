using System.ComponentModel.DataAnnotations;

namespace CrossclimbBackend.Models
{
    public sealed class LadderPair
    {
        [Required]
        public string Word { get; set; } 

        [Required]
        public string Clue { get; set; } 

        [Required]
        public string Reasoning { get; set; } 
    }
}