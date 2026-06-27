using System.Collections.Generic;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class OutcomeEvaluatorTests
    {
        private static readonly HashSet<string> Known = new HashSet<string> { "กา", "ปา", "ตา" };

        [Test]
        public void ExactTarget_IsCorrect()
        {
            Assert.AreEqual(Outcome.Correct, OutcomeEvaluator.Evaluate("กา", "กา", Known));
        }

        [Test]
        public void DifferentKnownWord_IsWrongWord()
        {
            Assert.AreEqual(Outcome.WrongWord, OutcomeEvaluator.Evaluate("ปา", "กา", Known));
        }

        [Test]
        public void UnknownString_IsNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate("าก", "กา", Known));
        }

        [Test]
        public void EmptyBuild_IsNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate("", "กา", Known));
        }

        [Test]
        public void NullBuild_IsNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate(null, "กา", Known));
        }

        [Test]
        public void NullKnownWords_FallsBackToNonWord()
        {
            Assert.AreEqual(Outcome.NonWord, OutcomeEvaluator.Evaluate("ปา", "กา", null));
            Assert.AreEqual(Outcome.Correct, OutcomeEvaluator.Evaluate("กา", "กา", null));
        }
    }
}
