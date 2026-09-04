using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cozy.Rendering
{
    /// <summary>
    /// Minimal free-fly camera for evaluating the showcase scene (WASD + mouse,
    /// Shift = fast, Q/E = down/up, hold right mouse to look, T/G = time of day).
    /// Works with the new Input System or the legacy input manager.
    /// </summary>
    [AddComponentMenu("Cozy Rendering/Cozy Fly Camera (showcase)")]
    public sealed class CozyFlyCamera : MonoBehaviour
    {
        public float moveSpeed = 8f;
        public float fastMultiplier = 4f;
        public float lookSensitivity = 0.12f;
        public bool holdRightMouseToLook = false;

        private float yaw, pitch;

        private void OnEnable()
        {
            var e = transform.eulerAngles;
            yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        private void Update()
        {
            Vector2 look = Vector2.zero, move = Vector2.zero;
            float vertical = 0f, timeDelta = 0f;
            bool fast = false, looking = true;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb != null)
            {
                move.x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                move.y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                vertical = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
                fast = kb.leftShiftKey.isPressed;
                timeDelta = (kb.tKey.isPressed ? 1f : 0f) - (kb.gKey.isPressed ? 1f : 0f);
            }
            if (mouse != null)
            {
                looking = !holdRightMouseToLook || mouse.rightButton.isPressed;
                look = mouse.delta.ReadValue();
            }
#else
            move.x = Input.GetAxisRaw("Horizontal");
            move.y = Input.GetAxisRaw("Vertical");
            vertical = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            fast = Input.GetKey(KeyCode.LeftShift);
            timeDelta = (Input.GetKey(KeyCode.T) ? 1f : 0f) - (Input.GetKey(KeyCode.G) ? 1f : 0f);
            looking = !holdRightMouseToLook || Input.GetMouseButton(1);
            look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
            if (looking)
            {
                yaw += look.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            float speed = moveSpeed * (fast ? fastMultiplier : 1f) * Time.deltaTime;
            transform.position += (transform.forward * move.y + transform.right * move.x + Vector3.up * vertical) * speed;

            var atmo = CozyAtmosphere.Active;
            if (atmo != null && timeDelta != 0f)
                atmo.timeOfDay = Mathf.Repeat(atmo.timeOfDay + timeDelta * Time.deltaTime * 2f, 24f);
        }
    }
}
