using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// [버퍼 H1] 시드 고정 난수. 스폰(§5-2)·첫 등장(§5-4)·감소 제거(§5-5)·검문 굴림(§8-2)을
    /// 시드 기반으로 만들어 "그 상황 다시 보기"(플레이테스트 재현성)를 가능케 한다.
    ///
    /// TrackerManager·InspectionSystem이 선택적으로 참조하며, 미할당이면 UnityEngine.Random으로 폴백한다.
    /// </summary>
    public class RngService : MonoBehaviour
    {
        [Tooltip("고정 시드 사용 여부. 끄면 시작 시 임의 시드.")]
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int seed = 12345;

        private System.Random _rng;

        public int Seed => seed;

        void Awake()
        {
            Reseed(useFixedSeed ? seed : System.Environment.TickCount);
        }

        /// <summary>시드를 다시 설정해 난수열을 초기화(재현 시작점).</summary>
        public void Reseed(int newSeed)
        {
            seed = newSeed;
            _rng = new System.Random(newSeed);
            Debug.Log($"[RngService] seed = {newSeed}");
        }

        /// <summary>[minInclusive, maxExclusive) 정수. 범위가 비면 minInclusive.</summary>
        public int RangeInt(int minInclusive, int maxExclusive)
        {
            EnsureRng();
            return maxExclusive <= minInclusive ? minInclusive : _rng.Next(minInclusive, maxExclusive);
        }

        /// <summary>[0,1) 실수.</summary>
        public float Value01()
        {
            EnsureRng();
            return (float)_rng.NextDouble();
        }

        void EnsureRng()
        {
            if (_rng == null) _rng = new System.Random(seed);
        }
    }
}
