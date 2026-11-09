using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace Body24.Quest
{
    /// <summary>Feeds the existing game from Unity XR Hands, without PICO runtime calls.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(TrackedHands))]
    public sealed class QuestOpenXRHands : MonoBehaviour
    {
        public TrackedHands input;
        readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
        XRHandSubsystem hands;
        XRInputSubsystem floorSubsystem;

        void Awake()
        {
            if (!input) input = GetComponent<TrackedHands>();
            if (!input.Simulation) input.Inject(default, default, false);
        }

        void Update()
        {
            if (input.Simulation) return;
            if (hands == null || !hands.running)
            {
                hands = null;
                SubsystemManager.GetSubsystems(handSubsystems);
                foreach (var candidate in handSubsystems)
                    if (candidate.running) { hands = candidate; break; }
            }
            // Retry after subsystem startup and on session restart.
            if (floorSubsystem == null || !floorSubsystem.running)
            {
                SubsystemManager.GetSubsystems(inputSubsystems);
                foreach (var candidate in inputSubsystems)
                    if (candidate.running && candidate.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    { floorSubsystem = candidate; break; }
            }
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool value) && value;
            input.Inject(hands != null ? Read(hands.leftHand, true, tracked) : default,
                hands != null ? Read(hands.rightHand, false, tracked) : default, tracked);
        }

        HandSample Read(XRHand hand, bool left, bool headTracked)
        {
            if (!headTracked || !hand.isTracked || !input.trackingSpace) return default;
            if (!Pose(hand, XRHandJointID.Wrist, out var wrist) ||
                !Pose(hand, XRHandJointID.ThumbTip, out var thumb) ||
                !Pose(hand, XRHandJointID.IndexTip, out var indexTip) ||
                !Pose(hand, XRHandJointID.IndexProximal, out var indexBase) ||
                !Pose(hand, XRHandJointID.MiddleProximal, out var middleBase) ||
                !Pose(hand, XRHandJointID.MiddleTip, out var middleTip) ||
                !Pose(hand, XRHandJointID.RingProximal, out var ringBase) ||
                !Pose(hand, XRHandJointID.RingTip, out var ringTip) ||
                !Pose(hand, XRHandJointID.LittleProximal, out var littleBase) ||
                !Pose(hand, XRHandJointID.LittleTip, out var littleTip)) return default;

            var space = input.trackingSpace;
            var wristWorld = space.TransformPoint(wrist.position);
            bool extended = Vector3.Distance(wristWorld, space.TransformPoint(middleTip.position)) > .12f &&
                Vector3.Distance(wristWorld, space.TransformPoint(ringTip.position)) > .105f &&
                Vector3.Distance(wristWorld, space.TransformPoint(littleTip.position)) > .09f;
            // Anatomical cross product avoids provider-specific joint rotation conventions.
            var normal = Vector3.Cross(indexBase.position - wrist.position, littleBase.position - wrist.position).normalized;
            if (left) normal = -normal;
            normal = space.TransformDirection(normal);
            var tip = space.TransformPoint(indexTip.position);
            return new HandSample
            {
                valid = true,
                palm = space.TransformPoint((middleBase.position + ringBase.position) * .5f),
                tip = tip,
                openPalmUp = extended && Vector3.Dot(normal, Vector3.up) > .45f,
                ok = extended && Vector3.Distance(space.TransformPoint(thumb.position), tip) <= input.config.okDistance
            };
        }

        static bool Pose(XRHand hand, XRHandJointID joint, out UnityEngine.Pose pose)
            => hand.GetJoint(joint).TryGetPose(out pose);

        void OnDisable()
        {
            // Keep lost/disabled tracking invalid; never fall back to PICO on a Quest.
            if (input && !input.Simulation) input.Inject(default, default, false);
        }
    }
}
