namespace Late.Game
{
    /// <summary>
    /// Resultado de una partida. Es lo que se guarda en local y, más adelante, lo que se envía al ranking.
    /// </summary>
    public readonly struct RunResult
    {
        /// <summary>Índice del nivel (Run0 = 0, Run1 = 1...).</summary>
        public int RunIndex { get; }

        /// <summary>Segundos desde que empezó la partida hasta cruzar la meta.</summary>
        public float ElapsedSeconds { get; }

        /// <summary>True si se llegó a la meta antes de que terminara la música.</summary>
        public bool BeatTheMusic { get; }

        public RunResult(int runIndex, float elapsedSeconds, bool beatTheMusic)
        {
            RunIndex = runIndex;
            ElapsedSeconds = elapsedSeconds;
            BeatTheMusic = beatTheMusic;
        }

        public override string ToString() =>
            $"Run{RunIndex}: {ElapsedSeconds:0.00}s, {(BeatTheMusic ? "antes" : "después")} de la música";
    }
}
