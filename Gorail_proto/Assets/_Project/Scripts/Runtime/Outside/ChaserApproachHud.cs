using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 우상단 추격자 접근 안내 패널(외부IP §1-1 ① / §4-1 ⑤ / §4-2 ①).
    ///
    /// 상태:
    ///   정보  — "추격자 접근 중 / 약 N분 후 도달 예상"  (N = 가장 가까운 추격자 역 수 × minutesPerStation)
    ///   경고  — "추격자 접근 중! 빨리 승강장으로 복귀하세요"  (작품활동 종료 후 위험 모드)
    ///   도착  — "추격자 현재 역 도착! 최대한 빨리 승강장으로 복귀하세요" + 패널 주변 붉은 반짝임
    ///
    /// 추격자가 내 역(거리 0)에 오면 '도착'이 최우선. 그 외엔 작품활동을 끝냈으면 '경고',
    /// 아직 안 했으면 '정보'. 추격자가 전혀 없으면 패널을 숨긴다.
    /// </summary>
    public class ChaserApproachHud : MonoBehaviour
    {
        [Header("패널/표시")]
        [Tooltip("패널 루트(추격자 없으면 숨김). 비우면 항상 표시.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text messageText;
        [Tooltip("도착 시 붉게 반짝일 글로우(패널 주변 테두리/배경). 비워도 됨.")]
        [SerializeField] private Image glowImage;

        [Header("도달 시간 계산")]
        [Tooltip("역 1칸당 분.")]
        [SerializeField] private int minutesPerStation = 5;

        [Header("문구")]
        [Tooltip("{0} 자리에 분(N)이 들어감.")]
        [TextArea] [SerializeField] private string infoFormat   = "추격자 접근 중\n약 {0}분 후 도달 예상";
        [TextArea] [SerializeField] private string warningText   = "추격자 접근 중!\n빨리 승강장으로 복귀하세요";
        [TextArea] [SerializeField] private string arrivedText   = "추격자 현재 역 도착!\n최대한 빨리 승강장으로 복귀하세요";

        [Header("붉은 반짝임(도착)")]
        [SerializeField] private Color blinkColor = new Color(0.95f, 0.20f, 0.20f, 1f);
        [Tooltip("초당 깜빡임 횟수에 비례.")]
        [SerializeField] private float blinkSpeed = 3f;

        private bool _danger;   // 작품활동을 끝낸 뒤(위험 모드)

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;

        void Start()
        {
            _danger = false;
            if (glowImage != null) SetGlowAlpha(0f);

            var aw = Artwork;
            if (aw != null) aw.ArtworkFinished += OnArtworkFinished;
        }

        void OnDestroy()
        {
            var aw = Artwork;
            if (aw != null) aw.ArtworkFinished -= OnArtworkFinished;
        }

        // 성공이든 실패든 활동을 끝냈으면 위험 모드(빨리 복귀)
        void OnArtworkFinished(bool succeeded, float fameGain, bool interrupted) => _danger = true;

        void Update()
        {
            int closest = ClosestTrackerHops();   // 추격자 없으면 int.MaxValue

            if (closest == int.MaxValue)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                return;
            }
            if (panelRoot != null) panelRoot.SetActive(true);

            if (closest <= 0)
            {
                // 도착 — 붉은 반짝임
                if (messageText != null) messageText.text = arrivedText;
                if (glowImage != null)
                {
                    float a = Mathf.PingPong(Time.time * blinkSpeed, 1f);
                    SetGlowAlpha(a);
                }
            }
            else
            {
                if (glowImage != null) SetGlowAlpha(0f);
                if (messageText != null)
                    messageText.text = _danger
                        ? warningText
                        : string.Format(infoFormat, closest * minutesPerStation);
            }
        }

        void SetGlowAlpha(float a)
        {
            var c = blinkColor; c.a = a;
            glowImage.color = c;
        }

        /// <summary>가장 가까운 추격자까지의 역 수(없으면 int.MaxValue).</summary>
        int ClosestTrackerHops()
        {
            var core  = GameCore.Instance;
            var graph = core?.Graph?.Graph;
            var list  = core?.Trackers?.Trackers;
            string me = core?.Space?.CurrentStationId;
            if (graph == null || list == null || string.IsNullOrEmpty(me)) return int.MaxValue;

            int closest = int.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                int d = graph.Distance(list[i].StationId, me);
                if (d >= 0 && d < closest) closest = d;
            }
            return closest;
        }
    }
}