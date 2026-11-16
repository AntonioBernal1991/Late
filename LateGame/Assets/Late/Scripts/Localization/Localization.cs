using System;
using UnityEngine;

namespace Late.Localization
{
    public enum Language
    {
        Spanish,
        English
    }

    /// <summary>
    /// Idioma actual del juego (R07). Los textos están en <see cref="Strings"/>; los componentes
    /// <see cref="LocalizedText"/> se actualizan solos al cambiar de idioma.
    /// Dónde se guarda la elección lo decide quien arranca el juego (<see cref="Saver"/>).
    /// </summary>
    public static class Localization
    {
        private static Language? _current;

        public static event Action Changed;

        /// <summary>Guarda el idioma elegido (SQLite). Si es null, la elección solo dura la sesión.</summary>
        public static Action<Language> Saver { get; set; }

        /// <summary>Idioma actual. Si nadie lo ha fijado, el del sistema (español si el móvil está en español).</summary>
        public static Language Current
        {
            get => _current ?? FromSystem(Application.systemLanguage);
            set
            {
                if (_current == value) return;
                _current = value;
                Saver?.Invoke(value);
                Changed?.Invoke();
            }
        }

        /// <summary>Fija el idioma guardado al arrancar, sin volver a guardarlo.</summary>
        public static void Load(Language language)
        {
            _current = language;
            Changed?.Invoke();
        }

        public static Language FromSystem(SystemLanguage system) =>
            system == SystemLanguage.Spanish || system == SystemLanguage.Catalan || system == SystemLanguage.Basque
                ? Language.Spanish
                : Language.English;

        public static string Code(Language language) => language == Language.Spanish ? "es" : "en";

        public static bool TryParse(string code, out Language language)
        {
            switch (code)
            {
                case "es": language = Language.Spanish; return true;
                case "en": language = Language.English; return true;
                default: language = default; return false;
            }
        }

        /// <summary>Texto de <paramref name="key"/> en el idioma actual. Si falta, devuelve la clave entre corchetes.</summary>
        public static string Get(string key) => Strings.Get(key, Current) ?? $"[{key}]";

        /// <summary>Texto con huecos {0}, {1}...</summary>
        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
