using UnityEngine;
namespace Body24
{
    public class EditorFloorPreview : MonoBehaviour
    {
        void Awake() { if (!Application.isEditor) gameObject.SetActive(false); }
    }
}
