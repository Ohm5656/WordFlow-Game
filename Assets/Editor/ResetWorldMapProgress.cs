using UnityEditor;
using UnityEngine;

// WorldMap island unlock is driven by PlayerPrefs. Test playthroughs advance
// HighestUnlockedRegion (MarkRegionCompleted), so the map starts on Island3 (region 2)
// instead of Island2 (region 1 = reference_forest). This resets progress to the start.
public static class ResetWorldMapProgress
{
    const string HighestKey = "WorldMapHighestUnlockedRegion";
    const string PendingKey = "WorldMapPendingUnlockRegion";

    [InitializeOnEnterPlayMode]
    static void ResetOnEnterPlayMode(EnterPlayModeOptions options)
    {
        ResetFreshDay(false);
    }

    [MenuItem("Tools/Scenes/Reset WorldMap Progress")]
    public static void Run()
    {
        ResetFreshDay(true);
    }

    static void ResetFreshDay(bool log)
    {
        if (log)
        {
            Debug.Log($"[ResetWorldMap] BEFORE: Highest={PlayerPrefs.GetInt(HighestKey, -1)} Pending={PlayerPrefs.GetInt(PendingKey, -1)}");
        }

        PlayerPrefs.DeleteKey(HighestKey);
        PlayerPrefs.DeleteKey(PendingKey);

        QuestStars.Clear(NightMode.QuestIds);
        NightMode.ResetAll(NightMode.QuestIds);
        PlayerPrefs.DeleteKey("StarEarnedCount");
        PlayerPrefs.DeleteKey("WordAssemblyTimerRemaining");
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterCrow");
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterAlt");
        PlayerPrefs.Save();

        // These can survive when Enter Play Mode Options disables domain reload.
        BearEncounterFlow.ReturnToForest = false;
        BearEncounterFlow.ResumeAtBeat2 = false;
        BearEncounterFlow.ResumeAtBeat3 = false;

        if (log)
        {
            Debug.Log("[ResetWorldMap] cleared -> next play is a fresh daytime run (Island2 -> reference_forest)");
        }
    }
}
