using UnityEngine;

namespace NasaHack
{
    // Makes a model look covered in Mars dust: tints every material toward dust color
    // and makes it less shiny. 0 = clean (flashback), ~0.5 = years on Mars.
    public class DustCover : MonoBehaviour
    {
        [Range(0f, 1f)] public float amount = 0.5f;
        public Color dust = new Color(0.72f, 0.5f, 0.34f);

        // glTFast, URP Lit, Built-in
        static readonly string[] ColorProps = { "baseColorFactor", "_BaseColor", "_Color" };
        static readonly string[] RoughProps = { "roughnessFactor" };
        static readonly string[] SmoothProps = { "_Smoothness", "_Glossiness" };

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)
                {
                    foreach (var p in ColorProps)
                    {
                        if (!m.HasProperty(p)) continue;
                        m.SetColor(p, Color.Lerp(m.GetColor(p), dust, amount));
                        break;
                    }
                    foreach (var p in RoughProps)
                        if (m.HasProperty(p)) m.SetFloat(p, Mathf.Lerp(m.GetFloat(p), 1f, amount));
                    foreach (var p in SmoothProps)
                        if (m.HasProperty(p)) m.SetFloat(p, Mathf.Lerp(m.GetFloat(p), 0f, amount));
                }
            }
        }
    }
}
