using UnityEngine;
namespace Body24
{
    [CreateAssetMenu(menuName = "BODY 24/Configuration")]
    public class Body24Config : ScriptableObject
    {
        [Header("Interaction (seconds / metres)")]
        public float holdSeconds = .5f, bonusWindow = .5f, probeRadius = .0125f;
        public float supportHeight = .09f, liftHeight = .15f, okDistance = .035f;
        public float shoulderDistance = .32f, shoulderCooldown = .35f;
        [Header("Calibration")]
        public float sampleSeconds = 1.5f, maximumSampleMotion = .06f;
        public int minimumSampleCount = 15;
        public float floorHeight = 0, minimumHeight = 1.1f, maximumHeight = 2.2f;
        public float minimumLaneHalfWidth = .22f, maximumLaneHalfWidth = .34f;
        public float operatorReach = .30f;
        [Header("Presence")]
        public float headRiseLimit = .38f, headDropLimit = .30f, backwardLimit = .5f, lateralLimit = .6f;
        public float presenceGrace = .65f, presenceHysteresis = .06f;
        [Header("Presentation")]
        public float followDistance = .6f, snapAngle = 20, followSpeed = 7, mergeSeconds = .35f;
        public Color ink = new Color(.035f, .065f, .085f, .97f);
        public Color mint = new Color(.35f, 1, .73f), gold = new Color(1, .78f, .27f);
    }
}
