#!/usr/bin/env python3
"""Генерация school_01..20: украшение поздней кампании + поиск решения. level_*.json не трогает."""
from __future__ import annotations

import copy
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN = ROOT / "Assets" / "Resources" / "Levels"
SCHOOL = CAMPAIGN / "School"

DIRS = ("Up", "Right", "Down", "Left")
STEP = {"Up": (0, 1), "Right": (1, 0), "Down": (0, -1), "Left": (-1, 0)}
OPP = {"Up": "Down", "Right": "Left", "Down": "Up", "Left": "Right"}


def pack(x: int, y: int) -> int:
    return (x << 32) ^ (y & 0xFFFFFFFF)


def step(dir_name: str):
    return STEP[dir_name]


def rotate_dir(dir_name: str, cw: bool) -> str:
    i = DIRS.index(dir_name)
    return DIRS[(i + (1 if cw else 3)) % 4]


def pointer_cells(pivot, length, dir_name):
    dx, dy = step(dir_name)
    return [(pivot[0] + dx * i, pivot[1] + dy * i) for i in range(max(1, length))]


def rotate_cells_cw(cells, pivot, cw: bool):
    px, py = pivot
    out = []
    for x, y in cells:
        dx, dy = x - px, y - py
        if cw:
            out.append((px + dy, py - dx))
        else:
            out.append((px - dy, py + dx))
    return out


def backpack_cells(anchor, dir_name):
    dx, dy = step(dir_name)
    return [tuple(anchor), (anchor[0] + dx, anchor[1] + dy)]


def load_json(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    return json.loads(text)


def arrow_path(a):
    if a.get("pathX") and a.get("pathY") and len(a["pathX"]) == len(a["pathY"]):
        return list(zip(a["pathX"], a["pathY"]))
    return [(a["x"], a["y"])]


def pretty(data: dict) -> str:
    lines = [
        "{",
        f'  "id": {data["id"]},',
        f'  "gridWidth": {data["gridWidth"]},',
        f'  "gridHeight": {data["gridHeight"]},',
        f'  "movesLimit": {data["movesLimit"]},',
    ]
    sol = ", ".join(f'"{s}"' for s in data.get("solution") or [])
    lines.append(f'  "solution": [{sol}],')
    lines.append('  "arrows": [')
    arrows = data.get("arrows") or []
    for i, a in enumerate(arrows):
        parts = [
            f'"id": "{a["id"]}"',
            f'"x": {a["x"]}',
            f'"y": {a["y"]}',
            f'"dir": "{a["dir"]}"',
            f'"color": "{a["color"]}"',
        ]
        if a.get("pathX"):
            parts.append('"pathX": [' + ", ".join(str(v) for v in a["pathX"]) + "]")
            parts.append('"pathY": [' + ", ".join(str(v) for v in a["pathY"]) + "]")
        parents = a.get("lockParents") or []
        if parents:
            parts.append('"lockParents": [' + ", ".join(f'"{p}"' for p in parents) + "]")
        comma = "," if i + 1 < len(arrows) else ""
        lines.append("    { " + ", ".join(parts) + " }" + comma)
    books = [b for b in (data.get("books") or []) if b]
    pointers = [p for p in (data.get("pointers") or []) if p]
    backpacks = [b for b in (data.get("backpacks") or []) if b]
    lines.append("  ],")
    lines.append('  "buttons": [')
    more = bool(books or pointers or backpacks)
    lines.append("  ]," if more else "  ]")

    def emit_array(name, items, last: bool, fmt):
        if not items:
            return
        lines.append(f'  "{name}": [')
        for i, item in enumerate(items):
            comma = "," if i + 1 < len(items) else ""
            lines.append("    { " + fmt(item) + " }" + comma)
        lines.append("  ]," if not last else "  ]")

    emit_array(
        "books",
        books,
        not pointers and not backpacks,
        lambda b: f'"id": "{b["id"]}", "x": {b["x"]}, "y": {b["y"]}, "blockedArrowId": "{b["blockedArrowId"]}", "keyArrowId": "{b["keyArrowId"]}"',
    )
    emit_array(
        "pointers",
        pointers,
        not backpacks,
        lambda p: f'"id": "{p["id"]}", "pivotX": {p["pivotX"]}, "pivotY": {p["pivotY"]}, "length": {p["length"]}, "dir": "{p["dir"]}", "rotateCW": {"true" if p["rotateCW"] else "false"}',
    )
    emit_array(
        "backpacks",
        backpacks,
        True,
        lambda b: f'"id": "{b["id"]}", "x": {b["x"]}, "y": {b["y"]}, "dir": "{b["dir"]}"',
    )
    lines.append("}")
    return "\n".join(lines) + "\n"


def sanitize(data: dict) -> bool:
    arrows = data.get("arrows") or []
    if not arrows:
        return False
    min_x = min_y = 10**9
    max_x = max_y = -10**9

    def touch(x, y):
        nonlocal min_x, min_y, max_x, max_y
        min_x = min(min_x, x)
        min_y = min(min_y, y)
        max_x = max(max_x, x)
        max_y = max(max_y, y)

    for a in arrows:
        path = arrow_path(a)
        if not path:
            return False
        a["x"], a["y"] = path[0]
        a["pathX"] = [p[0] for p in path]
        a["pathY"] = [p[1] for p in path]
        for x, y in path:
            touch(x, y)
    for b in data.get("books") or []:
        touch(b["x"], b["y"])
    for p in data.get("pointers") or []:
        for x, y in pointer_cells((p["pivotX"], p["pivotY"]), p["length"], p["dir"]):
            touch(x, y)
    for b in data.get("backpacks") or []:
        for x, y in backpack_cells((b["x"], b["y"]), b["dir"]):
            touch(x, y)
    if max_x - min_x > 48 or max_y - min_y > 48:
        return False
    dx = -min_x if min_x < 0 else 0
    dy = -min_y if min_y < 0 else 0
    if dx or dy:
        for a in arrows:
            a["x"] += dx
            a["y"] += dy
            a["pathX"] = [v + dx for v in a["pathX"]]
            a["pathY"] = [v + dy for v in a["pathY"]]
        for b in data.get("books") or []:
            b["x"] += dx
            b["y"] += dy
        for p in data.get("pointers") or []:
            p["pivotX"] += dx
            p["pivotY"] += dy
        for b in data.get("backpacks") or []:
            b["x"] += dx
            b["y"] += dy
        max_x += dx
        max_y += dy
    data["gridWidth"] = max(data.get("gridWidth", 1), max_x + 1)
    data["gridHeight"] = max(data.get("gridHeight", 1), max_y + 1)
    return True


class Sim:
    def __init__(self, data: dict, require_props=True):
        self.w = max(1, data["gridWidth"])
        self.h = max(1, data["gridHeight"])
        self.arrows = []
        self.by_id = {}
        for i, src in enumerate(data.get("arrows") or []):
            path = arrow_path(src)
            aid = src.get("id") or f"a{i}"
            arrow = {
                "id": aid,
                "dir": src.get("dir", "Up"),
                "lock": list(src.get("lockParents") or []),
                "path": path,
            }
            if aid in self.by_id:
                raise ValueError("dup id")
            self.arrows.append(arrow)
            self.by_id[aid] = arrow
        self.books = []
        for i, src in enumerate(data.get("books") or []):
            if not src:
                continue
            if src["blockedArrowId"] not in self.by_id or src["keyArrowId"] not in self.by_id:
                if require_props:
                    raise ValueError("bad book")
                continue
            self.books.append(dict(src))
        used = set()
        for a in self.arrows:
            for x, y in a["path"]:
                used.add(pack(x, y))
        for b in self.books:
            used.add(pack(b["x"], b["y"]))
        self.pointers = []
        for i, src in enumerate(data.get("pointers") or []):
            if not src:
                continue
            cells = pointer_cells((src["pivotX"], src["pivotY"]), src["length"], src["dir"])
            ok = True
            for x, y in cells:
                if not (0 <= x < self.w and 0 <= y < self.h) or pack(x, y) in used:
                    ok = False
                    break
                used.add(pack(x, y))
            if not ok:
                if require_props:
                    raise ValueError("bad pointer")
                continue
            self.pointers.append({
                "id": src.get("id") or f"p{i}",
                "pivot": (src["pivotX"], src["pivotY"]),
                "length": src["length"],
                "dir": src["dir"],
                "cw": bool(src.get("rotateCW")),
                "cells": cells,
            })
        self.backpacks = []
        for i, src in enumerate(data.get("backpacks") or []):
            if not src:
                continue
            cells = backpack_cells((src["x"], src["y"]), src["dir"])
            ok = True
            for x, y in cells:
                if not (0 <= x < self.w and 0 <= y < self.h) or pack(x, y) in used:
                    ok = False
                    break
                used.add(pack(x, y))
            if not ok:
                if require_props:
                    raise ValueError("bad backpack")
                continue
            self.backpacks.append({
                "id": src.get("id") or f"bp{i}",
                "anchor": (src["x"], src["y"]),
                "dir": src["dir"],
                "cells": cells,
            })
        authored_p = len([p for p in (data.get("pointers") or []) if p])
        authored_b = len([b for b in (data.get("backpacks") or []) if b])
        authored_k = len([b for b in (data.get("books") or []) if b])
        if require_props and (len(self.pointers) != authored_p or len(self.backpacks) != authored_b or len(self.books) != authored_k):
            raise ValueError("dropped props")

    def locked(self, arrow) -> bool:
        for p in arrow["lock"]:
            if p and p in self.by_id:
                return True
        for b in self.books:
            if b["blockedArrowId"] != arrow["id"]:
                continue
            if b["keyArrowId"] in self.by_id:
                return True
        return False

    def occupied(self, skip_pack=None):
        occ = set()
        alive = {a["id"] for a in self.arrows}
        for a in self.arrows:
            for x, y in a["path"]:
                occ.add(pack(x, y))
        for b in self.books:
            if b["keyArrowId"] in alive:
                occ.add(pack(b["x"], b["y"]))
        for p in self.pointers:
            for x, y in p["cells"]:
                occ.add(pack(x, y))
        for bp in self.backpacks:
            if bp is skip_pack:
                continue
            for x, y in bp["cells"]:
                occ.add(pack(x, y))
        return occ

    def path_clear(self, arrow) -> bool:
        path = arrow["path"]
        if not path:
            return False
        self_cells = {pack(x, y) for x, y in path}
        occ = self.occupied()
        dx, dy = step(arrow["dir"])
        cx, cy = path[-1][0] + dx, path[-1][1] + dy
        while 0 <= cx < self.w and 0 <= cy < self.h:
            key = pack(cx, cy)
            if key in occ and key not in self_cells:
                return False
            cx += dx
            cy += dy
        return True

    def rotate_pointers(self):
        occ = self.occupied()
        for p in self.pointers:
            for x, y in p["cells"]:
                occ.add(pack(x, y))
        for p in self.pointers:
            nxt = rotate_cells_cw(p["cells"], p["pivot"], p["cw"])
            can = len(nxt) == len(p["cells"])
            if can:
                for cell in nxt:
                    x, y = cell
                    if not (0 <= x < self.w and 0 <= y < self.h):
                        can = False
                        break
                    key = pack(x, y)
                    if key in occ and cell not in p["cells"]:
                        can = False
                        break
            if not can:
                continue
            for x, y in p["cells"]:
                occ.discard(pack(x, y))
            p["dir"] = rotate_dir(p["dir"], p["cw"])
            p["cells"] = nxt
            for x, y in nxt:
                occ.add(pack(x, y))

    def slide(self, pack_obj, plus: bool) -> bool:
        d = pack_obj["dir"] if plus else OPP[pack_obj["dir"]]
        dx, dy = step(d)
        new_anchor = (pack_obj["anchor"][0] + dx, pack_obj["anchor"][1] + dy)
        nxt = backpack_cells(new_anchor, pack_obj["dir"])
        occ = self.occupied(skip_pack=pack_obj)
        for x, y in nxt:
            if not (0 <= x < self.w and 0 <= y < self.h):
                return False
            if pack(x, y) in occ:
                return False
        pack_obj["anchor"] = new_anchor
        pack_obj["cells"] = nxt
        return True

    def legal(self):
        return [a for a in self.arrows if not self.locked(a) and self.path_clear(a)]

    def play_tokens(self, tokens) -> bool:
        expected = len(self.arrows)
        played = 0
        bp_by = {b["id"]: b for b in self.backpacks}
        for token in tokens:
            if ":" in token:
                pid, sign = token.rsplit(":", 1)
                if sign not in "+-" or pid not in bp_by:
                    return False
                if not self.slide(bp_by[pid], sign == "+"):
                    return False
                continue
            if token not in self.by_id:
                return False
            arrow = self.by_id[token]
            if arrow not in self.arrows:
                return False
            if self.locked(arrow) or not self.path_clear(arrow):
                return False
            self.arrows.remove(arrow)
            del self.by_id[token]
            self.rotate_pointers()
            played += 1
        return not self.arrows and played == expected

    def start_free(self) -> int:
        return sum(1 for a in self.arrows if not self.locked(a))

    def state_key(self):
        ids = tuple(sorted(a["id"] for a in self.arrows))
        pd = tuple(p["dir"] for p in self.pointers)
        bd = tuple((b["anchor"], b["dir"]) for b in self.backpacks)
        return (ids, pd, bd)

    def solve(self, preferred, node_limit=25000):
        tokens = []
        visited = set()
        nodes = [0]

        def rec() -> bool:
            if not self.arrows:
                return True
            nodes[0] += 1
            if nodes[0] > node_limit:
                return False
            key = self.state_key()
            if key in visited:
                return False
            visited.add(key)
            legal = self.legal()
            peel = None
            for pid in preferred:
                if pid in self.by_id:
                    peel = self.by_id[pid]
                    break

            def play_arrow(arrow) -> bool:
                poses = [(p["dir"], list(p["cells"])) for p in self.pointers]
                self.arrows.remove(arrow)
                del self.by_id[arrow["id"]]
                self.rotate_pointers()
                tokens.append(arrow["id"])
                ok = rec()
                if ok:
                    return True
                tokens.pop()
                for p, pose in zip(self.pointers, poses):
                    p["dir"], p["cells"] = pose
                self.arrows.append(arrow)
                self.by_id[arrow["id"]] = arrow
                return False

            if peel is not None and peel in legal:
                if play_arrow(peel):
                    return True
            for bp in self.backpacks:
                for plus in (True, False):
                    old_a, old_c = bp["anchor"], list(bp["cells"])
                    if not self.slide(bp, plus):
                        continue
                    tokens.append(bp["id"] + (":+" if plus else ":-"))
                    if rec():
                        return True
                    tokens.pop()
                    bp["anchor"] = old_a
                    bp["cells"] = old_c
            for arrow in legal:
                if peel is not None and arrow["id"] == peel["id"]:
                    continue
                if play_arrow(arrow):
                    return True
            return False

        ok = rec()
        return tokens if ok else None


def path_cells(data):
    used = set()
    for a in data.get("arrows") or []:
        for x, y in arrow_path(a):
            used.add((x, y))
    return used


def auto_books(data):
    w, h = data["gridWidth"], data["gridHeight"]
    used = path_cells(data)
    by_id = {a["id"]: a for a in data["arrows"]}
    placed = []
    used_book = set()
    for a in data["arrows"]:
        parents = a.get("lockParents") or []
        if not parents:
            continue
        key = parents[0]
        if key not in by_id or key == a["id"]:
            continue
        path = arrow_path(a)
        dx, dy = step(a["dir"])
        cx, cy = path[-1][0] + dx, path[-1][1] + dy
        found = None
        while 0 <= cx < w and 0 <= cy < h:
            if (cx, cy) not in used and (cx, cy) not in used_book:
                found = (cx, cy)
                break
            cx += dx
            cy += dy
        if not found:
            continue
        used_book.add(found)
        placed.append({
            "id": f"book_{a['id']}",
            "x": found[0],
            "y": found[1],
            "blockedArrowId": a["id"],
            "keyArrowId": key,
        })
    return placed


def densify_locks(data, want, min_free, rng):
    arrows = data["arrows"]
    locked = sum(1 for a in arrows if a.get("lockParents"))
    order = list(range(1, len(arrows)))
    rng.shuffle(order)
    for idx in order:
        if locked >= want:
            break
        child = arrows[idx]
        if child.get("lockParents"):
            continue
        parent = arrows[rng.randrange(0, idx)]
        if parent["id"] == child["id"]:
            continue
        child["lockParents"] = [parent["id"]]
        locked += 1
    free = sum(1 for a in arrows if not a.get("lockParents"))
    for a in reversed(arrows):
        if free >= min_free:
            break
        if not a.get("lockParents"):
            continue
        a["lockParents"] = []
        free += 1


def static_used(data, pointers=True, backpacks=True):
    used = path_cells(data)
    for b in data.get("books") or []:
        used.add((b["x"], b["y"]))
    if pointers:
        for p in data.get("pointers") or []:
            used.update(pointer_cells((p["pivotX"], p["pivotY"]), p["length"], p["dir"]))
    if backpacks:
        for b in data.get("backpacks") or []:
            used.update(backpack_cells((b["x"], b["y"]), b["dir"]))
    return used


def exit_rays(data, skip_first=2):
    w, h = data["gridWidth"], data["gridHeight"]
    used = path_cells(data)
    rays = set()
    arrows = data["arrows"]
    start = min(skip_first, len(arrows))
    for a in arrows[start:]:
        path = arrow_path(a)
        dx, dy = step(a["dir"])
        cx, cy = path[-1][0] + dx, path[-1][1] + dy
        while 0 <= cx < w and 0 <= cy < h:
            if (cx, cy) not in used:
                rays.add((cx, cy))
            cx += dx
            cy += dy
    return rays


def pointer_candidates(data):
    w, h = data["gridWidth"], data["gridHeight"]
    used = static_used(data, False, False)
    rays = exit_rays(data, 2)
    hits, rest = [], []
    for y in range(h):
        for x in range(w):
            if (x, y) in used:
                continue
            for d in DIRS:
                for length in (3, 4, 5):
                    cells = pointer_cells((x, y), length, d)
                    if any(not (0 <= cx < w and 0 <= cy < h) or (cx, cy) in used for cx, cy in cells):
                        continue
                    item = {
                        "id": "p1",
                        "pivotX": x,
                        "pivotY": y,
                        "length": length,
                        "dir": d,
                        "rotateCW": ((x + y + length) % 2 == 0),
                    }
                    if any(c in rays for c in cells):
                        hits.append(item)
                    else:
                        rest.append(item)
    return hits + rest


def backpack_candidates(data):
    w, h = data["gridWidth"], data["gridHeight"]
    used = static_used(data, True, False)
    rays = exit_rays(data, 2)
    out = []
    for rx, ry in rays:
        if (rx, ry) in used:
            continue
        for d in DIRS:
            cells = backpack_cells((rx, ry), d)
            if any(not (0 <= x < w and 0 <= y < h) or (x, y) in used for x, y in cells):
                continue
            if not any(c in rays for c in cells):
                continue
            out.append({"id": "bp1", "x": rx, "y": ry, "dir": d})
    return out


def specs(school_id: int):
    if school_id <= 3:
        return dict(want_pointer=False, optional_pointer=False, want_backpack=False, min_books=3, min_locks=5, slack=(0, 2))
    if school_id <= 7:
        return dict(want_pointer=True, optional_pointer=False, want_backpack=False, min_books=3, min_locks=5, slack=(0, 2))
    if school_id <= 10:
        return dict(want_pointer=False, optional_pointer=False, want_backpack=True, min_books=4, min_locks=6, slack=(0, 1))
    if school_id <= 12:
        return dict(want_pointer=False, optional_pointer=True, want_backpack=True, min_books=4, min_locks=6, slack=(0, 1))
    return dict(want_pointer=True, optional_pointer=False, want_backpack=True, min_books=5, min_locks=6, slack=(0, 1))


def is_solvable(data) -> bool:
    try:
        sim = Sim(data, True)
    except ValueError:
        return False
    return sim.play_tokens(data.get("solution") or [])


def backpack_required(data) -> bool:
    sol = data.get("solution") or []
    if not any(":" in t for t in sol):
        return False
    clone = copy.deepcopy(data)
    clone["solution"] = [t for t in sol if ":" not in t]
    return not is_solvable(clone)


def validate(data, school_id) -> str | None:
    if not sanitize(data):
        return "bounds"
    if not is_solvable(data):
        return "solvable"
    try:
        free = Sim(data, True).start_free()
    except ValueError as e:
        return str(e)
    if free < 2:
        return "free"
    books = len(data.get("books") or [])
    pointers = len(data.get("pointers") or [])
    backpacks = len(data.get("backpacks") or [])
    if school_id <= 3:
        if books < 3:
            return "books"
        if pointers or backpacks:
            return "extra props"
    elif school_id <= 7:
        if books < 3 or pointers != 1 or backpacks:
            return "4-7 shape"
    elif school_id <= 10:
        if books < 3 or pointers or backpacks < 1:
            return "8-10 shape"
        if not backpack_required(data):
            return "bp deco"
    elif school_id <= 12:
        if books < 3 or backpacks < 1:
            return "11-12 shape"
        if not backpack_required(data):
            return "bp deco"
    else:
        if books < 3 or pointers < 1 or backpacks < 1:
            return "13-20 shape"
        if not backpack_required(data):
            return "bp deco"
    return None


def commit(data, school_id, spec, preferred):
    if not sanitize(data):
        return None
    try:
        sim = Sim(data, True)
    except ValueError:
        return None
    tokens = sim.solve(preferred)
    if not tokens:
        return None
    data["solution"] = tokens
    lo, hi = spec["slack"]
    data["movesLimit"] = len(data["arrows"]) + (lo if hi <= lo else (school_id % (hi - lo + 1) + lo))
    err = validate(data, school_id)
    return data if err is None else None


def decorate(base, school_id, rng) -> dict | None:
    spec = specs(school_id)
    data = copy.deepcopy(base)
    data["id"] = school_id
    data["buttons"] = []
    data["pointers"] = []
    data["backpacks"] = []
    densify_locks(data, spec["min_locks"], 2, rng)
    data["books"] = auto_books(data)
    if len(data["books"]) < spec["min_books"]:
        return None
    preferred = list(data.get("solution") or [a["id"] for a in data["arrows"]])
    pointer_cands = pointer_candidates(data) if (spec["want_pointer"] or spec["optional_pointer"]) else []
    rng.shuffle(pointer_cands)

    def with_backpacks(cur):
        if not spec["want_backpack"]:
            cur["backpacks"] = []
            return commit(cur, school_id, spec, preferred)
        cands = backpack_candidates(cur)
        rng.shuffle(cands)
        for bp in cands[:8]:
            trial = copy.deepcopy(cur)
            trial["backpacks"] = [bp]
            done = commit(trial, school_id, spec, preferred)
            if done:
                return done
        return None

    if spec["want_pointer"] or spec["optional_pointer"]:
        for p in pointer_cands[:8]:
            trial = copy.deepcopy(data)
            trial["pointers"] = [p]
            done = with_backpacks(trial)
            if done:
                return done
        if spec["want_pointer"] and not spec["optional_pointer"]:
            return None
        data["pointers"] = []
        return with_backpacks(data)

    data["pointers"] = []
    return with_backpacks(data)


def campaign_files():
    files = sorted(CAMPAIGN.glob("level_*.json"))
    dense = []
    for f in files:
        data = load_json(f)
        n = len(data.get("arrows") or [])
        if data.get("id", 0) >= 70 and n >= 16:
            dense.append((n, f, data))
    dense.sort(reverse=True)
    return dense


def generate_one(school_id: int, dense, seed: int):
    rng = random.Random(seed)
    # rotate through densest campaign levels
    for offset in range(min(6, len(dense))):
        _, path, base = dense[(school_id * 3 + offset) % len(dense)]
        for extra in range(4):
            rng.seed(seed + extra * 997 + offset * 13)
            got = decorate(base, school_id, rng)
            if got:
                return got, path.name
    return None, None


def main():
    SCHOOL.mkdir(parents=True, exist_ok=True)
    dense = campaign_files()
    if len(dense) < 10:
        print("not enough campaign seeds", file=sys.stderr)
        return 1
    failed = []
    for i in range(1, 21):
        data, src = generate_one(i, dense, 11003 * i + 9176)
        if not data:
            failed.append(i)
            print(f"FAIL school_{i:02d}")
            continue
        out = SCHOOL / f"school_{i:02d}.json"
        out.write_text(pretty(data), encoding="utf-8")
        n = len(data["arrows"])
        b = len(data.get("books") or [])
        p = len(data.get("pointers") or [])
        k = len(data.get("backpacks") or [])
        print(f"OK school_{i:02d} arrows={n} books={b} pointers={p} backpacks={k} moves={data['movesLimit']} src={src} sol={len(data['solution'])}")
    # re-validate all
    print("--- validate ---")
    for i in range(1, 21):
        path = SCHOOL / f"school_{i:02d}.json"
        data = load_json(path)
        err = validate(data, i)
        print(f"school_{i:02d}: {err or 'ok'}")
        if err:
            failed.append(i)
    if failed:
        print("FAILED", sorted(set(failed)))
        return 1
    print("ALL 20 SOLVABLE")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
