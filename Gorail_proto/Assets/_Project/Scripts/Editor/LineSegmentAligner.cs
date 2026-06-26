using UnityEditor;
using UnityEngine;
using Game.Subway;

namespace Game.Editor
{
    /// <summary>
    /// UIBezierLine 세그먼트를 stationA·stationB 사이 중앙에 자동 배치.
    /// 위치·회전·가로 길이를 한 번에 맞춰준다.
    /// </summary>
    public static class LineSegmentAligner
    {
        [MenuItem("Subway/Align Line Segments to Stations")]
        static void AlignAll()
        {
            // [Stations] 컨테이너에서 역 이름 → RectTransform 맵 구성
            var stationMap = BuildStationMap();
            if (stationMap.Count == 0)
            {
                Debug.LogWarning("[LineSegmentAligner] Stn_ 오브젝트를 찾지 못했습니다.");
                return;
            }

            var segments = Object.FindObjectsByType<UIBezierLine>();
            int aligned = 0;

            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg.stationA) || string.IsNullOrEmpty(seg.stationB))
                    continue;

                if (!stationMap.TryGetValue(seg.stationA, out var rtA) ||
                    !stationMap.TryGetValue(seg.stationB, out var rtB))
                {
                    Debug.LogWarning($"[LineSegmentAligner] 역 위치 없음: {seg.stationA} 또는 {seg.stationB}");
                    continue;
                }

                var rt = seg.GetComponent<RectTransform>();
                Undo.RecordObject(rt, "Align Line Segment");

                rt.anchoredPosition = (rtA.anchoredPosition + rtB.anchoredPosition) * 0.5f;

                EditorUtility.SetDirty(rt);
                aligned++;
            }

            Debug.Log($"[LineSegmentAligner] {aligned} / {segments.Length} 세그먼트 정렬 완료.");
        }

        static System.Collections.Generic.Dictionary<string, RectTransform> BuildStationMap()
        {
            var map = new System.Collections.Generic.Dictionary<string, RectTransform>();
            foreach (var go in Object.FindObjectsByType<GameObject>())
            {
                if (!go.name.StartsWith("Stn_")) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) map[go.name.Substring(4)] = rt;
            }
            return map;
        }
    }
}
