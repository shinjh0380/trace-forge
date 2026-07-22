# 비동기 FileSink 성능 검증

## 목적

동기 파일 쓰기를 호출자 스레드에서 수행하던 기존 `FileSink`와 전용 워커 스레드로 쓰기를 이관한 비동기 `FileSink`를 같은 Unity Editor 조건에서 비교한다. 핵심 승인 기준은 비경합 producer 경로의 중앙값 시간이 기존 동기 구현보다 짧고, producer 경로에서 관측되는 GC 할당 이벤트가 없으며, 수락한 로그 100,000건이 모두 파일에 남는 것이다.

## 환경과 대상 커밋

- Unity: 6000.3.8f1 (`1c7db571dde0`), Windows Editor PlayMode, batch mode
- OS: Microsoft Windows 11 Home 10.0.26200, 64-bit
- CPU: 13th Gen Intel Core i7-13700HX, 16코어/24논리 프로세서
- 메모리: 32,461MB
- 타이머: `Stopwatch.GetTimestamp()`, 주파수 10,000,000 ticks/s
- 동기 기준선: `9034a1a` 상태
- 비동기 측정 상태: `d98821c`
  - 비동기 `FileSink` 구현: `c46bb58`
  - 최초 writer 오류 보존 수정: `62ca2b5`
- 원시 산출물:
  - 동기: `../async-file-sink-test-host/baseline-benchmark-{1..5}.xml`, `.log`
  - 비동기: `../async-file-sink-test-host/async-benchmark-{1..5}.xml`, `.log`

## 측정 방법

각 결과는 새 Unity 프로세스에서 독립적으로 측정했다. 실행마다 미리 만든 `LogEntry` 하나를 재사용해 1,000회 별도 warm-up을 완료한 후 100,000회를 측정했다. 비동기 측정의 실제 benchmark 파일은 warm-up용 sink와 분리하고 `append: false`로 다시 열었다.

비동기 queue capacity는 101,024로 설정했다. 따라서 100,000건 producer 구간에서는 queue 포화나 backpressure가 발생하지 않는 비경합 경로를 측정했다. sink 생성과 worker 시작은 타이머 밖에 두었다.

측정 경계는 다음과 같다.

1. producer: `Stopwatch` 시작부터 정확히 100,000회의 `Write(in entry)` 반환까지
2. completion: 같은 시작점부터 직후 `Flush()` 반환까지
3. 제외: sink 생성, worker 시작, `Dispose()`, 파일 line count 검증

할당 계측에는 `ProfilerCategory.Internal`의 `GC.Alloc` marker와 `ProfilerRecorderOptions.CollectOnlyOnCurrentThread`를 사용했다. recorder capacity는 16이며, producer 반복문 직전과 직후의 `recorder.Count` 차이를 기록했다. 파일을 닫은 뒤 line 수가 정확히 100,000인지 별도로 검증했다.

## 5회 원시 결과

괄호 안은 10,000,000 ticks/s 기준 환산 시간이다.

| 구현 | 실행 | Producer ticks | Flush 포함 completion ticks | GC.Alloc event/sample proxy | 파일 기록 수 |
|---|---:|---:|---:|---:|---:|
| 동기 | 1 | 3,780,583 (378.0583ms) | 3,782,148 (378.2148ms) | 1,000,000 | 100,000 |
| 동기 | 2 | 4,311,654 (431.1654ms) | 4,314,506 (431.4506ms) | 1,000,000 | 100,000 |
| 동기 | 3 | 3,360,780 (336.0780ms) | 3,362,645 (336.2645ms) | 1,000,000 | 100,000 |
| 동기 | 4 | 3,729,419 (372.9419ms) | 3,731,541 (373.1541ms) | 1,000,000 | 100,000 |
| 동기 | 5 | 4,404,292 (440.4292ms) | 4,407,295 (440.7295ms) | 1,000,000 | 100,000 |
| 비동기 | 1 | 307,334 (30.7334ms) | 5,990,637 (599.0637ms) | 0 | 100,000 |
| 비동기 | 2 | 187,786 (18.7786ms) | 4,251,176 (425.1176ms) | 0 | 100,000 |
| 비동기 | 3 | 280,278 (28.0278ms) | 5,497,390 (549.7390ms) | 0 | 100,000 |
| 비동기 | 4 | 243,632 (24.3632ms) | 4,705,489 (470.5489ms) | 0 | 100,000 |
| 비동기 | 5 | 310,909 (31.0909ms) | 8,853,385 (885.3385ms) | 0 | 100,000 |

모든 비동기 실행은 Unity Test Runner 결과 1/1 통과이며, `allocationSamples=0`, `persisted=100000`을 기록했다.

## 중앙값 비교

| 지표 | 동기 중앙값 | 비동기 중앙값 | 변화 |
|---|---:|---:|---:|
| Producer | 3,780,583 ticks (378.0583ms) | 280,278 ticks (28.0278ms) | 92.5864% 감소, 13.4887배 빠름 |
| Flush 포함 completion | 3,782,148 ticks (378.2148ms) | 5,497,390 ticks (549.7390ms) | 45.3510% 증가, 1.4535배 소요 |
| GC.Alloc event/sample proxy | 1,000,000 | 0 | producer 구간 관측 0 |
| 파일 기록 수 | 100,000 | 100,000 | 손실 없음 |

## 해석

비동기 구현은 호출자 스레드가 파일 포맷팅과 I/O를 기다리지 않게 해 비경합 producer 중앙값을 378.0583ms에서 28.0278ms로 낮췄다. 이는 로그 호출이 게임 로직을 점유하는 시간을 줄인 결과다.

반면 모든 항목이 파일에 기록되고 `Flush()`가 끝날 때까지의 중앙값은 378.2148ms에서 549.7390ms로 45.3510% 증가했다. 따라서 이번 결과는 caller latency 개선을 입증하지만 총 drain throughput 개선을 뜻하지 않는다. 종료나 강제 동기화 시점의 전체 배출 속도가 중요한 사용 사례에서는 worker의 포맷팅과 파일 쓰기 처리량을 별도로 최적화해야 한다.

## 제한 사항

- 할당 값은 **Unity 6000.3 Windows Editor-specific GC.Alloc event/sample count proxy**다. 할당 바이트 수가 아니며 Unity 버전이나 실행 환경을 넘는 절대값으로 해석할 수 없다.
- `ProfilerRecorder.Count`와 capacity에 대한 문서 설명과 달리, capacity 16에서 동기 기준선의 관측 Count가 1,000,000까지 증가했다. 이 때문에 동일 Unity 버전, 동일 marker, 동일 옵션에서의 상대 비교에만 사용했다.
- Windows Editor PlayMode 한 대에서만 측정했다. Player 빌드, Android/iOS 등 모바일 장치와 다른 데스크톱 환경은 측정하지 않았다.
- oversized queue로 비경합 경로만 측정했다. 기본 capacity 4,096에서 queue 포화 시 발생하는 backpressure와 producer 대기는 이 결과에 포함되지 않는다.
- wall-clock 결과에는 OS 스케줄링, worker 배치, 파일 시스템 캐시와 다른 시스템 부하의 잡음이 포함된다. 특히 비동기 completion 실행 간 편차가 크므로 중앙값을 사용했다.

## 결론

승인 기준은 통과했다.

- 비동기 producer 중앙값이 동기 기준선보다 낮음: 통과
- 비동기 producer의 GC.Alloc event/sample count proxy가 0: 통과
- 각 실행에서 100,000건 파일 보존: 통과

다만 Flush 포함 completion 중앙값은 45.3510% 회귀했다. 따라서 비동기 `FileSink`는 호출자 지연을 줄이는 목적에는 적합하지만, 전체 파일 배출 처리량이 기존 동기 구현보다 빨라졌다고 결론 내릴 수 없다.
