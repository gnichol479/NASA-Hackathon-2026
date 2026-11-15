using UnityEngine;

namespace NasaHack
{
    // Simple ballistic fall for jettisoned boosters (no physics colliders needed).
    public class FallingDebris : MonoBehaviour
    {
        public Vector3 velocity;
        public Vector3 spin;            // degrees per second
        public float lifetime = 20f;

        void Start() => Destroy(gameObject, lifetime);

        void Update()
        {
            velocity += Physics.gravity * Time.deltaTime;
            velocity *= 1f - 0.05f * Time.deltaTime; // a bit of air drag
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(spin * Time.deltaTime, Space.World);
        }
    }
}
