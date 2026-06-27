from __future__ import annotations

import hashlib
import math
import random
import re
from collections import Counter
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets" / "Scenes" / "ReferenceForestTilemapTrace.unity"
SCENE_META_PATH = SCENE_PATH.with_suffix(SCENE_PATH.suffix + ".meta")
OLD_SCENE_PATH = ROOT / "Assets" / "Scenes" / "ReferenceForestMap.unity"
TILE_ROOT = ROOT / "Assets" / "Gif" / "Super_Retro_Collection" / "Resources" / "Environments" / "TilePalette" / "Tiles"
PREFAB_ROOT = ROOT / "Assets" / "Prefabs" / "GifPlaceables" / "SuperRetro"
MAP_WIDTH = 96
MAP_HEIGHT = 54
DEFAULT_MATERIAL = "{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}"


@dataclass(frozen=True)
class ObjectRef:
    file_id: str
    guid: str
    type_id: str


@dataclass(frozen=True)
class TileRef:
    asset_path: str
    tile_guid: str
    sprite_file_id: str
    sprite_guid: str


@dataclass(frozen=True)
class SpriteRef:
    file_id: str
    guid: str
    size_x: float = 1.0
    size_y: float = 1.0


class Ids:
    def __init__(self) -> None:
        self.next_id = 100000

    def get(self) -> int:
        self.next_id += 1
        return self.next_id


def main() -> int:
    SCENE_PATH.parent.mkdir(parents=True, exist_ok=True)

    tiles = load_tiles()
    tilemaps: list[tuple[str, int, dict[tuple[int, int], TileRef]]] = [
        ("00_ground_grass_base", -20, {}),
        ("01_ground_grass_variation", -18, {}),
        ("02_river_water", -10, {}),
        ("03_river_edges_shore", -7, {}),
        ("04_grass_flowers_details", -3, {}),
        ("05_stone_paths", 0, {}),
        ("06_wood_bridge", 5, {}),
    ]

    ground, grass_variation, water, shore, grass_details, paths, bridge = [item[2] for item in tilemaps]
    fill_ground(ground, tiles)
    draw_water(water, tiles)
    draw_shore(shore, water, tiles)
    draw_grass_variation(grass_variation, water, tiles)
    draw_grass_details(grass_details, water, tiles)
    draw_paths(paths, water, tiles)
    draw_bridge(bridge, tiles)

    props = build_props(water)
    text = build_scene_yaml(tilemaps, props)
    SCENE_PATH.write_text(text, encoding="utf-8", newline="\n")
    write_scene_meta()
    print(f"Generated {asset_path(SCENE_PATH)} with {sum(len(tm[2]) for tm in tilemaps)} tiles and {len(props)} sprite objects")
    return 0


def load_tiles() -> dict[str, TileRef | list[TileRef]]:
    return {
        "grass": [tile_index(2721), tile_index(1846), tile_index(2712)],
        "details": [tile_index(i) for i in (804, 899, 1246, 2468, 2550, 3326, 5438, 5439)],
        "water": tile_path("Assets/Gif/Super_Retro_Collection/Resources/Animations/Water/water_02_16x16.asset"),
        "water_alt": tile_path("Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette_updates/2.7.0/beach_water_tiles_0.asset", fallback="Assets/Gif/Super_Retro_Collection/Resources/Animations/Water/water_02_16x16.asset"),
        "shore": tile_path("Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette_updates/2.7.0/beach_water_tiles_11.asset", fallback=f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles/unity_full_tilepalette_v2_2811.asset"),
        "stone": tile_index(2950),
        "stone_alt": tile_path(f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles/unity_full_tilepalette_v2_2951.asset", fallback=f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles/unity_full_tilepalette_v2_2950.asset"),
        "dirt": tile_index(2811),
        "wood": tile_index(2598),
    }


def tile_index(index: int) -> TileRef:
    return tile_path(f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles/unity_full_tilepalette_v2_{index}.asset")


def tile_path(path: str, fallback: str | None = None) -> TileRef:
    full_path = ROOT / path
    if not full_path.exists() and fallback:
        full_path = ROOT / fallback
        path = fallback

    text = full_path.read_text(encoding="utf-8-sig", errors="replace")
    meta = (full_path.with_suffix(full_path.suffix + ".meta")).read_text(encoding="utf-8-sig", errors="replace")
    tile_guid = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", meta, re.MULTILINE).group(1)
    sprite = re.search(r"m_Sprite:\s*\{fileID:\s*([^,]+),\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", text)
    if not sprite:
        raise RuntimeError(f"Tile has no m_Sprite: {path}")
    return TileRef(path, tile_guid, sprite.group(1).strip(), sprite.group(2).strip())


def prefab_sprite(relative_path: str) -> SpriteRef | None:
    path = PREFAB_ROOT / relative_path
    if not path.exists():
        return None
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    sprite = re.search(r"m_Sprite:\s*\{fileID:\s*([^,]+),\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", text)
    if not sprite:
        return None
    size_match = re.search(r"m_Size:\s*\{x:\s*([0-9.]+),\s*y:\s*([0-9.]+)\}", text)
    size_x = float(size_match.group(1)) if size_match else 1.0
    size_y = float(size_match.group(2)) if size_match else 1.0
    return SpriteRef(sprite.group(1).strip(), sprite.group(2).strip(), size_x, size_y)


def fill_ground(layer: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    grass = tiles["grass"]
    assert isinstance(grass, list)
    for x, y in each_cell():
        h = hash_cell(x, y, 11)
        tile = grass[0]
        if h % 17 == 0:
            tile = grass[1]
        elif h % 23 == 0:
            tile = grass[2]
        layer[(x, y)] = tile


def draw_water(layer: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    draw_river(layer, tiles, [(-42.5, 28), (-44.5, 21), (-47, 13), (-45.5, 5), (-47, -5), (-43.5, -16), (-43, -28)], 3.1, 0)
    draw_river(layer, tiles, [(-7.5, 28), (-8.8, 22), (-10, 15), (-7, 8), (-8.6, 2), (-5.2, -4), (-8, -11), (-8.4, -19), (-11, -28)], 4.2, 9)
    draw_river(layer, tiles, [(41, 28), (37, 22), (34, 15), (33, 9), (36, 3), (34, -4), (31, -11)], 3.2, 19)
    draw_river(layer, tiles, [(46, 25), (47, 16), (46, 7)], 1.9, 29)
    clear_ellipse(layer, (-2.0, -7.4), (2.4, 5.4))
    clear_ellipse(layer, (35.8, 15.2), (0.9, 4.5))
    clear_ellipse(layer, (9.0, 24.8), (3.4, 1.7))


def draw_river(layer: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]], points: list[tuple[float, float]], half_width: float, seed: int) -> None:
    water = tiles["water"]
    water_alt = tiles["water_alt"]
    assert isinstance(water, TileRef) and isinstance(water_alt, TileRef)
    for x, y in each_cell():
        point = (x + 0.5, y + 0.5)
        distance = distance_to_polyline(point, points)
        edge_noise = hash_cell(x, y, seed) % 100
        noisy_width = half_width + (0.55 if edge_noise < 18 else -0.35 if edge_noise > 86 else 0)
        if distance <= noisy_width:
            layer[(x, y)] = water_alt if hash_cell(x, y, seed + 7) % 5 == 0 else water


def draw_shore(layer: dict[tuple[int, int], TileRef], water: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    shore = tiles["shore"]
    dirt = tiles["dirt"]
    assert isinstance(shore, TileRef) and isinstance(dirt, TileRef)
    for x, y in each_cell():
        cell = (x, y)
        if cell in water or not has_adjacent(water, cell, True):
            continue
        layer[cell] = dirt if hash_cell(x, y, 37) % 5 == 0 else shore


def draw_grass_variation(layer: dict[tuple[int, int], TileRef], water: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    grass = tiles["grass"]
    assert isinstance(grass, list)
    patches = [
        ((-36, 17), (12, 7), 32), ((-36, 1), (12, 10), 28), ((-35, -21), (14, 6), 34),
        ((8, 14), (12, 11), 30), ((31, 0), (14, 11), 32), ((32, -22), (14, 6), 30),
        ((-2, -7), (3, 6), 42),
    ]
    for x, y in each_cell():
        if (x, y) in water:
            continue
        density = 4
        point = (x + 0.5, y + 0.5)
        for center, radius, patch_density in patches:
            if in_ellipse(point, center, radius):
                density = max(density, patch_density)
        h = hash_cell(x, y, 43)
        if h % 100 < density:
            layer[(x, y)] = grass[1 + h % 2]


def draw_grass_details(layer: dict[tuple[int, int], TileRef], water: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    details = tiles["details"]
    assert isinstance(details, list)
    clusters = [
        ((-35, 18), (11, 5), 38), ((-35, 2), (13, 11), 34), ((-34, -20), (13, 6), 40),
        ((-17, -12), (9, 5), 30), ((9, 15), (13, 10), 34), ((32, 0), (15, 11), 35),
        ((31, -20), (13, 6), 34), ((0, -7), (3, 6), 48),
    ]
    for x, y in each_cell():
        if (x, y) in water:
            continue
        density = 32 if has_adjacent(water, (x, y), True) else 7
        point = (x + 0.5, y + 0.5)
        for center, radius, cluster_density in clusters:
            if in_ellipse(point, center, radius):
                density = max(density, cluster_density)
        h = hash_cell(x, y, 53)
        if h % 100 < density:
            layer[(x, y)] = details[h % len(details)]


def draw_paths(layer: dict[tuple[int, int], TileRef], water: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    draw_path(layer, water, tiles, [(-38, 14.6), (-34, 14.2), (-30, 14.3), (-27, 12), (-30, 10.5), (-35, 10.8)], 1.25, True)
    draw_path(layer, water, tiles, [(-33.5, 10.6), (-31.5, 6.2), (-34.8, 1.4), (-31.2, -4.8), (-25.5, -9.8), (-17, -13)], 1.05, True)
    draw_path(layer, water, tiles, [(0.4, 23), (6, 22.5), (12.5, 21), (11.5, 18.6), (6, 17.8)], 1.05, True)
    draw_path(layer, water, tiles, [(31, 14.6), (36, 14.4), (42, 13.6)], 1.05, True)
    draw_path(layer, water, tiles, [(16, -11.5), (22, -14.2), (31, -14.8), (40, -12.8)], 1.15, True)
    draw_path(layer, water, tiles, [(-45, -14), (-39, -15.2), (-33, -15), (-27, -17.5)], 0.95, False)


def draw_path(layer: dict[tuple[int, int], TileRef], water: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]], points: list[tuple[float, float]], half_width: float, cobble: bool) -> None:
    stone = tiles["stone"]
    stone_alt = tiles["stone_alt"]
    dirt = tiles["dirt"]
    assert isinstance(stone, TileRef) and isinstance(stone_alt, TileRef) and isinstance(dirt, TileRef)
    for x, y in each_cell():
        if (x, y) in water:
            continue
        point = (x + 0.5, y + 0.5)
        distance = distance_to_polyline(point, points)
        if distance > half_width:
            continue
        h = hash_cell(x, y, 71 if cobble else 73)
        if h % 100 < 16 and distance > half_width * 0.65:
            continue
        layer[(x, y)] = (stone_alt if h % 3 == 0 else stone) if cobble else (stone if h % 4 == 0 else dirt)


def draw_bridge(layer: dict[tuple[int, int], TileRef], tiles: dict[str, TileRef | list[TileRef]]) -> None:
    wood = tiles["wood"]
    assert isinstance(wood, TileRef)
    for x in range(-16, 8):
        for y in range(-20, -16):
            layer[(x, y)] = wood
    for x in range(-16, 8, 3):
        layer[(x, -21)] = wood
        layer[(x, -16)] = wood
    for y in range(-21, -15):
        layer[(-17, y)] = wood
        layer[(8, y)] = wood


def build_props(water: dict[tuple[int, int], TileRef]) -> list[dict[str, object]]:
    props: list[dict[str, object]] = []

    def place(relative_path: str, name: str, x: float, y: float, sorting: int | None = None) -> None:
        sprite = prefab_sprite(relative_path)
        if not sprite:
            return
        props.append({"name": name, "sprite": sprite, "x": x, "y": y, "sorting": sorting if sorting is not None else sorting_for_y(y)})

    def gen(folder: str, name: str, x: float, y: float, sorting: int | None = None) -> None:
        place(f"Prefabs/{folder}/{name}.prefab", name, x, y, sorting)

    gen("Houses", "house_19", -35.6, 15.0, 50)
    gen("Houses", "house_19", 37.0, 15.0, 50)
    for name, x, y in [
        ("barrel_01", -42.0, 13.5), ("barrel_02", -27.0, 13.0), ("pot_01", -31.6, 9.0),
        ("barrel_01", 31.0, 13.5), ("barrel_02", 44.8, 13.0), ("pot_01", 40.8, 9.0),
    ]:
        folder = "Barrels" if name.startswith("barrel") else "Pots"
        gen(folder, name, x, y, 62)

    gen("Trees", "tree_23", -1.5, -6.8)
    gen("Trees", "tree_24", -2.0, -9.1)

    occupied: list[tuple[float, float]] = []
    blue_green = ["tree_01", "tree_02", "tree_03", "tree_08", "tree_09", "tree_10", "tree_11", "tree_12", "tree_31", "tree_32"]
    purple = ["tree_13", "tree_14", "tree_15", "tree_16", "tree_17", "tree_18"]
    mixed = ["tree_01", "tree_03", "tree_09", "tree_10", "tree_13", "tree_15", "tree_16", "tree_18", "tree_20", "tree_24"]
    scatter_trees(props, water, occupied, (-35, 21), (13, 6), 42, 100, blue_green)
    scatter_trees(props, water, occupied, (-36, 3), (14, 12), 62, 200, mixed)
    scatter_trees(props, water, occupied, (-36, -23), (14, 5), 30, 300, blue_green)
    scatter_trees(props, water, occupied, (9, 12), (13, 12), 58, 400, mixed)
    scatter_trees(props, water, occupied, (33, 2), (15, 12), 70, 500, mixed)
    scatter_trees(props, water, occupied, (36, 21), (11, 6), 34, 600, blue_green)
    scatter_trees(props, water, occupied, (32, -23), (14, 5), 30, 700, blue_green)
    scatter_trees(props, water, occupied, (-24, 5), (7, 8), 28, 800, purple)

    for tree, x, y in [
        ("tree_09", -45.2, 24.0), ("tree_10", -42.2, 21.0), ("tree_08", -39.2, 23.0),
        ("tree_11", -33.3, 23.2), ("tree_12", -29.9, 21.6), ("tree_15", -42.0, -1.8),
        ("tree_16", -38.8, -4.6), ("tree_17", -35.1, -5.8), ("tree_18", -30.8, -4.4),
        ("tree_03", -20.0, -22.0), ("tree_08", -16.8, -23.2), ("tree_09", -13.4, -22.6),
        ("tree_13", 5.0, 21.0), ("tree_15", 8.2, 18.2), ("tree_16", 11.8, 18.0),
        ("tree_18", 14.6, 15.1), ("tree_10", 31.0, 22.0), ("tree_09", 35.0, 22.5),
        ("tree_11", 41.0, 20.2), ("tree_15", 22.8, 6.0), ("tree_16", 26.1, 3.0),
        ("tree_17", 29.6, 1.5), ("tree_18", 33.0, -1.0), ("tree_13", 38.0, -2.2),
    ]:
        gen("Trees", tree, x, y)

    for rock, x, y in [
        ("rock_20", -43.4, 12.5), ("rock_18", -27.5, 13.5), ("rock_19", -39.0, 8.0),
        ("rock_21", -28.2, 5.6), ("rock_18", -38.6, -8.8), ("rock_22", -24.0, -9.8),
        ("rock_20", 7.2, 24.0), ("rock_21", 12.8, 20.3), ("rock_19", 17.0, -7.8),
        ("rock_20", 21.0, -8.3), ("rock_18", 29.0, 8.4), ("rock_21", 43.0, 5.6),
        ("rock_22", 19.0, 15.2), ("rock_23", 24.0, 14.0),
    ]:
        gen("Rocks", rock, x, y)

    rocks = ["rock_18", "rock_19", "rock_20", "rock_21", "rock_22", "rock_23", "rock_24"]
    crops = ["crop_04", "crop_05", "crop_06", "crop_08", "crop_09", "crop_10", "crop_11", "crop_12", "crop_13", "crop_15", "crop_16", "crop_18", "crop_19"]
    scatter_props(props, water, "Rocks", rocks, (-38, -19), (12, 5), 20, 1000)
    scatter_props(props, water, "Rocks", rocks, (30, -13), (14, 5), 18, 1100)
    scatter_props(props, water, "Crops", crops, (-39, -7), (9, 7), 38, 1200)
    scatter_props(props, water, "Crops", crops, (-19, -13), (8, 4), 24, 1300)
    scatter_props(props, water, "Crops", crops, (12, 13), (9, 7), 28, 1400)
    scatter_props(props, water, "Crops", crops, (31, -10), (12, 6), 42, 1500)
    for x, y in [(-32, 9), (-25, 9.8), (-18.4, -11.8), (30, 9), (40.5, 9.6), (20.8, -12)]:
        gen("Pots", "pot_01", x, y)

    for rel, name, x, y, order in [
        ("Hero/hero/color_1/idle/hero_idle_DOWN_0.prefab", "player_on_reference_bridge", -4.0, -18.1, 120),
        ("Characters/chara_01/spritesheet_0.prefab", "npc_left_orange_hair", -29.7, 10.8, 110),
        ("Characters/chara_02/spritesheet_0.prefab", "npc_left_blue_hair", -32.5, 13.0, 110),
        ("Characters/chara_03/spritesheet_0.prefab", "npc_left_lower", -24.0, 3.6, 112),
        ("Characters/chara_01/spritesheet_0.prefab", "npc_right_orange_hair", 35.0, 10.8, 110),
        ("Characters/chara_02/spritesheet_0.prefab", "npc_right_blue_hair", 38.0, 13.0, 110),
        ("Characters/Characters/atlas_frame16x20_0.prefab", "npc_bottom_center", 8.0, -25.0, 116),
        ("Characters/Animals/cats/cat1_16x20_0.prefab", "cat_left_bottom", -43.0, -13.4, 112),
        ("Characters/Animals/rabbits/rabbit1_16x20_0.prefab", "rabbit_right_bottom", 29.0, -13.0, 112),
        ("Characters/Animals/birds/bird1_16x20_0.prefab", "bird_top_right", 47.0, 23.0, 112),
    ]:
        sprite = prefab_sprite(rel)
        if sprite:
            props.append({"name": name, "sprite": sprite, "x": x, "y": y, "sorting": order})

    return props


def scatter_trees(props: list[dict[str, object]], water: dict[tuple[int, int], TileRef], occupied: list[tuple[float, float]], center: tuple[float, float], radius: tuple[float, float], count: int, seed: int, names: list[str]) -> None:
    rnd = random.Random(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 30:
        attempts += 1
        point = random_point_in_ellipse(rnd, center, radius)
        if blocked_by_water(water, point) or not has_spacing(occupied, point, 2.0):
            continue
        occupied.append(point)
        name = rnd.choice(names)
        sprite = prefab_sprite(f"Prefabs/Trees/{name}.prefab")
        if sprite:
            props.append({"name": f"tree_trace_{seed}_{placed:03}", "sprite": sprite, "x": point[0], "y": point[1], "sorting": sorting_for_y(point[1])})
        placed += 1


def scatter_props(props: list[dict[str, object]], water: dict[tuple[int, int], TileRef], folder: str, names: list[str], center: tuple[float, float], radius: tuple[float, float], count: int, seed: int) -> None:
    rnd = random.Random(seed)
    placed = 0
    attempts = 0
    while placed < count and attempts < count * 25:
        attempts += 1
        point = random_point_in_ellipse(rnd, center, radius)
        if blocked_by_water(water, point):
            continue
        name = rnd.choice(names)
        sprite = prefab_sprite(f"Prefabs/{folder}/{name}.prefab")
        if sprite:
            props.append({"name": f"{folder}_{seed}_{placed:03}", "sprite": sprite, "x": point[0], "y": point[1], "sorting": sorting_for_y(point[1])})
        placed += 1


def build_scene_yaml(tilemaps: list[tuple[str, int, dict[tuple[int, int], TileRef]]], props: list[dict[str, object]]) -> str:
    ids = Ids()
    root_go = ids.get()
    root_tf = ids.get()
    grid_go = ids.get()
    grid_tf = ids.get()
    grid_comp = ids.get()
    props_go = ids.get()
    props_tf = ids.get()
    camera_go = ids.get()
    camera_tf = ids.get()
    camera_comp = ids.get()

    tilemap_ids: list[tuple[str, int, dict[tuple[int, int], TileRef], int, int, int, int]] = []
    for name, sorting, cells in tilemaps:
        tilemap_ids.append((name, sorting, cells, ids.get(), ids.get(), ids.get(), ids.get()))

    prop_ids: list[tuple[dict[str, object], int, int, int]] = []
    for prop in props:
        prop_ids.append((prop, ids.get(), ids.get(), ids.get()))

    root_children = [grid_tf, props_tf, camera_tf]
    grid_children = [item[4] for item in tilemap_ids]
    prop_children = [item[2] for item in prop_ids]

    lines: list[str] = []
    lines.append(scene_preamble())
    lines.append(game_object_yaml(root_go, [root_tf], "ReferenceForestTilemapTrace_FromImage"))
    lines.append(transform_yaml(root_tf, root_go, 0, root_children, (0, 0, 0)))
    lines.append(game_object_yaml(grid_go, [grid_tf, grid_comp], "Grid"))
    lines.append(transform_yaml(grid_tf, grid_go, root_tf, grid_children, (0, 0, 0)))
    lines.append(grid_yaml(grid_comp, grid_go))
    lines.append(game_object_yaml(props_go, [props_tf], "07_sprite_objects_from_gif_prefabs"))
    lines.append(transform_yaml(props_tf, props_go, root_tf, prop_children, (0, 0, 0)))

    for name, sorting, cells, go_id, tf_id, renderer_id, tilemap_id in tilemap_ids:
        lines.append(game_object_yaml(go_id, [tf_id, renderer_id, tilemap_id], name))
        lines.append(transform_yaml(tf_id, go_id, grid_tf, [], (0, 0, 0)))
        lines.append(tilemap_renderer_yaml(renderer_id, go_id, sorting))
        lines.append(tilemap_yaml(tilemap_id, go_id, cells))

    for prop, go_id, tf_id, renderer_id in prop_ids:
        sprite = prop["sprite"]
        assert isinstance(sprite, SpriteRef)
        lines.append(game_object_yaml(go_id, [tf_id, renderer_id], str(prop["name"])))
        lines.append(transform_yaml(tf_id, go_id, props_tf, [], (float(prop["x"]), float(prop["y"]), 0)))
        lines.append(sprite_renderer_yaml(renderer_id, go_id, sprite, int(prop["sorting"])))

    lines.append(game_object_yaml(camera_go, [camera_tf, camera_comp], "Main Camera", tag="MainCamera"))
    lines.append(transform_yaml(camera_tf, camera_go, root_tf, [], (0, 0, -10)))
    lines.append(camera_yaml(camera_comp, camera_go))
    return "\n".join(lines) + "\n"


def scene_preamble() -> str:
    if OLD_SCENE_PATH.exists():
        text = OLD_SCENE_PATH.read_text(encoding="utf-8-sig", errors="replace")
        first_go = text.find("--- !u!1 ")
        if first_go > 0:
            return text[:first_go].rstrip()
    return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_OcclusionBakeSettings:\n    smallestOccluder: 5\n    smallestHole: 0.25\n    backfaceThreshold: 100\n  m_SceneGUID: 00000000000000000000000000000000\n  m_OcclusionCullingData: {fileID: 0}"


def game_object_yaml(go_id: int, components: list[int], name: str, tag: str = "Untagged") -> str:
    comp_lines = "\n".join(f"  - component: {{fileID: {component}}}" for component in components)
    return f"""--- !u!1 &{go_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comp_lines}
  m_Layer: 0
  m_Name: {yaml_name(name)}
  m_TagString: {tag}
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1"""


def transform_yaml(tf_id: int, go_id: int, parent_tf: int, children: list[int], position: tuple[float, float, float]) -> str:
    child_lines = " []" if not children else "\n" + "\n".join(f"  - {{fileID: {child}}}" for child in children)
    father = "{fileID: 0}" if parent_tf == 0 else f"{{fileID: {parent_tf}}}"
    return f"""--- !u!4 &{tf_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {fmt(position[0])}, y: {fmt(position[1])}, z: {fmt(position[2])}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{child_lines}
  m_Father: {father}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}"""


def grid_yaml(comp_id: int, go_id: int) -> str:
    return f"""--- !u!156049354 &{comp_id}
Grid:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CellSize: {{x: 1, y: 1, z: 1}}
  m_CellGap: {{x: 0, y: 0, z: 0}}
  m_CellLayout: 0
  m_CellSwizzle: 0"""


def tilemap_renderer_yaml(comp_id: int, go_id: int, sorting: int) -> str:
    return f"""--- !u!483693784 &{comp_id}
TilemapRenderer:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {DEFAULT_MATERIAL}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: {sorting}
  m_MaskInteraction: 0
  m_ChunkSize: {{x: 32, y: 32, z: 32}}
  m_ChunkCullingBounds: {{x: 0, y: 0, z: 0}}
  m_MaxChunkCount: 16
  m_MaxFrameAge: 16
  m_SortOrder: 0
  m_Mode: 0
  m_DetectChunkCullingBounds: 0"""


def tilemap_yaml(comp_id: int, go_id: int, cells: dict[tuple[int, int], TileRef]) -> str:
    unique: list[TileRef] = []
    index: dict[TileRef, int] = {}
    for tile in cells.values():
        if tile not in index:
            index[tile] = len(unique)
            unique.append(tile)
    counts = Counter(cells.values())
    total = len(cells)
    if cells:
        min_x = min(x for x, _ in cells)
        max_x = max(x for x, _ in cells)
        min_y = min(y for _, y in cells)
        max_y = max(y for _, y in cells)
    else:
        min_x = min_y = max_x = max_y = 0

    tile_lines = []
    for x, y in sorted(cells):
        idx = index[cells[(x, y)]]
        tile_lines.append(f"""  - first: {{x: {x}, y: {y}, z: 0}}
    second:
      serializedVersion: 2
      m_TileIndex: {idx}
      m_TileSpriteIndex: {idx}
      m_TileMatrixIndex: 0
      m_TileColorIndex: 0
      m_TileObjectToInstantiateIndex: 65535
      dummyAlignment: 0
      m_AllTileFlags: 1073741825""")
    asset_lines = []
    sprite_lines = []
    for tile in unique:
        count = counts[tile]
        asset_lines.append(f"""  - serializedVersion: 2
    m_RefCount: {count}
    m_Data: {{fileID: 11400000, guid: {tile.tile_guid}, type: 2}}""")
        sprite_lines.append(f"""  - serializedVersion: 2
    m_RefCount: {count}
    m_Data: {{fileID: {tile.sprite_file_id}, guid: {tile.sprite_guid}, type: 3}}""")
    matrix_ref_count = total if total else 1
    return f"""--- !u!1839735485 &{comp_id}
Tilemap:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_Tiles:
{chr(10).join(tile_lines) if tile_lines else '  []'}
  m_AnimatedTiles: {{}}
  m_TileAssetArray:
{chr(10).join(asset_lines) if asset_lines else '  []'}
  m_TileSpriteArray:
{chr(10).join(sprite_lines) if sprite_lines else '  []'}
  m_TileMatrixArray:
  - serializedVersion: 2
    m_RefCount: {matrix_ref_count}
    m_Data:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
  m_TileColorArray:
  - serializedVersion: 2
    m_RefCount: {matrix_ref_count}
    m_Data: {{r: 1, g: 1, b: 1, a: 1}}
  m_TileObjectToInstantiateArray: []
  m_AnimationFrameRate: 1
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_Origin: {{x: {min_x}, y: {min_y}, z: 0}}
  m_Size: {{x: {max_x - min_x + 1}, y: {max_y - min_y + 1}, z: 1}}
  m_TileAnchor: {{x: 0.5, y: 0.5, z: 0}}
  m_TileOrientation: 0
  m_TileOrientationMatrix:
    e00: 1
    e01: 0
    e02: 0
    e03: 0
    e10: 0
    e11: 1
    e12: 0
    e13: 0
    e20: 0
    e21: 0
    e22: 1
    e23: 0
    e30: 0
    e31: 0
    e32: 0
    e33: 1"""


def sprite_renderer_yaml(comp_id: int, go_id: int, sprite: SpriteRef, sorting: int) -> str:
    return f"""--- !u!212 &{comp_id}
SpriteRenderer:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {DEFAULT_MATERIAL}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: {sorting}
  m_MaskInteraction: 0
  m_Sprite: {{fileID: {sprite.file_id}, guid: {sprite.guid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {{x: {fmt(sprite.size_x)}, y: {fmt(sprite.size_y)}}}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_SpriteSortPoint: 0"""


def camera_yaml(comp_id: int, go_id: int) -> str:
    return f"""--- !u!20 &{comp_id}
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_id}}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {{r: 0.18, g: 0.28, b: 0.32, a: 1}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 1
  orthographic size: 27
  m_Depth: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022"""


def write_scene_meta() -> None:
    if SCENE_META_PATH.exists():
        return
    guid = hashlib.md5(asset_path(SCENE_PATH).encode("utf-8")).hexdigest()
    SCENE_META_PATH.write_text(f"""fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""", encoding="utf-8", newline="\n")


def each_cell():
    for y in range(-MAP_HEIGHT // 2, MAP_HEIGHT // 2):
        for x in range(-MAP_WIDTH // 2, MAP_WIDTH // 2):
            yield x, y


def hash_cell(x: int, y: int, seed: int) -> int:
    return abs(x * 73856093 ^ y * 19349663 ^ seed * 83492791)


def distance_to_polyline(point: tuple[float, float], points: list[tuple[float, float]]) -> float:
    return min(distance_to_segment(point, points[i], points[i + 1]) for i in range(len(points) - 1))


def distance_to_segment(point: tuple[float, float], a: tuple[float, float], b: tuple[float, float]) -> float:
    px, py = point
    ax, ay = a
    bx, by = b
    sx = bx - ax
    sy = by - ay
    length_sq = sx * sx + sy * sy
    if length_sq <= 0.0001:
        return math.dist(point, a)
    t = max(0.0, min(1.0, ((px - ax) * sx + (py - ay) * sy) / length_sq))
    return math.dist(point, (ax + sx * t, ay + sy * t))


def clear_ellipse(layer: dict[tuple[int, int], TileRef], center: tuple[float, float], radius: tuple[float, float]) -> None:
    for x, y in list(layer):
        if in_ellipse((x + 0.5, y + 0.5), center, radius):
            del layer[(x, y)]


def in_ellipse(point: tuple[float, float], center: tuple[float, float], radius: tuple[float, float]) -> bool:
    dx = (point[0] - center[0]) / radius[0]
    dy = (point[1] - center[1]) / radius[1]
    return dx * dx + dy * dy <= 1.0


def has_adjacent(layer: dict[tuple[int, int], TileRef], cell: tuple[int, int], diagonals: bool) -> bool:
    x, y = cell
    if (x + 1, y) in layer or (x - 1, y) in layer or (x, y + 1) in layer or (x, y - 1) in layer:
        return True
    return diagonals and ((x + 1, y + 1) in layer or (x + 1, y - 1) in layer or (x - 1, y + 1) in layer or (x - 1, y - 1) in layer)


def random_point_in_ellipse(rnd: random.Random, center: tuple[float, float], radius: tuple[float, float]) -> tuple[float, float]:
    angle = rnd.random() * math.pi * 2
    distance = math.sqrt(rnd.random())
    return center[0] + math.cos(angle) * radius[0] * distance, center[1] + math.sin(angle) * radius[1] * distance


def blocked_by_water(water: dict[tuple[int, int], TileRef], point: tuple[float, float]) -> bool:
    x = math.floor(point[0])
    y = math.floor(point[1])
    return (x, y) in water or (x + 1, y) in water or (x - 1, y) in water or (x, y + 1) in water or (x, y - 1) in water


def has_spacing(occupied: list[tuple[float, float]], point: tuple[float, float], min_distance: float) -> bool:
    min_sq = min_distance * min_distance
    return all((old[0] - point[0]) ** 2 + (old[1] - point[1]) ** 2 >= min_sq for old in occupied)


def sorting_for_y(y: float) -> int:
    return 80 + round((27.0 - y) * 2.5)


def fmt(value: float) -> str:
    if abs(value - round(value)) < 0.00001:
        return str(int(round(value)))
    return f"{value:.4f}".rstrip("0").rstrip(".")


def yaml_name(value: str) -> str:
    return value.replace(":", "_")


def asset_path(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


if __name__ == "__main__":
    raise SystemExit(main())
