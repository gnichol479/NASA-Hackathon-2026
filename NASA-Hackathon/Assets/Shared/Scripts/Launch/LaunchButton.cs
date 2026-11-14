using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace NasaHack
{
    // Big red button. Press it with a finger (poke), grab it, or point and pull the trigger.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class LaunchButton : MonoBehaviour
    {
        public DeltaIILaunch launch;
        public Transform cap;              // the part that moves down when pressed
        public float pressDepth = 0.015f;
        public Color pressedColor = new Color(0.25f, 0.05f, 0.05f);

        XRSimpleInteractable interactable;
        Vector3 capRest;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (cap) capRest = cap.localPosition;
        }

        void OnEnable() => interactable.selectEntered.AddListener(OnPressed);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnPressed);

        void OnPressed(SelectEnterEventArgs args)
        {
            if (launch == null || launch.Launched) return;

            if (args.interactorObject is XRBaseControllerInteractor ci && ci.xrController != null)
                ci.xrController.SendHapticImpulse(0.7f, 0.15f);

            if (cap)
            {
                cap.localPosition = capRest + Vector3.down * pressDepth;
                var r = cap.GetComponent<Renderer>();
                if (r)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", pressedColor);
                    block.SetColor("_EmissionColor", Color.black);
                    r.SetPropertyBlock(block);
                }
            }
            launch.BeginLaunch();
        }
    }
}
