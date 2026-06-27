using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.UI
{
    /// <summary>
    /// 에디터 전용 결함 배치 헬퍼(외부IP §2). 런타임 동작 없음 — 기획이 결함 위치·크기를
    /// 배경 위에서 시각적으로 잡아 IpCanvasData에 저장하기 위한 도구.
    ///
    /// 사용 순서(컴포넌트 우클릭 ▸ 컨텍스트 메뉴):
    ///   1) 배경 미리보기   — 대상 IP의 가로 배경/결함 스프라이트를 씬에 띄우고 비율을 맞춘다.
    ///   2) 결함을 드래그/크기조절로 원하는 자리에 둔다.
    ///   3) IP에 저장        — 결함의 현재 위치·크기를 배경 기준 정규화 값으로 IpCanvasData에 기록.
    ///   (이미 저장된 값을 다시 편집하려면 'IP에서 불러오기')
    ///
    /// IP마다: ip 칸을 그 IP 에셋으로 바꾸고 1~3 반복.
    /// </summary>
    public class DefectAuthoring : MonoBehaviour
    {
        [Tooltip("가로 배경 Image의 RectTransform (기준 사각형).")]
        public RectTransform background;
        [Tooltip("위치·크기를 잡을 결함 RectTransform.")]
        public RectTransform defect;
        [Tooltip("이 결함 정보를 저장/불러올 IP 데이터(.asset).")]
        public Game.Inventory.IpCanvasData ip;

#if UNITY_EDITOR
        [ContextMenu("배경 미리보기 해제 (스프라이트 비우기)")]
        void ClearPreview()
        {
            if (background != null)
            {
                var bgImg = background.GetComponent<Image>();
                if (bgImg != null) { Undo.RecordObject(bgImg, "Clear bg preview"); bgImg.sprite = null; EditorUtility.SetDirty(bgImg); }
            }
            if (defect != null)
            {
                var dImg = defect.GetComponent<Image>();
                if (dImg != null) { Undo.RecordObject(dImg, "Clear defect preview"); dImg.sprite = null; EditorUtility.SetDirty(dImg); }
            }
            Debug.Log("[DefectAuthoring] 미리보기 해제 — 스프라이트는 런타임에 IP별로 다시 채워집니다.");
        }

        [ContextMenu("1) 배경 미리보기 (IP 스프라이트 + 비율 맞춤)")]
        void Preview()
        {
            if (background == null || ip == null) { Debug.LogWarning("[DefectAuthoring] background/ip 칸을 채우세요."); return; }

            var bgImg = background.GetComponent<Image>();
            if (bgImg == null) { Debug.LogWarning("[DefectAuthoring] Background에 Image 컴포넌트가 없습니다."); return; }
            if (ip.outsideBackground == null)
            {
                Debug.LogWarning($"[DefectAuthoring] IP '{ip.ipId}'에 outsideBackground(가로 배경)가 없습니다 — IP 에셋에 먼저 넣으세요. (그래서 아무것도 안 떴던 것)");
                return;
            }

            // 배경 스프라이트 + 보이게(불투명·활성)
            Undo.RecordObject(bgImg, "Preview bg");
            bgImg.sprite = ip.outsideBackground;
            bgImg.color = Color.white;
            bgImg.enabled = true;
            EditorUtility.SetDirty(bgImg);

            // 앵커 가운데로 풀고(스트레치면 fit이 깨짐) 비율 맞춤 + 중앙 배치
            Undo.RecordObject(background, "Fit bg");
            background.anchorMin = background.anchorMax = background.pivot = new Vector2(0.5f, 0.5f);
            var s = ip.outsideBackground.rect;
            float aspect = s.width / Mathf.Max(s.height, 1f);
            var canvasRT = background.GetComponentInParent<Canvas>()?.transform as RectTransform;
            float h = (canvasRT != null && canvasRT.rect.height > 1f) ? canvasRT.rect.height : 800f;
            background.sizeDelta = new Vector2(h * aspect, h);
            background.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(background);

            // 결함 스프라이트도 미리보기로 채워 둔다(드래그할 때 보이게)
            if (defect != null && ip.defectSprite != null)
            {
                var dImg = defect.GetComponent<Image>();
                if (dImg != null) { Undo.RecordObject(dImg, "Preview defect"); dImg.sprite = ip.defectSprite; dImg.color = Color.white; EditorUtility.SetDirty(dImg); }
            }
            Debug.Log($"[DefectAuthoring] '{ip.ipId}' 배경 미리보기 완료 — Game 뷰에서 보세요. 결함을 드래그/크기조절 후 '3) IP에 저장'.");
        }

        [ContextMenu("2) IP에서 결함 불러오기")]
        void LoadFromIp()
        {
            if (background == null || defect == null || ip == null) { Debug.LogWarning("[DefectAuthoring] background/defect/ip 필요."); return; }

            // 가운데 앵커 + sizeDelta 형태로 풀어 → 자유롭게 드래그 가능
            float bw = background.rect.width, bh = background.rect.height;
            Undo.RecordObject(defect, "Load defect");
            defect.anchorMin = defect.anchorMax = defect.pivot = new Vector2(0.5f, 0.5f);
            defect.sizeDelta = new Vector2(ip.defectSize.x * bw, ip.defectSize.y * bh);
            defect.anchoredPosition = new Vector2((ip.defectPosition.x - 0.5f) * bw, (ip.defectPosition.y - 0.5f) * bh);
            EditorUtility.SetDirty(defect);
            Debug.Log($"[DefectAuthoring] '{ip.ipId}' 불러옴 — pos={ip.defectPosition}, size={ip.defectSize}");
        }

        [ContextMenu("3) 결함 위치·크기 → IP에 저장")]
        void CaptureToIp()
        {
            if (background == null || defect == null || ip == null) { Debug.LogWarning("[DefectAuthoring] background/defect/ip 필요."); return; }

            // 결함의 월드 코너 → 배경 로컬 → 0~1 정규화
            var c = new Vector3[4]; defect.GetWorldCorners(c);   // 0=좌하, 2=우상
            Vector3 bl = background.InverseTransformPoint(c[0]);
            Vector3 tr = background.InverseTransformPoint(c[2]);
            Rect r = background.rect;
            float nxMin = (bl.x - r.xMin) / r.width;
            float nyMin = (bl.y - r.yMin) / r.height;
            float nxMax = (tr.x - r.xMin) / r.width;
            float nyMax = (tr.y - r.yMin) / r.height;

            Vector2 pos  = new Vector2((nxMin + nxMax) * 0.5f, (nyMin + nyMax) * 0.5f);
            Vector2 size = new Vector2(Mathf.Abs(nxMax - nxMin), Mathf.Abs(nyMax - nyMin));

            Undo.RecordObject(ip, "Capture defect");
            ip.defectPosition = pos;
            ip.defectSize = size;
            EditorUtility.SetDirty(ip);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DefectAuthoring] '{ip.ipId}' 저장 완료 — defectPosition={pos}, defectSize={size}");
        }
#endif
    }
}