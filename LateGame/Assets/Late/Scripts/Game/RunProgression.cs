namespace Late.Game
{
    /// <summary>
    /// Regla de desbloqueo de niveles, separada de <see cref="LevelManager"/> para poder probarla sin escena.
    /// </summary>
    public static class RunProgression
    {
        /// <summary>
        /// Último nivel desbloqueado después de una partida. Ganar a la música desbloquea el siguiente;
        /// perder, o ganar un nivel anterior, no cambia nada. Nunca se pierde un nivel ya desbloqueado.
        /// </summary>
        public static int HighestUnlockedAfter(int highestUnlocked, RunResult result)
        {
            if (result.RunIndex < 0 || !result.BeatTheMusic) return highestUnlocked;
            return System.Math.Max(highestUnlocked, result.RunIndex + 1);
        }

        public static bool IsUnlocked(int highestUnlocked, int runIndex) => runIndex >= 0 && runIndex <= highestUnlocked;
    }
}
