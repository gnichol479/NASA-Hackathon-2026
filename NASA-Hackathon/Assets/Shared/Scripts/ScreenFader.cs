using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NasaHack
{
    // Black overlay just in front of the main camera.
    // Works in VR and on desktop, with any render pipeline.
    public class ScreenFader : MonoBehaviour
    {
        static ScreenFader instance;
        Image overlay;

        // Created on first use and parented to the camera tagged MainCamera.
        public static ScreenFader Instance
        {
            get
            {
                if (instance == null) instance = Create();
                return instance;
            }
        }

        static ScreenFader Create()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("ScreenFader: no camera tagged MainCamera, skipping fade.");
                return null;
            }

            var root = new GameObject("ScreenFader", typeof(RectTransform));
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.02f);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = short.MaxValue;
            ((RectTransform)root.transform).sizeDelta = new Vector2(10f, 10f);

            var child = new GameObject("Overlay", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var fader = root.AddComponent<ScreenFader>();
            fader.overlay = child.AddComponent<Image>();
            fader.overlay.raycastTarget = false;
            fader.SetAlpha(0f);
            return fader;
        }

        public void SetAlpha(float alpha)
        {
            var c = Color.black;
            c.a = alpha;
            overlay.color = c;
            overlay.enabled = alpha > 0f;
        }

        // 1 = fully black, 0 = clear.
        public IEnumerator Fade(float to, float seconds)
        {
            float from = overlay.color.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetAlpha(to);
        }
    }
}
