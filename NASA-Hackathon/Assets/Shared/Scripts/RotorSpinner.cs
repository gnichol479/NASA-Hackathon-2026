using UnityEngine;

namespace NasaHack
{
    // Spins Ingenuity's two coaxial rotors in opposite directions around the mast.
    // Finds "rotors_01" / "rotors_02" in the NASA model by name. Optional wobble for a damaged blade.
    public class RotorSpinner : MonoBehaviour
    {
        public Transform upperRotor;
        public Transform lowerRotor;
        [Tooltip("Visual spin at full speed, degrees per second. The real 2,400 rpm would just strobe on screen.")]
        public float fullSpeed = 1500f;
        [Tooltip("Seconds to reach full speed from rest.")]
        public float spinUpSeconds = 2.5f;
        [Tooltip("Tilt of the upper rotor in degrees, oscillating. 0 = healthy.")]
        public float wobbleDegrees;

        float speed;
        float target;
        float appliedTilt;
        Vector3 axisLocal = Vector3.up;
        Vector3 tiltAxisLocal = Vector3.right;

        public float SpeedFraction => fullSpeed > 0f ? speed / fullSpeed : 0f;

        void Awake()
        {
            if (upperRotor == null) upperRotor = FindDeep(transform, "rotors_01");
            if (lowerRotor == null) lowerRotor = FindDeep(transform, "rotors_02");
            var top = FindDeep(transform, "parts_01");
            var bottom = FindDeep(transform, "parts_02");
            var axisWorld = top != null && bottom != null ? (top.position - bottom.position).normalized : transform.up;
            axisLocal = transform.InverseTransformDirection(axisWorld);
            var perp = Vector3.Cross(axisLocal, Vector3.forward);
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(axisLocal, Vector3.right);
            tiltAxisLocal = perp.normalized;
        }

        // 0 = stopped, 1 = full speed.
        public void SetTarget(float fraction) => target = Mathf.Clamp01(fraction) * fullSpeed;

        void Update()
        {
            float accel = fullSpeed / Mathf.Max(0.1f, spinUpSeconds);
            speed = Mathf.MoveTowards(speed, target, accel * Time.deltaTime);
            var axis = transform.TransformDirection(axisLocal);
            float angle = speed * Time.deltaTime;
            if (upperRotor != null) upperRotor.RotateAround(upperRotor.position, axis, angle);
            if (lowerRotor != null) lowerRotor.RotateAround(lowerRotor.position, axis, -angle);

            // Wobble: oscillating tilt on the upper rotor, removed cleanly when set back to 0.
            float tilt = wobbleDegrees * Mathf.Sin(Time.time * 23f) * Mathf.Clamp01(SpeedFraction * 2f);
            if (upperRotor != null && !Mathf.Approximately(tilt, appliedTilt))
                upperRotor.RotateAround(upperRotor.position, transform.TransformDirection(tiltAxisLocal), tilt - appliedTilt);
            appliedTilt = tilt;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
