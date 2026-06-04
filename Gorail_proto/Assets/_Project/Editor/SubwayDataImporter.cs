using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Subway;

/// <summary>
/// subway_lines_1to9_seoul.json 을 읽어 1~9호선 에셋을 생성한다.
/// §3-3-1 절단 규칙 자동 적용 (IS_TRANSFER 플래그 활용).
/// Menu: Subway / ① Import Station Data
/// </summary>
public static class SubwayDataImporter
{
    // ── 입력 ─────────────────────────────────────────────────────────
    const string DataJson    = @"C:\Users\atz99\Downloads\subway_lines_1to9_seoul.json";

    // ── 출력 ─────────────────────────────────────────────────────────
    const string StationsDir = "Assets/_Project/Data/Subway/Stations";
    const string LinesDir    = "Assets/_Project/Data/Subway/Lines";
    const string NetworkPath = "Assets/_Project/Data/Subway/SubwayNetwork.asset";

    // ── §3-3-1 절단 파라미터 ──────────────────────────────────────────
    const int CutN = 1;

    // ── 860×550 캔버스 변환 바운드 ────────────────────────────────────
    const float LngMin = 126.77f, LngMax = 127.22f;
    const float LatMin = 37.44f,  LatMax = 37.70f;
    const float RefW   = 860f,    RefH   = 550f;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // JSON 파싱 클래스 (JsonUtility 호환)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    [Serializable] class DataRoot { public StationEntry[] stations; }
    [Serializable]
    class StationEntry
    {
        public string LINE_NUM;
        public string STATION_NM;
        public string STATION_CD;
        public string FR_CODE;
        public bool   IS_TRANSFER;
        public float  LNG;
        public float  LAT;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 노선 정의
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    class LineDef
    {
        public string key, id, name;
        public Color  color;
        public bool   circular;
    }

    static readonly LineDef[] AllLines =
    {
        new LineDef { key="01",          id="Line1",         name="1호선",        color=new Color(0.00f,0.32f,0.64f), circular=false },
        new LineDef { key="02",          id="Line2",         name="2호선",        color=new Color(0.20f,0.70f,0.29f), circular=true  },
        new LineDef { key="02_seongsu",  id="Line2Seongsu",  name="2호선 성수지선", color=new Color(0.20f,0.70f,0.29f), circular=false },
        new LineDef { key="02_sinjeong", id="Line2Sinjeong", name="2호선 신정지선", color=new Color(0.20f,0.70f,0.29f), circular=false },
        new LineDef { key="03",          id="Line3",         name="3호선",        color=new Color(1.00f,0.55f,0.00f), circular=false },
        new LineDef { key="04",          id="Line4",         name="4호선",        color=new Color(0.27f,0.51f,0.84f), circular=false },
        new LineDef { key="05",          id="Line5",         name="5호선",        color=new Color(0.59f,0.35f,0.69f), circular=false },
        new LineDef { key="05_macheon",  id="Line5Macheon",  name="5호선 마천지선", color=new Color(0.59f,0.35f,0.69f), circular=false },
        new LineDef { key="06",          id="Line6",         name="6호선",        color=new Color(0.80f,0.49f,0.18f), circular=false },
        new LineDef { key="07",          id="Line7",         name="7호선",        color=new Color(0.42f,0.62f,0.24f), circular=false },
        new LineDef { key="08",          id="Line8",         name="8호선",        color=new Color(0.90f,0.09f,0.42f), circular=false },
        new LineDef { key="09",          id="Line9",         name="9호선",        color=new Color(0.74f,0.69f,0.57f), circular=false },
    };

    // 지선 연결점 (지선 첫 역에 메인 노선 역 삽입)
    static readonly (string branchKey, string connName, string mainKey)[] BranchConns =
    {
        ("02_seongsu",  "성수",   "02"),
        ("02_sinjeong", "신도림", "02"),
        ("05_macheon",  "강동",   "05"),
    };

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 내부 작업 클래스
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    class Node
    {
        public string  name;
        public string  assetId;
        public bool    isTransfer;
        public Vector2 pos;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    [MenuItem("Subway/① Import Station Data")]
    static void Run()
    {
        Debug.Log("[SubwayImporter] ▶ 시작");

        var entries    = ParseJson();
        var lineNodes  = BuildLines(entries);

        ApplyCutting(lineNodes);
        CreateAssets(lineNodes);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SubwayImporter] ✔ 완료");
    }

    // ── 1. JSON 파싱 ──────────────────────────────────────────────────
    static List<StationEntry> ParseJson()
    {
        string json = File.ReadAllText(DataJson, System.Text.Encoding.UTF8);
        var root = JsonUtility.FromJson<DataRoot>(json);
        Debug.Log($"[SubwayImporter] 파싱: {root.stations.Length}역");
        return root.stations.ToList();
    }

    // ── 2. 노선별 역 노드 구성 ────────────────────────────────────────
    static Dictionary<string, List<Node>> BuildLines(List<StationEntry> entries)
    {
        var result = AllLines.ToDictionary(l => l.key, _ => new List<Node>());

        foreach (var e in entries)
        {
            string key = LineKey(e.LINE_NUM, e.FR_CODE);
            if (key == null || !result.ContainsKey(key)) continue;

            result[key].Add(new Node
            {
                name       = e.STATION_NM,
                assetId    = ToId(e.STATION_NM),
                isTransfer = e.IS_TRANSFER,
                pos        = ToCanvas(e.LNG, e.LAT),
            });
        }

        // 지선 연결점 삽입
        foreach (var (bk, connName, mk) in BranchConns)
        {
            if (!result.ContainsKey(bk) || !result.ContainsKey(mk)) continue;
            var conn = result[mk].FirstOrDefault(n => n.name == connName);
            if (conn == null || result[bk].Any(n => n.name == connName)) continue;
            result[bk].Insert(0, new Node
            {
                name       = conn.name,
                assetId    = conn.assetId,
                isTransfer = true,
                pos        = conn.pos,
            });
        }

        return result;
    }

    static string LineKey(string lineNum, string fr)
    {
        switch (lineNum)
        {
            case "01호선": return "01";
            case "02호선":
                if (fr.StartsWith("211-")) return "02_seongsu";
                if (fr.StartsWith("234-")) return "02_sinjeong";
                return "02";
            case "03호선": return "03";
            case "04호선": return "04";
            case "05호선":
                if (fr.Length > 1 && fr[0] == 'P' &&
                    int.TryParse(fr.Substring(1), out int n) && n >= 549 && n <= 555)
                    return "05_macheon";
                return "05";
            case "06호선": return "06";
            case "07호선": return "07";
            case "08호선": return "08";
            case "09호선": return "09";
        }
        return null;
    }

    // ── 3. §3-3-1 절단 규칙 ──────────────────────────────────────────
    static void ApplyCutting(Dictionary<string, List<Node>> lineNodes)
    {
        // 비순환 메인 노선만 절단 (02는 순환선 제외, 지선은 원래 짧으므로 제외)
        var targets = new[] { "01", "03", "04", "05", "06", "07", "08", "09" };

        foreach (var key in targets)
        {
            if (!lineNodes.ContainsKey(key)) continue;
            var list = lineNodes[key];

            var tIdx = list
                .Select((n, i) => (n, i))
                .Where(t => t.n.isTransfer)
                .Select(t => t.i)
                .ToList();

            if (tIdx.Count == 0) continue;

            int from = Math.Max(0, tIdx.First() - CutN);
            int to   = Math.Min(list.Count - 1, tIdx.Last() + CutN);
            int before = list.Count;
            lineNodes[key] = list.GetRange(from, to - from + 1);

            Debug.Log($"[SubwayImporter] {key}: {before}역 → {to - from + 1}역");
        }
    }

    // ── 4. 에셋 생성 ─────────────────────────────────────────────────
    static void CreateAssets(Dictionary<string, List<Node>> lineNodes)
    {
        EnsureFolder(StationsDir);
        EnsureFolder(LinesDir);
        ClearFolder(StationsDir);
        ClearFolder(LinesDir);

        var stnAssets   = new Dictionary<string, StationData>();
        var networkList = new List<LineData>();

        foreach (var def in AllLines)
        {
            if (!lineNodes.TryGetValue(def.key, out var nodes) || nodes.Count == 0)
            {
                Debug.LogWarning($"[SubwayImporter] {def.key}: 역 없음 — 스킵");
                continue;
            }

            var lineData = ScriptableObject.CreateInstance<LineData>();
            lineData.lineId      = def.id;
            lineData.displayName = def.name;
            lineData.lineColor   = def.color;
            lineData.isCircular  = def.circular;
            lineData.stations    = new List<StationData>();

            foreach (var node in nodes)
            {
                if (!stnAssets.TryGetValue(node.assetId, out var sd))
                {
                    sd = ScriptableObject.CreateInstance<StationData>();
                    sd.stationId   = node.assetId;
                    sd.displayName = node.name;
                    sd.mapPosition = node.pos;
                    AssetDatabase.CreateAsset(sd, $"{StationsDir}/{node.assetId}.asset");
                    stnAssets[node.assetId] = sd;
                }
                lineData.stations.Add(sd);
            }

            AssetDatabase.CreateAsset(lineData, $"{LinesDir}/{def.id}.asset");
            networkList.Add(lineData);
            Debug.Log($"[SubwayImporter] {def.name}: {nodes.Count}역");
        }

        var net = AssetDatabase.LoadAssetAtPath<SubwayNetworkData>(NetworkPath);
        if (net == null)
        {
            net = ScriptableObject.CreateInstance<SubwayNetworkData>();
            AssetDatabase.CreateAsset(net, NetworkPath);
        }
        net.lines = networkList;
        EditorUtility.SetDirty(net);

        Debug.Log($"[SubwayImporter] 역 에셋 {stnAssets.Count}개 / 노선 {networkList.Count}개");
    }

    // ── 유틸 ──────────────────────────────────────────────────────────
    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static void ClearFolder(string path)
    {
        foreach (var g in AssetDatabase.FindAssets("t:ScriptableObject", new[] { path }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
    }

    static string ToId(string name) =>
        name.Replace(" ", "_").Replace("/", "_").Replace("·", "_");

    static Vector2 ToCanvas(float lng, float lat)
    {
        float x = (lng - LngMin) / (LngMax - LngMin) * RefW;
        float y = (LatMax - lat) / (LatMax - LatMin) * RefH;
        return new Vector2(Mathf.Clamp(x, 0, RefW), Mathf.Clamp(y, 0, RefH));
    }
}
