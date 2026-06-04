using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SubwayMapPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundOverlay;
        [SerializeField] private Game.Subway.SubwayMapRenderer mapRenderer;
        [SerializeField] private SubwayMapZoom mapZoom;

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

        public void Show()
        {
            gameObject.SetActive(true);
            mapRenderer?.RefreshMarkers();
        }

        public void Hide()
        {
            // [D11] 닫아도 배율 리셋하지 않음 — 재오픈 시 이전 배율 유지(SubwayMapZoom.OnEnable).
            gameObject.SetActive(false);
        }
    }
}