using System.Text;
using UnityEngine;

/// <summary>
/// Keeps locally persisted gameplay progress aligned with the authenticated child profile.
/// The gameplay systems still read their existing PlayerPrefs keys, so this class swaps those
/// keys when the logged-in child changes and clears them for a brand-new child.
/// </summary>
public static class LocalProgressProfile
{
    // Temporary testing behavior: a fresh app/play launch should never resume the night loop.
    // Set this false when real day/night persistence is ready.
    private const bool ResetNightProgressOnLaunchForTestingEnabled = true;

    private const string ActiveChildKey = "wf_active_child_id";
    private const string ProfilePrefix = "wf_profile_";
    private const string ProfileExistsSuffix = "exists";
    private const string WorldMapHighestUnlockedRegionKey = "WorldMapHighestUnlockedRegion";
    private const string WorldMapPendingUnlockRegionKey = "WorldMapPendingUnlockRegion";
    private const string NightPhaseKey = "NightPhase";
    private const string NightRedoActiveKey = "NightRedoActive";
    private const string NightActiveQuestKey = "NightActiveQuest";
    private const string StarEarnedCountKey = "StarEarnedCount";
    private const string MagicStoneRetryAfterCrowKey = "MagicStonePuzzleRetryAfterCrow";
    private const string MagicStoneRetryAfterAltKey = "MagicStonePuzzleRetryAfterAlt";
    private const string WordAssemblyTimerRemainingKey = "WordAssemblyTimerRemaining";

    private static readonly string[] IntKeys =
    {
        WorldMapHighestUnlockedRegionKey,
        WorldMapPendingUnlockRegionKey,
        NightPhaseKey,
        NightRedoActiveKey,
        StarEarnedCountKey,
        MagicStoneRetryAfterCrowKey,
        MagicStoneRetryAfterAltKey
    };

    private static readonly string[] FloatKeys =
    {
        WordAssemblyTimerRemainingKey
    };

    private static readonly string[] StringKeys =
    {
        NightActiveQuestKey
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetNightProgressOnLaunchForTesting()
    {
        if (!ResetNightProgressOnLaunchForTestingEnabled)
        {
            return;
        }

        if (ClearNightProgressForTesting("app launch"))
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Returns true when global gameplay prefs were cleared for this authenticated child.
    /// </summary>
    public static bool ApplyAuthenticatedChild(string childId, bool isNewProfile)
    {
        if (string.IsNullOrWhiteSpace(childId))
        {
            return false;
        }

        string previousChildId = PlayerPrefs.GetString(ActiveChildKey, string.Empty);
        bool childChanged = !string.IsNullOrEmpty(previousChildId) && previousChildId != childId;
        if (childChanged)
        {
            SaveCurrentProfile(previousChildId);
        }

        bool clearedProgress = false;
        if (isNewProfile)
        {
            DeleteProfile(childId);
            ClearGameplayPrefs();
            clearedProgress = true;
        }
        else if (childChanged)
        {
            if (ProfileExists(childId))
            {
                LoadProfile(childId);
            }
            else
            {
                ClearGameplayPrefs();
                clearedProgress = true;
            }
        }
        else if (string.IsNullOrEmpty(previousChildId) && ProfileExists(childId))
        {
            LoadProfile(childId);
        }

        if (ResetNightProgressOnLaunchForTestingEnabled
            && ClearNightProgressForTesting("auth bootstrap"))
        {
            clearedProgress = true;
        }

        PlayerPrefs.SetString(ActiveChildKey, childId);
        SaveCurrentProfile(childId);
        PlayerPrefs.Save();
        return clearedProgress;
    }

    private static void SaveCurrentProfile(string childId)
    {
        if (string.IsNullOrWhiteSpace(childId))
        {
            return;
        }

        string prefix = ScopedPrefix(childId);
        CopyToProfile(IntKeys, prefix, PrefKind.Int);
        CopyToProfile(FloatKeys, prefix, PrefKind.Float);
        CopyToProfile(StringKeys, prefix, PrefKind.String);
        CopyQuestPrefsToProfile(prefix);
        PlayerPrefs.SetInt(prefix + ProfileExistsSuffix, 1);
    }

    private static void LoadProfile(string childId)
    {
        string prefix = ScopedPrefix(childId);
        CopyFromProfile(IntKeys, prefix, PrefKind.Int);
        CopyFromProfile(FloatKeys, prefix, PrefKind.Float);
        CopyFromProfile(StringKeys, prefix, PrefKind.String);
        CopyQuestPrefsFromProfile(prefix);
    }

    private static void DeleteProfile(string childId)
    {
        string prefix = ScopedPrefix(childId);
        DeleteProfileKeys(IntKeys, prefix);
        DeleteProfileKeys(FloatKeys, prefix);
        DeleteProfileKeys(StringKeys, prefix);
        DeleteQuestProfileKeys(prefix);
        PlayerPrefs.DeleteKey(prefix + ProfileExistsSuffix);
    }

    private static bool ProfileExists(string childId) =>
        PlayerPrefs.GetInt(ScopedPrefix(childId) + ProfileExistsSuffix, 0) == 1;

    private static void ClearGameplayPrefs()
    {
        DeleteGlobalKeys(IntKeys);
        DeleteGlobalKeys(FloatKeys);
        DeleteGlobalKeys(StringKeys);
        NightMode.ResetAll(NightMode.QuestIds);
        QuestStars.Clear(NightMode.QuestIds);
    }

    private static bool ClearNightProgressForTesting(string reason)
    {
        if (!HasNightProgress())
        {
            return false;
        }

        ClearGameplayPrefs();
        Debug.Log($"[Progress] Cleared night progress for testing on {reason}.");
        return true;
    }

    private static bool HasNightProgress() =>
        PlayerPrefs.GetInt(NightPhaseKey, 0) == 1
        || PlayerPrefs.GetInt(NightRedoActiveKey, 0) == 1
        || !string.IsNullOrEmpty(PlayerPrefs.GetString(NightActiveQuestKey, string.Empty));

    private static void CopyQuestPrefsToProfile(string prefix)
    {
        string[] questIds = NightMode.QuestIds;
        if (questIds == null)
        {
            return;
        }

        for (int i = 0; i < questIds.Length; i++)
        {
            string id = questIds[i];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            CopyToProfile("QuestStars_" + id, prefix, PrefKind.Int);
            CopyToProfile("NightDone_" + id, prefix, PrefKind.Int);
        }
    }

    private static void CopyQuestPrefsFromProfile(string prefix)
    {
        string[] questIds = NightMode.QuestIds;
        if (questIds == null)
        {
            return;
        }

        for (int i = 0; i < questIds.Length; i++)
        {
            string id = questIds[i];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            CopyFromProfile("QuestStars_" + id, prefix, PrefKind.Int);
            CopyFromProfile("NightDone_" + id, prefix, PrefKind.Int);
        }
    }

    private static void DeleteQuestProfileKeys(string prefix)
    {
        string[] questIds = NightMode.QuestIds;
        if (questIds == null)
        {
            return;
        }

        for (int i = 0; i < questIds.Length; i++)
        {
            string id = questIds[i];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            PlayerPrefs.DeleteKey(prefix + "QuestStars_" + id);
            PlayerPrefs.DeleteKey(prefix + "NightDone_" + id);
        }
    }

    private static void CopyToProfile(string[] keys, string prefix, PrefKind kind)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            CopyToProfile(keys[i], prefix, kind);
        }
    }

    private static void CopyToProfile(string key, string prefix, PrefKind kind)
    {
        string scopedKey = prefix + key;
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(scopedKey);
            return;
        }

        switch (kind)
        {
            case PrefKind.Int:
                PlayerPrefs.SetInt(scopedKey, PlayerPrefs.GetInt(key));
                break;
            case PrefKind.Float:
                PlayerPrefs.SetFloat(scopedKey, PlayerPrefs.GetFloat(key));
                break;
            case PrefKind.String:
                PlayerPrefs.SetString(scopedKey, PlayerPrefs.GetString(key));
                break;
        }
    }

    private static void CopyFromProfile(string[] keys, string prefix, PrefKind kind)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            CopyFromProfile(keys[i], prefix, kind);
        }
    }

    private static void CopyFromProfile(string key, string prefix, PrefKind kind)
    {
        string scopedKey = prefix + key;
        if (!PlayerPrefs.HasKey(scopedKey))
        {
            PlayerPrefs.DeleteKey(key);
            return;
        }

        switch (kind)
        {
            case PrefKind.Int:
                PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(scopedKey));
                break;
            case PrefKind.Float:
                PlayerPrefs.SetFloat(key, PlayerPrefs.GetFloat(scopedKey));
                break;
            case PrefKind.String:
                PlayerPrefs.SetString(key, PlayerPrefs.GetString(scopedKey));
                break;
        }
    }

    private static void DeleteGlobalKeys(string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            PlayerPrefs.DeleteKey(keys[i]);
        }
    }

    private static void DeleteProfileKeys(string[] keys, string prefix)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            PlayerPrefs.DeleteKey(prefix + keys[i]);
        }
    }

    private static string ScopedPrefix(string childId) => ProfilePrefix + Sanitize(childId) + "__";

    private static string Sanitize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return builder.ToString();
    }

    private enum PrefKind
    {
        Int,
        Float,
        String
    }
}
