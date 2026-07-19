/**
 * Live market drill-down for one item: current listing depth + recent sales,
 * with the derived numbers the detail panel shows. Fetched on click, never
 * cached — undercut prices go stale in minutes.
 */
import { MarketDetail } from '../../shared/types';

const median = (arr: number[]): number =>
  arr.length ? [...arr].sort((a, b) => a - b)[Math.floor(arr.length / 2)] : 0;

export async function marketDetail(world: string, id: number): Promise<MarketDetail> {
  const r = await fetch(`https://universalis.app/api/v2/${world}/${id}?listings=30&entries=100`, {
    signal: AbortSignal.timeout(30000),
  });
  if (!r.ok) throw new Error(`Universalis HTTP ${r.status}`);
  const m: any = await r.json();
  const listings = ((m.listings ?? []) as any[]).sort((a, b) => a.pricePerUnit - b.pricePerUnit);
  const sales = (m.recentHistory ?? []) as any[];

  const curMin = listings[0]?.pricePerUnit ?? 0;
  const listedQty = listings.reduce((s, l) => s + l.quantity, 0);
  const spanDays =
    sales.length > 1 ? Math.max((sales[0].timestamp - sales[sales.length - 1].timestamp) / 86400, 0.05) : 1;
  const soldQty = sales.reduce((s, e) => s + e.quantity, 0);
  const unitsPerDay = +(soldQty / spanDays).toFixed(1);
  const daysInv = unitsPerDay > 0 ? +((listedQty / unitsPerDay).toFixed(1)) : null;

  return {
    curMin,
    medPPU: median(sales.map((e) => e.pricePerUnit)),
    listedQty,
    unitsPerDay,
    daysInv,
    listings: listings.slice(0, 10).map((l) => ({ ppu: l.pricePerUnit, qty: l.quantity })),
    sales: sales.map((e) => ({ ppu: e.pricePerUnit, qty: e.quantity, t: e.timestamp * 1000 })),
  };
}
