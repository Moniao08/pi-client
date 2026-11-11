'use strict';
// 生成青绿主题图标 icon.ico / icon.png（纯 Node 实现，无依赖）
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const SIZE = 256;
const SS = 3; // 超采样

function crcTable() {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
}
const TBL = crcTable();
function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = TBL[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body), 0);
  return Buffer.concat([len, body, crc]);
}
function encodePng(w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (w * 4 + 1)] = 0;
    rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}
function roundRect(x, y, x0, y0, x1, y1, r) {
  if (x < x0 || x > x1 || y < y0 || y > y1) return false;
  const cx = Math.min(Math.max(x, x0 + r), x1 - r);
  const cy = Math.min(Math.max(y, y0 + r), y1 - r);
  const dx = x - cx;
  const dy = y - cy;
  return dx * dx + dy * dy <= r * r;
}

const px = Buffer.alloc(SIZE * SIZE * 4);
// 青绿渐变：#0FB39E -> #34D399
const C1 = { r: 15, g: 179, b: 158 };
const C2 = { r: 52, g: 211, b: 153 };

for (let y = 0; y < SIZE; y++) {
  for (let x = 0; x < SIZE; x++) {
    let r = 0,
      g = 0,
      b = 0,
      a = 0;
    for (let sy = 0; sy < SS; sy++) {
      for (let sx = 0; sx < SS; sx++) {
        const fx = x + (sx + 0.5) / SS;
        const fy = y + (sy + 0.5) / SS;
        if (!roundRect(fx, fy, 8, 8, 248, 248, 56)) continue;
        const t = (fx + fy) / (2 * SIZE);
        let cr = lerp(C1.r, C2.r, t);
        let cg = lerp(C1.g, C2.g, t);
        let cb = lerp(C1.b, C2.b, t);

        const isBar = roundRect(fx, fy, 56, 74, 200, 100, 8);
        const isLeft = roundRect(fx, fy, 82, 92, 106, 190, 8);
        const isRight = roundRect(fx, fy, 150, 92, 174, 190, 8);
        if (isBar || isLeft || isRight) {
          cr = 6;
          cg = 32;
          cb = 30;
        }
        r += cr;
        g += cg;
        b += cb;
        a += 255;
      }
    }
    const n = SS * SS;
    const i = (y * SIZE + x) * 4;
    const alpha = a / n;
    if (alpha > 0) {
      const covered = a / 255;
      px[i] = Math.round(r / covered);
      px[i + 1] = Math.round(g / covered);
      px[i + 2] = Math.round(b / covered);
      px[i + 3] = Math.round(alpha);
    }
  }
}

const png = encodePng(SIZE, SIZE, px);

const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(1, 4);
const entry = Buffer.alloc(16);
entry.writeUInt16LE(1, 4);
entry.writeUInt16LE(32, 6);
entry.writeUInt32LE(png.length, 8);
entry.writeUInt32LE(22, 12);

const out = path.join(__dirname, '..');
fs.writeFileSync(path.join(out, 'icon.ico'), Buffer.concat([header, entry, png]));
fs.writeFileSync(path.join(out, 'icon.png'), png);
console.log('icon.ico / icon.png 已更新（青绿）');
