# Native Logger 성능 재검증

## 목적과 판정 범위

2026-10-07 릴리스 preflight의 성능 차단 사유를 재검증한다. 측정 완료, 기능 테스트, 역사적 rig 재현성, producer 회귀 기준, 전체 성능 승인을 각각 구분한다. 기존 보고서와 임계값은 변경하지 않는다. 런타임 변경과 릴리스 작업은 수행하지 않는다.

## 시작 상태와 환경

- 공식/진단 성능 측정 날짜: 2026-10-13, Asia/Seoul. 호스트 복구·후속 검증·보고서 리뷰는 2026-10-14에 이어졌다.
- 브랜치: `feature/native-logger`, 시작 HEAD `e01a9c023d8bb4b081de311dfa5e186359c43cfb`, 시작 작업 트리 clean.
- dev: `e22d6185caf70627d7b06089c86046759824a033`; main: `3baeb05071e6291d5abd57e8182b3429f53b8cd2`.
- Unity 6000.3.8f1 (`1c7db571dde0`), Unity Test Framework 1.6.0, Windows Editor PlayMode batchmode.
- Windows 11 Home 10.0.26200, i7-13700HX 16코어/24논리 프로세서, 물리 메모리 33,240,568 KiB.
- C: 저장장치: SAMSUNG MZVL21T0HDLU-00BH1, NVMe SSD, 1,024,209,543,168 bytes. 배터리 상태 2, 충전량 100%; 활성 전원 구성표 Balanced. 과거 전원 모드/온도/클럭/충전 상태는 확인 불가.
- 시작 호스트: `.worktrees/native-logger-test-host`, 로컬 패키지 root, defines empty, EnterPlayModeOptionsEnabled=0 / options=0 (domain/scene reload enabled).
- 현재 배경 작업 프로세스가 관측되며 측정 중 종료하거나 전원 정책을 바꾸지 않는다. 실행별 시각/전체 CPU/프로세스 CPU 표본은 진단 자료이며 timed region의 연속 프로파일이 아니다.
- 산출물: `.worktrees/native-logger-test-host/.performance-revalidation-20261013/`. 기존 Phase 4/Session 7 산출물은 보존한다.

## 조건 대조

| 조건 | 2026-07-22 기록 | 2026-09-22 Phase 4 | 이번 재검증 |
|---|---|---|---|
| 소스 | d98821c async | f95dcc2 baseline / 61ffa6e current | f95dcc2 / 시작 HEAD e01a9c0 |
| 관련 소스 | FileSink/LogEntry | f95dcc2와 d98821c 관련 소스 동일 | 두 커밋 FileSink/LogEntry/ILogSink/Runtime asmdef diff 없음; 61ffa6e 대비 Runtime 차이는 LogCategory XML 주석 11줄뿐 |
| Unity/OS/CPU | 6000.3.8f1 / 26200 / i7-13700HX | 동일 기록 | 동일 버전/CPU 확인 |
| Test Framework | 과거 실제 호스트 원본 확인 불가 | 1.6.0 | 1.6.0 |
| domain reload | 기록 없음, 확인 불가 | disabled, scene reload enabled | 공식/진단 측정은 Phase 4처럼 disabled; 복원 후 전체 테스트는 기본 enabled |
| warm-up | 별도 sink 1,000회, Flush/Dispose | 동일 | 기존 Phase4Benchmarks 코드의 warm-up 재사용 |
| entry | Info, Default, prebuilt message, UTC ticks, exception null | 동일; 확장 entry는 stack null/context 0 | 동일 값 구성; 매 프로세스 UTC ticks가 달라지는 점 유지 |
| queue | 101,024 | 101,024 | 101,024 |
| 파일 경로 | 실행 계획/fixture는 OS temp; 당시 실제 파일 경로/저장장치 확인 불가 | host Artifacts/Phase4 | 같은 C: host 아래 이번 실행별 고유 파일; 과거 출력은 덮어쓰지 않음 |
| append | false | false | false |
| timer | 보고서는 GetTimestamp, 계획 초안은 별도 Stopwatch 둘; 실제 transient benchmark 원본 없음 | 동일 GetTimestamp 시작점 | Phase 4 실제 루프 유지 |
| GC 계측 | 실행 보고서는 ProfilerRecorder/Internal/GC.Alloc/current thread/capacity16; 계획 초안은 GC.GetAllocatedBytesForCurrentThread | recorder.Count 차이 | Phase 4 recorder 유지; event/sample proxy이며 bytes 아님 |
| 순서 | async 새 프로세스 5회 | C1→C2→C3→C4, 각 5회 | 공식 순서 동일; 교대 비교는 별도 |
| 전원/부하/캐시 | 확인 불가 | 확인 불가 | 전원/배경 부하 표본 기록; 캐시/thermal/클럭은 통제하지 않음 |

기존 실행 계획 코드와 실제 측정 보고서의 차이는 확인된 사실이다. 더 빠르거나 느린 숫자만으로 이 차이를 원인이라고 단정하지 않는다. 현재 FileSink 변화는 더 큰 LogEntry와 null StackTrace 검사이며, 직접 sink 호출에 Logger 정책 읽기는 포함되지 않는다.

## 사전 고정 측정 계획

아래 계획은 첫 Unity 측정 실행 전에 작성한다. 성공 실행은 모두 포함하며 극단값을 제거하지 않는다. 실패 실행은 XML/log/이유를 남기고 같은 ID를 덮어쓰지 않는다. 실패 수정이 필요하면 새 ID/별도 세트로 기록한다. 타임아웃은 240초이며 해당 프로세스만 종료한다.

1. **공식 세트 O**: C1 f95dcc2 5회 → C2 e01a9c0/None/capture off 5회 → C3 current/None/capture on 5회 → C4 current/ErrorAndAbove/capture off 5회. 총 20개 독립 Unity 프로세스. C3는 구독만 활성이고 timed region에서 Unity 메시지를 발생시키지 않는다.
2. **교대 진단 세트 A**: baseline, current(C2) 순으로 5쌍, 총 10개 독립 프로세스. 공식 결과를 대체하지 않는다. 동일한 sink-file 루프를 사용해 실행 시각에 따른 드리프트를 비교한다.
3. **TF 경로 진단 세트 T**: 현재 코드만 `TF.Info(prebuilt message)`를 None / ErrorAndAbove 정책으로 번갈아 5회씩, 총 10개 독립 프로세스. capture off, 단일 FileSink, no context, exception null. 1,000 warm-up / 100,000 timed calls / queue101024. IsEnabled, UTC timestamp 생성, 정책 읽기/Info 조기 반환, LogEntry 생성 및 dispatch를 실제 통과한다. Error/stack 캡처 비용, Unity callback, 보간, context lookup 비용은 측정하지 않는다. 승인 임계값 없는 진단 자료다.
4. **조건부 null-writer 진단 N**: 공식 C2와 교대 current completion 중앙값이 각각 baseline보다 높으면 baseline/current를 5쌍 더 실행한다. internal TextWriter.Null 생성자를 reflection으로 timed region 밖에서 선택하며 sink formatting/locks/Flush를 유지하고 파일 I/O를 제거한다. _acceptedCount/_writtenCount를 Flush 뒤 확인한다. 파일/행수는 N/A이고 공식 파일 측정을 대체하지 않는다. 역시 임계값 없는 진단 자료다. 조건 미충족이면 미실행을 기록한다.
5. 각 실행마다 고유 ID (`O-cN-rN`, `A-bN`/`A-cN`, `T-noneN`/`T-errorN`, `N-bN`/`N-cN`)를 사용한다. XML/log/측정 파일/행수 검증과 원시 CSV를 세션 디렉터리에 보존한다. baseline worktree는 `.worktrees/bench-revalidation-20261013`이며 root branch는 바꾸지 않는다.
6. 호스트 기존 Assets, manifest, package lock, ProjectSettings를 백업해 byte 단위로 복원한다. baseline 비호환 호스트 테스트를 수정하지 않고 기존 Assets 전체를 보관한 동안 동일한 최소 benchmark Assets를 사용한다. 새로운 benchmark도 별도 보관해 복원 뒤 남기지 않는다.
7. 측정 뒤 원래 Assets/설정/manifest/lock을 복원하고 기존 run-tests.ps1로 EditMode/PlayMode를 기본 domain reload에서 실행한다. 두 플랫폼 XML의 total/passed/failed/skipped 및 C# diagnostics를 기록한다. 테스트의 호스트 설정 변경도 다시 복원한다.

승인 조건은 역사적 producer **280,278 ticks**, completion **5,497,390 ticks** 각각 ±10%; 공식 C2–4 producer의 C1 대비 증가 ≤5%; 공식/교대 파일 실행 GC proxy=0 및 실제 100,000행이다. completion은 별도 지표이며 producer 기준을 적용하지 않는다. T/N에는 새로운 승인 기준을 만들지 않는다.

## 실행 결과

공식 O 20회, 교대 A 10회, TF T 10회: 총 **40/40** 성공, 제외·재실행 0회. 각 실행은 Test Runner 1/1, 실패/스킵 0이며 모든 파일에서 실제 **100,000행**을 확인했다. 모든 producer GC proxy는 0이다. 아래 값의 주파수는 10,000,000 ticks/s (10,000 ticks = 1ms)이다.

공식 C2 completion 중앙값 3,172,848은 C1 3,573,397보다 낮고, 교대 current 3,185,274도 baseline 3,253,245보다 낮다. 사전 조건은 두 비교에서 모두 증가하는 경우이므로 null-writer N은 **미실행**이다. 이를 N/A나 성공 실행으로 세지 않는다. 이전 completion 증가의 원인이 제거됐다고 단정하지 않는다.

### 공식 프로토콜 판정

| 구성 | Producer 중앙값 (ms) | C1 대비 | Completion 중앙값 (ms) | C1 대비 |
|---|---:|---:|---:|---:|
| C1 baseline | 20.8183 | 기준 | 357.3397 | 기준 |
| C2 None/off | 13.9983 | -32.7596% | 317.2848 | -11.2092% |
| C3 None/on, callback 없음 | 14.1473 | -32.0439% | 326.0938 | -8.7440% |
| C4 ErrorAndAbove/off, 직접 sink | 13.5612 | -34.8592% | 293.8192 | -17.7759% |

| 역사적 rig 지표 | 2026-07-22 중앙값 | 이번 C1 중앙값 | 변화 | 기존 ±10% 판정 |
|---|---:|---:|---:|---|
| Producer ticks | 280,278 | 208,183 | -25.7227% | **FAIL** |
| Completion ticks | 5,497,390 | 3,573,397 | -34.9983% | **FAIL** |

C2–4 producer ≤ C1의 105% 기준은 관측값 기준 **충족**이다. 역사적 rig가 유효하다는 뜻은 아니다. completion에는 producer의 5% 기준을 적용하지 않는다.

### 교대 진단과 TF 경로

교대 baseline/current producer 중앙값은 182,049 / 142,723 ticks이고 completion은 3,253,245 / 3,185,274 ticks다. current producer 변화는 약 -21.60%, completion 약 -2.09%다. 5쌍 중 completion이 current에서 증가한 쌍도 있고 감소한 쌍도 있으며 중앙값은 증가하지 않았다. 같은 baseline 코드의 공식→교대 중앙값도 producer 약 -12.55%, completion 약 -8.96% 변했다. 시간/실행 순서에 따른 변동이 관측됐지만, CPU 전력·클럭·온도·파일 캐시 중 어느 것이 원인인지는 특정할 수 없다.

T의 실제 TF.Info producer 중앙값은 None **23.5877ms**, ErrorAndAbove **24.7067ms**이고 completion은 각각 **332.3442ms**, **313.7578ms**다. IsEnabled, UTC timestamp 생성, 정책 읽기 및 Info 조기 반환, LogEntry 생성, sink dispatch를 포함한다. None/ErrorAndAbove producer 범위는 각각 22.4940–24.7846ms / 22.5070–26.2994ms로 겹친다. 단일 정책 읽기의 독립 비용이나 개선/회귀를 확정하지 않는다. Unity callback, Error stack capture, caller formatting, context ID lookup은 이 T 루프에 없다. **승인 기준 없는 진단 자료**이며 O 결과를 대체하지 않는다.

### 확인된 사실과 원인 가설

- `sizeof(LogEntry)`를 Mono DynamicMethod의 `sizeof` IL로 timed region 밖에서 확인: baseline **40 bytes**, current **56 bytes**. 더 큰 queue 항목/복사량은 사실이지만, 이것이 timing 변화의 원인이라는 인과 실험은 하지 않았다.
- baseline/current 첫 공식 파일 크기는 둘 다 **7,200,003 bytes**이며 100,000행이다. stack null인 경우 추가 스택 출력은 수행되지 않는다.
- 기준과 현재의 FileSink 경로는 enqueue lock/queue copy, worker dequeue, timestamp/verbosity/category formatting, StreamWriter 쓰기, Flush 대기로 구성된다. 현재 diff에는 null StackTrace 검사/출력과 확장 LogEntry가 있으나 직접 sink loop에는 Logger 정책 읽기가 없다.
- 이번 공식/교대에서는 지속적인 completion 중앙값 증가가 관측되지 않아 세부 writer 계측을 발동하지 않았다. 이전 증가를 특정 코드 구간 탓으로 결론 내리지 않는다.
- 실행 전/후 전체 CPU 표본 범위는 0–57%였다. WMI 프로세스 CPU 값은 여러 코어의 합산값일 수 있고 전체 CPU 표본과 동일한 시간 창이 아니다. 이 표본만으로 timed region의 부하나 인과관계를 증명할 수 없다.
- 과거 actual benchmark 소스/호스트, 전원/부하/캐시 조건이 남아 있지 않으므로 동일 환경 복원을 입증할 수 없다. Task 5 계획 초안과 실제 보고서는 timer/GC 계측/플랫폼 명령이 다르며, 실제 보고서 및 보존된 Phase 4 루프를 우선했다.

### 기능 및 할당 회귀

원래 Assets/manifest/lock/ProjectSettings를 해시로 복원한 뒤 기본 domain reload에서 EditMode **31/31** 통과. 첫 PlayMode는 referenced script missing 이후 결과 XML 없이 정지해 약 268초에 해당 Unity 프로세스를 종료했다. 로그와 실패 기록을 보존했다. Library/Temp를 이번 세션 폴더로 이동해 보존하고 새 캐시로 한 번 재실행한 결과 PlayMode **83/83**, 실패/스킵 0으로 통과했다. 기존 테스트·Runtime 코드는 수정하지 않았다. 재실행은 날짜가 넘어 2026-10-14 (Asia/Seoul)에 끝났다. 최초 실패를 성공 결과에서 제외하거나 숨기지 않는다.

기존 `Write_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`와 `LoggerPath_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`는 최종 PlayMode에서 통과했다. 전자는 control proxy 1과 measured proxy 0을, 후자는 `loggerPath=0, policy=None, capture=enabled`를 기록했고 recorder positive control도 통과했다. 이 테스트 검증과 40개 benchmark의 GC.Count 차이 0을 구분한다. 어느 쪽도 worker/caller/stack 경로의 일반적인 0바이트 보장이 아니다. [Unity Count 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.Count.html)는 collected sample count로 정의하며, bytes로 환산하지 않는다.

| 실행 | 테스트 결과 | C# warning 로그 건수 | 고유 경고 | C# error |
|---|---|---:|---:|---:|
| EditMode | 31/31, failed0/skipped0 | 0 | 0 | 0 |
| 최초 PlayMode | startup 정지, XML 없음; 실패 보존 | 0 | 0 | 0 |
| PlayMode retry1 (새 캐시) | 83/83, failed0/skipped0 | 3 | 1 | 0 |
| 성능 측정 40회 | 각 1/1 | 18 | 1 | 0 |

고유 경고는 `Editor/TraceForgeLogViewerWindow.cs:196`의 **CS0618**, `EditorUtility.InstanceIDToObject(int)` obsolete다. 같은 경고가 컴파일 로그의 여러 단계에 반복 출력됐다. 이전 warm-cache 검증에서 경고 0이었던 것은 full rebuild의 경고 0을 보장하지 않는다. 이 경고는 release-check의 compiler warning=0 조건을 충족하지 못한다. 수정에는 이번 Owned 밖 Editor 파일 변경이 필요하므로 수행하지 않았다.

### 재진입 범위 관측

별도 호스트 진단은 1/1이며 `REENTRY_OBSERVATION calls=3 maxDepth=3`을 기록했다. 안전을 위해 테스트 sink가 깊이를 3으로 제한했을 뿐 Logger가 재귀를 차단한 것은 아니다. `Logger.Dispatch`가 직접 `sink.Write(in entry)`를 호출하고 이 sink가 `TF.Info`를 호출하면 같은 dispatch로 다시 진입한다. 일반 사용자 sink의 무제한 재귀를 막는 guard가 없다. 기존 `RecursiveSinkLogging_IsGuarded`는 UnityLogCapture.OnMessage의 재진입만 검증한다. 체크리스트 22번의 범용 Logger 재진입 안전성은 **FAIL**로 기록한다. 별도 진단의 테스트 통과를 안전성 승인으로 해석하지 않는다. Runtime 변경은 하지 않았다.

### 산출물과 보존

세션 디렉터리의 `frozen-plan.json`, `run-revalidation.ps1`, `RevalidationBenchmarks.cs`, `results.csv`, `invocations.jsonl`, `execution.log`, `raw/*.xml`, `raw/*.log`, `raw/*.entries.log`가 실제 측정 근거다. 각 raw 행은 accepted/written=100000, result Passed, passed1/failed0/skipped0, frequency10000000을 포함한다. 원래 Phase4Benchmarks/실행기/CSV 해시와 세션 소스 해시도 보존한다. 기능 XML/log는 호스트 루트의 `revalidation-20261013-*`이며 최초 실패와 retry1을 별도 이름으로 남긴다. 캐시는 `Library.before-functional-repair`, `Temp.before-functional-repair`로 보존했다.

### 별도 코드 검토 진단 계획

공식/교대/TF 세트 완료 후, 체크리스트 22번을 확인하기 위한 비시간 측정 호스트 테스트를 별도로 실행한다. 기존 테스트는 수정하지 않는다. 사용자 sink에서 `TF.Info`를 재호출하되 최대 깊이를 3으로 제한해 stack overflow를 일으키지 않고 관측 호출 수/깊이를 기록한다. 이 테스트의 성공은 재진입 안전성 PASS를 뜻하지 않으며 관측 사실만 검증한다. Unity capture의 기존 재귀 테스트가 일반 Logger 재귀까지 검증하는지 코드와 함께 판정한다. 이 진단은 40회 성능 원시 결과와 승인 임계값에 영향을 주지 않는다.

## 전체 원시 결과

| ID | Set / config / mode | Producer ticks | Completion ticks | GC proxy | File lines | Entry bytes |
|---|---|---:|---:|---:|---:|---:|
| O-c1-r1 | Official / C1 / sink-file | 208183 | 3661623 | 0 | 100000 | 40 |
| O-c1-r2 | Official / C1 / sink-file | 218214 | 3573397 | 0 | 100000 | 40 |
| O-c1-r3 | Official / C1 / sink-file | 208118 | 3417145 | 0 | 100000 | 40 |
| O-c1-r4 | Official / C1 / sink-file | 265645 | 4112631 | 0 | 100000 | 40 |
| O-c1-r5 | Official / C1 / sink-file | 155612 | 3092466 | 0 | 100000 | 40 |
| O-c2-r1 | Official / C2 / sink-file | 131203 | 3172848 | 0 | 100000 | 56 |
| O-c2-r2 | Official / C2 / sink-file | 116339 | 3088327 | 0 | 100000 | 56 |
| O-c2-r3 | Official / C2 / sink-file | 140649 | 3083345 | 0 | 100000 | 56 |
| O-c2-r4 | Official / C2 / sink-file | 139983 | 3387414 | 0 | 100000 | 56 |
| O-c2-r5 | Official / C2 / sink-file | 156426 | 4333475 | 0 | 100000 | 56 |
| O-c3-r1 | Official / C3 / sink-file | 134890 | 3148133 | 0 | 100000 | 56 |
| O-c3-r2 | Official / C3 / sink-file | 127675 | 3024859 | 0 | 100000 | 56 |
| O-c3-r3 | Official / C3 / sink-file | 146502 | 3260938 | 0 | 100000 | 56 |
| O-c3-r4 | Official / C3 / sink-file | 142041 | 3270927 | 0 | 100000 | 56 |
| O-c3-r5 | Official / C3 / sink-file | 141473 | 3838543 | 0 | 100000 | 56 |
| O-c4-r1 | Official / C4 / sink-file | 124028 | 2824998 | 0 | 100000 | 56 |
| O-c4-r2 | Official / C4 / sink-file | 136664 | 2938192 | 0 | 100000 | 56 |
| O-c4-r3 | Official / C4 / sink-file | 135612 | 3104860 | 0 | 100000 | 56 |
| O-c4-r4 | Official / C4 / sink-file | 124945 | 3002918 | 0 | 100000 | 56 |
| O-c4-r5 | Official / C4 / sink-file | 149536 | 2782222 | 0 | 100000 | 56 |
| A-b1 | Alternating / C1 / sink-file | 179800 | 2950659 | 0 | 100000 | 40 |
| A-c1 | Alternating / C2 / sink-file | 137924 | 3185274 | 0 | 100000 | 56 |
| A-b2 | Alternating / C1 / sink-file | 185115 | 3253245 | 0 | 100000 | 40 |
| A-c2 | Alternating / C2 / sink-file | 129887 | 2923805 | 0 | 100000 | 56 |
| A-b3 | Alternating / C1 / sink-file | 182049 | 3608950 | 0 | 100000 | 40 |
| A-c3 | Alternating / C2 / sink-file | 148495 | 4329890 | 0 | 100000 | 56 |
| A-b4 | Alternating / C1 / sink-file | 172137 | 2866507 | 0 | 100000 | 40 |
| A-c4 | Alternating / C2 / sink-file | 146097 | 3704413 | 0 | 100000 | 56 |
| A-b5 | Alternating / C1 / sink-file | 197386 | 3593359 | 0 | 100000 | 40 |
| A-c5 | Alternating / C2 / sink-file | 142723 | 2920510 | 0 | 100000 | 56 |
| T-none1 | TF / C2 / tf-none | 247821 | 3448256 | 0 | 100000 | 56 |
| T-error1 | TF / C4 / tf-error | 239072 | 3000490 | 0 | 100000 | 56 |
| T-none2 | TF / C2 / tf-none | 235877 | 3336596 | 0 | 100000 | 56 |
| T-error2 | TF / C4 / tf-error | 262994 | 3465120 | 0 | 100000 | 56 |
| T-none3 | TF / C2 / tf-none | 224940 | 3109136 | 0 | 100000 | 56 |
| T-error3 | TF / C4 / tf-error | 225070 | 3331543 | 0 | 100000 | 56 |
| T-none4 | TF / C2 / tf-none | 232865 | 2896517 | 0 | 100000 | 56 |
| T-error4 | TF / C4 / tf-error | 247067 | 3102652 | 0 | 100000 | 56 |
| T-none5 | TF / C2 / tf-none | 247846 | 3323442 | 0 | 100000 | 56 |
| T-error5 | TF / C4 / tf-error | 261330 | 3137578 | 0 | 100000 | 56 |

## 중앙값과 변동 범위

| Set / config / mode | Producer median [min–max] ticks | Completion median [min–max] ticks |
|---|---:|---:|
| Alternating, 1, sink-file | 182049 [172137–197386] | 3253245 [2866507–3608950] |
| Alternating, 2, sink-file | 142723 [129887–148495] | 3185274 [2920510–4329890] |
| Official, 1, sink-file | 208183 [155612–265645] | 3573397 [3092466–4112631] |
| Official, 2, sink-file | 139983 [116339–156426] | 3172848 [3083345–4333475] |
| Official, 3, sink-file | 141473 [127675–146502] | 3260938 [3024859–3838543] |
| Official, 4, sink-file | 135612 [124028–149536] | 2938192 [2782222–3104860] |
| TF, 2, tf-none | 235877 [224940–247846] | 3323442 [2896517–3448256] |
| TF, 4, tf-error | 247067 [225070–262994] | 3137578 [3000490–3465120] |

## performance-checklist 27항목 대조

PASS는 해당 범위의 근거 충족, 승인 예외는 기존 사용자 결정과 API 계약, N/A는 해당 구현 없음이다. 승인 예외/N/A를 PASS 수에 합치지 않는다.

| # | 판정 | 실제 근거와 범위 |
|---|---|---|
| 1 | PASS | Logger.Write의 IsEnabled가 stack/entry/timestamp 생성 앞에 있다. caller formatting은 API 밖이며 guard 테스트로 분리한다. |
| 2 | PASS | 두 Write 및 WriteCaptured 모두 동일한 early filter를 사용한다. Context overload의 GetInstanceID는 Write 앞에서 평가되며 이 비용은 이번 TF 진단에서 제외한다. |
| 3 | 승인 예외 | D8/Phase 4의 caller-side guard 계약: 호출자 보간을 TF가 막을 수 없다. sample guard 및 message evaluation counter 테스트 통과. |
| 4 | N/A | params object[]/boxing formatting overload 없음. |
| 5 | N/A | lazy Func-message overload 없음; deferred issue 7은 미구현. |
| 6 | PASS | TF.IsEnabled와 Logger.IsEnabled의 AggressiveInlining 확인. |
| 7 | PASS | LogEntry value type; prebuilt direct entry / TF stack-local entry. Queue allocation은 timed region 밖이며 heap 자체가 없다는 뜻은 아니다. |
| 8 | 승인 예외 | 정상 message formatting은 FileSink에 있다. Logger의 sink-failure Console.Error 문자열은 Phase 3 오류 fallback이며 정상 producer 보장 밖이다. |
| 9 | N/A | Runtime hot path의 StringBuilder 없음. |
| 10 | PASS | Logger core는 enabled 검사 전에 message ToString을 호출하지 않는다. caller 인자는 별도 계약이다. |
| 11 | 승인 예외 | 기존 dynamic LogCategory API/설정 카테고리는 런타임 이름을 허용한다. 이 측정은 static predefined Default를 재사용한다. |
| 12 | 승인 예외 | D7은 Editor/Dev ErrorAndAbove 기본값을 허용한다. None/Info에서 stack capture에 진입하지 않고, Error/All 할당은 허용된 정책 비용이다. |
| 13 | PASS | ILogSink[] snapshot을 직접 순회한다. |
| 14 | PASS | Add/Remove/Clear는 copy-on-write 및 Interlocked.Exchange; producer는 writer lock을 잡지 않는다. |
| 15 | PASS | ILogSink.Write(in LogEntry), Dispatch(in entry); 개별 entry boxing 없음. 관측 sizeof는 40→56 bytes. |
| 16 | PASS | IsEnabled는 sink length0 early return, Dispatch도 null/empty check; no-sink 테스트 통과. |
| 17 | PASS | readonly struct LogEntry. |
| 18 | PASS | readonly struct LogCategory 및 사전 생성 category; per-entry category 생성 없음. |
| 19 | PASS | Logger.Write/Dispatch hot path에 LINQ 없음. |
| 20 | PASS | logging method에서 IEnumerable 반환 없음. |
| 21 | PASS | capture/registry delegate 구독은 구성/초기화 시점; Write마다 delegate 생성 없음. |
| 22 | **FAIL** | 일반 sink→TF.Info 재귀에 Logger guard 없음. bounded host probe calls3/depth3; Unity callback guard 테스트와 구분. |
| 23 | PASS | sink snapshot은 Interlocked.CompareExchange로 읽고 쓰기만 _sinksLock 사용. FileSink 자체 enqueue lock은 포함된 별도 sink 비용이다. |
| 24 | PASS | ILogSink, FileSink, RingBufferSink의 thread-safety XML 및 동시 producer 테스트 확인. |
| 25 | PASS | context overload XML에 main thread 명시. TF 진단은 context를 사용하지 않는다. |
| 26 | N/A | UnityConsoleSink 제거됨. 대체 UnityLogCapture의 ThreadStatic guard는 별도 테스트로 통과했으나 #22의 범용 guard는 아니다. |
| 27 | PASS | Reset/Bootstrap 순서 및 owned shutdown 코드를 확인했고 원래 default domain reload 전체 테스트는 새 캐시에서 통과했다. 첫 startup 실패도 보존한다. |

집계: **18 PASS / 4 승인 예외 / 4 N/A / 1 FAIL**. 따라서 체크리스트 전체 PASS가 아니다. 아직 측정하지 않은 비용은 Unity callback 시간, Error/All stack capture 시간, queue 포화, Player/mobile, writer 구간별 프로파일이다.

## 판정과 다음 결정

| 구분 | 판정 |
|---|---|
| 사전 계획대로 성능 측정 완료 | 완료: O20/A10/T10; 성공 결과 전부 보존, N 조건 미충족 |
| 기능 테스트 | PASS: Edit31/Play83, failed0/skipped0; Play 최초 startup 실패 1회 및 cache 재실행 별도 |
| 기존 producer 할당 회귀 | 지정된 current-thread proxy/positive control 범위 PASS; bytes 보장 아님 |
| 공식/교대/TF 파일 행수 | 40/40 실제 100000행, accepted/written 동일 |
| 역사적 rig 재현성 | **FAIL**, producer -25.7227%, completion -34.9983% |
| 현재 C2–4 producer 5% 기준 | 관측값 기준 충족; 역사적 rig PASS는 아님 |
| completion 증가 반복 | 이번 공식/교대 중앙값에서는 미관측; 과거 증가 원인은 미확정 |
| compiler warning=0 | **FAIL**, Viewer CS0618 고유 1건 |
| performance-checklist 전체 | **FAIL**, #22 범용 Logger 재진입 guard 미충족 |
| 전체 성능 승인 / 릴리스 차단 해소 | **미승인 / BLOCKED 유지** |

새 기준선을 채택하려면 재현 불가능한 과거 환경을 어떻게 취급할지, 무엇을 새 reference rig/측정 경계로 사용할지, 어떤 환경 통제를 고정할지에 대한 사용자 결정이 필요하다. 기존 ±10%/5% 기준을 바꾸거나 이번 수치를 자동 baseline으로 채택하지 않았다. 완료된 기능/할당 검증만으로 rig 실패를 상쇄하지 않는다.

추가 코드 작업이 필요하다면 Viewer deprecated API 경고의 수정 범위는 `Editor/TraceForgeLogViewerWindow.cs`, 범용 재귀 안전성은 `Runtime/Logger.cs` 및 별도 sink-dispatch 회귀 테스트다. 모두 이번 Owned 밖이라 수행하지 않았다. 다음 세션은 이 차단 사유의 처리/정책 결정부터 시작할 수 있으며, 바로 릴리스를 재개할 수 있다는 승인은 아니다.

원래 Assets/configuration은 내용 해시로 복원했고 진단 Assets 및 생성된 test scene은 별도 보존 폴더로 이동했다. baseline의 Unity-generated metas도 원시 결과와 구분해 보존 후 임시 worktree를 정리했다. 기존 BLOCKED 보고서·계획·벤치마크·Runtime/Editor/Tests/README/metadata/CI는 변경하지 않았다.

## 독립 리뷰

주 에이전트가 계획·분석·40회 실측·호스트 진단·보고서 작성. Luna 워커 없음. 독립 `/root/performance_revalidation_review`, `astra_review` (요청 GPT-6 Astra/low), 2026-10-14 판정: **accept — 보고서 정확성 승인, 전체 성능 BLOCKED 유지**. 리뷰어는 staged diff, 코드/실행기, 40행 CSV와 XML, 실제 파일 100,000행, 모든 중앙값과 N 조건, 복원 해시, 기능 결과와 경고, checklist #22의 source/probe를 대조했고 보고서 수리를 요구하지 않았다. 보고서 정확성 accept는 기준선 채택이나 릴리스 승인이 아니다. Native token usage: unavailable.
