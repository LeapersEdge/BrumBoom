using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Locks and hides cursor when enabled (Gameplay scene). Add to any active GO.
    /// </summary>
    public class CursorLocker : MonoBehaviour
    {
        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}


