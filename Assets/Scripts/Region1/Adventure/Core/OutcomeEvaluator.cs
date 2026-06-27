using System.Collections.Generic;

namespace WordFlow.Adventure.Core
{
    /// <summary>Local tile-build check. Decides gameplay branch independent of /grade.</summary>
    public static class OutcomeEvaluator
    {
        public static Outcome Evaluate(string builtString, string targetThai, ICollection<string> knownWordsThai)
        {
            builtString ??= string.Empty; // robust to any ICollection whose Contains throws on null
            if (builtString.Length > 0 && builtString == targetThai) return Outcome.Correct;
            if (knownWordsThai != null && knownWordsThai.Contains(builtString)) return Outcome.WrongWord;
            return Outcome.NonWord;
        }
    }
}
