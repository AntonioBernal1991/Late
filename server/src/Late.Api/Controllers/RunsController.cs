using Late.Api.Auth;
using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Late.Api.Controllers;

/// <summary>
/// Envío de partidas (R06F01). Solo para jugadores registrados (R05). Acepta varias de golpe porque el
/// juego guarda en SQLite las jugadas sin conexión y las envía todas al recuperarla.
/// </summary>
[ApiController]
[Route("api/runs")]
[Authorize]
public class RunsController(LateDbContext db, TimeProvider clock) : ControllerBase
{
    public const int MaxRunsPerRequest = 100;

    /// <summary>La canción más larga dura unos minutos; una hora ya es imposible.</summary>
    public const double MaxSeconds = 3600;

    /// <summary>Margen para relojes de móvil un poco adelantados.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    [HttpPost]
    [ProducesResponseType<SubmitRunsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Submit(SubmitRunsRequest request)
    {
        List<RunSubmission> runs = request.Runs ?? new();
        if (runs.Count == 0 || runs.Count > MaxRunsPerRequest)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Runs)] = [$"Entre 1 y {MaxRunsPerRequest} partidas por envío."]
            }));
        }

        int userId = User.UserId();
        DateTime latestAllowed = clock.GetUtcNow().UtcDateTime + FutureTolerance;
        Dictionary<int, int> levelIdByOrder = await db.Levels.ToDictionaryAsync(l => l.Order, l => l.Id);

        var response = new SubmitRunsResponse();
        var valid = new List<Run>();
        foreach (RunSubmission submission in runs)
        {
            DateTime playedAt = TruncateToMilliseconds(AsUtc(submission.PlayedAtUtc));
            bool ok = levelIdByOrder.TryGetValue(submission.LevelOrder, out int levelId)
                      && submission.ElapsedSeconds > 0 && submission.ElapsedSeconds <= MaxSeconds
                      && playedAt <= latestAllowed;
            if (!ok)
            {
                response.Rejected++;
                continue;
            }

            valid.Add(new Run
            {
                UserId = userId,
                LevelId = levelId,
                TimeSeconds = Math.Round((decimal)submission.ElapsedSeconds, 2, MidpointRounding.AwayFromZero),
                BeatTheMusic = submission.BeatTheMusic,
                PlayedAtUtc = playedAt,
            });
        }

        // Reenvíos: misma partida (usuario, nivel, fecha) ya guardada, o repetida en este mismo envío.
        List<DateTime> dates = valid.Select(r => r.PlayedAtUtc).Distinct().ToList();
        var stored = (await db.Runs
                .Where(r => r.UserId == userId && dates.Contains(r.PlayedAtUtc))
                .Select(r => new { r.LevelId, r.PlayedAtUtc })
                .ToListAsync())
            .Select(r => (r.LevelId, r.PlayedAtUtc))
            .ToHashSet();

        foreach (Run run in valid)
        {
            if (!stored.Add((run.LevelId, run.PlayedAtUtc)))
            {
                response.AlreadyStored++;
                continue;
            }
            db.Runs.Add(run);
            response.Accepted++;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otro envío con las mismas partidas se ha guardado a la vez. Al reintentar saldrán como AlreadyStored.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Envío simultáneo de las mismas partidas. Reintenta.");
        }
        return Ok(response);
    }

    /// <summary>Una fecha sin zona («2026-11-16T18:30:05») se toma como UTC, no como hora local del servidor.</summary>
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    /// <summary>La columna guarda milisegundos: se recorta igual para que la comparación de reenvíos sea exacta.</summary>
    private static DateTime TruncateToMilliseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind);
}
