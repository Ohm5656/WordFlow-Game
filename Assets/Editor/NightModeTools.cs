using UnityEditor;
using UnityEngine;

// Test menus for the night redo pass. There is no way to reach night mode without a full daytime
// playthrough, so these simulate the PlayerPrefs state a real playthrough would have left behind.
public static class NightModeTools
{
    [MenuItem("Tools/Night/Simulate Day Done (2★ + 2★)")]
    public static void SimulateTwoAndTwo()
    {
        QuestStars.RecordBest("paa", 2);
        QuestStars.RecordBest("kaa", 2);
        NightMode.NightPhase = true;
        NightMode.RedoActive = false;
        LogState("SimulateTwoAndTwo");
    }

    [MenuItem("Tools/Night/Simulate Day Done (2★ + 3★)")]
    public static void SimulateTwoAndThree()
    {
        QuestStars.RecordBest("paa", 2);
        QuestStars.RecordBest("kaa", 3);
        NightMode.NightPhase = true;
        NightMode.RedoActive = false;
        LogState("SimulateTwoAndThree");
    }

    [MenuItem("Tools/Night/Reset Night + Stars")]
    public static void ResetNightAndStars()
    {
        QuestStars.Clear(NightMode.QuestIds);
        NightMode.ResetAll(NightMode.QuestIds);
        PlayerPrefs.DeleteKey("StarEarnedCount");
        PlayerPrefs.DeleteKey("WordAssemblyTimerRemaining");
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterCrow");
        PlayerPrefs.DeleteKey("MagicStonePuzzleRetryAfterAlt");
        PlayerPrefs.Save();
        Debug.Log("[NightModeTools] cleared night + star + puzzle-retry state -> next play starts a clean day run");
    }

    [MenuItem("Tools/Night/Log State")]
    public static void LogState() => LogState("LogState");

    private static void LogState(string caller)
    {
        Debug.Log($"[NightModeTools:{caller}] NightPhase={NightMode.NightPhase} RedoActive={NightMode.RedoActive} " +
            $"ActiveQuest='{NightMode.ActiveQuest}' " +
            $"paa={QuestStars.Get("paa")}★ (doneTonight={NightMode.IsDoneThisNight("paa")}) " +
            $"kaa={QuestStars.Get("kaa")}★ (doneTonight={NightMode.IsDoneThisNight("kaa")})");
    }
}
