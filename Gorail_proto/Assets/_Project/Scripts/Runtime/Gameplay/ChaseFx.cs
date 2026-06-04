using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// [H6] 노선도 연출 오버레이. 마커 재생성(RefreshMarkers)과 충돌하지 않게 전용 [Fx] 레이어에 그린다.
    ///   ① 검문 깜빡임 — 같은 역 만남(검문) 시 그 역에 통과=초록/실패=빨강 링이 퍼지며 사라짐.
    ///   ② 최근접 추격자 강조 — 가장 가까운 추격자 위에 맥동(pulse)하는 헤일로를 띄워 위협을 한눈에.
    /// ChaseEvents 허브 하나만 구독한다.
    /// </summary>
    public class ChaseFx : MonoBehaviour
    {
        [SerializeField] private SubwayMapRenderer mapRenderer;
        [Tooltip("검문 깜빡임 지속(초).")]
        [SerializeField] private float flashSeconds = 0.7f;

        private static readonly Color PassColor = new Color(0.40f, 1f, 0.50f);
        private static readonly Color FailColor = new Color(1f, 0.30f, 0.25f);
        private static readonly Color HaloColor = new Color(1f, 0.85f, 0.20f);

        private RectTransform _fx;
        private Image _halo;
        private bool _subbed;

        private class Flash { public Image img; public float t; public float zoom; }
        private readonly List<Flash> _flashes = new List<Flash>();

        void Update()
        {
            if (!_subbed && ChaseEvents.Instance != null)
            {
                ChaseEvents.Instance.InspectionResolved += OnInspection;
                _subbed = true;
            }
            if (mapRenderer == null) return;

            UpdateFlashes();
            UpdateNearestHalo();
        }

        void OnDestroy()
        {
            if (_subbed && ChaseEvents.Instance != null)
                ChaseEvents.Instance.InspectionResolved -= OnInspection;
        }

        // ── ① 검문 깜빡임 ────────────────────────────────────────────────
        void OnInspection(string stationId, bool passed)
        {
            if (mapRenderer == null) return;
            var pos = mapRenderer.GetStationUIPos(stationId);
            if (!pos.HasValue) return;
            EnsureLayer();
            var img = mapRenderer.CreateFxCircle(_fx, pos.Value, 34f, passed ? PassColor : FailColor);
            _flashes.Add(new Flash { img = img, t = 0f, zoom = mapRenderer.ZoomComp });
        }

        void UpdateFlashes()
        {
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var f = _flashes[i];
                if (f.img == null) { _flashes.RemoveAt(i); continue; }
                f.t += Time.deltaTime;
                float p = Mathf.Clamp01(f.t / Mathf.Max(0.01f, flashSeconds));
                float scale = Mathf.Lerp(0.8f, 2.6f, p) * f.zoom;   // 퍼짐
                var c = f.img.color; c.a = Mathf.Lerp(0.9f, 0f, p); // 페이드
                f.img.color = c;
                f.img.rectTransform.localScale = Vector3.one * scale;
                if (p >= 1f) { Destroy(f.img.gameObject); _flashes.RemoveAt(i); }
            }
        }

        // ── ② 최근접 추격자 강조 ─────────────────────────────────────────
        void UpdateNearestHalo()
        {
            var core = GameCore.Instance;
            bool canShow = core != null && core.Space != null && core.Space.Current == Game.Core.Space.Subway
                           && core.Trackers != null && core.Trackers.Trackers.Count > 0
                           && core.Graph != null && core.Player != null;

            Vector2? pos = null;
            if (canShow)
            {
                var nearest = ChaseMetrics.NearestTracker(core.Graph.Graph, core.Trackers.Trackers, core.Player.CurrentStationId);
                if (nearest != null) pos = mapRenderer.GetStationUIPos(nearest.StationId);
            }

            if (!pos.HasValue) { if (_halo != null) _halo.enabled = false; return; }

            if (_halo == null)
            {
                EnsureLayer();
                _halo = mapRenderer.CreateFxCircle(_fx, pos.Value, 48f, HaloColor);
                _halo.transform.SetAsFirstSibling(); // 깜빡임보다 아래
            }
            _halo.enabled = true;
            _halo.rectTransform.anchoredPosition = pos.Value;
            float pulse = 1f + 0.20f * Mathf.Sin(Time.unscaledTime * 6f);
            _halo.rectTransform.localScale = Vector3.one * mapRenderer.ZoomComp * pulse;
            var hc = HaloColor; hc.a = 0.30f + 0.18f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            _halo.color = hc;
        }

        void EnsureLayer()
        {
            if (_fx == null) _fx = mapRenderer.GetOrCreateFxLayer();
        }
    }
}
