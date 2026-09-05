using UnityEngine;
using UnityEngine.InputSystem;

namespace SeedAndRock.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonExplorerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 5f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -22f;
        [SerializeField, Range(0f, 89f)] private float slopeLimit = 52f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.13f;
        [SerializeField] private float cameraHeight = 1.65f;

        private CharacterController controller;
        private Camera viewCamera;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private float pitch;
        private float verticalVelocity;

        /// <summary>Multiplier applied to look sensitivity (driven by the settings screen).</summary>
        public static float LookSensitivityScale = 1f;

        /// <summary>Horizontal view angle in degrees.</summary>
        public float Yaw => transform.eulerAngles.y;
        /// <summary>Vertical view angle in degrees (negative looks up).</summary>
        public float Pitch => pitch;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Camera ViewCamera => viewCamera;
        public bool IsSwimming { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = slopeLimit;
            controller.stepOffset = 0.35f;

            viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = new Vector3(0f, cameraHeight, 0f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");

            sprintAction = new InputAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            lookAction?.Enable();
            sprintAction?.Enable();
            jumpAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            sprintAction?.Disable();
            jumpAction?.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
            sprintAction?.Dispose();
            jumpAction?.Dispose();
        }

        private void Update()
        {
            UpdateLook();
            UpdateMovement();
        }

        /// <summary>Sets the view orientation directly (used when restoring a saved position).</summary>
        public void SetView(float yaw, float newPitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = Mathf.Clamp(newPitch, -85f, 85f);
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void ResetVelocity() => verticalVelocity = 0f;

        private void UpdateLook()
        {
            if (viewCamera == null)
                return;

            Vector2 look = lookAction.ReadValue<Vector2>() * (lookSensitivity * LookSensitivityScale);
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            transform.Rotate(Vector3.up * look.x);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            var backendWorld = SeedAndRock.World.WorldGenerator.Active;
            if (backendWorld != null && !backendWorld.TryGetHeightAt(transform.position.x,transform.position.z,out _)) return;
            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 planar = transform.right * input.x + transform.forward * input.y;
            if (planar.sqrMagnitude > 1f)
                planar.Normalize();

            float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
            var world = SeedAndRock.World.WorldGenerator.Active;
            float surface = 0f;
            IsSwimming = world != null && world.TryGetWaterSurfaceAt(transform.position.x,transform.position.z,out surface)
                && transform.position.y < surface - .8f;
            if (IsSwimming)
            {
                float ascend = jumpAction.IsPressed() ? 1f : Keyboard.current?.leftCtrlKey.isPressed == true ? -1f : 0f;
                float buoyancy = Mathf.Clamp((surface - 1.2f - transform.position.y)*2,-1.5f,1.5f);
                verticalVelocity = ascend != 0 ? ascend*3 : buoyancy;
                if (viewCamera != null && input.y != 0) verticalVelocity += viewCamera.transform.forward.y * input.y * 2;
                controller.Move((planar*3.2f+Vector3.up*verticalVelocity)*Time.deltaTime);
                return;
            }
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -2f;
                if (jumpAction.WasPressedThisFrame())
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 motion = planar * speed + Vector3.up * verticalVelocity;
            Vector3 next = transform.position + motion * Time.deltaTime;
            if(world==null || world.TryGetHeightAt(next.x,next.z,out _)) controller.Move(motion * Time.deltaTime);
        }
    }
}
