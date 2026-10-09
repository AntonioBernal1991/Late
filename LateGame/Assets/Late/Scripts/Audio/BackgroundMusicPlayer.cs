using System.Collections;
using UnityEngine;

namespace Late.Audio
{
    /// <summary>
    /// Reproduce la música del nivel. En Late la canción es el cronómetro: si termina antes de llegar
    /// a la meta, la partida cuenta como mal acabada. Hay una sola instancia, que sobrevive a los
    /// cambios de escena; se accede a ella con <see cref="Instance"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusicPlayer : MonoBehaviour
    {
        public static BackgroundMusicPlayer Instance { get; private set; }

        // Por si el "Enter Play Mode" está configurado sin recarga de dominio.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [Header("Música")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] [Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private bool loop = true;
        [Tooltip("Segundo de la canción desde el que empieza a sonar. Útil para empezar en el 'drop'.")]
        [SerializeField] [Min(0f)] private float startAtSeconds = 0f;
        [Tooltip("Si está activo, suena nada más cargar. Si no, hay que llamar a PlayMusic (lo hace GameManager).")]
        [SerializeField] private bool autoPlayOnAwake = false;

        [Header("Ciclo de vida")]
        [Tooltip("Sigue sonando al cargar otras escenas y evita duplicados.")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        private AudioSource _source;
        private Coroutine _fadeRoutine;

        public bool IsPlaying => Source != null && Source.isPlaying;

        private AudioSource Source => _source != null ? _source : (_source = GetComponent<AudioSource>());

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                if (Instance != null && Instance != this)
                {
                    // Si la instancia que ya existe no tiene canción y esta sí, se la pasa.
                    if (Instance.musicClip == null && musicClip != null)
                    {
                        Instance.SetMusicClip(musicClip);
                    }
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            Source.playOnAwake = false;
            ApplySourceSettings();

            // No pisar la canción si se asignó directamente en el AudioSource.
            if (musicClip == null && Source.clip != null)
            {
                musicClip = Source.clip;
            }
            else if (musicClip != null)
            {
                Source.clip = musicClip;
            }

            if (autoPlayOnAwake)
            {
                PlayMusic(restart: true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            if (Source == null) return;

            ApplySourceSettings();
            if (musicClip != null)
            {
                Source.clip = musicClip;
            }
        }

        public void SetMusicClip(AudioClip clip)
        {
            if (clip == null) return;
            musicClip = clip;
            Source.clip = clip;
        }

        public void SetStartAtSeconds(float seconds)
        {
            startAtSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>Empieza a sonar. Con <paramref name="restart"/> vuelve al segundo de inicio configurado.</summary>
        public void PlayMusic(bool restart = true)
        {
            StopFade();
            ApplySourceSettings();

            if (Source.clip == null && musicClip != null)
            {
                Source.clip = musicClip;
            }
            if (Source.clip == null) return;

            if (restart)
            {
                Source.time = startAtSeconds > 0f && Source.clip.length > 0f
                    ? Mathf.Clamp(startAtSeconds, 0f, Mathf.Max(0f, Source.clip.length - 0.01f))
                    : 0f;
            }

            Source.Play();
        }

        public void StopMusic()
        {
            StopFade();
            Source.Stop();
        }

        /// <summary>Baja el volumen hasta 0 en <paramref name="seconds"/> y después para o pausa.</summary>
        public void FadeOut(float seconds, bool pauseInsteadOfStop = false)
        {
            StopFade();
            _fadeRoutine = StartCoroutine(FadeOutRoutine(seconds, pauseInsteadOfStop));
        }

        private IEnumerator FadeOutRoutine(float seconds, bool pause)
        {
            float startVolume = Source.volume;
            float duration = Mathf.Max(0f, seconds);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }

            Source.volume = 0f;
            if (pause) Source.Pause();
            else Source.Stop();
            _fadeRoutine = null;
        }

        private void StopFade()
        {
            if (_fadeRoutine == null) return;
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private void ApplySourceSettings()
        {
            Source.loop = loop;
            Source.volume = volume;
        }
    }
}
