using UnityEngine;

/// Best star count (0-3) ever earned per quest. Written by MagicStonePuzzleController when a quest is
/// completed with the correct word; read by the night route to decide what can be redone.
public static class QuestStars
{
    private const string Prefix = "QuestStars_";
    public const int Max = 3;

    public static int Get(string questId) =>
        string.IsNullOrEmpty(questId) ? 0 : Mathf.Clamp(PlayerPrefs.GetInt(Prefix + questId, 0), 0, Max);

    /// Best-of: a night redo that goes worse never lowers the record.
    public static void RecordBest(string questId, int stars)
    {
        if (string.IsNullOrEmpty(questId)) return;
        int best = Mathf.Max(Get(questId), Mathf.Clamp(stars, 0, Max));
        PlayerPrefs.SetInt(Prefix + questId, best);
        PlayerPrefs.Save();
    }

    /// Completed but not perfect -> redoable at night.
    public static bool NeedsRedo(string questId)
    {
        int s = Get(questId);
        return s > 0 && s < Max;
    }

    /// Has any quest been scored at all? False on a fresh game — nothing has been played yet.
    public static bool AnyRecorded(string[] questIds)
    {
        if (questIds == null) return false;
        foreach (string id in questIds)
        {
            if (Get(id) > 0) return true;
        }
        return false;
    }

    public static bool AnyNeedsRedo(string[] questIds)
    {
        if (questIds == null) return false;
        foreach (string id in questIds)
        {
            if (NeedsRedo(id) && !NightMode.IsDoneThisNight(id)) return true;
        }
        return false;
    }

    public static void Clear(string[] questIds)
    {
        if (questIds == null) return;
        foreach (string id in questIds) PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.Save();
    }
}
