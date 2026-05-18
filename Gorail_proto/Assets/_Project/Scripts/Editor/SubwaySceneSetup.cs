using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class SubwaySceneSetup
    {
        [MenuItem("Gorail/Setup/Create SubwayScene")]
        public static void CreateSubwayScene()
        {
            // 1. 씬 생성
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. EventSystem
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            var inputModule = esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            // New Input System 사용 시 InputSystemUIInputModule으로 교체
            var inputSystemType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemType != null)
            {
                Object.DestroyImmediate(inputModule);
                esGO.AddComponent(inputSystemType);
            }

            // 3. Canvas (씬 루트)
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // 4. SubwaySceneController 부착
            var controllerType = System.Type.GetType("Game.UI.SubwaySceneController, Game.Runtime");
            MonoBehaviour controller = null;
            if (controllerType != null)
                controller = canvasGO.AddComponent(controllerType) as MonoBehaviour;

            // 5. Subway Map 버튼
            var btnGO = CreateButton(canvasGO.transform, "SubwayMapButton", "🗺 Subway Map",
                new Vector2(160, 50), new Vector2(32f, -32f),
                new Vector2(0, 1), new Vector2(0, 1),
                new Color(0.12f, 0.18f, 0.28f, 0.92f));

            // 6. SubwayMapPopup 프리팹 생성
            var popupGO = BuildPopup(canvasGO.transform);

            // 7. 버튼 → SubwaySceneController.OnSubwayMapButtonClicked 연결
            var btn = btnGO.GetComponent<Button>();
            if (controller != null && btn != null)
            {
                var method = controllerType.GetMethod("OnSubwayMapButtonClicked");
                var del = System.Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), controller, method)
                    as UnityEngine.Events.UnityAction;
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(btn.onClick, del);
            }

            // 8. SubwaySceneController에 popupGO 할당
            if (controller != null)
            {
                var popupField = controllerType.GetField("subwayMapPopup",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var popupComp = popupGO.GetComponent(
                    System.Type.GetType("Game.UI.SubwayMapPopup, Game.Runtime"));
                if (popupField != null && popupComp != null)
                    popupField.SetValue(controller, popupComp);
            }

            // 9. 프리팹 저장
            const string prefabPath = "Assets/_Project/Prefabs/UI/SubwayMapPopup.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(popupGO, prefabPath);
            Debug.Log($"프리팹 저장: {prefabPath}");

            // 10. 씬 저장
            const string scenePath = "Assets/_Project/Scenes/Levels/SubwayScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // 11. Build Settings에 추가
            AddSceneToBuild(scenePath);

            Debug.Log("SubwayScene 설정 완료!");
            EditorUtility.DisplayDialog("완료", "SubwayScene 생성이 완료되었습니다.", "확인");
        }

        // ─── 헬퍼 ────────────────────────────────────────────────────

        static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 size, Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax,
            Color bgColor)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            // 라벨
            var lblGO = new GameObject("Text");
            lblGO.AddComponent<RectTransform>();
            lblGO.transform.SetParent(go.transform, false);
            var lblRect = lblGO.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
            var txt = lblGO.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return go;
        }

        static GameObject BuildPopup(Transform canvasRoot)
        {
            // 루트 (전체 화면 오버레이)
            var root = new GameObject("SubwayMapPopup");
            root.AddComponent<RectTransform>();
            root.transform.SetParent(canvasRoot, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            // 반투명 배경 (클릭으로 닫기)
            var overlayGO = new GameObject("Overlay");
            overlayGO.AddComponent<RectTransform>();
            overlayGO.transform.SetParent(root.transform, false);
            var overlayRect = overlayGO.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.6f);
            var overlayBtn = overlayGO.AddComponent<Button>();
            overlayBtn.targetGraphic = overlayImg;
            overlayBtn.colors = ColorBlock.defaultColorBlock;

            // 패널 (노선도 컨테이너)
            var panelGO = new GameObject("MapPanel");
            panelGO.AddComponent<RectTransform>();
            panelGO.transform.SetParent(root.transform, false);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(960, 560);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.12f, 0.18f, 0.97f);

            // 타이틀
            var titleGO = new GameObject("Title");
            titleGO.AddComponent<RectTransform>();
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(0, 40);
            var titleTxt = titleGO.AddComponent<Text>();
            titleTxt.text = "서울 지하철 노선도";
            titleTxt.fontSize = 22;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;

            // 노선도 표시 영역 (SubwayMap 콘텐츠가 들어갈 자리)
            var mapAreaGO = new GameObject("MapArea");
            mapAreaGO.AddComponent<RectTransform>();
            mapAreaGO.transform.SetParent(panelGO.transform, false);
            var mapAreaRect = mapAreaGO.GetComponent<RectTransform>();
            mapAreaRect.anchorMin = new Vector2(0.02f, 0.1f);
            mapAreaRect.anchorMax = new Vector2(0.98f, 0.85f);
            mapAreaRect.offsetMin = mapAreaRect.offsetMax = Vector2.zero;
            var mapAreaImg = mapAreaGO.AddComponent<Image>();
            mapAreaImg.color = new Color(0.07f, 0.09f, 0.14f, 1f);

            // 닫기 버튼
            var closeBtnGO = CreateButton(panelGO.transform, "CloseButton", "✕",
                new Vector2(36, 36), new Vector2(-10, -10),
                new Vector2(1, 1), new Vector2(1, 1),
                new Color(0.3f, 0.1f, 0.1f, 0.9f));

            // SubwayMapPopup 컴포넌트
            var popupType = System.Type.GetType("Game.UI.SubwayMapPopup, Game.Runtime");
            if (popupType != null)
            {
                var popup = root.AddComponent(popupType) as MonoBehaviour;
                // closeButton, backgroundOverlay 필드 할당
                var closeBtn = closeBtnGO.GetComponent<Button>();
                var overlayBtnRef = overlayBtn;
                var closeField = popupType.GetField("closeButton",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var overlayField = popupType.GetField("backgroundOverlay",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                closeField?.SetValue(popup, closeBtn);
                overlayField?.SetValue(popup, overlayBtnRef);
            }

            return root;
        }

        static void AddSceneToBuild(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == scenePath) return;

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
