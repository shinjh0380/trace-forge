# TraceForge API Guidelines

퍼블릭 API 검토 기준 23항목. 위반 시 수정 후 머지.

---

## 네이밍 — 5항목

- [ ] 1. 퍼블릭 타입이 PascalCase
  - Grep: `^public (class|interface|struct|enum) [a-z]` → 소문자 시작 금지
- [ ] 2. 인터페이스가 `I` prefix로 시작
  - 올바름: `ILogSink`, `ILogger` / 잘못됨: `LogSink`, `Sink`
- [ ] 3. `LogVerbosity` enum 값 순서: `Trace, Debug, Info, Warning, Error, Fatal` (오름차순 severity)
- [ ] 4. 타입명에 `TraceForge` prefix 없음 (namespace로 충분)
  - 잘못됨: `TraceForgeLogger`, `TraceForgeLogSink`
- [ ] 5. Logger 계층 메서드명: `Log`, `LogWarning`, `LogError` (Unity Logger 패턴 정렬)
  - Sink 계층 메서드명: `Write` (구분 유지)

---

## 가시성 — 4항목

- [ ] 6. 내부 구현 클래스가 `internal` 또는 `private` — 의도치 않은 `public` 노출 없음
  - Grep: `public class.*Impl`, `public class.*Helper`, `public class.*Internal`
- [ ] 7. `protected` 멤버가 상속 확장 지원 목적으로만 사용 (sealed 클래스에 protected 없음)
- [ ] 8. Extension 메서드가 별도 `static` 클래스에 분리 (`LoggerExtensions`, `SinkExtensions`)
- [ ] 9. 복잡한 객체 생성에 팩토리/빌더 패턴 사용 — 생성자 직접 노출 최소화

---

## 메서드 시그니처 — 5항목

- [ ] 10. 퍼블릭 Log 메서드 파라미터 순서: `(LogCategory category, LogVerbosity verbosity, string message, ...)`
  - 또는 category/verbosity를 이미 담은 컨텍스트 오브젝트로 단순화된 형태
- [ ] 11. `object`/`params object[]` 오버로드 외에 boxing 방지용 제네릭 오버로드 제공
  - 예: `Log<T>(LogCategory, LogVerbosity, T value)` where `T : struct`
- [ ] 12. `Exception` 파라미터가 항상 마지막 위치 (optional 파라미터로)
- [ ] 13. 비동기 sink (`IAsyncLogSink`)가 있다면 `CancellationToken` 파라미터 포함
- [ ] 14. Optional 파라미터(default value)가 오버로드 체인의 대안으로 남용되지 않음
  - 오버로드 3개 이상이면 optional 파라미터 대신 오버로드 분리 권장

---

## 용어 일관성 — 4항목

- [ ] 15. `Write` vs `Log` 구분 유지
  - `ILogSink.Write(LogEntry)` — sink 레이어
  - `Logger.Log(...)` — 퍼블릭 레이어
- [ ] 16. `Sink`와 `Appender`/`Target`/`Handler` 혼용 없음
  - Grep: `Appender|Target|Handler` in `Runtime/` (타입명에서)
- [ ] 17. `Category`와 `Tag`/`Channel`/`Namespace` 혼용 없음
  - Grep: `Tag|Channel` in `Runtime/` (파라미터명/타입명에서)
- [ ] 18. `Verbosity`와 `Level`/`Severity`/`Priority` 혼용 없음
  - Grep: `LogLevel|Severity|Priority` in `Runtime/`

---

## Unity 정렬 — 2항목

- [ ] 19. `UnityEngine.Object context` 파라미터가 Unity Logger 패턴처럼 마지막 optional 파라미터로 지원됨
  - 예: `Log(LogCategory, LogVerbosity, string, UnityEngine.Object context = null)`
- [ ] 20. Logger wrapper 메서드에 `[HideInCallstack]` 어트리뷰트 적용 (Unity 2023+)
  - Console에서 클릭 시 wrapper가 아닌 실제 호출 위치로 이동

---

## 문서화 — 3항목

- [ ] 21. 모든 퍼블릭 타입과 메서드에 `<summary>` XML 주석 존재
  - Grep: `^\s+public` → 바로 위 줄에 `///` 없는 경우
- [ ] 22. 파라미터가 있는 메서드에 `<param name="...">` 태그 존재
  - `<returns>` 및 `<exception>` 태그도 해당 시 포함
- [ ] 23. 복잡한 사용법을 가진 API에 `<example>` 코드 스니펫 포함
  - 예: sink 등록, category 필터 설정, custom sink 구현
