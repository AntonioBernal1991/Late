using System;
using Late.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Late.Online
{
    /// <summary>Piezas de interfaz con el estilo del menú (terminal: monoespaciada y verde sobre negro).</summary>
    public class UiKit
    {
        public static readonly Color Green = new Color(0.2f, 1f, 0.4f);
        public static readonly Color Dim = new Color(0.55f, 0.6f, 0.55f);
        public static readonly Color Red = new Color(1f, 0.35f, 0.35f);
        public static readonly Color Background = Color.black; // opaco: el menú de debajo no debe distraer
        public static readonly Color FieldBackground = new Color(0.12f, 0.14f, 0.12f, 1f);

        public const float TextSize = 40f;
        public const float SmallTextSize = 32f;
        public const float TitleSize = 48f;

        private readonly TMP_FontAsset _font;

        public UiKit(TMP_FontAsset font) => _font = font;

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Contenedor vertical u horizontal que coloca a sus hijos solo.</summary>
        public static RectTransform Stack(Transform parent, string name, bool vertical, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            RectTransform rect = Rect(parent, name);
            HorizontalOrVerticalLayoutGroup layout = vertical
                ? rect.gameObject.AddComponent<VerticalLayoutGroup>()
                : (HorizontalOrVerticalLayoutGroup)rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = vertical;
            layout.childForceExpandHeight = false;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Dentro de otro contenedor, su ancho lo decide el padre; suelto, se ajusta a su contenido.
            if (!vertical && parent.GetComponent<LayoutGroup>() == null) fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        public TextMeshProUGUI Text(Transform parent, string text, float size = TextSize, Color? color = null)
        {
            RectTransform rect = Rect(parent, "Text");
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = _font;
            tmp.fontSize = size;
            tmp.color = color ?? Green;
            tmp.text = text;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Texto que cambia solo con el idioma.</summary>
        public TextMeshProUGUI Localized(Transform parent, string key, float size = TextSize, Color? color = null)
        {
            TextMeshProUGUI tmp = Text(parent, "", size, color);
            tmp.gameObject.AddComponent<LocalizedText>().Key = key;
            return tmp;
        }

        /// <summary>Botón de texto, como las entradas del menú. <paramref name="key"/> es una clave de idioma o null.</summary>
        public Button Button(Transform parent, string key, Action onClick, float size = TextSize, string literal = null)
        {
            TextMeshProUGUI label = key != null ? Localized(parent, key, size) : Text(parent, literal ?? "", size);
            label.raycastTarget = true;
            label.enableWordWrapping = false;
            var button = label.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 1f, 0.8f);
            colors.pressedColor = new Color(0.5f, 0.7f, 0.5f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public TMP_InputField Input(Transform parent, string placeholderKey, TMP_InputField.ContentType contentType)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = FieldBackground;
            var field = go.GetComponent<TMP_InputField>();
            field.contentType = contentType;
            field.pointSize = TextSize;
            field.textViewport.offsetMin = new Vector2(24f, 8f);
            field.textViewport.offsetMax = new Vector2(-24f, -8f);
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = _font;
                text.fontSize = TextSize;
                text.color = Green;
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var placeholder = (TMP_Text)field.placeholder;
            placeholder.color = Dim;
            placeholder.gameObject.AddComponent<LocalizedText>().Key = placeholderKey;
            go.AddComponent<LayoutElement>().preferredHeight = 90f;
            return field;
        }

        public static void Spacer(Transform parent, float height)
        {
            RectTransform rect = Rect(parent, "Spacer");
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }
    }
}
