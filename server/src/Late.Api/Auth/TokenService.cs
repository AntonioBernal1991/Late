using System.Security.Claims;
using System.Text;
using Late.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Late.Api.Auth;

/// <summary>Configuración de los tokens (sección «Jwt» de appsettings o variables Jwt__*).</summary>
public class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>Clave secreta HMAC-SHA256. Mínimo 32 bytes. Nunca en el repositorio: va en .env o user-secrets.</summary>
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "late-api";
    public string Audience { get; set; } = "late-game";

    /// <summary>Un jugador de móvil no debería tener que volver a entrar cada día.</summary>
    public int ExpiresInDays { get; set; } = 30;

    public SymmetricSecurityKey SigningKey()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Key);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key debe tener al menos 32 bytes. Configúrala en docker/.env (JWT_KEY) o con dotnet user-secrets.");
        }
        return new SymmetricSecurityKey(bytes);
    }
}

/// <summary>Crea el token JWT que el juego envía en cada petición autenticada.</summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        DateTime expires = now.AddDays(_options.ExpiresInDays);

        string token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Name),
            }),
            SigningCredentials = new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return (token, expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario del token. Los endpoints con [Authorize] siempre lo tienen.</summary>
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? throw new InvalidOperationException("Token sin «sub»."));
}
