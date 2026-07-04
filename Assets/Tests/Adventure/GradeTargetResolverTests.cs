using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class GradeTargetResolverTests
    {
        [TestCase(Outcome.Correct)]
        [TestCase(Outcome.NonWord)]
        public void SoundOutAttempt_UsesSoundOutReference(Outcome outcome)
        {
            Assert.AreEqual(
                "paa_soundout",
                GradeTargetResolver.Resolve(outcome, "paa", "paa_soundout", null));
        }

        [Test]
        public void WrongWord_UsesBuiltWholeWord()
        {
            Assert.AreEqual(
                "kaa",
                GradeTargetResolver.Resolve(Outcome.WrongWord, "paa", "paa_soundout", "kaa"));
        }

        [Test]
        public void MissingSoundOutReference_FallsBackToTarget()
        {
            Assert.AreEqual(
                "paa",
                GradeTargetResolver.Resolve(Outcome.Correct, "paa", "", null));
        }

        [Test]
        public void WrongWordWithoutKnownBuiltWord_UsesSoundOutReference()
        {
            Assert.AreEqual(
                "paa_soundout",
                GradeTargetResolver.Resolve(Outcome.WrongWord, "paa", "paa_soundout", null));
        }
    }
}
