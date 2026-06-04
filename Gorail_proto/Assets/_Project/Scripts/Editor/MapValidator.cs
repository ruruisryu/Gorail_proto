using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Subway;

namespace Game.Editor
{
    /// <summary>
    /// [버퍼 H4] 맵 데이터 무결성 검사 도구.
    /// SubwayNetworkData를 훑어 추격 로직이 딛고 설 그래프의 결함을 찾는다 —
    /// 끊긴 엣지·미연결 역·중복/빈 stationId·역 2개 미만 노선·비연결 컴포넌트,
    /// 그리고 환승역 비율(§3-3 목표 15~20%)을 리포트한다.
    /// 추격 버그가 데이터 문제인지 로직 문제인지 빠르게 가려준다.
    /// </summary>
    public static class MapValidator
    {
        [MenuItem("Tools/Subway/Validate Map Data")]
        public static void ValidateMenu()
        {
            var net = LoadFirstNetwork();
            if (net == null)
            {
                Debug.LogWarning("[MapValidator] SubwayNetworkData 에셋을 찾을 수 없습니다.");
                return;
            }
            Debug.Log(BuildReport(net));
        }

        public static SubwayNetworkData LoadFirstNetwork()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(SubwayNetworkData));
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<SubwayNetworkData>(path);
        }

        /// <summary>순수 검사 — 네트워크를 받아 사람이 읽는 리포트 문자열을 만든다.</summary>
        public static string BuildReport(SubwayNetworkData net)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MapValidator] 맵 데이터 무결성 리포트");

            if (net == null || net.lines == null)
            {
                sb.AppendLine("  ✗ 네트워크/노선 리스트가 null");
                return sb.ToString();
            }

            var errors   = new List<string>();
            var warnings = new List<string>();

            // stationId -> 그 id를 쓰는 StationData 인스턴스 집합 (중복·빈 id 검출)
            var idToAssets = new Dictionary<string, HashSet<StationData>>();
            // 인접 그래프 (비연결 컴포넌트·고립역 검출)
            var adj = new Dictionary<string, HashSet<string>>();
            var allStationIds = new HashSet<string>();
            int transferLineCountStations; // 계산은 아래에서

            int lineIdx = 0;
            foreach (var line in net.lines)
            {
                if (line == null) { errors.Add($"노선[{lineIdx}]이 null"); lineIdx++; continue; }
                var stns = line.stations;
                if (stns == null || stns.Count < 2)
                    errors.Add($"노선 '{line.lineId}'의 역이 2개 미만(추격 경로 불가)");

                if (stns != null)
                {
                    for (int i = 0; i < stns.Count; i++)
                    {
                        var s = stns[i];
                        if (s == null) { errors.Add($"노선 '{line.lineId}' 인덱스 {i}에 null 역"); continue; }
                        if (string.IsNullOrEmpty(s.stationId))
                        { errors.Add($"노선 '{line.lineId}'에 빈 stationId 역('{s.name}')"); continue; }

                        allStationIds.Add(s.stationId);
                        if (!idToAssets.TryGetValue(s.stationId, out var set))
                            idToAssets[s.stationId] = set = new HashSet<StationData>();
                        set.Add(s);
                        if (!adj.ContainsKey(s.stationId)) adj[s.stationId] = new HashSet<string>();
                    }
                    // 엣지
                    for (int i = 0; i < stns.Count - 1; i++)
                    {
                        if (stns[i] == null || stns[i + 1] == null) continue;
                        AddEdge(adj, stns[i].stationId, stns[i + 1].stationId);
                    }
                    if (line.isCircular && stns.Count > 1 && stns[0] != null && stns[stns.Count - 1] != null)
                        AddEdge(adj, stns[stns.Count - 1].stationId, stns[0].stationId);
                }
                lineIdx++;
            }

            // 중복 stationId(서로 다른 StationData가 같은 id)
            foreach (var kv in idToAssets)
                if (kv.Value.Count > 1)
                    errors.Add($"중복 stationId '{kv.Key}' — {kv.Value.Count}개 StationData가 공유");

            // 고립역(엣지 0)
            foreach (var id in allStationIds)
                if (!adj.TryGetValue(id, out var nb) || nb.Count == 0)
                    warnings.Add($"고립역(엣지 없음): {id}");

            // 환승역 비율(§3-3 목표 15~20%)
            int transferStations = CountTransferStations(net);
            int total = allStationIds.Count;
            float ratio = total > 0 ? (float)transferStations / total : 0f;
            transferLineCountStations = transferStations;

            // 비연결 컴포넌트 수
            int components = CountComponents(adj, allStationIds);
            if (components > 1)
                warnings.Add($"비연결 컴포넌트 {components}개 — 노선망이 끊겨 있음(추격 경로 없는 구간 존재)");

            // ── 출력 ──
            sb.AppendLine($"  노선 수: {net.lines.Count(l => l != null)}");
            sb.AppendLine($"  고유 역: {total}");
            sb.AppendLine($"  환승역: {transferLineCountStations} ({ratio:P1})  [§3-3 목표 15~20%]");
            if (ratio < 0.15f || ratio > 0.20f)
                warnings.Add($"환승역 비율 {ratio:P1}이 목표(15~20%) 밖 — 절단 규칙으로 조정 여지(§3-3)");

            sb.AppendLine($"  컴포넌트: {components}");
            sb.AppendLine();
            sb.AppendLine($"  ✗ 오류 {errors.Count}건:");
            foreach (var e in errors) sb.AppendLine($"     - {e}");
            sb.AppendLine($"  ⚠ 경고 {warnings.Count}건:");
            foreach (var w in warnings.Take(30)) sb.AppendLine($"     - {w}");
            if (warnings.Count > 30) sb.AppendLine($"     …외 {warnings.Count - 30}건");

            if (errors.Count == 0 && warnings.Count == 0)
                sb.AppendLine("  ✓ 무결성 이상 없음");

            return sb.ToString();
        }

        static int CountTransferStations(SubwayNetworkData net)
        {
            var lineCount = new Dictionary<string, int>();
            foreach (var line in net.lines)
            {
                if (line?.stations == null) continue;
                var seen = new HashSet<string>();
                foreach (var s in line.stations)
                {
                    if (s == null || string.IsNullOrEmpty(s.stationId)) continue;
                    if (seen.Add(s.stationId)) // 한 노선이 같은 역을 중복 포함해도 1회만
                        lineCount[s.stationId] = lineCount.TryGetValue(s.stationId, out var c) ? c + 1 : 1;
                }
            }
            return lineCount.Count(kv => kv.Value > 1);
        }

        static void AddEdge(Dictionary<string, HashSet<string>> adj, string a, string b)
        {
            if (a == b) return;
            if (!adj.ContainsKey(a)) adj[a] = new HashSet<string>();
            if (!adj.ContainsKey(b)) adj[b] = new HashSet<string>();
            adj[a].Add(b);
            adj[b].Add(a);
        }

        static int CountComponents(Dictionary<string, HashSet<string>> adj, HashSet<string> all)
        {
            var visited = new HashSet<string>();
            int comps = 0;
            foreach (var start in all)
            {
                if (visited.Contains(start)) continue;
                comps++;
                var q = new Queue<string>();
                q.Enqueue(start); visited.Add(start);
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!adj.TryGetValue(cur, out var nb)) continue;
                    foreach (var n in nb)
                        if (visited.Add(n)) q.Enqueue(n);
                }
            }
            return comps;
        }
    }
}
