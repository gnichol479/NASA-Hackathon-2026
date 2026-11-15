using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.IngenuityFlashback
{
    public enum IngenuityPartKind
    {
        RotorBlades,
        SolarPanel,
        LandingLegs
    }

    /// <summary>A simplified, grabbable assembly piece with a forgiving snap target.</summary>
    [DisallowMultipleComponent]
    public sealed class IngenuityAssemblyPart : MonoBehaviour
    {
        [SerializeField] private IngenuityPartKind kind;
        [SerializeField] private float snapRadius = 0.42f;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider partCollider;
        [SerializeField] private XRGrabInteractable grabInteractable;

        private IngenuityFlashbackSceneController owner;
        private Transform snapTarget;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool interactable = true;
        private bool snapped;

        public IngenuityPartKind Kind => kind;
        public bool IsSnapped => snapped;
        public bool IsInteractable => interactable && !snapped;
        public Transform SnapTarget => snapTarget;

        public void Configure(
            IngenuityFlashbackSceneController sceneOwner,
            IngenuityPartKind partKind,
            Transform target,
            float radius,
            Vector3 initialPosition,
            Quaternion initialRotation)
        {
            owner = sceneOwner;
            kind = partKind;
            snapTarget = target;
            snapRadius = Mathf.Max(0.05f, radius);
            spawnPosition = initialPosition;
            spawnRotation = initialRotation;
            body = GetComponent<Rigidbody>();
            partCollider = GetComponent<Collider>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            grabInteractable.throwOnDetach = false;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
            grabInteractable.colliders.Clear();
            if (partCollider != null)
                grabInteractable.colliders.Add(partCollider);
            grabInteractable.selectExited.RemoveListener(OnGrabReleased);
            grabInteractable.selectExited.AddListener(OnGrabReleased);
            SetInteractable(true);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (grabInteractable != null)
                grabInteractable.enabled = value && !snapped;
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (partCollider != null)
                partCollider.enabled = value && !snapped;
        }

        public bool BeginGrab(Transform holdAnchor)
        {
            if (!IsInteractable || holdAnchor == null)
                return false;

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            transform.SetParent(holdAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return true;
        }

        public bool ReleaseFromGrab()
        {
            if (!IsInteractable)
                return false;

            if (TrySnapAtCurrentPose())
                return true;

            transform.SetParent(null, true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
            }
            return false;
        }

        public bool TrySnapAtCurrentPose()
        {
            if (!IsInteractable || snapTarget == null)
                return false;

            if (Vector3.Distance(transform.position, snapTarget.position) > snapRadius)
                return false;

            SnapToTarget();
            return true;
        }

        /// <summary>Deterministic editor verification hook; uses the exact production snap path.</summary>
        public bool VerifySnapAtTarget()
        {
            if (!IsInteractable || snapTarget == null)
                return false;

            transform.position = snapTarget.position;
            transform.rotation = snapTarget.rotation;
            return TrySnapAtCurrentPose();
        }

        public void ResetPart()
        {
            snapped = false;
            if (transform.parent != null)
                transform.SetParent(null, true);

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (body != null)
            {
                ClearVelocityWhileDynamic();
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (partCollider != null)
                partCollider.enabled = interactable;
            if (grabInteractable != null)
                grabInteractable.enabled = interactable;
        }

        private void OnGrabReleased(SelectExitEventArgs _)
        {
            if (interactable && !snapped)
                TrySnapAtCurrentPose();
        }

        private void SnapToTarget()
        {
            snapped = true;
            transform.SetParent(snapTarget, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (body != null)
            {
                ClearVelocityWhileDynamic();
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (partCollider != null)
                partCollider.enabled = false;

            if (owner != null)
                owner.HandlePartSnapped(this);
        }

        private void ClearVelocityWhileDynamic()
        {
            if (body == null || body.isKinematic)
                return;

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
