using UnityEngine;

namespace SeedAndRock.Interaction
{
    /// <summary>Minimal interaction seam for later gathering, farming and building systems.</summary>
    public sealed class SeedAndRockInteractable : MonoBehaviour
    {
        [SerializeField] private string displayName = "Resonant Seed";
        [SerializeField, TextArea] private string interactionMessage = "The seed hums with dormant life.";

        public string DisplayName => displayName;

        public void Interact()
        {
            Debug.Log("[SeedAndRock] Interacted with " + displayName + ": " + interactionMessage, this);
        }
    }
}