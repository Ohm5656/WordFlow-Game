using UnityEditor;
using UnityEngine;

// WorldMap island unlock is driven by PlayerPrefs. Test playthroughs advance
// HighestUnlockedRegion (MarkRegionCompleted), so the map starts on Island3 (region 2)
// instead of Island2 (region 1 = reference_forest). This resets progress to the start.
public static class ResetWorldMapProgress
{
    const string HighestKey = "WorldMapHighestUnlockedRegion";
    const string PendingKey = "WorldMapPendingUnlockRegion";

    [MenuItem("Tools/Scenes/Reset WorldMap Progress")]
    public static void Run()
    {
        Debug.Log($"[ResetWorldMap] BEFORE: Highest={PlayerPrefs.GetInt(HighestKey, -1)} Pending={PlayerPrefs.GetInt(PendingKey, -1)}");
        PlayerPrefs.DeleteKey(HighestKey);
        PlayerPrefs.DeleteKey(PendingKey);
        PlayerPrefs.Save();
        Debug.Log("[ResetWorldMap] cleared -> next play: region 1 (Island2 -> reference_forest)");
    }
}
