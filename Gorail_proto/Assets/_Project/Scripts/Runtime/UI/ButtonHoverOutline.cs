using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(Button))]
    public class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color OutlineColor = new Color(0.6f, 1f, 0.2f, 1f);
        private const float Thickness = 3f;

        private GameObject borderRoot;

        void Awake()
        {
            borderRoot = new GameObject("HoverOutline");
            borderRoot.transform.SetParent(transform, false);
            borderRoot.transform.SetAsFirstSibling();

            var rootRT = borderRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            CreateStrip("Top",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -Thickness), new Vector2(0, 0));
            CreateStrip("Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),          new Vector2(0, Thickness));
            CreateStrip("Left",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0),          new Vector2(Thickness, 0));
            CreateStrip("Right",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(-Thickness, 0), new Vector2(0, 0));

            borderRoot.SetActive(false);
        }

        void CreateStrip(string stripName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(stripName);
            go.transform.SetParent(borderRoot.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var img = go.AddComponent<Image>();
            img.color = OutlineColor;
            img.raycastTarget = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            borderRoot.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            borderRoot.SetActive(false);
        }
    }
}
