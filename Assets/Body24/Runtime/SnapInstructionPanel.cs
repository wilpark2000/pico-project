using UnityEngine;
namespace Body24
{
    public class SnapInstructionPanel : MonoBehaviour
    {
        public Transform head;
        public Body24Config config;
        Vector3 facing;
        Vector3 target;
        bool initialized;
        void OnEnable() { initialized = false; }
        void LateUpdate()
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) return;
            if (!initialized || Vector3.Angle(facing, forward) > config.snapAngle)
            {
                facing = forward; target = head.position + forward * config.followDistance;
                if (!initialized) { transform.position = target; transform.rotation = Quaternion.LookRotation(forward); initialized = true; }
            }
            target = head.position + facing * config.followDistance;
            float t = 1 - Mathf.Exp(-config.followSpeed * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), t);
        }
    }
}
