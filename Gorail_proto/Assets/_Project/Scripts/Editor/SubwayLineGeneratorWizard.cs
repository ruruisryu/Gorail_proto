using System.Collections.Generic;
using Game.Subway;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬에 배치된 StationView 위치를 기반으로 UIBezierLine을 자동 생성한다.
/// 메뉴: Subway ▶ Generate Bezier Lines
///
/// 전제 조건:
///   - [Stations] 컨테이너 아래 역 GO들이 "Stn_<stationId>" 이름으로 배치돼 있을 것
///   - UIBezierLine 컴포넌트가 붙고 자식 GO 2개(웨이포인트)를 가진 프리팹이 준비돼 있을 것
/// </summary>
public class SubwayLineGeneratorWizard : ScriptableWizard
{
    [Tooltip("씬에 있는 SubwayMapRenderer")]
    public SubwayMapRenderer mapRenderer;

    [Tooltip("UIBezierLine + 자식 2개짜리 프리팹")]
    public GameObject bezierLinePrefab;

    [Tooltip("이미 있는 [Lines] 컨테이너를 지우고 새로 생성할지 여부")]
    public bool clearExisting = false;

    const string K_Renderer = "SLGW_mapRenderer";
    const string K_Prefab   = "SLGW_bezierLinePrefab";

    [MenuItem("Subway/Generate Bezier Lines")]
    static void Open() =>
        DisplayWizard<SubwayLineGeneratorWizard>("베지어 라인 자동 생성", "생성");

    void OnEnable()
    {
        mapRenderer      = LoadObj<SubwayMapRenderer>(K_Renderer);
        bezierLinePrefab = LoadObj<GameObject>(K_Prefab);
    }

    void OnWizardUpdate()
    {
        SaveObj(K_Renderer, mapRenderer);
        SaveObj(K_Prefab,   bezierLinePrefab);
    }

    static void SaveObj(string key, Object obj)
    {
        var path = obj != null ? AssetDatabase.GetAssetPath(obj) : "";
        EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
    }

    static T LoadObj<T>(string key) where T : Object
    {
        var guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    void OnWizardCreate()
    {
        if (mapRenderer == null)
        {
            EditorUtility.DisplayDialog("오류", "SubwayMapRenderer를 지정해주세요.", "확인");
            return;
        }
        if (bezierLinePrefab == null)
        {
            EditorUtility.DisplayDialog("오류", "UIBezierLine 프리팹을 지정해주세요.", "확인");
            return;
        }

        // ── private 필드 접근 ─────────────────────────────────────────
        var so = new SerializedObject(mapRenderer);
        var networkData  = so.FindProperty("networkData" ).objectReferenceValue as SubwayNetworkData;
        var mapContainer = so.FindProperty("mapContainer").objectReferenceValue as RectTransform;

        if (networkData == null || mapContainer == null)
        {
            EditorUtility.DisplayDialog("오류", "SubwayMapRenderer의 networkData·mapContainer가 비어 있습니다.", "확인");
            return;
        }

        // ── 역 위치 맵 구성 ([Stations] 하위 StationView) ─────────────
        var stationsT = mapContainer.Find("[Stations]");
        if (stationsT == null)
        {
            EditorUtility.DisplayDialog("오류", "[Stations] 컨테이너가 없습니다. 역을 먼저 배치해주세요.", "확인");
            return;
        }

        var stationRTMap = new Dictionary<string, RectTransform>();
        foreach (var view in stationsT.GetComponentsInChildren<StationView>(true))
        {
            var n = view.gameObject.name;
            if (n.StartsWith("Stn_"))
                stationRTMap[n.Substring(4)] = view.GetComponent<RectTransform>();
        }

        // ── [Lines] 컨테이너 준비 ────────────────────────────────────
        var linesT = mapContainer.Find("[Lines]");
        if (linesT != null && clearExisting)
        {
            Undo.DestroyObjectImmediate(linesT.gameObject);
            linesT = null;
        }

        RectTransform linesRT;
        if (linesT == null)
        {
            var go = new GameObject("[Lines]");
            Undo.RegisterCreatedObjectUndo(go, "Create [Lines]");
            go.transform.SetParent(mapContainer, false);
            linesRT = go.AddComponent<RectTransform>();
            linesRT.anchorMin = linesRT.anchorMax = linesRT.pivot = Vector2.one * 0.5f;
            linesRT.anchoredPosition = Vector2.zero;
            linesRT.sizeDelta        = Vector2.zero;
            go.transform.SetSiblingIndex(0);
        }
        else
        {
            linesRT = linesT.GetComponent<RectTransform>();
        }

        // ── 노선별 인접 역 쌍 순회 → UIBezierLine 생성 ──────────────
        int created = 0, skipped = 0;
        foreach (var line in networkData.lines)
        {
            if (line == null) continue;
            var stations = line.stations;

            for (int i = 0; i < stations.Count - 1; i++)
                if (TryCreate(line, stations[i], stations[i + 1], stationRTMap, linesRT)) created++;
                else skipped++;

            // 순환선 마지막↔첫 번째 구간
            if (line.isCircular && stations.Count > 1)
                if (TryCreate(line, stations[stations.Count - 1], stations[0], stationRTMap, linesRT)) created++;
                else skipped++;
        }

        EditorUtility.DisplayDialog(
            "완료",
            $"UIBezierLine 생성: {created}개\n역 위치 없어 스킵: {skipped}개",
            "확인");
    }

    bool TryCreate(LineData line, StationData a, StationData b,
                   Dictionary<string, RectTransform> rtMap, RectTransform parent)
    {
        if (a == null || b == null) return false;
        if (!rtMap.TryGetValue(a.stationId, out var rtA)) return false;
        if (!rtMap.TryGetValue(b.stationId, out var rtB)) return false;

        var go = PrefabUtility.InstantiatePrefab(bezierLinePrefab, parent) as GameObject;
        Undo.RegisterCreatedObjectUndo(go, "Generate Bezier Line");

        // GO 이름: "1호선_서울역-시청" 같은 형식
        string nameA = string.IsNullOrEmpty(a.displayName) ? a.stationId : a.displayName;
        string nameB = string.IsNullOrEmpty(b.displayName) ? b.stationId : b.displayName;
        go.name = $"{line.lineId}_{nameA}-{nameB}";

        // UIBezierLine 메타데이터 주입
        var bezier = go.GetComponent<UIBezierLine>();
        if (bezier != null)
        {
            bezier.stationA = a.stationId;
            bezier.stationB = b.stationId;
            bezier.lineId   = line.lineId;
            bezier.color    = line.lineColor;
        }

        // 프리팹 자체를 (0,0)에 고정 — 웨이포인트 위치로만 형태 결정
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
        }

        // 자식 웨이포인트를 각 역의 월드 위치로 이동
        // CollectPoints()가 InverseTransformPoint를 쓰므로 월드 위치 기준이 정확함
        if (go.transform.childCount >= 1)
            go.transform.GetChild(0).position = rtA.position;
        if (go.transform.childCount >= 2)
            go.transform.GetChild(1).position = rtB.position;

        EditorUtility.SetDirty(go);
        return true;
    }
}
