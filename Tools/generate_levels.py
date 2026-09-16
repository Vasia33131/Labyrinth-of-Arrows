#!/usr/bin/env python3
"""Port of Unpuzzle LevelFactory — writes Resources/Levels/level_XX.json."""
from __future__ import annotations

import json
import random
from dataclasses import dataclass, field
from pathlib import Path

UP, RIGHT, DOWN, LEFT = 0, 1, 2, 3
DIR_NAME = {0: "Up", 1: "Right", 2: "Down", 3: "Left"}
COLOR_NAME = {0: "Grey", 1: "Red", 2: "Blue", 3: "Green", 4: "Yellow"}
ROT_NAME = {0: "Rotate90CW", 1: "Rotate90CCW", 2: "Rotate180"}
ROT_STEPS = {0: 1, 1: 3, 2: 2}

GREY, RED, BLUE, GREEN, YELLOW = 0, 1, 2, 3, 4

L_SHAPES = [
    [(0, 0), (0, 1), (1, 1)],
    [(0, 0), (1, 0), (0, 1)],
    [(0, 0), (1, 0), (1, 1)],
    [(1, 0), (0, 1), (1, 1)],
    [(0, 0), (0, 1), (0, 2), (1, 2)],
    [(0, 0), (1, 0), (2, 0), (2, 1)],
]
PI_SHAPES = [
    [(0, 0), (2, 0), (0, 1), (1, 1), (2, 1)],
    [(0, 0), (1, 0), (2, 0), (0, 1), (2, 1)],
    [(0, 1), (1, 1), (2, 1), (1, 0)],
    [(0, 0), (1, 0), (2, 0), (1, 1)],
]


def pack(x: int, y: int) -> int:
    return (x << 32) ^ (y & 0xFFFFFFFF)


def step(d: int) -> tuple[int, int]:
    return ((0, 1), (1, 0), (0, -1), (-1, 0))[d]


def rotate(d: int, rotation: int) -> int:
    return (d + ROT_STEPS[rotation]) % 4


@dataclass
class Spec:
    min_arrows: int
    max_arrows: int
    width: int
    height: int
    locks: bool = False
    colors: bool = False
    buttons: bool = False
    shapes: bool = False
    use_pi: bool = False
    weave: bool = False
    strict: bool = False
    extra: bool = False
    shape_count: int = 0
    min_locks: int = 0
    max_locks: int = 0
    min_buttons: int = 0
    max_buttons: int = 0
    min_slack: int = 0
    max_slack: int = 0
    blue_pct: int = 0
    red_pct: int = 0
    weave_chance: float = 0.0


def spec_for(level_id: int) -> Spec:
    if level_id <= 10:
        return Spec(3, 5, 5, 6, weave=True, weave_chance=0.45, min_slack=5, max_slack=8)
    if level_id <= 30:
        return Spec(8, 15, 5, 7, locks=True, shapes=True, shape_count=1 if level_id <= 20 else 2,
                    min_locks=2, max_locks=5, weave=True, weave_chance=0.65, min_slack=3, max_slack=5)
    if level_id <= 60:
        return Spec(15, 25, 6, 8, locks=True, colors=True, buttons=True, shapes=True, use_pi=True,
                    shape_count=3, min_locks=4, max_locks=8, min_buttons=1, max_buttons=3,
                    weave=True, weave_chance=0.75, min_slack=1, max_slack=3, blue_pct=35, red_pct=25)
    return Spec(25, 40, 6, 9, locks=True, colors=True, buttons=True, shapes=True, use_pi=True,
                extra=True, strict=True, shape_count=5, min_locks=8, max_locks=16,
                min_buttons=2, max_buttons=4, weave=True, weave_chance=0.9,
                min_slack=0, max_slack=1, blue_pct=30, red_pct=25)


@dataclass
class Arrow:
    id: str
    x: int
    y: int
    dir: int
    color: int = GREY
    lock_parents: list[str] = field(default_factory=list)


@dataclass
class Button:
    x: int
    y: int
    rotation: int
    one_shot: bool
    once_per: bool
    filter_color: bool
    target: int


def path_clear(x: int, y: int, d: int, occ: set[int], w: int, h: int) -> bool:
    dx, dy = step(d)
    cx, cy = x + dx, y + dy
    while 0 <= cx < w and 0 <= cy < h:
        if pack(cx, cy) in occ:
            return False
        cx += dx
        cy += dy
    return True


def collect_empty(occ: set[int], w: int, h: int) -> list[tuple[int, int]]:
    return [(x, y) for y in range(h) for x in range(w) if pack(x, y) not in occ]


def blocking_cells(placed: list[Arrow], occ: set[int], w: int, h: int) -> list[tuple[int, int]]:
    seen: set[int] = set()
    out: list[tuple[int, int]] = []
    for a in placed:
        dx, dy = step(a.dir)
        cx, cy = a.x + dx, a.y + dy
        while 0 <= cx < w and 0 <= cy < h:
            key = pack(cx, cy)
            if key in occ:
                break
            if key not in seen:
                seen.add(key)
                out.append((cx, cy))
            cx += dx
            cy += dy
    return out


def place(placed: list[Arrow], occ: set[int], x: int, y: int, d: int) -> None:
    occ.add(pack(x, y))
    placed.append(Arrow(id=f"a{len(placed)}", x=x, y=y, dir=d))


def try_place_single(placed: list[Arrow], occ: set[int], w: int, h: int, rng: random.Random, spec: Spec) -> bool:
    empty = collect_empty(occ, w, h)
    hot = blocking_cells(placed, occ, w, h)
    if spec.weave and hot and rng.random() < spec.weave_chance:
        rng.shuffle(hot)
        rng.shuffle(empty)
        order = hot + [c for c in empty if c not in set(hot)]
    else:
        rng.shuffle(empty)
        order = empty
    dirs = [UP, RIGHT, DOWN, LEFT]
    for x, y in order:
        rng.shuffle(dirs)
        for d in dirs:
            if path_clear(x, y, d, occ, w, h):
                place(placed, occ, x, y, d)
                return True
    return False


def shape_fits(shape, ox, oy, occ, w, h) -> bool:
    for sx, sy in shape:
        x, y = ox + sx, oy + sy
        if not (0 <= x < w and 0 <= y < h) or pack(x, y) in occ:
            return False
    return True


def try_place_shape(placed: list[Arrow], occ: set[int], w: int, h: int, rng: random.Random, spec: Spec) -> bool:
    catalog = PI_SHAPES if spec.use_pi and rng.random() < 0.45 else L_SHAPES
    shape = catalog[rng.randrange(len(catalog))]
    origins = collect_empty(occ, w, h)
    rng.shuffle(origins)
    dirs = [UP, RIGHT, DOWN, LEFT]
    for ox, oy in origins:
        if not shape_fits(shape, ox, oy, occ, w, h):
            continue
        cells = [(ox + sx, oy + sy) for sx, sy in shape]
        rng.shuffle(cells)
        planned: list[Arrow] = []
        temp = set(occ)
        ok = True
        for x, y in cells:
            found = None
            rng.shuffle(dirs)
            for d in dirs:
                if path_clear(x, y, d, temp, w, h):
                    found = d
                    break
            if found is None:
                ok = False
                break
            planned.append(Arrow(id=f"a{len(placed) + len(planned)}", x=x, y=y, dir=found))
            temp.add(pack(x, y))
        if not ok:
            continue
        for a in planned:
            occ.add(pack(a.x, a.y))
            placed.append(a)
        return True
    return False


def apply_locks(solution: list[Arrow], spec: Spec, rng: random.Random) -> None:
    if not spec.locks or len(solution) < 2:
        return
    lock_count = min(spec.max_locks, len(solution) - 1)
    lock_count = rng.randint(min(spec.min_locks, lock_count), lock_count) if lock_count else 0
    locked: set[str] = set()
    if spec.strict:
        chain = min(lock_count, len(solution) - 1)
        for i in range(1, chain + 1):
            solution[i].lock_parents.append(solution[i - 1].id)
            locked.add(solution[i].id)
        lock_count -= chain
    for _ in range(lock_count):
        child_i = rng.randrange(1, len(solution))
        child = solution[child_i]
        if child.id in locked and rng.random() < 0.7:
            continue
        if spec.strict:
            parent_i = max(0, child_i - 1 - rng.randrange(0, min(3, child_i) or 1))
        else:
            parent_i = rng.randrange(0, child_i)
        pid = solution[parent_i].id
        if pid not in child.lock_parents:
            child.lock_parents.append(pid)
            locked.add(child.id)


def apply_colors(solution: list[Arrow], spec: Spec, rng: random.Random) -> None:
    if not spec.colors or not solution:
        return
    blue = max(1, len(solution) * spec.blue_pct // 100)
    red = max(1, len(solution) * spec.red_pct // 100)
    if blue + red > len(solution):
        red = max(1, len(solution) - blue)
    for i, a in enumerate(solution):
        if i < blue:
            a.color = GREY if rng.random() < 0.18 else BLUE
        elif len(solution) - i <= red:
            a.color = RED
        elif spec.extra and rng.random() < 0.35:
            a.color = GREEN if rng.random() < 0.5 else YELLOW
        else:
            a.color = GREY


def place_buttons(occ: set[int], w: int, h: int, spec: Spec, rng: random.Random) -> list[Button]:
    if not spec.buttons:
        return []
    empty = collect_empty(occ, w, h)
    if not empty:
        return []
    rng.shuffle(empty)
    count = min(rng.randint(spec.min_buttons, spec.max_buttons), len(empty))
    out = []
    for i in range(count):
        x, y = empty[i]
        out.append(Button(
            x=x, y=y, rotation=rng.choice((0, 1, 2)),
            one_shot=rng.random() < 0.4, once_per=True,
            filter_color=spec.colors and rng.random() < 0.35,
            target=BLUE if rng.random() < 0.5 else RED,
        ))
    return out


def to_level(level_id: int, spec: Spec, solution: list[Arrow], buttons: list[Button]) -> dict:
    slack = spec.min_slack if spec.max_slack <= spec.min_slack else spec.min_slack + (len(solution) % (spec.max_slack - spec.min_slack + 1))
    arrows = []
    for a in solution:
        entry = {"id": a.id, "x": a.x, "y": a.y, "dir": DIR_NAME[a.dir], "color": COLOR_NAME[a.color]}
        if a.lock_parents:
            entry["lockParents"] = list(a.lock_parents)
        arrows.append(entry)
    button_data = []
    for b in buttons:
        entry = {
            "x": b.x, "y": b.y, "rotation": ROT_NAME[b.rotation],
            "oneShot": b.one_shot, "oncePerArrow": b.once_per,
        }
        if b.filter_color:
            entry["affectOnlyTargetColor"] = True
            entry["targetColor"] = COLOR_NAME[b.target]
        button_data.append(entry)
    return {
        "id": level_id,
        "gridWidth": spec.width,
        "gridHeight": spec.height,
        "movesLimit": max(1, len(solution) + slack),
        "solution": [a.id for a in solution],
        "arrows": arrows,
        "buttons": button_data,
    }


def fallback(level_id: int, spec: Spec) -> dict:
    n = spec.min_arrows
    arrows = [{"id": f"a{i}", "x": i, "y": 0, "dir": "Down", "color": "Grey"} for i in range(n)]
    return {
        "id": level_id, "gridWidth": max(spec.width, n), "gridHeight": max(3, spec.height),
        "movesLimit": n + 8, "solution": [f"a{i}" for i in range(n)],
        "arrows": arrows, "buttons": [],
    }


def is_solvable(data: dict) -> bool:
    arrows = []
    by_id = {}
    for i, src in enumerate(data["arrows"]):
        aid = src.get("id") or f"a{i}"
        sim = {
            "id": aid, "x": src["x"], "y": src["y"],
            "dir": {v: k for k, v in DIR_NAME.items()}[src["dir"]],
            "color": {v: k for k, v in COLOR_NAME.items()}[src.get("color", "Grey")],
            "locks": list(src.get("lockParents") or []),
            "used": set(),
        }
        if aid in by_id:
            return False
        arrows.append(sim)
        by_id[aid] = sim
    buttons = []
    for src in data.get("buttons") or []:
        buttons.append({
            "x": src["x"], "y": src["y"],
            "rot": {v: k for k, v in ROT_NAME.items()}[src.get("rotation", "Rotate90CW")],
            "one": src.get("oneShot", False),
            "once": src.get("oncePerArrow", True),
            "filter": src.get("affectOnlyTargetColor", False),
            "target": {v: k for k, v in COLOR_NAME.items()}[src.get("targetColor", "Red")],
            "used": False,
        })
    order = data.get("solution") or [a["id"] for a in arrows]
    if len(order) != len(arrows):
        return False
    w, h = data["gridWidth"], data["gridHeight"]

    def locked(a) -> bool:
        return any(p in by_id for p in a["locks"])

    def color_ok(c) -> bool:
        return c != RED or all(x["color"] != BLUE for x in arrows)

    def path_ok(a) -> bool:
        occ = {pack(x["x"], x["y"]) for x in arrows}
        dx, dy = step(a["dir"])
        cx, cy = a["x"] + dx, a["y"] + dy
        while 0 <= cx < w and 0 <= cy < h:
            if pack(cx, cy) in occ:
                return False
            cx += dx
            cy += dy
        return True

    def gravity(rx, ry):
        col = sorted((a for a in arrows if a["x"] == rx and a["y"] > ry), key=lambda a: a["y"])
        expected = ry + 1
        for a in col:
            if a["y"] != expected:
                break
            new_y = a["y"] - 1
            for i, b in enumerate(buttons):
                if b["used"] and b["one"]:
                    continue
                if b["x"] != rx or b["y"] != new_y:
                    continue
                if b["filter"] and a["color"] != b["target"]:
                    continue
                if b["once"] and i in a["used"]:
                    continue
                a["used"].add(i)
                a["dir"] = rotate(a["dir"], b["rot"])
                b["used"] = True
                if b["once"]:
                    break
            a["y"] = new_y
            expected = a["y"] + 2

    for aid in order:
        a = by_id.get(aid)
        if a is None or a not in arrows or locked(a) or not color_ok(a["color"]) or not path_ok(a):
            return False
        rx, ry = a["x"], a["y"]
        arrows.remove(a)
        del by_id[aid]
        gravity(rx, ry)
    return not arrows


def try_generate(level_id: int, spec: Spec, rng: random.Random) -> dict | None:
    w, h = spec.width, spec.height
    target = rng.randint(spec.min_arrows, spec.max_arrows)
    occ: set[int] = set()
    placed: list[Arrow] = []
    if spec.shapes:
        for _ in range(spec.shape_count):
            try_place_shape(placed, occ, w, h, rng, spec)
    fail = 0
    while len(placed) < target:
        if try_place_single(placed, occ, w, h, rng, spec):
            fail = 0
            continue
        fail += 1
        if fail < 48:
            continue
        if len(placed) >= spec.min_arrows:
            break
        return None
    if len(placed) < spec.min_arrows:
        return None
    solution = list(reversed(placed))
    apply_locks(solution, spec, rng)
    apply_colors(solution, spec, rng)
    buttons = place_buttons(occ, w, h, spec, rng)
    data = to_level(level_id, spec, solution, buttons)
    if is_solvable(data):
        return data
    data["buttons"] = []
    return data if is_solvable(data) else None


def generate(level_id: int) -> tuple[dict, bool]:
    spec = spec_for(level_id)
    rng = random.Random(level_id * 7919 + 42)
    for _ in range(80):
        data = try_generate(level_id, spec, rng)
        if data:
            return data, False
    return fallback(level_id, spec), True


def main() -> None:
    out_dir = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "Levels"
    out_dir.mkdir(parents=True, exist_ok=True)
    fallbacks = 0
    for i in range(1, 101):
        data, fb = generate(i)
        if fb:
            fallbacks += 1
        path = out_dir / f"level_{i:02d}.json"
        path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"{path.name}: arrows={len(data['arrows'])} moves={data['movesLimit']} buttons={len(data['buttons'])} fallback={fb}")
    print(f"done, fallbacks={fallbacks}")


if __name__ == "__main__":
    main()
