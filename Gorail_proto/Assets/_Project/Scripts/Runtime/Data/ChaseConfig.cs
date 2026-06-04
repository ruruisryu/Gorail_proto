using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 추격 슬라이스의 모든 튜닝값을 담는 단일 config (기획서 §15·§16).
    ///
    /// 원칙: 게임플레이 코드는 수치를 하드코딩하지 않고 전부 이 SO를 참조한다.
    /// 대부분 §16의 [미확정] 값이라 지금은 자리표시자 기본값이며,
    /// 플레이테스트(D단계)에서 인스펙터로 조정해 확정한다.
    ///
    /// ②TurnResolver가 실제로 쓰는 값: stepAnimSeconds, inspectAtMidStations.
    /// 나머지(n·m, 체증 곡선, 노선당 상한, 통과율)는 ⑤~⑧에서 이 SO를 참조해 사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ChaseConfig", menuName = "Chase/Chase Config")]
    public class ChaseConfig : ScriptableObject
    {
        [Header("이동 — §4-3 이동 시간 고정")]
        [Tooltip("1역 이동에 드는 게임 내 시간(분). 거리와 무관하게 고정. (§4-1 X)")]
        public float stationTravelMinutes = 2f;

        [Tooltip("역 1칸을 연출하는 실시간 길이(초). '역 골라 쭉 이동'의 체감 속도. (②)")]
        [Range(0.02f, 1.5f)]
        public float stepAnimSeconds = 0.25f;

        [Header("추격 1규칙 — §4-1 (⑤⑥에서 사용)")]
        [Tooltip("플레이어 n역 이동당 Tracker가 전진하는 역 수 m. 지금은 stub만 참조.")]
        public int chaserStepsPerPlayerStep = 1;

        [Header("다량이동 체증 — §4-2 (⑥에서 사용)")]
        [Tooltip("1회 이동에 포함된 역 수 k(가로축)가 클수록 추격 가중(세로축)을 키운다. " +
                 "시간이 아니라 추격량에 거는 보정. 곡선 형태·강도는 D단계 확정.")]
        public AnimationCurve congestionCurve = AnimationCurve.Linear(1f, 1f, 10f, 2f);

        [Header("노선당 개체수 상한 — §5-2 (⑤에서 사용)")]
        [Tooltip("인덱스 = 수배도 레벨(0~5), 값 = 활성 노선 1개당 Tracker 상한. (제안값)")]
        public int[] perLineCapByWantedLevel = { 0, 1, 2, 3, 4, 5 };

        [Header("첫 등장 — §5-4")]
        [Tooltip("세션 최초 스폰 시 플레이어 뒤로 떨어뜨릴 역 범위(최소~최대). 균등 랜덤.")]
        public int firstSpawnMinBehind = 6;
        public int firstSpawnMaxBehind = 8;

        [Header("검문 — §8 (⑧에서 사용)")]
        [Tooltip("ON이면 통과 중인 중간역에서도 같은 역 검문이 발동(§8-1). " +
                 "OFF면 도착역에서만 검문. 추후 기획 변경 여지가 있어 토글로 둠.")]
        public bool inspectAtMidStations = true;

        [Tooltip("같은 역에서 Tracker와 만났을 때 검문 통과 확률(§8-2). 핵심 튜닝 손잡이.")]
        [Range(0f, 1f)]
        public float inspectionPassRate = 0.7f;

        /// <summary>수배도 레벨에 해당하는 노선당 상한을 안전하게 반환(§5-2).</summary>
        public int PerLineCap(int wantedLevel)
        {
            if (perLineCapByWantedLevel == null || perLineCapByWantedLevel.Length == 0) return 0;
            int i = Mathf.Clamp(wantedLevel, 0, perLineCapByWantedLevel.Length - 1);
            return perLineCapByWantedLevel[i];
        }
    }
}
