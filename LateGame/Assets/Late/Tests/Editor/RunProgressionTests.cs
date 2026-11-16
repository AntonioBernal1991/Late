using Late.Game;
using NUnit.Framework;

namespace Late.Tests
{
    /// <summary>R02F03T01P01: ganar a la música en un nivel desbloquea el siguiente.</summary>
    public class RunProgressionTests
    {
        [Test]
        public void AlEmpezar_SoloElPrimerNivel()
        {
            Assert.IsTrue(RunProgression.IsUnlocked(0, 0));
            Assert.IsFalse(RunProgression.IsUnlocked(0, 1));
            Assert.IsFalse(RunProgression.IsUnlocked(0, -1));
        }

        [Test]
        public void GanarALaMusica_DesbloqueaElSiguiente()
        {
            int unlocked = RunProgression.HighestUnlockedAfter(0, new RunResult(0, 52.3f, true));
            Assert.AreEqual(1, unlocked);
        }

        [Test]
        public void PerderContraLaMusica_NoDesbloquea()
        {
            int unlocked = RunProgression.HighestUnlockedAfter(0, new RunResult(0, 71.0f, false));
            Assert.AreEqual(0, unlocked);
        }

        [Test]
        public void GanarUnNivelAnterior_NoQuitaLoDesbloqueado()
        {
            int unlocked = RunProgression.HighestUnlockedAfter(3, new RunResult(0, 40f, true));
            Assert.AreEqual(3, unlocked);
        }

        [Test]
        public void ResultadoSinNivel_SeIgnora()
        {
            int unlocked = RunProgression.HighestUnlockedAfter(1, new RunResult(-1, 10f, true));
            Assert.AreEqual(1, unlocked);
        }

        [Test]
        public void AlmacenEnMemoria_GuardaProgresoYResultados()
        {
            var store = new InMemoryProgressStore();
            Assert.AreEqual(0, store.LoadHighestUnlockedRun());

            store.SaveHighestUnlockedRun(2);
            store.SaveRunResult(new RunResult(1, 48.5f, true));

            Assert.AreEqual(2, store.LoadHighestUnlockedRun());
            Assert.AreEqual(1, store.Results.Count);
            Assert.AreEqual(1, store.Results[0].RunIndex);
            Assert.IsTrue(store.Results[0].BeatTheMusic);
        }
    }
}
