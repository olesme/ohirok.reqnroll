// Prints the LUCIDE icon block for report.js from lucide-static's SVG files (build-time helper, not part of any build).
// Usage: npm install lucide-static@1.69.0 in a scratch folder, then
//   node build/lucide-icons.js <scratch>/node_modules/lucide-static > block.js
// and paste block.js over the LUCIDE object in report.js (keep the /*! license */ comment above it).
const fs = require('fs');
const path = require('path');
const pkg = process.argv[2] || path.join(__dirname, 'node_modules/lucide-static');
const dir = path.join(pkg, 'icons');
const version = require(path.join(pkg, 'package.json')).version;
const names = ['chart-column', 'printer', 'refresh-cw', 'hash', 'square-arrow-out-up-right', 'moon', 'sun',
  'sliders-horizontal', 'funnel', 'funnel-plus', 'x', 'chevron-left', 'chevron-right', 'panel-left-close', 'panel-left-open',
  'maximize-2', 'download', 'zoom-in', 'zoom-out', 'file', 'external-link', 'history', 'settings',
  'layers', 'play', 'folder', 'shield', 'rows-3', 'table', 'file-text', 'rotate-ccw', 'chevrons-up-down', 'chevrons-down-up'];
const lines = names.map(n => {
  const svg = fs.readFileSync(path.join(dir, n + '.svg'), 'utf8');
  const inner = svg.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>[\s\S]*$/, '').replace(/\s+/g, ' ').replace(/> </g, '><').trim();
  return `        '${n}': '${inner.replace(/'/g, "\\'")}'`;
});
process.stdout.write(`    var LUCIDE = {
${lines.join(',\n')}
    };
`);
