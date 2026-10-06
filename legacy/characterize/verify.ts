// v1's verifyItem against recorded XIVAPI and Garland Tools responses.
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [fixtures, out] = process.argv.slice(2);
const read = (name: string) => readFileSync(join(fixtures, name), 'utf8');
globalThis.fetch = (async (input: any) => {
  const url = new URL(String(input));
  let body: string | null = null;
  if (url.host === 'v2.xivapi.com') {
    const q = url.searchParams.get('query')!;
    const name = q.match(/Name[=~]"(.*)"/)![1];
    const file = (q.includes('~') ? 'search-fuzzy-' : 'search-') + name.replace(/ /g, '_') + '.json';
    try { body = read(file); } catch { body = '{"results":[]}'; }
  } else if (url.pathname.includes('/core/')) {
    body = read('garland-core-locations.json');
  } else {
    const id = url.pathname.match(/(\d+)\.json$/)![1];
    body = read(`garland-${id}.json`);
  }
  return new Response(body, { status: 200 });
}) as typeof fetch;

const LEGACY = new URL('../electron/desktop', import.meta.url).href;
const { verifyItem } = await import(`${LEGACY}/src/main/core/verify.service.ts`);
const tracked = new Set<number>([5121]);
const queries = ['Zinc Ore', 'Raw Imperial Jade', 'Cloud Mica', 'Mythrite Ingot', 'Distilled Water', 'Grade 8 Dark Matter', 'Darksteel Ore', 'Gilded Purple Pixie', 'Imperial Jad'];
const results: Record<string, unknown> = {};
for (const q of queries) results[q] = await verifyItem(q, tracked);
writeFileSync(out, JSON.stringify(results, null, 1) + '\n');
console.log(Object.keys(results).length, 'verified');
