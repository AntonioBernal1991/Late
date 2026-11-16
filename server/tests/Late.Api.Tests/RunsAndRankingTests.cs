using System.Net;
using System.Net.Http.Json;
using Late.Api.Data;
using Late.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Late.Api.Tests;

/// <summary>R06F01: enviar resultados (también los pendientes sin conexión) y consultar el ranking.</summary>
public class RunsAndRankingTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly DateTime Day = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private async Task<RankingResponse> RankingAsync(int level, int? top = null)
    {
        string url = $"/api/levels/{level}/ranking" + (top.HasValue ? $"?top={top}" : "");
        return (await Client.GetFromJsonAsync<RankingResponse>(url))!;
    }

    private async Task<List<Run>> StoredRunsAsync()
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LateDbContext>().Runs.OrderBy(r => r.Id).ToListAsync();
    }

    // ---------- Envío ----------

    [Fact]
    public async Task SinCuenta_NoSePuedeEnviar()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/runs",
            new SubmitRunsRequest { Runs = [Run(0, 50, true)] });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenFalso_NoSePuedeEnviar()
    {
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.e30.firma-falsa");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/runs",
            new SubmitRunsRequest { Runs = [Run(0, 50, true)] });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnviarPartidas_SeGuardanConDosDecimales()
    {
        HttpClient player = await PlayerAsync("antonio");

        SubmitRunsResponse result = await SubmitAsync(player,
            Run(0, 52.3456, true, Day),
            Run(1, 61.004, false, Day.AddMinutes(2)));

        Assert.Equal(2, result.Accepted);
        List<Run> stored = await StoredRunsAsync();
        Assert.Equal(52.35m, stored[0].TimeSeconds);
        Assert.True(stored[0].BeatTheMusic);
        Assert.Equal(61.00m, stored[1].TimeSeconds);
        Assert.False(stored[1].BeatTheMusic);
    }

    [Fact]
    public async Task ReenviarLasPendientes_NoDuplica()
    {
        HttpClient player = await PlayerAsync("antonio");
        RunSubmission[] pending = [Run(0, 50, true, Day.AddMilliseconds(123)), Run(0, 55, true, Day.AddMinutes(1))];
        await SubmitAsync(player, pending);

        // Se cortó la conexión antes de recibir la respuesta: el juego las vuelve a enviar con una nueva.
        SubmitRunsResponse retry = await SubmitAsync(player, [.. pending, Run(0, 49, true, Day.AddMinutes(5))]);

        Assert.Equal(1, retry.Accepted);
        Assert.Equal(2, retry.AlreadyStored);
        Assert.Equal(3, (await StoredRunsAsync()).Count);
    }

    [Fact]
    public async Task PartidaRepetidaEnElMismoEnvio_SeGuardaUnaVez()
    {
        HttpClient player = await PlayerAsync("antonio");

        SubmitRunsResponse result = await SubmitAsync(player, Run(0, 50, true, Day), Run(0, 50, true, Day));

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.AlreadyStored);
    }

    [Fact]
    public async Task PartidasImposibles_SeRechazanYElRestoSeGuarda()
    {
        HttpClient player = await PlayerAsync("antonio");

        SubmitRunsResponse result = await SubmitAsync(player,
            Run(0, 50, true, Day),                          // válida
            Run(99, 50, true, Day.AddMinutes(1)),           // nivel inexistente
            Run(0, -3, true, Day.AddMinutes(2)),            // tiempo negativo
            Run(0, 4000, false, Day.AddMinutes(3)),         // más de una hora
            Run(0, 50, true, DateTime.UtcNow.AddDays(1)));  // en el futuro

        Assert.Equal(1, result.Accepted);
        Assert.Equal(4, result.Rejected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task EnvioVacioODemasiadoGrande_400(int count)
    {
        HttpClient player = await PlayerAsync("antonio");
        var runs = Enumerable.Range(0, count).Select(i => Run(0, 50, true, Day.AddSeconds(i))).ToList();

        HttpResponseMessage response = await player.PostAsJsonAsync("/api/runs", new SubmitRunsRequest { Runs = runs });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Ranking ----------

    [Fact]
    public async Task Niveles_Run0YRun1()
    {
        var levels = await Client.GetFromJsonAsync<List<LevelDto>>("/api/levels");

        Assert.Equal(["Run0", "Run1"], levels!.Select(l => l.Name));
        Assert.Equal([0, 1], levels!.Select(l => l.Order));
    }

    [Fact]
    public async Task Ranking_MejorTiempoDeCadaJugadorGanando()
    {
        HttpClient ana = await PlayerAsync("ana");
        HttpClient bea = await PlayerAsync("bea");
        HttpClient carlos = await PlayerAsync("carlos");

        await SubmitAsync(ana, Run(0, 58, true, Day), Run(0, 51.5, true, Day.AddMinutes(1)), Run(0, 40, false, Day.AddMinutes(2)));
        await SubmitAsync(bea, Run(0, 53, true, Day));
        await SubmitAsync(carlos, Run(0, 45, false, Day), Run(1, 30, true, Day)); // en Run0 nunca ganó

        RankingResponse ranking = await RankingAsync(0);

        Assert.Equal("Run0", ranking.LevelName);
        Assert.Equal(["ana", "bea"], ranking.Entries.Select(e => e.UserName));
        Assert.Equal([1, 2], ranking.Entries.Select(e => e.Position));
        Assert.Equal(51.5, ranking.Entries[0].BestSeconds);
        Assert.Equal(Day.AddMinutes(1), ranking.Entries[0].AchievedAtUtc);
    }

    [Fact]
    public async Task Ranking_AIgualTiempoGanaQuienLoHizoAntes()
    {
        HttpClient ana = await PlayerAsync("ana");
        HttpClient bea = await PlayerAsync("bea");
        await SubmitAsync(ana, Run(0, 50, true, Day.AddHours(1)));
        await SubmitAsync(bea, Run(0, 50, true, Day));

        RankingResponse ranking = await RankingAsync(0);

        Assert.Equal(["bea", "ana"], ranking.Entries.Select(e => e.UserName));
    }

    [Fact]
    public async Task Ranking_TopLimitaLasFilas()
    {
        for (int i = 0; i < 5; i++)
        {
            HttpClient player = await PlayerAsync($"jugador{i}");
            await SubmitAsync(player, Run(0, 50 + i, true, Day));
        }

        RankingResponse ranking = await RankingAsync(0, top: 3);

        Assert.Equal(["jugador0", "jugador1", "jugador2"], ranking.Entries.Select(e => e.UserName));
    }

    [Fact]
    public async Task Ranking_EmpateEnElUltimoPuesto_DecideLaFecha()
    {
        HttpClient ana = await PlayerAsync("ana");
        HttpClient bea = await PlayerAsync("bea");
        HttpClient carlos = await PlayerAsync("carlos");
        await SubmitAsync(ana, Run(0, 40, true, Day));
        await SubmitAsync(bea, Run(0, 50, true, Day.AddHours(2)));
        await SubmitAsync(carlos, Run(0, 50, true, Day.AddHours(1)));

        RankingResponse ranking = await RankingAsync(0, top: 2);

        Assert.Equal(["ana", "carlos"], ranking.Entries.Select(e => e.UserName));
    }

    [Fact]
    public async Task Ranking_NivelSinPartidas_Vacio()
    {
        Assert.Empty((await RankingAsync(1)).Entries);
    }

    [Fact]
    public async Task Ranking_NivelInexistente_404()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/levels/99/ranking");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
