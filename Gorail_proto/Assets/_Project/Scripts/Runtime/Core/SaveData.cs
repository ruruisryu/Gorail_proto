using System.Collections.Generic;

namespace Game.Core
{
    [System.Serializable]
    public class SaveData
    {
        // ── 플레이어 ──────────────────────────────────────────────────────
        public string       stationId;
        public string       lineId;
        public int          direction;
        public bool         directionLocked;
        public List<string> activeLines = new();

        // ── 게임 시간 ─────────────────────────────────────────────────────
        public int day;
        public int hour;
        public int minute;

        // ── 자원 ──────────────────────────────────────────────────────────
        public int   money;
        public float fame;
        public int   fameLastArtworkMinutes;
        public int   famePrevMinutes;

        // ── 추격자 ────────────────────────────────────────────────────────
        public List<TrackerEntry> trackers = new();
        public float              trackerDebt;

        // ── 승강장 ────────────────────────────────────────────────────────
        public List<string> artworkDoneStations = new();

        [System.Serializable]
        public class TrackerEntry
        {
            public string stationId;
            public string lineId;
        }
    }
}
