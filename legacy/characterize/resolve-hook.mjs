// Lets Node run the v1 TypeScript as-is: extensionless sibling imports resolve to ".ts", and
// names that are only types (interfaces, type aliases) are dropped from import lists, because
// Node's type stripping keeps every imported name.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const LEGACY = fileURLToPath(new URL('../electron/desktop', import.meta.url));
const typeNames = new Set();
function scan(dir) {
  for (const f of readdirSync(dir)) {
    const p = join(dir, f);
    if (f === 'node_modules') continue;
    if (statSync(p).isDirectory()) scan(p);
    else if (p.endsWith('.ts')) {
      for (const m of readFileSync(p, 'utf8').matchAll(/export\s+(?:interface|type)\s+(\w+)/g)) typeNames.add(m[1]);
    }
  }
}
scan(join(LEGACY, 'src'));
scan(join(LEGACY, 'renderer', 'src'));

export async function resolve(specifier, context, next) {
  try {
    return await next(specifier, context);
  } catch (e) {
    if (specifier.startsWith('.') && !/\.[cm]?[jt]s$/.test(specifier)) return next(specifier + '.ts', context);
    throw e;
  }
}

export async function load(url, context, next) {
  const result = await next(url, context);
  if (!url.endsWith('.ts') || !url.includes('legacy')) return result;
  let source = String(result.source);
  source = source.replace(/import\s*\{([^}]*)\}\s*from\s*('[^']*');/g, (_all, names, from) => {
    const kept = names.split(',').map((n) => n.trim()).filter((n) => n && !typeNames.has(n.split(/\s+as\s+/)[0]));
    return kept.length ? `import { ${kept.join(', ')} } from ${from};` : '';
  });
  // Angular/Electron-only modules are never imported by the files the harness loads.
  return { ...result, source, format: 'module-typescript' };
}
