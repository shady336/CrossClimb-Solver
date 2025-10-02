using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CrossclimbBackend.Models
{
    public sealed class SolveLadderResponse
    {
        [Required]
        public string[] Ladder { get; set; } 

        [Required]
        public List<LadderPair> Pairs { get; set; } 
    }
}