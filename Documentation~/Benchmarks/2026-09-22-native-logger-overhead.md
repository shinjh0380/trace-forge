# Native Logger 오버헤드 및 성능 계약 검증

## 목적

Phase 3 이후 확장된 `LogEntry`를 사용하는 비동기 FileSink의 비포화 producer 경로를 `f95dcc2`와 비교한다. [2026-07-22 측정](2026-07-22-file-sink-performance.md)의 경계와 표 형식을 유지하며, 과거 기준선 재현 여부와 현재 구성 간 비교를 구분한다. 과장된 할당·stripping 문구를 수정한 Phase 4의 실제 검증 결과도 기록한다.

## 환경과 대상 커밋

- Unity: 6000.3.8f1 (`1c7db571dde0`), Windows Editor PlayMode, batch mode
- OS: Microsoft Windows 11 Home 10.0.26200, 64-bit
- CPU: 13th Gen Intel Core i7-13700HX, 16코어/24논리 프로세서
- 메모리: 32,461MB
- 타이머: `Stopwatch.GetTimestamp()`, 주파수 10,000,000 ticks/s
- 기준선: `f95dcc2`, 임시 `.worktrees/bench-baseline` checkout. FileSink와 LogEntry 소스는 과거 측정의 `d98821c`와 동일하다.
- 비교 상태: `61ffa6e` (`feature/native-logger`의 `227677f` + Phase 4 IsEnabled 변경). 측정 후 같은 Runtime 소스를 이 커밋으로 기록했다. Runtime 변경은 `TF.IsEnabled`/내부 strip probes와 `Logger.IsEnabled`의 sink 길이 검사뿐이다. `Logger.Write`, 정책 읽기, LogEntry, FileSink는 Phase 3 상태를 유지했다.
- 호스트: `.worktrees/native-logger-test-host`, Unity Test Framework 1.6.0. 기존 Test Framework 시작 문제를 피하기 위해 domain reload 비활성, scene reload 활성으로 실행했다. 기본 domain reload 검증은 주장하지 않는다.
- 원시 산출물: 호스트의 `phase4-c{1..4}-run{1..5}.xml`, `.log`, `Artifacts/Phase4/results.csv`. 실제 파일 검증은 `Artifacts/Phase4/configuration-{1..4}.log`에서 수행했다.
- 호스트 전용 코드/실행기: `.phase4-staging/Phase4Benchmarks.cs`, `run-phase4-filtered.ps1`, `run-phase4-benchmark-set.ps1`. 이 파일들은 커밋하지 않는다. 아래 표가 영구 측정 기록이다.

## 측정 방법

구성 1 → 2 → 3 → 4 순서로, 각 구성마다 새 Unity 프로세스 5개를 실행했다. 실패한 숫자를 제외하거나 유리한 값이 나올 때까지 재측정하지 않았다. benchmark 하나만 필터링했으며 20회 모두 Test Runner 1/1 통과다.

하나의 미리 만든 Info `LogEntry`를 재사용했다. 별도 sink에서 1,000회 warm-up하고 Flush/Dispose한 뒤, 같은 파일을 `append: false`로 다시 열었다. 측정용 queue capacity는 101,024다. 100,000개를 모두 수용할 수 있어 queue 포화 대기는 배제하지만, worker와의 잠금 경합·OS 스케줄링은 포함한다.

1. producer: 동일 시작점부터 100,000회의 `FileSink.Write(in entry)` 반환까지
2. completion: 같은 시작점부터 직후 `Flush()` 반환까지
3. 제외: sink 생성, worker 시작, Dispose, 파일 line count 검증

`ProfilerCategory.Internal`의 `GC.Alloc`, `CollectOnlyOnCurrentThread`, recorder capacity 16을 사용해 producer 직전/직후 Count 차이를 기록했다. 이는 할당 바이트 수가 아닌 해당 Unity 환경의 event/sample count proxy다. 각 파일은 Dispose 후 정확히 100,000행인지 단정했다.

| 구성 | 코드 | 정책 | Unity capture |
|---|---|---|---|
| 1 | `f95dcc2` | 해당 기능 없음 | 해당 기능 없음 |
| 2 | Phase 3 + Phase 4 IsEnabled | None | off |
| 3 | 구성 2와 동일 | None | on, 측정 중 Unity 로그를 발생시키지 않음 |
| 4 | 구성 2와 동일 | ErrorAndAbove | off |

호스트 manifest를 구성 1에서만 baseline worktree로 전환하고 즉시 원복했다. 과거 API와 호환되지 않는 호스트 probe는 측정 중 보관하고 동일한 최소 benchmark assets만 네 구성에 사용했다. 종료 후 원래 assets·manifest·package lock을 복원하고 baseline worktree를 제거했다.

**측정 경계의 한계:** 이 루프는 `Logger.Write`를 호출하지 않는다. 따라서 구성 3의 숫자는 Unity 이벤트 처리 비용을, 구성 4의 숫자는 Logger 정책 읽기 비용을 측정하지 않는다. 모든 entry의 StackTrace는 null이다. 정책/capture 상태를 요구대로 설정했지만, 차이를 해당 기능의 비용이나 최적화 효과로 해석하지 않는다.

## 5회 원시 결과

괄호 안은 10,000,000 ticks/s 기준 환산 시간이다.

| 구성 | 실행 | Producer ticks | Flush 포함 completion ticks | GC.Alloc event/sample proxy | 파일 기록 수 |
|---|---:|---:|---:|---:|---:|
| 1 | 1 | 171,873 (17.1873ms) | 2,843,463 (284.3463ms) | 0 | 100,000 |
| 1 | 2 | 168,472 (16.8472ms) | 2,921,623 (292.1623ms) | 0 | 100,000 |
| 1 | 3 | 178,630 (17.8630ms) | 3,006,809 (300.6809ms) | 0 | 100,000 |
| 1 | 4 | 98,145 (9.8145ms) | 3,235,097 (323.5097ms) | 0 | 100,000 |
| 1 | 5 | 172,319 (17.2319ms) | 2,859,495 (285.9495ms) | 0 | 100,000 |
| 2 | 1 | 168,834 (16.8834ms) | 3,815,709 (381.5709ms) | 0 | 100,000 |
| 2 | 2 | 198,290 (19.8290ms) | 4,417,066 (441.7066ms) | 0 | 100,000 |
| 2 | 3 | 162,739 (16.2739ms) | 3,709,089 (370.9089ms) | 0 | 100,000 |
| 2 | 4 | 150,575 (15.0575ms) | 3,096,011 (309.6011ms) | 0 | 100,000 |
| 2 | 5 | 177,499 (17.7499ms) | 3,696,679 (369.6679ms) | 0 | 100,000 |
| 3 | 1 | 125,571 (12.5571ms) | 3,350,569 (335.0569ms) | 0 | 100,000 |
| 3 | 2 | 157,302 (15.7302ms) | 3,513,432 (351.3432ms) | 0 | 100,000 |
| 3 | 3 | 145,554 (14.5554ms) | 3,342,540 (334.2540ms) | 0 | 100,000 |
| 3 | 4 | 143,070 (14.3070ms) | 3,322,542 (332.2542ms) | 0 | 100,000 |
| 3 | 5 | 114,094 (11.4094ms) | 2,770,028 (277.0028ms) | 0 | 100,000 |
| 4 | 1 | 82,076 (8.2076ms) | 3,125,534 (312.5534ms) | 0 | 100,000 |
| 4 | 2 | 126,356 (12.6356ms) | 3,165,227 (316.5227ms) | 0 | 100,000 |
| 4 | 3 | 143,042 (14.3042ms) | 3,206,972 (320.6972ms) | 0 | 100,000 |
| 4 | 4 | 138,623 (13.8623ms) | 3,353,067 (335.3067ms) | 0 | 100,000 |
| 4 | 5 | 163,198 (16.3198ms) | 3,887,783 (388.7783ms) | 0 | 100,000 |

## 중앙값 비교

| 구성 | Producer 중앙값 | 구성 1 대비 | Completion 중앙값 | 구성 1 대비 |
|---|---:|---:|---:|---:|
| 1 | 171,873 ticks (17.1873ms) | 기준 | 2,921,623 ticks (292.1623ms) | 기준 |
| 2 | 168,834 ticks (16.8834ms) | -1.7682% | 3,709,089 ticks (370.9089ms) | +26.9530% |
| 3 | 143,070 ticks (14.3070ms) | -16.7583% | 3,342,540 ticks (334.2540ms) | +14.4070% |
| 4 | 138,623 ticks (13.8623ms) | -19.3457% | 3,206,972 ticks (320.6972ms) | +9.7668% |

과거 비동기 기준선 대비 구성 1의 producer는 280,278 → 171,873 ticks로 **-38.6777%**, completion은 5,497,390 → 2,921,623 ticks로 **-46.8544%**다. 둘 다 ±10% 범위를 벗어나므로 **과거 rig 재현성 기준은 실패**다. 더 빠른 값도 이 재현성 기준에서는 통과가 아니다.

## 해석

현재 환경 안에서는 구성 2–4의 producer 중앙값이 구성 1보다 높지 않아 5% 초과 회귀가 관측되지 않았다. 그러므로 조건부 Logger.Write profiling/수정/재측정 단계는 발동하지 않았고, 정책 읽기나 struct 전달을 임의로 최적화하지 않았다. 정책 저장소는 이미 static int이며 sink 계약은 `in LogEntry`를 유지한다.

구성 3·4의 낮은 숫자를 capture나 정책 덕분의 성능 개선이라고 볼 수 없다. 측정 경로가 같고 해당 Logger 분기를 지나지 않으며, 구성별 프로세스를 순서대로 실행했으므로 시간에 따른 시스템 부하 변동도 분리되지 않는다. 특히 producer 범위가 8.2076–19.8290ms로 넓다.

Completion 중앙값은 세 구성 모두 증가했다. 전체 파일 배출 처리량이 개선됐다는 결론은 내리지 않는다. 과거 rig 재현성 실패 때문에 이 상대 비교를 역사적 기준선과 연속된 성능 승인으로도 사용하지 않는다.

## 제한 사항

- Windows Editor 단일 기기의 결과다. Player, 모바일, 다른 Unity 버전은 측정하지 않았다.
- `GC.Alloc` 0은 지정된 producer 구간에서 관측한 event/sample 0이다. 모든 로그 호출이나 caller interpolation, stack capture, worker formatting의 할당이 0이라는 뜻이 아니다.
- default capacity 4,096의 queue 포화·backpressure는 측정하지 않았다.
- CPU 전력 상태·OS 스케줄러·파일 캐시·worker 경합을 통제하지 않았다. 과거와의 차이 원인은 이번 데이터만으로 특정할 수 없다.
- 직접 sink 호출이므로 실제 TF/Logger의 필터·DateTime·정책 읽기·Unity callback 비용은 포함하지 않는다. 해당 경로의 capture 활성/None 할당 회귀 테스트는 별도로 통과했지만 시간 오버헤드 보장은 아니다.
- 기본 domain reload에서의 Test Framework 실행은 Phase 3에서 시작 실패가 확인됐으며 이번 성능 수치의 검증 범위에 포함되지 않는다.

## 결론

**측정과 기능 검증은 완료했지만, 성능 승인 전체를 통과로 판정하지 않는다.**

| 기준 | 판정 |
|---|---|
| 과거 중앙값 ±10% 이내로 rig 재현 | **실패**: producer -38.6777%, completion -46.8544% |
| 현재 구성 1 대비 구성 2–4 producer 5% 초과 회귀 없음 | 관측값 기준 충족; rig 재현성 실패와 측정 경계 제한이 있음 |
| producer GC.Alloc event/sample 0 | 20/20 충족 |
| 파일 100,000행 | 20/20 충족 |
| 기본 EditMode / PlayMode 기능 테스트 | 24/24, 83/83, skipped 0 |
| 실제 strip defines 컴파일 | EditMode 24/24, `trace=True debug=True path=stripped`; Runtime filtering 19/19 |

기존 메서드 본문 stripping은 호출자 인자 평가를 제거하지 않는다. no-sink 및 strip 상태를 반영한 IsEnabled guard, 인자 평가 counter 테스트, 실제 심볼 컴파일이 README의 수정된 계약을 뒷받침한다. README에는 이번 결과를 일반적인 속도·무할당 보장으로 옮기지 않았다.
