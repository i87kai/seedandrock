using UnityEngine;
using UnityEngine.InputSystem;

namespace SeedAndRock.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SeedAndRockInteractionRaycaster : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float interactionDistance = 3.5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private Camera viewCamera;
        private InputAction interactAction;

        private void Awake()
        {
            viewCamera = GetComponentInChildren<Camera>();
            interactAction = new InputAction("Interact", InputActionType.Button);
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable() => interactAction?.Enable();
        private void OnDisable() => interactAction?.Disable();
        private void OnDestroy() => interactAction?.Dispose();

        private void Update()
        {
            if (!interactAction.WasPressedThisFrame() || viewCamera == null)
                return;

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
                return;

            SeedAndRockInteractable interactable = hit.collider.GetComponentInParent<SeedAndRockInteractable>();
            if (interactable != null)
                interactable.Interact();
        }
    }
}