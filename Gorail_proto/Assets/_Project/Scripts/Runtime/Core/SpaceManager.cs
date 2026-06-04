using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public enum Space { Subway, Platform, Ground }

    /// <summary>
    /// [S3] 세 공간(지하철·승강장·지상) 전환 허브(추격 슬라이스 §9). SubwayScene을 영속 베이스로 두고
    /// 승강장(PlatformScene)·지상(OutsideScene)을 additive로 로드/언로드한다. 상태는 베이스에 남아 유지된다.
    ///
    /// 흐름: 지하철 도착 → 승강장 → (재탑승/반대/환승)지하철 또는 (특별역)지상 → 복귀 승강장.
    /// </summary>
    public class SpaceManager : MonoBehaviour
    {
        [SerializeField] private string platformSceneName = "PlatformScene";
        [SerializeField] private string groundSceneName   = "OutsideScene";
        [Tooltip("지하철 공간일 때만 보일 노선도 루트(승강장/지상에선 숨김).")]
        [SerializeField] private GameObject subwayMapRoot;

        public Space Current { get; private set; } = Space.Subway;
        /// <summary>현재 승강장/지상이 속한 역 ID(어느 역에서 내렸는가).</summary>
        public string CurrentStationId { get; private set; }

        public event System.Action<Space> SpaceChanged;

        void Start() => ApplyVisibility(); // 시작은 지하철 공간

        /// <summary>도착역 하차 → 승강장 진입(§7-1).</summary>
        public void EnterPlatform(string stationId)
        {
            CurrentStationId = stationId;
            Unload(groundSceneName);
            Load(platformSceneName);
            SetSpace(Space.Platform);
        }

        /// <summary>승강장에서 지하철로(재탑승·반대방향·환승 후).</summary>
        public void EnterSubway()
        {
            Unload(platformSceneName);
            Unload(groundSceneName);
            SetSpace(Space.Subway);
        }

        /// <summary>승강장에서 지상으로(특별역만, §9·§10).</summary>
        public void EnterGround(string stationId)
        {
            CurrentStationId = stationId;
            Unload(platformSceneName);
            Load(groundSceneName);
            SetSpace(Space.Ground);
        }

        void SetSpace(Space s)
        {
            Current = s;
            ApplyVisibility();
            SpaceChanged?.Invoke(s);
            Debug.Log($"[SpaceManager] → {s} @ {CurrentStationId}");
        }

        void ApplyVisibility()
        {
            if (subwayMapRoot != null) subwayMapRoot.SetActive(Current == Space.Subway);
        }

        void Load(string sceneName)
        {
            if (!IsLoaded(sceneName)) SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }

        void Unload(string sceneName)
        {
            if (IsLoaded(sceneName)) SceneManager.UnloadSceneAsync(sceneName);
        }

        bool IsLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.name == sceneName && sc.isLoaded) return true;
            }
            return false;
        }
    }
}
