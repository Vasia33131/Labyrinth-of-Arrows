#!/usr/bin/env node
/** Генерация school_01..20. Кампанию level_*.json не трогает. */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const CAMPAIGN = path.join(ROOT, "Assets", "Resources", "Levels");
const SCHOOL = path.join(CAMPAIGN, "School");

const DIRS = ["Up", "Right", "Down", "Left"];
const STEP = { Up: [0, 1], Right: [1, 0], Down: [0, -1], Left: [-1, 0] };
const OPP = { Up: "Down", Right: "Left", Down: "Up", Left: "Right" };

function key(x, y) { return `${x},${y}`; }
function step(dir) { return STEP[dir]; }
function rotateDir(dir, cw) {
  const i = DIRS.indexOf(dir);
  return DIRS[(i + (cw ? 1 : 3)) % 4];
}
function pointerCells(px, py, length, dir) {
  const [dx, dy] = step(dir);
  const n = Math.max(1, length);
  const cells = [];
  for (let i = 0; i < n; i++) cells.push([px + dx * i, py + dy * i]);
  return cells;
}
function rotateCells(cells, pivot, cw) {
  const [px, py] = pivot;
  return cells.map(([x, y]) => {
    const dx = x - px, dy = y - py;
    return cw ? [px + dy, py - dx] : [px - dy, py + dx];
  });
}
function backpackCells(ax, ay, dir) {
  const [dx, dy] = step(dir);
  return [[ax, ay], [ax + dx, ay + dy]];
}
function arrowPath(a) {
  if (a.pathX && a.pathY && a.pathX.length === a.pathY.length && a.pathX.length > 0) {
    return a.pathX.map((x, i) => [x, a.pathY[i]]);
  }
  return [[a.x, a.y]];
}

function mulberry32(seed) {
  let t = seed >>> 0;
  return () => {
    t += 0x6D2B79F5;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
  };
}
function rngInt(rng, n) { return Math.floor(rng() * n); }
function shuffle(arr, rng) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = rngInt(rng, i + 1);
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

function pretty(data) {
  const lines = [
    "{",
    `  "id": ${data.id},`,
    `  "gridWidth": ${data.gridWidth},`,
    `  "gridHeight": ${data.gridHeight},`,
    `  "movesLimit": ${data.movesLimit},`,
  ];
  const sol = (data.solution || []).map((s) => `"${s}"`).join(", ");
  lines.push(`  "solution": [${sol}],`);
  lines.push('  "arrows": [');
  const arrows = data.arrows || [];
  arrows.forEach((a, i) => {
    const parts = [
      `"id": "${a.id}"`,
      `"x": ${a.x}`,
      `"y": ${a.y}`,
      `"dir": "${a.dir}"`,
      `"color": "${a.color}"`,
    ];
    if (a.pathX) {
      parts.push(`"pathX": [${a.pathX.join(", ")}]`);
      parts.push(`"pathY": [${a.pathY.join(", ")}]`);
    }
    const parents = a.lockParents || [];
    if (parents.length) parts.push(`"lockParents": [${parents.map((p) => `"${p}"`).join(", ")}]`);
    lines.push("    { " + parts.join(", ") + " }" + (i + 1 < arrows.length ? "," : ""));
  });
  const books = (data.books || []).filter(Boolean);
  const pointers = (data.pointers || []).filter(Boolean);
  const backpacks = (data.backpacks || []).filter(Boolean);
  lines.push("  ],");
  lines.push('  "buttons": [');
  const more = books.length || pointers.length || backpacks.length;
  lines.push(more ? "  ]," : "  ]");
  const emit = (name, items, last, fmt) => {
    if (!items.length) return;
    lines.push(`  "${name}": [`);
    items.forEach((item, i) => {
      lines.push("    { " + fmt(item) + " }" + (i + 1 < items.length ? "," : ""));
    });
    lines.push(last ? "  ]" : "  ],");
  };
  emit("books", books, !pointers.length && !backpacks.length, (b) =>
    `"id": "${b.id}", "x": ${b.x}, "y": ${b.y}, "blockedArrowId": "${b.blockedArrowId}", "keyArrowId": "${b.keyArrowId}"`);
  emit("pointers", pointers, !backpacks.length, (p) =>
    `"id": "${p.id}", "pivotX": ${p.pivotX}, "pivotY": ${p.pivotY}, "length": ${p.length}, "dir": "${p.dir}", "rotateCW": ${p.rotateCW ? "true" : "false"}`);
  emit("backpacks", backpacks, true, (b) =>
    `"id": "${b.id}", "x": ${b.x}, "y": ${b.y}, "dir": "${b.dir}"`);
  lines.push("}");
  return lines.join("\n") + "\n";
}

function sanitize(data) {
  const arrows = data.arrows || [];
  if (!arrows.length) return false;
  let minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
  const touch = (x, y) => { minX = Math.min(minX, x); minY = Math.min(minY, y); maxX = Math.max(maxX, x); maxY = Math.max(maxY, y); };
  for (const a of arrows) {
    const p = arrowPath(a);
    if (!p.length) return false;
    a.x = p[0][0]; a.y = p[0][1];
    a.pathX = p.map((c) => c[0]);
    a.pathY = p.map((c) => c[1]);
    p.forEach(([x, y]) => touch(x, y));
  }
  for (const b of data.books || []) touch(b.x, b.y);
  for (const p of data.pointers || []) pointerCells(p.pivotX, p.pivotY, p.length, p.dir).forEach(([x, y]) => touch(x, y));
  for (const b of data.backpacks || []) backpackCells(b.x, b.y, b.dir).forEach(([x, y]) => touch(x, y));
  if (maxX - minX > 48 || maxY - minY > 48) return false;
  const dx = minX < 0 ? -minX : 0;
  const dy = minY < 0 ? -minY : 0;
  if (dx || dy) {
    for (const a of arrows) {
      a.x += dx; a.y += dy;
      a.pathX = a.pathX.map((v) => v + dx);
      a.pathY = a.pathY.map((v) => v + dy);
    }
    for (const b of data.books || []) { b.x += dx; b.y += dy; }
    for (const p of data.pointers || []) { p.pivotX += dx; p.pivotY += dy; }
    for (const b of data.backpacks || []) { b.x += dx; b.y += dy; }
    maxX += dx; maxY += dy;
  }
  data.gridWidth = Math.max(data.gridWidth || 1, maxX + 1);
  data.gridHeight = Math.max(data.gridHeight || 1, maxY + 1);
  return true;
}

class Sim {
  constructor(data, requireProps = true) {
    this.w = Math.max(1, data.gridWidth);
    this.h = Math.max(1, data.gridHeight);
    this.arrows = [];
    this.byId = new Map();
    (data.arrows || []).forEach((src, i) => {
      const id = src.id || `a${i}`;
      if (this.byId.has(id)) throw new Error("dup id");
      const arrow = { id, dir: src.dir || "Up", lock: [...(src.lockParents || [])], path: arrowPath(src) };
      this.arrows.push(arrow);
      this.byId.set(id, arrow);
    });
    this.books = [];
    for (const src of data.books || []) {
      if (!src) continue;
      if (!this.byId.has(src.blockedArrowId) || !this.byId.has(src.keyArrowId)) {
        if (requireProps) throw new Error("bad book");
        continue;
      }
      this.books.push({ ...src });
    }
    const used = new Set();
    for (const a of this.arrows) for (const [x, y] of a.path) used.add(key(x, y));
    for (const b of this.books) used.add(key(b.x, b.y));
    this.pointers = [];
    for (const [i, src] of (data.pointers || []).entries()) {
      if (!src) continue;
      const cells = pointerCells(src.pivotX, src.pivotY, src.length, src.dir);
      let ok = true;
      for (const [x, y] of cells) {
        if (x < 0 || y < 0 || x >= this.w || y >= this.h || used.has(key(x, y))) { ok = false; break; }
        used.add(key(x, y));
      }
      if (!ok) { if (requireProps) throw new Error("bad pointer"); continue; }
      this.pointers.push({ id: src.id || `p${i}`, pivot: [src.pivotX, src.pivotY], length: src.length, dir: src.dir, cw: !!src.rotateCW, cells });
    }
    this.backpacks = [];
    for (const [i, src] of (data.backpacks || []).entries()) {
      if (!src) continue;
      const cells = backpackCells(src.x, src.y, src.dir);
      let ok = true;
      for (const [x, y] of cells) {
        if (x < 0 || y < 0 || x >= this.w || y >= this.h || used.has(key(x, y))) { ok = false; break; }
        used.add(key(x, y));
      }
      if (!ok) { if (requireProps) throw new Error("bad backpack"); continue; }
      this.backpacks.push({ id: src.id || `bp${i}`, anchor: [src.x, src.y], dir: src.dir, cells });
    }
    const ap = (data.pointers || []).filter(Boolean).length;
    const ab = (data.backpacks || []).filter(Boolean).length;
    const ak = (data.books || []).filter(Boolean).length;
    if (requireProps && (this.pointers.length !== ap || this.backpacks.length !== ab || this.books.length !== ak)) {
      throw new Error("dropped props");
    }
  }

  locked(arrow) {
    for (const p of arrow.lock) if (p && this.byId.has(p)) return true;
    for (const b of this.books) {
      if (b.blockedArrowId !== arrow.id) continue;
      if (this.byId.has(b.keyArrowId)) return true;
    }
    return false;
  }

  occupied(skipPack = null) {
    const occ = new Set();
    const alive = new Set(this.arrows.map((a) => a.id));
    for (const a of this.arrows) for (const [x, y] of a.path) occ.add(key(x, y));
    for (const b of this.books) if (alive.has(b.keyArrowId)) occ.add(key(b.x, b.y));
    for (const p of this.pointers) for (const [x, y] of p.cells) occ.add(key(x, y));
    for (const bp of this.backpacks) {
      if (bp === skipPack) continue;
      for (const [x, y] of bp.cells) occ.add(key(x, y));
    }
    return occ;
  }

  pathClear(arrow) {
    const path = arrow.path;
    if (!path.length) return false;
    const self = new Set(path.map(([x, y]) => key(x, y)));
    const occ = this.occupied();
    const [dx, dy] = step(arrow.dir);
    let cx = path[path.length - 1][0] + dx;
    let cy = path[path.length - 1][1] + dy;
    while (cx >= 0 && cy >= 0 && cx < this.w && cy < this.h) {
      const k = key(cx, cy);
      if (occ.has(k) && !self.has(k)) return false;
      cx += dx; cy += dy;
    }
    return true;
  }

  rotatePointers() {
    const occ = this.occupied();
    for (const p of this.pointers) for (const [x, y] of p.cells) occ.add(key(x, y));
    for (const p of this.pointers) {
      const nxt = rotateCells(p.cells, p.pivot, p.cw);
      let can = nxt.length === p.cells.length;
      const self = new Set(p.cells.map(([x, y]) => key(x, y)));
      if (can) {
        for (const [x, y] of nxt) {
          if (x < 0 || y < 0 || x >= this.w || y >= this.h) { can = false; break; }
          const k = key(x, y);
          if (occ.has(k) && !self.has(k)) { can = false; break; }
        }
      }
      if (!can) continue;
      for (const [x, y] of p.cells) occ.delete(key(x, y));
      p.dir = rotateDir(p.dir, p.cw);
      p.cells = nxt;
      for (const [x, y] of nxt) occ.add(key(x, y));
    }
  }

  slide(bp, plus) {
    const d = plus ? bp.dir : OPP[bp.dir];
    const [dx, dy] = step(d);
    const ax = bp.anchor[0] + dx, ay = bp.anchor[1] + dy;
    const nxt = backpackCells(ax, ay, bp.dir);
    const occ = this.occupied(bp);
    for (const [x, y] of nxt) {
      if (x < 0 || y < 0 || x >= this.w || y >= this.h) return false;
      if (occ.has(key(x, y))) return false;
    }
    bp.anchor = [ax, ay];
    bp.cells = nxt;
    return true;
  }

  legal() { return this.arrows.filter((a) => !this.locked(a) && this.pathClear(a)); }

  playTokens(tokens) {
    const expected = this.arrows.length;
    let played = 0;
    const bpBy = new Map(this.backpacks.map((b) => [b.id, b]));
    for (const token of tokens) {
      const sep = token.lastIndexOf(":");
      if (sep > 0 && sep === token.length - 2) {
        const sign = token[token.length - 1];
        const pid = token.slice(0, sep);
        if ((sign !== "+" && sign !== "-") || !bpBy.has(pid)) return false;
        if (!this.slide(bpBy.get(pid), sign === "+")) return false;
        continue;
      }
      if (!this.byId.has(token)) return false;
      const arrow = this.byId.get(token);
      if (!this.arrows.includes(arrow)) return false;
      if (this.locked(arrow) || !this.pathClear(arrow)) return false;
      this.arrows.splice(this.arrows.indexOf(arrow), 1);
      this.byId.delete(token);
      this.rotatePointers();
      played++;
    }
    return this.arrows.length === 0 && played === expected;
  }

  startFree() { return this.arrows.filter((a) => !this.locked(a)).length; }

  stateKey() {
    const ids = this.arrows.map((a) => a.id).sort().join(",");
    const pd = this.pointers.map((p) => p.dir).join(",");
    const bd = this.backpacks.map((b) => `${b.anchor[0]},${b.anchor[1]},${b.dir}`).join(";");
    return `${ids}|${pd}|${bd}`;
  }

  solve(preferred, nodeLimit = 25000) {
    const tokens = [];
    const visited = new Set();
    let nodes = 0;
    const rec = () => {
      if (this.arrows.length === 0) return true;
      if (++nodes > nodeLimit) return false;
      const sk = this.stateKey();
      if (visited.has(sk)) return false;
      visited.add(sk);
      const legal = this.legal();
      let peel = null;
      for (const pid of preferred) {
        if (this.byId.has(pid)) { peel = this.byId.get(pid); break; }
      }
      const playArrow = (arrow) => {
        const poses = this.pointers.map((p) => ({ dir: p.dir, cells: p.cells.map((c) => [...c]) }));
        this.arrows.splice(this.arrows.indexOf(arrow), 1);
        this.byId.delete(arrow.id);
        this.rotatePointers();
        tokens.push(arrow.id);
        if (rec()) return true;
        tokens.pop();
        this.pointers.forEach((p, i) => { p.dir = poses[i].dir; p.cells = poses[i].cells; });
        this.arrows.push(arrow);
        this.byId.set(arrow.id, arrow);
        return false;
      };
      if (peel && legal.includes(peel) && playArrow(peel)) return true;
      for (const arrow of legal) {
        if (peel && arrow.id === peel.id) continue;
        if (playArrow(arrow)) return true;
      }
      const slidesSoFar = tokens.filter((t) => t.includes(":")).length;
      if (legal.length === 0 && slidesSoFar < 8) {
        for (const bp of this.backpacks) {
          for (const plus of [true, false]) {
            const oldA = [...bp.anchor], oldC = bp.cells.map((c) => [...c]);
            if (!this.slide(bp, plus)) continue;
            tokens.push(bp.id + (plus ? ":+" : ":-"));
            if (rec()) return true;
            tokens.pop();
            bp.anchor = oldA;
            bp.cells = oldC;
          }
        }
      }
      return false;
    };
    return rec() ? tokens : null;
  }
}

function pathCells(data) {
  const used = new Set();
  for (const a of data.arrows || []) for (const [x, y] of arrowPath(a)) used.add(key(x, y));
  return used;
}

function autoBooks(data) {
  const w = data.gridWidth, h = data.gridHeight;
  const used = pathCells(data);
  const byId = new Map(data.arrows.map((a) => [a.id, a]));
  const placed = [];
  const usedBook = new Set();
  for (const a of data.arrows) {
    const parents = a.lockParents || [];
    if (!parents.length) continue;
    const kid = parents[0];
    if (!byId.has(kid) || kid === a.id) continue;
    const pth = arrowPath(a);
    const [dx, dy] = step(a.dir);
    let cx = pth[pth.length - 1][0] + dx, cy = pth[pth.length - 1][1] + dy;
    let found = null;
    while (cx >= 0 && cy >= 0 && cx < w && cy < h) {
      const k = key(cx, cy);
      if (!used.has(k) && !usedBook.has(k)) { found = [cx, cy]; break; }
      cx += dx; cy += dy;
    }
    if (!found) continue;
    usedBook.add(key(found[0], found[1]));
    placed.push({ id: `book_${a.id}`, x: found[0], y: found[1], blockedArrowId: a.id, keyArrowId: kid });
  }
  return placed;
}

function densifyLocks(data, want, minFree, rng) {
  const arrows = data.arrows;
  let locked = arrows.filter((a) => (a.lockParents || []).length).length;
  const order = Array.from({ length: arrows.length - 1 }, (_, i) => i + 1);
  shuffle(order, rng);
  for (const idx of order) {
    if (locked >= want) break;
    const child = arrows[idx];
    if ((child.lockParents || []).length) continue;
    const parent = arrows[rngInt(rng, idx)];
    if (parent.id === child.id) continue;
    child.lockParents = [parent.id];
    locked++;
  }
  let free = arrows.filter((a) => !(a.lockParents || []).length).length;
  for (let i = arrows.length - 1; i >= 0 && free < minFree; i--) {
    if (!(arrows[i].lockParents || []).length) continue;
    arrows[i].lockParents = [];
    free++;
  }
}

function staticUsed(data, pointers, backpacks) {
  const used = pathCells(data);
  for (const b of data.books || []) used.add(key(b.x, b.y));
  if (pointers) for (const p of data.pointers || []) for (const [x, y] of pointerCells(p.pivotX, p.pivotY, p.length, p.dir)) used.add(key(x, y));
  if (backpacks) for (const b of data.backpacks || []) for (const [x, y] of backpackCells(b.x, b.y, b.dir)) used.add(key(x, y));
  return used;
}

function exitRays(data, skipFirst = 2) {
  const w = data.gridWidth, h = data.gridHeight;
  const used = pathCells(data);
  const rays = new Set();
  const start = Math.min(skipFirst, data.arrows.length);
  for (const a of data.arrows.slice(start)) {
    const pth = arrowPath(a);
    const [dx, dy] = step(a.dir);
    let cx = pth[pth.length - 1][0] + dx, cy = pth[pth.length - 1][1] + dy;
    while (cx >= 0 && cy >= 0 && cx < w && cy < h) {
      const k = key(cx, cy);
      if (!used.has(k)) rays.add(k);
      cx += dx; cy += dy;
    }
  }
  return rays;
}

function pointerCandidates(data) {
  const w = data.gridWidth, h = data.gridHeight;
  const used = staticUsed(data, false, false);
  const rays = exitRays(data, 2);
  const hits = [], rest = [];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (used.has(key(x, y))) continue;
      for (const d of DIRS) {
        for (const length of [3, 4, 5]) {
          const cells = pointerCells(x, y, length, d);
          if (cells.some(([cx, cy]) => cx < 0 || cy < 0 || cx >= w || cy >= h || used.has(key(cx, cy)))) continue;
          const item = { id: "p1", pivotX: x, pivotY: y, length, dir: d, rotateCW: ((x + y + length) % 2 === 0) };
          if (cells.some(([cx, cy]) => rays.has(key(cx, cy)))) hits.push(item);
          else rest.push(item);
        }
      }
    }
  }
  return hits.concat(rest);
}

function backpackCandidates(data) {
  const w = data.gridWidth, h = data.gridHeight;
  const used = staticUsed(data, true, false);
  const rays = exitRays(data, 2);
  const out = [];
  for (const rk of rays) {
    const [rx, ry] = rk.split(",").map(Number);
    if (used.has(rk)) continue;
    for (const d of DIRS) {
      const cells = backpackCells(rx, ry, d);
      if (cells.some(([x, y]) => x < 0 || y < 0 || x >= w || y >= h || used.has(key(x, y)))) continue;
      if (!cells.some(([x, y]) => rays.has(key(x, y)))) continue;
      out.push({ id: "bp1", x: rx, y: ry, dir: d });
    }
  }
  return out;
}

function specs(id) {
  if (id <= 3) return { wantPointer: false, optionalPointer: false, wantBackpack: false, minBooks: 3, minLocks: 5, slack: [0, 2] };
  if (id <= 7) return { wantPointer: true, optionalPointer: false, wantBackpack: false, minBooks: 3, minLocks: 5, slack: [0, 2] };
  if (id <= 10) return { wantPointer: false, optionalPointer: false, wantBackpack: true, minBooks: 4, minLocks: 6, slack: [0, 1] };
  if (id <= 12) return { wantPointer: false, optionalPointer: true, wantBackpack: true, minBooks: 4, minLocks: 6, slack: [0, 1] };
  return { wantPointer: true, optionalPointer: false, wantBackpack: true, minBooks: 5, minLocks: 6, slack: [0, 1] };
}

function isSolvable(data) {
  try { return new Sim(data, true).playTokens(data.solution || []); }
  catch { return false; }
}

function backpackRequired(data) {
  const sol = data.solution || [];
  if (!sol.some((t) => t.includes(":"))) return false;
  const clone = structuredClone(data);
  clone.solution = sol.filter((t) => !t.includes(":"));
  return !isSolvable(clone);
}

function validate(data, schoolId) {
  if (!sanitize(data)) return "bounds";
  if (!isSolvable(data)) return "solvable";
  let free;
  try { free = new Sim(data, true).startFree(); }
  catch (e) { return String(e.message || e); }
  if (free < 2) return "free";
  const books = (data.books || []).length;
  const pointers = (data.pointers || []).length;
  const backpacks = (data.backpacks || []).length;
  if (schoolId <= 3) {
    if (books < 3) return "books";
    if (pointers || backpacks) return "extra props";
  } else if (schoolId <= 7) {
    if (books < 3 || pointers !== 1 || backpacks) return "4-7 shape";
  } else if (schoolId <= 10) {
    if (books < 3 || pointers || backpacks < 1) return "8-10 shape";
    if (!backpackRequired(data)) return "bp deco";
  } else if (schoolId <= 12) {
    if (books < 3 || backpacks < 1) return "11-12 shape";
    if (!backpackRequired(data)) return "bp deco";
  } else {
    if (books < 3 || pointers < 1 || backpacks < 1) return "13-20 shape";
    if (!backpackRequired(data)) return "bp deco";
  }
  return null;
}

function commit(data, schoolId, spec, preferred) {
  if (!sanitize(data)) return null;
  let sim;
  try { sim = new Sim(data, true); } catch { return null; }
  const tokens = sim.solve(preferred);
  if (!tokens) return null;
  data.solution = tokens;
  const [lo, hi] = spec.slack;
  data.movesLimit = data.arrows.length + (hi <= lo ? lo : (schoolId % (hi - lo + 1) + lo));
  return validate(data, schoolId) == null ? data : null;
}

function decorate(base, schoolId, rng) {
  const spec = specs(schoolId);
  const data = structuredClone(base);
  data.id = schoolId;
  data.buttons = [];
  data.pointers = [];
  data.backpacks = [];
  densifyLocks(data, spec.minLocks, 2, rng);
  data.books = autoBooks(data);
  if (data.books.length < spec.minBooks) return null;
  const preferred = [...(data.solution || data.arrows.map((a) => a.id))];
  const pointerCands = (spec.wantPointer || spec.optionalPointer) ? pointerCandidates(data) : [];
  shuffle(pointerCands, rng);

  const withBackpacks = (cur) => {
    if (!spec.wantBackpack) {
      cur.backpacks = [];
      return commit(cur, schoolId, spec, preferred);
    }
    const cands = backpackCandidates(cur);
    shuffle(cands, rng);
    for (const bp of cands.slice(0, 8)) {
      const trial = structuredClone(cur);
      trial.backpacks = [bp];
      const done = commit(trial, schoolId, spec, preferred);
      if (done) return done;
    }
    return null;
  };

  if (spec.wantPointer || spec.optionalPointer) {
    for (const p of pointerCands.slice(0, 8)) {
      const trial = structuredClone(data);
      trial.pointers = [p];
      const done = withBackpacks(trial);
      if (done) return done;
    }
    if (spec.wantPointer && !spec.optionalPointer) return null;
    data.pointers = [];
    return withBackpacks(data);
  }
  data.pointers = [];
  return withBackpacks(data);
}

function campaignFiles() {
  const files = fs.readdirSync(CAMPAIGN).filter((f) => /^level_\d+\.json$/.test(f));
  const dense = [];
  for (const f of files) {
    const data = JSON.parse(fs.readFileSync(path.join(CAMPAIGN, f), "utf8"));
    const n = (data.arrows || []).length;
    if ((data.id || 0) >= 70 && n >= 16) dense.push({ n, file: f, data });
  }
  dense.sort((a, b) => b.n - a.n);
  return dense;
}

function generateOne(schoolId, dense, seed) {
  for (let offset = 0; offset < Math.min(6, dense.length); offset++) {
    const item = dense[(schoolId * 3 + offset) % dense.length];
    for (let extra = 0; extra < 4; extra++) {
      const rng = mulberry32((seed + extra * 997 + offset * 13) >>> 0);
      const got = decorate(item.data, schoolId, rng);
      if (got) return { data: got, src: item.file };
    }
  }
  return { data: null, src: null };
}

function main() {
  fs.mkdirSync(SCHOOL, { recursive: true });
  const dense = campaignFiles();
  if (dense.length < 10) {
    console.error("not enough campaign seeds");
    process.exit(1);
  }
  const failed = [];
  for (let i = 1; i <= 20; i++) {
    const { data, src } = generateOne(i, dense, 11003 * i + 9176);
    if (!data) {
      failed.push(i);
      console.log(`FAIL school_${String(i).padStart(2, "0")}`);
      continue;
    }
    const out = path.join(SCHOOL, `school_${String(i).padStart(2, "0")}.json`);
    fs.writeFileSync(out, pretty(data), "utf8");
    console.log(`OK school_${String(i).padStart(2, "0")} arrows=${data.arrows.length} books=${(data.books || []).length} pointers=${(data.pointers || []).length} backpacks=${(data.backpacks || []).length} moves=${data.movesLimit} src=${src} sol=${data.solution.length}`);
  }
  console.log("--- validate ---");
  for (let i = 1; i <= 20; i++) {
    const p = path.join(SCHOOL, `school_${String(i).padStart(2, "0")}.json`);
    const data = JSON.parse(fs.readFileSync(p, "utf8"));
    const err = validate(data, i);
    console.log(`school_${String(i).padStart(2, "0")}: ${err || "ok"}`);
    if (err) failed.push(i);
  }
  if (failed.length) {
    console.log("FAILED", [...new Set(failed)].sort((a, b) => a - b).join(","));
    process.exit(1);
  }
  console.log("ALL 20 SOLVABLE");
}

main();
