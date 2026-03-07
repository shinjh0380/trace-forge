# Unity UPM & TraceForge 참조 노트

설계 결정 시 참고할 기술 기준 및 제약 사항.

---

## Unity 6.3 LTS 관련

### C# 언어 수준
- Unity 6.3 = C# 9.0 지원
- 사용 가능: record, init-only setter, target-typed new, pattern matching (C# 9)
- 미사용: C# 10+ (global using, file-scoped namespace)

### 주요 Unity API

```csharp
// Unity Console 브릿지
UnityEngine.Debug.Log(string message, UnityEngine.Object context)
UnityEngine.Debug.LogWarning(string message, UnityEngine.Object context)
UnityEngine.Debug.LogError(string message, UnityEngine.Object context)
UnityEngine.Debug.LogException(Exception exception, UnityEngine.Object context)

// 콜스택 숨김 (Unity 2023+, Unity 6 포함)
[HideInCallstack] // method attribute

// 로그 메시지 수신
Application.logMessageReceived += (condition, stackTrace, type) => { };
Application.logMessageReceivedThreaded += ...; // 멀티스레드 버전

// 도메인 리로드 대응
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void Init() { /* 정적 상태 재초기화 */ }
```

### 알려진 제약
- `[HideInCallstack]`은 Unity 2023.1부터 지원 (Unity 6.3 포함)
- `Application.logMessageReceived`는 Main Thread에서만 안전
- `Application.logMessageReceivedThreaded`는 Unity 로그를 TraceForge로 포워딩 시 사용

---

## UPM 패키지 구조 기준

### package.json 필수 필드
```json
{
  "name": "com.vendor.package",     // reverse domain
  "version": "1.0.0",               // semver
  "unity": "6000.3",                // min Unity version
  "displayName": "Human Name",
  "description": "...",
  "author": { "name": "...", "email": "..." },
  "license": "MIT"
}
```

### 디렉토리 컨벤션
- `Runtime/` — 런타임 코드 (asmdef 필수)
- `Editor/` — 에디터 전용 코드 (asmdef 필수, includePlatforms: ["Editor"])
- `Tests/` — 테스트 (asmdef 필수, testables 설정)
- `Samples~/` — 임포트 가능한 샘플 (물결표 = UPM 숨김 폴더)
- `Documentation~/` — 문서 (선택, 물결표 = UPM 숨김 폴더)

### Git URL 설치
```
https://github.com/user/repo.git                    # 루트에 package.json
https://github.com/user/repo.git?path=Packages/com.vendor.pkg  # 서브디렉토리
```

---

## Sink 아키텍처 참조

### 비교 대상 (학습용)
| 라이브러리 | Sink 명칭 | 특징 |
|------------|-----------|------|
| Serilog | Sink | ILogEventSink.Emit(LogEvent) |
| NLog | Target | ThreadAgnosticAttribute로 스레드 안전성 표시 |
| Microsoft.Extensions.Logging | ILoggerProvider | scope 개념 포함 |
| ZLogger (Unity 특화) | IAsyncLogProcessor | async-first, zero-alloc format |

### TraceForge Sink 설계 원칙
- `ILogSink.Write(in LogEntry entry)` — synchronous, `in` 파라미터로 struct 복사 최소화
- sink는 스레드 안전성을 자체 보장 (문서 명시)
- UnityConsoleSink는 Main Thread 전용 (명시 필요)
- 비동기 sink는 별도 `IAsyncLogSink` 인터페이스로 분리 고려

---

## 성능 기준

### 로깅 오버헤드 목표 (disabled path)
- **목표**: 0 바이트 할당 (Profiler에서 측정 시)
- **허용**: 조건 체크 1-2회 비교 연산
- **금지**: any `new`, string interpolation, array creation

### 로깅 오버헤드 목표 (enabled path)
- LogEntry 생성: 스택 할당 (struct)
- 포맷팅: sink 책임 (logger core X)
- sink 디스패치: 배열 직접 인덱싱, no LINQ

### Unity Profiler 측정 방법
```csharp
// 테스트에서 GC.Collect() 후 GC.GetTotalMemory(false) 비교
// 또는 ProfilerRecorder 사용 (Unity 2021+)
var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
```

---

## 알려진 Unity 로깅 문제 패턴

1. **재귀 루프**: `Application.logMessageReceived` 핸들러 내에서 `Debug.Log` 호출 → 무한 재귀
2. **도메인 리로드**: Play Mode 진입/종료 시 정적 필드 초기화됨 — sink 목록 초기화 필요
3. **스레드 안전성**: `Debug.Log`는 Main Thread 전용 — Worker Thread에서 큐잉 후 Main Thread 디스패치 필요
4. **스택 트레이스 성능**: `StackTrace` 객체 생성은 무거움 — 기본 비활성화 권장
