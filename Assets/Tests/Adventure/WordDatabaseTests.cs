using NUnit.Framework;
using UnityEngine;
using WordFlow.Adventure.Data;

namespace WordFlow.Adventure.Tests
{
    public sealed class WordDatabaseTests
    {
        private static WordEncounterData Word(string id, string thai)
        {
            var w = ScriptableObject.CreateInstance<WordEncounterData>();
            w.id = id; w.thai = thai;
            return w;
        }

        [Test]
        public void Lookup_ByThai_ReturnsEntry()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));
            db.words.Add(Word("paa", "ปา"));

            Assert.AreEqual("kaa", db.LookupByThai("กา").id);
        }

        [Test]
        public void Lookup_UnknownThai_ReturnsNull()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));

            Assert.IsNull(db.LookupByThai("าก"));
        }

        [Test]
        public void Contains_KnownThai_True_UnknownThai_False()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.words.Add(Word("kaa", "กา"));

            Assert.IsTrue(db.ContainsThai("กา"));
            Assert.IsFalse(db.ContainsThai("xx"));
        }
    }
}
