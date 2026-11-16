using System.Text.RegularExpressions;
using Late.Api.Auth;
using Late.Api.Data;
using Late.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Late.Api.Controllers;

/// <summary>Registro e inicio de sesión (R05F01).</summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimits.Auth)]
public partial class AuthController(
    LateDbContext db,
    IPasswordHasher<User> hasher,
    TokenService tokens,
    TimeProvider clock) : ControllerBase
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,30}$")]
    private static partial Regex UserNamePattern();

    // Comprobación básica: algo@algo.algo. La de verdad sería enviar un correo de confirmación.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    /// <summary>Crea la cuenta y devuelve ya el token, para no obligar a entrar después.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        string name = request.UserName?.Trim() ?? "";
        string email = request.Email?.Trim().ToLowerInvariant() ?? "";
        string password = request.Password ?? "";

        var errors = new Dictionary<string, string[]>();
        if (!UserNamePattern().IsMatch(name))
            errors[nameof(request.UserName)] = ["Entre 3 y 30 caracteres: letras, números, «_», «.» o «-»."];
        if (email.Length > 100 || !EmailPattern().IsMatch(email))
            errors[nameof(request.Email)] = ["Email no válido."];
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            errors[nameof(request.Password)] = [$"Entre {MinPasswordLength} y {MaxPasswordLength} caracteres."];
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        if (await db.Users.AnyAsync(u => u.Name == name || u.Email == email))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "El nombre o el email ya están registrados.");
        }

        var user = new User { Name = name, Email = email, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos registros simultáneos con el mismo nombre: el índice único decide.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "El nombre o el email ya están registrados.");
        }

        return StatusCode(StatusCodes.Status201Created, CreateResponse(user));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        string login = request.UserNameOrEmail?.Trim() ?? "";
        string loginEmail = login.ToLowerInvariant();
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Name == login || u.Email == loginEmail);

        // Mismo mensaje si el usuario no existe o la contraseña falla: no se revela qué cuentas existen.
        PasswordVerificationResult result = user == null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");
        if (user == null || result == PasswordVerificationResult.Failed)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Usuario o contraseña incorrectos.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // El algoritmo de hash ha mejorado desde que se creó la cuenta: se actualiza ahora.
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync();
        }

        return Ok(CreateResponse(user));
    }

    private AuthResponse CreateResponse(User user)
    {
        (string token, DateTime expires) = tokens.CreateToken(user);
        return new AuthResponse { Token = token, UserName = user.Name, ExpiresAtUtc = expires };
    }
}
