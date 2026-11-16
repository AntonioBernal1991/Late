using System;
using System.Collections.Generic;

// Contratos de la API: los mismos archivos los compilan el servidor (Late.Shared.csproj) y Unity
// (paquete local con Late.Shared.asmdef). Por eso solo usan C# compatible con .NET Standard 2.1:
// clases simples con propiedades, sin records ni tipos que Unity no tenga.
namespace Late.Shared
{
    /// <summary>POST /api/auth/register</summary>
    public class RegisterRequest
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    /// <summary>POST /api/auth/login. Se puede entrar con el nombre o con el email.</summary>
    public class LoginRequest
    {
        public string UserNameOrEmail { get; set; }
        public string Password { get; set; }
    }

    /// <summary>Respuesta de registro e inicio de sesión.</summary>
    public class AuthResponse
    {
        /// <summary>Token JWT para la cabecera <c>Authorization: Bearer ...</c>.</summary>
        public string Token { get; set; }
        public string UserName { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>Una partida jugada. Equivale al RunResult del juego más la fecha.</summary>
    public class RunSubmission
    {
        /// <summary>Orden del nivel: Run0 = 0, Run1 = 1...</summary>
        public int LevelOrder { get; set; }
        public double ElapsedSeconds { get; set; }
        public bool BeatTheMusic { get; set; }

        /// <summary>Cuándo se jugó, en UTC. Las partidas sin conexión se envían más tarde con su fecha real.</summary>
        public DateTime PlayedAtUtc { get; set; }
    }

    /// <summary>POST /api/runs: una o varias partidas (las pendientes de enviar).</summary>
    public class SubmitRunsRequest
    {
        public List<RunSubmission> Runs { get; set; } = new List<RunSubmission>();
    }

    public class SubmitRunsResponse
    {
        /// <summary>Partidas nuevas guardadas.</summary>
        public int Accepted { get; set; }

        /// <summary>Partidas que ya estaban guardadas (reenvíos). No es un error: el cliente puede borrarlas.</summary>
        public int AlreadyStored { get; set; }

        /// <summary>
        /// Partidas inválidas (nivel inexistente, tiempo imposible, fecha futura). No se guardan y
        /// reenviarlas no cambiaría nada, así que el cliente también puede borrarlas de pendientes.
        /// </summary>
        public int Rejected { get; set; }
    }

    /// <summary>Progreso de un nivel para la cuenta.</summary>
    public class LevelProgress
    {
        public int LevelOrder { get; set; }

        /// <summary>Mejor tiempo ganando a la música; null si no lo ha ganado nunca.</summary>
        public double? BestSeconds { get; set; }
    }

    /// <summary>GET /api/progress: el progreso de la cuenta, para seguir en cualquier dispositivo.</summary>
    public class ProgressResponse
    {
        /// <summary>Último nivel desbloqueado (0 = solo el primero). Ganar un nivel desbloquea el siguiente.</summary>
        public int HighestUnlockedLevel { get; set; }
        public List<LevelProgress> Levels { get; set; } = new List<LevelProgress>();
    }

    /// <summary>GET /api/levels</summary>
    public class LevelDto
    {
        public int Order { get; set; }
        public string Name { get; set; }
    }

    /// <summary>Una fila del ranking: el mejor tiempo de un jugador ganando a la música.</summary>
    public class RankingEntry
    {
        public int Position { get; set; }
        public string UserName { get; set; }
        public double BestSeconds { get; set; }
        public DateTime AchievedAtUtc { get; set; }
    }

    /// <summary>GET /api/levels/{order}/ranking</summary>
    public class RankingResponse
    {
        public int LevelOrder { get; set; }
        public string LevelName { get; set; }
        public List<RankingEntry> Entries { get; set; } = new List<RankingEntry>();
    }
}
