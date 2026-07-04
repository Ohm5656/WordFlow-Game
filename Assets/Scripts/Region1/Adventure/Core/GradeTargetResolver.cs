namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Picks which backend word id /grade scores against. Correct/NonWord grade the encounter
    /// word's sound-out reference; WrongWord grades the whole-word id actually built.
    /// Encounters without a sound-out id keep the original whole-word behaviour.
    /// </summary>
    public static class GradeTargetResolver
    {
        public static string Resolve(Outcome outcome, string targetId, string soundOutWordId, string builtWordId)
        {
            if (outcome == Outcome.WrongWord && !string.IsNullOrEmpty(builtWordId))
                return builtWordId;
            if (!string.IsNullOrEmpty(soundOutWordId))
                return soundOutWordId;
            return targetId;
        }
    }
}
