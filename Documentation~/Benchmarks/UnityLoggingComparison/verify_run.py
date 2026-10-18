"""Verify actual output after Unity has exited; never runs inside timed loops."""
from pathlib import Path
import hashlib
import json
import re
import sys

directory=Path(sys.argv[1])
result=json.loads((directory/'result.json').read_text(encoding='utf-8-sig'))
assert result['measuredCalls']==100000 and result['warmupCalls']==1000
assert result['unityVersion']=='6000.6.6f1' and result['batchMode']
assert not result['unityCapture'] and result['ringBuffers']==0
assert result['unityStackPolicy']==result['traceforgeStackPolicy']=='None'
assert result['handler']=='UnityEngine.DebugLogHandler'
assert result['loggingEnabled'] and result['unityLogFilter']=='Log'
assert result['producerTicks']>0 and result['stopwatchFrequency']>0
assert result['gcControlSamples']>0
assert result['registeredSinks']==(1 if result['mode']=='traceforge' else 0)
if result['mode']=='traceforge':
    assert result['queueCapacity']==4096
    assert result['acceptedEntries']==result['writtenEntries']==101000
    assert result['hasFlushCompletion'] and result['producerAndFlushTicks']>=result['producerTicks']
else:
    assert not result['hasFlushCompletion']

pattern=re.compile(r'TFBENCH (WARM|DATA) (\d{6}) \| Player=7 Health=100 X=12\.5 Y=0 Z=8\.0$')
output=Path(result['outputPath'])
sequences={'WARM':[],'DATA':[]}
with output.open(encoding='utf-8-sig',errors='strict') as stream:
    for line in stream:
        found=pattern.search(line.rstrip('\r\n'))
        if found: sequences[found[1]].append(int(found[2]))
assert sequences['WARM']==list(range(1000)),('warmup output mismatch',len(sequences['WARM']))
assert sequences['DATA']==list(range(100000)),('timed output mismatch',len(sequences['DATA']))
native=Path(result['nativeLogPath'])
native_text=native.read_text(encoding='utf-8-sig',errors='replace')
assert not re.search(r'\b(?:error|warning)\s+(?:CS|UAC)\d+',native_text), 'Compilation diagnostics present'
if result['mode']=='traceforge':
    assert 'TFBENCH DATA ' not in native_text,'TF calls unexpectedly reached Unity native log'
receipt={
    'measuredMessages':len(sequences['DATA']),
    'warmupMessages':len(sequences['WARM']),
    'uniqueOrderedSequence':True,
    'outputBytes':output.stat().st_size,
    'outputSha256':hashlib.sha256(output.read_bytes()).hexdigest(),
    'nativeLogSha256':hashlib.sha256(native.read_bytes()).hexdigest(),
    'compilationDiagnostics':0,
    'outputVerifiedAfterProcessExit':True
}
(directory/'verification.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'mode':result['mode'],'producerMs':result['producerTicks']*1000/result['stopwatchFrequency'],**receipt}))
