using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Detecta un deslizamiento horizontal con el dedo (o con el ratón en el editor) para girar en móvil.
    /// </summary>
    public class SwipeDetector
    {
        private bool _tracking;
        private Vector2 _startPos;
        private float _startTime;

        public float MinPixels { get; set; } = 80f;
        /// <summary>El desplazamiento horizontal tiene que ser al menos esto por el vertical.</summary>
        public float HorizontalDominance { get; set; } = 1.5f;
        /// <summary>Duración máxima del gesto en segundos. 0 = sin límite.</summary>
        public float MaxTime { get; set; } = 0.5f;
        public bool MouseInEditor { get; set; } = true;

        /// <summary>Lee la entrada de este frame: -1 = swipe a la izquierda, 1 = a la derecha, 0 = nada.</summary>
        public int Tick()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) Begin(touch.position);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) return End(touch.position);
                return 0;
            }

#if UNITY_EDITOR
            if (MouseInEditor)
            {
                if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition);
                else if (Input.GetMouseButtonUp(0)) return End(Input.mousePosition);
            }
#endif
            return 0;
        }

        public void Cancel()
        {
            _tracking = false;
        }

        private void Begin(Vector2 position)
        {
            _tracking = true;
            _startPos = position;
            _startTime = Time.unscaledTime;
        }

        private int End(Vector2 position)
        {
            if (!_tracking) return 0;
            _tracking = false;

            if (MaxTime > 0f && Time.unscaledTime - _startTime > MaxTime) return 0;

            Vector2 delta = position - _startPos;
            if (Mathf.Abs(delta.x) < Mathf.Max(1f, MinPixels)) return 0;
            if (Mathf.Abs(delta.x) < HorizontalDominance * Mathf.Abs(delta.y)) return 0;

            return delta.x < 0f ? -1 : 1;
        }
    }
}
