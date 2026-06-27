from __future__ import annotations

import re
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

import generate_reference_forest_tilemap_scene as trace_yaml


ROOT = Path(__file__).resolve().parents[1]
TARGET_SCENE = ROOT / "Assets" / "Gif" / "Super_Retro_Collection" / "Samples" / "real_region1.unity"
REFERENCE_IMAGE = ROOT / "Assets" / "Reference" / "reference_forest.png"
ROOT_NAME = "FINAL_WATER_LAND_FROM_REFERENCE"
PIXELS_PER_UNIT = 16
ID_START = 883_000_000_000_000_000


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def write_text(path: Path, text: str) -> None:
    path.write_text(text, encoding="utf-8", newline="\n")


def guid_from_meta(asset_path: Path) -> str:
    meta = read_text(asset_path.with_suffix(asset_path.suffix + ".meta"))
    match = re.search(r"(?m)^guid:\s*([0-9a-f]{32})\s*$", meta)
    if not match:
        raise RuntimeError(f"Missing guid in {asset_path}.meta")
    return match.group(1)


def tile_ref(asset_path: str) -> trace_yaml.TileRef:
    path = ROOT / asset_path
    if not path.exists():
        raise FileNotFoundError(path)
    text = read_text(path)
    sprite = re.search(r"m_Sprite:\s*\{fileID:\s*([^,]+),\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", text)
    if not sprite:
        raise RuntimeError(f"Missing m_Sprite in {asset_path}")
    return trace_yaml.TileRef(
        asset_path,
        guid_from_meta(path),
        sprite.group(1).strip(),
        sprite.group(2).strip(),
    )


def tile_index(index: int) -> trace_yaml.TileRef:
    return tile_ref(
        f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles/"
        f"unity_full_tilepalette_v2_{index}.asset"
    )


def beach_tile(index: int) -> trace_yaml.TileRef:
    return tile_ref(
        f"Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette_updates/2.7.0/"
        f"beach_water_tiles_{index}.asset"
    )


def hash_cell(x: int, y: int, seed: int) -> int:
    return abs(x * 73856093 ^ y * 19349663 ^ seed * 83492791)


def build_water_mask(image: np.ndarray) -> np.ndarray:
    hsv = cv2.cvtColor(image, cv2.COLOR_RGB2HSV)
    water = (
        (hsv[:, :, 0] >= 82)
        & (hsv[:, :, 0] <= 106)
        & (hsv[:, :, 1] >= 60)
        & (hsv[:, :, 2] >= 80)
        & (image[:, :, 0] < 110)
        & (image[:, :, 1] > 85)
        & (image[:, :, 2] > 100)
    ).astype(np.uint8)

    water = cv2.morphologyEx(water, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    water = cv2.morphologyEx(water, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))

    components, labels, stats, _ = cv2.connectedComponentsWithStats(water, 8)
    large_water = np.zeros_like(water)
    for idx in range(1, components):
        if stats[idx, cv2.CC_STAT_AREA] >= 10_000:
            large_water[labels == idx] = 1

    return cv2.morphologyEx(large_water, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))


def build_gray_mask(image: np.ndarray, water_mask: np.ndarray) -> np.ndarray:
    r = image[:, :, 0].astype(np.int16)
    g = image[:, :, 1].astype(np.int16)
    b = image[:, :, 2].astype(np.int16)
    mask = (
        (r > 80)
        & (r < 205)
        & (g > 80)
        & (g < 205)
        & (b > 80)
        & (b < 215)
        & (np.abs(r - g) < 34)
        & (np.abs(g - b) < 42)
        & (water_mask == 0)
    ).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((2, 2), np.uint8))
    return cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))


def build_bridge_mask(image: np.ndarray, water_mask: np.ndarray) -> np.ndarray:
    r = image[:, :, 0].astype(np.int16)
    g = image[:, :, 1].astype(np.int16)
    b = image[:, :, 2].astype(np.int16)
    h, w = water_mask.shape
    yy, xx = np.mgrid[0:h, 0:w]
    bridge_area = (xx >= 470) & (xx <= 810) & (yy >= 590) & (yy <= 735)
    mask = (
        bridge_area
        & (water_mask == 0)
        & (r > 115)
        & (g > 60)
        & (g < 170)
        & (b < 125)
        & (r > b + 35)
    ).astype(np.uint8)
    return cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))


def block_for_cell(mask_or_image: np.ndarray, cell: tuple[int, int]) -> np.ndarray:
    cx, cy = cell
    h, w = mask_or_image.shape[:2]
    center_x = w / 2 + (cx + 0.5) * PIXELS_PER_UNIT
    center_y = h / 2 - (cy + 0.5) * PIXELS_PER_UNIT
    x0 = max(0, int(round(center_x - 8)))
    x1 = min(w, int(round(center_x + 8)))
    y0 = max(0, int(round(center_y - 8)))
    y1 = min(h, int(round(center_y + 8)))
    return mask_or_image[y0:y1, x0:x1]


def each_reference_cell() -> list[tuple[int, int]]:
    return [(cx, cy) for cy in range(-25, 26) for cx in range(-45, 45)]


def choose_grass_tile(block: np.ndarray, candidates: list[trace_yaml.TileRef]) -> trace_yaml.TileRef:
    r = block[:, :, 0].astype(np.float32)
    g = block[:, :, 1].astype(np.float32)
    b = block[:, :, 2].astype(np.float32)
    green_pixels = (g > 70) & (g >= r * 0.78) & (g >= b * 0.72)
    if green_pixels.mean() >= 0.12:
        mean = np.array([r[green_pixels].mean(), g[green_pixels].mean(), b[green_pixels].mean()])
    else:
        mean = block.reshape(-1, 3).mean(axis=0)

    brightness = float(mean.mean())
    green_bias = float(mean[1] - (mean[0] + mean[2]) * 0.5)
    score_seed = int(brightness * 2 + green_bias * 5)
    idx = abs(score_seed) % len(candidates)

    if brightness > 145 and len(candidates) >= 4:
        idx = hash_cell(int(mean[0]), int(mean[1]), 21) % min(8, len(candidates))
    elif brightness < 95 and len(candidates) >= 4:
        idx = len(candidates) - 1 - (hash_cell(int(mean[1]), int(mean[2]), 22) % min(8, len(candidates)))
    return candidates[idx]


def choose_water_tile(cell: tuple[int, int], water_cells: set[tuple[int, int]], edge: dict[str, trace_yaml.TileRef], full: list[trace_yaml.TileRef]) -> trace_yaml.TileRef:
    x, y = cell
    north = (x, y + 1) in water_cells
    south = (x, y - 1) in water_cells
    west = (x - 1, y) in water_cells
    east = (x + 1, y) in water_cells

    if not north and not west:
        return edge["nw"]
    if not north and not east:
        return edge["ne"]
    if not south and not west:
        return edge["sw"]
    if not south and not east:
        return edge["se"]
    if not north:
        return edge["n"]
    if not south:
        return edge["s"]
    if not west:
        return edge["w"]
    if not east:
        return edge["e"]
    return full[hash_cell(x, y, 31) % len(full)]


def build_cells() -> tuple[list[tuple[str, int, dict[tuple[int, int], trace_yaml.TileRef]]], dict[str, int]]:
    image = np.array(Image.open(REFERENCE_IMAGE).convert("RGBA"))[:, :, :3]
    water_mask = build_water_mask(image)
    gray_mask = build_gray_mask(image, water_mask)
    bridge_mask = build_bridge_mask(image, water_mask)

    grass_candidates = [
        tile_index(i)
        for i in (
            253,
            1237,
            1261,
            5672,
            588,
            589,
            5506,
            5515,
            1234,
            1457,
            1570,
        )
    ]
    grass_detail_tiles = [tile_index(i) for i in (2920, 2921, 2925, 2926, 2940, 2941)]
    stone_tiles = [tile_index(i) for i in (4276, 4277, 4278, 4388, 4390, 4500, 4501, 4502, 4612, 4613, 4724, 4725)]
    bridge_tiles = [tile_index(i) for i in (2933, 2934)]
    water_full = [
        tile_ref("Assets/Gif/Super_Retro_Collection/Resources/Animations/Water/water_02_16x16.asset"),
        beach_tile(8),
    ]
    water_edges = {
        "nw": beach_tile(0),
        "ne": beach_tile(1),
        "sw": beach_tile(2),
        "se": beach_tile(3),
        "n": beach_tile(5),
        "w": beach_tile(7),
        "e": beach_tile(9),
        "s": beach_tile(11),
    }

    primary_water: set[tuple[int, int]] = set()
    candidate_water: set[tuple[int, int]] = set()
    cells = each_reference_cell()
    for cell in cells:
        block = block_for_cell(water_mask, cell)
        coverage = float(block.mean()) if block.size else 0
        if coverage >= 0.28:
            primary_water.add(cell)
        elif coverage >= 0.10:
            candidate_water.add(cell)

    water_cells = set(primary_water)
    for x, y in candidate_water:
        if any((x + dx, y + dy) in primary_water for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            water_cells.add((x, y))

    grass_base: dict[tuple[int, int], trace_yaml.TileRef] = {}
    grass_variation: dict[tuple[int, int], trace_yaml.TileRef] = {}
    water: dict[tuple[int, int], trace_yaml.TileRef] = {}
    water_edge: dict[tuple[int, int], trace_yaml.TileRef] = {}
    stone_path: dict[tuple[int, int], trace_yaml.TileRef] = {}
    bridge: dict[tuple[int, int], trace_yaml.TileRef] = {}
    grass_details: dict[tuple[int, int], trace_yaml.TileRef] = {}

    for cell in cells:
        x, y = cell
        grass_hash = hash_cell(x, y, 20) % 100
        medium_grass = tuple(grass_candidates[:9])
        dark_grass = tuple(grass_candidates[9:])
        if grass_hash < 3:
            grass_base[cell] = dark_grass[hash_cell(x, y, 21) % len(dark_grass)]
        else:
            grass_base[cell] = medium_grass[hash_cell(x, y, 23) % len(medium_grass)]

        if cell in water_cells:
            tile = choose_water_tile(cell, water_cells, water_edges, water_full)
            if tile in water_edges.values():
                water_edge[cell] = tile
            else:
                water[cell] = tile
            continue

        gray_coverage = float(block_for_cell(gray_mask, cell).mean())
        bridge_coverage = float(block_for_cell(bridge_mask, cell).mean())
        if bridge_coverage >= 0.16:
            bridge[cell] = bridge_tiles[hash_cell(x, y, 41) % len(bridge_tiles)]
        elif gray_coverage >= 0.30:
            stone_path[cell] = stone_tiles[hash_cell(x, y, 42) % len(stone_tiles)]
        elif gray_coverage >= 0.18 and hash_cell(x, y, 43) % 100 < 28:
            stone_path[cell] = stone_tiles[hash_cell(x, y, 44) % len(stone_tiles)]
        else:
            block = block_for_cell(image, cell)
            r = block[:, :, 0]
            g = block[:, :, 1]
            b = block[:, :, 2]
            flowerish = (
                ((r > 170) & (g < 130) & (b < 150))
                | ((r > 205) & (g > 190) & (b > 175))
                | ((r > 190) & (g > 90) & (g < 170) & (b < 90))
            )
            if flowerish.mean() >= 0.050 and hash_cell(x, y, 45) % 100 < 12:
                grass_details[cell] = grass_detail_tiles[hash_cell(x, y, 46) % len(grass_detail_tiles)]

        if hash_cell(x, y, 47) % 100 < 3:
            grass_variation[cell] = dark_grass[hash_cell(x, y, 48) % len(dark_grass)]

    tilemaps = [
        ("Final_00_land_base_grass_from_reference", -180, grass_base),
        ("Final_01_land_grass_variation_from_reference", -170, grass_variation),
        ("Final_02_water_full_from_reference", -150, water),
        ("Final_03_water_edges_from_reference", -145, water_edge),
        ("Final_04_stone_paths_from_reference", -130, stone_path),
        ("Final_05_bridge_from_reference", -120, bridge),
        ("Final_06_small_land_details_from_reference", -110, grass_details),
    ]
    stats = {
        "grass_base": len(grass_base),
        "grass_variation": len(grass_variation),
        "water_full": len(water),
        "water_edges": len(water_edge),
        "stone_path": len(stone_path),
        "bridge": len(bridge),
        "grass_details": len(grass_details),
    }
    return tilemaps, stats


class Ids:
    def __init__(self) -> None:
        self.next_id = ID_START

    def get(self) -> int:
        self.next_id += 1
        return self.next_id


def build_docs() -> tuple[str, int, dict[str, int]]:
    ids = Ids()
    root_go, root_tf = ids.get(), ids.get()
    grid_go, grid_tf, grid_comp = ids.get(), ids.get(), ids.get()
    tilemaps, stats = build_cells()

    tilemap_ids = [(name, sorting, cells, ids.get(), ids.get(), ids.get(), ids.get()) for name, sorting, cells in tilemaps]
    grid_children = [item[4] for item in tilemap_ids]

    lines: list[str] = []
    lines.append(trace_yaml.game_object_yaml(root_go, [root_tf], ROOT_NAME))
    lines.append(trace_yaml.transform_yaml(root_tf, root_go, 0, [grid_tf], (0, 0, 0)))
    lines.append(trace_yaml.game_object_yaml(grid_go, [grid_tf, grid_comp], "FINAL_WATER_LAND_GRID"))
    lines.append(trace_yaml.transform_yaml(grid_tf, grid_go, root_tf, grid_children, (0, 0, 0)))
    lines.append(trace_yaml.grid_yaml(grid_comp, grid_go))
    for name, sorting, cells, go_id, tf_id, renderer_id, tilemap_id in tilemap_ids:
        lines.append(trace_yaml.game_object_yaml(go_id, [tf_id, renderer_id, tilemap_id], name))
        lines.append(trace_yaml.transform_yaml(tf_id, go_id, grid_tf, [], (0, 0, 0)))
        lines.append(trace_yaml.tilemap_renderer_yaml(renderer_id, go_id, sorting))
        lines.append(trace_yaml.tilemap_yaml(tilemap_id, go_id, cells))

    return "\n".join(lines) + "\n", root_tf, stats


def set_named_objects_active(text: str, states: dict[str, int]) -> str:
    pattern = re.compile(r"--- !u!1 &-?\d+\nGameObject:\n.*?(?=\n--- !u!|\Z)", re.S)
    result: list[str] = []
    last = 0
    for match in pattern.finditer(text):
        block = match.group(0)
        name_match = re.search(r"(?m)^  m_Name: (.+)$", block)
        name = name_match.group(1).strip() if name_match else ""
        if name in states:
            block = re.sub(r"(?m)^(  m_IsActive: )\d+$", rf"\g<1>{states[name]}", block, count=1)
        result.append(text[last : match.start()])
        result.append(block)
        last = match.end()
    result.append(text[last:])
    return "".join(result)


def append_scene(text: str, docs: str, root_tf: int) -> str:
    if ROOT_NAME in text:
        raise RuntimeError(f"{ROOT_NAME} already exists in the scene.")
    scene_roots = re.search(
        r"(?ms)^--- !u!1660057539 &-?\d+\nSceneRoots:\n.*?\n  m_Roots:\n(?:  - \{fileID: -?\d+\}\n)*",
        text,
    )
    if not scene_roots:
        raise RuntimeError("Could not find SceneRoots in target scene.")
    roots_block = scene_roots.group(0).rstrip() + f"\n  - {{fileID: {root_tf}}}\n"
    return text[: scene_roots.start()] + docs + roots_block + text[scene_roots.end() :]


def main() -> int:
    text = read_text(TARGET_SCENE)
    text = set_named_objects_active(
        text,
        {
            "Grid": 0,
            "PIXEL_MATCH_TRACE_TILEMAP_PREFABS": 0,
            "REBUILD_FROM_REFERENCE_ASSETS": 0,
            "REBUILD_TILE_MATCH_FROM_REFERENCE_16PX": 0,
            "PIXEL_MATCH_REFERENCE_ROOT": 1,
            "Reference_Background_100pct": 1,
            "Reference_Overlay_35pct_Toggle_For_Tracing": 0,
        },
    )
    docs, root_tf, stats = build_docs()
    text = append_scene(text, docs, root_tf)
    write_text(TARGET_SCENE, text)
    print(f"target={TARGET_SCENE.relative_to(ROOT)}")
    print(f"root={ROOT_NAME}")
    for key, value in stats.items():
        print(f"{key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
