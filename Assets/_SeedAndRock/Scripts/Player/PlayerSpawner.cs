using SeedAndRock.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedAndRock.Player
{
    /// <summary>
    /// Finds or creates the single explorer player and moves it safely. The player is never parented to
    /// generated world content so regenerating a world does not destroy it.
    /// </summary>
    public static class PlayerSpawner
    {
        public const string PlayerName = "SeedAndRock_Player";

        public static FirstPersonExplorerController Find() =>
            Object.FindAnyObjectByType<FirstPersonExplorerController>(FindObjectsInactive.Include);

        public static FirstPersonExplorerController EnsurePlayer(Vector3 spawnPosition)
        {
            FirstPersonExplorerController controller = Find();
            if (controller == null)
            {
                GameObject player = new GameObject(PlayerName, typeof(CharacterController), typeof(FirstPersonExplorerController), typeof(SeedAndRockInteractionRaycaster));
                controller = player.GetComponent<FirstPersonExplorerController>();
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(player.transform, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 2500f;
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }

            Teleport(controller, spawnPosition, 0f, 0f);
            return controller;
        }

        /// <summary>Moves the player and resets its view, keeping the CharacterController in sync with physics.</summary>
        public static void Teleport(FirstPersonExplorerController controller, Vector3 position, float yaw, float pitch)
        {
            if (controller == null) return;
            CharacterController body = controller.GetComponent<CharacterController>();
            bool wasEnabled = body != null && body.enabled;
            if (body != null) body.enabled = false;
            controller.transform.position = position;
            controller.SetView(yaw, pitch);
            controller.ResetVelocity();
            if (body != null) body.enabled = wasEnabled;
            Physics.SyncTransforms();
        }
    }
}
