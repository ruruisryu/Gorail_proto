using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// ⑧ 검문 시스템(§8). 같은 역에서 Tracker와 만나면 확률 게이트로 통과/게임오버를 판정한다.
    ///
    /// 연출 분리판: RequestInspection으로 결과를 미리 굴려 "보류"하고 InspectionStarted를 쏜다.
    /// InspectionView가 게이지 연출을 보여준 뒤 CompleteInspection을 호출하면 그때 결과를 적용한다
    /// (통과 → 해당 Tracker 제거, 실패 → 게임오버). 검문 뷰가 없으면 즉시 적용(안전장치).
    ///
    /// 호출부(TurnResolver 등)는 IsInspecting이 false가 될 때까지 기다린 뒤 LastGameOver로 분기한다.
    /// </summary>
    public class InspectionSystem : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float inspectionPassRate = 0.7f;

        public float InspectionPassRate { get => inspectionPassRate; set => inspectionPassRate = Mathf.Clamp01(value); }

        RngService     Rng            => GameCore.Instance?.Rng;
        TrackerManager trackerManager => GameCore.Instance?.Trackers;
        GameManager    gameManager    => GameCore.Instance?.Game;

        /// <summary>검문 연출 시작(역). InspectionView가 구독해 게이지를 재생.</summary>
        public event System.Action<string> InspectionStarted;
        /// <summary>검문 판정 적용 시(역, 통과여부). 통계·토스트용.</summary>
        public event System.Action<string, bool> InspectionResolved;

        /// <summary>검문 연출이 진행 중인지(호출부가 이 동안 대기).</summary>
        public bool IsInspecting { get; private set; }
        /// <summary>이번 검문의 예정 결과(연출 중 공개용).</summary>
        public bool PendingPassed { get; private set; }
        /// <summary>직전 검문이 게임오버였는지(연출 끝난 뒤 분기용).</summary>
        public bool LastGameOver { get; private set; }

        private string _pendingStation;

        /// <summary>검문 연출 시작 요청. 같은 역에 추격자가 있으면 결과를 굴려 보류하고 InspectionStarted 발생.
        /// 추격자가 없으면 아무 일도 안 함(IsInspecting=false 유지).</summary>
        public void RequestInspection(string stationId)
        {
            if (IsInspecting) return;
            LastGameOver = false;
            if (trackerManager == null || string.IsNullOrEmpty(stationId)) return;
            if (!trackerManager.HasTrackerAt(stationId)) return;   // 같은 역 아님 → 검문 없음

            float roll = Rng != null ? Rng.Value01() : Random.value;
            PendingPassed = roll <= inspectionPassRate;
            _pendingStation = stationId;
            IsInspecting = true;
            Debug.Log($"[Inspection] 검문 시작 @ {stationId} (예정: {(PendingPassed ? "통과" : "실패")}, 통과율 {inspectionPassRate:P0})");

            if (InspectionStarted != null) InspectionStarted.Invoke(stationId);
            else CompleteInspection();   // 검문 뷰 없음 → 즉시 판정(안전장치, 무한 대기 방지)
        }

        /// <summary>연출이 끝난 뒤 InspectionView가 호출 — 결과 적용(추격자 제거/게임오버) + InspectionResolved.</summary>
        public void CompleteInspection()
        {
            if (!IsInspecting) return;
            IsInspecting = false;
            string stn = _pendingStation;

            if (PendingPassed)
            {
                trackerManager?.RemoveTrackersAt(stn);     // 어그로 해제(§8-2)
                LastGameOver = false;
                Debug.Log($"[Inspection] 검문 통과 @ {stn}");
                InspectionResolved?.Invoke(stn, true);
            }
            else
            {
                LastGameOver = true;
                Debug.Log($"[Inspection] 검문 실패 @ {stn} → 게임오버");
                InspectionResolved?.Invoke(stn, false);
                gameManager?.TriggerGameOver($"검문 실패 @ {stn}");
            }
        }

        /// <summary>즉시 판정(연출 없음) — 디버그/폴백용. 게임오버이면 true.</summary>
        public bool ResolveAt(string stationId)
        {
            RequestInspection(stationId);
            if (IsInspecting) CompleteInspection();   // 뷰가 있어 보류됐다면 즉시 마무리
            return LastGameOver;
        }
    }
}