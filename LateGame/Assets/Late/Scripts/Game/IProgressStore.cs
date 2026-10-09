namespace Late.Game
{
    /// <summary>
    /// Dónde se guarda el progreso del jugador. Hoy solo en memoria; en la fase 4 se guardará en
    /// SQLite y en la fase 6 se sincronizará con la API, sin tocar el resto del juego.
    /// </summary>
    public interface IProgressStore
    {
        /// <summary>Índice del último nivel desbloqueado (0 = solo el primero).</summary>
        int LoadHighestUnlockedRun();

        void SaveHighestUnlockedRun(int runIndex);

        /// <summary>Guarda el resultado de una partida (tiempo y si se ganó a la música).</summary>
        void SaveRunResult(RunResult result);
    }
}
