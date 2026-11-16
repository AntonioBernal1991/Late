using System.Net;
using System.Net.Http.Json;
using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Late.Api.Tests;

/// <summary>R05F01: registro e inicio de sesión seguros (contraseña con hash y token JWT).</summary>
public class AuthTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Registrarse_DevuelveUnToken()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            UserName = "antonio", Email = "Antonio@Late.Test", Password = "contraseña-segura",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal("antonio", auth.UserName);
        Assert.True(auth.ExpiresAtUtc > DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task LaContraseñaSeGuardaConHash()
    {
        await RegisterAsync("antonio", password: "contraseña-segura");

        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        User user = await scope.ServiceProvider.GetRequiredService<LateDbContext>().Users.SingleAsync();
        Assert.DoesNotContain("contraseña-segura", user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash); // formato de PasswordHasher (PBKDF2 con sal)
        Assert.Equal("antonio@late.test", user.Email);
    }

    [Theory]
    [InlineData("antonio", "otro@late.test")]   // mismo nombre
    [InlineData("ANTONIO", "otro@late.test")]   // mismo nombre con otras mayúsculas
    [InlineData("otro", "ANTONIO@late.test")]   // mismo email
    public async Task NombreOEmailRepetido_Conflicto(string name, string email)
    {
        await RegisterAsync("antonio", "antonio@late.test");

        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { UserName = name, Email = email, Password = "contraseña-segura" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("an", "a@late.test", "contraseña-segura", "UserName")]
    [InlineData("con espacios", "a@late.test", "contraseña-segura", "UserName")]
    [InlineData("antonio", "no-es-un-email", "contraseña-segura", "Email")]
    [InlineData("antonio", "a@late.test", "corta", "Password")]
    public async Task DatosInvalidos_400ConElCampo(string name, string email, string password, string field)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { UserName = name, Email = email, Password = password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("antonio")]
    [InlineData("antonio@late.test")]
    [InlineData("ANTONIO@LATE.TEST")]
    public async Task EntrarConNombreOEmail(string login)
    {
        await RegisterAsync("antonio", "antonio@late.test", "contraseña-segura");

        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { UserNameOrEmail = login, Password = "contraseña-segura" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("antonio", (await response.Content.ReadFromJsonAsync<AuthResponse>())!.UserName);
    }

    [Fact]
    public async Task ContraseñaIncorrectaOUsuarioInexistente_MismoError()
    {
        await RegisterAsync("antonio", password: "contraseña-segura");

        HttpResponseMessage wrongPassword = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { UserNameOrEmail = "antonio", Password = "otra-contraseña" });
        HttpResponseMessage unknownUser = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { UserNameOrEmail = "nadie", Password = "contraseña-segura" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var b = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(a!.Title, b!.Title);
    }

    [Fact]
    public async Task DemasiadosIntentos_429()
    {
        // Una API aparte con un límite pequeño, sobre la misma base de datos.
        await using var limited = new LateApiFactory(Fixture.ConnectionString, authPermitsPerMinute: 3);
        HttpClient client = limited.CreateClient();
        var login = new LoginRequest { UserNameOrEmail = "nadie", Password = "contraseña-segura" };

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
    }
}
