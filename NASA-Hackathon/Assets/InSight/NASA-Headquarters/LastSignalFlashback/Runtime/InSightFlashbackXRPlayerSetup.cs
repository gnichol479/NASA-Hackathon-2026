using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace LastSignal.InSightFlashback
{
    /// <summary>Scene-local settings. XRI owns locomotion; TrackedPoseDriver alone owns head pose.</summary>
    [DisallowMultipleComponent]
    public sealed class InSightFlashbackXRPlayerSetup : MonoBehaviour
    {
        public const float DeviceEyeHeight = 1.65f;
        private XROrigin origin;
        private DynamicMoveProvider move;
        private ActionBasedSnapTurnProvider turn;
        private CharacterController capsule;
        private bool configured;
        public bool TrackingReady { get; private set; }

        public void Configure(GameObject rig, Camera camera)
        {
            origin = rig.GetComponentInChildren<XROrigin>(true);
            if (origin == null || camera == null) return;
            DisableTunnelingVignette(rig);
            origin.Camera = camera;
            origin.CameraYOffset = DeviceEyeHeight;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            camera.tag = "MainCamera";
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 80f;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;

            var pose = camera.GetComponent<TrackedPoseDriver>();
            if (pose != null)
            {
                pose.enabled = true;
                pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
                pose.ignoreTrackingState = false;
            }

            capsule = origin.Origin.GetComponent<CharacterController>();
            if (capsule != null)
            {
                capsule.enabled = true;
                capsule.height = DeviceEyeHeight;
                capsule.radius = .22f;
                capsule.skinWidth = .02f;
                capsule.center = new Vector3(0f, DeviceEyeHeight * .5f + .02f, 0f);
                capsule.stepOffset = .2f;
                capsule.minMoveDistance = 0f;
            }
            var driver = origin.Origin.GetComponent<CharacterControllerDriver>();
            if (driver != null)
            {
                driver.enabled = true;
                driver.minHeight = .5f;
                driver.maxHeight = 2.3f;
            }

            move = rig.GetComponentInChildren<DynamicMoveProvider>(true);
            if (move != null)
            {
                move.moveSpeed = 1.25f;
                move.enableStrafe = true;
                move.useGravity = true;
                move.leftHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
                move.rightHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
            }
            turn = rig.GetComponentInChildren<ActionBasedSnapTurnProvider>(true);
            if (turn != null)
            {
                turn.turnAmount = 30f;
                turn.debounceTime = .45f;
                turn.enableTurnAround = false;
            }
            // Starter controller managers arbitrate move / turn / teleport actions and suspend
            // locomotion while manipulating objects or scrolling UI. Keep their references intact.
            foreach (var manager in rig.GetComponentsInChildren<ActionBasedControllerManager>(true))
            {
                manager.smoothMotionEnabled = manager.name.Contains("Left");
                manager.smoothTurnEnabled = false;
            }
            foreach (var smoothTurn in rig.GetComponentsInChildren<ContinuousTurnProviderBase>(true))
                smoothTurn.enabled = false;
            foreach (var actions in rig.GetComponentsInChildren<InputActionManager>(true))
                actions.enabled = true;

            if (!Application.isPlaying && origin.CameraFloorOffsetObject != null)
            {
                // Edit-mode preview only. XROrigin sets this to zero for live floor tracking.
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.up * DeviceEyeHeight;
                camera.transform.localPosition = Vector3.zero;
                camera.transform.localRotation = Quaternion.identity;
            }
            configured = true;
            SetLocomotion(!Application.isPlaying);
        }

        private static void DisableTunnelingVignette(GameObject rig)
        {
            // The shared XRI rig includes the Starter Assets tunneling mesh. Its locomotion
            // providers close the view to a circle during movement, so disable that visual for
            // this scene while leaving the rig's movement and tracked head pose untouched.
            foreach (Transform child in rig.GetComponentsInChildren<Transform>(true))
            {
                if (child != rig.transform && child.name == "TunnelingVignette")
                    child.gameObject.SetActive(false);
            }
        }

        private IEnumerator Start()
        {
            if (!configured)
            {
                var savedOrigin = GetComponentInChildren<XROrigin>(true);
                if (savedOrigin != null) Configure(gameObject, savedOrigin.Camera);
            }
            if (!configured) yield break;
            float began = Time.realtimeSinceStartup;
            bool warned = false;
            // Do not integrate gravity against a zero-height, untracked camera at startup.
            while (!HasTrackedHead())
            {
                if (!warned && Time.realtimeSinceStartup - began > 8f)
                {
                    warned = true;
                    Debug.LogWarning("[InSight XR] Waiting for a tracked headset. Put on the Quest in Link, focus Game view, and check OpenXR Oculus Touch is enabled. " + DescribeStatus(), this);
                }
                yield return null;
            }
            // Allow TrackedPoseDriver and XROrigin to apply the first tracked pose and floor mode.
            yield return null;
            bool capsuleEnabled = capsule != null && capsule.enabled;
            if (capsule != null) capsule.enabled = false;
            Vector3 forward = Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > .01f)
                origin.MatchOriginUpCameraForward(Vector3.up, Vector3.forward);
            Vector3 headPosition = origin.Camera.transform.position;
            // Align only horizontal position; preserve the measured physical floor height.
            origin.MoveCameraToWorldLocation(new Vector3(InSightFacilityLayout.Arrival.x,
                headPosition.y, InSightFacilityLayout.Arrival.z));
            if (capsule != null) capsule.enabled = capsuleEnabled;
            TrackingReady = true;
            SetLocomotion(true);
            Debug.Log("[InSight XR] Tracking ready. " + DescribeStatus(), this);
        }

        private static bool HasTrackedHead()
        {
            foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
                if (device is XRHMD head && head.isTracked.isPressed &&
                    (head.trackingState.ReadValue() & (int)(InputTrackingState.Position | InputTrackingState.Rotation)) ==
                    (int)(InputTrackingState.Position | InputTrackingState.Rotation)) return true;
            return false;
        }

        private void SetLocomotion(bool enabled)
        {
            if (move != null) move.enabled = enabled;
            if (turn != null) turn.enabled = enabled;
        }

        public string DescribeStatus()
        {
            if (origin == null) return "XR Origin missing.";
            string devices = string.Join(", ", UnityEngine.InputSystem.InputSystem.devices);
            return $"ready={TrackingReady}, floorMode={origin.CurrentTrackingOriginMode}, " +
                $"cameraWorld={origin.Camera.transform.position:F3}, headLocal={origin.Camera.transform.localPosition:F3}, " +
                $"headYaw={origin.Camera.transform.eulerAngles.y:F1}, origin={origin.transform.position:F3}, " +
                $"moveEnabled={move != null && move.leftHandMoveAction.action != null && move.leftHandMoveAction.action.enabled}, " +
                $"turnEnabled={turn != null && turn.rightHandSnapTurnAction.action != null && turn.rightHandSnapTurnAction.action.enabled}, devices=[{devices}]";
        }
    }
}
