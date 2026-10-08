#!/usr/bin/env python3
"""Aggregate real trial summaries; never treat illustrative allocation costs as timings."""
import argparse,csv,json,statistics
from collections import defaultdict
from pathlib import Path

def analyze(root):
    groups=defaultdict(list)
    for path in Path(root).rglob('summary.json'):
        row=json.loads(path.read_text(encoding='utf-8'))
        # Device, scene and task must match before comparing conditions.
        groups[(row['device'],row['scene'],row['task'],row['policy'])].append(row)
    out=[]
    for (device,scene,task,policy),rows in sorted(groups.items()):
        measured=[r for r in rows if r.get('frames',0)>0]
        out.append(dict(device=device,scene=scene,task=task,policy=policy,trials=len(rows),measured_trials=len(measured),success_rate=sum(r['success'] for r in rows)/len(rows),median_duration_s=statistics.median(r['durationSeconds'] for r in rows),median_p95_ms=statistics.median(r['p95FrameMs'] for r in measured) if measured else '',violations=sum(r['requirementViolations'] for r in rows)))
    return out

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('results',type=Path);p.add_argument('--output',type=Path,default=Path('comparison.csv'));a=p.parse_args();rows=analyze(a.results)
    if not rows:raise SystemExit('No summary.json trials found')
    with a.output.open('w',newline='',encoding='utf-8') as f:w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
    print(f'{len(rows)} device/scene/task/policy groups written to {a.output}')
if __name__=='__main__':main()
