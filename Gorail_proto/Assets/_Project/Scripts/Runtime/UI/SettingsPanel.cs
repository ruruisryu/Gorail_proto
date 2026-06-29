using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 설정 패널 UI 컨트롤러.
    /// 인스펙터에서 슬라이더/토글을 연결한 뒤 각 컴포넌트의 OnValueChanged에
    /// 이 컴포넌트의 해당 메서드를 연결한다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        const string KeyAutoDisembark = "AutoDisembark";

        [Header("볼륨 슬라이더")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("자동하차 토글")]
        [SerializeField] private Toggle autoDisembarkToggle;

        void OnEnable()
        {
            var sm = SoundManager.Instance;
            if (sm != null)
            {
                SetSliderSilent(masterSlider, sm.MasterVolume);
                SetSliderSilent(bgmSlider,    sm.BGMVolume);
                SetSliderSilent(sfxSlider,    sm.SFXVolume);
                SetSliderSilent(voiceSlider,  sm.VoiceVolume);
            }

            if (autoDisembarkToggle != null)
            {
                bool val = PlayerPrefs.GetInt(KeyAutoDisembark, 1) == 1;
                autoDisembarkToggle.isOn = val;
                ApplyAutoDisembark(val);
            }
        }

        // ── 볼륨 콜백 (슬라이더 OnValueChanged에 연결) ──────────────────

        public void OnMasterChanged(float value) => SoundManager.Instance.MasterVolume = value;
        public void OnBGMChanged(float value)    => SoundManager.Instance.BGMVolume    = value;
        public void OnSFXChanged(float value)    => SoundManager.Instance.SFXVolume    = value;
        public void OnVoiceChanged(float value)  => SoundManager.Instance.VoiceVolume  = value;

        // ── 자동하차 토글 콜백 ───────────────────────────────────────────

        public void OnAutoDisembarkChanged(bool value)
        {
            PlayerPrefs.SetInt(KeyAutoDisembark, value ? 1 : 0);
            ApplyAutoDisembark(value);
        }

        void ApplyAutoDisembark(bool value)
        {
            var core = GameCore.Instance;
            if (core != null) core.AutoDisembark = value;
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        // 이벤트 발화 없이 슬라이더 값만 세팅
        void SetSliderSilent(Slider slider, float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
        }
    }
}
