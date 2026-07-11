using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Scripted quest intro for the reference_forest map. The character walks the authored
/// <c>path</c> waypoints, stopping at each quest beat; quest actors + their "!" markers only
/// appear (fade in) when the character arrives, and are cleared by a click (fade out), after
/// which the character continues.
///
/// Beat 1 (wp_1): walk to wp_1, face wp_2, reveal <see cref="villager"/> + its "!".
///   Click the villager or "!" -> fade out -> walk to wp_2.
/// Beat 2 (wp_2): reveal the two crows, which fly back-and-forth across the route (alternating,
///   like a blockade) with a wing-flap; then reveal the crow "!". Click the "!" or a crow ->
///   crows + "!" fade out -> walk to wp_3.
///
/// Movement is done by translating the character body transform; the pack's CharacterAppearance
/// reads that position delta and plays the 4-direction walk automatically.
/// </summary>
public sealed class QuestPathSequence : MonoBehaviour
{
    [Header("Character")]
    [Tooltip("The character body that moves (spritesheet_7) - has Animator + CharacterAppearance.")]
    [SerializeField] private Transform body;
    [SerializeField] private Animator bodyAnimator;
    [Tooltip("The character's SpriteRenderer, faded out when the hero leaves at wp_12.")]
    [SerializeField] private SpriteRenderer bodySprite;
    [Tooltip("Old auto-walker on the body; disabled at runtime so it doesn't fight this sequence.")]
    [SerializeField] private MonoBehaviour autoWalkerToDisable;

    [Header("Waypoints")]
    [SerializeField] private Transform wp1;
    [SerializeField] private Transform wp2;
    [SerializeField] private Transform wp3;
    [SerializeField] private Transform wp4;
    [SerializeField] private Transform wp5;
    [SerializeField] private Transform wp6;
    [SerializeField] private Transform wp7;
    [SerializeField] private Transform wp8;
    [SerializeField] private Transform wp9;
    [SerializeField] private Transform wp10;
    [SerializeField] private Transform wp11;
    [SerializeField] private Transform wp12;

    [Header("Quest 1 - villager")]
    [SerializeField] private SpriteRenderer villager;
    [Tooltip("The exclamation-mark root to activate (its visible sprite + MarkerBob are on the child).")]
    [SerializeField] private GameObject villagerMarkerRoot;
    [SerializeField] private SpriteRenderer villagerMarkerSprite;

    [Header("Quest 2 - crows")]
    [SerializeField] private GameObject crow1Root;
    [SerializeField] private SpriteRenderer crow1Sprite;
    [SerializeField] private GameObject crow2Root;
    [SerializeField] private SpriteRenderer crow2Sprite;
    [Tooltip("The exclamation mark riding crow 1 (Mark (2)).")]
    [SerializeField] private GameObject crowMarkRoot;
    [SerializeField] private SpriteRenderer crowMarkSprite;
    [SerializeField] private string crowSpritesResourcePath = "Characters/Animals/birds/bird6_16x20";

    [Header("Tuning")]
    [SerializeField] private float startDelay = 0.3f;
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private float arriveDistance = 0.05f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float clickPadding = 0.35f;
    [Tooltip("Seconds a quest stays revealed at its point before it auto-completes (replaces the old tap-to-complete).")]
    [SerializeField] private float questAutoHold = 1.5f;
    [Tooltip("Animator 'speed' forced while walking so the walk clip plays even at low moveSpeed (state needs >0.01).")]
    [SerializeField] private float walkAnimSpeedParam = 1f;
    [Tooltip("Walk this fraction of the way from wp_1 toward the bear in one continuous run before the quest reveals (0 = stop at wp_1).")]
    [SerializeField] private float questApproachFraction = 0.6f;
    [Tooltip("Buzz the device (tablet) like a notification when a quest appears.")]
    [SerializeField] private bool enableVibration = true;

    [Header("Bear encounter scene link")]
    [Tooltip("Scene loaded when the hero reaches the first quest (the bear word-build encounter).")]
    [SerializeField] private string bearEncounterSceneName = "CutScene_bear";
    [Tooltip("Scene loaded when the hero reaches the second quest (the crow / ga encounter).")]
    [SerializeField] private string crowEncounterSceneName = "CutScene_ga";
    [Tooltip("Black fade-out duration before loading the bear encounter scene.")]
    [SerializeField] private float sceneExitCoverDuration = 1.0f;

    [Header("Crow flight")]
    [Tooltip("Crow 1 flies straight back and forth between these two points (wp_ga1 / wp_ga1 (1)).")]
    [SerializeField] private Transform crow1PointA;
    [SerializeField] private Transform crow1PointB;
    [Tooltip("Crow 2 flies straight back and forth between these two points (wp_ga2 / wp_ga2 (1)).")]
    [SerializeField] private Transform crow2PointA;
    [SerializeField] private Transform crow2PointB;
    [Tooltip("Flight speed (world units / sec).")]
    [SerializeField] private float crowFlySpeed = 2f;
    [SerializeField] private float crowFlapFps = 8f;
    [Tooltip("Flip the crow sprite to face its horizontal travel direction (assumes art faces right).")]
    [SerializeField] private bool crowFaceMovement = true;

    [Header("Quest 3 - sick fox")]
    [Tooltip("Normal walking foxes that hide when they 'get sick' (fox1_16x20_4 / fox1_16x20_7).")]
    [SerializeField] private GameObject walkFox1;
    [SerializeField] private GameObject walkFox2;
    [Tooltip("Sick fox actors shown in their place (fox_sick / fox_sick (1)).")]
    [SerializeField] private GameObject sickFox1Root;
    [SerializeField] private SpriteRenderer sickFox1Sprite;
    [SerializeField] private GameObject sickFox2Root;
    [SerializeField] private SpriteRenderer sickFox2Sprite;
    [Tooltip("The '!' on the sick fox (Mark (3)).")]
    [SerializeField] private GameObject sickFoxMarkRoot;
    [SerializeField] private SpriteRenderer sickFoxMarkSprite;
    [Tooltip("How far along wp_2 -> wp_3 (0..1) the walking foxes turn sick.")]
    [Range(0f, 1f)] [SerializeField] private float sickFoxTriggerFraction = 0.5f;

    [Header("Quest 4 - crows over crops")]
    [Tooltip("First crow that circles the crop field (bird6_16x20_5 (1)).")]
    [SerializeField] private GameObject quest4Crow1Root;
    [SerializeField] private SpriteRenderer quest4Crow1Sprite;
    [Tooltip("Second crow that circles the crop field (bird6_16x20_7 (1)).")]
    [SerializeField] private GameObject quest4Crow2Root;
    [SerializeField] private SpriteRenderer quest4Crow2Sprite;
    [Tooltip("The '!' over the blighted crops (L4_Trees/Mark (3)); its MarkerBob does the up/down bob.")]
    [SerializeField] private GameObject quest4MarkRoot;
    [SerializeField] private SpriteRenderer quest4MarkSprite;
    [Tooltip("Closed-loop rectangle the crows fly around, in order (frame/wp_5 .. frame/wp_5 (4)).")]
    [SerializeField] private Transform[] quest4FlightLoop;
    [Tooltip("Blighted crops shown before the quest (crop_*_6); hidden on completion.")]
    [SerializeField] private GameObject[] quest4CropsBefore;
    [Tooltip("Healthy crops revealed on completion (crop_*_5).")]
    [SerializeField] private GameObject[] quest4CropsAfter;
    [Tooltip("Extra '!' icons over the crop field that fade in/out with the quest-4 crows (Mark (4), Mark (5)).")]
    [SerializeField] private GameObject[] quest4ExtraMarkRoots;
    [SerializeField] private SpriteRenderer[] quest4ExtraMarkSprites;

    [Header("Quest 5 - sick pigs + nightfall")]
    [Tooltip("Pacing pigs that fall sick (pig_01_move_down...). Hidden when they turn sick.")]
    [SerializeField] private GameObject walkPig1;
    [SerializeField] private GameObject walkPig2;
    [Tooltip("Sick pig actors shown in their place (pig_sick / pig_sick (1)).")]
    [SerializeField] private GameObject sickPig1Root;
    [SerializeField] private SpriteRenderer sickPig1Sprite;
    [SerializeField] private GameObject sickPig2Root;
    [SerializeField] private SpriteRenderer sickPig2Sprite;
    [Tooltip("The '!' over the sick pig (pig_sick/Mark4); its MarkerBob does the up/down bob.")]
    [SerializeField] private GameObject pigMarkRoot;
    [SerializeField] private SpriteRenderer pigMarkSprite;
    [Tooltip("How far along wp_6 -> wp_7 (0..1) the pacing pigs turn sick.")]
    [Range(0f, 1f)] [SerializeField] private float sickPigTriggerFraction = 0.5f;
    [Tooltip("Night lighting (dark overlay + warm glow pools) faded 0->1 during the wp_8 -> wp_9 walk.")]
    [SerializeField] private NightLighting nightLighting;
    [SerializeField] private float nightFadeDuration = 6f;

    private bool crowPatrolActive;
    private bool quest4PatrolActive;
    private int facingLock = -1;
    private Dictionary<string, Sprite> crowSprites;

    private void Awake()
    {
        if (autoWalkerToDisable != null)
        {
            autoWalkerToDisable.enabled = false;
        }

        // Defensive: also kill any QuestAutoWalker left on the body.
        if (body != null)
        {
            QuestAutoWalker walker = body.GetComponent<QuestAutoWalker>();
            if (walker != null)
            {
                walker.enabled = false;
            }
        }

        HideAtStart(villager != null ? villager.gameObject : null, villager);
        HideAtStart(villagerMarkerRoot, villagerMarkerSprite);
        HideAtStart(crow1Root, crow1Sprite);
        HideAtStart(crow2Root, crow2Sprite);
        HideAtStart(crowMarkRoot, crowMarkSprite);

        // Sick foxes stay hidden until quest 3; the walking foxes keep pacing normally until then.
        HideAtStart(sickFox1Root, sickFox1Sprite);
        HideAtStart(sickFox2Root, sickFox2Sprite);
        HideAtStart(sickFoxMarkRoot, sickFoxMarkSprite);

        // Quest 4 crows + "!" stay hidden until the character walks past the crop field.
        HideAtStart(quest4Crow1Root, quest4Crow1Sprite);
        HideAtStart(quest4Crow2Root, quest4Crow2Sprite);
        HideAtStart(quest4MarkRoot, quest4MarkSprite);
        HideAtStartAll(quest4ExtraMarkRoots, quest4ExtraMarkSprites);

        // Quest 5 sick pigs + "!" stay hidden until the pacing pigs fall sick.
        HideAtStart(sickPig1Root, sickPig1Sprite);
        HideAtStart(sickPig2Root, sickPig2Sprite);
        HideAtStart(pigMarkRoot, pigMarkSprite);
        if (nightLighting != null)
        {
            nightLighting.SetNight(0f);
        }
    }

    private void Start()
    {
        StartCoroutine(RunSequence());
    }

    private void OnDisable()
    {
        GameAudio.StopForestFootsteps();
    }

    // Keep the character facing a fixed direction while it is stopped at a quest beat,
    // overriding CharacterAppearance (which only sets orientation while moving).
    private void LateUpdate()
    {
        if (facingLock >= 0 && bodyAnimator != null)
        {
            bodyAnimator.SetInteger("orientation", facingLock);
            bodyAnimator.SetFloat("speed", 0f);
        }
    }

    private static void HideAtStart(GameObject root, SpriteRenderer sprite)
    {
        if (sprite != null)
        {
            SetAlpha(sprite, 0f);
        }

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    // Make the quest-1 bear + "!" fully visible immediately (used on return so they can fade out).
    private void ShowQuest1Actor()
    {
        if (villager != null)
        {
            villager.gameObject.SetActive(true);
            SetAlpha(villager, 1f);
        }

        if (villagerMarkerRoot != null)
        {
            villagerMarkerRoot.SetActive(true);
        }

        if (villagerMarkerSprite != null)
        {
            SetAlpha(villagerMarkerSprite, 1f);
        }
    }

    // Re-show the two crows fully visible (used on return from the ga encounter so they can fade off).
    private void ShowResumeCrows()
    {
        if (crow1Root != null) crow1Root.SetActive(true);
        if (crow1Sprite != null) SetAlpha(crow1Sprite, 1f);
        if (crow2Root != null) crow2Root.SetActive(true);
        if (crow2Sprite != null) SetAlpha(crow2Sprite, 1f);
    }

    private IEnumerator RunSequence()
    {
        // Returned from the bear encounter? Pre-place the hero at wp_1 and re-show the bear + "!"
        // (they were left visible when we entered) BEFORE the black reveal finishes, so they are
        // already on screen as it fades in - matching how the player left them.
        bool resuming2 = BearEncounterFlow.ResumeAtBeat2; // back from the bear -> do the crow (ga) quest
        bool resuming3 = BearEncounterFlow.ResumeAtBeat3; // back from the crow (ga) -> skip to Beat 3 (foxes)
        if (resuming2)
        {
            if (wp1 != null && body != null)
            {
                Vector3 resumePos = wp1.position;
                resumePos.z = body.position.z;
                body.position = resumePos;
            }
            if (wp1 != null && wp2 != null)
            {
                facingLock = OrientationFor(wp2.position - wp1.position);
            }
            ShowQuest1Actor();
        }
        else if (resuming3)
        {
            // Land at the crow quest spot (wp_2) facing the next quest; re-show the crows so they
            // can fly off as the black reveal fades in — matching how the bear return re-shows the bear.
            if (wp2 != null && body != null)
            {
                Vector3 resumePos = wp2.position;
                resumePos.z = body.position.z;
                body.position = resumePos;
            }
            if (wp2 != null && wp3 != null)
            {
                facingLock = OrientationFor(wp3.position - wp2.position);
            }
            ShowResumeCrows();
        }

        // Wait for the scene's black fade-in (reveal) to finish before any gameplay begins, so the
        // hero never moves while the screen is still covered.
        yield return new WaitUntil(() => SceneFadeController.RevealComplete);

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (resuming2)
        {
            // The bear quest was completed inside word_build_paa_polished: the bear + "!" fade out
            // now, the hero stays where it left off (wp_1), then the remaining quests run unchanged.
            BearEncounterFlow.ResumeAtBeat2 = false;
            GameAudio.PlayAfterQuest();
            yield return new WaitForSeconds(questAutoHold);
            yield return FadeOutAndHide(new[] { villager, villagerMarkerSprite },
                new[] { villager != null ? villager.gameObject : null, villagerMarkerRoot });
        }
        else if (resuming3)
        {
            // The crow (ga) quest is done: the freed crows fade off, then Beat 3 (foxes) continues.
            BearEncounterFlow.ResumeAtBeat3 = false;
            GameAudio.PlayAfterQuest();
            yield return new WaitForSeconds(questAutoHold);
            crowPatrolActive = false;
            yield return FadeOutAndHide(new[] { crow1Sprite, crow2Sprite },
                new[] { crow1Root, crow2Root });
        }
        else
        {
            // ---- Beat 1: walk to wp_1 — the bear quest pops the moment the hero arrives
            //      there, and fades in WHILE the hero keeps running toward it (no stop).
            if (wp1 != null)
            {
                yield return MoveTo(wp1.position);
            }

            // Quest appears now (at wp_1); the reveal runs in the background during the walk.
            Vibrate();
            Coroutine questReveal = StartCoroutine(
                Reveal(villagerMarkerRoot, villagerMarkerSprite, villager != null ? villager.gameObject : null, villager));

            if (villager != null && body != null && questApproachFraction > 0f)
            {
                // No facingLock and no pause here — CharacterRunDirection steers the turn
                // while the hero keeps moving, so the direction change reads as one motion.
                facingLock = -1;
                Vector3 bearPos = villager.transform.position;
                Vector3 approach = Vector3.Lerp(body.position, bearPos, Mathf.Clamp01(questApproachFraction));
                approach.z = body.position.z;
                yield return MoveTo(approach);
                facingLock = OrientationFor(bearPos - body.position);   // face the bear on arrival
            }
            else if (wp1 != null && wp2 != null)
            {
                facingLock = OrientationFor(wp2.position - wp1.position);
            }

            yield return questReveal;   // make sure the "!" is fully in before the beat
            yield return new WaitForSeconds(questAutoHold);   // brief beat looking at the bear + "!"

            // The bear stays visible. Fade to black and enter the word-build (bear) encounter;
            // on a successful build it loads us back and we resume at Beat 2 (bear now gone).
            BearEncounterFlow.ReturnToForest = true;
            yield return PlayQuestEnterThenCover();
            SceneManager.LoadScene(bearEncounterSceneName);
            yield break;
        }

        // ---- Beat 2: walk to wp_2, crows fly in and block, reveal crow "!", then enter the crow
        //      (ga) encounter — the SAME fade + click-quest hand-off Beat 1 uses for the bear.
        //      Skipped when resuming3 (we already came back FROM the ga encounter).
        if (!resuming3)
        {
            facingLock = -1;
            if (wp2 != null)
            {
                yield return MoveTo(wp2.position);
            }

            if (wp2 != null && wp3 != null)
            {
                facingLock = OrientationFor(wp3.position - wp2.position);
            }

            // Crows fade in at their placed spots while they begin their straight back-and-forth flight.
            ActivateForFade(crow1Root, crow1Sprite);
            ActivateForFade(crow2Root, crow2Sprite);
            StartCrowFlight();
            yield return Fade(new[] { crow1Sprite, crow2Sprite }, 0f, 1f);

            // Now the "!" appears on the crow.
            Vibrate();
            yield return Reveal(crowMarkRoot, crowMarkSprite, null, null);

            yield return new WaitForSeconds(questAutoHold);   // auto-completes on arrival (no tap)

            // The crows stay visible; fade to black and enter CutScene_ga. On success the crow
            // scene sets ResumeAtBeat3 and loads us back here, resuming past the crow quest.
            crowPatrolActive = false;
            yield return PlayQuestEnterThenCover();
            SceneManager.LoadScene(crowEncounterSceneName);
            yield break;
        }

        // ---- Beat 3: walk wp_2 -> wp_3; partway, the walking foxes turn sick ----
        facingLock = -1;
        if (wp2 != null && wp3 != null)
        {
            Vector3 trigger = Vector3.Lerp(wp2.position, wp3.position, Mathf.Clamp01(sickFoxTriggerFraction));
            yield return MoveTo(trigger);

            // The walking foxes become sick: hide them, fade in the sick foxes + "!".
            if (walkFox1 != null) walkFox1.SetActive(false);
            if (walkFox2 != null) walkFox2.SetActive(false);
            Vibrate();
            ActivateForFade(sickFox1Root, sickFox1Sprite);
            ActivateForFade(sickFox2Root, sickFox2Sprite);
            ActivateForFade(sickFoxMarkRoot, sickFoxMarkSprite);
            yield return Fade(new[] { sickFox1Sprite, sickFox2Sprite, sickFoxMarkSprite }, 0f, 1f);

            yield return MoveTo(wp3.position);
        }
        else if (wp3 != null)
        {
            yield return MoveTo(wp3.position);
        }

        // Reach wp_3: turn to face wp_4, then walk to wp_4.
        if (wp3 != null && wp4 != null)
        {
            facingLock = OrientationFor(wp4.position - wp3.position);
            yield return new WaitForSeconds(0.15f);   // brief turn before moving
            facingLock = -1;
            yield return MoveTo(wp4.position);
            facingLock = OrientationFor(wp4.position - wp3.position);   // hold facing the stable
        }

        // At wp_4 the player can press the "!" to do the quest.
        if (sickFoxMarkSprite != null)
        {
            yield return new WaitForSeconds(questAutoHold);   // auto-completes on arrival (no tap)
            yield return FadeOutAndHide(new[] { sickFox1Sprite, sickFox2Sprite, sickFoxMarkSprite },
                new[] { sickFox1Root, sickFox2Root, sickFoxMarkRoot });

            // The foxes return to walking normally as before.
            if (walkFox1 != null) walkFox1.SetActive(true);
            if (walkFox2 != null) walkFox2.SetActive(true);
        }

        // ---- Beat 4: wp_4 -> wp_5, turn to face wp_6, then walk down past the crow-blighted crops ----
        facingLock = -1;
        if (wp5 != null)
        {
            yield return MoveTo(wp5.position);
        }

        // At wp_5, turn to face wp_6 (straight down) and hold a beat before continuing.
        if (wp5 != null && wp6 != null)
        {
            facingLock = OrientationFor(wp6.position - wp5.position);
            yield return new WaitForSeconds(0.15f);
        }

        // Crows + "!" fade in as the walk to wp_6 begins; the crows circle the crop field on the frame loop.
        // The extra field "!" icons (Mark (4)/(5)) fade in with the crows and fade out when they leave.
        ActivateForFade(quest4Crow1Root, quest4Crow1Sprite);
        ActivateForFade(quest4Crow2Root, quest4Crow2Sprite);
        ActivateForFade(quest4MarkRoot, quest4MarkSprite);
        ActivateForFadeAll(quest4ExtraMarkRoots, quest4ExtraMarkSprites);
        StartQuest4Flight();
        Vibrate();
        StartCoroutine(Fade(new[] { quest4Crow1Sprite, quest4Crow2Sprite, quest4MarkSprite }, 0f, 1f));
        StartCoroutine(Fade(quest4ExtraMarkSprites, 0f, 1f));

        // Keep walking straight down to wp_6 while the fade + flight play.
        facingLock = -1;
        if (wp6 != null)
        {
            yield return MoveTo(wp6.position);
        }

        // Hold facing down while waiting at wp_6 for the player to press the quest.
        if (wp5 != null && wp6 != null)
        {
            facingLock = OrientationFor(wp6.position - wp5.position);
        }

        // Complete by clicking a crow, the dedicated "!" (if present), or one of the field "!" icons.
        yield return new WaitForSeconds(questAutoHold);   // auto-completes on arrival (no tap)
        quest4PatrolActive = false;
        StartCoroutine(FadeOutAndHide(quest4ExtraMarkSprites, quest4ExtraMarkRoots));   // extra "!" leave with the crows
        yield return FadeOutAndHide(new[] { quest4Crow1Sprite, quest4Crow2Sprite, quest4MarkSprite },
            new[] { quest4Crow1Root, quest4Crow2Root, quest4MarkRoot });

        // The blighted crops turn healthy: hide the _6 set, reveal the _5 set.
        SwapCrops();
        facingLock = -1;

        // ---- Beat 5: wp_6 -> wp_7 (pigs fall sick), wp_7 -> wp_8, press to trigger nightfall ----
        if (wp6 != null && wp7 != null)
        {
            facingLock = OrientationFor(wp7.position - wp6.position);   // turn to face wp_7 (up)
            yield return new WaitForSeconds(0.15f);
        }

        facingLock = -1;
        if (wp6 != null && wp7 != null)
        {
            Vector3 trigger = Vector3.Lerp(wp6.position, wp7.position, Mathf.Clamp01(sickPigTriggerFraction));
            yield return MoveTo(trigger);

            // The pacing pigs fall sick: hide them, fade in the sick pigs + "!".
            if (walkPig1 != null) walkPig1.SetActive(false);
            if (walkPig2 != null) walkPig2.SetActive(false);
            Vibrate();
            ActivateForFade(sickPig1Root, sickPig1Sprite);
            ActivateForFade(sickPig2Root, sickPig2Sprite);
            ActivateForFade(pigMarkRoot, pigMarkSprite);
            yield return Fade(new[] { sickPig1Sprite, sickPig2Sprite, pigMarkSprite }, 0f, 1f);

            yield return MoveTo(wp7.position);
        }
        else if (wp7 != null)
        {
            yield return MoveTo(wp7.position);
        }

        // Reach wp_7: turn to face wp_8 (right), then walk to wp_8.
        if (wp7 != null && wp8 != null)
        {
            facingLock = OrientationFor(wp8.position - wp7.position);
            yield return new WaitForSeconds(0.15f);   // brief turn before moving
            facingLock = -1;
            yield return MoveTo(wp8.position);
            facingLock = OrientationFor(wp8.position - wp7.position);   // hold facing wp_8
        }

        // Only at wp_8 can the player press the "!" to trigger the quest.
        if (pigMarkSprite != null)
        {
            yield return new WaitForSeconds(questAutoHold);   // auto-completes on arrival (no tap)
            yield return FadeOutAndHide(new[] { pigMarkSprite, sickPig1Sprite, sickPig2Sprite },
                new[] { pigMarkRoot, sickPig1Root, sickPig2Root });

            // The pigs return to walking normally, as before.
            if (walkPig1 != null) walkPig1.SetActive(true);
            if (walkPig2 != null) walkPig2.SetActive(true);

            // ---- Beat 6: wp_8 -> wp_9 with day->night, then wp_9..wp_12, then the hero leaves ----
            // Face wp_9, then walk there while the night overlay fades in (day -> night).
            if (wp8 != null && wp9 != null)
            {
                facingLock = OrientationFor(wp9.position - wp8.position);
                yield return new WaitForSeconds(0.15f);
            }

            facingLock = -1;
            StartCoroutine(FadeNight());
            if (wp9 != null)
            {
                yield return MoveTo(wp9.position);
            }

            // Continue wp_9 -> wp_10 -> wp_11 -> wp_12, facing each leg before moving.
            yield return WalkFacing(wp9, wp10);
            yield return WalkFacing(wp10, wp11);
            yield return WalkFacing(wp11, wp12);

            // Reached wp_12: the hero fades out and disappears.
            yield return FadeOutBody();
        }
    }

    // Let the quest-enter cue finish while the encounter is still visible, then cover the screen.
    // The duration comes from the assigned clip, so changing click-quest.mp3 stays in sync.
    private IEnumerator PlayQuestEnterThenCover()
    {
        GameAudio.PlayQuestEnter();
        float cueDuration = GameAudio.QuestEnterDuration;
        if (cueDuration > 0f)
        {
            yield return new WaitForSeconds(cueDuration);
        }

        yield return SceneFadeController.Cover(sceneExitCoverDuration);
    }

    // Turn to face 'to' (relative to 'from'), hold briefly, then walk to 'to'.
    private IEnumerator WalkFacing(Transform from, Transform to)
    {
        if (to == null)
        {
            yield break;
        }

        Transform reference = from != null ? from : body;
        if (reference != null)
        {
            facingLock = OrientationFor(to.position - reference.position);
            yield return new WaitForSeconds(0.15f);
        }

        facingLock = -1;
        yield return MoveTo(to.position);
    }

    private IEnumerator FadeOutBody()
    {
        facingLock = -1;

        SpriteRenderer sprite = bodySprite != null
            ? bodySprite
            : (body != null ? body.GetComponent<SpriteRenderer>() : null);
        if (sprite != null)
        {
            yield return Fade(new[] { sprite }, 1f, 0f);
        }

        if (body != null)
        {
            body.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeNight()
    {
        if (nightLighting == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, nightFadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            nightLighting.SetNight(SmootherStep(elapsed / duration));
            yield return null;
        }

        nightLighting.SetNight(1f);
    }

    // ----------------------------------------------------------------- movement

    private IEnumerator MoveTo(Vector3 targetWorld)
    {
        if (body == null)
        {
            yield break;
        }

        targetWorld.z = body.position.z;
        bool isWalking = Vector3.Distance(body.position, targetWorld) > arriveDistance;
        if (isWalking)
        {
            GameAudio.StartForestFootsteps();
        }

        while (true)
        {
            Vector3 current = body.position;
            Vector3 delta = targetWorld - current;
            float dist = delta.magnitude;
            if (dist <= arriveDistance)
            {
                break;
            }

            float step = Mathf.Min(moveSpeed * Time.deltaTime, dist);
            body.position = current + delta / dist * step;

            // Force the walk clip on: at low moveSpeed the per-frame delta CharacterAppearance
            // measures is below the controller's 0.01 walk threshold, so set 'speed' ourselves
            // here in Update (after CharacterAppearance's FixedUpdate, before the Animator evaluates).
            if (bodyAnimator != null)
            {
                bodyAnimator.SetFloat("speed", walkAnimSpeedParam);
            }

            yield return null;
        }

        body.position = targetWorld;
        if (isWalking)
        {
            GameAudio.StopForestFootsteps();
        }
    }

    // Matches SuperRetroMainBundle.CharacterAppearance: 0 up / 2 left / 4 down / 6 right.
    private static int OrientationFor(Vector3 dir)
    {
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        if (ax >= ay)
        {
            return dir.x >= 0f ? 6 : 2;
        }

        return dir.y >= 0f ? 0 : 4;
    }

    // Short "notification" buzz on the handheld device (tablet/phone); no-op on desktop.
    private void Vibrate()
    {
        if (!enableVibration)
        {
            return;
        }

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    // ----------------------------------------------------------------- reveal / fade

    private IEnumerator Reveal(GameObject rootA, SpriteRenderer spriteA, GameObject rootB, SpriteRenderer spriteB)
    {
        ActivateForFade(rootA, spriteA);
        ActivateForFade(rootB, spriteB);
        yield return Fade(new[] { spriteA, spriteB }, 0f, 1f);
    }

    private static void ActivateForFade(GameObject root, SpriteRenderer sprite)
    {
        if (sprite != null)
        {
            SetAlpha(sprite, 0f);
        }

        if (root != null)
        {
            root.SetActive(true);
        }
    }

    private static void HideAtStartAll(GameObject[] roots, SpriteRenderer[] sprites)
    {
        int count = Mathf.Max(roots != null ? roots.Length : 0, sprites != null ? sprites.Length : 0);
        for (int i = 0; i < count; i++)
        {
            HideAtStart(
                roots != null && i < roots.Length ? roots[i] : null,
                sprites != null && i < sprites.Length ? sprites[i] : null);
        }
    }

    private static void ActivateForFadeAll(GameObject[] roots, SpriteRenderer[] sprites)
    {
        int count = Mathf.Max(roots != null ? roots.Length : 0, sprites != null ? sprites.Length : 0);
        for (int i = 0; i < count; i++)
        {
            ActivateForFade(
                roots != null && i < roots.Length ? roots[i] : null,
                sprites != null && i < sprites.Length ? sprites[i] : null);
        }
    }

    private IEnumerator FadeOutAndHide(SpriteRenderer[] sprites, GameObject[] roots)
    {
        yield return Fade(sprites, 1f, 0f);
        if (roots != null)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    roots[i].SetActive(false);
                }
            }
        }
    }

    private IEnumerator Fade(SpriteRenderer[] sprites, float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float a = Mathf.Lerp(from, to, SmootherStep(elapsed / duration));
            SetAlpha(sprites, a);
            yield return null;
        }

        SetAlpha(sprites, to);
    }

    private static void SetAlpha(SpriteRenderer[] sprites, float a)
    {
        if (sprites == null)
        {
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            SetAlpha(sprites[i], a);
        }
    }

    private static void SetAlpha(SpriteRenderer sprite, float a)
    {
        if (sprite == null)
        {
            return;
        }

        Color color = sprite.color;
        color.a = a;
        sprite.color = color;
    }

    // ----------------------------------------------------------------- click

    private IEnumerator WaitForClick(params SpriteRenderer[] targets)
    {
        while (true)
        {
            if (TryGetClickWorldPosition(out Vector3 worldPosition) && HitsAny(worldPosition, targets))
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool HitsAny(Vector3 worldPosition, SpriteRenderer[] targets)
    {
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        for (int i = 0; i < targets.Length; i++)
        {
            SpriteRenderer sprite = targets[i];
            if (sprite == null || !sprite.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = sprite.bounds;
            bounds.Expand(clickPadding);
            if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetClickWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Vector2 screenPosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else
        {
            return false;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
        }
        else
        {
            return false;
        }
#endif

        worldPosition = camera.ScreenToWorldPoint(screenPosition);
        return true;
    }

    // ----------------------------------------------------------------- crow flight

    private void StartCrowFlight()
    {
        crowPatrolActive = true;
        if (crowSprites == null)
        {
            LoadCrowSprites();
        }

        if (crow1Sprite != null)
        {
            StartCoroutine(CrowFly(crow1Sprite.transform, crow1Sprite, crow1PointA, crow1PointB));
        }

        if (crow2Sprite != null)
        {
            StartCoroutine(CrowFly(crow2Sprite.transform, crow2Sprite, crow2PointA, crow2PointB));
        }
    }

    // Straight, constant-speed back-and-forth flight between two points (no sway/bob).
    private IEnumerator CrowFly(Transform crow, SpriteRenderer sprite, Transform pointA, Transform pointB)
    {
        if (crow == null || pointA == null || pointB == null)
        {
            yield break;
        }

        Sprite[] flap = GetFlapFrames(sprite);
        float flapInterval = crowFlapFps > 0f ? 1f / crowFlapFps : 0f;
        float flapClock = 0f;
        int flapIndex = 0;
        float speed = Mathf.Max(0.01f, crowFlySpeed);
        Transform target = pointB;

        while (crowPatrolActive)
        {
            Vector3 current = crow.position;
            Vector3 destination = target.position;
            destination.z = current.z;

            Vector3 delta = destination - current;
            float distance = delta.magnitude;
            float step = speed * Time.deltaTime;

            if (distance <= step || distance < 0.0001f)
            {
                crow.position = destination;
                target = target == pointA ? pointB : pointA;   // ping-pong between the two points
            }
            else
            {
                crow.position = current + delta / distance * step;
                if (crowFaceMovement && sprite != null && Mathf.Abs(delta.x) > 0.0001f)
                {
                    sprite.flipX = delta.x < 0f;
                }
            }

            if (flap != null && flap.Length > 1 && flapInterval > 0f)
            {
                flapClock += Time.deltaTime;
                if (flapClock >= flapInterval)
                {
                    flapClock -= flapInterval;
                    flapIndex = (flapIndex + 1) % flap.Length;
                    sprite.sprite = flap[flapIndex];
                }
            }

            yield return null;
        }
    }

    // ----------------------------------------------------------------- quest 4 crow loop + crops

    private void StartQuest4Flight()
    {
        quest4PatrolActive = true;
        if (crowSprites == null)
        {
            LoadCrowSprites();
        }

        Transform[] loop = quest4FlightLoop;
        if (loop == null || loop.Length < 2)
        {
            return;
        }

        if (quest4Crow1Sprite != null)
        {
            StartCoroutine(CrowFlyLoop(quest4Crow1Sprite.transform, quest4Crow1Sprite, loop, 0));
        }

        if (quest4Crow2Sprite != null)
        {
            // Start the second crow on the opposite side so the two chase each other around the square.
            StartCoroutine(CrowFlyLoop(quest4Crow2Sprite.transform, quest4Crow2Sprite, loop, loop.Length / 2));
        }
    }

    // Constant-speed flight around a closed loop of waypoints (the rectangle 'frame' around the field).
    private IEnumerator CrowFlyLoop(Transform crow, SpriteRenderer sprite, Transform[] loop, int startIndex)
    {
        if (crow == null || loop == null || loop.Length < 2)
        {
            yield break;
        }

        Sprite[] flap = GetFlapFrames(sprite);
        float flapInterval = crowFlapFps > 0f ? 1f / crowFlapFps : 0f;
        float flapClock = 0f;
        int flapIndex = 0;
        float speed = Mathf.Max(0.01f, crowFlySpeed);
        int targetIndex = ((startIndex % loop.Length) + loop.Length) % loop.Length;

        while (quest4PatrolActive)
        {
            Transform target = loop[targetIndex];
            if (target == null)
            {
                targetIndex = (targetIndex + 1) % loop.Length;
                yield return null;
                continue;
            }

            Vector3 current = crow.position;
            Vector3 destination = target.position;
            destination.z = current.z;

            Vector3 delta = destination - current;
            float distance = delta.magnitude;
            float step = speed * Time.deltaTime;

            if (distance <= step || distance < 0.0001f)
            {
                crow.position = destination;
                targetIndex = (targetIndex + 1) % loop.Length;   // advance to the next corner, wrapping around
            }
            else
            {
                crow.position = current + delta / distance * step;
                if (crowFaceMovement && sprite != null && Mathf.Abs(delta.x) > 0.0001f)
                {
                    sprite.flipX = delta.x < 0f;
                }
            }

            if (flap != null && flap.Length > 1 && flapInterval > 0f)
            {
                flapClock += Time.deltaTime;
                if (flapClock >= flapInterval)
                {
                    flapClock -= flapInterval;
                    flapIndex = (flapIndex + 1) % flap.Length;
                    sprite.sprite = flap[flapIndex];
                }
            }

            yield return null;
        }
    }

    private void SwapCrops()
    {
        SetActiveAll(quest4CropsBefore, false);
        SetActiveAll(quest4CropsAfter, true);
    }

    private static void SetActiveAll(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                objects[i].SetActive(active);
            }
        }
    }

    private void LoadCrowSprites()
    {
        crowSprites = new Dictionary<string, Sprite>();
        if (string.IsNullOrEmpty(crowSpritesResourcePath))
        {
            return;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>(crowSpritesResourcePath);
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null)
            {
                crowSprites[loaded[i].name] = loaded[i];
            }
        }
    }

    // Flap = the 3 frames of the crow's own row in the sheet (frames are laid out 3-per-row).
    private Sprite[] GetFlapFrames(SpriteRenderer sprite)
    {
        if (sprite == null || sprite.sprite == null || crowSprites == null || crowSprites.Count == 0)
        {
            return null;
        }

        string name = sprite.sprite.name;
        int underscore = name.LastIndexOf('_');
        if (underscore < 0 || !int.TryParse(name.Substring(underscore + 1), out int index))
        {
            return null;
        }

        string prefix = name.Substring(0, underscore + 1);
        int rowStart = index / 3 * 3;
        List<Sprite> frames = new List<Sprite>(3);
        for (int k = 0; k < 3; k++)
        {
            if (crowSprites.TryGetValue(prefix + (rowStart + k), out Sprite frame))
            {
                frames.Add(frame);
            }
        }

        return frames.Count > 0 ? frames.ToArray() : null;
    }

    private static float SmootherStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
}
