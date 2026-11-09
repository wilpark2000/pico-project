using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace Body24
{
    public struct HandSample
    {
        public bool valid, openPalmUp, ok;
        public Vector3 palm, tip;
    }
    [DefaultExecutionOrder(-50)]
    public class TrackedHands : MonoBehaviour
    {
        public Body24Config config;
        public Camera head;
        public Transform trackingSpace;
        public Transform[] probeVisuals;
        readonly CapsuleCollider[] probeColliders = new CapsuleCollider[2];
        public bool simulateInEditor = true;
        [Header("Editor camera controls")]
        public float simulatorMoveSpeed = .8f;
        public float simulatorLookSensitivity = .12f;
        Vector3 simulatorOffset;
        Vector2 simulatorLook;
        public HandSample[] Hands { get; } = new HandSample[2];
        public bool HeadValid { get; private set; }
        public bool Simulation => Application.isEditor && simulateInEditor;
        public bool ExitPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public Transform playFrame;
        public float laneWidth = .26f;
        public bool placed;
        [System.NonSerialized] public Transform fingerSelectionPlane;
        public bool useInjectedInput;
        public void Inject(HandSample leftHand, HandSample rightHand, bool headValid)
        { useInjectedInput = true; Hands[0] = leftHand; Hands[1] = rightHand; HeadValid = headValid; }
        public void LockPlayFrame()
        {
            if (Simulation && playFrame)
            {
                // Switch simulator coordinates without jumping when the preview locks.
                simulatorOffset = playFrame.InverseTransformPoint(head.transform.position) - new Vector3(0, simulatedPlank ? .65f : 1.7f, simulatedPlank ? .10f : 0);
                var angles = (Quaternion.Inverse(playFrame.rotation) * head.transform.rotation).eulerAngles;
                simulatorLook = new Vector2(Mathf.DeltaAngle(0, angles.y), Mathf.DeltaAngle(0, angles.x) - (simulatedPlank ? 62 : 40));
            }
            placed = true;
        }
        bool simulatedPlank;
#if ENABLE_PICO_XR_SDK
        HandJointLocations left, right;
#endif
        void Start()
        {
            ConfigureProbeColliders();
            // An external XR provider owns device setup when feeding this component.
            if (Simulation || useInjectedInput) return;
            var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var subsystem in subsystems) subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
#if ENABLE_PICO_XR_SDK
            PXR_Manager.EnableVideoSeeThrough = true;
#endif
        }
        void Update()
        {
            if (useInjectedInput) { }
            else if (Simulation) Simulate();
            else
            {
                var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                HeadValid = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked;
#if ENABLE_PICO_XR_SDK
                Hands[0] = Read(HandType.HandLeft, ref left);
                Hands[1] = Read(HandType.HandRight, ref right);
#else
                Hands[0] = Hands[1] = default;
#endif
            }
            for (int i = 0; i < 2; i++) if (probeVisuals != null && i < probeVisuals.Length && probeVisuals[i])
            {
                probeVisuals[i].gameObject.SetActive(Hands[i].valid && !fingerSelectionPlane);
                probeVisuals[i].SetPositionAndRotation(Hands[i].palm, Quaternion.identity);
            }
        }
        public void ConfigureProbeColliders()
        {
            for (int i = 0; i < probeColliders.Length; i++)
            {
                if (probeVisuals == null || i >= probeVisuals.Length || !probeVisuals[i]) continue;
                var probe = probeVisuals[i];
                probe.localScale = Vector3.one * (config.probeRadius * 2);
                probe.rotation = Quaternion.identity;
                var capsule = probe.GetComponent<CapsuleCollider>();
                if (!capsule) capsule = probe.gameObject.AddComponent<CapsuleCollider>();
                // Sphere mesh diameter is one local unit. Capsule.height includes its end caps.
                capsule.radius = .5f;
                capsule.height = 1.3f;
                capsule.direction = 1; // Y axis
                capsule.center = Vector3.zero;
                capsule.isTrigger = true;
                probeColliders[i] = capsule;
            }
        }
        public CapsuleCollider GetProbeCollider(int hand)
        {
            if (!probeColliders[hand]) ConfigureProbeColliders();
            return probeColliders[hand];
        }
        void LateUpdate()
        {
            // Ignore hand, head and parent rotations; capsules stay vertical in world space.
            if (probeVisuals == null) return;
            foreach (var probe in probeVisuals) if (probe) probe.rotation = Quaternion.identity;
        }
#if ENABLE_PICO_XR_SDK
        HandSample Read(HandType type, ref HandJointLocations locations)
        {
            var sample = new HandSample();
            if (!PXR_HandTracking.GetJointLocations(type, ref locations) || locations.isActive == 0 || locations.jointCount < 26 || locations.jointLocations == null) return sample;
            int[] required = { 0, 1, 5, 10, 12, 15, 17, 20, 25 };
            foreach (int j in required)
                if ((locations.jointLocations[j].locationStatus & HandLocationStatus.PositionTracked) == 0) return sample;
            var joints = locations.jointLocations;
            sample.valid = HeadValid;
            sample.palm = trackingSpace.TransformPoint((joints[12].pose.Position.ToVector3() + joints[17].pose.Position.ToVector3()) * .5f);
            sample.tip = trackingSpace.TransformPoint(joints[10].pose.Position.ToVector3());
            var thumb = trackingSpace.TransformPoint(joints[5].pose.Position.ToVector3());
            var wrist = trackingSpace.TransformPoint(joints[1].pose.Position.ToVector3());
            var middleTip = trackingSpace.TransformPoint(joints[15].pose.Position.ToVector3());
            var ringTip = trackingSpace.TransformPoint(joints[20].pose.Position.ToVector3());
            var pinkyTip = trackingSpace.TransformPoint(joints[25].pose.Position.ToVector3());
            bool extended = Vector3.Distance(wrist, middleTip) > .12f && Vector3.Distance(wrist, ringTip) > .105f && Vector3.Distance(wrist, pinkyTip) > .09f;
            Vector3 normal = trackingSpace.rotation * joints[0].pose.Orientation.ToQuat() * Vector3.up;
            sample.openPalmUp = extended && Vector3.Dot(normal, Vector3.up) > .45f;
            sample.ok = extended && Vector3.Distance(thumb, sample.tip) <= config.okDistance;
            return sample;
        }
#endif
        void Simulate()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            HeadValid = kb == null || !kb.tKey.isPressed;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            { simulatedPlank = !simulatedPlank; ResetSimulatorView(); }
            if (kb != null && kb.hKey.wasPressedThisFrame) ResetSimulatorView();
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * simulatorLookSensitivity;
                simulatorLook.x += delta.x;
                simulatorLook.y = Mathf.Clamp(simulatorLook.y - delta.y, -(simulatedPlank ? 62 : 40) + 5, 85 - (simulatedPlank ? 62 : 40));
            }
            if (kb != null)
            {
                Vector3 movement = new Vector3((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                    (kb.rKey.isPressed ? 1 : 0) - (kb.fKey.isPressed ? 1 : 0),
                    (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                simulatorOffset += Quaternion.Euler(0, simulatorLook.x, 0) * Vector3.ClampMagnitude(movement, 1) * simulatorMoveSpeed * Mathf.Min(Time.unscaledDeltaTime, .1f);
            }
            Vector3 origin = placed && playFrame ? playFrame.position : Vector3.zero;
            Quaternion rotation = placed && playFrame ? playFrame.rotation : Quaternion.identity;
            head.transform.position = origin + rotation * (new Vector3(0, simulatedPlank ? .65f : 1.7f, simulatedPlank ? .10f : placed ? 0 : -.12f) + simulatorOffset);
            head.transform.rotation = rotation * Quaternion.Euler((simulatedPlank ? 62 : 40) + simulatorLook.y, simulatorLook.x, 0);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = origin + rotation * new Vector3((i == 0 ? -1 : 1) * laneWidth, simulatedPlank ? .04f : 1.25f, simulatedPlank ? .44f : .55f);
                Hands[i] = new HandSample { valid = HeadValid, palm = p, tip = p, openPalmUp = true, ok = kb != null && kb.oKey.isPressed };
            }
            int selecting = kb != null && kb.leftShiftKey.isPressed ? 1 : 0;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                Ray ray = head.ScreenPointToRay(mouse.position.ReadValue());
                var selectionPlane = fingerSelectionPlane ? new Plane(fingerSelectionPlane.forward, fingerSelectionPlane.position) : new Plane(Vector3.up, new Vector3(0, config.floorHeight + Body24App.PlayzoneFloorLift, 0));
                if (selectionPlane.Raycast(ray, out float d))
                {
                    var h = Hands[selecting]; h.palm = h.tip = ray.GetPoint(d); Hands[selecting] = h;
                }
            }
            if (kb != null)
            {
                if (kb.bKey.isPressed) { var other = Hands[1 - selecting]; other.palm.y += .25f; other.tip = other.palm; Hands[1 - selecting] = other; }
                for (int i = 0; i < 2; i++) if (i == 0 ? kb.qKey.isPressed : kb.eKey.isPressed)
                { var h = Hands[i]; h.palm = h.tip = head.transform.position + head.transform.right * (i == 0 ? -.15f : .15f); Hands[i] = h; }
            }
        }
        public void ResetSimulatorView() { simulatorOffset = Vector3.zero; simulatorLook = Vector2.zero; }
    }
}
