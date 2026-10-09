using System.Collections.Generic;

namespace Late.Game
{
    /// <summary>
    /// Progreso que solo dura mientras el juego está abierto. Es la implementación por defecto
    /// hasta que exista la versión con SQLite.
    /// </summary>
    public class InMemoryProgressStore : IProgressStore
    {
        private int _highestUnlockedRun;
        private readonly List<RunResult> _results = new List<RunResult>();

        public IReadOnlyList<RunResult> Results => _results;

        public int LoadHighestUnlockedRun() => _highestUnlockedRun;

        public void SaveHighestUnlockedRun(int runIndex) => _highestUnlockedRun = runIndex;

        public void SaveRunResult(RunResult result) => _results.Add(result);
    }
}
