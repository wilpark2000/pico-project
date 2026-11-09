using UnityEngine;
namespace Body24
{
    public class PlayzoneView : MonoBehaviour
    {
        public LineRenderer outline, centre, leftLane, rightLane;
        public void Layout(float depth, float halfWidth, float lane)
        {
            Set(outline, new[] { new Vector3(-halfWidth, 0, 0), new Vector3(-halfWidth, 0, depth), new Vector3(halfWidth, 0, depth), new Vector3(halfWidth, 0, 0), new Vector3(-halfWidth, 0, 0) });
            Set(centre, new[] { new Vector3(0, 0, 0), new Vector3(0, 0, depth) });
            SetLane(leftLane, -lane, depth);
            SetLane(rightLane, lane, depth);
        }
        public void ShowLanes(bool visible) { leftLane.gameObject.SetActive(visible); rightLane.gameObject.SetActive(visible); }
        void SetLane(LineRenderer line, float x, float depth)
        {
            const float length = .3f, topOffset = .3f;
            var top = new Vector3(x, 0, depth - topOffset);
            var bottom = top - Vector3.forward * length;
            Set(line, new[] { bottom, top });
            var icon = line.transform.Find("Hand Icon");
            if (icon != null)
            {
                var midpoint = (bottom + top) * .5f;
                icon.position = line.useWorldSpace ? midpoint : line.transform.TransformPoint(midpoint);
            }
        }
        void Set(LineRenderer line, Vector3[] points) { line.positionCount = points.Length; line.SetPositions(points); }
    }
}
