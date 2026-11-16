using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Late.Api.Controllers;

/// <summary>Niveles y ranking por nivel (R06). Público: no hace falta cuenta para consultarlo.</summary>
[ApiController]
[Route("api/levels")]
public class LevelsController(LateDbContext db) : ControllerBase
{
    public const int DefaultTop = 10;
    public const int MaxTop = 100;

    [HttpGet]
    public async Task<List<LevelDto>> GetLevels() =>
        await db.Levels.OrderBy(l => l.Order)
            .Select(l => new LevelDto { Order = l.Order, Name = l.Name })
            .ToListAsync();

    /// <summary>
    /// Los mejores jugadores de un nivel: el mejor tiempo de cada uno ganando a la música.
    /// A igual tiempo va delante quien lo consiguió antes.
    /// </summary>
    [HttpGet("{order:int}/ranking")]
    [ProducesResponseType<RankingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRanking(int order, [FromQuery] int top = DefaultTop)
    {
        Level? level = await db.Levels.FirstOrDefaultAsync(l => l.Order == order);
        if (level == null) return Problem(statusCode: StatusCodes.Status404NotFound, title: $"No existe el nivel {order}.");
        top = Math.Clamp(top, 1, MaxTop);

        IQueryable<Run> wins = db.Runs.Where(r => r.LevelId == level.Id && r.BeatTheMusic);

        // 1. Mejor tiempo de cada jugador.
        var bestPerUser = wins
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Best = g.Min(r => r.TimeSeconds) });

        // 2. Solo interesan los que pueden entrar en el top: hasta el tiempo del puesto «top»,
        //    empates incluidos (el desempate por fecha se hace después).
        decimal? cutoff = await bestPerUser
            .OrderBy(b => b.Best)
            .Skip(top - 1)
            .Select(b => (decimal?)b.Best)
            .FirstOrDefaultAsync();
        var bestByUser = cutoff == null
            ? await bestPerUser.ToListAsync()
            : await bestPerUser.Where(b => b.Best <= cutoff).ToListAsync();

        // 3. Cuándo lo consiguió cada uno (la primera vez que hizo ese tiempo) y su nombre.
        var userIds = bestByUser.Select(b => b.UserId).ToList();
        IQueryable<Run> candidateRuns = wins.Where(r => userIds.Contains(r.UserId));
        if (cutoff != null) candidateRuns = candidateRuns.Where(r => r.TimeSeconds <= cutoff);
        var bestRuns = await candidateRuns
            .Select(r => new { r.UserId, r.User!.Name, r.TimeSeconds, r.PlayedAtUtc })
            .ToListAsync();

        var entries = bestByUser
            .Select(b =>
            {
                var first = bestRuns
                    .Where(r => r.UserId == b.UserId && r.TimeSeconds == b.Best)
                    .MinBy(r => r.PlayedAtUtc)!;
                return new { first.Name, b.Best, first.PlayedAtUtc };
            })
            .OrderBy(e => e.Best)
            .ThenBy(e => e.PlayedAtUtc)
            .Take(top)
            .Select((e, i) => new RankingEntry
            {
                Position = i + 1,
                UserName = e.Name,
                BestSeconds = (double)e.Best,
                AchievedAtUtc = DateTime.SpecifyKind(e.PlayedAtUtc, DateTimeKind.Utc),
            })
            .ToList();

        return Ok(new RankingResponse { LevelOrder = level.Order, LevelName = level.Name, Entries = entries });
    }
}
