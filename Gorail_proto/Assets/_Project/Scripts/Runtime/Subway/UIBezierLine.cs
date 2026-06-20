using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Subway
{
    /// <summary>
    /// 두 역을 잇는 노선 곡선. Canvas 안에서 동작하는 커스텀 Graphic.
    ///
    /// 워크플로:
    ///   1. 이 컴포넌트를 가진 GameObject를 [Lines] 컨테이너 아래에 둔다.
    ///   2. 자식 오브젝트(빈 GameObject)를 원하는 만큼 추가하고 씬 뷰에서 드래그로 위치 조정.
    ///      → 자식 순서대로 Catmull-Rom 스플라인 통과점이 된다.
    ///   3. stationA, stationB, lineId만 인스펙터에서 입력.
    ///
    /// [ExecuteAlways]로 에디터에서도 자식 이동 시 곡선이 즉시 갱신된다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIBezierLine : Graphic
    {
        [Header("연결 정보")]
        public string stationA;
        public string stationB;
        public string lineId;

        [Header("두께·품질")]
        public float baseWidth = 6f;
        [Tooltip("구간당 보간 단계 수. 높을수록 부드러움.")]
        public int segments = 40;

        private float _widthScale = 1f;

        /// <summary>줌 역보정 배율. SubwayMapRenderer가 줌 변경 시 호출.</summary>
        public void SetWidthScale(float scale)
        {
            _widthScale = scale;
            SetVerticesDirty();
        }

        // 자식 위치가 바뀌면 에디터·런타임 모두 재그리기
        void OnTransformChildrenChanged()
        {
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var pts = CollectPoints();
            if (pts.Count < 2) return;

            float halfW = baseWidth * _widthScale * 0.5f;
            var sampled = SampleCatmullRom(pts, segments);

            for (int i = 0; i < sampled.Count - 1; i++)
            {
                Vector2 a   = sampled[i];
                Vector2 b   = sampled[i + 1];
                Vector2 dir = (b - a);
                if (dir.sqrMagnitude < 0.0001f) continue;
                Vector2 perp = new Vector2(-dir.normalized.y, dir.normalized.x) * halfW;

                int v = vh.currentVertCount;
                vh.AddVert(a - perp, color, Vector2.zero);
                vh.AddVert(a + perp, color, Vector2.zero);
                vh.AddVert(b + perp, color, Vector2.zero);
                vh.AddVert(b - perp, color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }

        /// <summary>
        /// 자식 오브젝트의 월드 위치를 이 Graphic의 로컬 스페이스로 변환해 수집한다.
        /// OnPopulateMesh의 좌표계 = Graphic RectTransform 로컬 스페이스이므로
        /// anchoredPosition 대신 InverseTransformPoint를 사용해야 앵커·피벗 설정과 무관하게 정확히 일치한다.
        /// </summary>
        List<Vector2> CollectPoints()
        {
            var pts = new List<Vector2>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                Vector2 local = transform.InverseTransformPoint(child.position);
                pts.Add(local);
            }
            return pts;
        }

        static List<Vector2> SampleCatmullRom(List<Vector2> pts, int segsPerSpan)
        {
            var result = new List<Vector2>();
            int n = pts.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(i - 1, 0)];
                Vector2 p1 = pts[i];
                Vector2 p2 = pts[i + 1];
                Vector2 p3 = pts[Mathf.Min(i + 2, n - 1)];
                int count = (i == n - 2) ? segsPerSpan + 1 : segsPerSpan;
                for (int j = 0; j < count; j++)
                {
                    float t = j / (float)segsPerSpan;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            return result;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
