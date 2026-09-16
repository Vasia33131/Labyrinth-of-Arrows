#!/usr/bin/env node
"use strict";

const fs = require("fs");
const path = require("path");

const UP = 0, RIGHT = 1, DOWN = 2, LEFT = 3;
const DIR_NAME = { 0: "Up", 1: "Right", 2: "Down", 3: "Left" };
const COLOR_NAME = { 0: "Grey", 1: "Red", 2: "Blue", 3: "Green", 4: "Yellow" };
const ROT_NAME = { 0: "Rotate90CW", 1: "Rotate90CCW", 2: "Rotate180" };
const ROT_STEPS = { 0: 1, 1: 3, 2: 2 };
const GREY = 0, RED = 1, BLUE = 2, GREEN = 3, YELLOW = 4;

const L_SHAPES = [
  [[0, 0], [0, 1], [1, 1]],
  [[0, 0], [1, 0], [0, 1]],
  [[0, 0], [1, 0], [1, 1]],
  [[1, 0], [0, 1], [1, 1]],
  [[0, 0], [0, 1], [0, 2], [1, 2]],
  [[0, 0], [1, 0], [2, 0], [2, 1]],
];
const PI_SHAPES = [
  [[0, 0], [2, 0], [0, 1], [1, 1], [2, 1]],
  [[0, 0], [1, 0], [2, 0], [0, 1], [2, 1]],
  [[0, 1], [1, 1], [2, 1], [1, 0]],
  [[0, 0], [1, 0], [2, 0], [1, 1]],
];

function mulberry32(seed) {
  let a = seed >>> 0;
  return function rand() {
    a |= 0;
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function makeRng(seed) {
  const rnd = mulberry32(seed);
  return {
    random: rnd,
    next: (n) => Math.floor(rnd() * n),
    randint: (a, b) => a + Math.floor(rnd() * (b - a + 1)),
    shuffle(arr) {
      for (let i = arr.length - 1; i > 0; i--) {
        const j = this.next(i + 1);
        const t = arr[i];
        arr[i] = arr[j];
        arr[j] = t;
      }
      return arr;
    },
    choice(arr) {
      return arr[this.next(arr.length)];
    },
  };
}

function pack(x, y) {
  return (x * 0x100000000) + (y >>> 0);
}

function step(d) {
  return [[0, 1], [1, 0], [0, -1], [-1, 0]][d];
}

function rotate(d, rotation) {
  return (d + ROT_STEPS[rotation]) % 4;
}

function specFor(id) {
  if (id <= 10) {
    return { minA: 3, maxA: 5, w: 5, h: 6, locks: false, colors: false, buttons: false, shapes: false, usePi: false, extra: false, strict: false, shapeCount: 0, minLocks: 0, maxLocks: 0, minBtn: 0, maxBtn: 0, minSlack: 5, maxSlack: 8, blue: 0, red: 0, weave: true, weaveChance: 0.45 };
  }
  if (id <= 30) {
    return { minA: 8, maxA: 15, w: 5, h: 7, locks: true, colors: false, buttons: false, shapes: true, usePi: false, extra: false, strict: false, shapeCount: id <= 20 ? 1 : 2, minLocks: 2, maxLocks: 5, minBtn: 0, maxBtn: 0, minSlack: 3, maxSlack: 5, blue: 0, red: 0, weave: true, weaveChance: 0.65 };
  }
  if (id <= 60) {
    return { minA: 15, maxA: 25, w: 6, h: 8, locks: true, colors: true, buttons: true, shapes: true, usePi: true, extra: false, strict: false, shapeCount: 3, minLocks: 4, maxLocks: 8, minBtn: 1, maxBtn: 3, minSlack: 1, maxSlack: 3, blue: 35, red: 25, weave: true, weaveChance: 0.75 };
  }
  return { minA: 25, maxA: 40, w: 6, h: 9, locks: true, colors: true, buttons: true, shapes: true, usePi: true, extra: true, strict: true, shapeCount: 5, minLocks: 8, maxLocks: 16, minBtn: 2, maxBtn: 4, minSlack: 0, maxSlack: 1, blue: 30, red: 25, weave: true, weaveChance: 0.9 };
}

function pathClear(x, y, d, occ, w, h) {
  const [dx, dy] = step(d);
  let cx = x + dx;
  let cy = y + dy;
  while (cx >= 0 && cy >= 0 && cx < w && cy < h) {
    if (occ.has(pack(cx, cy))) return false;
    cx += dx;
    cy += dy;
  }
  return true;
}

function collectEmpty(occ, w, h) {
  const out = [];
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!occ.has(pack(x, y))) out.push([x, y]);
    }
  }
  return out;
}

function blockingCells(placed, occ, w, h) {
  const seen = new Set();
  const out = [];
  for (const a of placed) {
    const [dx, dy] = step(a.dir);
    let cx = a.x + dx;
    let cy = a.y + dy;
    while (cx >= 0 && cy >= 0 && cx < w && cy < h) {
      const key = pack(cx, cy);
      if (occ.has(key)) break;
      if (!seen.has(key)) {
        seen.add(key);
        out.push([cx, cy]);
      }
      cx += dx;
      cy += dy;
    }
  }
  return out;
}

function place(placed, occ, x, y, d) {
  occ.add(pack(x, y));
  placed.push({ id: `a${placed.length}`, x, y, dir: d, color: GREY, lockParents: [] });
}

function tryPlaceSingle(placed, occ, w, h, rng, spec) {
  const empty = collectEmpty(occ, w, h);
  const hot = blockingCells(placed, occ, w, h);
  let order;
  if (spec.weave && hot.length && rng.random() < spec.weaveChance) {
    rng.shuffle(hot);
    rng.shuffle(empty);
    const hotSet = new Set(hot.map(([x, y]) => pack(x, y)));
    order = hot.concat(empty.filter(([x, y]) => !hotSet.has(pack(x, y))));
  } else {
    rng.shuffle(empty);
    order = empty;
  }
  const dirs = [UP, RIGHT, DOWN, LEFT];
  for (const [x, y] of order) {
    rng.shuffle(dirs);
    for (const d of dirs) {
      if (!pathClear(x, y, d, occ, w, h)) continue;
      place(placed, occ, x, y, d);
      return true;
    }
  }
  return false;
}

function shapeFits(shape, ox, oy, occ, w, h) {
  for (const [sx, sy] of shape) {
    const x = ox + sx;
    const y = oy + sy;
    if (x < 0 || y < 0 || x >= w || y >= h || occ.has(pack(x, y))) return false;
  }
  return true;
}

function tryPlaceShape(placed, occ, w, h, rng, spec) {
  const catalog = spec.usePi && rng.random() < 0.45 ? PI_SHAPES : L_SHAPES;
  const shape = catalog[rng.next(catalog.length)];
  const origins = collectEmpty(occ, w, h);
  rng.shuffle(origins);
  const dirs = [UP, RIGHT, DOWN, LEFT];
  for (const [ox, oy] of origins) {
    if (!shapeFits(shape, ox, oy, occ, w, h)) continue;
    const cells = shape.map(([sx, sy]) => [ox + sx, oy + sy]);
    rng.shuffle(cells);
    const planned = [];
    const temp = new Set(occ);
    let ok = true;
    for (const [x, y] of cells) {
      let found = null;
      rng.shuffle(dirs);
      for (const d of dirs) {
        if (!pathClear(x, y, d, temp, w, h)) continue;
        found = d;
        break;
      }
      if (found === null) {
        ok = false;
        break;
      }
      planned.push({ id: `a${placed.length + planned.length}`, x, y, dir: found, color: GREY, lockParents: [] });
      temp.add(pack(x, y));
    }
    if (!ok) continue;
    for (const a of planned) {
      occ.add(pack(a.x, a.y));
      placed.push(a);
    }
    return true;
  }
  return false;
}

function applyLocks(solution, spec, rng) {
  if (!spec.locks || solution.length < 2) return;
  let lockCount = Math.min(spec.maxLocks, solution.length - 1);
  const minL = Math.min(spec.minLocks, lockCount);
  lockCount = lockCount <= 0 ? 0 : rng.randint(minL, lockCount);
  const locked = new Set();
  if (spec.strict) {
    const chain = Math.min(lockCount, solution.length - 1);
    for (let i = 1; i <= chain; i++) {
      solution[i].lockParents.push(solution[i - 1].id);
      locked.add(solution[i].id);
    }
    lockCount -= chain;
  }
  for (let n = 0; n < lockCount; n++) {
    const childI = rng.randint(1, solution.length - 1);
    const child = solution[childI];
    if (locked.has(child.id) && rng.random() < 0.7) continue;
    let parentI;
    if (spec.strict) {
      const span = Math.min(3, childI);
      parentI = Math.max(0, childI - 1 - (span > 0 ? rng.next(span) : 0));
    } else {
      parentI = rng.next(childI);
    }
    const pid = solution[parentI].id;
    if (!child.lockParents.includes(pid)) {
      child.lockParents.push(pid);
      locked.add(child.id);
    }
  }
}

function applyColors(solution, spec, rng) {
  if (!spec.colors || !solution.length) return;
  let blue = Math.max(1, Math.floor(solution.length * spec.blue / 100));
  let red = Math.max(1, Math.floor(solution.length * spec.red / 100));
  if (blue + red > solution.length) red = Math.max(1, solution.length - blue);
  for (let i = 0; i < solution.length; i++) {
    if (i < blue) {
      solution[i].color = rng.random() < 0.18 ? GREY : BLUE;
    } else if (solution.length - i <= red) {
      solution[i].color = RED;
    } else if (spec.extra && rng.random() < 0.35) {
      solution[i].color = rng.random() < 0.5 ? GREEN : YELLOW;
    } else {
      solution[i].color = GREY;
    }
  }
}

function placeButtons(occ, w, h, spec, rng) {
  if (!spec.buttons) return [];
  const empty = collectEmpty(occ, w, h);
  if (!empty.length) return [];
  rng.shuffle(empty);
  const count = Math.min(rng.randint(spec.minBtn, spec.maxBtn), empty.length);
  const out = [];
  for (let i = 0; i < count; i++) {
    const [x, y] = empty[i];
    out.push({
      x, y,
      rotation: rng.choice([0, 1, 2]),
      oneShot: rng.random() < 0.4,
      oncePer: true,
      filter: spec.colors && rng.random() < 0.35,
      target: rng.random() < 0.5 ? BLUE : RED,
    });
  }
  return out;
}

function toLevel(id, spec, solution, buttons) {
  const slack = spec.maxSlack <= spec.minSlack
    ? spec.minSlack
    : spec.minSlack + (solution.length % (spec.maxSlack - spec.minSlack + 1));
  return {
    id,
    gridWidth: spec.w,
    gridHeight: spec.h,
    movesLimit: Math.max(1, solution.length + slack),
    solution: solution.map((a) => a.id),
    arrows: solution.map((a) => {
      const e = { id: a.id, x: a.x, y: a.y, dir: DIR_NAME[a.dir], color: COLOR_NAME[a.color] };
      if (a.lockParents.length) e.lockParents = a.lockParents.slice();
      return e;
    }),
    buttons: buttons.map((b) => {
      const e = {
        x: b.x, y: b.y, rotation: ROT_NAME[b.rotation],
        oneShot: b.oneShot, oncePerArrow: b.oncePer,
      };
      if (b.filter) {
        e.affectOnlyTargetColor = true;
        e.targetColor = COLOR_NAME[b.target];
      }
      return e;
    }),
  };
}

function fallback(id, spec) {
  const n = spec.minA;
  const arrows = [];
  const solution = [];
  for (let i = 0; i < n; i++) {
    const aid = `a${i}`;
    solution.push(aid);
    arrows.push({ id: aid, x: i, y: 0, dir: "Down", color: "Grey" });
  }
  return {
    id, gridWidth: Math.max(spec.w, n), gridHeight: Math.max(3, spec.h),
    movesLimit: n + 8, solution, arrows, buttons: [],
  };
}

const DIR_FROM = Object.fromEntries(Object.entries(DIR_NAME).map(([k, v]) => [v, Number(k)]));
const COLOR_FROM = Object.fromEntries(Object.entries(COLOR_NAME).map(([k, v]) => [v, Number(k)]));
const ROT_FROM = Object.fromEntries(Object.entries(ROT_NAME).map(([k, v]) => [v, Number(k)]));

function isSolvable(data) {
  const arrows = [];
  const byId = new Map();
  for (let i = 0; i < data.arrows.length; i++) {
    const src = data.arrows[i];
    const id = src.id || `a${i}`;
    const sim = {
      id, x: src.x, y: src.y,
      dir: DIR_FROM[src.dir],
      color: COLOR_FROM[src.color || "Grey"],
      locks: src.lockParents || [],
      used: new Set(),
    };
    if (byId.has(id)) return false;
    arrows.push(sim);
    byId.set(id, sim);
  }
  const buttons = (data.buttons || []).map((src) => ({
    x: src.x, y: src.y,
    rot: ROT_FROM[src.rotation || "Rotate90CW"],
    one: !!src.oneShot,
    once: src.oncePerArrow !== false,
    filter: !!src.affectOnlyTargetColor,
    target: COLOR_FROM[src.targetColor || "Red"],
    used: false,
  }));
  const order = data.solution && data.solution.length === arrows.length
    ? data.solution
    : arrows.map((a) => a.id);
  const w = data.gridWidth;
  const h = data.gridHeight;

  const locked = (a) => a.locks.some((p) => byId.has(p));
  const colorOk = (c) => c !== RED || !arrows.some((x) => x.color === BLUE);
  const pathOk = (a) => {
    const occ = new Set(arrows.map((x) => pack(x.x, x.y)));
    const [dx, dy] = step(a.dir);
    let cx = a.x + dx;
    let cy = a.y + dy;
    while (cx >= 0 && cy >= 0 && cx < w && cy < h) {
      if (occ.has(pack(cx, cy))) return false;
      cx += dx;
      cy += dy;
    }
    return true;
  };
  const gravity = (rx, ry) => {
    const col = arrows.filter((a) => a.x === rx && a.y > ry).sort((a, b) => a.y - b.y);
    let expected = ry + 1;
    for (const a of col) {
      if (a.y !== expected) break;
      const newY = a.y - 1;
      for (let i = 0; i < buttons.length; i++) {
        const b = buttons[i];
        if (b.used && b.one) continue;
        if (b.x !== rx || b.y !== newY) continue;
        if (b.filter && a.color !== b.target) continue;
        if (b.once && a.used.has(i)) continue;
        a.used.add(i);
        a.dir = rotate(a.dir, b.rot);
        b.used = true;
        if (b.once) break;
      }
      a.y = newY;
      expected = a.y + 2;
    }
  };

  for (const aid of order) {
    const a = byId.get(aid);
    if (!a || !arrows.includes(a) || locked(a) || !colorOk(a.color) || !pathOk(a)) return false;
    const rx = a.x;
    const ry = a.y;
    arrows.splice(arrows.indexOf(a), 1);
    byId.delete(aid);
    gravity(rx, ry);
  }
  return arrows.length === 0;
}

function tryGenerate(id, spec, rng) {
  const { w, h } = spec;
  const target = rng.randint(spec.minA, spec.maxA);
  const occ = new Set();
  const placed = [];
  if (spec.shapes) {
    for (let i = 0; i < spec.shapeCount; i++) tryPlaceShape(placed, occ, w, h, rng, spec);
  }
  let fail = 0;
  while (placed.length < target) {
    if (tryPlaceSingle(placed, occ, w, h, rng, spec)) {
      fail = 0;
      continue;
    }
    fail++;
    if (fail < 48) continue;
    if (placed.length >= spec.minA) break;
    return null;
  }
  if (placed.length < spec.minA) return null;
  const solution = placed.slice().reverse();
  applyLocks(solution, spec, rng);
  applyColors(solution, spec, rng);
  const buttons = placeButtons(occ, w, h, spec, rng);
  let data = toLevel(id, spec, solution, buttons);
  if (isSolvable(data)) return data;
  data = toLevel(id, spec, solution, []);
  return isSolvable(data) ? data : null;
}

function generate(id) {
  const spec = specFor(id);
  const rng = makeRng(id * 7919 + 42);
  for (let i = 0; i < 80; i++) {
    const data = tryGenerate(id, spec, rng);
    if (data) return { data, fallback: false };
  }
  return { data: fallback(id, spec), fallback: true };
}

function main() {
  const outDir = path.join(__dirname, "..", "Assets", "Resources", "Levels");
  fs.mkdirSync(outDir, { recursive: true });
  let fallbacks = 0;
  for (let i = 1; i <= 100; i++) {
    const { data, fallback: fb } = generate(i);
    if (fb) fallbacks++;
    const file = path.join(outDir, `level_${String(i).padStart(2, "0")}.json`);
    fs.writeFileSync(file, `${JSON.stringify(data, null, 2)}\n`, "utf8");
    console.log(`${path.basename(file)}: arrows=${data.arrows.length} moves=${data.movesLimit} buttons=${data.buttons.length} fallback=${fb}`);
  }
  console.log(`done, fallbacks=${fallbacks}`);
}

main();
