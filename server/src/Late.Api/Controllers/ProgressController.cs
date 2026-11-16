using Late.Api.Auth;
using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Late.Api.Controllers;

/// <summary>
/// Progreso de la cuenta: el nivel desbloqueado y los mejores tiempos. No se guarda aparte: se calcula
/// a partir de las partidas (ganar un nivel desbloquea el siguiente), así nunca puede contradecirlas.
/// </summary>
[ApiController]
[Route("api/progress")]
[Authorize]
public class ProgressController(LateDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ProgressResponse> Get()
    {
        int userId = User.UserId();

        var bestByLevel = await db.Runs
            .Where(r => r.UserId == userId && r.BeatTheMusic)
            .GroupBy(r => r.Level!.Order)
            .Select(g => new { LevelOrder = g.Key, Best = g.Min(r => r.TimeSeconds) })
            .OrderBy(x => x.LevelOrder)
            .ToListAsync();

        return new ProgressResponse
        {
            HighestUnlockedLevel = bestByLevel.Count == 0 ? 0 : bestByLevel.Max(x => x.LevelOrder) + 1,
            Levels = bestByLevel
                .Select(x => new LevelProgress { LevelOrder = x.LevelOrder, BestSeconds = (double)x.Best })
                .ToList(),
        };
    }
}
