using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 플레이어 기본 정보 HUD — 좌상단 상시 표시 (UI기획서 §1, §2-1).
    /// 인스펙터에서 UI 요소를 직접 연결한다.
    /// </summary>
    public class PlayerInfoHud : MonoBehaviour
    {
        [SerializeField] private RectTransform playerInfoRoot;
        
        [Header("명성")]
        [Tooltip("단계별 링 Image (인덱스 0=1단계 색, 1=2단계 색, …). 각각 Filled/Radial360/Top으로 설정.")]
        [SerializeField] private Image[]         fameRings;
        [SerializeField] private TextMeshProUGUI fameTxt;

        [Header("수배도 (인덱스 0=하단 → 4=상단)")]
        [SerializeField] private Image[] wantedIcons;
        [SerializeField] private Sprite wantedIconUnfilled;
        [SerializeField] private Sprite wantedIconFilled;

        [Header("시간 / 돈")]
        [SerializeField] private TextMeshProUGUI timeTxt;
        [SerializeField] private TextMeshProUGUI moneyTxt;

        [Header("델타 텍스트 (변화량 표시)")]
        [SerializeField] private TextMeshProUGUI moneyDeltaTxt;
        [SerializeField] private TextMeshProUGUI timeDeltaTxt;
        [SerializeField] private Color deltaPositiveColor = new Color(0.2f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color deltaNegativeColor = new Color(0.9f, 0.3f, 0.2f, 1f);

        [Header("연출")]
        [SerializeField] private float moneyCountDuration = 0.4f;
        [SerializeField] private float timePunchDuration  = 0.25f;
        [SerializeField] private float timePunchStrength  = 6f;
        [SerializeField] private bool  timeCountUp        = false;
        [SerializeField] private float timeCountDuration  = 0.4f;
        [SerializeField] private float deltaRisePx        = 24f;
        [SerializeField] private float deltaFadeDuration  = 0.9f;
        
        [SerializeField] private SubwayMapPopup mapPopup;

        private System.Action<float>         _onFame;
        private System.Action<int>           _onWanted;
        private System.Action<int, int, int> _onTime;
        private System.Action<int>           _onMoney;

        private Vector3 _originPosition;

        private int   _displayedMoney;
        private Tween _moneyTween;
        private int   _displayedTimeMinutes;
        private int   _displayedTimeDay;
        private Tween _timeTween;

        // 델타 애니메이션용
        private Sequence _moneyDeltaSeq;
        private Sequence _timeDeltaSeq;
        private Vector2  _moneyDeltaOrigin;
        private Vector2  _timeDeltaOrigin;

        void Start()
        {
            // 델타 텍스트 초기 상태 숨기기
            if (moneyDeltaTxt != null)
            {
                moneyDeltaTxt.alpha  = 0f;
                _moneyDeltaOrigin    = moneyDeltaTxt.rectTransform.anchoredPosition;
            }
            if (timeDeltaTxt != null)
            {
                timeDeltaTxt.alpha = 0f;
                _timeDeltaOrigin   = timeDeltaTxt.rectTransform.anchoredPosition;
            }

            var core = GameCore.Instance;
            if (core != null)
            {
                _onFame   = _ => Refresh();
                _onWanted = _ => Refresh();
                _onTime   = (d, h, m) => RefreshTime(animate: true);
                _onMoney  = newVal => AnimateMoney(newVal);

                if (core.Fame     != null) core.Fame.FameChanged       += _onFame;
                if (core.Wanted   != null) core.Wanted.WantedChanged   += _onWanted;
                if (core.GameTime != null) core.GameTime.TimeChanged   += _onTime;
                if (core.Money    != null) core.Money.MoneyChanged     += _onMoney;
            }
            
            _originPosition = playerInfoRoot.anchoredPosition;
            
            mapPopup.OnClose += OnMapPopupClose;
            mapPopup.OnOpen += OnMapPopupOpen;
            
            Refresh();
        }

        void OnDestroy()
        {
            _moneyTween?.Kill();
            _timeTween?.Kill();
            _moneyDeltaSeq?.Kill();
            _timeDeltaSeq?.Kill();
            var core = GameCore.Instance;
            if (core == null) return;
            if (core.Fame     != null) core.Fame.FameChanged       -= _onFame;
            if (core.Wanted   != null) core.Wanted.WantedChanged   -= _onWanted;
            if (core.GameTime != null) core.GameTime.TimeChanged   -= _onTime;
            if (core.Money    != null) core.Money.MoneyChanged     -= _onMoney;
                        
            mapPopup.OnClose -= OnMapPopupClose;
            mapPopup.OnOpen -= OnMapPopupOpen;
        }
        
        void OnMapPopupClose()
        {
            playerInfoRoot.anchoredPosition = _originPosition + Vector3.up * 80f;
        }
        
        void OnMapPopupOpen()
        {
            playerInfoRoot.anchoredPosition = _originPosition;
        }

        void Refresh()
        {
            var core = GameCore.Instance;
            if (core == null) return;

            float fame   = core.Fame   != null ? core.Fame.CurrentFame   : 0f;
            int   wanted = core.Wanted != null ? core.Wanted.WantedLevel : 0;

            if (fameRings != null)
            {
                int   currentStage = (int)(fame / 100f);
                float partial      = (fame % 100f) / 100f;
                for (int i = 0; i < fameRings.Length; i++)
                {
                    if (fameRings[i] == null) continue;
                    if (i < currentStage)       fameRings[i].fillAmount = 1f;
                    else if (i == currentStage) fameRings[i].fillAmount = partial;
                    else                        fameRings[i].fillAmount = 0f;
                }
            }

            if (fameTxt != null) fameTxt.text = ((int)fame).ToString();

            if (wantedIcons != null)
                for (int i = 0; i < wantedIcons.Length; i++)
                    wantedIcons[i].sprite = i < wanted ? wantedIconFilled : wantedIconUnfilled;

            // 초기화 시엔 애니메이션 없이 바로 세팅
            _displayedMoney = GameCore.Instance?.Money?.CurrentMoney ?? 0;
            SetMoneyText(_displayedMoney);
            var gt0 = GameCore.Instance?.GameTime;
            _displayedTimeDay     = gt0?.Day    ?? 0;
            _displayedTimeMinutes = gt0 != null ? gt0.Hour * 60 + gt0.Minute : 0;
            RefreshTime(animate: false);
        }

        // ── 시간 ────────────────────────────────────────────────────────

        void RefreshTime(bool animate)
        {
            if (timeTxt == null) return;
            var gt = GameCore.Instance?.GameTime;
            if (gt == null) return;

            int targetMinutes = gt.Hour * 60 + gt.Minute;
            int targetDay     = gt.Day;

            if (animate)
            {
                int deltaMinutes = (targetDay - _displayedTimeDay) * 1440
                                 + (targetMinutes - _displayedTimeMinutes);
                if (deltaMinutes != 0)
                    ShowDelta(timeDeltaTxt, ref _timeDeltaSeq, _timeDeltaOrigin,
                        FormatTimeDelta(deltaMinutes), deltaMinutes > 0);
            }

            if (!animate || !timeCountUp)
            {
                _timeTween?.Kill();
                _displayedTimeMinutes = targetMinutes;
                _displayedTimeDay     = targetDay;
                SetTimeText(targetDay, targetMinutes);

                if (animate && !timeCountUp)
                {
                    var rt  = timeTxt.rectTransform;
                    var ori = rt.anchoredPosition;
                    DOTween.Sequence()
                        .Append(rt.DOAnchorPos(ori + Vector2.up * timePunchStrength, timePunchDuration * 0.4f).SetEase(Ease.OutQuad))
                        .Append(rt.DOAnchorPos(ori, timePunchDuration * 0.6f).SetEase(Ease.InQuad))
                        .SetUpdate(true);
                }
                return;
            }

            // 카운트업 모드
            _timeTween?.Kill();
            int from    = _displayedTimeMinutes;
            int fromDay = _displayedTimeDay;
            _timeTween = DOTween
                .To(() => from, v =>
                {
                    from = v;
                    int day = fromDay + (v >= 1440 ? 1 : 0);
                    SetTimeText(day, v % 1440);
                }, targetMinutes + (targetDay - _displayedTimeDay) * 1440, timeCountDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _displayedTimeMinutes = targetMinutes;
                    _displayedTimeDay     = targetDay;
                });
        }

        void SetTimeText(int day, int totalMinutes)
        {
            int h = (totalMinutes / 60) % 24;
            int m = totalMinutes % 60;
            timeTxt.text = $"Day{day}  {h:00}:{m:00}";
        }

        static string FormatTimeDelta(int deltaMinutes)
        {
            bool neg = deltaMinutes < 0;
            int  abs = Mathf.Abs(deltaMinutes);
            int  h   = abs / 60;
            int  m   = abs % 60;
            string sign = neg ? "-" : "+";
            return h > 0
                ? $"{sign}{h}시간 {m}분"
                : $"{sign}{m}분";
        }

        // ── 돈 ──────────────────────────────────────────────────────────

        void AnimateMoney(int target)
        {
            if (moneyTxt == null) { _displayedMoney = target; return; }

            int delta = target - _displayedMoney;
            if (delta != 0)
                ShowDelta(moneyDeltaTxt, ref _moneyDeltaSeq, _moneyDeltaOrigin,
                    $"{(delta > 0 ? "+" : "")}{delta:N0}원", delta > 0);

            if (target == _displayedMoney) return;

            _moneyTween?.Kill();
            int from = _displayedMoney;
            _moneyTween = DOTween
                .To(() => from, v => { from = v; SetMoneyText(v); }, target, moneyCountDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() => _displayedMoney = target);
        }

        void SetMoneyText(int value) =>
            moneyTxt.text = $"{value:N0}원";

        // ── 델타 팝업 ────────────────────────────────────────────────────

        void ShowDelta(TextMeshProUGUI txt, ref Sequence seq, Vector2 origin,
                       string label, bool positive)
        {
            if (txt == null) return;

            seq?.Kill();
            txt.text  = label;
            txt.color = positive ? deltaPositiveColor : deltaNegativeColor;
            txt.rectTransform.anchoredPosition = origin;
            txt.alpha = 1f;

            seq = DOTween.Sequence()
                .Append(txt.rectTransform
                    .DOAnchorPos(origin + Vector2.up * deltaRisePx, deltaFadeDuration)
                    .SetEase(Ease.OutCubic))
                .Join(txt.DOFade(0f, deltaFadeDuration).SetEase(Ease.InQuad))
                .SetUpdate(true)
                .OnComplete(() => txt.rectTransform.anchoredPosition = origin);
        }
    }
}
