#!/usr/bin/env node
/**
 * MD → 交互式思维导图
 *
 * 用法:
 *   node scripts/md2mindmap.mjs <input.md> [output.html]
 *
 * 行为:
 *   1. 调用 markmap-cli --offline 把 Markdown 转为自包含 HTML（库已内联，断网可开）
 *   2. 注入标题与「导出 SVG / 导出 PNG」按钮（缩放/折叠为 markmap 内置能力）
 *
 * 输出: 浏览器直接打开，可缩放、折叠/展开节点、导出 SVG/PNG。
 */
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const [, , input, output = input.replace(/\.md$/i, '') + '.mindmap.html'] = process.argv;
if (!input || !input.toLowerCase().endsWith('.md')) {
  console.error('用法: node scripts/md2mindmap.mjs <input.md> [output.html]');
  process.exit(1);
}

// 1. markmap-cli 转换（离线模式，全部资源内联）
// Windows 上 npx 是 .cmd，需 shell:true；参数引号由 Node 统一处理
const result = spawnSync('npx', ['--yes', 'markmap-cli', '--offline', input, '-o', output], {
  stdio: 'inherit',
  shell: process.platform === 'win32',
});
if (result.status !== 0) process.exit(result.status ?? 1);

// 2. 注入导出按钮（window.mm 为 markmap 实例，来自 markmap-cli 模板）
const html = readFileSync(output, 'utf8');
const title = path.basename(input).replace(/\.md$/i, '') + ' · 思维导图';
const inject = `<script>
(() => {
  const svg = document.querySelector('svg#mindmap');
  const save = (name, part, type) => {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([part], { type }));
    a.download = name;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  };
  const bar = document.createElement('div');
  bar.style.cssText = 'position:absolute;bottom:20px;left:20px;z-index:10;display:flex;gap:8px';
  const btn = (label, fn) => {
    const b = document.createElement('button');
    b.textContent = label;
    b.onclick = fn;
    bar.appendChild(b);
  };
  btn('导出 SVG', () => {
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    save('mindmap.svg', new XMLSerializer().serializeToString(clone), 'image/svg+xml');
  });
  btn('导出 PNG', async () => {
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    const img = new Image();
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(clone));
    await img.decode();
    const rect = svg.getBoundingClientRect();
    const c = document.createElement('canvas');
    c.width = rect.width * 2;
    c.height = rect.height * 2;
    const ctx = c.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, c.width, c.height);
    ctx.scale(2, 2);
    ctx.drawImage(img, 0, 0);
    c.toBlob((blob) => save('mindmap.png', blob, 'image/png'));
  });
  document.body.appendChild(bar);
})();
</script>`;
let out = html.replace('<title>Markmap</title>', `<title>${title}</title>`);
out = out.includes('</body>') ? out.replace('</body>', inject + '</body>') : out + inject;
writeFileSync(output, out);

console.log(`已生成思维导图: ${path.resolve(output)}`);
