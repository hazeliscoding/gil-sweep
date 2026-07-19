/**
 * The verify-and-track pipeline, productized from the curation workflow that
 * built the bundled DB. The load-bearing rule: an item is gatherable only if
 * Garland's `item.nodes` lists nodes for THE ITEM ITSELF — node partials alone
 * can belong to a crafted item's ingredients (the Desert Lapis lesson). Top
 * sellers with no nodes get classified (crafted / vendor / likely FC voyage)
 * so they can be tracked as never-farm traps instead of resurfacing weekly.
 */
import { Expansion, TrackedItem, VerifyResult } from '../../shared/types';

const expansionOf = (lvl: number): Expansion =>
  lvl <= 50 ? 'ARR' : lvl <= 60 ? 'HW' : lvl <= 70 ? 'StB' : lvl <= 80 ? 'ShB' : lvl <= 90 ? 'EW' : 'DT';

async function getJson(url: string): Promise<any> {
  const r = await fetch(url, { signal: AbortSignal.timeout(20000) });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
  return r.json();
}

/** Garland zone id → name, fetched once per session (big file). */
let locationIndex: Record<string, { name: string }> | null = null;
async function zoneName(zoneId: number | undefined): Promise<string> {
  if (!zoneId) return '';
  try {
    locationIndex ??= (await getJson('https://garlandtools.org/db/doc/core/en/3/data.json')).locationIndex;
    return locationIndex?.[String(zoneId)]?.name ?? '';
  } catch {
    return '';
  }
}

export async function verifyItem(
  query: string,
  existingIds: Set<number>,
): Promise<{ result: VerifyResult; item?: TrackedItem }> {
  // 1. Resolve the name to an item id (exact first, fuzzy as fallback).
  let hit: any;
  for (const q of [`Name="${query}"`, `Name~"${query}"`]) {
    const res = await getJson(
      `https://v2.xivapi.com/api/search?sheets=Item&query=${encodeURIComponent(q)}&fields=Name`,
    );
    hit = res.results?.[0];
    if (hit) break;
  }
  if (!hit) return { result: { found: false, gatherable: false, reason: `no item named "${query}"` } };
  const id: number = hit.row_id;
  const name: string = hit.fields?.Name ?? query;
  if (existingIds.has(id)) {
    return { result: { found: true, id, name, gatherable: false, alreadyTracked: true, reason: 'already tracked' } };
  }

  // 2. Garland: does the item itself have gathering nodes?
  const doc = await getJson(`https://garlandtools.org/db/doc/item/en/3/${id}.json`);
  const it = doc.item ?? {};
  const nodeIds = new Set(((it.nodes ?? []) as number[]).map(Number));
  const nodes = ((doc.partials ?? []) as any[])
    .filter((p) => p.type === 'node' && nodeIds.has(Number(p.id)))
    .map((p) => p.obj);

  if (nodes.length) {
    const n = nodes[0];
    const lt = String(n.lt ?? '').toLowerCase();
    const kind = lt.includes('legendary')
      ? 'legendary'
      : lt.includes('unspoiled')
        ? 'unspoiled'
        : lt.includes('ephemeral')
          ? 'ephemeral'
          : 'node';
    const spawns = [...new Set(nodes.flatMap((x) => (x.ti ?? []) as number[]))].sort((a, b) => a - b);
    const zone = await zoneName(n.z);
    const item: TrackedItem = {
      name,
      id,
      job: (n.t ?? 0) <= 1 ? 'MIN' : 'BTN',
      level: n.l ?? 0,
      where: [n.n, zone].filter(Boolean).join(', ') || 'unverified',
      kind,
      expansion: expansionOf(n.l ?? 0),
      ...(spawns.length ? { spawns } : {}),
      note: 'added in-app (Garland-verified nodes)',
    };
    return {
      result: {
        found: true,
        id,
        name,
        gatherable: true,
        reason: `${kind} lv${n.l}${spawns.length ? ` · spawns ${spawns.join('/')} ET` : ''} · ${item.where}`,
      },
      item,
    };
  }

  // 3. No nodes → classify the trap so it stops resurfacing in "not tracked yet".
  if (it.craft?.length) {
    return {
      result: { found: true, id, name, gatherable: false, reason: 'crafted item — the Crafting page covers these; not tracked' },
    };
  }
  const hasVendor = !!(it.vendors?.length || it.tradeShops?.length);
  const item: TrackedItem = {
    name,
    id,
    job: 'both',
    level: 0,
    where: hasVendor ? 'vendor purchase' : 'unknown source (no nodes/recipe/vendor)',
    kind: hasVendor ? 'vendor' : 'submarine',
    expansion: 'ARR',
    note: `added in-app - NOT gatherable${hasVendor ? '' : '; likely FC voyage loot'}`,
  };
  return {
    result: {
      found: true,
      id,
      name,
      gatherable: false,
      reason: hasVendor
        ? 'vendor item — tracked as a non-farm so it stops resurfacing'
        : 'no nodes, recipe, or vendor — likely FC voyage loot; tracked as a non-farm',
    },
    item,
  };
}
