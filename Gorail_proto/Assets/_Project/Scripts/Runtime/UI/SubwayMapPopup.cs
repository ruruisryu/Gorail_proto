using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SubwayMapPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundOverlay;
        [SerializeField] private SubwayMapZoom mapZoom;

        Game.Subway.SubwayMapRenderer mapRenderer => Game.Core.GameCore.Instance?.MapRenderer;

        private CanvasGroup canvasGroup;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (backgroundOverlay != null)
                backgroundOverlay.onClick.AddListener(Hide);
        }

        public void Show(float scale = 1f, bool viewOnly = false)
        {
            gameObject.SetActive(true);
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one * scale;
            SetStationsInteractable(!viewOnly);
            mapRenderer?.RefreshMarkers();
        }

        public void Hide()
        {
            SetStationsInteractable(true);
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        void SetStationsInteractable(bool interactable)
        {
            var mr = mapRenderer;
            if (mr == null) return;
            var stationsT = mr.MapContainer != null ? mr.MapContainer.Find("[Stations]") : null;
            if (stationsT == null) return;
            var cg = stationsT.GetComponent<CanvasGroup>();
            if (cg == null) cg = stationsT.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = interactable;
        }

    }
}