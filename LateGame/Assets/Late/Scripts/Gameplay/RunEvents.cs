using System;
using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Eventos de la partida que lanza el gameplay y escucha el flujo del juego (GameManager).
    /// Así el gameplay no necesita conocer al GameManager.
    /// </summary>
    public static class RunEvents
    {
        /// <summary>El jugador ha cruzado la meta del laberinto.</summary>
        public static event Action EndReached;

        public static void RaiseEndReached() => EndReached?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => EndReached = null;
    }
}
