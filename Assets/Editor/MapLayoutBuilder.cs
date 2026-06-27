using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class MapLayoutBuilder
{
    const string LayoutPath = "map_layout.json";

    [MenuItem("Tools/Reference Map/Build From Layout")]
    public static void Build()
    {
        if (!File.Exists(LayoutPath)) { Debug.LogError($"MapLayoutBuilder: missing {LayoutPath}"); return; }
        var layout = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(LayoutPath));
        int height = Convert.ToInt32(layout["height"]);
        var assets  = (Dictionary<string, object>)layout["assets"];
        var layers  = (Dictionary<string, object>)layout["layers"];
        var objects = (List<object>)layout["objects"];

        ClearAll();
        PaintLayers(layers, assets, height);
        PlaceObjects(objects, assets, height);
        if (layout.ContainsKey("quests"))
            BuildQuests((List<object>)layout["quests"], height);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("MapLayoutBuilder: build complete");
    }

    static Tilemap Tilemap(string name) => GameObject.Find($"Grid/{name}")?.GetComponent<Tilemap>();
    static Transform ObjParent(string layer) => GameObject.Find($"Objects/{layer}")?.transform;
    static int OrderFor(float worldY, bool character) => 5000 - Mathf.RoundToInt(worldY * 10f) + (character ? 1 : 0);

    static void ClearAll()
    {
        foreach (var n in new[]{"L0_Ground","L1_Water","L2_Path"}) Tilemap(n)?.ClearAllTiles();
        foreach (var l in new[]{"L3_GroundDeco","L4_Trees","L5_Structures","L6_Characters"})
        {
            var p = ObjParent(l); if (p == null) continue;
            for (int i = p.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(p.GetChild(i).gameObject);
        }
        var quests = GameObject.Find("Quests");
        if (quests != null) UnityEngine.Object.DestroyImmediate(quests);
    }

    // Builds a "Quests" root with one empty GameObject per zone, and a named
    // empty QuestPoint child per point positioned at its grid cell center.
    // These live outside the L4/L5/L6 parents so they are never cleared by
    // object placement, and are rebuilt fresh here on every run.
    static void BuildQuests(List<object> quests, int height)
    {
        var ground = Tilemap("L0_Ground");
        if (ground == null) { Debug.LogError("MapLayoutBuilder: Grid/L0_Ground missing for quests"); return; }
        var root = new GameObject("Quests");
        foreach (var z in quests)
        {
            var zone = (Dictionary<string,object>)z;
            var zoneGo = new GameObject((string)zone["zone"]);
            zoneGo.transform.SetParent(root.transform);
            foreach (var p in (List<object>)zone["points"])
            {
                var pt = (Dictionary<string,object>)p;
                int x = Convert.ToInt32(pt["x"]); int y = Convert.ToInt32(pt["y"]);
                var go = new GameObject((string)pt["name"]);
                go.transform.SetParent(zoneGo.transform);
                go.transform.position = ground.GetCellCenterWorld(new Vector3Int(x, (height - 1) - y, 0));
            }
        }
        Debug.Log($"MapLayoutBuilder: built {quests.Count} quest zones");
    }

    static void PaintLayers(Dictionary<string,object> layers, Dictionary<string,object> assets, int height)
    {
        foreach (var kv in layers)
        {
            var tm = Tilemap(kv.Key); if (tm == null) continue;
            var rows = (List<object>)kv.Value;
            for (int y = 0; y < rows.Count; y++)
            {
                var row = (List<object>)rows[y];
                for (int x = 0; x < row.Count; x++)
                {
                    var id = row[x] as string; if (string.IsNullOrEmpty(id)) continue;
                    if (!assets.ContainsKey(id)) { Debug.LogError($"MapLayoutBuilder: unknown asset id '{id}'"); continue; }
                    var tile = Resolve<TileBase>(assets, id);
                    if (tile) tm.SetTile(new Vector3Int(x, (height - 1) - y, 0), tile);
                }
            }
        }
    }

    static void PlaceObjects(List<object> objects, Dictionary<string,object> assets, int height)
    {
        var ground = Tilemap("L0_Ground");
        if (ground == null) { Debug.LogError("MapLayoutBuilder: Grid/L0_Ground missing"); return; }
        foreach (var o in objects)
        {
            var e = (Dictionary<string,object>)o;
            string id = (string)e["asset"];
            if (!assets.ContainsKey(id)) { Debug.LogError($"MapLayoutBuilder: unknown asset id '{id}'"); continue; }
            int x = Convert.ToInt32(e["x"]); int y = Convert.ToInt32(e["y"]);
            string layer = (string)e["layer"];
            var parent = ObjParent(layer); if (parent == null) { Debug.LogError($"No parent {layer}"); continue; }
            var entry = (Dictionary<string,object>)assets[id];
            string type = (string)entry["type"];
            Vector3 pos = ground.GetCellCenterWorld(new Vector3Int(x, (height - 1) - y, 0));

            GameObject go;
            if (type == "prefab")
            {
                var prefab = Resolve<GameObject>(assets, id); if (prefab == null) continue;
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else // sprite
            {
                var sprite = ResolveSprite((string)entry["path"], id); if (sprite == null) continue;
                go = new GameObject($"{id}_{x}_{y}");
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>().sprite = sprite;
            }
            go.name = $"{id}_{x}_{y}";
            float sx = e.ContainsKey("sx") ? Convert.ToSingle(e["sx"]) : 1f;
            float sy = e.ContainsKey("sy") ? Convert.ToSingle(e["sy"]) : 1f;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            var rend = go.GetComponentInChildren<SpriteRenderer>();
            float lift = rend != null ? rend.bounds.extents.y - 0.5f : 0f;
            go.transform.position = new Vector3(pos.x, pos.y + lift, 0f);
            int order = OrderFor(pos.y, layer == "L6_Characters");
            if (layer == "L3_GroundDeco") order -= 2000; // ground detail renders beneath trees/props
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder = order;
        }
    }

    static T Resolve<T>(Dictionary<string,object> assets, string id) where T : UnityEngine.Object
    {
        var path = (string)((Dictionary<string,object>)assets[id])["path"];
        var obj = AssetDatabase.LoadAssetAtPath<T>(path);
        if (obj == null) Debug.LogError($"MapLayoutBuilder: asset not found id '{id}': {path}");
        return obj;
    }

    static Sprite ResolveSprite(string path, string id)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path)) if (a is Sprite sp) return sp;
        Debug.LogError($"MapLayoutBuilder: sprite not found id '{id}': {path}");
        return null;
    }
}
