using UnityEngine;
using TMPro;
namespace Body24
{
    public class HoldTarget : MonoBehaviour
    {
        public RectTransform surface;
        public UnityEngine.UI.Image background, progress, border;
        public TMP_Text label, caption;
        public BoxCollider hitVolume;
        public Color normalColor;
        public bool activateOnPress;
        [System.NonSerialized] public System.Action<bool> action;
        [System.NonSerialized] public bool gameplay;
        public bool Interactable => isActiveAndEnabled && gameObject.activeInHierarchy && action != null;
        public bool Contains(HandSample hand, CapsuleCollider capsule)
        {
            if (!Interactable || !hand.valid || !capsule || !capsule.enabled || !capsule.gameObject.activeInHierarchy) return false;
            // Select only with the collider attached to the visible palm sphere.
            // Never create another invisible contact volume at the fingertip.
            return Physics.ComputePenetration(
                capsule, capsule.transform.position, capsule.transform.rotation,
                hitVolume, hitVolume.transform.position, hitVolume.transform.rotation,
                out _, out _);
        }
        public void Fill(float amount) { progress.fillAmount = amount; }
        public void Highlight(bool selected, Color color) { border.color = selected ? color : new Color(.22f, .38f, .43f, 1); }
        public void SetLabel(string text, string subtitle = "") { label.text = text; if (caption) caption.text = subtitle; }
        public void SetSize(Vector2 size)
        {
            surface.sizeDelta = size;
            foreach (var image in new[] { background, progress })
            {
                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.one * 3; rect.offsetMax = Vector2.one * -3;
            }
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = new Vector2(0, size.y * .09f);
            label.rectTransform.sizeDelta = new Vector2(size.x - 16, size.y * .62f);
            if (caption)
            {
                caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(.5f, .5f);
                caption.rectTransform.anchoredPosition = new Vector2(0, -size.y * .31f);
                caption.rectTransform.sizeDelta = new Vector2(size.x - 16, size.y * .22f);
            }
            hitVolume.size = new Vector3(size.x, size.y, 60);
            hitVolume.center = Vector3.zero;
        }
    }
}
