/// <summary>
/// Cross-scene hand-off flags for the bear word-build encounter that the first
/// reference_forest quest beat opens.
///
/// Flow:
///  - reference_forest Beat 1 reveals the bear, sets <see cref="ReturnToForest"/>,
///    and loads word_build_paa_polished.
///  - On a successful build word_build_paa_polished sees <see cref="ReturnToForest"/>,
///    sets <see cref="ResumeAtBeat2"/>, and loads reference_forest back.
///  - reference_forest sees <see cref="ResumeAtBeat2"/> and skips Beat 1 (the bear is
///    already gone), continuing at Beat 2.
///
/// Plain statics survive scene loads within a play session and reset on domain
/// reload / play-stop, matching the existing MagicStonePuzzleController flag pattern.
/// If the encounter is opened standalone (no forest), both flags stay false and the
/// encounter keeps its original behaviour (no auto-return).
/// </summary>
public static class BearEncounterFlow
{
    /// <summary>Set by the forest before entering the encounter; the encounter returns to the forest on success.</summary>
    public static bool ReturnToForest;

    /// <summary>Set while returning; tells the forest to skip Beat 1 and resume at Beat 2 (crow/ga quest).</summary>
    public static bool ResumeAtBeat2;

    /// <summary>Set while returning from the crow (ga) encounter; the forest skips Beat 1+2 and resumes at Beat 3.</summary>
    public static bool ResumeAtBeat3;
}
