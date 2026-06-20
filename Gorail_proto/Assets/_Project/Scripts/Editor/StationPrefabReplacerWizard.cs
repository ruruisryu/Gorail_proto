using System.Collections.Generic;
using Game.Subway;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Stations] 아래 역 GO를 노선 수·특별역 여부에 맞는 프리팹으로 교체한다.
/// 메뉴: Subway ▶ Replace Station Prefabs
///
/// 분류 기준:
///   특별역 = StationFeature.Landmark 또는 Shop
///   노선 수 = networkData에서 해당 stationId가 등장하는 고유 lineId 수
/// </summary>
public class StationPrefabReplacerWizard : ScriptableWizard
{
    [Header("씬 참조")]
    public SubwayMapRenderer mapRenderer;

    [Header("프리팹 (비워두면 해당 종류 스킵)")]
    public GameObject prefabGeneral;           // 일반역          (1노선, 비특별)
    public GameObject prefabSpecial;           // 특별역          (1노선, 특별)
    public GameObject prefabTransfer2;         // 환승역 2노선     (비특별)
    public GameObject prefabTransfer2Special;  // 환승역 2노선     (특별)
    public GameObject prefabTransfer3;         // 환승역 3노선+    (비특별)
    public GameObject prefabTransfer3Special;  // 환승역 3노선+    (특별)

    [Header("옵션")]
    [Tooltip("프리팹 교체 없이 lineRings의 lineId만 채울 때 체크")]
    public bool fillLineIdsOnly = false;

    // EditorPrefs 키
    const string K_Renderer       = "SPR_mapRenderer";
    const string K_General        = "SPR_prefabGeneral";
    const string K_Special        = "SPR_prefabSpecial";
    const string K_Transfer2      = "SPR_prefabTransfer2";
    const string K_Transfer2Spec  = "SPR_prefabTransfer2Special";
    const string K_Transfer3      = "SPR_prefabTransfer3";
    const string K_Transfer3Spec  = "SPR_prefabTransfer3Special";

    [MenuItem("Subway/Replace Station Prefabs")]
    static void Open() =>
        DisplayWizard<StationPrefabReplacerWizard>("역 프리팹 교체", "교체 실행");

    void OnEnable()
    {
        mapRenderer          = LoadObj<SubwayMapRenderer>(K_Renderer);
        prefabGeneral        = LoadObj<GameObject>(K_General);
        prefabSpecial        = LoadObj<GameObject>(K_Special);
        prefabTransfer2      = LoadObj<GameObject>(K_Transfer2);
        prefabTransfer2Special = LoadObj<GameObject>(K_Transfer2Spec);
        prefabTransfer3      = LoadObj<GameObject>(K_Transfer3);
        prefabTransfer3Special = LoadObj<GameObject>(K_Transfer3Spec);
    }

    // 필드가 바뀔 때마다 자동 저장
    void OnWizardUpdate()
    {
        SaveObj(K_Renderer,      mapRenderer);
        SaveObj(K_General,       prefabGeneral);
        SaveObj(K_Special,       prefabSpecial);
        SaveObj(K_Transfer2,     prefabTransfer2);
        SaveObj(K_Transfer2Spec, prefabTransfer2Special);
        SaveObj(K_Transfer3,     prefabTransfer3);
        SaveObj(K_Transfer3Spec, prefabTransfer3Special);
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

        var so          = new SerializedObject(mapRenderer);
        var networkData = so.FindProperty("networkData" ).objectReferenceValue as SubwayNetworkData;
        var mapContainer= so.FindProperty("mapContainer").objectReferenceValue as RectTransform;

        if (networkData == null || mapContainer == null)
        {
            EditorUtility.DisplayDialog("오류", "SubwayMapRenderer의 networkData·mapContainer가 비어 있습니다.", "확인");
            return;
        }

        // ── 역별 노선 수 집계 ─────────────────────────────────────────
        var stationLines = new Dictionary<string, HashSet<string>>();
        foreach (var line in networkData.lines)
        {
            if (line == null) continue;
            foreach (var stn in line.stations)
            {
                if (stn == null) continue;
                if (!stationLines.ContainsKey(stn.stationId))
                    stationLines[stn.stationId] = new HashSet<string>();
                stationLines[stn.stationId].Add(line.lineId);
            }
        }

        // ── [Stations] 컨테이너 ───────────────────────────────────────
        var stationsT = mapContainer.Find("[Stations]");
        if (stationsT == null)
        {
            EditorUtility.DisplayDialog("오류", "[Stations] 컨테이너가 없습니다.", "확인");
            return;
        }

        // 역별 노선 ID 목록 (networkData 순서 유지)
        var stationLineIds = new Dictionary<string, List<string>>();
        foreach (var line in networkData.lines)
        {
            if (line == null) continue;
            foreach (var stn in line.stations)
            {
                if (stn == null) continue;
                if (!stationLineIds.ContainsKey(stn.stationId))
                    stationLineIds[stn.stationId] = new List<string>();
                if (!stationLineIds[stn.stationId].Contains(line.lineId))
                    stationLineIds[stn.stationId].Add(line.lineId);
            }
        }

        var views = stationsT.GetComponentsInChildren<StationView>(true);

        int replaced = 0, filled = 0, skipped = 0;
        foreach (var view in views)
        {
            if (view == null) continue;

            // stationData가 없으면 GO 이름으로 추론
            StationData data = view.stationData;
            if (data == null)
            {
                var n = view.gameObject.name;
                if (n.StartsWith("Stn_"))
                {
                    var id = n.Substring(4);
                    foreach (var line in networkData.lines)
                        foreach (var stn in line?.stations ?? new List<StationData>())
                            if (stn != null && stn.stationId == id) { data = stn; break; }
                }
                if (data == null) { skipped++; continue; }
            }

            stationLineIds.TryGetValue(data.stationId, out var lineIds);

            if (fillLineIdsOnly)
            {
                // 교체 없이 lineId·색·라벨만 채우기
                FillLineRingIds(view.gameObject, lineIds, networkData);
                view.SetLabel(data.displayName);
                EditorUtility.SetDirty(view.gameObject);
                filled++;
            }
            else
            {
                int lineCount  = lineIds?.Count ?? 1;
                bool isSpecial = data.featureType == StationFeature.Landmark
                              || data.featureType == StationFeature.Shop;

                GameObject prefab = PickPrefab(lineCount, isSpecial);
                if (prefab == null) { skipped++; continue; }

                if (PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject) == prefab)
                {
                    FillLineRingIds(view.gameObject, lineIds, networkData);
                    view.SetLabel(data.displayName);
                    EditorUtility.SetDirty(view.gameObject);
                    skipped++;
                    continue;
                }

                var newGO = Replace(view, data, prefab);
                FillLineRingIds(newGO, lineIds, networkData);
                newGO.GetComponent<StationView>()?.SetLabel(data.displayName);
                EditorUtility.SetDirty(newGO);
                replaced++;
            }
        }

        string msg = fillLineIdsOnly
            ? $"lineId 채움: {filled}개 / 스킵: {skipped}개"
            : $"교체: {replaced}개 / lineId 채움 포함 / 스킵: {skipped}개";
        EditorUtility.DisplayDialog("완료", msg, "확인");
    }

    GameObject PickPrefab(int lineCount, bool isSpecial)
    {
        if (lineCount >= 3) return isSpecial ? prefabTransfer3Special : prefabTransfer3;
        if (lineCount == 2) return isSpecial ? prefabTransfer2Special : prefabTransfer2;
        return isSpecial ? prefabSpecial : prefabGeneral;
    }

    static GameObject Replace(StationView oldView, StationData data, GameObject prefab)
    {
        var oldGO    = oldView.gameObject;
        var oldRT    = oldGO.GetComponent<RectTransform>();
        var parent   = oldRT.parent;
        var sibIdx   = oldRT.GetSiblingIndex();
        var name     = oldGO.name;
        var aPos     = oldRT.anchoredPosition;
        var scale    = oldRT.localScale;
        var rotation = oldRT.localRotation;

        Undo.DestroyObjectImmediate(oldGO);

        var newGO = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        Undo.RegisterCreatedObjectUndo(newGO, "Replace Station Prefab");

        newGO.name = name;
        newGO.transform.SetSiblingIndex(sibIdx);

        var newRT = newGO.GetComponent<RectTransform>();
        newRT.anchorMin        = Vector2.one * 0.5f;
        newRT.anchorMax        = Vector2.one * 0.5f;
        newRT.pivot            = Vector2.one * 0.5f;
        newRT.anchoredPosition = aPos;
        newRT.localScale       = scale;
        newRT.localRotation    = rotation;

        var newView = newGO.GetComponent<StationView>();
        if (newView != null) newView.stationData = data;

        EditorUtility.SetDirty(newGO);
        return newGO;
    }

    /// <summary>
    /// StationView.lineRings 의 lineId 필드를 networkData 순서의 lineIds로 채운다.
    /// ring(Image) 참조는 프리팹에서 이미 연결되어 있으므로 건드리지 않는다.
    /// </summary>
    static void FillLineRingIds(GameObject go, List<string> lineIds, SubwayNetworkData networkData)
    {
        if (lineIds == null || lineIds.Count == 0) return;
        var view = go.GetComponent<StationView>();
        if (view == null) return;

        // lineId → lineColor 조회 테이블
        var colorMap = new Dictionary<string, Color>();
        if (networkData != null)
            foreach (var line in networkData.lines)
                if (line != null && !colorMap.ContainsKey(line.lineId))
                    colorMap[line.lineId] = line.lineColor;

        var viewSO    = new SerializedObject(view);
        var ringsProp = viewSO.FindProperty("lineRings");
        if (ringsProp == null) return;

        for (int i = 0; i < ringsProp.arraySize && i < lineIds.Count; i++)
        {
            var elem = ringsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("lineId").stringValue = lineIds[i];

            // ring Image 색 적용
            var ringProp = elem.FindPropertyRelative("ring");
            if (ringProp?.objectReferenceValue is UnityEngine.UI.Image img
                && colorMap.TryGetValue(lineIds[i], out var col))
            {
                Undo.RecordObject(img, "Set Ring Color");
                img.color = col;
                EditorUtility.SetDirty(img);
            }
        }

        viewSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(go);
    }
}

