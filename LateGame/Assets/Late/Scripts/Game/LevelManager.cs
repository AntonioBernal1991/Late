using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Late.Game
{
    /// <summary>
    /// Progresión de niveles. Al principio solo está desbloqueado el primero; cada vez que se gana
    /// a la música en un nivel se desbloquea el siguiente. Pinta los botones del menú en verde
    /// (desbloqueado) o rojo (bloqueado).
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Botones de nivel (índice = nivel)")]
        [SerializeField] private List<Button> runButtons = new List<Button>();

        [Header("Color del texto de los botones")]
        [SerializeField] private Color lockedTextColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color unlockedTextColor = new Color(0f, 1f, 0f, 1f);

        [Header("Depuración")]
        [SerializeField] private bool log = false;

        private IProgressStore _store = new InMemoryProgressStore();
        private int _unlockedUpToIndex;

        /// <summary>Cambia dónde se guarda el progreso (SQLite, API...). Recarga lo desbloqueado.</summary>
        public void SetProgressStore(IProgressStore store)
        {
            _store = store ?? new InMemoryProgressStore();
            _unlockedUpToIndex = _store.LoadHighestUnlockedRun();
            RefreshButtons();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _unlockedUpToIndex = _store.LoadHighestUnlockedRun();
        }

        private void OnEnable() => GameManager.RunFinished += OnRunFinished;

        private void OnDisable() => GameManager.RunFinished -= OnRunFinished;

        private void Start() => RefreshButtons();

        private void OnValidate()
        {
            // Mantener los colores al día mientras se edita.
            if (!Application.isPlaying) RefreshButtons();
        }

        public bool IsUnlocked(int runIndex) => runIndex >= 0 && runIndex <= _unlockedUpToIndex;

        private void OnRunFinished(RunResult result)
        {
            if (result.RunIndex < 0) return;

            _store.SaveRunResult(result);

            if (result.BeatTheMusic && result.RunIndex + 1 > _unlockedUpToIndex)
            {
                _unlockedUpToIndex = result.RunIndex + 1;
                _store.SaveHighestUnlockedRun(_unlockedUpToIndex);
            }

            if (log) Debug.Log($"[LevelManager] {result}. Desbloqueado hasta Run{_unlockedUpToIndex}.", this);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < runButtons.Count; i++)
            {
                Button button = runButtons[i];
                if (button == null) continue;

                bool unlocked = IsUnlocked(i);
                button.interactable = unlocked;

                if (!TrySetButtonTextColor(button, unlocked ? unlockedTextColor : lockedTextColor) && log)
                {
                    Debug.LogWarning($"[LevelManager] El botón '{button.name}' no tiene texto que colorear.", button);
                }
            }
        }

        private static bool TrySetButtonTextColor(Button button, Color color)
        {
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.color = color;
                return true;
            }

            // Cualquier Graphic que no sea el fondo ni una imagen: cubre TextMeshPro sin depender de él.
            foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == button.targetGraphic || graphic is Image) continue;
                graphic.color = color;
                return true;
            }
            return false;
        }
    }
}
