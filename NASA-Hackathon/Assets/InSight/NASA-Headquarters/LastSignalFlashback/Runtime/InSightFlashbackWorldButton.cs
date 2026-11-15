using System;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

namespace LastSignal.InSightFlashback
{
    /// <summary>Small physics-backed world button so controller rays need no UI package setup.</summary>
    [DisallowMultipleComponent]
    public sealed class InSightFlashbackWorldButton : MonoBehaviour
    {
        [SerializeField] private Renderer faceRenderer;
        [SerializeField] private Collider hitCollider;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private XRSimpleInteractable xrInteractable;

        private Action pressed;
        private MaterialPropertyBlock colorBlock;
        private Color activeColor;
        private bool interactable;
        private bool hovered;

        public bool Interactable => interactable;

        public void Configure(string buttonLabel, Action onPressed, Color buttonColor, TMP_Text textComponent)
        {
            pressed = onPressed;
            activeColor = buttonColor;
            labelText = textComponent;
            if (labelText != null)
                labelText.text = buttonLabel;
            faceRenderer = GetComponentInChildren<Renderer>();
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
            Color color = !interactable
                ? new Color(0.22f, 0.25f, 0.29f, 1f)
                : hovered ? Color.Lerp(activeColor, Color.white, 0.35f) : activeColor;
            if (colorBlock == null)
                colorBlock = new MaterialPropertyBlock();
            faceRenderer.GetPropertyBlock(colorBlock);
            colorBlock.SetColor(Shader.PropertyToID("_BaseColor"),color);
            colorBlock.SetColor(Shader.PropertyToID("_Color"),color);
            faceRenderer.SetPropertyBlock(colorBlock);
        }
    }
}
