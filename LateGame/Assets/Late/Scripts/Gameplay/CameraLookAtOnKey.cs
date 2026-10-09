using System.Collections.Generic;
using Late.Audio;
using UnityEngine;

namespace Late.Gameplay
{
    /// <summary>
    /// Secuencia final: la cámara gira hacia el ojo y cierra el campo de visión. Al terminar muestra
    /// la pantalla de buen o mal final según si la música seguía sonando al cruzar la meta.
    /// También se puede activar con una tecla (K) para probarla.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraLookAtOnKey : MonoBehaviour
    {
        [SerializeField] private KeyCode key = KeyCode.K;
        [Tooltip("Objetivo al que mira la cámara (el ojo). GameManager lo asigna al cargar cada nivel.")]
        [SerializeField] private Transform target;
        [Tooltip("Si está activo, solo gira en horizontal (mantiene la inclinación).")]
        [SerializeField] private bool yawOnly = false;
        [Tooltip("Segundos que tarda en girar. 0 = instantáneo.")]
        [SerializeField] private float rotateDuration = 0.15f;

        [Header("Campo de visión")]
        [SerializeField] private bool animateFov = true;
        [Tooltip("FOV al mirar al objetivo. Unity lo limita, así que valores <= 0 pueden no notarse.")]
        [SerializeField] private float targetFovWhenLooking = 0f;
        [Tooltip("Segundos que tarda en cambiar el FOV. 0 = instantáneo.")]
        [SerializeField] private float fovDuration = 2f;

        [Header("Al terminar la secuencia")]
        [Tooltip("[0] se activa si la música ya había terminado (mal final), [1] si seguía sonando (buen final).")]
        [SerializeField] private List<GameObject> activateOnFovReachedList = new List<GameObject>(2);
        [Tooltip("Para la música al terminar la secuencia.")]
        [SerializeField] private bool stopMusicOnFovReached = true;
        [Tooltip("Pausa en vez de parar.")]
        [SerializeField] private bool pauseInsteadOfStop = false;
        [Tooltip("Segundos que tarda la música en apagarse (0 = de golpe).")]
        [SerializeField] private float musicFadeOutSeconds = 1.25f;

        private Camera _cam;

        private bool _rotating;
        private float _rotateT;
        private Quaternion _rotateFrom;
        private Quaternion _rotateTo;
        private Quaternion _savedRotation;
        private bool _isLookingAtTarget;

        private bool _fovAnimating;
        private float _fovT;
        private float _fovFrom;
        private float _fovTo;
        private float _savedFov;
        private bool _pendingFinish;

        private bool? _cachedMusicWasPlaying;

        public bool IsLookingAtTarget => _isLookingAtTarget;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Deja la cámara lista para otra partida: cancela animaciones y toma la pose actual como la normal.
        /// </summary>
        public void ForceResetLookStateToCurrentPose()
        {
            _rotating = false;
            _rotateT = 0f;
            _fovAnimating = false;
            _fovT = 0f;
            _pendingFinish = false;
            _cachedMusicWasPlaying = null;

            _isLookingAtTarget = false;
            _savedRotation = transform.rotation;
            _savedFov = _cam.fieldOfView;
        }

        /// <summary>Cancela cualquier animación de FOV y fija este valor como el normal.</summary>
        public void ForceResetFov(float fov)
        {
            _fovAnimating = false;
            _pendingFinish = false;
            _fovT = 0f;

            _cam.fieldOfView = fov;
            _savedFov = fov;
        }

        /// <summary>Empieza a mirar al objetivo (si no lo estaba haciendo ya).</summary>
        public void StartLookAtTarget()
        {
            if (!_isLookingAtTarget) SetLooking(true);
        }

        /// <summary>Vuelve a la dirección de antes (si estaba mirando al objetivo).</summary>
        public void ReturnToPreviousLook()
        {
            if (_isLookingAtTarget) SetLooking(false);
        }

        /// <summary>
        /// Guarda si la música suena justo ahora (al cruzar la meta). La pantalla final se muestra
        /// después, cuando acaba la animación, pero con la decisión tomada en este momento.
        /// </summary>
        public void CacheMusicStateForFovReachedActivation()
        {
            _cachedMusicWasPlaying = IsMusicPlaying();
        }

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                SetLooking(!_isLookingAtTarget);
            }

            TickRotation();
            TickFov();
        }

        private void SetLooking(bool look)
        {
            if (target == null) return;

            _rotateFrom = transform.rotation;

            if (look)
            {
                _savedRotation = _rotateFrom;
                _savedFov = _cam.fieldOfView;

                Vector3 direction = target.position - transform.position;
                if (yawOnly) direction.y = 0f;
                if (direction.sqrMagnitude < 0.000001f) return;

                _rotateTo = Quaternion.LookRotation(direction.normalized, Vector3.up);
                _isLookingAtTarget = true;
                _pendingFinish = true;

                if (animateFov) StartFov(targetFovWhenLooking);
                else OnFovReached();
            }
            else
            {
                _rotateTo = _savedRotation;
                _isLookingAtTarget = false;
                _pendingFinish = false;

                if (animateFov) StartFov(_savedFov);
            }

            _rotateT = 0f;
            _rotating = true;
        }

        private void TickRotation()
        {
            if (!_rotating) return;

            _rotateT = rotateDuration <= 0f ? 1f : _rotateT + Time.deltaTime / rotateDuration;
            float t = Mathf.Clamp01(_rotateT);
            transform.rotation = Quaternion.Slerp(_rotateFrom, _rotateTo, t);
            if (t >= 1f) _rotating = false;
        }

        private void StartFov(float to)
        {
            _fovFrom = _cam.fieldOfView;
            _fovTo = to;
            _fovT = 0f;
            _fovAnimating = true;
        }

        private void TickFov()
        {
            if (!_fovAnimating) return;

            _fovT = fovDuration <= 0f ? 1f : _fovT + Time.deltaTime / fovDuration;
            float t = Mathf.Clamp01(_fovT);
            _cam.fieldOfView = Mathf.Lerp(_fovFrom, _fovTo, t);

            if (t >= 1f)
            {
                _fovAnimating = false;
                OnFovReached();
            }
        }

        /// <summary>Fin de la secuencia: muestra la pantalla de buen o mal final y apaga la música.</summary>
        private void OnFovReached()
        {
            if (!_pendingFinish) return;
            _pendingFinish = false;

            if (activateOnFovReachedList != null && activateOnFovReachedList.Count >= 2)
            {
                // Si nadie guardó el estado (p. ej. al pulsar la tecla a mano), se decide ahora.
                bool musicWasPlaying = _cachedMusicWasPlaying ?? IsMusicPlaying();
                _cachedMusicWasPlaying = null;

                int index = musicWasPlaying ? 1 : 0;
                for (int i = 0; i < activateOnFovReachedList.Count; i++)
                {
                    if (activateOnFovReachedList[i] != null) activateOnFovReachedList[i].SetActive(i == index);
                }
            }

            if (stopMusicOnFovReached && BackgroundMusicPlayer.Instance != null)
            {
                BackgroundMusicPlayer.Instance.FadeOut(musicFadeOutSeconds, pauseInsteadOfStop);
            }
        }

        private static bool IsMusicPlaying() =>
            BackgroundMusicPlayer.Instance != null && BackgroundMusicPlayer.Instance.IsPlaying;
    }
}
