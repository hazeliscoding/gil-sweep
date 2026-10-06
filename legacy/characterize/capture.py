# Captures real market responses for Gil Sweep fixtures. Args: <repo data dir> <out dir>
import json, sys, time, urllib.request, os
data, out = sys.argv[1], sys.argv[2]
UA = {'User-Agent': 'GilSweep fixture capture (github.com/hazeliscoding/gil-sweep)'}
def get(url, body=None):
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body else None,
        headers={**UA, **({'Content-Type':'application/json'} if body else {})})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())
items = json.load(open(os.path.join(data,'items.json'),encoding='utf-8'))
crafts = json.load(open(os.path.join(data,'crafts.json'),encoding='utf-8'))
demand = json.load(open(os.path.join(data,'garland-demand.json'),encoding='utf-8'))
ids = []
for i in items: ids.append(i['id'])
for d in demand.values():
    for c in d['consumers']: ids.append(c['id'])
for r in crafts.values():
    for ing in r['ingredients']: ids.append(ing['id'])
# v1 order: dedupe preserving first occurrence
seen=set(); uniq=[]
for i in ids:
    if i not in seen: seen.add(i); uniq.append(i)
print('ids', len(uniq), 'chunks', (len(uniq)+99)//100)
world='Cactuar'
agg=[]
for k in (range(0,len(uniq),100) if not os.path.exists(os.path.join(out,'aggregated-all.json')) else []):
    chunk=uniq[k:k+100]
    j=get(f'https://universalis.app/api/v2/aggregated/{world}/'+','.join(map(str,chunk)))
    agg.extend(j.get('results',[])); time.sleep(0.3)
if agg: json.dump({'results':agg}, open(os.path.join(out,'aggregated-all.json'),'w'))
farm=[i['id'] for i in items]
cur={}
for k in range(0,len(farm),20):
    chunk=farm[k:k+20]
    j=get(f'https://universalis.app/api/v2/{world}/'+','.join(map(str,chunk))+'?listings=20&entries=20')
    cur.update(j.get('items',{})); time.sleep(0.3)
json.dump({'items':cur}, open(os.path.join(out,'current-items.json'),'w'))
hist={'items':{}}
for k in range(0,len(farm),25):
    h=get(f'https://universalis.app/api/v2/history/{world}/'+','.join(map(str,farm[k:k+25]))+'?entriesToReturn=300'); hist['items'].update(h.get('items',{})); time.sleep(0.3)
json.dump(hist, open(os.path.join(out,'history-items.json'),'w'))
worlds=get('https://universalis.app/api/v2/worlds'); json.dump(worlds, open(os.path.join(out,'worlds.json'),'w'))
dcs=get('https://universalis.app/api/v2/data-centers'); json.dump(dcs, open(os.path.join(out,'data-centers.json'),'w'))
try:
    sb=get('https://api.saddlebagexchange.com/api/ffxivmarketshare', {'server':world,'time_period':168,'sales_amount':2,'average_price':50,'filters':[47,48,49],'sort_by':'marketValue'})
    json.dump(sb, open(os.path.join(out,'saddlebag.json'),'w'))
except Exception as e: print('saddlebag failed', e)
print('done')
