using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 기본 뷰의 단일 결함(외부IP §2)을 현재 IP 데이터로 구성·배치한다.
    /// 결함 정보(스프라이트·하이라이트·위치)는 IpCanvasData에 들어 있고,
    /// 현재 IP는 GroundBaseView.CurrentIp(역 ipCanvas ?? 기본 IP)에서 받는다.
    ///
    /// → 씬의 Defect 오브젝트는 ipId·스프라이트를 비워두면 되고, 위치도 데이터의
    ///   정규화 좌표(defectPosition)로 런타임에 잡힌다. IP당 결함 1개 기준.
    /// </summary>
    public class DefectController : MonoBehaviour
    {
        [Tooltip("현재 IP를 제공하는 GroundBaseView (보통 같은 기본 뷰).")]
        [SerializeField] private GroundBaseView view;
        [Tooltip("구성·배치할 단일 결함.")]
        [SerializeField] private Defect defect;

        void Start() => Refresh();

        public void Refresh()
        {
            if (defect == null) { Debug.LogWarning("[DefectController] Defect 미연결."); return; }
            var ip = view != null ? view.CurrentIp : null;

            if (ip == null || ip.defectSprite == null)
            {
                defect.gameObject.SetActive(false);
                Debug.Log($"[DefectController] IP='{(ip != null ? ip.ipId : "null")}' — 결함 데이터 없음, 숨김");
                return;
            }

            defect.Configure(ip.defectSprite, ip.defectHighlightSprite, ip.ipId);

            // 위치+크기를 정규화 앵커 사각형으로 적용 (배경 크기 계산 불필요·해상도 독립)
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