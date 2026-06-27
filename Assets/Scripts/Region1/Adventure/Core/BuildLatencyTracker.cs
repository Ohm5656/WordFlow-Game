using System;
using System.Collections.Generic;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Times the build phase from "tiles interactive" (Start) to IsComplete (Complete).
    /// Pure C#, zero Unity deps. Latency is measured on BUILDING (cognition / RAN-relevant),
    /// not on speech onset. Fed seconds; reports milliseconds.
    /// </summary>
    public sealed class BuildLatencyTracker
    {
        private readonly List<long> _perTileMs = new List<long>();
        private double _startSeconds;
        private double _lastEventSeconds;
        private bool _running;

        public bool HasResult { get; private set; }
        public long TotalMs { get; private set; }
        public IReadOnlyList<long> PerTilePlacementMs => _perTileMs;

        public void Start(double nowSeconds)
        {
            _perTileMs.Clear();
            _startSeconds = nowSeconds;
            _lastEventSeconds = nowSeconds;
            _running = true;
            HasResult = false;
            TotalMs = 0;
        }

        public void RecordPlacement(double nowSeconds)
        {
            if (!_running) return;
            _perTileMs.Add(ToMs(nowSeconds - _lastEventSeconds));
            _lastEventSeconds = nowSeconds;
        }

        public void Complete(double nowSeconds)
        {
            if (!_running) return;
            TotalMs = ToMs(nowSeconds - _startSeconds);
            HasResult = true;
            _running = false;
        }

        public void Reset()
        {
            _perTileMs.Clear();
            _running = false;
            HasResult = false;
            TotalMs = 0;
        }

        private static long ToMs(double seconds) => (long)Math.Round(Math.Max(0.0, seconds) * 1000.0);
    }
}
