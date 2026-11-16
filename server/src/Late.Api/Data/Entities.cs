namespace Late.Api.Data;

/// <summary>USUARIOS: un jugador registrado.</summary>
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Siempre en minúsculas, para que el login no dependa de mayúsculas.</summary>
    public string Email { get; set; } = "";

    /// <summary>Hash PBKDF2 con sal (PasswordHasher de ASP.NET Core Identity). Nunca la contraseña.</summary>
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    public List<Run> Runs { get; set; } = new();
}

/// <summary>NIVELES: Run0, Run1... <see cref="Order"/> es el índice que usa el juego.</summary>
public class Level
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }

    public List<Run> Runs { get; set; } = new();
}

/// <summary>PARTIDAS: una partida terminada de un usuario en un nivel.</summary>
public class Run
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int LevelId { get; set; }
    public Level? Level { get; set; }

    /// <summary>Segundos con dos decimales (DECIMAL(6,2)).</summary>
    public decimal TimeSeconds { get; set; }
    public bool BeatTheMusic { get; set; }

    /// <summary>Cuándo se jugó (UTC, con milisegundos). Con el usuario y el nivel identifica la partida.</summary>
    public DateTime PlayedAtUtc { get; set; }
}
