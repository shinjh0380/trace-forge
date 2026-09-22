# TraceForge 보류 이슈

이 문서는 2026-07-22 프로젝트 분석에서 확인했지만 비동기 파일 로깅 작업 범위에서는 수정하지 않기로 합의한 항목을 기록한다.

## 1. Log Viewer와 RingBufferSink가 연결되지 않음

**해결됨 (`0381ebd`, Phase 1)** — the Editor bootstrap creates and registers
the default ring buffer, and the Log Viewer binds to the active registry sink
across window reopen and Play Mode transitions. Editor tests cover registration
and release behavior.

### 해결 전 상태

`TraceForgeLogViewerWindow`에는 `SetRingBuffer()`가 있지만 호출 경로가 없다. 샘플이 `RingBufferSink`를 등록해도 Viewer가 해당 인스턴스를 찾거나 전달받지 못한다.

### 당시 영향

제공된 기본 흐름으로는 Log Viewer가 항상 빈 상태로 남는다.

### 완료 내용

- Viewer가 활성 `RingBufferSink`를 명시적인 공개 API 또는 안전한 registry로 연결한다.
- 창 재오픈과 Play Mode 전환 후에도 올바른 sink를 표시한다.
- 연결, 갱신, clear 동작을 Editor 테스트로 검증한다.

## 2. 카테고리 verbosity override 의미 불일치

**해결됨 (`9b89441`)** — 카테고리 값이 있으면 전역 최소값을 대체하는 true override로 통일했다.

### 해결 전 상태

`Logger.IsEnabled(verbosity, category)`가 전역 최소 verbosity를 먼저 검사해 반환한다. 따라서 카테고리 설정은 전역 필터보다 더 엄격하게 만들 수 있지만 더 낮은 verbosity를 다시 허용할 수 없다. 문서는 카테고리 설정이 전역 값을 override하며 우선한다고 설명한다.

### 영향

전역 `Info`, 특정 카테고리 `Trace` 같은 설정이 기대와 다르게 동작한다.

### 검증 결과

- 전역 `Info` / 카테고리 `Trace`에서는 해당 카테고리의 Trace가 통과하고, 전역 `Trace` / 카테고리 `Error`에서는 Info가 차단된다.
- `ClearCategoryVerbosity` 후 전역 값으로 복귀하며, 카테고리 없는 `IsEnabled(Verbosity)`는 전역 값만 따른다.
- 기존 필터를 복원한 검증에서 신규 회귀 단정 2건이 실패했고, 새 구현은 Phase 3a PlayMode 67/67로 통과했다. CHANGELOG의 Unreleased에 Breaking 변경을 기록했다.

## 3. Project Settings가 자동 적용되지 않음

**해결됨 (`55a4359`)** — Runtime 설정/부트스트랩 기반. SettingsProvider와 Player 빌드 주입은 이 문서를 갱신한 Phase 2 Editor 커밋에서 통합했다.

### 해결 전 상태

`ApplySavedSettings()`는 존재하지만 자동 초기화 속성이나 호출처가 없다. UI는 `SettingsScope.Project`지만 값은 프로젝트 단위가 아닌 `EditorPrefs`에 저장된다. 플레이어 빌드에도 전달되지 않는다.

### 영향

설정을 변경한 순간에만 Logger에 반영되며, 도메인 재로드나 에디터 재시작 후 저장값과 실제 런타임 값이 달라질 수 있다.

### 검증 결과

- `ProjectSettings/TraceForgeSettings.asset`을 프로젝트 단위로 저장하고 Provider 편집을 Logger에 즉시 반영한다.
- Player 빌드에 임시 Preloaded Asset으로 전달하며 성공·실패·저장 상태 재로드 후 원본 목록 복원을 테스트했다.
- Unity 6000.3.8f1에서 실제 Play Mode 3회 전환과 Windows 개발 Player의 `Awake` 이전 설정 적용을 확인했다. 호스트의 기본 domain reload 설정은 기존 Test Framework 문제로 미검증이며, 반복 검사는 domain reload 비활성 상태에서 실행했다.

## 4. Disabled-path 및 compile-time stripping 계약

**해결됨 (`61ffa6e`)** — 문서가 보장하는 범위를 caller-side guard와 body-level stripping semantics에 맞게 수정했고, sink가 없을 때와 strip probe 조합의 `IsEnabled` 동작을 검증했다. 성능 rig 재현성 판정은 별도 benchmark 보고서의 실패 기록을 따른다.

### 해결 전 상태

호출 인자는 `TF.Trace()` 진입 전에 평가되므로 메서드 본문을 전처리기로 비워도 문자열 생성과 부수 효과가 남는다. `IsEnabled()`는 `TRACEFORGE_STRIP_TRACE`와 `TRACEFORGE_STRIP_DEBUG`, 등록 sink 유무를 반영하지 않았다.

### 영향

사용자가 문서의 보장을 신뢰하면 비활성 로그에서도 예상하지 않은 할당과 계산이 발생할 수 있다.

### 해결 내용

- README와 본 문서에서 메서드 본문 stripping과 인자 평가의 차이를 명시했다.
- stripping 심볼과 sink 상태를 반영하도록 `IsEnabled()` 계약을 맞췄다.
- expensive/interpolated message에는 global/category `IsEnabled()` caller guard를 권장한다.

## 5. Thread safety와 flush 생명주기 계약 불일치

**해결됨 (`55a4359`)** — bootstrap 소유 sink의 종료 flush/dispose 및 subsystem 초기화 전 정리. Editor 전환·재로드 정리는 이 문서를 갱신한 Phase 2 Editor 커밋에서 통합했다.

### 해결 전 상태

기존 문서는 모든 sink가 thread-safe라고 설명했지만 `UnityConsoleSink`는 Main Thread 전용이었다. 또한 `ILogSink.Flush()`는 애플리케이션 종료 시 자동 호출된다고 설명하지만 명시적인 종료 hook은 없고 subsystem registration 시점에만 flush된다.

비동기 파일 로깅 작업에서 `UnityConsoleSink`는 제거되지만, 종료 시 자동 flush와 sink 소유권 문제는 별도로 남는다.

### 영향

종료 순서나 sink 제거 방식에 따라 버퍼가 남거나 리소스 해제 책임이 불명확할 수 있다.

### 검증 결과

- bootstrap이 생성한 sink만 remove → flush → dispose한다. `TF.Reset()`은 사용자 sink를 dispose하지 않으며, 사용자 sink의 정리는 생성한 코드의 책임이다.
- 실제 Player가 5초 후 종료할 때 `traceforge.log`의 마지막 항목 보존을 확인했다. 반복 Play Mode 전환 사이에도 파일을 독점 쓰기로 다시 열 수 있었다.
- `Logger.Reset()`의 선행 Shutdown, 사용자 소유권, 등록 중 재진입 종료를 테스트했다. `ILogSink.Flush` XML 문서도 실제 소유권 계약과 일치시켰다.

## 6. 릴리스 메타데이터 불일치

**메타데이터 해결됨 (Phase 5)** — package URL과 README 설치 경로를
`shinjh0380/trace-forge`로 통일하고, 최소 `6000.0`과 테스트 버전 `6000.3`을
구분했다. `0.2.0` 변경 이력을 보완하고 URL·버전 회귀 테스트를 추가했다.
CI 실행 결과는 Phase 5 계획에 기록하며, 새 릴리스 버전과 tag 확정은 Session 7에 남긴다.

### 해결 전 상태

- package 버전과 changelog 최신 버전이 다르다.
- README 설치 URL과 package repository URL이 다르다.
- Unity 6.3 LTS, Unity 6.x, 최소 `6000.0` 표기가 혼재한다.
- 배포용 `main`에는 테스트가 없고 자동 검증 흐름이 보이지 않는다.

### 당시 영향

설치 경로, 지원 버전, 변경 이력과 릴리스 신뢰성이 불명확하다.

### 완료 내용

- canonical repository URL을 하나로 통일했다. author name/email은 그대로 유지했다.
- 최소 지원 Unity 버전과 테스트 버전을 구분했다.
- package 버전과 changelog의 첫 릴리스 항목을 `0.2.0`으로 맞췄다. 새 버전·tag는 아직 만들지 않았다.
- `dev` push/PR과 이번 feature 브랜치 검증용 CI를 추가했다. 실제 CI 통과 여부와 릴리스 검증은 별도로 확인한다.

## 7. Lazy formatting overloads

### 현재 상태

Caller-side guard는 보간과 expensive message construction을 피할 수 있지만, 호출부마다 guard를 작성해야 한다. C# 9/Unity 6 환경에서는 interpolated string handler를 사용할 수 없으므로, allocation-free formatting overloads를 이 Phase에서 구현하지 않는다.

### 향후 방향

`TF.Debug<T0>(string format, T0 arg0)` 같은 overloads와 sink-side lazy formatting은 `LogEntry`가 미포맷 인자를 운반하고 모든 sink가 지연 포맷을 지원하도록 바꾸는 구조적 작업이 필요하다. 별도 설계와 측정 대상으로 남긴다. 자세한 범위는 [Phase 4 Performance Contract — Deferred](Plans/2026-09-22-debug-log-replacement/04-performance-contract.md#deferred-design-separately-after-phase-5)를 따른다.
