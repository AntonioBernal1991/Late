using System;
using System.Collections.Generic;
using System.Globalization;
using Late.Game;
using SQLite;

namespace Late.Data
{
    /// <summary>Partida guardada en local que aún no se ha enviado al servidor.</summary>
    public readonly struct PendingResult
    {
        public int Id { get; }
        public RunResult Result { get; }
        public DateTime DateUtc { get; }

        public PendingResult(int id, RunResult result, DateTime dateUtc)
        {
            Id = id;
            Result = result;
            DateUtc = dateUtc;
        }
    }

    /// <summary>
    /// Progreso, resultados y ajustes guardados en SQLite (R04). Funciona sin conexión: cada partida
    /// queda en PENDIENTES hasta que se envíe al ranking (fase del servidor).
    /// </summary>
    public sealed class SqliteProgressStore : IProgressStore, IDisposable
    {
        private const string DateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        private readonly SQLiteConnection _db;
        private readonly Func<DateTime> _utcNow;

        /// <param name="databasePath">Ruta del archivo. Se crea si no existe.</param>
        /// <param name="utcNow">Reloj; las pruebas pasan uno fijo.</param>
        public SqliteProgressStore(string databasePath, Func<DateTime> utcNow = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _db = new SQLiteConnection(databasePath);
            _db.CreateTable<ProgresoRow>();
            _db.CreateTable<PendienteRow>();
            _db.CreateTable<AjusteRow>();
        }

        // ---------- IProgressStore ----------

        public int LoadHighestUnlockedRun() =>
            _db.ExecuteScalar<int>("SELECT COALESCE(MAX(nivel_id), 0) FROM progreso WHERE desbloqueado = 1");

        /// <summary>Desbloquea todos los niveles hasta <paramref name="runIndex"/> sin tocar sus mejores tiempos.</summary>
        public void SaveHighestUnlockedRun(int runIndex)
        {
            _db.RunInTransaction(() =>
            {
                for (int i = 0; i <= runIndex; i++)
                {
                    _db.Execute(
                        "INSERT INTO progreso (nivel_id, desbloqueado) VALUES (?, 1) " +
                        "ON CONFLICT(nivel_id) DO UPDATE SET desbloqueado = 1", i);
                }
            });
        }

        /// <summary>
        /// Guarda la partida como pendiente de enviar y, si se ganó a la música, actualiza el mejor tiempo.
        /// Las dos cosas van en una transacción: o se guardan ambas o ninguna.
        /// </summary>
        public void SaveRunResult(RunResult result)
        {
            _db.RunInTransaction(() =>
            {
                _db.Insert(new PendienteRow
                {
                    NivelId = result.RunIndex,
                    TiempoSeg = result.ElapsedSeconds,
                    GanoMusica = result.BeatTheMusic,
                    Fecha = _utcNow().ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture)
                });

                if (result.BeatTheMusic)
                {
                    // El nivel ganado está desbloqueado por definición.
                    _db.Execute(
                        "INSERT INTO progreso (nivel_id, desbloqueado, mejor_tiempo) VALUES (?, 1, ?) " +
                        "ON CONFLICT(nivel_id) DO UPDATE SET mejor_tiempo = " +
                        "CASE WHEN mejor_tiempo IS NULL OR excluded.mejor_tiempo < mejor_tiempo " +
                        "THEN excluded.mejor_tiempo ELSE mejor_tiempo END",
                        result.RunIndex, (double)result.ElapsedSeconds);
                }
            });
        }

        // ---------- Tiempos ----------

        /// <summary>Mejor tiempo ganando a la música en ese nivel, o null si no se ha ganado nunca.</summary>
        public double? LoadBestTime(int runIndex) =>
            _db.Find<ProgresoRow>(runIndex)?.MejorTiempo;

        // ---------- Pendientes de enviar ----------

        public IReadOnlyList<PendingResult> LoadPendingResults()
        {
            var rows = _db.Query<PendienteRow>("SELECT * FROM pendientes ORDER BY id");
            var results = new List<PendingResult>(rows.Count);
            foreach (PendienteRow row in rows)
            {
                DateTime date = DateTime.ParseExact(row.Fecha, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                results.Add(new PendingResult(row.Id,
                    new RunResult(row.NivelId, (float)row.TiempoSeg, row.GanoMusica), date));
            }
            return results;
        }

        /// <summary>Borra las partidas que el servidor ya ha confirmado.</summary>
        public void RemovePendingResults(IEnumerable<int> ids)
        {
            _db.RunInTransaction(() =>
            {
                foreach (int id in ids) _db.Delete<PendienteRow>(id);
            });
        }

        // ---------- Ajustes ----------

        public string LoadSetting(string key, string defaultValue = null) =>
            _db.Find<AjusteRow>(key)?.Valor ?? defaultValue;

        public void SaveSetting(string key, string value) =>
            _db.InsertOrReplace(new AjusteRow { Clave = key, Valor = value });

        public void Dispose() => _db.Dispose();
    }
}
