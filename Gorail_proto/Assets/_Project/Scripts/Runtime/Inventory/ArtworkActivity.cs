using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.Inventory
{
    /// <summary>
    /// 재료 배치 화면의 완성도 / 명성 / 활동 시작(외부IP §3-1, §3-5).
    /// 실루엣 격자의 채움률을 읽어 완성도(하/중/상)를 정하고,
    /// 활동 시작 시 ArtworkSystem에 등급을 넘겨 작품활동을 시작한다.
    /// 같은 오브젝트의 InventoryView(작품활동 패널)가 열릴 때만 표시된다.
    /// </summary>
    [RequireComponent(typeof(InventoryView))]
    public class ArtworkActivity : MonoBehaviour
    {
        [Header("실루엣 격자(재료 배치) — 채움률을 읽을 대상")]
        [SerializeField] private InventorySystem inventory;   // = ArtworkInventory

        [Header("완성도 임계 (채움률, 외부IP §3-5)")]
        [SerializeField] private float lowAt  = 0.30f;  // 하
        [SerializeField] private float midAt  = 0.60f;  // 중
        [SerializeField] private float highAt = 0.90f;  // 상

        [Header("표시 (한글 폰트를 넣어야 글자가 보임)")]
        [SerializeField] private TMP_FontAsset font;

        [Header("닫을 화면 (비우면 자동으로 찾음)")]
        [SerializeField] private ArtworkScreen screen;

        private InventoryView _view;
        private GameObject _ui;
        private TextMeshProUGUI _completeTxt, _fameTxt, _btnTxt;
        private Button _startBtn;
        private Image[] _dots;
        private float _lastLoggedRatio = -1f;

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;

        void Start()
        {
            _view = GetComponent<InventoryView>();
            if (screen == null) screen = FindFirstObjectByType<ArtworkScreen>();
            BuildUI();
            _ui.SetActive(_view.IsOpen);
            _view.OpenChanged += OnOpenChanged;
        }

        void OnDestroy()
        {
            if (_view != null) _view.OpenChanged -= OnOpenChanged;
        }

        void OnOpenChanged(bool open)
        {
            _ui.SetActive(open);
            if (open) Refresh();
        }

        void Update()
        {
            if (_view != null && _view.IsOpen) Refresh();
        }

        void Refresh()
        {
            if (inventory == null && _view != null) inventory = _view.Inventory; // 같은 패널이 보는 격자를 따라감
            if (inventory?.Grid == null) return;
            float r = inventory.Grid.FillRatio;
            int stage = StageOf(r);   // 0=미달, 1=하, 2=중, 3=상

            if (Mathf.Abs(r - _lastLoggedRatio) > 0.001f)
            {
                Debug.Log($"[ArtworkActivity] 실루엣 채움률 {r:P0} " +
                          $"(채운칸 {inventory.Grid.FilledCellCount}/{inventory.Grid.UsableCellCount}) → 완성도 {StageName(stage)}");
                _lastLoggedRatio = r;
            }

            for (int i = 0; i < _dots.Length; i++)
                _dots[i].color = (i < stage) ? new Color(0.35f, 0.95f, 0.45f, 1f)
                                             : new Color(1f, 1f, 1f, 0.22f);

            _completeTxt.text = $"완성도: {StageName(stage)} · {r:P0}";
            (int fmin, int fmax) = FameRange(stage);
            _fameTxt.text = stage >= 1 ? $"획득 명성 {fmin}~{fmax}" : "재료를 더 채워줘 (30%↑)";

            bool can = stage >= 1;
            _startBtn.interactable = can;
            if (_btnTxt != null) _btnTxt.alpha = can ? 1f : 0.4f;
        }

        void StartActivity()
        {
            if (inventory?.Grid == null) return;
            int stage = StageOf(inventory.Grid.FillRatio);
            if (stage < 1) return;

            var grade = stage == 3 ? ArtworkGrade.High
                      : stage == 2 ? ArtworkGrade.Mid
                                   : ArtworkGrade.Low;
            Artwork?.StartArtwork(grade);
            if (screen != null) screen.Close(); else _view.Close();
            Debug.Log($"[ArtworkActivity] 활동 시작 — 완성도 {StageName(stage)} ({inventory.Grid.FillRatio:P0})");
        }

        int    StageOf(float r) => r >= highAt ? 3 : r >= midAt ? 2 : r >= lowAt ? 1 : 0;
        string StageName(int s) => s switch { 3 => "상", 2 => "중", 1 => "하", _ => "미달" };
        (int, int) FameRange(int s) => s switch { 3 => (24, 36), 2 => (16, 24), 1 => (8, 12), _ => (0, 0) };

        // ── UI 생성 (그리드 아래에 작은 패널) ──
        void BuildUI()
        {
            _ui = NewUI("ArtworkPanel", transform, out var rt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -240f); // 그리드 아래쯤(위치 안 맞으면 이 값 조정)
            rt.sizeDelta = new Vector2(360f, 130f);

            _dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var d = NewUI($"Dot{i}", rt, out var drt);
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
                drt.pivot = new Vector2(0.5f, 1f);
                drt.sizeDelta = new Vector2(24, 24);
                drt.anchoredPosition = new Vector2((i - 1) * 34f, -8f);
                _dots[i] = d.AddComponent<Image>();
                _dots[i].color = new Color(1f, 1f, 1f, 0.22f);
                var dol = d.AddComponent<Outline>();
                dol.effectColor = new Color(0f, 0f, 0f, 0.85f);
                dol.effectDistance = new Vector2(1.5f, -1.5f);
            }

            _completeTxt = NewText("Complete", rt, 22, new Vector2(0, -34));
            _fameTxt     = NewText("Fame", rt, 16, new Vector2(0, -64));

            var btnGO = NewUI("StartBtn", rt, out var brt);
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(160, 40);
            brt.anchoredPosition = new Vector2(0, -82);
            btnGO.AddComponent<Image>().color = new Color(0.20f, 0.50f, 0.90f, 0.92f);
            _startBtn = btnGO.AddComponent<Button>();
            _startBtn.onClick.AddListener(StartActivity);

            _btnTxt = NewText("BtnTxt", brt, 18, Vector2.zero);
            _btnTxt.text = "활동 시작";
        }

        TextMeshProUGUI NewText(string name, Transform parent, float size, Vector2 pos)
        {
            var go = NewUI(name, parent, out var rt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(340, size + 10);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            return t;
        }

        static GameObject NewUI(string name, Transform parent, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            rt = (RectTransform)go.transform;
            return go;
        }
    }
}