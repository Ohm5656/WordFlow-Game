"""
Clean up Region 1 map_layout.json into a readable green tilemap:
- Reduce trees drastically (keep thinned edge border + named forest clusters + island tree).
- Open 5x5 around every quest point; clear a 1-cell buffer around paths.
- Swap off-theme props (torii -> wooden signpost column, big statue -> rock landmark).
- Remove the 2 clustered ambient lamps at the bridge.
- Add a LIGHT scatter of natural tall-grass detail on a new L3_GroundDeco layer (~12% of
  open grass, capped), to keep the map from feeling bare without clutter.

Deterministic (hash-based, no RNG) so it is re-runnable. Run from repo root:
    python tools/clean_map_layout.py
"""
import json, os, shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYOUT = os.path.join(ROOT, "map_layout.json")
BACKUP = os.path.join(ROOT, "map_layout.before_clean.json")

GRASS_DETAIL_PATH = ("Assets/Gif/Super_Retro_Collection/Resources/Prefabs_with_behavior/"
                     "tall_grass_react_on_contact/Sprites/tallgrass_01_front.png")

TREES = {"tree_conifer", "tree_conifer2", "tree_conifer3", "tree_blue",
         "tree_purple", "tree_red", "tree_round"}

# Forest cluster rectangles to KEEP (authoring coords, x0..x1 / y0..y1 inclusive)
CLUSTERS = [
    (0, 6, 9, 18),    # left forest (Zone B)
    (6, 12, 0, 3),    # blue grove top-left
    (29, 34, 0, 4),   # grove top-right
    (18, 24, 2, 7),   # purple center grove
    (24, 30, 5, 10),  # right grove
    (29, 35, 11, 17), # bottom-right forest
]
BORDER_KEEP_PCT = 50   # thin the edge border to ~50%
CLUSTER_KEEP_PCT = 70  # thin clusters to ~70%
DETAIL_PCT = 18        # grass-detail coverage of open grass (~18%, within 10-20% target)
DETAIL_CAP = 70        # hard cap on grass-detail tufts
QUEST_CLEAR_R = 2      # Chebyshev radius cleared of trees around each quest point
PATH_BUFFER = 1        # Chebyshev buffer cleared of trees around path cells

ISLAND_TREE = (16, 12)  # always keep the red island landmark


def h(x, y, salt=0):
    return abs(((x * 73856093) ^ (y * 19349663) ^ (salt * 83492791))) % 100


def in_border(x, y, W, H):
    return x <= 1 or x >= W - 2 or y <= 1 or y >= H - 2


def in_cluster(x, y):
    return any(x0 <= x <= x1 and y0 <= y <= y1 for (x0, x1, y0, y1) in CLUSTERS)


def cheby_cells(cells, r):
    out = set()
    for (x, y) in cells:
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                out.add((x + dx, y + dy))
    return out


def main():
    with open(LAYOUT, "r", encoding="utf-8") as f:
        d = json.load(f)
    if not os.path.exists(BACKUP):
        shutil.copy(LAYOUT, BACKUP)
        print("Wrote JSON backup:", BACKUP)

    W, H = d["width"], d["height"]

    # idempotency: drop any grass detail from a previous run before re-scattering
    d["objects"] = [o for o in d["objects"] if o.get("asset") != "grass_detail"]

    # --- register grass detail asset ---
    d["assets"]["grass_detail"] = {"type": "sprite", "path": GRASS_DETAIL_PATH}

    # --- gather geometry ---
    water_cells = {(x, y) for y, row in enumerate(d["layers"]["L1_Water"])
                   for x, v in enumerate(row) if v}
    path_cells = {(x, y) for y, row in enumerate(d["layers"]["L2_Path"])
                  for x, v in enumerate(row) if v}
    quest_cells = {(p["x"], p["y"]) for z in d.get("quests", []) for p in z["points"]}

    # --- swap off-theme props + drop clustered bridge lamps ---
    drop_lamps = {(14, 15), (19, 15)}
    swaps = {"torii": "column", "statue": "rock"}
    new_objs = []
    swapped = {"torii": 0, "statue": 0}
    dropped_lamp = 0
    for o in d["objects"]:
        if o["asset"] in TREES:
            new_objs.append(o)  # handled in declutter below
            continue
        if o["asset"] == "lamp" and (o["x"], o["y"]) in drop_lamps:
            dropped_lamp += 1
            continue
        if o["asset"] in swaps:
            swapped[o["asset"]] += 1
            o = dict(o); o["asset"] = swaps[o["asset"]]
            o.pop("sx", None); o.pop("sy", None)  # use natural scale for the substitute
        new_objs.append(o)
    d["objects"] = new_objs

    # cells occupied by non-tree props (keep trees away from them)
    prop_cells = {(o["x"], o["y"]) for o in d["objects"] if o["asset"] not in TREES}

    # zones that must stay clear of trees
    clear_zone = cheby_cells(quest_cells, QUEST_CLEAR_R) \
        | cheby_cells(path_cells, PATH_BUFFER) | prop_cells

    # --- declutter trees ---
    kept, removed = [], 0
    for o in d["objects"]:
        if o["asset"] not in TREES:
            kept.append(o); continue
        x, y = o["x"], o["y"]
        if (x, y) == ISLAND_TREE:
            kept.append(o); continue
        if (x, y) in clear_zone:
            removed += 1; continue
        if in_border(x, y, W, H):
            if h(x, y) < BORDER_KEEP_PCT:
                kept.append(o)
            else:
                removed += 1
        elif in_cluster(x, y):
            if h(x, y) < CLUSTER_KEEP_PCT:
                kept.append(o)
            else:
                removed += 1
        else:
            removed += 1  # open interior -> clear
    d["objects"] = kept

    # --- light grass-detail scatter on open grass ---
    tree_cells = {(o["x"], o["y"]) for o in d["objects"] if o["asset"] in TREES}
    prop_cells = {(o["x"], o["y"]) for o in d["objects"] if o["asset"] not in TREES}
    blocked = water_cells | path_cells | quest_cells | tree_cells | prop_cells \
        | cheby_cells(quest_cells, 1)  # keep quest tiles themselves tidy
    detail = 0
    for y in range(H):
        for x in range(W):
            if detail >= DETAIL_CAP:
                break
            if (x, y) in blocked:
                continue
            if h(x, y, salt=7) < DETAIL_PCT:
                d["objects"].append({"asset": "grass_detail", "x": x, "y": y,
                                     "layer": "L3_GroundDeco"})
                detail += 1

    with open(LAYOUT, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=1)

    n_trees = sum(1 for o in d["objects"] if o["asset"] in TREES)
    print(f"Trees removed: {removed}  (remaining trees: {n_trees})")
    print(f"Off-theme swapped: torii->column x{swapped['torii']}, statue->rock x{swapped['statue']}")
    print(f"Bridge ambient lamps dropped: {dropped_lamp}")
    print(f"Grass-detail tufts added: {detail}")
    print(f"Total objects now: {len(d['objects'])}")


if __name__ == "__main__":
    main()
