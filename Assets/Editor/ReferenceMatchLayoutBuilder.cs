using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ReferenceMatchLayoutBuilder
{
    private const string ScenePath = "Assets/Gif/Super_Retro_Collection/Samples/real_region1.unity";
    private const string RootName = "REFERENCE_MATCH_LAYOUT";
    private const string BridgePrefabPath = "Assets/Prefabs/Generated/reference_plank_bridge.prefab";
    private const float MapWidthTiles = 92f;
    private const float MapHeightTiles = 51f;

    private static readonly string[] TreePrefabs =
    {
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_01.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_02.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_03.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_04.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_05.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_06.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_07.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_08.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_09.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_10.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_11.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_12.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_13.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_14.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_15.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_16.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_17.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_18.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_19.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_20.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_21.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_22.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_23.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_24.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_25.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_26.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_27.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_28.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_29.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_30.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_31.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Trees/tree_32.prefab",
    };

    private static readonly string[] RockPrefabs =
    {
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_01.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_02.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_03.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_04.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_05.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_06.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_07.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_08.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_09.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_10.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_11.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_12.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_13.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_14.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Rocks/rock_15.prefab",
    };

    private static readonly string WaterPrefab =
        "Assets/Prefabs/GifPlaceables/SuperRetro/Animations/Water/water_02_16x16.prefab";

    private static readonly string[] CharacterSprites =
    {
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Characters/chara_0.png",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Characters/chara_1.png",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Characters/chara_2.png",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Characters/chara_3.png",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Characters/chara_4.png",
        "Assets/Gif/Super_Retro_Collection/Resources/Characters/Monsters/Monsters_05_0.png",
    };

    private static readonly string[] CharacterPrefabs =
    {
        "Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_01/spritesheet_0.prefab",
        "Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_01/spritesheet_1.prefab",
        "Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_02/spritesheet_0.prefab",
        "Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_02/spritesheet_1.prefab",
        "Assets/Prefabs/GifPlaceables/SuperRetro/Characters/chara_03/spritesheet_0.prefab",
        "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Player/Player_01.prefab",
    };

    private static readonly string[] PathSprites =
    {
        "Assets/Art/other/WorldMap lock/1/sheet01_path_disc_01.png",
        "Assets/Art/other/WorldMap lock/1/sheet01_path_disc_02.png",
        "Assets/Art/other/WorldMap lock/2/sheet02_path_disc_mid.png",
        "Assets/Art/other/WorldMap lock/3/sheet03_path_disc_01.png",
        "Assets/Art/other/WorldMap lock/3/sheet03_path_disc_02.png",
        "Assets/Art/other/WorldMap lock/5/sheet05_walking_path_tile.png",
    };

    private static readonly string[] DecorationSprites =
    {
        "Assets/Art/other/WorldMap lock/1/sheet01_grass_tuft_01.png",
        "Assets/Art/other/WorldMap lock/1/sheet01_grass_tuft_02.png",
        "Assets/Art/other/WorldMap lock/1/sheet01_grass_tuft_03.png",
        "Assets/Art/other/WorldMap lock/2/sheet02_grass_tuft_01.png",
        "Assets/Art/other/WorldMap lock/2/sheet02_grass_tuft_02.png",
        "Assets/Art/other/WorldMap lock/2/sheet02_grass_tuft_03.png",
        "Assets/Art/other/WorldMap lock/4/sheet04_small_bush_01.png",
        "Assets/Art/other/WorldMap lock/4/sheet04_small_bush_02.png",
    };

    [MenuItem("NSC Tools/Build Reference Match Layout")]
    public static void Build()
    {
        EnsureScene();
        EnsureBridgePrefab();

        var oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        var root = new GameObject(RootName);
        var ground = AddLayer(root.transform, "Ground_uses_FINAL_WATER_LAND_GRID", -500);
        var water = AddLayer(root.transform, "Water_uses_FINAL_WATER_LAND_GRID", -400);
        var path = AddLayer(root.transform, "Path", -200);
        var decoration = AddLayer(root.transform, "Decoration", -50);
        var objects = AddLayer(root.transform, "Object", 100);
        var character = AddLayer(root.transform, "Character", 250);
        var foreground = AddLayer(root.transform, "Foreground", 500);

        SetActive("FINAL_WATER_LAND_FROM_REFERENCE", true);
        SetActive("reference_forest", false);

        PlaceHouses(objects);
        PlaceWater(water);
        PlaceBridge(objects);
        PlacePaths(path);
        PlaceTrees(objects, foreground);
        PlaceRocks(objects);
        PlaceDecorations(decoration);
        PlaceCharacters(character);
        MovePlayerToBridge();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("ReferenceMatchLayoutBuilder: built REFERENCE_MATCH_LAYOUT and saved real_region1.");
    }

    private static Transform AddLayer(Transform root, string name, int sortingBase)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0f, 0f, sortingBase / 10000f);
        return go.transform;
    }

    private static void EnsureScene()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    private static void EnsureBridgePrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Generated"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");
        }

        var deck = LoadSprite("Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/root/single/house_autotile_0_0.png");
        var rail = LoadSprite("Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Autotiles/root/single/house_autotile_5_0.png");
        if (deck == null)
        {
            Debug.LogWarning("ReferenceMatchLayoutBuilder: bridge deck sprite missing; bridge prefab not updated.");
            return;
        }

        var bridge = new GameObject("reference_plank_bridge");
        for (var x = 0; x < 18; x++)
        {
            var piece = AddSprite(bridge.transform, $"deck_{x:00}", deck, (x - 8.5f) * 0.9f, 0f, 0f, 0.95f, 180);
            piece.transform.localScale = new Vector3(0.78f, 0.92f, 1f);
        }

        if (rail != null)
        {
            for (var x = 0; x < 5; x++)
            {
                var left = AddSprite(bridge.transform, $"post_left_{x}", rail, x * 4f - 8f, -1.75f, 0f, 0.72f, 190);
                left.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var right = AddSprite(bridge.transform, $"post_right_{x}", rail, x * 4f - 8f, 1.75f, 0f, 0.72f, 190);
                right.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(bridge, BridgePrefabPath);
        Object.DestroyImmediate(bridge);
        AssetDatabase.SaveAssets();
    }

    private static void PlaceHouses(Transform parent)
    {
        var housePath = "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Houses/house_08.prefab";
        InstantiatePrefab(housePath, parent, "house_left_reference", Tile(20f, 13f), 1.75f, 170);
        InstantiatePrefab(housePath, parent, "house_right_reference_repeat", Tile(77f, 13f), 1.75f, 170);
    }

    private static void PlaceWater(Transform parent)
    {
        var waterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPrefab);
        if (waterPrefab == null)
        {
            Debug.LogWarning($"ReferenceMatchLayoutBuilder: missing water prefab {WaterPrefab}");
            return;
        }

        var leftRiver = new[] { new Vector2(6, -2), new Vector2(4, 5), new Vector2(5, 12), new Vector2(7, 18), new Vector2(10, 24) };
        var centerRiver = new[] { new Vector2(39, -2), new Vector2(38, 6), new Vector2(42, 13), new Vector2(43, 21), new Vector2(41, 29), new Vector2(43, 37), new Vector2(36, 53) };
        var rightRiver = new[] { new Vector2(67, -2), new Vector2(69, 5), new Vector2(69, 12), new Vector2(66, 19), new Vector2(62, 27), new Vector2(65, 35), new Vector2(75, 40) };

        var index = 0;
        for (var y = 0; y <= 51; y++)
        {
            for (var x = 0; x <= 92; x++)
            {
                var p = new Vector2(x, y);
                var inWater =
                    DistanceToPolyline(p, leftRiver) < 3.2f ||
                    DistanceToPolyline(p, centerRiver) < 4.1f ||
                    DistanceToPolyline(p, rightRiver) < 4.3f;

                var isIsland = Vector2.Distance(p, new Vector2(42f, 31.5f)) < 2.5f;
                if (!inWater || isIsland)
                {
                    continue;
                }

                var pos = Tile(x, y);
                var go = InstantiatePrefab(WaterPrefab, parent, $"water_{index:0000}", pos, 1f, 0);
                if (go != null)
                {
                    go.transform.position += new Vector3(0f, 0f, 0.02f);
                    SetRenderersFixed(go, 8);
                }

                index++;
            }
        }
    }

    private static void PlaceBridge(Transform parent)
    {
        InstantiatePrefab(BridgePrefabPath, parent, "bridge_lower_center", Tile(41f, 42.3f), 1.12f, 210);
    }

    private static void PlacePaths(Transform parent)
    {
        var points = new List<Vector2>
        {
            new Vector2(8, 17), new Vector2(9, 17), new Vector2(10, 17), new Vector2(11, 17), new Vector2(12, 17),
            new Vector2(13, 17), new Vector2(14, 17), new Vector2(15, 17), new Vector2(16, 17), new Vector2(17, 18),
            new Vector2(18, 19), new Vector2(19, 19), new Vector2(20, 19), new Vector2(21, 19), new Vector2(22, 19),
            new Vector2(23, 19), new Vector2(24, 20), new Vector2(25, 21), new Vector2(26, 21), new Vector2(27, 21),
            new Vector2(29, 20), new Vector2(30, 20), new Vector2(31, 20),
            new Vector2(19, 35), new Vector2(20, 36), new Vector2(21, 37), new Vector2(22, 38), new Vector2(23, 39),
            new Vector2(24, 40), new Vector2(25, 41), new Vector2(26, 42), new Vector2(27, 43), new Vector2(28, 44),
            new Vector2(17, 39), new Vector2(18, 40), new Vector2(19, 41), new Vector2(20, 42), new Vector2(21, 43),
            new Vector2(58, 39), new Vector2(59, 39), new Vector2(60, 40), new Vector2(61, 40), new Vector2(62, 41),
            new Vector2(63, 42), new Vector2(64, 42), new Vector2(65, 43), new Vector2(66, 44), new Vector2(67, 45),
            new Vector2(47, 3), new Vector2(48, 3), new Vector2(49, 4), new Vector2(50, 4), new Vector2(51, 5),
            new Vector2(52, 5), new Vector2(53, 6), new Vector2(54, 6), new Vector2(55, 7), new Vector2(56, 7),
        };

        for (var i = 0; i < points.Count; i++)
        {
            var sprite = LoadSprite(PathSprites[i % PathSprites.Length]);
            if (sprite == null)
            {
                continue;
            }

            var jitter = new Vector2(((i * 37) % 9 - 4) * 0.05f, ((i * 19) % 7 - 3) * 0.05f);
            AddSprite(parent, $"path_stone_{i:000}", sprite, Tile(points[i].x, points[i].y) + new Vector3(jitter.x, jitter.y, 0f), 0.56f, -80);
        }
    }

    private static void PlaceTrees(Transform objects, Transform foreground)
    {
        var centers = new[]
        {
            new TreeCluster(16, 5, 19, 10, 16, objects),
            new TreeCluster(9, 31, 25, 11, 13, foreground),
            new TreeCluster(20, 45, 18, 10, 8, foreground),
            new TreeCluster(52, 18, 35, 10, 15, objects),
            new TreeCluster(73, 29, 28, 12, 16, foreground),
            new TreeCluster(83, 5, 16, 8, 10, objects),
            new TreeCluster(39, 26, 16, 5, 11, objects),
        };

        var index = 0;
        foreach (var cluster in centers)
        {
            for (var i = 0; i < cluster.Count; i++)
            {
                var angle = i * 2.399963f;
                var radius = 0.25f + ((i * 17) % 100) / 100f;
                var tx = cluster.X + Mathf.Cos(angle) * cluster.RadiusX * radius;
                var ty = cluster.Y + Mathf.Sin(angle) * cluster.RadiusY * radius;
                var path = TreePrefabs[(index * 7 + i * 3) % TreePrefabs.Length];
                var scale = 0.85f + ((i * 11) % 9) * 0.035f;
                InstantiatePrefab(path, cluster.Parent, $"tree_{index:00}_{i:00}", Tile(tx, ty), scale, 120);
            }

            index++;
        }

        InstantiatePrefab(TreePrefabs[25], objects, "island_orange_tree_reference", Tile(42f, 31f), 1.25f, 160);
        InstantiatePrefab(TreePrefabs[26], objects, "island_orange_tree_reference_back", Tile(41.2f, 29.8f), 1.05f, 155);
    }

    private static void PlaceRocks(Transform parent)
    {
        var positions = new[]
        {
            new Vector2(10, 21), new Vector2(12, 20), new Vector2(29, 26), new Vector2(35, 23),
            new Vector2(58, 7), new Vector2(60, 8), new Vector2(63, 31), new Vector2(65, 31),
            new Vector2(57, 38), new Vector2(59, 38), new Vector2(66, 3), new Vector2(4, 7),
            new Vector2(31, 11), new Vector2(78, 21), new Vector2(81, 20), new Vector2(68, 45),
        };

        for (var i = 0; i < positions.Length; i++)
        {
            var prefab = RockPrefabs[i % RockPrefabs.Length];
            InstantiatePrefab(prefab, parent, $"rock_cluster_{i:00}", Tile(positions[i].x, positions[i].y), 0.95f, 130);
        }
    }

    private static void PlaceDecorations(Transform parent)
    {
        var positions = new[]
        {
            new Vector2(8, 10), new Vector2(11, 14), new Vector2(27, 12), new Vector2(31, 28),
            new Vector2(16, 29), new Vector2(18, 32), new Vector2(21, 28), new Vector2(5, 40),
            new Vector2(7, 42), new Vector2(71, 38), new Vector2(74, 41), new Vector2(79, 37),
            new Vector2(86, 41), new Vector2(53, 47), new Vector2(31, 42), new Vector2(30, 45),
            new Vector2(61, 4), new Vector2(25, 24), new Vector2(76, 24), new Vector2(84, 22),
        };

        for (var i = 0; i < positions.Length; i++)
        {
            var sprite = LoadSprite(DecorationSprites[i % DecorationSprites.Length]);
            if (sprite == null)
            {
                continue;
            }

            AddSprite(parent, $"grass_bush_{i:00}", sprite, Tile(positions[i].x, positions[i].y), 0.75f, -30);
        }
    }

    private static void PlaceCharacters(Transform parent)
    {
        var placements = new[]
        {
            new CharacterPlacement(0, 18.0f, 18.8f, "npc_blond_front_house"),
            new CharacterPlacement(1, 20.2f, 18.8f, "npc_blue_front_house"),
            new CharacterPlacement(2, 25.0f, 27.4f, "npc_orange_center"),
            new CharacterPlacement(3, 24.0f, 29.0f, "npc_small_center"),
            new CharacterPlacement(4, 53.0f, 47.0f, "npc_bottom_right"),
            new CharacterPlacement(5, 59.2f, 1.4f, "monster_top_reference"),
        };

        for (var i = 0; i < placements.Length; i++)
        {
            var p = placements[i];
            var prefabPath = CharacterPrefabs[p.SpriteIndex % CharacterPrefabs.Length];
            var go = InstantiatePrefab(prefabPath, parent, p.Name, Tile(p.X, p.Y), 1f, 280);
            if (go != null)
            {
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null)
                    {
                        behaviour.enabled = false;
                    }
                }
            }
        }
    }

    private static void MovePlayerToBridge()
    {
        var player = GameObject.Find("Player_sprite");
        if (player == null)
        {
            return;
        }

        player.transform.position = Tile(40.5f, 42f);
        foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingOrder = 300;
        }
    }

    private static GameObject InstantiatePrefab(string prefabPath, Transform parent, string name, Vector3 position, float scale, int baseOrder)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"ReferenceMatchLayoutBuilder: missing prefab {prefabPath}");
            return null;
        }

        var go = PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene()) as GameObject;
        if (go == null)
        {
            return null;
        }

        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        SetRenderers(go, baseOrder);
        return go;
    }

    private static GameObject AddSprite(Transform parent, string name, Sprite sprite, float x, float y, float z, float scale, int baseOrder)
    {
        return AddSprite(parent, name, sprite, new Vector3(x, y, z), scale, baseOrder);
    }

    private static GameObject AddSprite(Transform parent, string name, Sprite sprite, Vector3 position, float scale, int baseOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = SortOrder(position.y, baseOrder);
        return go;
    }

    private static void SetRenderers(GameObject root, int baseOrder)
    {
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingOrder = SortOrder(renderer.transform.position.y, baseOrder);
        }
    }

    private static void SetRenderersFixed(GameObject root, int order)
    {
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingOrder = order;
        }
    }

    private static int SortOrder(float worldY, int baseOrder)
    {
        return baseOrder + Mathf.RoundToInt(-worldY * 10f);
    }

    private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> polyline)
    {
        var best = float.MaxValue;
        for (var i = 0; i < polyline.Count - 1; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, polyline[i], polyline[i + 1]));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
        return Vector2.Distance(point, a + segment * t);
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite subSprite)
            {
                return subSprite;
            }
        }

        return null;
    }

    private static Vector3 Tile(float x, float y)
    {
        return new Vector3(x - MapWidthTiles * 0.5f, MapHeightTiles * 0.5f - y, 0f);
    }

    private static void SetActive(string name, bool active)
    {
        var go = GameObject.Find(name);
        if (go != null)
        {
            go.SetActive(active);
        }
    }

    private readonly struct TreeCluster
    {
        public TreeCluster(float x, float y, int count, float radiusX, float radiusY, Transform parent)
        {
            X = x;
            Y = y;
            Count = count;
            RadiusX = radiusX;
            RadiusY = radiusY;
            Parent = parent;
        }

        public readonly float X;
        public readonly float Y;
        public readonly int Count;
        public readonly float RadiusX;
        public readonly float RadiusY;
        public readonly Transform Parent;
    }

    private readonly struct CharacterPlacement
    {
        public CharacterPlacement(int spriteIndex, float x, float y, string name)
        {
            SpriteIndex = spriteIndex;
            X = x;
            Y = y;
            Name = name;
        }

        public readonly int SpriteIndex;
        public readonly float X;
        public readonly float Y;
        public readonly string Name;
    }
}
