/**
 * Saddlebag Exchange marketshare client — trend states (spiking/increasing/…)
 * and weekly sold counts. Optional: a sweep completes without trend data if
 * this endpoint is down, so failures resolve to null instead of throwing.
 */
import { SaddlebagParams } from '../../shared/types';

export interface SaddlebagRow {
  name: string;
  itemID: string;
  avg: number;
  quantitySold: number;
  state: string;
}

export async function fetchSaddlebag(world: string, params: SaddlebagParams): Promise<SaddlebagRow[] | null> {
  try {
    const r = await fetch('https://api.saddlebagexchange.com/api/ffxivmarketshare', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'User-Agent': 'gil-sweep/0.1' },
      body: JSON.stringify({
        server: world,
        time_period: params.timePeriod,
        sales_amount: params.salesAmount,
        average_price: params.averagePrice,
        filters: params.filters,
        sort_by: 'marketValue',
      }),
      signal: AbortSignal.timeout(30000),
    });
    if (!r.ok) return null;
    const j: any = await r.json();
    return j.data ?? null;
  } catch (e) {
    console.warn('[gil-sweep] Saddlebag unavailable:', (e as Error).message);
    return null;
  }
}
