using UnityEngine;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 기본 뷰의 단일 결함(외부IP §2)을 현재 IP 데이터로 구성·배치한다.
    /// 결함 정보(스프라이트·하이라이트·위치)는 IpCanvasData에 들어 있고,
    /// 현재 IP는 GroundBaseView.CurrentIp(역 ipCanvas ?? 기본 IP)에서 받는다.
    ///
    /// 이미 작품활동을 완료한 역(쿨타임)이면 결함을 숨긴다(§6). 작품활동이 끝나면
    /// 즉시 다시 판정해 그 자리에서 결함이 사라지게 한다.
    /// </summary>
    public class DefectController : MonoBehaviour
    {
        [Tooltip("현재 IP를 제공하는 GroundBaseView (보통 같은 기본 뷰).")]
        [SerializeField] private GroundBaseView view;
        [Tooltip("구성·배치할 단일 결함.")]
        [SerializeField] private Defect defect;

        Game.Gameplay.ArtworkSystem Artwork => GameCore.Instance?.Artwork;

        void Start()
        {
            var aw = Artwork;
            if (aw != null) aw.ArtworkFinished += OnArtworkFinished;
            Refresh();
        }

        void OnDestroy()
        {
            var aw = Artwork;
            if (aw != null) aw.ArtworkFinished -= OnArtworkFinished;
        }

        void OnArtworkFinished(bool succeeded, float fameGain, bool interrupted) => Refresh();

        public void Refresh()
        {
            if (defect == null) { Debug.LogWarning("[DefectController] Defect 미연결."); return; }
            var ip = view != null ? view.CurrentIp : null;

            // 이미 작품활동 완료한 역이면 결함 숨김(쿨타임, §6)
            string stn = GameCore.Instance?.Space?.CurrentStationId;
            bool done = GameCore.Instance?.Platform != null && GameCore.Instance.Platform.IsArtworkDone(stn);

            if (done || ip == null || ip.defectSprite == null)
            {
                defect.gameObject.SetActive(false);
                Debug.Log($"[DefectController] 결함 숨김 (완료={done}, IP='{(ip != null ? ip.ipId : "null")}')");
                return;
            }

            defect.Configure(ip.defectSprite, ip.defectHighlightSprite, ip.ipId);

            // 위치+크기를 정규화 앵커 사각형으로 적용
            var rt = (RectTransform)defect.transform;
            Vector2 half = ip.defectSize * 0.5f;
            rt.anchorMin = ip.defectPosition - half;
            rt.anchorMax = ip.defectPosition + half;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            defect.gameObject.SetActive(true);
            Debug.Log($"[DefectController] IP='{ip.ipId}' 결함 배치 @ pos={ip.defectPosition}, size={ip.defectSize}");
        }
    }
}