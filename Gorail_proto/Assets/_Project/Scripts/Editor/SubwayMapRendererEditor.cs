using Game.Subway;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SubwayMapRenderer))]
public class SubwayMapRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── 맵 편집 도구 ──", EditorStyles.boldLabel);

        var r = (SubwayMapRenderer)target;

        GUI.backgroundColor = new Color(0.45f, 0.85f, 0.45f);
        if (GUILayout.Button("▶  Build Map  (역 + 선 전체 재생성)", GUILayout.Height(34)))
        {
            Undo.RegisterFullObjectHierarchyUndo(r.gameObject, "Build Map");
            r.BuildMap();
            EditorUtility.SetDirty(r.gameObject);
        }

        GUI.backgroundColor = new Color(0.65f, 0.9f, 0.7f);
        if (GUILayout.Button("⤓  Apply Layout  (SO 위치 + Spread → 화면 재배치)", GUILayout.Height(30)))
        {
            Undo.RegisterFullObjectHierarchyUndo(r.gameObject, "Apply Layout");
            r.ApplyLayout();
            EditorUtility.SetDirty(r.gameObject);
        }

        GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
        if (GUILayout.Button("↺  Update Lines  (현재 역 위치 기준 선 갱신)", GUILayout.Height(30)))
        {
            Undo.RegisterFullObjectHierarchyUndo(r.gameObject, "Update Lines");
            r.UpdateLines();
            EditorUtility.SetDirty(r.gameObject);
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.35f);
        if (GUILayout.Button("💾  Save Positions  (역 위치 → StationData SO 저장)", GUILayout.Height(30)))
        {
            r.SavePositions();
        }

        GUI.backgroundColor = Color.white;

        // ── Scene 위치 편집 토글 (선택 상태와 무관하게 동작) ──
        EditorGUILayout.Space(8);
        bool on = StationSceneEditTool.Active;
        bool next = GUILayout.Toggle(
            on, on ? "✋  Scene 위치 편집: 켜짐 — 파란 점 클릭/드래그"
                   : "✋  Scene 위치 편집 (역 점을 드래그해 개별 이동)",
            "Button", GUILayout.Height(28));
        if (next != on) StationSceneEditTool.SetActive(next, r);

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "역 위치의 진짜 주소 = StationData(SO). 프리팹은 모양만 담당.\n\n" +
            "① Build Map — 역+선 최초 생성 (역 종류가 바뀔 때만)\n" +
            "② Station Spread 슬라이더 → 자동 재배치 (수동: Apply Layout)\n" +
            "③ ✋ Scene 위치 편집을 켜고, Scene 뷰에서 파란 점을 드래그 → 그 역만 이동\n" +
            "④ Update Lines로 선 갱신 → Save Positions로 SO에 저장\n\n" +
            "※ 편집 중엔 이동 기즈모가 숨겨져 부모를 실수로 끌 수 없습니다.",
            MessageType.Info);

        if (StationSceneEditTool.Active && StationSceneEditTool.Selected != null)
            EditorGUILayout.HelpBox("선택된 역: " + StationSceneEditTool.Selected.name, MessageType.None);
    }
}

/// <summary>
/// 역 위치를 Scene 뷰에서 개별 드래그로 편집하는 도구.
/// SceneView.duringSceneGui에 전역 구독하므로 Hierarchy 선택 상태와 무관하게 동작한다.
/// (CustomEditor의 OnSceneGUI는 대상이 선택됐을 때만 돌아 부적합했다.)
/// Screen Space-Overlay 맵 위에 보이도록 점을 GUI 오버레이에 그린다.
/// </summary>
[InitializeOnLoad]
static class StationSceneEditTool
{
    public static bool Active { get; private set; }
    public static StationView Selected;

    static SubwayMapRenderer renderer;
    static bool dragging;
    static bool prevToolsHidden;
    static Texture2D dotTex;

    // 도메인 리로드 후엔 도구가 비활성 상태이므로, 숨겨졌을 수 있는 이동 기즈모를 복구한다.
    static StationSceneEditTool()
    {
        Tools.hidden = false;
    }

    public static void SetActive(bool on, SubwayMapRenderer r)
    {
        if (on)
        {
            renderer = r;
            if (!Active)
            {
                SceneView.duringSceneGui += OnScene;
                prevToolsHidden = Tools.hidden;
                Tools.hidden = true;            // 이동 기즈모 숨김 → 부모 실수 이동 방지
            }
            Active = true;
        }
        else
        {
            if (Active)
            {
                SceneView.duringSceneGui -= OnScene;
                Tools.hidden = prevToolsHidden;
            }
            Active = false;
            dragging = false;
            Selected = null;
        }
        SceneView.RepaintAll();
    }

    static void OnScene(SceneView sv)
    {
        if (!Active) return;
        if (renderer == null)
        {
            renderer = Object.FindFirstObjectByType<SubwayMapRenderer>(FindObjectsInactive.Include);
            if (renderer == null) return;
        }
        var stations = FindStations(renderer);
        if (stations == null) return;

        var cam = sv != null ? sv.camera : null;
        var e = Event.current;
        if (dotTex == null) dotTex = MakeDotTex();

        // 점 + 라벨은 GUI 오버레이(최상단)에 그린다.
        Handles.BeginGUI();
        GUI.Label(new Rect(8, 8, 420, 20),
            "● 역 위치 편집 ON — 파란 점을 드래그해 개별 이동, 끝나면 Save Positions");

        foreach (var view in stations)
        {
            if (view == null) continue;
            var rt = view.transform as RectTransform;
            if (rt == null) continue;
            Vector3 world = rt.position;
            if (Culled(cam, world)) continue;

            Vector2 gp = HandleUtility.WorldToGUIPoint(world);
            bool  isSel = (view == Selected);
            float rad   = isSel ? 7f : 4.5f;
            var   rect  = new Rect(gp.x - rad, gp.y - rad, rad * 2f, rad * 2f);

            var prev = GUI.color;
            GUI.color = isSel ? new Color(1f, 0.55f, 0.1f) : new Color(0.2f, 0.7f, 1f);
            GUI.DrawTexture(rect, dotTex);
            GUI.color = prev;

            if (isSel)
                GUI.Label(new Rect(gp.x + 9f, gp.y - 22f, 160f, 18f), view.name);

            if (!dragging && e.type == EventType.MouseDown && e.button == 0 &&
                rect.Contains(e.mousePosition))
            {
                Selected = view;
                dragging = true;
                e.Use();
                if (sv != null) sv.Repaint();
            }
        }
        Handles.EndGUI();

        // 드래그 → 선택 역만 z=0 평면 위 마우스 위치로 이동
        if (dragging && Selected != null)
        {
            var rt = Selected.transform as RectTransform;
            if (rt == null) dragging = false;
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                var plane = new Plane(Vector3.forward, new Vector3(0, 0, rt.position.z));
                if (plane.Raycast(ray, out float d))
                {
                    Vector3 w = ray.GetPoint(d);
                    Undo.RecordObject(rt, "Move Station");
                    rt.position = new Vector3(w.x, w.y, rt.position.z);
                    EditorUtility.SetDirty(rt);
                }
                e.Use();
                if (sv != null) sv.Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                dragging = false;
                e.Use();
            }
        }
    }

    static bool Culled(Camera cam, Vector3 world)
    {
        if (cam == null) return false;
        Vector3 vp = cam.WorldToViewportPoint(world);
        return vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f;
    }

    static StationView[] FindStations(SubwayMapRenderer r)
    {
        var so = new SerializedObject(r);
        var mc = so.FindProperty("mapContainer");
        var container = mc != null ? mc.objectReferenceValue as RectTransform : null;
        if (container == null) return null;
        return container.GetComponentsInChildren<StationView>(true);
    }

    static Texture2D MakeDotTex()
    {
        int n = 32; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var c = new Vector2(n * 0.5f, n * 0.5f); float rr = n * 0.5f - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(rr - dist + 0.5f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply(); t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }
}
