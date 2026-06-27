using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory
{
    /// <summary>
    /// 0~1 값을 원형(파이)으로 채워 보여주는 재사용 게이지.
    /// 완성도(재료 배치)·작품활동 진행 등 "비율을 원으로 채우는" 곳에 공용으로 쓴다.
    /// 원형 모양은 흰 원 스프라이트(Unity 기본 Circle 등)를 Filled·Radial360으로 채워 만든다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RadialGauge : MonoBehaviour
    {
        private Image _track, _fill;
        private TextMeshProUGUI _label;

        /// <summary>게이지 시각요소 생성. 코드에서 한 번 호출.</summary>
        public void Build(Sprite circle, Color trackColor, Color fillColor,
                          float size, TMP_FontAsset font, float labelSize = 22f)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(size, size);

            _track = NewCircle("Track", rt, circle, trackColor, size);

            _fill = NewCircle("Fill", rt, circle, fillColor, size);
            _fill.type          = Image.Type.Filled;
            _fill.fillMethod    = Image.FillMethod.Radial360;
            _fill.fillOrigin    = (int)Image.Origin360.Top;
            _fill.fillClockwise = true;
            _fill.fillAmount    = 0f;

            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(rt, false);
            var lrt = (RectTransform)lgo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            _label = lgo.AddComponent<TextMeshProUGUI>();
            if (font != null) _label.font = font;
            _label.fontSize  = labelSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color     = Color.white;
        }

        /// <summary>채움 비율(0~1) + 가운데 텍스트(선택) 갱신.</summary>
        public void SetValue(float v01, string center = null)
        {
            if (_fill != null) _fill.fillAmount = Mathf.Clamp01(v01);
            if (center != null && _label != null) _label.text = center;
        }

        public void SetFillColor(Color c) { if (_fill != null) _fill.color = c; }

        static Image NewCircle(string name, Transform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;         // 비우면 사각형으로라도 표시(원 스프라이트 권장)
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}