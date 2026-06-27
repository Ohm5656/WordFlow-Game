"""
Enrich map_layout.json for Region 1:
- Preserves terrain (L0 grass, L1 water/land shape) and existing trees.
- Adds navigation paths (L2_Path) for player flow: bridge <-> both villages.
- Adds village/forest/riverside decoration props (existing assets only).
- Adds a "quests" section: 5 zones x 5 quest points (25 total) with visual anchors.
- Removes only the trees that sit on a path / quest-anchor / prop cell so anchors stay visible.

Re-runnable: load -> transform -> write. Run from repo root: python tools/enrich_map_layout.py
"""
import json, os, shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYOUT = os.path.join(ROOT, "map_layout.json")
BACKUP = os.path.join(ROOT, "map_layout.backup.json")

PREFAB = "Assets/Gif/Super_Retro_Collection/Resources/Prefabs/"

# --- New prop assets (existing prefabs only) ---
NEW_ASSETS = {
    "barrel":   {"type": "prefab", "path": PREFAB + "Barrels/barrel_01.prefab"},
    "crate":    {"type": "prefab", "path": PREFAB + "Crates/crate_01.prefab"},
    "pot":      {"type": "prefab", "path": PREFAB + "Pots/pot_01.prefab"},
    "flower":   {"type": "prefab", "path": PREFAB + "Potted plants/potted_plant_12.prefab"},
    "bush":     {"type": "prefab", "path": PREFAB + "Potted plants/potted_plant_03.prefab"},
    "crop":     {"type": "prefab", "path": PREFAB + "Crops/crop_01.prefab"},
    "crop2":    {"type": "prefab", "path": PREFAB + "Crops/crop_05.prefab"},
    "campfire": {"type": "prefab", "path": PREFAB + "Fires/Fire_01.prefab"},
    "lamp":     {"type": "prefab", "path": PREFAB + "Lamps/lamp_01.prefab"},
    "torch":    {"type": "prefab", "path": PREFAB + "Torches/Torch_01.prefab"},
    "statue":   {"type": "prefab", "path": PREFAB + "Statues/statue_01.prefab"},
    "torii":    {"type": "prefab", "path": PREFAB + "Torii/torii_01.prefab"},
    "column":   {"type": "prefab", "path": PREFAB + "Columns/column_05.prefab"},  # fence/sign substitute
    "well":     {"type": "prefab", "path": PREFAB + "Pots/pot_20.prefab"},        # well substitute
}

# Default per-asset scale for new props (kept modest for LD readability)
SCALE = {
    "torii": 1.2, "statue": 1.1, "campfire": 1.0, "well": 1.2, "barrel": 1.0,
    "crate": 1.0, "pot": 1.0, "flower": 1.0, "bush": 1.0, "crop": 1.0,
    "crop2": 1.0, "lamp": 1.0, "torch": 1.0, "column": 1.0,
}

# --- 5 quest zones x 5 points: (num, name, x, y, purpose_th, anchor_asset) ---
ZONES = [
    ("QuestZone_A_StartingVillage", [
        (1,  "QuestPoint_01",  5,  6, "พบผู้ใหญ่บ้าน รับเควสต์แรก",     "lamp"),
        (2,  "QuestPoint_02", 10,  6, "ป้ายประกาศ/กระดานข่าวหมู่บ้าน",   "torii"),
        (3,  "QuestPoint_03",  4,  9, "แผงตลาด: นับ/ขนสินค้า",          "crate"),
        (4,  "QuestPoint_04", 10,  9, "บ่อน้ำหมู่บ้าน: ตักน้ำ",          "well"),
        (5,  "QuestPoint_05",  9, 10, "กองไฟรวมพล: ฟังเรื่องเล่า",       "campfire"),
    ]),
    ("QuestZone_B_ForestRocks", [
        (6,  "QuestPoint_06",  2, 13, "ปริศนาก้อนหิน",                  "rock"),
        (7,  "QuestPoint_07",  6, 14, "เก็บดอกไม้ในป่า",                "flower"),
        (8,  "QuestPoint_08",  2, 16, "คนตัดไม้: เก็บฟืน",              "crate"),
        (9,  "QuestPoint_09",  6, 17, "ศาลเจ้าในป่า",                   "statue"),
        (10, "QuestPoint_10",  4, 11, "พุ่มเบอร์รี่: เก็บผลไม้",         "bush"),
    ]),
    ("QuestZone_C_RiverBridge", [
        (11, "QuestPoint_11", 13, 13, "จุดตกปลาฝั่งตะวันตก",            "pot"),
        (12, "QuestPoint_12", 20, 13, "จุดตกปลาฝั่งตะวันออก",           "barrel"),
        (13, "QuestPoint_13", 13, 17, "ยามเฝ้าสะพาน (ตะวันตก)",        "lamp"),
        (14, "QuestPoint_14", 20, 17, "ยามเฝ้าสะพาน (ตะวันออก)",       "lamp"),
        (15, "QuestPoint_15", 16, 13, "ต้นไม้บนเกาะกลางน้ำ",           "torch"),
    ]),
    ("QuestZone_D_VillageCamp", [
        (16, "QuestPoint_16", 29,  6, "หัวหน้าแคมป์: รับภารกิจ",         "campfire"),
        (17, "QuestPoint_17", 34,  6, "ร้านค้าแลกเปลี่ยน",              "barrel"),
        (18, "QuestPoint_18", 28,  9, "บ่อน้ำฝั่งขวา",                  "well"),
        (19, "QuestPoint_19", 34, 10, "หอสังเกตการณ์/คบเพลิง",          "torch"),
        (20, "QuestPoint_20", 31, 10, "แปลงผักหมู่บ้าน",                "crop"),
    ]),
    ("QuestZone_E_PondForest", [
        (21, "QuestPoint_21", 33, 16, "ริมบ่อน้ำ: สังเกตสัตว์น้ำ",       "pot"),
        (22, "QuestPoint_22", 27, 15, "แปลงเกษตร: ปลูก/เก็บเกี่ยว",      "crop2"),
        (23, "QuestPoint_23", 24, 17, "แคมป์ในป่า",                    "campfire"),
        (24, "QuestPoint_24", 30, 18, "ศาลริมน้ำ",                     "torii"),
        (25, "QuestPoint_25", 35, 13, "จุดชมวิว/คบเพลิง",               "torch"),
    ]),
]

# --- Ambient decoration props (sparse, for village/forest life) ---
AMBIENT = [
    # left village
    ("barrel", 6, 8), ("pot", 8, 8), ("crop", 3, 8), ("lamp", 8, 5),
    # right village
    ("crate", 33, 8), ("pot", 30, 7), ("lamp", 30, 5), ("crop", 33, 10),
    # riverside near bridge
    ("lamp", 14, 15), ("lamp", 19, 15),
    # forest / pond ambience
    ("bush", 1, 12), ("flower", 3, 17), ("bush", 25, 16), ("flower", 26, 18),
]

# --- Navigation path segments (authoring coords; x=col, y=row from top) ---
def hseg(y, x0, x1):  # horizontal run inclusive
    return [(x, y) for x in range(min(x0, x1), max(x0, x1) + 1)]
def vseg(x, y0, y1):  # vertical run inclusive
    return [(x, y) for y in range(min(y0, y1), max(y0, y1) + 1)]

PATH_CELLS = set()
# West spine: left village down to bridge west landing
PATH_CELLS |= set(vseg(8, 9, 15))
PATH_CELLS |= set(hseg(15, 8, 13))
PATH_CELLS |= set(vseg(13, 15, 16))
PATH_CELLS.add((14, 16))
# East spine: right village down to bridge east landing
PATH_CELLS |= set(vseg(31, 9, 15))
PATH_CELLS |= set(hseg(15, 20, 31))
PATH_CELLS |= set(vseg(20, 15, 16))
PATH_CELLS.add((19, 16))
# Branch into Zone B (forest)
PATH_CELLS |= set(vseg(5, 12, 16))
PATH_CELLS |= set(hseg(16, 3, 5))
# Branch into Zone E (pond/garden)
PATH_CELLS |= set(hseg(15, 24, 31))
PATH_CELLS |= set(vseg(27, 15, 17))


def main():
    with open(LAYOUT, "r", encoding="utf-8") as f:
        d = json.load(f)
    if not os.path.exists(BACKUP):
        shutil.copy(LAYOUT, BACKUP)
        print("Wrote JSON backup:", BACKUP)

    W, H = d["width"], d["height"]

    # 1) Register new assets (idempotent)
    d["assets"].update(NEW_ASSETS)

    # 2) Paint navigation paths into L2_Path (never over river cols 15-18)
    river_cols = {15, 16, 17, 18}
    path_rows = d["layers"]["L2_Path"]
    painted = 0
    for (x, y) in PATH_CELLS:
        if 0 <= x < W and 0 <= y < H and x not in river_cols:
            if path_rows[y][x] != "path":
                path_rows[y][x] = "path"
                painted += 1

    # 3) Collect all cells that must be clear of trees
    quest_cells = {(x, y) for (_z, pts) in ZONES for (_n, _nm, x, y, _p, _a) in pts}
    ambient_cells = {(x, y) for (_a, x, y) in AMBIENT}
    clear_cells = set(PATH_CELLS) | quest_cells | ambient_cells

    # 4) Remove trees that collide with those cells (keep all other objects)
    TREES = {"tree_conifer", "tree_conifer2", "tree_conifer3", "tree_blue",
             "tree_purple", "tree_red", "tree_round"}
    kept, removed = [], 0
    for o in d["objects"]:
        if o["asset"] in TREES and (o["x"], o["y"]) in clear_cells:
            removed += 1
            continue
        kept.append(o)
    d["objects"] = kept

    # 5) Append anchor props (one per quest point) + ambient props
    def add_prop(asset, x, y, layer="L5_Structures"):
        s = SCALE.get(asset, 1.0)
        obj = {"asset": asset, "x": x, "y": y, "layer": layer}
        if s != 1.0:
            obj["sx"] = s; obj["sy"] = s
        d["objects"].append(obj)

    anchor_props = 0
    for (_zone, pts) in ZONES:
        for (_num, _name, x, y, _purpose, anchor) in pts:
            if anchor == "rock":
                # rock already exists in scene as a tree-layer prop; ensure one here
                d["objects"].append({"asset": "rock", "x": x, "y": y, "layer": "L4_Trees"})
            else:
                add_prop(anchor, x, y)
            anchor_props += 1
    for (asset, x, y) in AMBIENT:
        add_prop(asset, x, y)

    # 6) Build quests section
    d["quests"] = [
        {
            "zone": zone,
            "points": [
                {"name": name, "x": x, "y": y, "purpose": purpose}
                for (_num, name, x, y, purpose, _anchor) in pts
            ],
        }
        for (zone, pts) in ZONES
    ]

    with open(LAYOUT, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=1)

    print(f"Paths painted: {painted}")
    print(f"Trees removed (on path/quest/prop cells): {removed}")
    print(f"Quest anchor props added: {anchor_props}")
    print(f"Ambient props added: {len(AMBIENT)}")
    print(f"Total objects now: {len(d['objects'])}")
    print(f"Quest zones: {len(d['quests'])}, total points: {sum(len(z['points']) for z in d['quests'])}")


if __name__ == "__main__":
    main()
