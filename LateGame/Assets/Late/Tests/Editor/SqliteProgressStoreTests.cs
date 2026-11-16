using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Late.Data;
using Late.Game;
using NUnit.Framework;
using SQLite;

namespace Late.Tests
{
    /// <summary>R04F01: progreso, tiempos y ajustes en SQLite, sin conexión y entre sesiones.</summary>
    public class SqliteProgressStoreTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 11, 16, 18, 30, 5, 250, DateTimeKind.Utc);

        private string _path;
        private SqliteProgressStore _store;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"late_test_{Guid.NewGuid():N}.db");
            _store = Open();
        }

        [TearDown]
        public void TearDown()
        {
            _store?.Dispose();
            if (File.Exists(_path)) File.Delete(_path);
        }

        private SqliteProgressStore Open() => new SqliteProgressStore(_path, () => FixedNow);

        /// <summary>Simula cerrar el juego y volver a abrirlo.</summary>
        private void Reopen()
        {
            _store.Dispose();
            _store = Open();
        }

        [Test]
        public void BaseNueva_SoloElPrimerNivel()
        {
            Assert.AreEqual(0, _store.LoadHighestUnlockedRun());
            Assert.IsNull(_store.LoadBestTime(0));
            Assert.AreEqual(0, _store.LoadPendingResults().Count);
        }

        [Test]
        public void ElProgresoSobreviveAReiniciarElJuego()
        {
            _store.SaveRunResult(new RunResult(0, 55.5f, true));
            _store.SaveHighestUnlockedRun(1);

            Reopen();

            Assert.AreEqual(1, _store.LoadHighestUnlockedRun());
            Assert.AreEqual(55.5, _store.LoadBestTime(0).Value, 1e-4);
            Assert.AreEqual(1, _store.LoadPendingResults().Count);
        }

        [Test]
        public void MejorTiempo_SoloBajaYSoloGanando()
        {
            _store.SaveRunResult(new RunResult(0, 58f, true));
            _store.SaveRunResult(new RunResult(0, 61f, true));   // peor: no cambia
            _store.SaveRunResult(new RunResult(0, 40f, false));  // perdiendo: no cuenta
            Assert.AreEqual(58.0, _store.LoadBestTime(0).Value, 1e-4);

            _store.SaveRunResult(new RunResult(0, 52.25f, true)); // mejor
            Assert.AreEqual(52.25, _store.LoadBestTime(0).Value, 1e-4);
        }

        [Test]
        public void DesbloquearNoBorraLosMejoresTiempos()
        {
            _store.SaveRunResult(new RunResult(0, 50f, true));
            _store.SaveHighestUnlockedRun(2);

            Assert.AreEqual(2, _store.LoadHighestUnlockedRun());
            Assert.AreEqual(50.0, _store.LoadBestTime(0).Value, 1e-4);
            Assert.IsNull(_store.LoadBestTime(1));
        }

        [Test]
        public void PerderNoDesbloquea()
        {
            _store.SaveRunResult(new RunResult(0, 70f, false));
            Assert.AreEqual(0, _store.LoadHighestUnlockedRun());
        }

        [Test]
        public void CadaPartidaQuedaPendienteDeEnviar()
        {
            _store.SaveRunResult(new RunResult(0, 58f, true));
            _store.SaveRunResult(new RunResult(1, 63.5f, false));

            var pending = _store.LoadPendingResults();

            Assert.AreEqual(2, pending.Count);
            Assert.AreEqual(0, pending[0].Result.RunIndex);
            Assert.IsTrue(pending[0].Result.BeatTheMusic);
            Assert.AreEqual(1, pending[1].Result.RunIndex);
            Assert.AreEqual(63.5f, pending[1].Result.ElapsedSeconds, 1e-4f);
            Assert.IsFalse(pending[1].Result.BeatTheMusic);
            Assert.AreEqual(FixedNow, pending[1].DateUtc);
            Assert.AreEqual(DateTimeKind.Utc, pending[1].DateUtc.Kind);
        }

        [Test]
        public void LasPendientesEnviadasSeBorran()
        {
            _store.SaveRunResult(new RunResult(0, 58f, true));
            _store.SaveRunResult(new RunResult(0, 57f, true));
            _store.SaveRunResult(new RunResult(1, 66f, false));
            var pending = _store.LoadPendingResults();

            _store.RemovePendingResults(pending.Take(2).Select(p => p.Id));

            var left = _store.LoadPendingResults();
            Assert.AreEqual(1, left.Count);
            Assert.AreEqual(1, left[0].Result.RunIndex);
            // Borrar pendientes no toca el progreso.
            Assert.AreEqual(57.0, _store.LoadBestTime(0).Value, 1e-4);
        }

        [Test]
        public void AjustesSeGuardanYSeSobrescriben()
        {
            Assert.AreEqual("es", _store.LoadSetting("idioma", "es"));

            _store.SaveSetting("idioma", "en");
            Reopen();
            Assert.AreEqual("en", _store.LoadSetting("idioma", "es"));

            _store.SaveSetting("idioma", "es");
            Assert.AreEqual("es", _store.LoadSetting("idioma"));
        }

        [Test]
        public void ProgresoDeOtraCuenta_SustituyeAlDelDispositivo()
        {
            // El dispositivo tenía el progreso de otra cuenta: Run2 desbloqueado y un tiempo en Run0.
            _store.SaveRunResult(new RunResult(0, 40f, true));
            _store.SaveHighestUnlockedRun(2);

            _store.ApplyAccountProgress(1, new Dictionary<int, double> { [0] = 55.5 }, replace: true);

            Assert.AreEqual(1, _store.LoadHighestUnlockedRun());
            Assert.AreEqual(55.5, _store.LoadBestTime(0).Value, 1e-4);
            Assert.IsNull(_store.LoadBestTime(1));
        }

        [Test]
        public void ProgresoDeLaMismaCuenta_SeQuedaLoMejorDeCadaLado()
        {
            // En el dispositivo: Run1 ganado sin conexión (aún no lo sabe el servidor).
            _store.SaveRunResult(new RunResult(0, 60f, true));
            _store.SaveRunResult(new RunResult(1, 45f, true));
            _store.SaveHighestUnlockedRun(2);

            // En el servidor: solo Run0, pero con un tiempo mejor (jugado en otro móvil).
            _store.ApplyAccountProgress(1, new Dictionary<int, double> { [0] = 50.0 }, replace: false);

            Assert.AreEqual(2, _store.LoadHighestUnlockedRun());
            Assert.AreEqual(50.0, _store.LoadBestTime(0).Value, 1e-4);
            Assert.AreEqual(45.0, _store.LoadBestTime(1).Value, 1e-4);
        }

        [Test]
        public void SustituirElProgreso_NoTocaLasPendientesNiLosAjustes()
        {
            _store.SaveRunResult(new RunResult(0, 40f, true));
            _store.SaveSetting("idioma", "en");

            _store.ApplyAccountProgress(0, new Dictionary<int, double>(), replace: true);

            Assert.AreEqual(1, _store.LoadPendingResults().Count);
            Assert.AreEqual("en", _store.LoadSetting("idioma"));
        }

        [Test]
        public void LasTablasTienenLosNombresDeLaMemoria()
        {
            _store.Dispose();
            _store = null;
            using (var db = new SQLiteConnection(_path))
            {
                var tables = db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name");
                CollectionAssert.IsSubsetOf(new[] { "ajustes", "pendientes", "progreso" }, tables);

                var columns = db.GetTableInfo("pendientes").Select(c => c.Name).ToArray();
                CollectionAssert.AreEquivalent(new[] { "id", "nivel_id", "tiempo_seg", "gano_musica", "fecha" }, columns);
            }
        }
    }
}
