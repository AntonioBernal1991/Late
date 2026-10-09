using System.Collections;
using UnityEngine;

namespace Late.Game
{
    /// <summary>
    /// Pantalla negra que tapa los cambios de nivel. Se muestra de golpe y desaparece con un fundido.
    /// </summary>
    public class LoadingOverlay
    {
        private readonly CanvasGroup _group;
        private readonly MonoBehaviour _runner;
        private Coroutine _fadeRoutine;

        /// <param name="group">CanvasGroup de una imagen negra a pantalla completa. Puede ser null (sin overlay).</param>
        /// <param name="runner">Componente con el que lanzar la corrutina del fundido.</param>
        public LoadingOverlay(CanvasGroup group, MonoBehaviour runner)
        {
            _group = group;
            _runner = runner;
        }

        public void ShowImmediate()
        {
            if (_group == null) return;

            StopFade();
            _group.gameObject.SetActive(true);
            _group.alpha = 1f;
            SetBlocking(true);
        }

        public void FadeOut(float seconds)
        {
            if (_group == null) return;

            StopFade();
            if (seconds <= 0f)
            {
                Hide();
                return;
            }
            _fadeRoutine = _runner.StartCoroutine(FadeOutRoutine(seconds));
        }

        private IEnumerator FadeOutRoutine(float seconds)
        {
            float startAlpha = _group.alpha;

            // Sigue bloqueando la entrada mientras se desvanece.
            SetBlocking(true);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(startAlpha, 0f, t / seconds);
                yield return null;
            }

            Hide();
            _fadeRoutine = null;
        }

        private void Hide()
        {
            _group.alpha = 0f;
            SetBlocking(false);
            _group.gameObject.SetActive(false);
        }

        private void SetBlocking(bool blocking)
        {
            _group.blocksRaycasts = blocking;
            _group.interactable = blocking;
        }

        private void StopFade()
        {
            if (_fadeRoutine == null) return;
            _runner.StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }
}
