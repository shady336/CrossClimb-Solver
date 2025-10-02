using System.ComponentModel.DataAnnotations;

namespace CrossclimbBackend.Models
{
    public sealed class SolveEndsResponse
    {
        [Required]
        public string Answer { get; set; } 

        [Required]
        public string Reasoning { get; set; } 
    }
}