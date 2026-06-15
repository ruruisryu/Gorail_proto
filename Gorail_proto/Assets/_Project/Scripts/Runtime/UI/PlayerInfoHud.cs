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
        [Header("명성")]
        [Tooltip("단계별 링 Image (인덱스 0=1단계 색, 1=2단계 색, …). 각각 Filled/Radial360/Top으로 설정.")]
        [SerializeField] private Image[]         fameRings;  // 단계 수만큼
        [SerializeField] private TextMeshProUGUI fameTxt;    // 원형 중앙 숫자

        [Header("수배도 (인덱스 0=하단 → 4=상단)")]
        [SerializeField] private Image[] wantedIcons;        // 별 아이콘 5개
        [SerializeField] private Sprite wantedIconUnfilled;
        [SerializeField] private Sprite wantedIconFilled;

        [Header("시간 / 돈")]
        [SerializeField] private TextMeshProUGUI timeTxt;
        [SerializeField] private TextMeshProUGUI moneyTxt;

        private System.Action<float>       _onFame;
        private System.Action<int>         _onWanted;
        private System.Action<int, int, int> _onTime;
        private System.Action<int>         _onMoney;

        void Start()
        {
            var core = GameCore.Instance;
            if (core != null)
            {
                _onFame   = _ => Refresh();
                _onWanted = _ => Refresh();
                _onTime   = (d, h, m) => RefreshTime();
                _onMoney  = _ => RefreshMoney();

                if (core.Fame   != null) core.Fame.FameChanged       += _onFame;
                if (core.Wanted != null) core.Wanted.WantedChanged  += _onWanted;
                if (core.GameTime != null) core.GameTime.TimeChanged += _onTime;
                if (core.Money   != null) core.Money.MoneyChanged    += _onMoney;
            }
            Refresh();
        }

        void OnDestroy()
        {
            var core = GameCore.Instance;
            if (core == null) return;
            if (core.Fame   != null) core.Fame.FameChanged       -= _onFame;
            if (core.Wanted != null) core.Wanted.WantedChanged  -= _onWanted;
            if (core.GameTime != null) core.GameTime.TimeChanged -= _onTime;
            if (core.Money   != null) core.Money.MoneyChanged    -= _onMoney;
        }

        void Refresh()
        {
            var core = GameCore.Instance;
            if (core == null) return;

            float fame   = core.Fame != null ? core.Fame.CurrentFame : 0f;
            int   wanted = core.Wanted != null ? core.Wanted.WantedLevel : 0;

            // 원형 슬라이더: 단계별 Image를 아래서부터 쌓아 올림
            // 완료 단계 → fillAmount 1, 현재 단계 → 부분 채움, 미래 단계 → 0
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

            // 수배도 별
            if (wantedIcons != null)
                for (int i = 0; i < wantedIcons.Length; i++)
                    wantedIcons[i].sprite = i < wanted
                        ? wantedIconFilled
                        : wantedIconUnfilled;

            RefreshTime();
            RefreshMoney();
        }

        void RefreshTime()
        {
            if (timeTxt == null) return;
            var gt = GameCore.Instance?.GameTime;
            if (gt != null)
                timeTxt.text = $"Day{gt.Day}  {gt.Hour:00}:{gt.Minute:00}";
        }

        void RefreshMoney()
        {
            if (moneyTxt == null) return;
            int money = GameCore.Instance?.Money?.CurrentMoney ?? 0;
            moneyTxt.text = $"{money:N0}원";
        }
    }
}
