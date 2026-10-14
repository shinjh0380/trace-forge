# Logger 재진입 guard 수정과 비용 검증

## 범위와 시작 상태

2026-10-14, `feature/native-logger`, 시작 HEAD `f7e1499a26ba1a29dc929f4343fd1166cbfbdc70`, clean. 이전 재검증의 **보고서 정확성 accept**와 **전체 성능 승인 BLOCKED**를 구분한다. 이 검증은 Viewer CS0618 및 일반 sink 재귀 문제의 수정에 한정하며 릴리스 승인이나 새 기준선 채택이 아니다.

## 구현과 재현

`Logger.Dispatch`에 `[ThreadStatic] bool` guard를 둔다. sink `Write` 및 실패 보고 중 같은 스레드의 중첩 로그는 모든 sink에서 버린다. 원래 entry는 나머지 sink에 전달하고 `finally`에서 guard를 해제한다. 두 일반 Write 오버로드와 WriteCaptured가 공유하는 경계이며, 다른 스레드·Flush·설정 변경을 전역 차단하지 않는다. public API와 IsEnabled는 변경하지 않는다. caller 인자 평가·entry/stack 생성은 guard 앞에서 수행된다.

수정 전 bounded 회귀 테스트는 깊이 3에서 멈추도록 설계했다. `red-dispatch`: total20/passed15/failed5/skipped0, 네 일반/captured 조합에서 calls3/maxDepth3, context Write도 실패. 수정 후 첫 `green-dispatch`는 missing referenced test-runner script로 XML 없이 300초 timeout/exit -1. 로그와 run receipt를 보존했다. Library/Temp를 이동·보존하고 `green-dispatch-retry1-fresh`: **20/20, failed0/skipped0**, calls1/maxDepth1. 재귀 차단·나머지 sink·다음 entry·sink/stderr 예외·다른 스레드 독립성을 검증한다.

Viewer는 `UNITY_6000_3_OR_NEWER`에서 `EditorUtility.EntityIdToObject((EntityId)instanceId)`를 사용한다. 이전 Unity 6000.0–6000.2 branch는 기존 API를 유지한다. [Unity 6000.3 공식 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/EditorUtility.bindings.cs)는 obsolete wrapper가 같은 cast/EntityId API로 연결됨을 보여주며, [6000.2 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.2/Editor/Mono/EditorUtility.bindings.cs)는 instance-ID API를 제공한다. live/zero/destroyed ID의 ping 회귀 테스트를 추가했다. 로컬 설치는 6000.3.8f1만 있어 이전 Editor branch를 이번 세션에서 실행하지 않았다.

## 환경과 사전 측정 절차

- Unity 6000.3.8f1, UTF 1.6.0, Windows Editor PlayMode batchmode. 정확한 OS/CPU/전원 상태는 세션 `environment.json`, 실행 전후 CPU 표본은 `cpu-snapshots.jsonl`에 보존한다. 배경 작업·클럭·thermal/cache는 통제하지 않는다.
- before는 f7e1499 detached `.worktrees/guard-before`, after는 root의 수정본. 원래 호스트 manifest/lock/ProjectSettings/Assets를 해시와 복사본으로 보존한다.
- 첫 timing 전에 `frozen-plan.md`로 5쌍/10개 독립 프로세스를 고정했다. 순서는 AB/BA/AB/BA/AB. 초기 warm-cache startup 문제를 확인한 뒤 **측정 전** 프로토콜을 명시: 진단은 domain reload disabled/scene reload enabled, 전체 기능 검증은 복원 후 기본 reload. 측정 수치가 나온 뒤 순서를 바꾸거나 극단값을 제거하지 않는다.
- 각 프로세스에서 네 경로: TF.Info None/off, TF.Info ErrorAndAbove/off(Info), TF.Info None/capture 구독 on(실제 callback 없음), Logger.WriteCaptured None/supplied stack. captured는 timed region 밖에서 만든 typed delegate를 호출하고 invocation 비용은 양쪽에 동일하게 포함한다.
- 각 경로 10,000 warm-up 후 1,000,000 timed calls, prebuilt message, counting sink 하나. 실제 facade/Logger의 filtering/timestamp/policy/entry/dispatch/interface Write를 통과한다. FileSink 직호출·파일 I/O·queue 비용으로 대체하지 않는다.
- Stopwatch.GetTimestamp와 frequency를 기록한다. counted sink calls=1,000,000, current-thread ProfilerRecorder GC.Alloc capacity64 Count delta=0을 단정한다. 이는 sample/event proxy이며 bytes나 모든 경로의 allocation 보장이 아니다.
- guard 비용 진단에는 새 승인 threshold가 없다. 기존 직접 FileSink producer 5% 및 과거 rig ±10% gate를 유지한다. 두 다른 측정 경계를 혼합하거나 이번 결과를 새 baseline으로 채택하지 않는다.

## 측정 결과

10/10 독립 프로세스가 각 4/4, failed0/skipped0으로 통과했다. 제외·실패·재측정 0회. 40개 측정 모두 accepted=1,000,000, GC sample proxy=0, timer frequency=10,000,000 ticks/s다. 각 process가 같은 네 mode를 순서대로 측정한다. CPU 전후 표본은 9–50%였으며 timed region의 연속 CPU 프로파일이 아니다. Windows 11 Home 10.0.26200, i7-13700HX(16C/24T), RAM 33,240,568 KiB, Balanced 전원, 배터리 상태2/100%였다.

### 중앙값과 범위

ticks를 10,000으로 나누면 ms다. 변화는 **중앙값의 비율**이며 쌍별 비율의 중앙값과 다르다.

| Mode | 경로 | Before median [min–max] ticks | After median [min–max] ticks | 변화 |
|---|---|---:|---:|---:|
| 0 | TF.Info None/off | 634209 [618020–702424] | 685399 [649760–689278] | +8.0715% |
| 1 | TF.Info ErrorAndAbove/off | 644282 [623731–681566] | 682118 [664651–692458] | +5.8726% |
| 2 | TF.Info None/capture on, callback 없음 | 643481 [630536–677036] | 680233 [649998–704733] | +5.7114% |
| 3 | Logger.WriteCaptured supplied stack | 639742 [619178–679463] | 662782 [641640–674170] | +3.6015% |

TF None/off의 1,000,000회 producer 중앙값은 63.4209→68.5399ms, 차이는 5.1190ms다. 안전 guard 추가 후 세 TF 구성에서 중앙값 증가가 5%를 넘었다는 관측을 숨기지 않는다. 다만 이는 counting sink의 실제 Logger 경로 진단이며, 기존 direct FileSink 5% gate의 대체 실험은 아니다. guard의 독립 명령어 비용 또는 모든 환경의 일정한 회귀율을 확정하지 않는다. 성능 개선 작업으로 범위를 확대하지 않았다.

### 모든 원시 값과 쌍별 비교

Pair1/3/5는 before→after, Pair2/4는 after→before다. 각 행의 두 실행은 별도 Unity 프로세스이며, 동일 pair의 네 행은 각 process 안의 네 테스트다.

| Mode | Pair | Before ticks | After ticks | 변화 |
|---|---:|---:|---:|---:|
| 0 | 1 | 633975 | 658096 | +3.8047% |
| 0 | 2 | 702424 | 689278 | -1.8715% |
| 0 | 3 | 634209 | 649760 | +2.4520% |
| 0 | 4 | 655882 | 687272 | +4.7859% |
| 0 | 5 | 618020 | 685399 | +10.9024% |
| 1 | 1 | 644282 | 692458 | +7.4775% |
| 1 | 2 | 681566 | 685174 | +0.5294% |
| 1 | 3 | 623731 | 664651 | +6.5605% |
| 1 | 4 | 660932 | 682118 | +3.2055% |
| 1 | 5 | 628404 | 678308 | +7.9414% |
| 2 | 1 | 642991 | 649998 | +1.0898% |
| 2 | 2 | 676078 | 704733 | +4.2384% |
| 2 | 3 | 643481 | 684580 | +6.3870% |
| 2 | 4 | 677036 | 677509 | +0.0699% |
| 2 | 5 | 630536 | 680233 | +7.8817% |
| 3 | 1 | 633796 | 641640 | +1.2376% |
| 3 | 2 | 663805 | 663719 | -0.0130% |
| 3 | 3 | 639742 | 674170 | +5.3815% |
| 3 | 4 | 679463 | 658743 | -3.0495% |
| 3 | 5 | 619178 | 662782 | +7.0422% |

### 시간 block 변동

각 revision의 첫 2회 중앙값과 뒤 3회 중앙값을 별도로 비교한다. 표본 수가 작고 달라 안정성 통과 검증은 아니다.

| Mode | Before 첫2→뒤3 ticks (변화) | After 첫2→뒤3 ticks (변화) |
|---|---:|---:|
| 0 | 668199.5→634209 (-5.0869%) | 673687→685399 (+1.7385%) |
| 1 | 662924→628404 (-5.2072%) | 688816→678308 (-1.5255%) |
| 2 | 659534.5→643481 (-2.4341%) | 677365.5→680233 (+0.4233%) |
| 3 | 648800.5→639742 (-1.3962%) | 652679.5→662782 (+1.5479%) |

이번 before 코드에서도 약 5%의 block drift가 있다. 이전 재검증의 12.55%와 함께 안정성/새 reference 승인 문제를 남긴다. 새 정책 제안의 안정성 조건을 이미 통과했다고 간주하지 않는다.

## 전체 재컴파일 및 기능 검증

원래 manifest/lock/ProjectSettings/Assets를 복원하고 Library/Temp를 보존·이동한 뒤 전체 EditMode **32/32**, failed0/skipped0, C# warning/error 0을 관측했다. **이 실행의 Csc는 Bee 전역 CacheHit이므로 실제 전체 재컴파일 증거가 아니다.** 최초 독립 리뷰가 이 서술 오류를 지적해 아래 별도 실행으로 보완했다.

`full-compile-isolated-bee.log`: Library/Temp를 다시 보존·이동하고, 이번 프로세스에만 `BEE_CACHE_DIRECTORY`를 새 빈 `Bee.isolated/`로 지정해 `Unity.exe -batchmode -quit -projectPath ... -logFile ...`를 실행했다. [Unity cache 위치 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/build-cache-location-reference.html)에 따른 전역 캐시 격리다. **exit0, Csc CacheHit0, C# warning0/error0**. Runtime/Editor/Tests.Runtime/Tests.Editor 네 assembly 모두 실제 Csc 실행 및 CacheWrite를 확인했다. 기존 전역 cache는 삭제하지 않았고 환경 변수는 원복했다. 이것이 실제 전체 C# 재컴파일의 근거이며, 기존 캐시 사용 테스트 로그와 구분한다. 컴파일 전후 production source hash도 동일하다.

전체 PlayMode는 기본 domain reload에서 최종 **92/92**, failed0/skipped0, C# warning/error 0이다. 단, 아래 **호스트 전용 임시 참조 보정**을 적용한 실행이다. 원래 호스트 상태에서의 성공으로 표현하지 않는다.

| 실행/진단 | 실제 결과 |
|---|---|
| `guard-fix-20261014-editmode` | 새 캐시, 32/32, failed0/skipped0 |
| `guard-fix-20261014-playmode` | missing runner script/no XML, 약 50초 후 해당 프로세스만 종료 |
| `...playmode-retry1-fresh` | 새 Library/Temp에서도 같은 startup 정지, 약 146초 후 종료 |
| `host-runner-prepare` / `host-runner-reimport` | exit0, 로드된 runner 타입은 있으나 script asset GetClass는 null; 강제 재임포트로 복구되지 않음 |
| `full-playmode-filter-all` | 전체 일치 regex filter도 같은 startup 정지, XML 없음 |
| `...playmode-retry2-quarantine` | 생성 InitTestScene, Library/Temp/UserSettings를 보존·격리해도 같은 startup 정지, XML 없음 |
| `playmode-runtime-assembly` | 기본 reload, Runtime assembly 전체 90/90, failed0/skipped0 |
| `playmode-host-assembly` | 기존 host sample assembly만 선택해도 같은 startup 정지, XML 없음 |
| `...playmode-retry3-host-reference` | host asmdef의 미사용 UnityEditor.TestRunner 참조만 임시 제거, 기본 reload 전체 92/92, failed0/skipped0 |
| `...playmode-restored-host` | 원래 참조/설정/Assets 복원 뒤 같은 startup 정지 재현, XML 없음; 원래 호스트의 제약으로 남김 |

호스트 `Assets/Phase1SampleVerification/TraceForge.HostSample.Tests.asmdef`는 PlayMode assembly인데 미사용 `UnityEditor.TestRunner`를 참조했다. 해당 참조 한 개만 임시 제거한 최종 전체 실행은 기존 테스트 본문/단정/선택을 바꾸지 않았다. `case-set-comparison.json`으로 이전 Edit31/Play83의 모든 case가 포함되고, 추가된 case가 정확히 Editor1/Runtime9개임을 확인했다. 테스트를 제외하거나 skip해서 통과시키지 않았다. 원래 참조를 복원하면 문제가 재현되지만 이를 Unity 전반의 원인으로 일반화하지 않는다. 이 임시 호스트 보정은 커밋하지 않았고 원래 asmdef로 복원했다. 표준 원복 호스트의 무필터 PlayMode 실행 문제는 아직 해결됐다고 하지 않는다.

두 기존 할당 테스트는 Runtime90 및 전체92 실행에서 통과했다. `LoggerPath_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`: `loggerPath=0, policy=None, capture=enabled`. `Write_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`: control29→30/sample1, measured0(capacity1024). positive control과 정상 producer 경로의 proxy 결과이며 bytes 보장이 아니다.

최초 red/timeout을 포함한 모든 로그·XML·실행/종료 receipt와 캐시·생성 scene·진단 helper를 보존했다. 최종 복원은 시작 snapshot과 SHA256 대조해 **manifest/lock/ProjectSettings/원래 Assets54개 mismatch0, Assets extra0**, 기본 reload0/options0 및 빈 defines다. 원복 호스트 추가 실행의 startup 실패도 보존하고 설정을 다시 복원했다. 과거 벤치마크와 그 산출물은 수정하지 않았다.

## 성능 체크리스트와 남은 승인

체크리스트 #22는 **PASS**: bounded red의 calls3/depth3에서 green의 calls1/depth1로 바뀌었고, 두 Write/captured 경계, finally 복구, sink/stderr 예외 이후의 다음 로그, 나머지 sink, 다른 스레드를 실제 검증했다. 그 외 기존 승인 예외(D7/D8·동적 category·오류 fallback)와 N/A 범위는 이전 보고서의 계약을 유지한다.

| 항목 | 코드 검토 판정/범위 |
|---|---|
| 1–2, 6–7, 10, 13–21, 23–25 | 기존 PASS 범위 유지: early filter, value types/in 전달, COW sink snapshot, hot-path LINQ/boxing/delegate 생성 없음, 스레딩/context 계약. 이번 diff가 해당 API/경로를 넓히지 않음 |
| 22 | PASS: ThreadStatic guard + red/green/동시성/예외 회귀 검증 |
| 27 | 기본 reload의 Runtime90 및 전체92 검증 통과. 전체92는 위 임시 호스트 참조 보정 조건이며 원복 host startup 제약을 함께 기록 |
| 3, 8, 11, 12 | 기존 승인 예외: caller formatting/D8, 오류 fallback formatting, 동적 category, D7 Editor/Dev error stack 기본값 |
| 4, 5, 9, 26 | N/A: params/lazy formatting/StringBuilder/UnityConsoleSink 없음. Unity capture의 별도 guard와 기존 테스트는 유지 |

코드 계약 범위 **19 PASS / 4 승인 예외 / 4 N/A**이며 새로운 시간 회귀나 역사적 rig의 성능 승인을 뜻하지 않는다. `rg -n 'Debug\.Log' Runtime Editor Samples~`는 0건, `git diff --check` 통과. ripwire는 WSL wrapper에서 Git baseline을 읽지 못해 quality-delta가 unavailable이며, 한정한 Dispatch edit-check도 no-baseline이다. 이를 품질 통과로 세지 않고 실제 git diff/컴파일/테스트/독립 리뷰로 대조한다.

과거 rig 재현성 FAIL 및 전체 성능 승인 BLOCKED는 유지한다. [기준선 채택 정책 제안](2026-10-14-baseline-adoption-proposal.md)은 아직 승인되지 않았다.

## 산출물과 독립 리뷰

`.worktrees/native-logger-test-host/.guard-fix-20261014/`에 최초 red/timeout/retry XML/log, runs.jsonl, frozen-plan.md, runner/benchmark source, source hashes, 원시 CSV/CPU 표본, 환경, 원본 host snapshot 및 cache archive를 보존한다. 전체 suite XML/log는 host root의 `guard-fix-20261014-*`다. 측정 후 Logger/TF/Viewer hash가 after 측정 시점과 같음을 확인했다. 주 에이전트 직접 구현/검증; Luna worker 없음. 독립 `/root/guard_fix_astra_review`, `astra_review`(요청 GPT-6 Astra/low)는 코드 결함 없이 fresh compile 근거 서술에 **fix**를 요청했다. 주 에이전트가 한 번 보완하여 Bee 격리 실제 컴파일을 추가하고 문서를 바로잡았으며 최종 재리뷰 결과는 아래에 기록한다. Token usage unavailable.

2026-10-14 최종 재리뷰: **accept — 코드·회귀 검증·보고서 정확성 승인, 전체 성능·릴리스 BLOCKED 유지**. 리뷰어는 네 assembly의 실제 Csc/CacheWrite와 CacheHit0, C# 진단0, source/host 복원 hash, red/green/조건부 전체 suite, 40개 XML→CSV 및 중앙값을 대조했다. 추가 수정 요구 없음. 이전 Editor branch 미실행 및 원복 host startup 제약은 승인 범위에서 제거되지 않았다. 코드/동작 문서/회귀 테스트는 `934221a`로 별도 커밋했다. 임시 비교 worktree를 정리하며 원시 산출물은 호스트에 보존한다.
