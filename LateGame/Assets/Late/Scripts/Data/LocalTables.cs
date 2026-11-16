using SQLite;

namespace Late.Data
{
    // Tablas de la base de datos local (SQLite). Los nombres coinciden con el diseño de la memoria.

    /// <summary>PROGRESO: un registro por nivel jugado o desbloqueado.</summary>
    [Table("progreso")]
    public class ProgresoRow
    {
        [PrimaryKey, Column("nivel_id")]
        public int NivelId { get; set; }

        /// <summary>1 si el nivel está desbloqueado.</summary>
        [Column("desbloqueado"), NotNull]
        public bool Desbloqueado { get; set; }

        /// <summary>Mejor tiempo ganando a la música, en segundos. Null si aún no se ha ganado.</summary>
        [Column("mejor_tiempo")]
        public double? MejorTiempo { get; set; }
    }

    /// <summary>PENDIENTES: partidas que todavía no se han enviado al ranking online.</summary>
    [Table("pendientes")]
    public class PendienteRow
    {
        [PrimaryKey, AutoIncrement, Column("id")]
        public int Id { get; set; }

        [Column("nivel_id"), NotNull]
        public int NivelId { get; set; }

        [Column("tiempo_seg"), NotNull]
        public double TiempoSeg { get; set; }

        [Column("gano_musica"), NotNull]
        public bool GanoMusica { get; set; }

        /// <summary>Fecha en UTC, formato ISO 8601 (texto, para que sea legible y ordenable).</summary>
        [Column("fecha"), NotNull]
        public string Fecha { get; set; }
    }

    /// <summary>AJUSTES: pares clave-valor (idioma, volumen...).</summary>
    [Table("ajustes")]
    public class AjusteRow
    {
        [PrimaryKey, Column("clave")]
        public string Clave { get; set; }

        [Column("valor")]
        public string Valor { get; set; }
    }
}
