using System.Collections;
using UnityEngine;

/// <summary>
/// Starts the word-assembly reveal in the minimal Boss scene without the legacy forest/boss intro.
/// </summary>
public sealed class BossWordAssemblyStarter : MonoBehaviour
{
    [SerializeField] private MagicStonePuzzleController puzzle;
    [SerializeField, Min(0f)] private float revealDelay = 0.05f;

    private Coroutine revealRoutine;

    private void Awake()
    {
        MagicStonePuzzleController[] controllers = GetComponentsInChildren<MagicStonePuzzleController>(true);
        if (puzzle == null)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].gameObject.name == "magic_stone")
                {
                    puzzle = controllers[i];
                    break;
                }
            }
        }

        // The book contains copied controllers for its result-page variants.  Only magic_stone
        // owns the interactive glyphs, so leaving the copies active can capture the puzzle flow.
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i] != puzzle)
            {
                controllers[i].enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        revealRoutine = StartCoroutine(RevealRoutine());
    }

    private void OnDisable()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }
    }

    private IEnumerator RevealRoutine()
    {
        // Let the prefab complete Awake/layout before capturing the hidden stone positions.
        yield return null;

        if (revealDelay > 0f)
        {
            yield return new WaitForSeconds(revealDelay);
        }

        if (puzzle == null)
        {
            Debug.LogError("[BossWordAssemblyStarter] MagicStonePuzzleController was not found in WordAssembly.", this);
            yield break;
        }

        puzzle.PrepareForIntro();
        yield return puzzle.PlayIntroReveal();
        revealRoutine = null;
    }
}
