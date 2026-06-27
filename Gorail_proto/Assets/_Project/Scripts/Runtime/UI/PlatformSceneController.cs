using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;
using Game.Subway;

namespace Game.UI
{
    /// <summary>
    /// [S5] 승강장 씬 UGUI 컨트롤러.
    ///
    /// 패널 흐름: LineSelectPanel → DirectionSelectPanel
    ///   1. 호선 선택: 현재 노선(재탑승) + 환승 가능 노선 버튼 목록(프리팹)
    ///   2. 방면 선택: 선택한 호선의 양방향 이웃한 역 버튼
    ///
    /// 인스펙터 연결 필요 항목은 [SerializeField] 참조.
    /// 버튼 onClick은 아래 public 메서드를 직접 연결:
    ///   - WayOutButton.onClick  → OnWayOutClicked()
    ///   - BackButton.onClick    → OnBackClicked()
    /// </summary>
    public class PlatformSceneController : MonoBehaviour
    {
        // ── 나가는 곳 ─────────────────────────────────────────────────────
        [Header("나가는 곳 버튼")]
        [SerializeField] private Button   wayOutButton;
        [SerializeField] private TMP_Text wayOutStationName;
        [SerializeField] private TMP_Text wayOutStationNum;

        // ── 호선 선택 패널 ────────────────────────────────────────────────
        [Header("호선 선택 패널")]
        [SerializeField] private GameObject lineSelectPanel;
        [SerializeField] private Transform  lineButtonContainer; // VerticalLayoutGroup
        [SerializeField] private GameObject lineSelectorPrefab;  // LineSelectorButtonUI

        // ── 방면 선택 패널 ────────────────────────────────────────────────
        [Header("방면 선택 패널")]
        [SerializeField] private GameObject directionSelectPanel;
        [SerializeField] private Image      directionHeaderBar;  // 선택 호선 색
        [SerializeField] private Image[]    lineTextCircle; // 선택 호선 색 (원)
        [SerializeField] private TMP_Text[] lineText;   // 선택 호선 표시명
        [SerializeField] private Button     backwardButton;
        [SerializeField] private TMP_Text   backwardLabel;       // "○○ 방면"
        [SerializeField] private Button     forwardButton;
        [SerializeField] private TMP_Text   forwardLabel;        // "○○ 방면"

        // ── 경고 메시지 ───────────────────────────────────────────────────
        [Header("경고 (외부IP 작품 후 재진입 시도)")] [SerializeField]
        private string dangerWarning = "지금 밖으로 나가면 위험할 것 같다...";

        // ── 내부 ─────────────────────────────────────────────────────────
        private string _pendingLineId;
        private bool   _pendingIsSameLine;

        PlatformController Platform   => GameCore.Instance?.Platform;
        SubwayMapRenderer  MapRenderer => GameCore.Instance?.MapRenderer;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        void OnEnable()
        {
            wayOutButton.onClick.AddListener(OnWayOutClicked);
            RefreshWayOut();
            ShowLineSelect();
            BuildLineButtons();
        }

        void OnDisable() => ClearLineButtons();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 나가는 곳
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        void RefreshWayOut()
        {
            if (wayOutButton == null) return;
            wayOutButton.gameObject.SetActive(Platform?.IsSpecialStation == true);

            var graph     = GameCore.Instance?.Graph?.Graph;
            var stationId = Platform?.CurrentStation;
            if (graph != null && !string.IsNullOrEmpty(stationId))
            {
                var station = graph.GetStation(stationId);
                if (wayOutStationName != null) wayOutStationName.text = station?.displayName ?? stationId;
            }
            if (wayOutStationNum != null) wayOutStationNum.text = UnityEngine.Random.Range(1, 7).ToString();
        }

        private void OnWayOutClicked()
        {
            var plat = Platform;
            if (plat == null) return;

            // 이미 작품활동을 한 역이면 경고
            if (!plat.CanGoOutside)
            {
                MoveNotificationHud.Instance.ShowPopUp(dangerWarning, new Vector2(0, -350));
                return;
            }

            plat.GoOutside();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 호선 선택 패널
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        void BuildLineButtons()
        {
            ClearLineButtons();
            var plat   = Platform;
            var player = GameCore.Instance?.Player;
            if (plat == null || player == null || lineSelectorPrefab == null) return;

            bool fromGround = plat.CameFromGround;

            // 현재 노선 — 재탑승 or 탑승하는 방향
            string currentLabel = fromGround
                ? $"{MapRenderer.GetLineDisplayName(player.CurrentLineId)} 탑승하는 방향"
                : $"{MapRenderer.GetLineDisplayName(player.CurrentLineId)} 재탑승";
            SpawnLineButton(player.CurrentLineId, currentLabel, isSameLine: true);

            // 환승 가능 노선
            foreach (var lineId in plat.AvailableTransferLines)
            {
                string transferLabel = fromGround
                    ? $"{MapRenderer.GetLineDisplayName(lineId)} 탑승하는 방향"
                    : $"{MapRenderer.GetLineDisplayName(lineId)} 갈아타는 방향";
                SpawnLineButton(lineId, transferLabel, isSameLine: false);
            }
        }

        void SpawnLineButton(string lineId, string labelText, bool isSameLine)
        {
            var go  = Instantiate(lineSelectorPrefab, lineButtonContainer);
            var btn = go.GetComponent<LineSelectorButtonUI>();
            if (btn == null) return;

            var color       = MapRenderer != null ? MapRenderer.GetLineColor(lineId)       : Color.gray;
            var displayName = MapRenderer != null ? MapRenderer.GetLineDisplayName(lineId) : lineId;

            btn.Setup(color, labelText, () => OnLineButtonClicked(lineId, isSameLine));
        }

        void ClearLineButtons()
        {
            if (lineButtonContainer == null) return;
            for (int i = lineButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(lineButtonContainer.GetChild(i).gameObject);
        }

        void OnLineButtonClicked(string lineId, bool isSameLine)
        {
            _pendingLineId     = lineId;
            _pendingIsSameLine = isSameLine;
            ShowDirectionPanel(lineId, isSameLine);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 방면 선택 패널
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        void ShowDirectionPanel(string lineId, bool isSameLine)
        {
            var plat = Platform;
            if (plat == null) return;

            var color       = MapRenderer != null ? MapRenderer.GetLineColor(lineId) : Color.gray;
            var displayName = MapRenderer != null ? MapRenderer.GetLineDisplayName(lineId) : lineId;
            var numMatch   = System.Text.RegularExpressions.Regex.Match(displayName, @"\d+");
            var lineNumber = numMatch.Success ? numMatch.Value : displayName;

            if (directionHeaderBar != null) directionHeaderBar.color = color;
            foreach (var circle in lineTextCircle) circle.color = color;
            foreach (var text in lineText) text.text = lineNumber;

            var (bwd, fwd) = plat.GetTransferDirectionLabels(lineId);
            if (backwardLabel != null) backwardLabel.text = bwd != null ? $"{bwd} 방면" : "—";
            if (forwardLabel  != null) forwardLabel.text  = fwd != null ? $"{fwd} 방면" : "—";
            backwardButton.interactable = bwd != null;
            forwardButton.interactable  = fwd != null;

            backwardButton.onClick.RemoveAllListeners();
            forwardButton.onClick.RemoveAllListeners();

            if (isSameLine)
            {
                backwardButton.onClick.AddListener(() => plat.BoardCurrentLineWithDirection(-1));
                forwardButton.onClick.AddListener( () => plat.BoardCurrentLineWithDirection(+1));
            }
            else
            {
                backwardButton.onClick.AddListener(() => plat.TransferWithDirection(lineId, -1));
                forwardButton.onClick.AddListener( () => plat.TransferWithDirection(lineId, +1));
            }

            lineSelectPanel.SetActive(false);
            directionSelectPanel.SetActive(true);
        }

        // Inspector의 BackButton.onClick에 연결
        public void OnBackClicked() => ShowLineSelect();

        void ShowLineSelect()
        {
            if (lineSelectPanel      != null) lineSelectPanel.SetActive(true);
            if (directionSelectPanel != null) directionSelectPanel.SetActive(false);
        }
    }
}
