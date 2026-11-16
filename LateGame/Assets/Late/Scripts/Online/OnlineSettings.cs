using TMPro;
using UnityEngine;

namespace Late.Online
{
    /// <summary>
    /// Configuración de la parte online. El asset vive en Resources/LateOnlineSettings para cargarlo
    /// sin referencias desde la escena.
    /// </summary>
    [CreateAssetMenu(menuName = "Late/Ajustes online", fileName = ResourceName)]
    public class OnlineSettings : ScriptableObject
    {
        public const string ResourceName = "LateOnlineSettings";
        private const string DefaultServerUrl = "http://localhost:5080";

        [Tooltip("URL de la API. En el editor, localhost; en un móvil, la IP del equipo con Docker " +
                 "(p. ej. http://192.168.1.20:5080) o la URL HTTPS del servidor publicado.")]
        [SerializeField] private string serverUrl = DefaultServerUrl;

        [Tooltip("Segundos entre reintentos de envío mientras haya partidas pendientes.")]
        [Min(10f)]
        [SerializeField] private float retrySeconds = 60f;

        [Tooltip("La barra online solo se ve mientras este objeto de la escena está activo.")]
        [SerializeField] private string menuObjectName = "Menu";

        [Tooltip("Pantallas que tapan el menú aunque esté activo (la portada): mientras alguna esté activa, la barra no se ve.")]
        [SerializeField] private string[] hiddenWhileActive = { "Intro Image" };

        [Tooltip("Fuente de la interfaz online (la misma que el menú).")]
        [SerializeField] private TMP_FontAsset font;

        public string ServerUrl => string.IsNullOrWhiteSpace(serverUrl) ? DefaultServerUrl : serverUrl.TrimEnd('/');
        public float RetrySeconds => retrySeconds;
        public string MenuObjectName => menuObjectName;
        public string[] HiddenWhileActive => hiddenWhileActive ?? System.Array.Empty<string>();
        public TMP_FontAsset Font => font != null ? font : TMP_Settings.defaultFontAsset;

        /// <summary>El asset de Resources, o uno con los valores por defecto si no existe.</summary>
        public static OnlineSettings Load()
        {
            var settings = Resources.Load<OnlineSettings>(ResourceName);
            return settings != null ? settings : CreateInstance<OnlineSettings>();
        }
    }
}
