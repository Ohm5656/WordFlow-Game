using UnityEngine;

/// The night pass: after the daytime forest run ends, the world is in NIGHT PHASE and any quest that
/// scored &lt;3 stars can be redone once per night session.
///  - NightPhase  : the world has turned to night (set when the day forest run ends). Drives the
///                  WorldMap night look (dark, only the playable island lit).
///  - RedoActive  : we are inside a night redo run (forest + its puzzle scenes render at night,
///                  the owl intro is skipped). Set when the player clicks the (lit) island at night.
///  - ActiveQuest : the quest id currently being redone (empty = not inside a puzzle). Set by the
///                  forest right before it loads the puzzle scene; consumed when it comes back.
///  - DoneThisNight(id) : that quest was attempted this night session — do not offer it again, even
///                  if it is still at 2 stars. Without this the night route loops forever on a quest
///                  the child fails to improve.
public static class NightMode
{
    /// The quests that own a puzzle, in walk order. Must equal each puzzle's targetWordId.
    /// Single source of truth — QuestPathSequence.nightQuestIds mirrors this as a serialized default.
    public static readonly string[] QuestIds = { "paa", "kaa" };

    private const string PhaseKey = "NightPhase";
    private const string RedoKey = "NightRedoActive";
    private const string ActiveQuestKey = "NightActiveQuest";
    private const string DonePrefix = "NightDone_";

    /// True once the daytime run has ended. Guarded by "has any quest actually been scored?" — a
    /// stale flag (a killed play session, an editor test) can otherwise leave a fresh game starting
    /// at night with no stars to redo, which is never a valid state.
    public static bool NightPhase
    {
        get => PlayerPrefs.GetInt(PhaseKey, 0) == 1 && QuestStars.AnyRecorded(QuestIds);
        set { PlayerPrefs.SetInt(PhaseKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool RedoActive
    {
        get => PlayerPrefs.GetInt(RedoKey, 0) == 1;
        set { PlayerPrefs.SetInt(RedoKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string ActiveQuest
    {
        get => PlayerPrefs.GetString(ActiveQuestKey, string.Empty);
        set { PlayerPrefs.SetString(ActiveQuestKey, value ?? string.Empty); PlayerPrefs.Save(); }
    }

    public static bool IsDoneThisNight(string questId) =>
        !string.IsNullOrEmpty(questId) && PlayerPrefs.GetInt(DonePrefix + questId, 0) == 1;

    public static void MarkDoneThisNight(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        PlayerPrefs.SetInt(DonePrefix + questId, 1);
        PlayerPrefs.Save();
    }

    /// Pressed the star button: a brand new night session — every quest is offerable again.
    public static void BeginSession(string[] questIds)
    {
        if (questIds != null)
        {
            foreach (string id in questIds) PlayerPrefs.DeleteKey(DonePrefix + id);
        }
        ActiveQuest = string.Empty;
        RedoActive = true;
    }

    /// Back on the WorldMap: the run is over (the night phase itself stays on).
    public static void EndSession()
    {
        ActiveQuest = string.Empty;
        RedoActive = false;
    }

    public static void ResetAll(string[] questIds)
    {
        PlayerPrefs.DeleteKey(PhaseKey);
        PlayerPrefs.DeleteKey(RedoKey);
        PlayerPrefs.DeleteKey(ActiveQuestKey);
        if (questIds != null)
        {
            foreach (string id in questIds) PlayerPrefs.DeleteKey(DonePrefix + id);
        }
        PlayerPrefs.Save();
    }
}
