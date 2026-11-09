using UnityEngine;
namespace Body24
{
    public class EditorSimulatorOverlay : MonoBehaviour
    {
        public TrackedHands input;
        void Start()
        {
            if (input.Simulation)
            {
                var driver = input.head.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                if (driver) driver.enabled = false;
            }
        }
    }
}
