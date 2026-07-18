/**
 * Retainer selling advice: pulls live listings + recent sales for the target
 * items and recommends list price, stack size, and priority. Ported from the
 * ffxiv-market-sweep CLI (retainer-plan.mjs) — the heuristics are deliberate:
 *  - stack = median quantity of *actual sales*, rounded to a natural size
 *  - crashed floor: cheapest listing far under the sales-median clearing price
 *    → don't chase it down, list near what buyers actually pay
 *  - days of inventory = listed quantity ÷ live sales rate → saturation verdict
 */
import { RetainerAdvice, RetainerTarget } from '../../shared/types';

const median = (arr: number[]): number =>
  arr.length ? [...arr].sort((a, b) => a - b)[Math.floor(arr.length / 2)] : 0;

/** Round a raw quantity to the stack sizes people actually list at. */
const niceStack = (n: number): number =>
  n >= 5000 ? 9999 : n >= 500 ? 999 : n >= 50 ? 99 : n >= 10 ? Math.round(n / 10) * 10 : Math.max(1, Math.round(n));

export async function retainerPlan(world: string, targets: RetainerTarget[]): Promise<RetainerAdvice[]> {
  const out: RetainerAdvice[] = [];
  for (let i = 0; i < targets.length; i += 100) {
    const chunk = targets.slice(i, i + 100);
    const r = await fetch(
      `https://universalis.app/api/v2/${world}/${chunk.map((t) => t.id).join(',')}?listings=50&entries=50`,
      { signal: AbortSignal.timeout(30000) },
    );
    if (!r.ok) throw new Error(`Universalis HTTP ${r.status}`);
    const j: any = await r.json();
    const byId = j.items ?? { [j.itemID]: j }; // single-item responses aren't wrapped
    for (const t of chunk) out.push(adviseOne(t, byId[t.id] ?? {}));
  }
  return out;
}

function adviseOne(t: RetainerTarget, m: any): RetainerAdvice {
  const listings = ((m.listings ?? []) as any[]).sort((a, b) => a.pricePerUnit - b.pricePerUnit);
  const sales = (m.recentHistory ?? []) as any[];
  const curMin = listings[0]?.pricePerUnit ?? 0;
  const listedQty = listings.reduce((s, l) => s + l.quantity, 0);
  const spanDays =
    sales.length > 1 ? Math.max((sales[0].timestamp - sales[sales.length - 1].timestamp) / 86400, 0.05) : 1;
  const soldQty = sales.reduce((s, e) => s + e.quantity, 0);
  const unitsPerDay = soldQty / spanDays;
  const medStack = median(sales.map((e) => e.quantity));
  const medPPU = median(sales.map((e) => e.pricePerUnit));
  const daysInv = unitsPerDay > 0 ? listedQty / unitsPerDay : Infinity;
  const crashedFloor = curMin > 0 && medPPU > 0 && curMin < medPPU * 0.6;
  const listPrice = curMin === 0 ? medPPU : crashedFloor ? Math.round(medPPU * 0.95) : Math.max(curMin - 1, 1);
  return {
    id: t.id,
    curMin,
    medPPU,
    listPrice,
    stack: t.kind === 'map' ? 1 : niceStack(medStack),
    daysInv: Number.isFinite(daysInv) ? +daysInv.toFixed(1) : null,
    unitsPerDay: Math.round(unitsPerDay),
    verdict:
      curMin === 0
        ? 'EMPTY MARKET — list high'
        : crashedFloor
          ? 'floor crashed — hold price'
          : daysInv > 7
            ? 'saturated — low priority'
            : daysInv < 1
              ? 'shortage — price up'
              : 'healthy',
  };
}
