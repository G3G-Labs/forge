// Gera a marca do Forge: SVG mestre, PNGs em vários tamanhos, versão branca e app.ico.
// Mesma família visual do WSL Vision: forma facetada + círculo escuro com o prompt ">_".
// O Forge é uma chama facetada em tons de azul a violeta (a chama mais quente da forja é azul).
// Sem dependências: rasterizador próprio com supersampling e PNG via zlib.
//   node tools/gerar-logo.js assets/brand
// Depois, copie app.ico e forge-logo-128.png (como logo.png) para src/Forge.App/Assets.
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const OUT = process.argv[2];
fs.mkdirSync(OUT, { recursive: true });

// ---------- Geometria (espaço 1024 x 1024) ----------
const C = { x: 512, y: 612 };          // centro do círculo e do leque de facetas
const R = 196;                          // raio do círculo escuro
const flame = [
  [592, 58],                            // ponta principal
  [690, 176], [782, 300], [852, 432], [884, 574], [866, 724], [796, 852], [674, 938], [512, 968],
  [350, 938], [228, 852], [158, 724], [140, 574], [168, 432], [238, 316],
  [352, 168],                           // ponta da língua esquerda
  [446, 286],                           // reentrância entre as línguas
];

// Cores ao longo da diagonal de luz (alto-esquerda claro → baixo-direita escuro).
const stops = [
  [0.00, '#3F45C4'],
  [0.30, '#5F6DE8'],
  [0.55, '#7A9CFA'],
  [0.78, '#9CBCFF'],
  [1.00, '#C4D8FF'],
];
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const toHex = c => '#' + c.map(v => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('').toUpperCase();
function ramp(t) {
  t = Math.max(0, Math.min(1, t));
  for (let i = 1; i < stops.length; i++) {
    if (t <= stops[i][0]) {
      const [t0, c0] = stops[i - 1], [t1, c1] = stops[i];
      const k = (t - t0) / (t1 - t0), a = hex(c0), b = hex(c1);
      return a.map((v, j) => v + (b[j] - v) * k);
    }
  }
  return hex(stops[stops.length - 1][1]);
}

// Facetas: leque de triângulos do centro até cada aresta da chama.
const facets = flame.map((p, i) => {
  const q = flame[(i + 1) % flame.length];
  const mx = (p[0] + q[0] + C.x) / 3 - C.x, my = (p[1] + q[1] + C.y) / 3 - C.y;
  // Projeção na diagonal de luz, normalizada pelo tamanho da chama.
  const light = (-(mx) * 0.6 - my * 0.8) / 430;
  let rgb = ramp(0.5 + light * 0.62);
  // Facetas vizinhas alternam um pouco o tom, para as arestas aparecerem como no WSL Vision.
  const shade = i % 2 === 0 ? 1.0 : 0.93;
  rgb = rgb.map(v => v * shade);
  return { tri: [[C.x, C.y], p, q], color: toHex(rgb) };
});

// Checa que o leque não dobra (todos os triângulos com a mesma orientação).
const signs = facets.map(f => {
  const [a, b, c] = f.tri;
  return Math.sign((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]));
});
if (new Set(signs).size !== 1) throw new Error('Facetas dobradas: ' + signs.join(','));

// Prompt ">_" em branco, traços com pontas redondas.
const stroke = 56;
const glyph = [
  { a: [C.x - 96, C.y - 82], b: [C.x - 16, C.y] },
  { a: [C.x - 16, C.y], b: [C.x - 96, C.y + 82] },
  { a: [C.x + 42, C.y + 84], b: [C.x + 118, C.y + 84] },
];
// Versão pequena (16 e 24 px): só o ">" e mais grosso, para não virar borrão.
const glyphSmall = [
  { a: [C.x - 70, C.y - 96], b: [C.x + 30, C.y] },
  { a: [C.x + 30, C.y], b: [C.x - 70, C.y + 96] },
];

const circleTop = '#1D2550', circleBottom = '#121834', white = '#FFFFFF';

// ---------- SVG ----------
function svg({ mono = false, small = false } = {}) {
  const parts = [];
  parts.push('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" width="1024" height="1024">');
  parts.push('  <title>Forge</title>');
  if (mono) {
    parts.push(`  <path fill="${white}" fill-rule="evenodd" d="M${flame.map(p => p.join(' ')).join(' L')} Z ${circlePath()}"/>`);
    parts.push(`  <g fill="none" stroke="${white}" stroke-width="${small ? 76 : stroke}" stroke-linecap="round" stroke-linejoin="round">`);
    // Na versão branca o prompt é vazado: o círculo vira um furo e o ">_" volta a ser branco dentro dele.
  } else {
    for (const f of facets)
      parts.push(`  <path fill="${f.color}" d="M${f.tri.map(p => p.join(' ')).join(' L')} Z"/>`);
    parts.push(`  <circle cx="${C.x}" cy="${C.y}" r="${R}" fill="${circleBottom}"/>`);
    parts.push(`  <path fill="${circleTop}" d="${halfCirclePath()}"/>`);
    parts.push(`  <g fill="none" stroke="${white}" stroke-width="${small ? 76 : stroke}" stroke-linecap="round" stroke-linejoin="round">`);
  }
  const g = small ? glyphSmall : glyph;
  parts.push(`    <path d="M${g[0].a.join(' ')} L${g[0].b.join(' ')} L${g[1].b.join(' ')}"/>`);
  if (!small) parts.push(`    <path d="M${g[2].a.join(' ')} L${g[2].b.join(' ')}"/>`);
  parts.push('  </g>');
  parts.push('</svg>');
  return parts.join('\n') + '\n';
}
function circlePath() {
  return `M${C.x - R} ${C.y} a${R} ${R} 0 1 0 ${2 * R} 0 a${R} ${R} 0 1 0 ${-2 * R} 0 Z`;
}
// Metade de cima-esquerda do círculo (corte a 45°), um tom mais claro.
function halfCirclePath() {
  const d = R / Math.SQRT2;
  const x1 = (C.x + d).toFixed(1), y1 = (C.y - d).toFixed(1), x2 = (C.x - d).toFixed(1), y2 = (C.y + d).toFixed(1);
  return `M${x1} ${y1} A${R} ${R} 0 0 0 ${x2} ${y2} Z`;
}

// ---------- Rasterizador ----------
const insideTri = (px, py, [a, b, c]) => {
  const d1 = (px - b[0]) * (a[1] - b[1]) - (a[0] - b[0]) * (py - b[1]);
  const d2 = (px - c[0]) * (b[1] - c[1]) - (b[0] - c[0]) * (py - c[1]);
  const d3 = (px - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (py - a[1]);
  const neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
  return !(neg && pos);
};
const insidePoly = (px, py, poly) => {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [xi, yi] = poly[i], [xj, yj] = poly[j];
    if ((yi > py) !== (yj > py) && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
};
const nearSegment = (px, py, { a, b }, r) => {
  const dx = b[0] - a[0], dy = b[1] - a[1];
  const t = Math.max(0, Math.min(1, ((px - a[0]) * dx + (py - a[1]) * dy) / (dx * dx + dy * dy)));
  const x = a[0] + t * dx - px, y = a[1] + t * dy - py;
  return x * x + y * y <= r * r;
};

// Cor RGBA de um ponto no espaço 1024.
function sample(x, y, { mono, small }) {
  const g = small ? glyphSmall : glyph;
  const r = (small ? 76 : stroke) / 2;
  const inCircle = (x - C.x) ** 2 + (y - C.y) ** 2 <= R * R;
  const onGlyph = g.some(s => nearSegment(x, y, s, r));
  if (mono) {
    if (onGlyph) return [255, 255, 255, 255];
    if (inCircle) return null;
    return insidePoly(x, y, flame) ? [255, 255, 255, 255] : null;
  }
  if (onGlyph) return [255, 255, 255, 255];
  if (inCircle) return [...hex(x + y < C.x + C.y ? circleTop : circleBottom), 255];
  for (const f of facets) if (insideTri(x, y, f.tri)) return [...hex(f.color), 255];
  return null;
}

function render(size, opts = {}) {
  const ss = size <= 64 ? 8 : size <= 256 ? 4 : 3;
  const scale = 1024 / size;
  const px = Buffer.alloc(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < ss; sy++) {
        for (let sx = 0; sx < ss; sx++) {
          const c = sample((x + (sx + 0.5) / ss) * scale, (y + (sy + 0.5) / ss) * scale, opts);
          if (c) { r += c[0] * c[3]; g += c[1] * c[3]; b += c[2] * c[3]; a += c[3]; }
        }
      }
      const i = (y * size + x) * 4;
      if (a > 0) { px[i] = r / a; px[i + 1] = g / a; px[i + 2] = b / a; px[i + 3] = a / (ss * ss); }
    }
  }
  return png(size, size, px);
}

// ---------- PNG e ICO ----------
const crcTable = new Uint32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc32 = buf => { let c = 0xffffffff; for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
function png(w, h, rgba) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0)),
  ]);
}
// ICO com PNG embutido (Windows Vista em diante), um por tamanho.
function ico(images) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(images.length, 4);
  let offset = 6 + 16 * images.length;
  const dir = images.map(({ size, data }) => {
    const e = Buffer.alloc(16);
    e[0] = size >= 256 ? 0 : size; e[1] = size >= 256 ? 0 : size; e[2] = 0; e[3] = 0;
    e.writeUInt16LE(1, 4); e.writeUInt16LE(32, 6); e.writeUInt32LE(data.length, 8); e.writeUInt32LE(offset, 12);
    offset += data.length;
    return e;
  });
  return Buffer.concat([header, ...dir, ...images.map(i => i.data)]);
}

// ---------- Saída ----------
const write = (name, data) => fs.writeFileSync(path.join(OUT, name), data);
write('forge-logo.svg', svg());
write('forge-logo-branca.svg', svg({ mono: true }));
write('forge-logo-pequeno.svg', svg({ small: true }));

const only = process.argv[3]; // "preview": só o 1024 para conferir
write('forge-logo.png', render(1024));
if (only !== 'preview') {
  write('forge-logo-branca.png', render(1024, { mono: true }));
  const icon = [];
  for (const size of [16, 24, 32, 48, 64, 128, 256, 512]) {
    const data = render(size, { small: size <= 24 });
    write(`forge-logo-${size}.png`, data);
    if (size <= 256) icon.push({ size, data });
  }
  write('app.ico', ico(icon));
}
console.log('ok', fs.readdirSync(OUT).join(' '));
