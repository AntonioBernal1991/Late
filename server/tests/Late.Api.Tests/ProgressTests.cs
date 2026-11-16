using System.Net;
using System.Net.Http.Json;
using Late.Shared;

namespace Late.Api.Tests;

/// <summary>El progreso va con la cuenta: se recupera en cualquier dispositivo.</summary>
public class ProgressTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly DateTime Day = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<ProgressResponse> ProgressAsync(HttpClient player) =>
        (await player.GetFromJsonAsync<ProgressResponse>("/api/progress"))!;

    [Fact]
    public async Task SinCuenta_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/progress")).StatusCode);
    }

    [Fact]
    public async Task CuentaNueva_SoloElPrimerNivel()
    {
        HttpClient player = await PlayerAsync("ana");

        ProgressResponse progress = await ProgressAsync(player);

        Assert.Equal(0, progress.HighestUnlockedLevel);
        Assert.Empty(progress.Levels);
    }

    [Fact]
    public async Task PerderNoDesbloquea()
    {
        HttpClient player = await PlayerAsync("ana");
        await SubmitAsync(player, Run(0, 70, false, Day));

        Assert.Equal(0, (await ProgressAsync(player)).HighestUnlockedLevel);
    }

    [Fact]
    public async Task GanarDesbloqueaElSiguienteYGuardaElMejorTiempo()
    {
        HttpClient player = await PlayerAsync("ana");
        await SubmitAsync(player,
            Run(0, 58, true, Day),
            Run(0, 52.5, true, Day.AddMinutes(1)),
            Run(1, 40, false, Day.AddMinutes(2)));

        ProgressResponse progress = await ProgressAsync(player);

        Assert.Equal(1, progress.HighestUnlockedLevel);
        LevelProgress run0 = Assert.Single(progress.Levels);
        Assert.Equal(0, run0.LevelOrder);
        Assert.Equal(52.5, run0.BestSeconds);
    }

    [Fact]
    public async Task CadaCuentaTieneSuProgreso()
    {
        HttpClient ana = await PlayerAsync("ana");
        HttpClient bea = await PlayerAsync("bea");
        await SubmitAsync(ana, Run(0, 50, true, Day), Run(1, 60, true, Day.AddMinutes(1)));

        Assert.Equal(2, (await ProgressAsync(ana)).HighestUnlockedLevel);
        Assert.Equal(0, (await ProgressAsync(bea)).HighestUnlockedLevel);
    }
}
