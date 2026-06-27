using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class TelemetryQueueTests
    {
        [Test]
        public void Empty_TryPeek_ReturnsFalse()
        {
            var q = new TelemetryQueue();
            Assert.IsTrue(q.IsEmpty);
            Assert.AreEqual(0, q.Count);
            Assert.IsFalse(q.TryPeek(0.0, out _));
        }

        [Test]
        public void Enqueue_HeadIsReadyImmediately()
        {
            var q = new TelemetryQueue();
            q.Enqueue("{\"a\":1}");
            Assert.AreEqual(1, q.Count);
            Assert.IsTrue(q.TryPeek(0.0, out string line));
            Assert.AreEqual("{\"a\":1}", line);
        }

        [Test]
        public void Enqueue_IgnoresNullAndBlank()
        {
            var q = new TelemetryQueue();
            q.Enqueue(null);
            q.Enqueue("");
            q.Enqueue("   ");
            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void OnSent_DropsHead_FifoOrder()
        {
            var q = new TelemetryQueue();
            q.Enqueue("first");
            q.Enqueue("second");
            Assert.IsTrue(q.TryPeek(0.0, out string head));
            Assert.AreEqual("first", head);
            q.OnSent();
            Assert.AreEqual(1, q.Count);
            Assert.IsTrue(q.TryPeek(0.0, out string next));
            Assert.AreEqual("second", next);
        }

        [Test]
        public void OnFailed_HoldsHeadUntilBackoffElapses()
        {
            var q = new TelemetryQueue();
            q.Enqueue("x");
            q.OnFailed(100.0);                          // one failure at t=100
            double wait = TelemetryQueue.BackoffSeconds(1);
            Assert.IsFalse(q.TryPeek(100.0, out _));    // still in backoff
            Assert.IsFalse(q.TryPeek(100.0 + wait - 0.01, out _));
            Assert.IsTrue(q.TryPeek(100.0 + wait, out _)); // ready again
        }

        [Test]
        public void Backoff_GrowsAndCaps()
        {
            Assert.AreEqual(0.0, TelemetryQueue.BackoffSeconds(0)); // no failures -> ready
            Assert.Less(TelemetryQueue.BackoffSeconds(1), TelemetryQueue.BackoffSeconds(2));
            Assert.Less(TelemetryQueue.BackoffSeconds(2), TelemetryQueue.BackoffSeconds(3));
            // far out -> clamped to the cap, never unbounded
            Assert.AreEqual(TelemetryQueue.BackoffSeconds(50), TelemetryQueue.BackoffSeconds(60));
        }

        [Test]
        public void OnSent_ResetsFailureStreak_ForNextHead()
        {
            var q = new TelemetryQueue();
            q.Enqueue("a");
            q.Enqueue("b");
            q.OnFailed(0.0);
            q.OnFailed(TelemetryQueue.BackoffSeconds(1)); // two consecutive failures on head "a"
            q.OnSent();                                   // "a" finally sent -> drop, reset streak
            Assert.IsTrue(q.TryPeek(0.0, out string next), "new head should be ready immediately");
            Assert.AreEqual("b", next);
        }

        [Test]
        public void SerializeLoad_RoundTrips_SkippingBlankLines()
        {
            var q = new TelemetryQueue();
            q.Enqueue("{\"a\":1}");
            q.Enqueue("{\"b\":2}");
            string text = q.Serialize();

            var loaded = new TelemetryQueue();
            loaded.Load("\n" + text + "\n\n");   // stray blank lines must be ignored
            Assert.AreEqual(2, loaded.Count);
            Assert.IsTrue(loaded.TryPeek(0.0, out string head));
            Assert.AreEqual("{\"a\":1}", head);
        }

        [Test]
        public void Load_ReplacesExistingContents()
        {
            var q = new TelemetryQueue();
            q.Enqueue("old");
            q.Load("new1\nnew2");
            Assert.AreEqual(2, q.Count);
            Assert.IsTrue(q.TryPeek(0.0, out string head));
            Assert.AreEqual("new1", head);
        }
    }
}
