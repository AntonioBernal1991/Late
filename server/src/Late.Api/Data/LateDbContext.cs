using Microsoft.EntityFrameworkCore;

namespace Late.Api.Data;

/// <summary>
/// Base de datos del servidor (MySQL). Los nombres de tablas y columnas son los del diseño de la memoria.
/// </summary>
public class LateDbContext(DbContextOptions<LateDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Run> Runs => Set<Run>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.ToTable("usuarios");
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Name).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(u => u.CreatedAtUtc).HasColumnName("fecha_alta").HasColumnType("datetime(3)");
            e.HasIndex(u => u.Name).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        model.Entity<Level>(e =>
        {
            e.ToTable("niveles");
            e.Property(l => l.Id).HasColumnName("id");
            e.Property(l => l.Name).HasColumnName("nombre").HasMaxLength(30).IsRequired();
            e.Property(l => l.Order).HasColumnName("orden");
            e.HasIndex(l => l.Order).IsUnique();

            // Los niveles del juego. Al añadir uno nuevo se añade aquí y se crea una migración.
            e.HasData(
                new Level { Id = 1, Name = "Run0", Order = 0 },
                new Level { Id = 2, Name = "Run1", Order = 1 });
        });

        model.Entity<Run>(e =>
        {
            e.ToTable("partidas");
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.UserId).HasColumnName("usuario_id");
            e.Property(r => r.LevelId).HasColumnName("nivel_id");
            e.Property(r => r.TimeSeconds).HasColumnName("tiempo_seg").HasPrecision(6, 2);
            e.Property(r => r.BeatTheMusic).HasColumnName("gano_musica");
            e.Property(r => r.PlayedAtUtc).HasColumnName("fecha").HasColumnType("datetime(3)");

            e.HasOne(r => r.User).WithMany(u => u.Runs).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Level).WithMany(l => l.Runs).HasForeignKey(r => r.LevelId).OnDelete(DeleteBehavior.Restrict);

            // Una partida reenviada (sin conexión, reintentos) no se guarda dos veces.
            e.HasIndex(r => new { r.UserId, r.LevelId, r.PlayedAtUtc }).IsUnique();
            // Para el ranking: mejores tiempos ganando en un nivel.
            e.HasIndex(r => new { r.LevelId, r.BeatTheMusic, r.TimeSeconds });
        });
    }
}
