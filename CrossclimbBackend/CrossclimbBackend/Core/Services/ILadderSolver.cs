using CrossclimbBackend.Models;
using CrossclimbBackend.Core.Models;

namespace CrossclimbBackend.Core.Services
{
    public interface ILadderSolver
    {
        Task<(SolveLadderResponse response, AoaiResponse aoaiInfo)> SolveAsync(SolveLadderRequest request);
    }
}