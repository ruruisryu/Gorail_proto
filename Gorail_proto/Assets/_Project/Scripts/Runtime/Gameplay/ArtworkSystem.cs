using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    public enum ArtworkGrade { High, Mid, Low }

    /// <summary>
    /// 작품활동 시스템(scene_system_spec §5-1).
    /// 완성도(High/Mid/Low) × 성공/실패 입력을 받아 소요 시간을 게임 시간에 반영하고,
    /// 성공 시 명성 증가분을 FameSystem에 전달한다.
    /// </summary>
    public class ArtworkSystem : MonoBehaviour
    {
        private RngService Rng => GameCore.Instance?.Rng;

        [Header("작품활동 소요 시간(분) 범위 (§5-1)")]
        [SerializeField] private int artworkHighMinMin = 25; [SerializeField] private int artworkHighMinMax = 35;
        [SerializeField] private int artworkMidMinMin  = 15; [SerializeField] private int artworkMidMinMax  = 25;
        [SerializeField] private int artworkLowMinMin  = 10; [SerializeField] private int artworkLowMinMax  = 20;

        [Header("작품활동 명성 증가 — 성공 시 (§5-1) / 기획서: 하8~12, 중16~24, 상24~36")]
        [SerializeField] private float fameHighBase = 30f; [SerializeField] private float fameHighVar = 6f;
        [SerializeField] private float fameMidBase  = 20f; [SerializeField] private float fameMidVar  = 4f;
        [SerializeField] private float fameLowBase  = 10f; [SerializeField] private float fameLowVar  = 2f;

        FameSystem     Fame     => GameCore.Instance?.Fame;
        GameTimeSystem GameTime => GameCore.Instance?.GameTime;

        /// <summary>작품활동 결과(§5-1). 성공 시 완성도별 명성 증가 + 시간 소모. 실패 시 시간만 소모.</summary>
        public void OnArtworkResult(ArtworkGrade grade, bool success)
        {
            int duration = grade switch
            {
                ArtworkGrade.High => RandRangeInt(artworkHighMinMin, artworkHighMinMax),
                ArtworkGrade.Mid  => RandRangeInt(artworkMidMinMin,  artworkMidMinMax),
                _                 => RandRangeInt(artworkLowMinMin,  artworkLowMinMax),
            };

            float fameGain = 0f;
            if (success)
            {
                float baseV = grade switch
                {
                    ArtworkGrade.High => fameHighBase,
                    ArtworkGrade.Mid  => fameMidBase,
                    _                 => fameLowBase,
                };
                float variance = grade switch
                {
                    ArtworkGrade.High => fameHighVar,
                    ArtworkGrade.Mid  => fameMidVar,
                    _                 => fameLowVar,
                };
                fameGain = baseV + RandRange(-variance, variance);
            }

            // FameSystem에 작품활동 완료를 알림 — 유예 타이머 리셋 + 시간 전진 + 명성 증가를 순서대로 처리
            Fame?.OnArtworkCompleted(fameGain, duration);

            if (success)
                Debug.Log($"[ArtworkSystem] {grade} 성공 +{fameGain:0.0} 명성, {duration}분 소요");
            else
                Debug.Log($"[ArtworkSystem] {grade} 실패, {duration}분 소요");
        }

        float RandRange(float a, float b)  => Rng != null ? a + (b - a) * Rng.Value01() : Random.Range(a, b);
        int   RandRangeInt(int a, int b)   => Rng != null ? a + Mathf.FloorToInt((b - a + 1) * Rng.Value01()) : Random.Range(a, b + 1);
    }
}
