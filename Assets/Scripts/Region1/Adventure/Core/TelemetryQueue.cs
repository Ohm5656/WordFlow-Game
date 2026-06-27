using System;
using System.Collections.Generic;
using System.Text;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Pure C# durable-telemetry FIFO. Holds pending journal lines (each one JSON object
    /// string) and the retry bookkeeping for the head: a failed send pushes the head into
    /// an exponential backoff so the pump doesn't hammer a down backend. Zero Unity/IO deps —
    /// the MonoBehaviour owns disk + HTTP and feeds the wall clock in seconds.
    /// </summary>
    public sealed class TelemetryQueue
    {
        private const double BackoffBaseSeconds = 1.0;
        private const double BackoffCapSeconds = 30.0;

        private readonly List<string> _lines = new List<string>();
        private int _headFailures;     // consecutive failures of the CURRENT head
        private double _nextAttemptAt;  // earliest wall-clock (seconds) the head may be retried

        public int Count => _lines.Count;
        public bool IsEmpty => _lines.Count == 0;

        /// <summary>Append one journal line. Null/blank is ignored (never journalled).</summary>
        public void Enqueue(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine)) return;
            _lines.Add(jsonLine);
        }

        /// <summary>True (with the head line) only if a line exists and its backoff has elapsed.</summary>
        public bool TryPeek(double nowSeconds, out string line)
        {
            if (_lines.Count == 0 || nowSeconds < _nextAttemptAt)
            {
                line = null;
                return false;
            }
            line = _lines[0];
            return true;
        }

        /// <summary>Head sent successfully: drop it and clear the failure streak for the next head.</summary>
        public void OnSent()
        {
            if (_lines.Count > 0) _lines.RemoveAt(0);
            _headFailures = 0;
            _nextAttemptAt = 0.0;
        }

        /// <summary>Head send failed: grow the streak and hold the head until the next backoff window.</summary>
        public void OnFailed(double nowSeconds)
        {
            _headFailures++;
            _nextAttemptAt = nowSeconds + BackoffSeconds(_headFailures);
        }

        /// <summary>Newline-joined journal text (one line per pending entry).</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_lines[i]);
            }
            return sb.ToString();
        }

        /// <summary>Replace contents from journal text, skipping blank lines. Resets retry state.</summary>
        public void Load(string text)
        {
            _lines.Clear();
            _headFailures = 0;
            _nextAttemptAt = 0.0;
            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                var trimmed = raw.Trim();
                if (trimmed.Length > 0) _lines.Add(trimmed);
            }
        }

        /// <summary>Exponential backoff in seconds: 0 for no failures, doubling, clamped to a cap.</summary>
        public static double BackoffSeconds(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0) return 0.0;
            double delay = BackoffBaseSeconds * Math.Pow(2, consecutiveFailures - 1);
            return Math.Min(BackoffCapSeconds, delay);
        }
    }
}
