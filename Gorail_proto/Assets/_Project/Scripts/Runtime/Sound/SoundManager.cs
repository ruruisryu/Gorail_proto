using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// BGM + SFX + Voice 통합 매니저.
    ///
    /// 클립 직접 전달:
    ///   SoundManager.Instance.PlayBGM(clip);
    ///   SoundManager.Instance.PlaySFX(clip);
    ///   SoundManager.Instance.PlayVoice(clip);
    ///
    /// 이름으로 Resources 로드:
    ///   SoundManager.Instance.PlayBGM("bgm_subway");
    ///   SoundManager.Instance.PlaySFX("sfx_coin");
    ///   SoundManager.Instance.PlayVoice("voice_hello");
    ///
    /// Resources 폴더 구조 (인스펙터에서 경로 변경 가능):
    ///   Resources/BGM/
    ///   Resources/SFX/
    ///   Resources/Voice/
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Resources 경로")]
        [SerializeField] private string bgmPath   = "BGM";
        [SerializeField] private string sfxPath   = "SFX";
        [SerializeField] private string voicePath = "Voice";

        [Header("BGM")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField][Range(0f, 1f)] private float bgmVolume     = 1f;
        [SerializeField] private float bgmFadeDuration              = 0.5f;

        [Header("Voice")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField][Range(0f, 1f)] private float voiceVolume   = 1f;
        [SerializeField] private float voiceFadeDuration            = 0.3f;

        [Header("BGM 덕킹 (보이스 재생 중 BGM 볼륨 감소)")]
        [SerializeField] private bool  autoDuckBGM        = true;
        [SerializeField][Range(0f, 1f)] private float duckVolume    = 0.7f;
        [SerializeField] private float duckFadeIn         = 0.5f;  // 덕킹 시작까지 걸리는 시간
        [SerializeField] private float duckFadeOut        = 0.5f;  // 복원까지 걸리는 시간

        [Header("SFX")]
        [SerializeField][Range(0f, 1f)] private float sfxVolume     = 1f;
        [SerializeField] private int sfxPoolSize                    = 8;

        private readonly List<AudioSource> _sfxPool = new();
        private Tween _bgmFadeTween;
        private Tween _bgmDuckTween;
        private Tween _voiceFadeTween;

        // 클립 캐시 (이름 → AudioClip)
        private readonly Dictionary<string, AudioClip> _cache = new();

        // 보이스 큐
        private readonly Queue<(AudioClip clip, System.Action onComplete)> _voiceQueue = new();
        private Coroutine _voiceQueueCoroutine;

        // ── 볼륨 프로퍼티 ────────────────────────────────────────────────

        public float BGMVolume
        {
            get => bgmVolume;
            set { bgmVolume = Mathf.Clamp01(value); if (bgmSource) bgmSource.volume = bgmVolume; }
        }

        public float VoiceVolume
        {
            get => voiceVolume;
            set { voiceVolume = Mathf.Clamp01(value); if (voiceSource) voiceSource.volume = voiceVolume; }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set { sfxVolume = Mathf.Clamp01(value); foreach (var s in _sfxPool) s.volume = sfxVolume; }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSource   = EnsureSource(bgmSource,   loop: true,  volume: bgmVolume);
            voiceSource = EnsureSource(voiceSource,  loop: false, volume: voiceVolume);

            for (int i = 0; i < sfxPoolSize; i++)
                _sfxPool.Add(CreateSFXSource());
        }

        void Start()
        {
            //SoundManager.Instance.PlayBGM("Han river loop_1"); // 시작화면 브금
        }

        void OnDestroy()
        {
            _bgmFadeTween?.Kill();
            _voiceFadeTween?.Kill();
        }

        // ── Resources 로드 ───────────────────────────────────────────────

        AudioClip Load(string folder, string clipName)
        {
            string key = $"{folder}/{clipName}";
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>(key);
            if (clip == null) Debug.LogWarning($"[SoundManager] 클립 없음: Resources/{key}");
            else _cache[key] = clip;
            return clip;
        }

        // ── BGM ─────────────────────────────────────────────────────────

        public void PlayBGM(string clipName, bool fade = true, bool loop = true) =>
            PlayBGM(Load(bgmPath, clipName), fade, loop);

        public void PlayBGM(AudioClip clip, bool fade = true, bool loop = true)
        {
            if (bgmSource.clip == clip && bgmSource.isPlaying && bgmSource.loop == loop) return;
            _bgmFadeTween?.Kill();
            _bgmDuckTween?.Kill();

            float targetVolume = bgmVolume;

            if (!fade)
            {
                SetBGMClip(clip, loop);
                bgmSource.volume = targetVolume;
                return;
            }

            float half = bgmFadeDuration * 0.5f;
            _bgmFadeTween = DOTween.Sequence()
                .Append(DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, 0f, half).SetEase(Ease.InQuad))
                .AppendCallback(() => SetBGMClip(clip, loop))
                .Append(DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, targetVolume, half).SetEase(Ease.OutQuad))
                .SetUpdate(true);
        }

        public void StopBGM(bool fade = true) => PlayBGM((AudioClip)null, fade);
        public void PauseBGM()  => bgmSource?.Pause();
        public void ResumeBGM() => bgmSource?.UnPause();
        public void SetBGMVolume(float v) => BGMVolume = v;

        void SetBGMClip(AudioClip clip, bool loop = true)
        {
            bgmSource.loop = loop;
            bgmSource.clip = clip;
            if (clip != null) bgmSource.Play();
            else              bgmSource.Stop();
        }

        void DuckBGM()
        {
            if (!autoDuckBGM || bgmSource == null || !bgmSource.isPlaying) return;
            _bgmDuckTween?.Kill();
            _bgmDuckTween = DOTween
                .To(() => bgmSource.volume, v => bgmSource.volume = v, duckVolume, duckFadeIn)
                .SetEase(Ease.InQuad).SetUpdate(true);
        }

        void RestoreBGM()
        {
            if (!autoDuckBGM || bgmSource == null) return;
            _bgmDuckTween?.Kill();
            _bgmDuckTween = DOTween
                .To(() => bgmSource.volume, v => bgmSource.volume = v, bgmVolume, duckFadeOut)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // ── Voice ────────────────────────────────────────────────────────

        /// <summary>즉시 재생. 재생 중이던 보이스는 중단하고 큐도 비운다.</summary>
        public void PlayVoice(string clipName, bool fade = false) =>
            PlayVoice(Load(voicePath, clipName), fade);

        public void PlayVoice(AudioClip clip, bool fade = false)
        {
            if (clip == null) return;
            ClearVoiceQueue();
            PlayVoiceImmediate(clip, fade);
        }

        /// <summary>큐에 추가. 현재 재생 중이면 끝난 뒤 순서대로 재생된다. onComplete는 해당 클립 재생 완료 시 호출.</summary>
        public void EnqueueVoice(string clipName, System.Action onComplete = null) =>
            EnqueueVoice(Load(voicePath, clipName), onComplete);

        public void EnqueueVoice(AudioClip clip, System.Action onComplete = null)
        {
            if (clip == null) return;
            _voiceQueue.Enqueue((clip, onComplete));
            if (_voiceQueueCoroutine == null)
                _voiceQueueCoroutine = StartCoroutine(VoiceQueueRoutine());
        }

        /// <summary>큐를 비우고 현재 재생 중인 보이스도 정지한다.</summary>
        public void ClearVoiceQueue()
        {
            _voiceQueue.Clear();
            if (_voiceQueueCoroutine != null) { StopCoroutine(_voiceQueueCoroutine); _voiceQueueCoroutine = null; }
        }

        public void StopVoice(bool fade = false)
        {
            ClearVoiceQueue();
            _voiceFadeTween?.Kill();
            if (!fade) { voiceSource?.Stop(); return; }

            _voiceFadeTween = DOTween
                .To(() => voiceSource.volume, v => voiceSource.volume = v, 0f, voiceFadeDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() => { voiceSource.Stop(); voiceSource.volume = voiceVolume; });
        }

        public void SetVoiceVolume(float v) => VoiceVolume = v;

        void PlayVoiceImmediate(AudioClip clip, bool fade = false)
        {
            _voiceFadeTween?.Kill();
            if (!fade)
            {
                voiceSource.Stop();
                voiceSource.clip   = clip;
                voiceSource.volume = voiceVolume;
                voiceSource.Play();
                return;
            }

            float half = voiceFadeDuration * 0.5f;
            _voiceFadeTween = DOTween.Sequence()
                .Append(DOTween.To(() => voiceSource.volume, v => voiceSource.volume = v, 0f, half).SetEase(Ease.InQuad))
                .AppendCallback(() => { voiceSource.clip = clip; voiceSource.Play(); })
                .Append(DOTween.To(() => voiceSource.volume, v => voiceSource.volume = v, voiceVolume, half).SetEase(Ease.OutQuad))
                .SetUpdate(true);
        }

        IEnumerator VoiceQueueRoutine()
        {
            DuckBGM();
            while (_voiceQueue.Count > 0)
            {
                var (clip, onComplete) = _voiceQueue.Dequeue();
                PlayVoiceImmediate(clip);
                yield return null; // 재생 시작까지 한 프레임 대기
                yield return new WaitWhile(() => voiceSource.isPlaying);
                onComplete?.Invoke();
            }
            RestoreBGM();
            _voiceQueueCoroutine = null;
        }

        // ── SFX ─────────────────────────────────────────────────────────

        public void PlaySFX(string clipName, float volumeScale = 1f) =>
            PlaySFX(Load(sfxPath, clipName), volumeScale);

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            var src = GetFreeSFXSource();
            src.volume = sfxVolume * volumeScale;
            src.PlayOneShot(clip);
        }

        /// <summary>
        /// SFX를 루프로 재생한다. 반환된 AudioSource로 Stop()을 직접 호출해 정지.
        /// StopLoopSFX(source)를 써도 된다.
        /// </summary>
        public AudioSource PlaySFXLoop(string clipName, float volumeScale = 1f) =>
            PlaySFXLoop(Load(sfxPath, clipName), volumeScale);

        public AudioSource PlaySFXLoop(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return null;
            var src = GetFreeSFXSource();
            src.clip   = clip;
            src.loop   = true;
            src.volume = sfxVolume * volumeScale;
            src.Play();
            return src;
        }

        /// <summary>PlaySFXLoop로 받은 소스를 정지하고 풀로 반환한다.</summary>
        public void StopLoopSFX(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            src.loop = false;
            src.clip = null;
        }

        public void PlaySFXAtPoint(AudioClip clip, Vector3 worldPos, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, worldPos, sfxVolume * volumeScale);
        }

        public void SetSFXVolume(float v) => SFXVolume = v;

        // ── 내부 유틸 ───────────────────────────────────────────────────

        AudioSource EnsureSource(AudioSource existing, bool loop, float volume)
        {
            if (existing != null) { existing.volume = volume; existing.loop = loop; return existing; }
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = loop; src.volume = volume;
            return src;
        }

        AudioSource CreateSFXSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = false; src.volume = sfxVolume;
            return src;
        }

        AudioSource GetFreeSFXSource()
        {
            foreach (var src in _sfxPool)
                if (!src.isPlaying) return src;
            var newSrc = CreateSFXSource();
            _sfxPool.Add(newSrc);
            return newSrc;
        }
    }
}
