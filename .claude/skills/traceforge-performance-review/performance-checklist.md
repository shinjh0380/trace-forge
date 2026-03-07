# TraceForge Performance Checklist

핵심 원칙: **disabled log path에서 힙 할당 제로**

---

## Disabled Log Path — 7항목

- [ ] 1. verbosity 체크가 문자열 보간/Format 전에 수행됨
  - Grep: `$"` 또는 `string.Format` in Logger.cs → 앞에 IsEnabled 체크 존재 확인
- [ ] 2. `IsEnabled(category, verbosity)` 조기 탈출이 모든 퍼블릭 Log 메서드에 적용됨
- [ ] 3. `$""` 문자열 보간이 enabled 체크 없이 Log 인수로 직접 사용되지 않음
- [ ] 4. `params object[]` 오버로드가 disabled 경로에서 배열을 할당하지 않음
  - 대안: generic 오버로드 (`Log<T0>`, `Log<T0, T1>`) 제공 확인
- [ ] 5. 람다/Func 인수가 enabled 체크 통과 후에만 호출됨 (defer 패턴)
- [ ] 6. `[MethodImpl(MethodImplOptions.AggressiveInlining)]`이 `IsEnabled` 등 단순 조건 메서드에 적용됨
- [ ] 7. `LogEntry`가 `struct` 타입 (힙 할당 없이 스택에서 생성)

---

## 문자열 처리 — 5항목

- [ ] 8. 메시지 포맷 결합(concatenation)이 sink 내부에서만 이루어짐 (Logger core X)
- [ ] 9. `StringBuilder` 사용 시 재사용 또는 오브젝트 풀링 적용
- [ ] 10. enabled 체크 전에 `ToString()` 호출이 없음
- [ ] 11. `LogCategory` 이름이 `const` 또는 `static readonly string` (런타임 생성 X)
- [ ] 12. 스택 트레이스 캡처(`StackTrace`, `Environment.StackTrace`)가 명시적 opt-in으로만 활성화됨

---

## Sink 디스패치 — 4항목

- [ ] 13. 내부 sink 목록이 `IReadOnlyList<ILogSink>` 또는 배열 — `List<T>.ForEach`보다 직접 인덱싱
- [ ] 14. sink 등록/해제가 copy-on-write 또는 `Interlocked.Exchange`로 읽기 경로에 lock 없음
- [ ] 15. `ILogSink.Write(LogEntry entry)` 호출 시 인터페이스 디스패치 외 추가 박싱 없음
  - `LogEntry`가 struct면 ref/in 파라미터 고려
- [ ] 16. null sink 또는 빈 sink 목록에 대한 조기 탈출 경로 존재

---

## 할당 패턴 — 5항목

- [ ] 17. `LogEntry`가 `readonly struct` (불변 value type)
- [ ] 18. `LogCategory`가 value type 또는 flyweight string (런타임 인스턴스 최소화)
- [ ] 19. LINQ 사용이 hot path (Logger.Log 경로)에 없음
  - Grep: `using System.Linq` in `Runtime/Core/`
- [ ] 20. 로그 메서드에서 `IEnumerable` 반환 없음 (필터링은 내부에서 완결)
- [ ] 21. 이벤트/Action 델리게이트 할당이 초기화 시점에만 이루어짐 (Log 호출마다 X)

---

## 스레딩 — 3항목

- [ ] 22. `Logger.Log`가 재진입 안전 (reentrant-safe) — 같은 스레드 재귀 호출 시 무한루프 없음
- [ ] 23. sink 목록 읽기가 lock-free (쓰기만 lock) 또는 `ReaderWriterLockSlim` 사용
- [ ] 24. 각 sink의 스레드 안전성 보장 수준이 XML 주석에 문서화됨

---

## Unity 특화 — 3항목

- [ ] 25. Main Thread에서만 호출 가능한 API (UnityConsoleSink 등)가 XML 주석에 명시됨
- [ ] 26. `UnityConsoleSink`에서 `Application.logMessageReceived` 콜백 내 Log 호출 방지
  - (Unity Console → TraceForge → Unity Console 무한 루프)
  - Grep: `logMessageReceived` → 재귀 방지 guard 존재 확인
- [ ] 27. 도메인 리로드(`[RuntimeInitializeOnLoadMethod]` 또는 `[InitializeOnLoad]`) 시
  정적 Logger 상태가 올바르게 재초기화됨
