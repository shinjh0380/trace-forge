"""Recompute the public API comparison using every frozen-plan observation."""
from pathlib import Path
import csv
import hashlib
import json
import math
import shutil
import statistics
import sys

SOURCE=Path(__file__).resolve().parent
evidence=Path(sys.argv[1]).resolve()
plan=json.loads((evidence/'frozen-plan.json').read_text(encoding='utf-8-sig'))
environment=json.loads((evidence/'environment.json').read_text(encoding='utf-8-sig'))
def source_matches(field, filename):
    raw=(SOURCE/filename).read_bytes()
    # Git can normalize Windows checkout line endings; allow only that change.
    lf=raw.replace(b'\r\n',b'\n')
    return environment[field].lower() in {hashlib.sha256(data).hexdigest() for data in (raw,lf,lf.replace(b'\n',b'\r\n'))}
assert source_matches('PlanSha256','plan.json')
assert source_matches('SourceSha256','ReadmeLoggingComparison.cs')
assert source_matches('RunnerSha256','run.ps1')
assert source_matches('VerifierSha256','verify_run.py')
host=json.loads((evidence/'host-after-verification.json').read_text(encoding='utf-8-sig'))
assert not host['ChangedExistingFiles'] and host['HelperRemovedFromAssets']
rows=[]
for index,mode in enumerate(plan['order'],1):
    name=f'run{index:02}-{mode}'
    directory=evidence/name
    result=json.loads((directory/'result.json').read_text(encoding='utf-8-sig'))
    verified=json.loads((directory/'verification.json').read_text(encoding='utf-8-sig'))
    exited=json.loads((directory/'exit.json').read_text(encoding='utf-8-sig'))
    assert exited['ExitCode']==0 and result['mode']==mode
    assert verified['measuredMessages']==100000 and verified['uniqueOrderedSequence']
    assert verified['compilationDiagnostics']==0
    assert hashlib.sha256(Path(result['outputPath']).read_bytes()).hexdigest()==verified['outputSha256']
    extra={}
    for channel in ['stdout','stderr']:
        raw=(directory/f'{channel}.txt').read_bytes()
        extra[channel+'Bytes']=len(raw)
        extra[channel+'MeasuredMarkers']=raw.count(b'TFBENCH DATA ')
    # These are additional output observations, not exclusions or replacements.
    (directory/'extra-channel-verification.json').write_text(json.dumps(extra,indent=2)+'\n',encoding='utf-8')
    target=SOURCE/'results'/name
    target.mkdir(parents=True,exist_ok=True)
    for filename in ['result.json','verification.json','invocation.json','exit.json','extra-channel-verification.json']:
        shutil.copyfile(directory/filename,target/filename)
    rows.append({
        'run':index,'block':(index+1)//2,'mode':mode,
        'producerTicks':result['producerTicks'],'stopwatchFrequency':result['stopwatchFrequency'],
        'producerMs':result['producerTicks']*1000/result['stopwatchFrequency'],
        'producerAndFlushMs':result['producerAndFlushTicks']*1000/result['stopwatchFrequency'] if result['hasFlushCompletion'] else None,
        'allocationRecorderEnabled':result['allocationRecorderEnabled'],'profilerEnabled':result['profilerEnabled'],
        'allocationCallstacksEnabled':result['allocationCallstacksEnabled'],
        'persistedMeasuredMessages':verified['measuredMessages'],'persistedWarmupMessages':verified['warmupMessages'],
        'outputBytes':verified['outputBytes'],**extra
    })
assert len(rows)==10
assert all(sum(r['mode']==mode for r in rows)==5 for mode in ['unity','traceforge'])
medians={mode:statistics.median(r['producerMs'] for r in rows if r['mode']==mode) for mode in ['unity','traceforge']}
pairs=[]
for block in range(1,6):
    pair={row['mode']:row for row in rows if row['block']==block}
    pairs.append(pair['traceforge']['producerMs']/pair['unity']['producerMs'])
logs=[math.log(ratio) for ratio in pairs]
half=2.776445105*statistics.stdev(logs)/math.sqrt(5)
interval=[math.exp(statistics.mean(logs)-half),math.exp(statistics.mean(logs)+half)]
summary={
    'protocol':plan['protocol'],'sourceCommit':environment['ProductCommit'],
    'successfulProcesses':10,'processesPerMode':5,'measuredCallsPerProcess':100000,
    'producerMediansMs':medians,
    'producerRangesMs':{mode:[min(r['producerMs'] for r in rows if r['mode']==mode),max(r['producerMs'] for r in rows if r['mode']==mode)] for mode in ['unity','traceforge']},
    'medianCallerSpeedup':medians['unity']/medians['traceforge'],
    'medianCallerTimeReductionPercent':100*(1-medians['traceforge']/medians['unity']),
    'pairedProducerRatiosTfOverUnity':pairs,
    'descriptivePairedLogRatioT95Interval':interval,
    'directionSupportedInObservedWorkload':interval[1]<1,
    'traceforgeProducerAndFlushMedianMs':statistics.median(r['producerAndFlushMs'] for r in rows if r['mode']=='traceforge'),
    'unityComparableFlushCompletion':None,
    'allMessagesPersistedInOrder':True,
    'allCompilationDiagnostics':0,
    'excludedSuccessfulRuns':0,
    'extraStdoutOrStderrData':any(r['stdoutMeasuredMarkers'] or r['stderrMeasuredMarkers'] for r in rows),
    'existingHostFilesUnchanged':host['ExistingHostFiles'],
    'hostHelperRemoved':host['HelperRemovedFromAssets'],
    'rows':rows
}
(SOURCE/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
with (SOURCE/'measurements.csv').open('w',newline='',encoding='utf-8') as stream:
    writer=csv.DictWriter(stream,fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
shutil.copyfile(evidence/'environment.json',SOURCE/'environment.json')
shutil.copyfile(evidence/'host-after-verification.json',SOURCE/'host-verification.json')
print(json.dumps({key:value for key,value in summary.items() if key!='rows'},indent=2))
