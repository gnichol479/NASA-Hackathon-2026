using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace NasaHack
{
    // Builds world-space UI panels that XR controller rays can click.
    // Panels grow to fit their text. 1000 px = 1 m.
    public static class UiFactory
    {
        public const float PixelsPerMeter = 1000f;
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.1f, 0.9f);
        public static readonly Color Accent = new Color(1f, 0.55f, 0.2f);
        public static readonly Color TextColor = new Color(0.95f, 0.93f, 0.9f);

        // Vertical stack: add title, body, buttons as children in order.
        public static RectTransform CreatePanel(string name, Transform parent, float widthPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(widthPx, 100f);
            rect.localScale = Vector3.one / PixelsPerMeter;

            go.AddComponent<Image>().color = PanelColor;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        public static TextMeshProUGUI AddText(Transform parent, string name, string text, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = TextColor;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button AddButton(Transform parent, string name, string label, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = Accent;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var size = go.AddComponent<LayoutElement>();
            size.minHeight = 84f;
            size.preferredHeight = 84f;

            labelText = AddText(go.transform, "Label", label, 36f, FontStyles.Bold);
            labelText.color = new Color(0.08f, 0.06f, 0.05f);
            labelText.alignment = TextAlignmentOptions.Center;
            var r = labelText.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return button;
        }

        // Rotate a panel so its text reads correctly from a point (e.g. the player's head).
        public static void FacePoint(Transform panel, Vector3 viewer)
        {
            var dir = panel.position - viewer;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) panel.rotation = Quaternion.LookRotation(dir);
        }
    }
}
