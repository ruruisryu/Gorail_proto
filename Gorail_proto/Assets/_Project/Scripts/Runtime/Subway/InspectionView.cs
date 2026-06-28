using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// §8 검문 연출 뷰. SubwayScene의 persistent HUD(오버레이 캔버스)에 두면 지하철/외부 두 씬 위에 모두 뜬다.
    /// InspectionSystem.InspectionStarted를 받아 도넛 게이지를 채우며 긴장감을 주다가,
    /// 끝에서 예정 결과(통과/실패)를 공개하고 잠시 보여준 뒤 CompleteInspection()으로 실제 판정을 적용한다.
    /// (작품활동 게이지 ArtworkProgressView와 같은 "사용자 배치 Image를 fillAmount로 구동" 방식.)
    /// </summary>
    public class InspectionView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject panelRoot;
        [Tooltip("선택: 역 이름이 들어갈 타이틀. {0}에 역 이름이 들어감.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("타이틀 형식. {0}=역 이름. 예: \"{0} 검문\", 또는 그냥 \"{0}\".")]
        [SerializeField] private string titleFormat = "{0}";

        [Header("도넛 게이지 (사용자 배치 Image)")]
        [Tooltip("Image Type=Filled / Radial360 권장. 코드가 fillAmount만 구동.")]
        [SerializeField] private Image gaugeFillImage;
        [Tooltip("선택: 퍼센트 텍스트. 비우면 표시 안 함.")]
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private bool percentIncludesSign = false;

        [Header("연출 속도 (긴장감)")]
        [SerializeField] private float fastSeconds = 2f;
        [SerializeField] private float slowSeconds = 2f;
        [Range(0.5f, 0.95f)]
        [SerializeField] private float fastEndPercent = 0.8f;

        [Header("상태/결과 문구 (한 텍스트를 바꿔치기)")]
        [Tooltip("'검문 중입니다...' 텍스트. 연출 중엔 진행 문구, 끝엔 통과/검거로 바뀜.")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private string runningText = "검문 중입니다...";
        [SerializeField] private string passText = "검문 통과";
        [SerializeField] private string failText = "검거!";
        [SerializeField] private Color runningColor = Color.white;
        [SerializeField] private Color passColor = new Color(0.45f, 1f, 0.5f);
        [SerializeField] private Color failColor = new Color(1f, 0.35f, 0.3f);

        [Header("결과 공개 (선택: 추가 이펙트 오브젝트)")]
        [Tooltip("선택. 통과 시 켜질 추가 연출 오브젝트(아이콘/이펙트 등). 텍스트만 바꿀 거면 비워도 됨.")]
        [SerializeField] private GameObject passEffect;
        [Tooltip("선택. 실패 시 켜질 추가 연출 오브젝트. 이게 보인 뒤 게임오버가 적용됨.")]
        [SerializeField] private GameObject failEffect;
        [Tooltip("결과를 보여주는 시간(초). 이 시간이 지나면 판정 적용.")]
        [SerializeField] private float revealHold = 1.2f;

        InspectionSystem Inspection => GameCore.Instance?.Inspection;

        bool  _running;
        float _t;
        bool  _revealed;
        float _revealT;

        float TotalAnimSeconds => fastSeconds + slowSeconds;

        void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (passEffect != null) passEffect.SetActive(false);
            if (failEffect != null) failEffect.SetActive(false);
            ConfigureGaugeImage();

            var insp = Inspection;
            if (insp != null) insp.InspectionStarted += OnInspectionStarted;
            else Debug.LogWarning("[InspectionView] GameCore.Inspection 없음 — 검문 연출 표시 안 됨.");
        }

        void OnDestroy()
        {
            var insp = Inspection;
            if (insp != null) insp.InspectionStarted -= OnInspectionStarted;
        }

        void OnInspectionStarted(string station)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (titleText != null) titleText.text = string.Format(titleFormat, ResolveStationName(station));
            if (passEffect != null) passEffect.SetActive(false);
            if (failEffect != null) failEffect.SetActive(false);
            if (statusText != null) { statusText.text = runningText; statusText.color = runningColor; }
            _running = true; _t = 0f; _revealed = false; _revealT = 0f;
            SetGauge(0f);
        }

        void Update()
        {
            if (!_running) return;

            if (!_revealed)
            {
                _t += Time.deltaTime;
                SetGauge(CurveFill(_t));
                if (_t >= TotalAnimSeconds) Reveal();
            }
            else
            {
                _revealT += Time.deltaTime;
                if (_revealT >= revealHold) Finish();
            }
        }

        // 게이지 가득 → 예정 결과 공개
        void Reveal()
        {
            _revealed = true; _revealT = 0f;
            SetGauge(1f);
            bool passed = Inspection != null && Inspection.PendingPassed;

            if (statusText != null)
            {
                statusText.text  = passed ? passText : failText;
                statusText.color = passed ? passColor : failColor;
            }

            var fx = passed ? passEffect : failEffect;
            if (fx != null) fx.SetActive(true);
        }

        // 결과 보여준 뒤 → 실제 판정 적용(추격자 제거/게임오버)
        void Finish()
        {
            _running = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (passEffect != null) passEffect.SetActive(false);
            if (failEffect != null) failEffect.SetActive(false);
            Inspection?.CompleteInspection();   // 여기서 통과=추격자 제거 / 실패=게임오버
        }

        // 0~fastEndPercent 빠르게 → 100% 느리게(§4-1 곡선 재활용)
        float CurveFill(float t)
        {
            if (fastSeconds <= 0f) return 1f;
            if (t < fastSeconds) return fastEndPercent * (t / fastSeconds);
            if (slowSeconds <= 0f) return 1f;
            if (t < fastSeconds + slowSeconds)
                return fastEndPercent + (1f - fastEndPercent) * ((t - fastSeconds) / slowSeconds);
            return 1f;
        }

        string ResolveStationName(string stationId)
        {
            var st = GameCore.Instance?.Graph?.Graph?.GetStation(stationId);
            return st != null ? st.displayName : (stationId ?? "");
        }

        void ConfigureGaugeImage()
        {
            if (gaugeFillImage == null) return;
            gaugeFillImage.type        = Image.Type.Filled;
            gaugeFillImage.fillMethod  = Image.FillMethod.Radial360;
            gaugeFillImage.fillOrigin  = (int)Image.Origin360.Top;
            gaugeFillImage.fillClockwise = true;
        }

        void SetGauge(float fill01)
        {
            fill01 = Mathf.Clamp01(fill01);
            if (gaugeFillImage != null) gaugeFillImage.fillAmount = fill01;
            if (percentText != null)
            {
                int p = Mathf.RoundToInt(fill01 * 100f);
                percentText.text = percentIncludesSign ? $"{p}%" : p.ToString();
            }
        }
    }
}