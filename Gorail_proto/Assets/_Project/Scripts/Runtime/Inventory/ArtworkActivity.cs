using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.Inventory
{
    /// <summary>
    /// 재료 배치 완성도 / 명성 / 활동 시작(외부IP §3-1·§3-5).
    /// 코드로 UI를 생성하지 않고, 씬에 직접 만든 인디케이터·텍스트·버튼을 '구동'한다.
    /// 실루엣 격자의 채움률(FillRatio)을 읽어 완성도(하/중/상)를 정하고,
    /// 인디케이터 스프라이트를 활성/비활성으로 교체, 활동 시작 시 등급을 ArtworkSystem에 넘긴다.
    /// </summary>
    public class ArtworkActivity : MonoBehaviour
    {
        [Header("채움률을 읽을 실루엣 격자(재료 배치 뷰)")]
        [Tooltip("재료 배치(오른쪽) InventoryView. inventory를 비우면 여기서 자동으로 가져온다.")]
        [SerializeField] private InventoryView silhouetteView;
        [SerializeField] private InventorySystem inventory;   // 직접 지정도 가능(보통 비움)

        [Header("완성도 임계(채움률, §3-5)")]
        [SerializeField] private float lowAt = 0.30f;   // 하
        [SerializeField] private float midAt = 0.60f;   // 중
        [SerializeField] private float highAt = 0.90f;  // 상

        [Header("완성도 인디케이터 3개 (하 → 중 → 상 순서로 연결)")]
        [SerializeField] private Image[] indicators;
        [Tooltip("각 단계 '켜짐' 스프라이트. 3개 넣으면 단계별로, 1개만 넣으면 공통으로 사용.")]
        [SerializeField] private Sprite[] activeSprites;
        [Tooltip("각 단계 '꺼짐' 스프라이트. 3개 또는 1개.")]
        [SerializeField] private Sprite[] inactiveSprites;

        [Header("텍스트 / 버튼 (씬에서 만든 것 연결)")]
        [Tooltip("\"획득 명성 00~00\" 표시.")]
        [SerializeField] private TMP_Text fameText;
        [Tooltip("\"00/00\" — 채운 칸 / 전체 칸.")]
        [SerializeField] private TMP_Text cellCountText;
        [Tooltip("\"00%\" — 완성도 퍼센트.")]
        [SerializeField] private TMP_Text percentText;
        [Tooltip("활동 시작 버튼.")]
        [SerializeField] private Button startButton;

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;

        InventorySystem Inv =>
            inventory != null ? inventory :
            (silhouetteView != null ? silhouetteView.Inventory : null);

        void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(StartActivity);
        }

        // 패널이 열려 있을 때(=이 오브젝트 활성)만 매 프레임 갱신
        void Update() => Refresh();

        public void Refresh()
        {
            var inv = Inv;
            if (inv?.Grid == null) return;

            float r = inv.Grid.FillRatio;
            int filled = inv.Grid.FilledCellCount;
            int total = inv.Grid.UsableCellCount;
            int stage = StageOf(r);   // 0=미달, 1=하, 2=중, 3=상

            // 인디케이터: 하→중→상 순서로 활성/비활성 스프라이트 교체
            if (indicators != null)
            {
                for (int i = 0; i < indicators.Length; i++)
                {
                    if (indicators[i] == null) continue;
                    bool on = i < stage;
                    var s = on ? Pick(activeSprites, i) : Pick(inactiveSprites, i);
                    if (s != null) indicators[i].sprite = s;
                }
            }

            if (cellCountText != null) cellCountText.text = $"{filled:00}/{total:00}";
            if (percentText != null) percentText.text = $"{r:P0}";
            if (fameText != null)
            {
                (int fmin, int fmax) = FameRange(stage);
                fameText.text = $"획득 명성 {fmin}~{fmax}";
            }
            if (startButton != null) startButton.interactable = stage >= 1;
        }

        void StartActivity()
        {
            var inv = Inv;
            if (inv?.Grid == null) return;
            int stage = StageOf(inv.Grid.FillRatio);
            if (stage < 1) return;

            var grade = stage == 3 ? ArtworkGrade.High
                      : stage == 2 ? ArtworkGrade.Mid
                                   : ArtworkGrade.Low;
            Artwork?.StartArtwork(grade);
            FindFirstObjectByType<ArtworkScreen>()?.Close();
            Debug.Log($"[ArtworkActivity] 활동 시작 — 완성도 {StageName(stage)} ({inv.Grid.FillRatio:P0})");
        }

        static Sprite Pick(Sprite[] a, int i) =>
            (a != null && a.Length > 0) ? a[Mathf.Min(i, a.Length - 1)] : null;

        int StageOf(float r) => r >= highAt ? 3 : r >= midAt ? 2 : r >= lowAt ? 1 : 0;
        string StageName(int s) => s switch { 3 => "상", 2 => "중", 1 => "하", _ => "미달" };
        (int, int) FameRange(int s) => s switch { 3 => (24, 36), 2 => (16, 24), 1 => (8, 12), _ => (0, 0) };
    }
}