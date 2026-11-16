using System.Net.Http.Headers;
using System.Net.Http.Json;
using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;

namespace Late.Api.Tests;

/// <summary>
/// Un MySQL real en Docker (el mismo motor que en producción) y la API completa en memoria.
/// Se crea una vez para todas las pruebas; cada clase de pruebas vacía las tablas al empezar.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string TestJwtKey = "clave-solo-para-pruebas-de-integracion-0123456789";

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4")
        .WithDatabase("late")
        .WithUsername("late")
        .WithPassword("late_test_password")
        .Build();

    public LateApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        Factory = new LateApiFactory(_mysql.GetConnectionString(), authPermitsPerMinute: 1000);
        Factory.CreateClient(); // arranca la API y aplica las migraciones
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mysql.DisposeAsync();
    }

    public string ConnectionString => _mysql.GetConnectionString();

    public async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LateDbContext>();
        await db.Runs.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }
}

public class LateApiFactory(string connectionString, int authPermitsPerMinute) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Key", ApiFixture.TestJwtKey);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimit:AuthPermitsPerMinute", authPermitsPerMinute.ToString());
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

/// <summary>Base de las pruebas: base de datos vacía y atajos para registrarse y enviar partidas.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;
    protected HttpClient Client { get; } = fixture.Factory.CreateClient();

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<AuthResponse> RegisterAsync(string name, string? email = null, string password = "contraseña-segura")
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = name,
            Email = email ?? $"{name}@late.test",
            Password = password,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Un cliente HTTP con el token de un jugador recién registrado.</summary>
    protected async Task<HttpClient> PlayerAsync(string name)
    {
        AuthResponse auth = await RegisterAsync(name);
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    protected static RunSubmission Run(int level, double seconds, bool won, DateTime? playedAt = null) => new()
    {
        LevelOrder = level,
        ElapsedSeconds = seconds,
        BeatTheMusic = won,
        PlayedAtUtc = playedAt ?? new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
    };

    protected static async Task<SubmitRunsResponse> SubmitAsync(HttpClient player, params RunSubmission[] runs)
    {
        HttpResponseMessage response = await player.PostAsJsonAsync("/api/runs", new SubmitRunsRequest { Runs = runs.ToList() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmitRunsResponse>())!;
    }
}
