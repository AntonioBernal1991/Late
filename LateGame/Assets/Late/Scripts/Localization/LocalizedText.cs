using TMPro;
using UnityEngine;

namespace Late.Localization
{
    /// <summary>Pone en el TMP_Text de su objeto el texto de <see cref="Key"/> en el idioma actual.</summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text _text;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                // En el editor la escena conserva el texto original; se traduce al jugar.
                if (Application.isPlaying) Refresh();
            }
        }

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_text == null) _text = GetComponent<TMP_Text>();
            _text.text = Localization.Get(key);
        }
    }
}
