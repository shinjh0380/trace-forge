# TraceForge 보류 이슈

이 문서는 2026-07-22 프로젝트 분석에서 확인했지만 비동기 파일 로깅 작업 범위에서는 수정하지 않기로 합의한 항목을 기록한다.

## 1. Log Viewer와 RingBufferSink가 연결되지 않음

### 현재 상태

`TraceForgeLogViewerWindow`에는 `SetRingBuffer()`가 있지만 호출 경로가 없다. 샘플이 `RingBufferSink`를 등록해도 Viewer가 해당 인스턴스를 찾거나 전달받지 못한다.

### 영향

제공된 기본 흐름으로는 Log Viewer가 항상 빈 상태로 남는다.

### 향후 완료 조건

- Viewer가 활성 `RingBufferSink`를 명시적인 공개 API 또는 안전한 registry로 연결한다.
- 창 재오픈과 Play Mode 전환 후에도 올바른 sink를 표시한다.
- 연결, 갱신, clear 동작을 Editor 테스트로 검증한다.

## 2. 카테고리 verbosity override 의미 불일치

### 현재 상태

`Logger.IsEnabled(verbosity, category)`가 전역 최소 verbosity를 먼저 검사해 반환한다. 따라서 카테고리 설정은 전역 필터보다 더 엄격하게 만들 수 있지만 더 낮은 verbosity를 다시 허용할 수 없다. 문서는 카테고리 설정이 전역 값을 override하며 우선한다고 설명한다.

### 영향

전역 `Info`, 특정 카테고리 `Trace` 같은 설정이 기대와 다르게 동작한다.

### 향후 완료 조건

- 카테고리 설정을 진정한 override로 할지 추가 제한으로 할지 명시적으로 결정한다.
- 구현, XML 문서, README의 의미를 통일한다.
- 전역보다 높은 값과 낮은 값 모두에 대한 회귀 테스트를 추가한다.

## 3. Project Settings가 자동 적용되지 않음

### 현재 상태

`ApplySavedSettings()`는 존재하지만 자동 초기화 속성이나 호출처가 없다. UI는 `SettingsScope.Project`지만 값은 프로젝트 단위가 아닌 `EditorPrefs`에 저장된다. 플레이어 빌드에도 전달되지 않는다.

### 영향

설정을 변경한 순간에만 Logger에 반영되며, 도메인 재로드나 에디터 재시작 후 저장값과 실제 런타임 값이 달라질 수 있다.

### 향후 완료 조건

- 설정의 범위를 Editor 개인 설정, 프로젝트 설정, Player 설정 중 하나로 확정한다.
- 선택한 범위에 맞는 저장 방식을 사용한다.
- 시작, 도메인 재로드, Play Mode 전환 및 Player 적용을 테스트한다.

## 4. Zero-allocation 및 compile-time stripping 표현이 과도함

### 현재 상태

호출 인자는 `TF.Trace()` 진입 전에 평가되므로 메서드 본문을 전처리기로 비워도 문자열 생성과 부수 효과가 남는다. `IsEnabled()`는 `TRACEFORGE_STRIP_TRACE`와 `TRACEFORGE_STRIP_DEBUG` 또는 등록 sink 유무를 고려하지 않는다.

### 영향

사용자가 문서의 보장을 신뢰하면 비활성 로그에서도 예상하지 않은 할당과 계산이 발생할 수 있다.

### 향후 완료 조건

- 보장 범위를 정확히 문서화하거나 호출 전 할당을 피하는 API를 설계한다.
- stripping 심볼별 `IsEnabled()` 동작을 일치시킨다.
- 메시지 생성 부수 효과와 할당을 검증하는 테스트를 추가한다.

## 5. Thread safety와 flush 생명주기 계약 불일치

### 현재 상태

기존 문서는 모든 sink가 thread-safe라고 설명했지만 `UnityConsoleSink`는 Main Thread 전용이었다. 또한 `ILogSink.Flush()`는 애플리케이션 종료 시 자동 호출된다고 설명하지만 명시적인 종료 hook은 없고 subsystem registration 시점에만 flush된다.

비동기 파일 로깅 작업에서 `UnityConsoleSink`는 제거되지만, 종료 시 자동 flush와 sink 소유권 문제는 별도로 남는다.

### 영향

종료 순서나 sink 제거 방식에 따라 버퍼가 남거나 리소스 해제 책임이 불명확할 수 있다.

### 향후 완료 조건

- `Logger`, `TF`, sink 소유자의 flush 및 dispose 책임을 명문화한다.
- 애플리케이션 종료, Play Mode 전환, subsystem registration, `TF.Reset()`을 각각 검증한다.
- 문서와 실제 hook을 일치시킨다.

## 6. 릴리스 메타데이터 불일치

### 현재 상태

- package 버전과 changelog 최신 버전이 다르다.
- README 설치 URL과 package repository URL이 다르다.
- Unity 6.3 LTS, Unity 6.x, 최소 `6000.0` 표기가 혼재한다.
- 배포용 `main`에는 테스트가 없고 자동 검증 흐름이 보이지 않는다.

### 영향

설치 경로, 지원 버전, 변경 이력과 릴리스 신뢰성이 불명확하다.

### 향후 완료 조건

- canonical repository URL을 하나로 통일한다.
- 실제 최소 지원 Unity 버전과 권장 버전을 구분해 표기한다.
- package 버전, changelog, tag를 동일하게 맞춘다.
- `dev` 테스트를 사용해 배포용 `main` 패키지를 검증하는 CI 또는 release check를 마련한다.
