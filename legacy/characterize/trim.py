# Trims captured responses to the fields Gil Sweep reads; keeps the API's own shapes.
import json, os, sys
cap, out = sys.argv[1], sys.argv[2]
os.makedirs(os.path.join(out,'Universalis'), exist_ok=True); os.makedirs(os.path.join(out,'Saddlebag'), exist_ok=True)
def scoped(o):
    return {k: {kk: vv for kk, vv in v.items() if kk in ('price','quantity')} for k, v in (o or {}).items() if k in ('world','dc','region')}
agg = json.load(open(os.path.join(cap,'aggregated-all.json')))['results']
res = []
for r in agg:
    t = {'itemId': r['itemId']}
    for q in ('nq','hq'):
        t[q] = {f: scoped(r.get(q,{}).get(f)) for f in ('minListing','averageSalePrice','dailySaleVelocity')}
    t['worldUploadTimes'] = r.get('worldUploadTimes', [])
    res.append(t)
json.dump({'results': res}, open(os.path.join(out,'Universalis','aggregated-cactuar.json'),'w'), separators=(',',':'))
LISTING = ('pricePerUnit','quantity','hq','retainerName','lastReviewTime')
SALE = ('pricePerUnit','quantity','hq','timestamp')
TOP = ('itemID','worldID','worldName','lastUploadTime','stackSizeHistogram','listingsCount','recentHistoryCount','unitsForSale','unitsSold','nqSaleVelocity','hqSaleVelocity','regularSaleVelocity','averagePrice','minPrice','hasData')
def cur(v):
    t = {k: v[k] for k in TOP if k in v}
    t['listings'] = [{k: l[k] for k in LISTING if k in l} for l in v.get('listings',[])]
    t['recentHistory'] = [{k: e[k] for k in SALE if k in e} for e in v.get('recentHistory',[])]
    return t
c = json.load(open(os.path.join(cap,'current-items.json')))['items']
json.dump({'items': {k: cur(v) for k, v in c.items()}}, open(os.path.join(out,'Universalis','current-cactuar.json'),'w'), separators=(',',':'))
h = json.load(open(os.path.join(cap,'history-items.json')))['items']
hist = {k: {'itemID': v['itemID'], 'worldID': v.get('worldID'), 'worldName': v.get('worldName'), 'lastUploadTime': v.get('lastUploadTime'),
            'entries': [{kk: e[kk] for kk in SALE if kk in e} for e in v.get('entries',[])]} for k, v in h.items()}
json.dump({'items': hist}, open(os.path.join(out,'Universalis','history-cactuar.json'),'w'), separators=(',',':'))
for f in ('worlds.json','data-centers.json'):
    json.dump(json.load(open(os.path.join(cap,f))), open(os.path.join(out,'Universalis',f),'w'), indent=1)
sb = json.load(open(os.path.join(cap,'saddlebag.json')))['data']
KEEP = ('name','itemID','avg','median','quantitySold','state','percentChange')
rows = [{k: r[k] for k in KEEP if k in r} for r in sb]
json.dump({'data': rows}, open(os.path.join(out,'Saddlebag','marketshare-cactuar.json'),'w'), indent=1)
from collections import Counter
print(Counter(r['state'] for r in rows))
print(sorted((os.path.getsize(os.path.join(dp,f)), os.path.join(dp,f)) for dp,_,fs in os.walk(out) for f in fs))
