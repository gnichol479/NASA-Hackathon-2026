using UnityEngine;

namespace NasaHack
{
    // Keeps a panel turned toward the player (around the vertical axis only).
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            UiFactory.FacePoint(transform, cam.transform.position);
        }
    }
}
