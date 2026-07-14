using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// The 3-slot wooden star board shown beside a night-redoable quest actor: panel_star.png as the
/// base, a gold star.png stamped on each earned slot, with an effect_star.png sparkle behind it.
/// World-space, built entirely at runtime (the forest has no UI canvas for this). Renders ABOVE
/// NightLighting's NightDark overlay (sortingOrder 32000) so it is never swallowed into the dark —
/// the quest's own light pool (NightLighting.SetDynamicLight) is what lights the actor beneath it.
public static class QuestStarBadge
{
    private const int PanelOrder = 32100;
    private const int SparkleOrder = 32101;
    private const int StarOrder = 32102;

    // Sized in WORLD UNITS, computed from each sprite's own bounds at runtime — no pixel-perfect
    // guesswork needed if the art changes. Tune these three numbers in-editor by eye.
    private const float PanelWidthWorld = 2.1f;
    private const float StarWidthFraction = 0.16f;    // of panel width
    private const float SparkleWidthFraction = 0.22f; // of panel width

    // Slot centres as fractions of the panel's own width/height (0,0 = bottom-left corner of the
    // sprite, 1,1 = top-right). The art is a symmetric 3-slot plank; the middle slot fraction is
    // exact by symmetry, the outer two are eyeballed off the reference image — nudge these if the
    // stars don't land inside the embossed circles.
    private static readonly Vector2[] SlotFractions =
    {
        new Vector2(0.26f, 0.47f),
        new Vector2(0.50f, 0.47f),
        new Vector2(0.74f, 0.47f),
    };

    private static readonly Dictionary<string, GameObject> Active = new Dictionary<string, GameObject>();

    /// Builds and fades in the board for `questId` beside `anchor`. Safe to call once per quest —
    /// a second call while one is already up for that id is a no-op.
    public static IEnumerator Spawn(string questId, Transform anchor, Vector3 offset, int filledStars, float fadeDuration)
    {
        if (string.IsNullOrEmpty(questId) || anchor == null) yield break;
        if (Active.ContainsKey(questId)) yield break;

        Sprite panelSprite = Resources.Load<Sprite>("Stars/panel_star");
        Sprite starSprite = Resources.Load<Sprite>("Stars/star");
        Sprite sparkleSprite = Resources.Load<Sprite>("Stars/effect_star");
        if (panelSprite == null)
        {
            Debug.LogWarning("[QuestStarBadge] Stars/panel_star not found — no board spawned");
            yield break;
        }

        GameObject root = new GameObject($"QuestStarBadge_{questId}");
        root.transform.position = anchor.position + offset;
        Active[questId] = root;

        GameObject panelGo = new GameObject("panel");
        panelGo.transform.SetParent(root.transform, false);
        SpriteRenderer panelRenderer = panelGo.AddComponent<SpriteRenderer>();
        panelRenderer.sprite = panelSprite;
        panelRenderer.sortingOrder = PanelOrder;

        float panelRawWidth = Mathf.Max(0.0001f, panelSprite.bounds.size.x);
        float panelScale = PanelWidthWorld / panelRawWidth;
        panelGo.transform.localScale = Vector3.one * panelScale;
        float panelWorldWidth = panelSprite.bounds.size.x * panelScale;
        float panelWorldHeight = panelSprite.bounds.size.y * panelScale;

        var sprites = new List<SpriteRenderer> { panelRenderer };

        for (int i = 0; i < SlotFractions.Length && i < filledStars; i++)
        {
            Vector2 frac = SlotFractions[i];
            Vector3 slotLocalPos = new Vector3(
                (frac.x - 0.5f) * panelWorldWidth,
                (frac.y - 0.5f) * panelWorldHeight,
                0f);

            if (sparkleSprite != null)
            {
                GameObject sparkleGo = new GameObject($"sparkle_{i}");
                sparkleGo.transform.SetParent(root.transform, false);
                sparkleGo.transform.localPosition = slotLocalPos;
                SpriteRenderer sparkleRenderer = sparkleGo.AddComponent<SpriteRenderer>();
                sparkleRenderer.sprite = sparkleSprite;
                sparkleRenderer.sortingOrder = SparkleOrder;
                float sparkleWidth = PanelWidthWorld * SparkleWidthFraction;
                sparkleGo.transform.localScale = Vector3.one *
                    (sparkleWidth / Mathf.Max(0.0001f, sparkleSprite.bounds.size.x));
                sprites.Add(sparkleRenderer);
            }

            if (starSprite != null)
            {
                GameObject starGo = new GameObject($"star_{i}");
                starGo.transform.SetParent(root.transform, false);
                starGo.transform.localPosition = slotLocalPos;
                SpriteRenderer starRenderer = starGo.AddComponent<SpriteRenderer>();
                starRenderer.sprite = starSprite;
                starRenderer.sortingOrder = StarOrder;
                float starWidth = PanelWidthWorld * StarWidthFraction;
                starGo.transform.localScale = Vector3.one *
                    (starWidth / Mathf.Max(0.0001f, starSprite.bounds.size.x));
                sprites.Add(starRenderer);
            }
        }

        SetAlpha(sprites, 0f);
        float duration = Mathf.Max(0.01f, fadeDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAlpha(sprites, Mathf.Clamp01(t / duration));
            yield return null;
        }
        SetAlpha(sprites, 1f);
    }

    /// Fades the board for `questId` out and destroys it. No-op if none is up.
    public static IEnumerator FadeOut(string questId, float duration)
    {
        if (!Active.TryGetValue(questId, out GameObject root) || root == null)
        {
            Active.Remove(questId);
            yield break;
        }

        var sprites = new List<SpriteRenderer>(root.GetComponentsInChildren<SpriteRenderer>(true));
        float d = Mathf.Max(0.01f, duration);
        for (float t = 0f; t < d; t += Time.deltaTime)
        {
            SetAlpha(sprites, 1f - Mathf.Clamp01(t / d));
            yield return null;
        }

        Active.Remove(questId);
        Object.Destroy(root);
    }

    /// Destroys every board immediately (no fade) — used before a scene load.
    public static void HideAll()
    {
        foreach (KeyValuePair<string, GameObject> kvp in Active)
        {
            if (kvp.Value != null) Object.Destroy(kvp.Value);
        }
        Active.Clear();
    }

    private static void SetAlpha(List<SpriteRenderer> sprites, float a)
    {
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null) continue;
            Color c = sprites[i].color;
            c.a = a;
            sprites[i].color = c;
        }
    }
}
