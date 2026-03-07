# TraceForge

Unity 6.3 LTS UPM 경량 로깅 패키지.

## 아키텍처

- **Sink 기반**: `ILogSink` 인터페이스를 구현하여 로그 목적지를 교체/조합
- **Category + Verbosity 모델**: 카테고리별 필터링 + 6단계 verbosity (Trace/Debug/Info/Warning/Error/Fatal)
- **Unity Console 브릿지**: 선택적 `UnityConsoleSink` — 기본 제공, opt-out 가능
- **Zero-allocation 원칙**: disabled log path에서 문자열 할당 없음

## UPM 패키지 구조

레포 루트 = 패키지 루트 (Git URL 직접 설치 구조)

```
(repo root)
├── package.json                   (com.skrawberries.traceforge, v0.1.0)
├── CHANGELOG.md
├── LICENSE.md
├── README.md
├── Runtime/
│   ├── TraceForge.Runtime.asmdef
│   ├── AssemblyInfo.cs            (InternalsVisibleTo tests)
│   ├── TF.cs                      (정적 파사드)
│   ├── Verbosity.cs               (enum: Trace~Fatal, Off)
│   ├── LogCategory.cs             (readonly struct)
│   ├── LogEntry.cs                (readonly struct)
│   ├── ILogSink.cs                (Write(in LogEntry), Flush())
│   ├── Logger.cs                  (internal 코어 엔진)
│   ├── LoggerConfig.cs            (런타임 설정)
│   ├── Categories.cs              (Default, Gameplay, Network, UI, Audio, Physics, AI, Performance)
│   └── Sinks/
│       ├── UnityConsoleSink.cs    (Main Thread 전용, ThreadStatic re-entry guard)
│       ├── RingBufferSink.cs      (circular buffer, lock 기반)
│       └── FileSink.cs            (IDisposable, UTF-8, lock 기반)
├── Editor/
│   ├── TraceForge.Editor.asmdef
│   ├── TraceForgeSettingsProvider.cs  (Project Settings > TraceForge)
│   ├── TraceForgeLogViewerWindow.cs   (Window > TraceForge > Log Viewer)
│   └── TraceForgeMenuItems.cs
├── Tests/
│   ├── Runtime/
│   │   ├── TraceForge.Tests.Runtime.asmdef
│   │   ├── VerbosityFilteringTests.cs
│   │   ├── CategoryFilteringTests.cs
│   │   ├── SinkDispatchTests.cs
│   │   ├── RingBufferSinkTests.cs
│   │   ├── FileSinkTests.cs
│   │   └── LogEntryTests.cs
│   └── Editor/
│       ├── TraceForge.Tests.Editor.asmdef
│       ├── SettingsProviderTests.cs
│       └── PackageMetaValidationTests.cs
├── Documentation~/
│   └── TraceForge.md
└── Samples~/
    └── BasicUsage/
        ├── TraceForgeBasicUsage.cs
        └── README.md
```

## 브랜칭 전략

- **main**: UPM 패키지 전용 (Git URL 설치 대상). Claude 파일 없음. .meta 포함.
- **dev**: 작업 브랜치. Claude 파일(`.claude/`, `CLAUDE.md`) + .meta 포함.
- 모든 개발은 `dev`에서 진행. `main`에서 직접 커밋 금지.
- 머지: `dev → main` 단방향. `traceforge-release-merge` 스킬 사용.
- .gitignore: 브랜치마다 다름 (main은 `.claude/`와 `CLAUDE.md`를 ignore).

## 코딩 컨벤션

- Namespace: `TraceForge` (Runtime), `TraceForge.Editor` (Editor)
- 퍼블릭 타입명에 `TraceForge` prefix 사용 금지 — namespace로 충분
- `Debug.Log` 직접 호출 금지 — `UnityConsoleSink` 경유
- Hot path: verbosity/category 체크를 문자열 생성 전에 수행
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` 적용: `IsEnabled` 등 단순 조건 메서드
- 외부 패키지 의존성 금지 (Runtime asmdef)
- 인터페이스: `I` prefix (`ILogSink`, `ILogger`)
- Sink 계층 메서드명: `Write` / Logger 계층 메서드명: `Log`

## 핵심 타입 용어

| 개념 | 사용 용어 | 사용 금지 |
|------|-----------|-----------|
| 로그 대상 | Sink | Appender, Target |
| 분류 기준 | Category | Tag, Channel |
| 출력 수준 | Verbosity | Level, Severity |

## 스킬 사용 지침

| 스킬 | 사용 시점 |
|------|----------|
| `traceforge-package-audit` | 스캐폴딩 직후, 릴리스 전 UPM 구조 검증 |
| `traceforge-performance-review` | 코어 로직 변경 후 hot-path 할당 검사 |
| `traceforge-api-consistency` | 퍼블릭 API 추가/변경 후 일관성 검증 |
| `traceforge-release-check` | 버전 태그 전 최종 관문 |
| `traceforge-release-merge` | dev→main 머지 시 |
| `traceforge-version-bump` | 버전 업그레이드 시 |
| `traceforge-research` | 아키텍처 의사결정 전 격리된 조사 |
