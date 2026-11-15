using UnityEngine;
using UnityEngine.XR;

namespace LastSignal.InSightFlashback
{
    /// <summary>
    /// Lightweight input adapter for UnityEngine.XR devices, plus a keyboard/mouse Editor fallback.
    /// It deliberately depends on no XR Interaction Toolkit package so it can live in this folder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InSightFlashbackInputRig : MonoBehaviour
    {
        private const float RayLength = 7f;
        private const float MoveSpeed = 1.25f;
        private const float SnapTurnDegrees = 30f;

        private InSightFlashbackSceneController owner;
        private Camera playerCamera;
        private CharacterController characterController;
        private Transform leftPose;
        private Transform rightPose;
        private Transform leftHoldAnchor;
        private Transform rightHoldAnchor;
        private LineRenderer leftRay;
        private LineRenderer rightRay;
        private InSightAssemblyPart leftHeldPart;
        private InSightAssemblyPart rightHeldPart;
        private InSightFlashbackWorldButton leftHoveredButton;
        private InSightFlashbackWorldButton rightHoveredButton;
        private bool leftGripWasDown;
        private bool rightGripWasDown;
        private bool leftTriggerWasDown;
        private bool rightTriggerWasDown;
        private bool editorMouseWasDown;
        private bool hasHeadTracking;
        private float nextSnapTurnTime;
        private Material rayMaterial;

        public void Configure(
            InSightFlashbackSceneController sceneOwner,
            Camera sceneCamera,
            CharacterController character)
        {
            owner = sceneOwner;
            playerCamera = sceneCamera;
            characterController = character;

            leftPose = CreatePose("Left Controller Pose", new Vector3(-0.22f, -0.18f, 0.42f));
            rightPose = CreatePose("Right Controller Pose", new Vector3(0.22f, -0.18f, 0.42f));
            leftHoldAnchor = CreateHoldAnchor(leftPose, "Left Grab Hold Anchor");
            rightHoldAnchor = CreateHoldAnchor(rightPose, "Right Grab Hold Anchor");

            rayMaterial = new Material(Shader.Find("Sprites/Default"));
            leftRay = CreateRay(leftPose, "Left Controller Ray", new Color(0.2f, 0.78f, 1f, 0.9f));
            rightRay = CreateRay(rightPose, "Right Controller Ray", new Color(1f, 0.63f, 0.2f, 0.9f));
        }

        private Transform CreatePose(string poseName, Vector3 fallbackLocalPosition)
        {
            var poseObject = new GameObject(poseName);
            poseObject.transform.SetParent(transform, false);
            poseObject.transform.localPosition = fallbackLocalPosition;
            GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hand.name = "Controller Hand Marker";
            hand.transform.SetParent(poseObject.transform, false);
            hand.transform.localScale = Vector3.one * 0.075f;
            Collider handCollider = hand.GetComponent<Collider>();
            if (handCollider != null)
                handCollider.enabled = false;
            return poseObject.transform;
        }

        private static Transform CreateHoldAnchor(Transform pose, string anchorName)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetParent(pose, false);
            anchor.localPosition = new Vector3(0f, 0f, 0.28f);
            anchor.localRotation = Quaternion.identity;
            return anchor;
        }

        private LineRenderer CreateRay(Transform pose, string rayName, Color color)
        {
            var rayObject = new GameObject(rayName);
            rayObject.transform.SetParent(pose, false);
            var line = rayObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.012f;
            line.endWidth = 0.004f;
            line.material = rayMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.18f);
            line.numCapVertices = 2;
            return line;
        }

        private void Update()
        {
            if (owner == null || playerCamera == null)
                return;

            UpdateHeadPose();
            UpdateMovement();

            InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool hasLeftController = leftDevice.isValid;
            bool hasRightController = rightDevice.isValid;

            UpdateControllerPose(leftDevice, hasLeftController, leftPose, true);
            UpdateControllerPose(rightDevice, hasRightController, rightPose, false);

            ProcessHand(leftDevice, hasLeftController, leftPose, leftHoldAnchor, leftRay,
                ref leftHeldPart, ref leftHoveredButton, ref leftGripWasDown, ref leftTriggerWasDown);
            ProcessHand(rightDevice, hasRightController, rightPose, rightHoldAnchor, rightRay,
                ref rightHeldPart, ref rightHoveredButton, ref rightGripWasDown, ref rightTriggerWasDown);

            if (!hasHeadTracking && !hasLeftController && !hasRightController)
                UpdateEditorMouseFallback();
        }

        private void UpdateHeadPose()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid && head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition))
            {
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
                if (localPosition.y < 0.35f)
                    localPosition.y += 1.62f;
                playerCamera.transform.localPosition = localPosition;
                playerCamera.transform.localRotation = localRotation;
                hasHeadTracking = true;
            }
            else
            {
                hasHeadTracking = false;
                playerCamera.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            }
        }

        private void UpdateControllerPose(InputDevice device, bool isTracked, Transform pose, bool leftHand)
        {
            if (isTracked && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition))
            {
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
                pose.localPosition = localPosition;
                pose.localRotation = localRotation;
                return;
            }

            if (hasHeadTracking)
                return;

            float side = leftHand ? -0.25f : 0.25f;
            pose.position = playerCamera.transform.position
                + playerCamera.transform.right * side
                - playerCamera.transform.up * 0.17f
                + playerCamera.transform.forward * 0.38f;
            pose.rotation = playerCamera.transform.rotation;
        }

        private void UpdateMovement()
        {
            if (characterController == null)
                return;

            Vector2 movement = Vector2.zero;
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool hasStick = left.isValid && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out movement);

            if (!hasStick && !hasHeadTracking)
            {
                movement.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                movement.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                if (Input.GetKey(KeyCode.Q))
                    transform.Rotate(0f, -75f * Time.deltaTime, 0f);
                if (Input.GetKey(KeyCode.E))
                    transform.Rotate(0f, 75f * Time.deltaTime, 0f);
            }

            Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(playerCamera.transform.right, Vector3.up).normalized;
            Vector3 move = (forward * movement.y + right * movement.x) * MoveSpeed * Time.deltaTime;
            characterController.Move(move);

            if (hasStick && Time.time >= nextSnapTurnTime)
            {
                if (movement.x > 0.78f)
                {
                    transform.Rotate(0f, SnapTurnDegrees, 0f);
                    nextSnapTurnTime = Time.time + 0.38f;
                }
                else if (movement.x < -0.78f)
                {
                    transform.Rotate(0f, -SnapTurnDegrees, 0f);
                    nextSnapTurnTime = Time.time + 0.38f;
                }
            }
        }

        private void ProcessHand(
            InputDevice device,
            bool isTracked,
            Transform pose,
            Transform holdAnchor,
            LineRenderer line,
            ref InSightAssemblyPart heldPart,
            ref InSightFlashbackWorldButton hoveredButton,
            ref bool gripWasDown,
            ref bool triggerWasDown)
        {
            bool gripDown = isTracked && device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripValue) && gripValue;
            bool triggerDown = isTracked && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue) && triggerValue;
            bool gripReleased = gripWasDown && !gripDown;
            bool gripPressed = gripDown && !gripWasDown;
            bool triggerPressed = triggerDown && !triggerWasDown;
            gripWasDown = gripDown;
            triggerWasDown = triggerDown;

            if (!isTracked && !hasHeadTracking)
            {
                gripDown = false;
                triggerDown = false;
            }

            InSightAssemblyPart part = FindPart(pose.position, pose.forward);
            InSightFlashbackWorldButton button = FindButton(pose.position, pose.forward);
            SetHoveredButton(ref hoveredButton, button);
            DrawRay(line, pose.position, pose.forward);

            if (gripPressed && heldPart == null && part != null && part.BeginGrab(holdAnchor))
                heldPart = part;
            if (gripReleased && heldPart != null)
            {
                heldPart.ReleaseFromGrab();
                heldPart = null;
            }
            if (triggerPressed && button != null)
                button.Press();
        }

        private InSightAssemblyPart FindPart(Vector3 origin, Vector3 direction)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, RayLength, ~0, QueryTriggerInteraction.Ignore))
                return null;
            InSightAssemblyPart part = hit.collider.GetComponentInParent<InSightAssemblyPart>();
            return part != null && part.IsInteractable ? part : null;
        }

        private InSightFlashbackWorldButton FindButton(Vector3 origin, Vector3 direction)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, RayLength, ~0, QueryTriggerInteraction.Ignore))
                return null;
            InSightFlashbackWorldButton button = hit.collider.GetComponentInParent<InSightFlashbackWorldButton>();
            return button != null && button.Interactable ? button : null;
        }

        private static void SetHoveredButton(ref InSightFlashbackWorldButton previous, InSightFlashbackWorldButton next)
        {
            if (previous == next)
                return;
            if (previous != null)
                previous.SetHovered(false);
            previous = next;
            if (previous != null)
                previous.SetHovered(true);
        }

        private static void DrawRay(LineRenderer line, Vector3 origin, Vector3 direction)
        {
            if (line == null)
                return;
            Vector3 end = origin + direction * RayLength;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, RayLength, ~0, QueryTriggerInteraction.Ignore))
                end = hit.point;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
        }

        private void UpdateEditorMouseFallback()
        {
            Ray aim = playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            rightPose.SetPositionAndRotation(aim.origin, Quaternion.LookRotation(aim.direction, Vector3.up));
            rightHoldAnchor.position = aim.origin + aim.direction * 1.25f;
            rightHoldAnchor.rotation = rightPose.rotation;
            DrawRay(rightRay, aim.origin, aim.direction);

            if (Input.GetMouseButton(1))
            {
                float yaw = Input.GetAxis("Mouse X") * 2f;
                float pitch = -Input.GetAxis("Mouse Y") * 2f;
                transform.Rotate(0f, yaw, 0f);
                playerCamera.transform.Rotate(pitch, 0f, 0f, Space.Self);
            }

            bool mouseDown = Input.GetMouseButton(0);
            if (mouseDown && !editorMouseWasDown)
            {
                InSightFlashbackWorldButton button = FindButton(aim.origin, aim.direction);
                if (button != null)
                {
                    button.Press();
                }
                else
                {
                    InSightAssemblyPart part = FindPart(aim.origin, aim.direction);
                    if (part != null && part.BeginGrab(rightHoldAnchor))
                        rightHeldPart = part;
                }
            }
            if (!mouseDown && editorMouseWasDown && rightHeldPart != null)
            {
                rightHeldPart.ReleaseFromGrab();
                rightHeldPart = null;
            }
            editorMouseWasDown = mouseDown;
        }
    }
}
