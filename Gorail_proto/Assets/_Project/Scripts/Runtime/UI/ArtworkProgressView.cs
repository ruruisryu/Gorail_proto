using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// 작품활동 중 연출(외부IP §4) — 씬에서 만든 패널을 구동.
    /// 도넛 게이지(직접 배치한 Image를 radial-fill로 채움) + 역 이름 + 경과 시간 + 추격자 위치 점.
    ///
    /// 게이지: 직접 배치한 도넛 Image의 크기·위치 그대로 두고 fillAmount만 채운다.
    ///   0 → fastEndPercent : fastSeconds(균일·빠르게) / 그 이후 → 100% : slowSeconds(느리게)
    /// 중앙 % 숫자는 게이지 값에 연동. 성패는 ArtworkSystem이 정한다(§5 로직/연출 분리).
    ///
    /// 추격자 점(§4-2): 현재 노선에서 내 좌우 trackerRange역 범위 안의 추격자를
    /// trackerTrack(배경 슬라이더의 좌끝~우끝 영역) 위에 0~1 비율로 찍는다.
    /// </summary>
    public class ArtworkProgressView : MonoBehaviour
    {
        [Header("패널 (시작 시 비활성, 활동 시작 시 켜짐)")]
        [SerializeField] private GameObject panelRoot;

        [Header("완료/실패 이펙트 (나중에 교체 가능 — 켜고/끄기만 함)")]
        [Tooltip("성공(게이지 100%) 시 켜는 연출 오브젝트.")]
        [SerializeField] private GameObject successEffect;
        [Tooltip("실패(추격자 도달) 시 켜는 연출 오브젝트.")]
        [SerializeField] private GameObject failEffect;
        [Tooltip("이펙트를 보여줄 때까지의 지연(초). 0이면 즉시.")]
        [SerializeField] private float effectDelay = 0f;
        [Tooltip("이펙트를 보여준 뒤 패널을 끄기까지 유지 시간(초).")]
        [SerializeField] private float holdSeconds = 1f;

        [Header("표시")]
        [SerializeField] private TMP_Text titleText;     // 역 이름
        [SerializeField] private TMP_Text elapsedText;   // 경과 시간(분)

        [Header("도넛 게이지 (직접 배치한 Image를 채움)")]
        [Tooltip("내가 배치한 도넛 Image. 이 크기·위치 그대로 두고 fillAmount만 채운다.")]
        [SerializeField] private Image gaugeFillImage;
        [Tooltip("뒤에 깔리는 빈 링(회색 트랙). 선택 — 없으면 안 채워진 부분은 투명.")]
        [SerializeField] private Image gaugeTrackImage;
        [Tooltip("게이지 중앙 % 텍스트.")]
        [SerializeField] private TMP_Text percentText;
        [Tooltip("켜면 '76%'처럼 % 기호 포함. %를 별도 오브젝트로 뒀으면 꺼서 숫자('76')만 출력.")]
        [SerializeField] private bool percentIncludesSign = false;
        [Tooltip("켜면 아래 색을 fill/track 이미지에 입힌다. 끄면 이미지가 가진 색 그대로 사용.")]
        [SerializeField] private bool overrideColors = false;
        [SerializeField] private Color gaugeFill  = new Color(0.35f, 0.85f, 0.50f, 1f);
        [SerializeField] private Color gaugeTrack = new Color(0.80f, 0.80f, 0.80f, 1f);

        [Header("게이지 연출 속도(§4-1)")]
        [SerializeField] private float fastSeconds = 3f;   // 0 → fastEndPercent
        [SerializeField] private float slowSeconds = 4f;   // fastEndPercent → 100%
        [Range(0.5f, 0.95f)]
        [SerializeField] private float fastEndPercent = 0.80f;

        [Header("추격자 위치 점(§4-2)")]
        [Tooltip("배경 슬라이더의 좌끝~우끝에 맞춘 빈 RectTransform. 이 안에 추격자 점을 찍는다.")]
        [SerializeField] private RectTransform trackerTrack;
        [Tooltip("내 좌우 몇 역까지 표시할지.")]
        [SerializeField] private int trackerRange = 3;
        [SerializeField] private Sprite trackerDotSprite;   // 비우면 흰 사각
        [SerializeField] private Color trackerDotColor = new Color(0.95f, 0.30f, 0.30f, 1f);
        [SerializeField] private float trackerDotSize = 18f;
        [Tooltip("점이 목표 위치로 미끄러지는 속도(클수록 빠르게 따라붙음).")]
        [SerializeField] private float trackerSmooth = 4f;
        [Tooltip("위아래로 둥둥 뜨는 진폭(px).")]
        [SerializeField] private float trackerBobAmount = 6f;
        [SerializeField] private float trackerBobSpeed = 2f;

        // 추격자별 점을 유지(풀)하며 위치를 보간 → 끊김 없이 미끄러짐
        private readonly List<GameObject> _dots = new();
        private readonly List<float> _dotX = new();   // 현재 정규화 x
        private readonly List<bool>  _dotShown = new();

        [Header("경과 시간(연출과 함께 부드럽게)")]
        [Tooltip("게임이 알려준 총 소요(분)이 아직 없을 때 쓸 기본값.")]
        [SerializeField] private int fallbackTotalMinutes = 20;

        private bool  _running;
        private float _animT;
        private bool  _logicDone, _interrupted, _success;
        private int   _elapsedMin, _totalMin;

        // 마무리(이펙트 → 유지 → 닫기) 단계
        private bool  _finishing, _finishSuccess, _effectShown;
        private float _finishT;

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;
        float TotalAnimSeconds => fastSeconds + slowSeconds;

        void Start()
        {
            // 배치한 도넛 Image를 radial-fill로 설정(크기·위치는 그대로)
            if (gaugeFillImage != null)
            {
                gaugeFillImage.type          = Image.Type.Filled;
                gaugeFillImage.fillMethod    = Image.FillMethod.Radial360;
                gaugeFillImage.fillOrigin    = (int)Image.Origin360.Top;
                gaugeFillImage.fillClockwise = true;
                gaugeFillImage.fillAmount    = 0f;
                if (overrideColors) gaugeFillImage.color = gaugeFill;
            }
            else Debug.LogWarning("[ArtworkProgress] Gauge Fill Image 미연결 — 도넛이 채워지지 않습니다.");

            if (gaugeTrackImage != null && overrideColors) gaugeTrackImage.color = gaugeTrack;

            if (panelRoot != null) panelRoot.SetActive(false);

            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  += OnStarted;
                aw.ProgressTicked  += OnTicked;
                aw.ArtworkFinished += OnFinished;
            }
            else Debug.LogWarning("[ArtworkProgress] GameCore.Artwork 없음 — 진행 게이지 표시 안 됨.");
        }

        void OnDestroy()
        {
            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  -= OnStarted;
                aw.ProgressTicked  -= OnTicked;
                aw.ArtworkFinished -= OnFinished;
            }
        }

        void OnStarted()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            _running = true; _animT = 0f;
            _logicDone = _interrupted = _success = false;
            _finishing = _effectShown = false; _finishT = 0f;
            if (successEffect != null) successEffect.SetActive(false);
            if (failEffect != null) failEffect.SetActive(false);
            _elapsedMin = 0;

            if (titleText != null) titleText.text = ResolveTitle();
            if (elapsedText != null) elapsedText.text = "0분";
            SetGauge(0f);
        }

        void OnTicked(int elapsed, int total) { _elapsedMin = elapsed; _totalMin = total; }

        void OnFinished(bool succeeded, float fameGain, bool interrupted)
        {
            _logicDone = true;
            _interrupted = interrupted;
            _success = succeeded && !interrupted;
        }

        void Update()
        {
            if (_running)
            {
                _animT += Time.deltaTime;
                float fill = CurveFill(_animT);
                SetGauge(fill);

                // 경과 시간: 게이지 채움에 비례해 부드럽게 (게임 틱처럼 5분씩 튀지 않게)
                int total = _totalMin > 0 ? _totalMin : fallbackTotalMinutes;
                if (elapsedText != null) elapsedText.text = $"{Mathf.FloorToInt(fill * total)}분";

                UpdateTrackerDots();

                if (_interrupted) { BeginFinish(false); return; }
                if (_animT >= TotalAnimSeconds && _logicDone && _success) BeginFinish(true);
                return;
            }

            if (_finishing)
            {
                _finishT += Time.deltaTime;
                if (!_effectShown && _finishT >= effectDelay)
                {
                    var fx = _finishSuccess ? successEffect : failEffect;
                    if (fx != null) fx.SetActive(true);
                    _effectShown = true;
                }
                if (_finishT >= effectDelay + holdSeconds) DoClose();
            }
        }

        // 게이지 완료/추격자 도달 → 이펙트 보여줄 마무리 단계로
        void BeginFinish(bool success)
        {
            _running = false;
            _finishing = true;
            _finishSuccess = success;
            _effectShown = false;
            _finishT = 0f;
            if (success) SetGauge(1f);   // 성공이면 게이지 100% 고정
            ClearDots();
        }

        void DoClose()
        {
            _finishing = false;
            if (successEffect != null) successEffect.SetActive(false);
            if (failEffect != null) failEffect.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void SetGauge(float fill01)
        {
            fill01 = Mathf.Clamp01(fill01);
            if (gaugeFillImage != null) gaugeFillImage.fillAmount = fill01;
            if (percentText != null)
            {
                int pct = Mathf.RoundToInt(fill01 * 100f);
                percentText.text = percentIncludesSign ? $"{pct}%" : pct.ToString();
            }
        }

        float CurveFill(float t)
        {
            if (fastSeconds <= 0f) return 1f;
            if (t < fastSeconds) return fastEndPercent * (t / fastSeconds);
            if (slowSeconds <= 0f) return 1f;
            if (t < fastSeconds + slowSeconds)
                return fastEndPercent + (1f - fastEndPercent) * ((t - fastSeconds) / slowSeconds);
            return 1f;
        }

        string ResolveTitle()
        {
            var core = GameCore.Instance;
            string stn = core?.Space?.CurrentStationId;
            var st = core?.Graph?.Graph?.GetStation(stn);
            return st != null ? st.displayName : (stn ?? "");
        }

        // ── 추격자 점(§4-2): 점을 유지하며 목표 위치로 미끄러지고 위아래로 둥둥 ──
        void UpdateTrackerDots()
        {
            if (trackerTrack == null) return;

            var core = GameCore.Instance;
            var graph = core?.Graph?.Graph;
            var trackers = core?.Trackers?.Trackers;
            string me   = core?.Space?.CurrentStationId;
            string line = core?.Player?.CurrentLineId;
            if (graph == null || trackers == null || string.IsNullOrEmpty(me) || string.IsNullOrEmpty(line))
            {
                HideAllDots();
                return;
            }

            // 현재 노선에서 내 앞/뒤 trackerRange역 수집(좌=뒤, 우=앞)
            var fwd = new List<string>();
            var bwd = new List<string>();
            string cur = me;
            for (int i = 0; i < trackerRange; i++)
            {
                var (_, f) = graph.GetLineNeighbors(line, cur);
                if (string.IsNullOrEmpty(f)) break; cur = f; fwd.Add(f);
            }
            cur = me;
            for (int i = 0; i < trackerRange; i++)
            {
                var (b, _) = graph.GetLineNeighbors(line, cur);
                if (string.IsNullOrEmpty(b)) break; cur = b; bwd.Add(b);
            }

            int n = trackers.Count;
            EnsureDotPool(n);

            // 프레임레이트 독립 보간 계수
            float k = 1f - Mathf.Exp(-trackerSmooth * Time.deltaTime);

            for (int i = 0; i < n; i++)
            {
                string sid = trackers[i].StationId;
                int signed; bool inRange = true;
                if (sid == me) signed = 0;
                else
                {
                    int fi = fwd.IndexOf(sid);
                    int bi = bwd.IndexOf(sid);
                    if (fi >= 0) signed =  (fi + 1);
                    else if (bi >= 0) signed = -(bi + 1);
                    else { signed = 0; inRange = false; }   // 좌우 범위 밖
                }

                var go = _dots[i];
                if (!inRange) { if (go.activeSelf) go.SetActive(false); _dotShown[i] = false; continue; }

                float targetNx = 0.5f + (float)signed / (2f * trackerRange);   // 0~1

                if (!_dotShown[i])           // 막 나타남 → 순간이동(미끄러짐 시작점 고정)
                {
                    _dotX[i] = targetNx;
                    go.SetActive(true);
                    _dotShown[i] = true;
                }
                else
                {
                    _dotX[i] = Mathf.Lerp(_dotX[i], targetNx, k);   // 미끄러지듯
                }

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(Mathf.Clamp01(_dotX[i]), 0.5f);
                float bob = Mathf.Sin(Time.time * trackerBobSpeed + i * 1.7f) * trackerBobAmount;
                rt.anchoredPosition = new Vector2(0f, bob);
            }

            // 추격자가 줄었으면 남는 점 숨김
            for (int i = n; i < _dots.Count; i++) { if (_dots[i].activeSelf) _dots[i].SetActive(false); _dotShown[i] = false; }
        }

        void EnsureDotPool(int n)
        {
            while (_dots.Count < n)
            {
                var go = new GameObject("TrackerDot", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(trackerTrack, false);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(trackerDotSize, trackerDotSize);
                var img = go.GetComponent<Image>();
                img.sprite = trackerDotSprite;
                img.color = trackerDotColor;
                img.raycastTarget = false;
                go.SetActive(false);
                _dots.Add(go);
                _dotX.Add(0.5f);
                _dotShown.Add(false);
            }
        }

        void HideAllDots()
        {
            for (int i = 0; i < _dots.Count; i++)
            {
                if (_dots[i] != null && _dots[i].activeSelf) _dots[i].SetActive(false);
                if (i < _dotShown.Count) _dotShown[i] = false;
            }
        }

        void ClearDots() => HideAllDots();
    }
}