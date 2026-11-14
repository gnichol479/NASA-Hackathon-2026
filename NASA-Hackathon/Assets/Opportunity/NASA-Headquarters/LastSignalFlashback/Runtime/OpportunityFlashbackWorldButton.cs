using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.OpportunityFlashback
{
    /// <summary>Small physics-backed world button so controller rays need no UI package setup.</summary>
    [DisallowMultipleComponent]
    public sealed class OpportunityFlashbackWorldButton : MonoBehaviour
    {
        [SerializeField] private Renderer faceRenderer;
        [SerializeField] private Collider hitCollider;
        [SerializeField] private Text labelText;
        [SerializeField] private XRSimpleInteractable xrInteractable;

        private Action pressed;
        private Color activeColor;
        private bool interactable;
        private bool hovered;

        public bool Interactable => interactable;

        public void Configure(string buttonLabel, Action onPressed, Color buttonColor, Text textComponent)
        {
            pressed = onPressed;
            activeColor = buttonColor;
            labelText = textComponent;
            if (labelText != null)
                labelText.text = buttonLabel;
            faceRenderer = GetComponent<Renderer>();
            hitCollider = GetComponent<Collider>();
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            xrInteractable.colliders.Clear();
            if (hitCollider != null)
                xrInteractable.colliders.Add(hitCollider);
            xrInteractable.selectEntered.RemoveListener(OnXRSelected);
            xrInteractable.selectEntered.AddListener(OnXRSelected);
            xrInteractable.hoverEntered.RemoveListener(OnXRHoverEntered);
            xrInteractable.hoverEntered.AddListener(OnXRHoverEntered);
            xrInteractable.hoverExited.RemoveListener(OnXRHoverExited);
            xrInteractable.hoverExited.AddListener(OnXRHoverExited);
            SetInteractable(true);
        }

        public void SetLabel(string value)
        {
            if (labelText != null)
                labelText.text = value;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (hitCollider != null)
                hitCollider.enabled = value;
            if (xrInteractable != null)
                xrInteractable.enabled = value;
            RefreshColor();
        }

        public void SetHovered(bool value)
        {
            if (hovered == value)
                return;
            hovered = value;
            RefreshColor();
        }

        public void Press()
        {
            if (interactable)
                pressed?.Invoke();
        }

        private void OnXRSelected(SelectEnterEventArgs _)
        {
            Press();
        }

        private void OnXRHoverEntered(HoverEnterEventArgs _)
        {
            SetHovered(true);
        }

        private void OnXRHoverExited(HoverExitEventArgs _)
        {
            SetHovered(false);
        }

        private void RefreshColor()
        {
            if (faceRenderer == null)
                return;
            faceRenderer.material.color = !interactable
                ? new Color(0.22f, 0.25f, 0.29f, 1f)
                : hovered ? Color.Lerp(activeColor, Color.white, 0.35f) : activeColor;
        }
    }
}
