namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Picks which backend word id /grade scores against. Correct/NonWord grade the encounter
    /// word's SOUND-OUT reference (the child said ปอ อา ปา); WrongWord grades the whole-word id
    /// of what they actually built. Empty soundOutWordId falls back to the plain target id, so
    /// encounters without a sound-out entry keep today's behaviour.
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
