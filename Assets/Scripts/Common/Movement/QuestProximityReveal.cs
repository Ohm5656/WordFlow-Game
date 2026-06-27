using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hides each child of this object at runtime, then reveals each one individually
/// once the player character walks within <see cref="revealRadius"/> of that child.
/// Put this on the "quest" container; its children (markers, NPCs) are the items.
///
/// Once an item is revealed it stays revealed (re-hiding on quest completion is a
/// separate future step — call <see cref="HideItem"/> / <see cref="HideAll"/> for that).
/// </summary>
public sealed class QuestProximityReveal : MonoBehaviour
{
    [Tooltip("Player transform. Leave empty to auto-detect at runtime.")]
    [SerializeField] private Transform player;

    [Tooltip("Tried first when auto-detecting the player (ignored if the tag is undefined).")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Object names tried (in order) when the tag lookup fails.")]
    [SerializeField] private string[] playerNameFallbacks = { "playerRoot", "base_basic_pbr" };

    [Tooltip("How close (world units) the player must get to an item to reveal it.")]
    [SerializeField] private float revealRadius = 3.5f;

    [Tooltip("Compare distance on the XY plane only (top-down 2D). Ignores Z.")]
    [SerializeField] private bool ignoreZ = true;

    [Tooltip("Hide all items when the game starts.")]
    [SerializeField] private bool hideAtStart = true;

    [Tooltip("Fade every item in at game start (after a short delay) instead of waiting for the player to approach.")]
    [SerializeField] private bool revealAtStart = false;

    [Tooltip("Delay before the start fade-in (seconds).")]
    [SerializeField] private float revealAtStartDelay = 0.3f;

    [Tooltip("Invoked once per item the moment it is revealed (e.g. to make the player walk to it).")]
    [SerializeField] private UnityEvent<Transform> onItemRevealed;

    private readonly List<Transform> items = new List<Transform>();
    private readonly HashSet<Transform> revealed = new HashSet<Transform>();
    private readonly HashSet<Transform> excluded = new HashSet<Transform>();
    private float nextPlayerSearch;

    private void Awake()
    {
        items.Clear();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (excluded.Contains(child))
            {
                child.gameObject.SetActive(false);
                continue;
            }

            items.Add(child);
        }

        if (hideAtStart)
        {
            foreach (Transform item in items)
                if (item != null) item.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (revealAtStart)
            Invoke(nameof(RevealAllNow), Mathf.Max(0f, revealAtStartDelay));
    }

    private void RevealAllNow()
    {
        for (int i = 0; i < items.Count; i++) RevealItem(items[i]);
    }

    private void Update()
    {
        if (revealAtStart) return;                  // start fade-in handles reveals
        if (revealed.Count == items.Count) return;  // everything already shown

        if (player == null)
        {
            if (Time.unscaledTime < nextPlayerSearch) return;
            nextPlayerSearch = Time.unscaledTime + 0.3f;
            player = ResolvePlayer();
            if (player == null) return;
        }

        float r2 = revealRadius * revealRadius;
        Vector3 p = player.position;

        for (int i = 0; i < items.Count; i++)
        {
            Transform item = items[i];
            if (item == null || revealed.Contains(item)) continue;

            Vector3 d = item.position - p;
            if (ignoreZ) d.z = 0f;
            if (d.sqrMagnitude <= r2) RevealItem(item);
        }
    }

    private void RevealItem(Transform item)
    {
        if (item == null || revealed.Contains(item)) return;
        item.gameObject.SetActive(true);
        revealed.Add(item);

        var shake = item.GetComponent<RevealPopShake>();
        if (shake != null) shake.Play();

        onItemRevealed?.Invoke(item);
    }

    private Transform ResolvePlayer()
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null) return tagged.transform;
            }
            catch (UnityException) { /* tag not defined in project — fall through */ }
        }

        if (playerNameFallbacks != null)
        {
            foreach (string n in playerNameFallbacks)
            {
                if (string.IsNullOrEmpty(n)) continue;
                GameObject named = GameObject.Find(n);
                if (named != null) return named.transform;
            }
        }
        return null;
    }

    /// <summary>
    /// Permanently keep <paramref name="item"/> hidden: hide it now and never reveal it (it is
    /// dropped from the reveal set so neither proximity nor start-reveal will show it again).
    /// Safe to call before or after Awake.
    /// </summary>
    public void ExcludePermanently(Transform item)
    {
        if (item == null) return;
        excluded.Add(item);
        items.Remove(item);
        revealed.Remove(item);
        item.gameObject.SetActive(false);
    }

    // ---- Public API for later "hide on quest complete" wiring -----------------------

    /// <summary>Hide a single item again (e.g. after its quest is finished).</summary>
    public void HideItem(Transform item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        revealed.Remove(item);
    }

    /// <summary>Hide every item again.</summary>
    public void HideAll()
    {
        foreach (Transform item in items)
            if (item != null) item.gameObject.SetActive(false);
        revealed.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        foreach (Transform child in transform)
            if (child != null) Gizmos.DrawWireSphere(child.position, revealRadius);
    }
}
