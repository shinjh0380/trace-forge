# TraceForge 비동기 파일 로깅 설계

- 상태: 사용자 승인 완료
- 작성일: 2026-07-22
- 대상 브랜치: `feature/async-file-sink` (`dev` 기반)

## 목적

`UnityEngine.Debug.Log*`를 로그 출력 경로에서 완전히 제거하고, 기존 `TF` 및 `ILogSink` API를 유지하면서 호출 스레드의 로깅 비용을 줄인다. 권장 출력 조합은 비동기 `FileSink`와 기존 `RingBufferSink`다.

## 범위

### 포함

- 기존 `FileSink` 내부를 고정 용량 큐와 전용 C# writer thread 기반으로 변경
- `UnityConsoleSink`와 관련 메타 파일 제거
- `Logger`의 sink 오류 fallback을 `Console.Error`로 변경
- README, 런타임 문서, 샘플에서 Unity Console 사용 제거
- 순서, 무손실, 동시성, flush, dispose 및 호출 스레드 할당 검증
- 현재 알려진 이슈 1~6을 별도 문서에 기록

### 제외

- `TF.Info`, `TF.Debug` 등 호출 API 변경
- 문자열 보간식의 호출 전 할당 개선
- 로그 뷰어와 `RingBufferSink` 연결 수정
- 카테고리 필터, Project Settings, stripping 의미 수정
- WebGL 지원
- 자동 파일 회전, 압축, 업로드

## 결정 사항

- 지원 대상은 Windows, macOS, Linux, Android, iOS다.
- WebGL은 background thread 및 파일시스템 제약 때문에 이번 범위에서 제외한다.
- 큐는 무손실 정책을 사용한다. 큐가 가득 차면 빈 슬롯이 생길 때까지 producer가 대기한다.
- `UnityConsoleSink`는 호환 계층을 남기지 않고 완전히 제거한다.
- 비동기화는 `Logger` 전체가 아니라 `FileSink` 내부에만 적용한다.
- 기존 `TF` 및 `ILogSink` API는 유지한다.

## 목표 데이터 흐름

```text
TF 호출
  -> Logger 필터링
     -> RingBufferSink.Write: 기존 동기 메모리 저장
     -> FileSink.Write: 고정 용량 큐에 LogEntry 등록
                         |
                         v
                    전용 writer thread
                         |
                         v
                  포맷팅 및 StreamWriter 기록
```

## FileSink 설계

### API 호환성

기존 두 인자 생성자는 그대로 유지하고, 큐 크기를 지정할 수 있는 오버로드를 추가한다.

```csharp
public FileSink(string filePath, bool append = false)
public FileSink(string filePath, bool append, int queueCapacity)
```

기본 큐 용량은 4,096개다. `queueCapacity <= 0`은 `ArgumentOutOfRangeException`으로 거부한다.

### 큐

- 생성 시 `LogEntry[]` 원형 버퍼를 한 번 할당한다.
- `Monitor`로 head, tail, count와 상태를 보호한다.
- 정상 enqueue 구간에서는 파일 포맷팅과 I/O를 하지 않는다.
- 큐가 가득 차면 `Monitor.Wait`하고, writer가 슬롯을 반환하면 다시 진행한다.
- writer는 큐 순서대로 항목을 가져가므로 단일 전역 기록 순서를 보존한다.
- 다중 producer의 동시 호출을 지원한다.

### Writer

- `System.Threading.Thread`를 사용하며 `IsBackground = true`로 설정한다.
- 파일 포맷팅과 `StreamWriter.Write*`는 writer thread에서만 수행한다.
- 일반 로그는 결합된 최종 문자열을 만들지 않고 timestamp, verbosity, category, message를 순서대로 writer에 기록한다.
- verbosity 문자열은 고정 문자열을 사용한다.
- 예외 로그의 `Exception.ToString()` 할당은 희귀 오류 경로로 허용한다.

### Flush

- `Flush()` 진입 시점까지 접수된 마지막 sequence를 캡처한다.
- 해당 sequence가 실제 `StreamWriter`에 기록될 때까지 대기한다.
- writer와 동기화한 뒤 `StreamWriter.Flush()`를 호출한다.
- `Flush()` 이후에 접수된 로그까지 기다릴 필요는 없다.

### Dispose

- 신규 enqueue를 중단하고 대기 중인 producer를 깨운다.
- 이미 접수된 항목을 모두 기록한다.
- writer thread를 종료하고 join한 다음 `StreamWriter`를 닫는다.
- cleanup은 항상 시도하며, 저장된 writer 오류가 있으면 cleanup 후 호출자에게 전달한다.
- dispose 이후 `Write()`는 기존 동작과 같이 아무 작업도 하지 않는다.

## 오류 처리

- 파일 생성 실패는 생성자에서 즉시 예외로 전달한다.
- writer에서 I/O 오류가 발생하면 최초 예외를 저장하고 sink를 fault 상태로 전환한다.
- fault 전환 시 producer, `Flush()`, `Dispose()` 대기자를 모두 깨운다.
- fault 이후 `Write()`와 `Flush()`는 무한 대기하지 않고 저장된 원인과 함께 실패한다.
- `Logger`는 sink 예외가 게임 루프까지 전파되지 않도록 기존처럼 격리한다.
- `Logger`의 최후 fallback은 `Debug.LogError` 대신 `Console.Error.WriteLine`을 사용한다.
- 디스크 부족이나 권한 상실 같은 영구 I/O 장애에서는 무손실을 보장할 수 없으며, 조용히 손실시키는 대신 오류를 명시적으로 노출한다.

## 생명주기와 소유권

- `FileSink`는 계속 `IDisposable`이며 생성한 코드가 소유한다.
- 사용자는 sink를 제거한 뒤 dispose하거나, 애플리케이션 종료 시 dispose해야 한다.
- 이번 변경에서 `Logger`가 임의로 사용자 sink를 dispose하도록 소유권 규칙을 바꾸지 않는다.
- 샘플은 `OnDestroy()`에서 `FileSink.Dispose()`를 호출한다.

## 테스트 전략

구현은 `dev` 기반 작업 브랜치에서 기존 NUnit 테스트를 확장하며 테스트 우선으로 진행한다.

1. 생성자 입력 및 기존 API 호환성
2. `Flush()` 후 모든 로그가 정확히 한 번 기록됨
3. 기록 순서 보존
4. 작은 큐가 포화됐을 때 producer가 대기하고 writer 진행 후 재개됨
5. 다중 producer에서 누락과 중복이 없음
6. `Dispose()`가 잔여 로그를 모두 기록하고 thread를 종료함
7. dispose 이후 `Write()`가 기존처럼 무시됨
8. 큐가 포화되지 않은 일반 로그 경로의 producer thread에서 `Write()`당 추가 할당이 없음
9. 런타임 소스에 `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, `Debug.LogException` 참조가 없음

패키지 루트 자체는 Unity 프로젝트가 아니므로, 실제 NUnit 실행은 로컬 패키지를 참조하는 임시 Unity 호스트 프로젝트에서 Unity Test Runner로 수행한다. 저장소에는 호스트 프로젝트 산출물을 남기지 않는다.

## 성능 검증

- 변경 전에 기존 동기 `FileSink` 기준값을 기록한다.
- 동일한 사전 생성 메시지 100,000개로 warm-up 후 측정한다.
- enqueue 구간과 `Flush()`를 포함한 전체 완료 구간을 분리해 측정한다.
- 새 구현의 producer-side enqueue 시간이 기존 동기 구현보다 짧아야 한다.
- 큐가 포화되지 않은 상태에서 producer thread 기준 `Write()`당 추가 할당은 0B여야 한다.
- `Flush()` 이후 파일의 레코드 수는 입력 수와 정확히 같아야 한다.
- Unity 6000.0과 6000.3 API 기준으로 Runtime, Editor, 테스트 컴파일을 확인한다.

## 문서 변경

- README에서 Unity Console sink 초기화와 관련 설명을 제거한다.
- 샘플 기본 sink를 `FileSink`와 `RingBufferSink`로 제한한다.
- `Documentation~/TraceForge.md`에서 thread safety와 출력 경로를 새 구조에 맞춘다.
- 나중에 처리할 기존 이슈는 `Documentation~/DeferredIssues.md`에 유지한다.

## 완료 조건

- `UnityConsoleSink.cs`와 메타 파일이 없다.
- 런타임 로깅 코드에 `UnityEngine.Debug.Log*` 호출이 없다.
- 기존 `TF` 호출 API가 컴파일된다.
- 비동기 `FileSink`의 무손실, 순서, flush, dispose 및 동시성 테스트가 통과한다.
- 성능 검증에서 producer 호출 경로가 기존 동기 구현보다 빠르고 추가 할당이 없다.
- Unity 6000.0 및 6000.3 컴파일이 통과한다.
- README, 샘플, 런타임 문서가 구현과 일치한다.
