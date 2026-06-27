using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class ForestAnimationSetup
{
    const string AnimDir = "Assets/Animations/forest";
    const string PigBase = "Assets/Gif/Super_Retro_Collection/Resources/Characters/Animals/pigs/pig_01/move/";

    // Pig walk frame rate. 4 fps => 4 frames = 1.0s loop (calm, natural idle). Lower = slower.
    const float PigFps = 4f;

    static void EnsureDir()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder("Assets/Animations", "forest");
    }

    [MenuItem("Tools/Forest/Animate Pigs")]
    public static string AnimatePigs()
    {
        var log = new StringBuilder();
        EnsureDir();
        string[] dirs = new[] { "down", "left", "right" };
        var controllerByDir = new Dictionary<string, RuntimeAnimatorController>();

        foreach (var d in dirs)
        {
            string sheet = PigBase + "pig_01_move_" + d + "_32x32_4frames.png";
            var all = AssetDatabase.LoadAllAssetsAtPath(sheet);
            var sprites = all.OfType<Sprite>().OrderBy(s =>
            {
                var parts = s.name.Split('_'); int n; return int.TryParse(parts.Last(), out n) ? n : 0;
            }).ToArray();
            if (sprites.Length == 0) { log.AppendLine("NO SPRITES for " + sheet); continue; }

            var clip = new AnimationClip { frameRate = PigFps };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / PigFps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string clipPath = AnimDir + "/pig_walk_" + d + ".anim";
            AssetDatabase.CreateAsset(clip, clipPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(AnimDir + "/pig_walk_" + d + ".controller", clip);
            controllerByDir[d] = ctrl;
            log.AppendLine("Created clip+controller '" + d + "' (" + sprites.Length + " frames)");
        }

        var animalRoot = GameObject.Find("Objects/animal");
        int assigned = 0;
        if (animalRoot == null) { log.AppendLine("ERROR: Objects/animal not found"); }
        else
        {
            foreach (Transform t in animalRoot.transform)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                string dir = "down";
                if (sr != null && sr.sprite != null)
                {
                    string p = AssetDatabase.GetAssetPath(sr.sprite);
                    if (p.Contains("_left_")) dir = "left";
                    else if (p.Contains("_right_")) dir = "right";
                    else dir = "down";
                }
                if (!controllerByDir.ContainsKey(dir)) dir = "down";
                var anim = t.GetComponent<Animator>();
                if (anim == null) anim = t.gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = controllerByDir[dir];
                anim.updateMode = AnimatorUpdateMode.Normal;
                EditorUtility.SetDirty(t.gameObject);
                assigned++;
                log.AppendLine("Pig '" + t.name + "' -> " + dir);
            }
            EditorSceneManager.MarkSceneDirty(animalRoot.scene);
        }
        AssetDatabase.SaveAssets();
        log.AppendLine("DONE pigs. assigned=" + assigned);
        return log.ToString();
    }

    static readonly string[] PlantKeywords = { "tree", "crop", "plant", "bush", "flower", "grass", "shrub", "fern" };

    [MenuItem("Tools/Forest/Add Sway To Plants")]
    public static string AddSwayToPlants()
    {
        var log = new StringBuilder();
        var roots = new List<Transform>();
        var l4 = GameObject.Find("Objects/L4_Trees");
        if (l4 != null) roots.Add(l4.transform);
        var grass = GameObject.Find("Grid/grass");
        if (grass != null) roots.Add(grass.transform);

        if (roots.Count == 0) { return "ERROR: no plant containers (Objects/L4_Trees, Grid/grass) found"; }

        int added = 0, skipped = 0;
        UnityEngine.Random.InitState(12345);
        foreach (var root in roots)
        {
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                string lname = sr.gameObject.name.ToLowerInvariant();
                bool isPlant = PlantKeywords.Any(k => lname.Contains(k));
                if (!isPlant) { skipped++; continue; }
                var sway = sr.GetComponent<SwaySprite>();
                if (sway == null) sway = sr.gameObject.AddComponent<SwaySprite>();
                sway.amplitude = UnityEngine.Random.Range(1.5f, 3.2f);
                sway.speed = UnityEngine.Random.Range(1.1f, 1.9f);
                sway.phase = UnityEngine.Random.Range(0f, 6.2831853f);
                EditorUtility.SetDirty(sr.gameObject);
                added++;
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }
        log.AppendLine("DONE sway. added=" + added + " skipped(non-plant)=" + skipped);
        return log.ToString();
    }

    [MenuItem("Tools/Forest/Diagnose Water")]
    public static void DiagnoseWater()
    {
        var go = GameObject.Find("Grid/L1_Water");
        if (go == null) { Debug.LogError("[Water] Grid/L1_Water not found"); return; }
        var tm = go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        if (tm == null) { Debug.LogError("[Water] no Tilemap on L1_Water"); return; }

        var counts = new Dictionary<string, int>();
        var pathByName = new Dictionary<string, string>();
        var b = tm.cellBounds;
        int total = 0;
        foreach (var pos in b.allPositionsWithin)
        {
            var tile = tm.GetTile(pos);
            if (tile == null) continue;
            total++;
            string key = tile.GetType().Name + ":" + tile.name;
            counts.TryGetValue(key, out int c); counts[key] = c + 1;
            if (!pathByName.ContainsKey(key)) pathByName[key] = AssetDatabase.GetAssetPath(tile);
        }
        var sb = new StringBuilder();
        sb.Append("[Water] cells=" + total + " distinct=" + counts.Count + " || ");
        foreach (var kv in counts.OrderByDescending(k => k.Value).Take(15))
            sb.Append(kv.Value + "x " + kv.Key + " @ " + pathByName[kv.Key] + "  ||  ");
        Debug.Log(sb.ToString());
    }

    // ---- Sprite sheet -> sliced sprites -> prefabs ----
    const string TreesSheet = "Assets/Art/Trees/Trees+.png";
    const string TreesPrefabDir = "Assets/Prefabs/Trees";

    [MenuItem("Tools/Forest/Import And Prefab Trees")]
    public static void ImportAndPrefabTrees()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TreesSheet) == null)
        {
            Debug.LogError("[Trees] sheet not found at " + TreesSheet);
            return;
        }

        // 1) Base import settings (pixel-art) + make readable for auto-slicing.
        var importer = (TextureImporter)AssetImporter.GetAtPath(TreesSheet);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TreesSheet);

        // 2) Auto-detect sprite rectangles by transparent gaps (internal Unity util via reflection).
        var utilType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.InternalSpriteUtility");
        var method = utilType.GetMethod("GenerateAutomaticSpriteRectangles",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var rects = (Rect[])method.Invoke(null, new object[] { tex, 8, 0 });
        if (rects == null || rects.Length == 0) { Debug.LogError("[Trees] auto-slice found 0 rects"); return; }

        // Order top-left -> bottom-right for stable naming (texture y grows upward).
        var ordered = rects
            .OrderByDescending(r => Mathf.RoundToInt((r.y + r.height) / 16f))
            .ThenBy(r => r.x)
            .ToArray();

        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < ordered.Length; i++)
        {
            metas.Add(new SpriteMetaData
            {
                name = "tree_" + i.ToString("000"),
                rect = ordered[i],
                alignment = (int)SpriteAlignment.BottomCenter, // pivot at base of trunk
                pivot = new Vector2(0.5f, 0f)
            });
        }

#pragma warning disable 0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore 0618
        importer.isReadable = false; // revert; sprite fileIDs stay stable by name
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(TreesSheet, ImportAssetOptions.ForceSynchronousImport);

        // 3) One prefab per sliced sprite, each with SpriteRenderer + SwaySprite.
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(TreesPrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Trees");

        var sprites = AssetDatabase.LoadAllAssetsAtPath(TreesSheet).OfType<Sprite>().ToArray();
        int made = 0;
        foreach (var sp in sprites)
        {
            var go = new GameObject(sp.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = 20;
            go.AddComponent<SwaySprite>();
            string prefabPath = TreesPrefabDir + "/" + sp.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Trees] sliced=" + ordered.Length + " sprites=" + sprites.Length + " prefabs=" + made + " -> " + TreesPrefabDir);
    }

    const string SlicesCsv = "Assets/Art/Trees/_slices.csv";
    const string RenameCsv = "Assets/Art/Trees/_rename_map.csv";

    [MenuItem("Tools/Forest/Dump Tree Rects")]
    public static void DumpTreeRects()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TreesSheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TreesSheet).OfType<Sprite>()
            .OrderBy(s => { int n; int.TryParse(s.name.Replace("tree_", ""), out n); return n; })
            .ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("# texW=" + tex.width + " texH=" + tex.height + " (rect origin is bottom-left)");
        sb.AppendLine("name,x,y,w,h");
        foreach (var s in sprites)
        {
            var r = s.rect;
            sb.AppendLine(s.name + "," + r.x + "," + r.y + "," + r.width + "," + r.height);
        }
        System.IO.File.WriteAllText(SlicesCsv, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log("[Trees] dumped " + sprites.Length + " rects -> " + SlicesCsv);
    }

    [MenuItem("Tools/Forest/Rename Tree Prefabs")]
    public static void RenameTreePrefabs()
    {
        if (!System.IO.File.Exists(RenameCsv)) { Debug.LogError("[Trees] no map at " + RenameCsv); return; }
        var lines = System.IO.File.ReadAllLines(RenameCsv);
        int ok = 0, fail = 0;
        var seen = new HashSet<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#")) continue;
            var parts = t.Split(',');
            if (parts.Length < 2) continue;
            string oldName = parts[0].Trim();
            string newName = parts[1].Trim();
            if (oldName == newName || newName.Length == 0) continue;
            if (!seen.Add(newName)) { Debug.LogWarning("[Trees] dup new name skipped: " + newName); fail++; continue; }

            string oldPath = TreesPrefabDir + "/" + oldName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) == null) { fail++; continue; }

            // Rename the root GameObject inside the prefab too.
            var contents = PrefabUtility.LoadPrefabContents(oldPath);
            contents.name = newName;
            PrefabUtility.SaveAsPrefabAsset(contents, oldPath);
            PrefabUtility.UnloadPrefabContents(contents);

            string err = AssetDatabase.RenameAsset(oldPath, newName);
            if (string.IsNullOrEmpty(err)) ok++; else { Debug.LogWarning("[Trees] rename fail " + oldName + " -> " + newName + ": " + err); fail++; }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Trees] renamed ok=" + ok + " fail=" + fail);
    }

    // ---- Walking animals (patrol) + animated fire ----
    const string FoxSheet = "Assets/Gif/Super_Retro_Collection/Resources/Characters/Animals/foxes/fox1_16x20.png";
    const string PigRightSheet = PigBase + "pig_01_move_right_32x32_4frames.png";
    const string FireSheet = "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Fires/Sprites/fire_camp_01.png";

    static Sprite[] OrderedSprites(string sheetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
            .OrderBy(s => { var p = s.name.Split('_'); int n; return int.TryParse(p.Last(), out n) ? n : 0; })
            .ToArray();
    }

    static RuntimeAnimatorController BuildLoopController(string name, Sprite[] frames, float fps)
    {
        EnsureDir();
        var clip = new AnimationClip { frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        AssetDatabase.CreateAsset(clip, AnimDir + "/" + name + ".anim");
        return AnimatorController.CreateAnimatorControllerAtPathWithClip(AnimDir + "/" + name + ".controller", clip);
    }

    static Animator EnsureAnimator(GameObject go, RuntimeAnimatorController ctrl)
    {
        var a = go.GetComponent<Animator>();
        if (a == null) a = go.AddComponent<Animator>();
        a.runtimeAnimatorController = ctrl;
        a.updateMode = AnimatorUpdateMode.Normal;
        return a;
    }

    static int TrailingInt(string s)
    {
        var parts = s.Split('_');
        int n;
        return int.TryParse(parts[parts.Length - 1], out n) ? n : 0;
    }

    [MenuItem("Tools/Forest/Animate Pigs Foxes Fire")]
    public static void AnimatePigsFoxesFire()
    {
        var log = new StringBuilder();

        // --- Fire (3-frame campfire, stays in place -> keep an Animator) ---
        var fireFrames = OrderedSprites(FireSheet);
        if (fireFrames.Length > 0)
        {
            var fireCtrl = BuildLoopController("fire_camp", fireFrames, 8f);
            var fire = GameObject.Find("Objects/L5_Structures/fire_camp_01_0");
            if (fire != null) { EnsureAnimator(fire, fireCtrl); EditorUtility.SetDirty(fire); log.Append("fire ok; "); }
            else log.Append("fire NOT FOUND; ");
        }
        else log.Append("fire frames=0; ");

        // --- Directional frame sets ---
        var pigUp = OrderedSprites(PigBase + "pig_01_move_up_32x32_4frames.png");
        var pigDown = OrderedSprites(PigBase + "pig_01_move_down_32x32_4frames.png");
        var pigLeft = OrderedSprites(PigBase + "pig_01_move_left_32x32_4frames.png");
        var pigRight = OrderedSprites(PigRightSheet);

        // Fox 3x4 grid, rows top->bottom = down, left, right, up.
        var foxAll = OrderedSprites(FoxSheet);
        Sprite[] foxDown = { foxAll[0], foxAll[1], foxAll[2] };
        Sprite[] foxLeft = { foxAll[3], foxAll[4], foxAll[5] };
        Sprite[] foxRight = { foxAll[6], foxAll[7], foxAll[8] };
        Sprite[] foxUp = { foxAll[9], foxAll[10], foxAll[11] };

        var animal = GameObject.Find("Objects/animal");
        int n = 0;
        if (animal != null)
        {
            foreach (Transform t in animal.transform)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sr.sprite);

                bool horizontal; int initialDir; Sprite[] fwd, back;

                if (path.Contains("/pigs/"))
                {
                    if (path.Contains("move_up")) { horizontal = false; initialDir = 1; fwd = pigUp; back = pigDown; }
                    else if (path.Contains("move_down")) { horizontal = false; initialDir = -1; fwd = pigUp; back = pigDown; }
                    else if (path.Contains("move_right")) { horizontal = true; initialDir = 1; fwd = pigRight; back = pigLeft; }
                    else { horizontal = true; initialDir = -1; fwd = pigRight; back = pigLeft; }
                }
                else if (path.Contains("/foxes/"))
                {
                    int idx = TrailingInt(sr.sprite.name); // 0..11
                    if (idx <= 2) { horizontal = false; initialDir = -1; fwd = foxUp; back = foxDown; }      // placed facing down
                    else if (idx <= 5) { horizontal = true; initialDir = -1; fwd = foxRight; back = foxLeft; } // placed facing left
                    else if (idx <= 8) { horizontal = true; initialDir = 1; fwd = foxRight; back = foxLeft; }  // placed facing right
                    else { horizontal = false; initialDir = 1; fwd = foxUp; back = foxDown; }                  // placed facing up
                }
                else continue;

                // Remove the Animator so PatrolWalk fully owns the sprite frames.
                var an = t.GetComponent<Animator>();
                if (an != null) UnityEngine.Object.DestroyImmediate(an);

                var pw = t.GetComponent<PatrolWalk>();
                if (pw == null) pw = t.gameObject.AddComponent<PatrolWalk>();
                pw.rangeForward = 1.0f; // tune with the green gizmo to touch the fence
                pw.rangeBack = 1.0f;
                pw.speed = 0.15f;       // slower, calmer pacing
                pw.fps = 5f;
                pw.horizontal = horizontal;
                pw.initialDir = initialDir;
                pw.forwardFrames = fwd;
                pw.backFrames = back;
                EditorUtility.SetDirty(t.gameObject);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(animal.scene);
        }
        else log.Append("animal group NOT FOUND; ");

        AssetDatabase.SaveAssets();
        Debug.Log("[AnimFF] " + log + "patrol animals=" + n);
    }
}
