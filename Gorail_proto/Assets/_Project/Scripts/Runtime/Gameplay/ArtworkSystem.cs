using System.Collections;
using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    public enum ArtworkGrade { High, Mid, Low }

    /// <summary>
    /// 작품활동 시스템(scene_system_spec §5-1).
    /// StartArtwork로 세션을 시작하면 5분 단위로 게임 시간·추격자가 전진하고,
    /// 추격자가 현재 역에 도달하면 작품이 강제 실패된다.
    /// </summary>
    public class ArtworkSystem : MonoBehaviour
    {
        [Header("작품활동 소요 시간(분) 범위 (§5-1)")]
        [SerializeField] private int artworkHighMinMin = 25; [SerializeField] private int artworkHighMinMax = 35;
        [SerializeField] private int artworkMidMinMin  = 15; [SerializeField] private int artworkMidMinMax  = 25;
        [SerializeField] private int artworkLowMinMin  = 10; [SerializeField] private int artworkLowMinMax  = 20;

        [Header("작품활동 명성 증가 — 성공 시 (§5-1)")]
        [SerializeField] private float fameHighBase = 30f; [SerializeField] private float fameHighVar = 6f;
        [SerializeField] private float fameMidBase  = 20f; [SerializeField] private float fameMidVar  = 4f;
        [SerializeField] private float fameLowBase  = 10f; [SerializeField] private float fameLowVar  = 2f;

        [Tooltip("틱 1회(5분)당 실제 시간(초). 낮출수록 진행이 빠르게 표시됨.")]
        [SerializeField] private float tickRealSeconds = 0.5f;

        RngService     Rng      => GameCore.Instance?.Rng;
        FameSystem     Fame     => GameCore.Instance?.Fame;
        GameTimeSystem GameTime => GameCore.Instance?.GameTime;

        /// <summary>세션 진행 중인지.</summary>
        public bool IsActive => _coroutine != null;

        /// <summary>틱마다 발생 (경과 분, 총 분).</summary>
        public event System.Action<int, int> ProgressTicked;

        /// <summary>세션 시작 시 발생. 연출(진행 게이지)이 표시를 켜는 신호.</summary>
        public event System.Action ArtworkStarted;

        /// <summary>이번 세션의 "확정 소요 시간"(등급 최소치, 분). 진행 게이지 0~90% 구간의 기준점(§4-1).
        /// 총 소요시간은 [확정, 최대] 사이에서 굴려지며, 확정~총량 구간이 90~100% 꼬리.</summary>
        public int ConfirmedMinutes => _confirmedMin;
        private int _confirmedMin;

        /// <summary>세션 종료 시 발생 (성공 여부, 명성 증가량, 추격자 도달로 인한 강제 실패 여부).</summary>
        public event System.Action<bool, float, bool> ArtworkFinished;

        private Coroutine _coroutine;

        /// <summary>작품 세션을 시작한다. 이미 진행 중이면 중단 후 재시작.</summary>
        public void StartArtwork(ArtworkGrade grade)
        {
            if (_coroutine != null) StopCoroutine(_coroutine);
            _coroutine = StartCoroutine(RunArtwork(grade));
        }

        /// <summary>진행 중인 세션을 외부에서 취소한다.</summary>
        public void CancelArtwork()
        {
            if (_coroutine == null) return;
            StopCoroutine(_coroutine);
            _coroutine = null;
        }

        IEnumerator RunArtwork(ArtworkGrade grade)
        {
            int   total   = RollDuration(grade);
            float fame    = RollFame(grade);
            int   elapsed = 0;
            var   core    = GameCore.Instance;
            string station = core?.Space?.CurrentStationId;

            _confirmedMin = GradeMin(grade);   // 0~90% 구간 기준점(§4-1)
            try { ArtworkStarted?.Invoke(); }
            catch (System.Exception e) { Debug.LogError($"[Artwork] ArtworkStarted 구독자 예외(무시하고 진행): {e}"); }

            while (elapsed < total)
            {
                int step = Mathf.Min(5, total - elapsed);

                // 게임 시간·추격자 전진
                GameTime?.Advance(step);
                core?.Trackers?.AdvanceByMinutes(step);
                elapsed += step;

                ProgressTickedSafe(elapsed, total);

                // 추격자 도달 체크
                if (!string.IsNullOrEmpty(station) &&
                    (core?.Trackers?.HasTrackerAt(station) ?? false))
                {
                    Debug.Log($"[Artwork] 추격자 도달({station}) → 작품 강제 실패 ({elapsed}/{total}분)");
                    _coroutine = null;
                    GameCore.Instance?.SaveGame();
                    ArtworkFinished?.Invoke(false, 0f, true);
                    yield break;
                }

                if (elapsed < total)
                    yield return new WaitForSeconds(tickRealSeconds);
            }

            // 완료 — 명성 반영(시간은 이미 틱에서 전진됨, duration=0 전달)
            if (fame > 0f) Fame?.OnArtworkCompleted(fame);

            _coroutine = null;
            GameCore.Instance?.SaveGame();
            ArtworkFinished?.Invoke(true, fame, false);

            Debug.Log($"[Artwork] {grade} 성공 +{fame:0.0} 명성 {elapsed}분 소요");
        }

        int RollDuration(ArtworkGrade grade) => grade switch
        {
            ArtworkGrade.High => RandInt(artworkHighMinMin, artworkHighMinMax),
            ArtworkGrade.Mid  => RandInt(artworkMidMinMin,  artworkMidMinMax),
            _                 => RandInt(artworkLowMinMin,  artworkLowMinMax),
        };

        /// <summary>등급별 확정 소요 시간(최소치) — 게이지 0~90% 구간 기준(§4-1).</summary>
        int GradeMin(ArtworkGrade grade) => grade switch
        {
            ArtworkGrade.High => artworkHighMinMin,
            ArtworkGrade.Mid  => artworkMidMinMin,
            _                 => artworkLowMinMin,
        };

        /// <summary>진행 이벤트 발행 — 구독자(연출) 예외가 세션 코루틴을 죽이지 않도록 격리.</summary>
        void ProgressTickedSafe(int elapsed, int total)
        {
            try { ProgressTicked?.Invoke(elapsed, total); }
            catch (System.Exception e) { Debug.LogError($"[Artwork] ProgressTicked 구독자 예외(무시): {e}"); }
        }

        float RollFame(ArtworkGrade grade)
        {
            float b = grade switch { ArtworkGrade.High => fameHighBase, ArtworkGrade.Mid => fameMidBase, _ => fameLowBase };
            float v = grade switch { ArtworkGrade.High => fameHighVar,  ArtworkGrade.Mid => fameMidVar,  _ => fameLowVar  };
            return b + RandFloat(-v, v);
        }

        float RandFloat(float a, float b) => Rng != null ? a + (b - a) * Rng.Value01() : Random.Range(a, b);
        int   RandInt(int a, int b)       => Rng != null ? a + Mathf.FloorToInt((b - a + 1) * Rng.Value01()) : Random.Range(a, b + 1);
    }
}