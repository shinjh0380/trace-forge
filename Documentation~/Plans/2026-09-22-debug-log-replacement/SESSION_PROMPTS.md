# TraceForge Debug.Log 대체 리팩토링 — 세션별 Codex 프롬프트

`Documentation~/Plans/2026-09-22-debug-log-replacement/` 의 Phase 0~5 를 7개 세션으로 나눈 것입니다.
이하 `00-orchestration.md` 를 "지도", `0N-*.md` 를 "Phase N 문서" 라고 부릅니다.
세션 하나당 Codex 태스크 하나. 위에서부터 순서대로 진행하고, 앞 세션의 "끝나고 확인" 항목이
통과하기 전에 다음 세션을 시작하지 마세요.

AVERUN·Nomad Summoner 프롬프트와 다른 점 세 가지.

- **진행 추적은 각 Phase 문서의 `## Steps` 체크박스**입니다. Step 완료 시 그 줄을 `[x]` 로 바꾸고 같은 커밋에 넣습니다. `PROGRESS.md` 는 만들지 않습니다.
- **Unity 는 Codex 가 batchmode 로 직접 돌립니다.** 테스트 호스트(`.worktrees/native-logger-test-host`)를 세션 1 이 만들고, 모든 통과 기준은 `Unity.exe -batchmode -runTests` 실측입니다. Unity Editor 를 눈으로 확인하는 것만 사람 몫입니다.
- **브랜치 규칙이 있습니다.** 모든 작업은 `feature/native-logger`(`dev` 기반)에서. `main` 직접 커밋 금지. `main` 갱신은 세션 7 에서 `traceforge-release-merge` 절차로만.

## 저장소 현재 상태 (2026-09-22 실측 — 프롬프트가 이 상태를 전제로 씀)

| Phase | 지도 표기 | 실제 |
|---|---|---|
| 0 브랜치 수렴 | `[ ]` | **미착수.** 루트 체크아웃이 `main`(`3baeb05`). `dev`(`de3fafc`)는 `feature/async-file-sink`(`f95dcc2`)의 조상 — 비동기 FileSink 가 `dev` 에 없음. `main` 에만 있는 콘텐츠 커밋 6개(`8798c94` .meta, `902843f`·`ad5d5eb`·`5ac6ef1`·`61e5420` README, `4cee0ee` HideInCallstack+0.2.0). `Tests/` 최신본은 feature 브랜치에만. `.worktrees/async-file-sink` prunable, `.worktrees/implement-v0.1.0` 빈 폴더. 미추적: `.agents/`, `.codex/`, `Documentation~/Plans/2026-09-22-debug-log-replacement/` |
| 1 에디터 가시성 | `[ ]` | 미착수. `TraceForgeLogViewerWindow.SetRingBuffer()` 호출처 없음, `TraceForgeMenuItems.cs:19` 에 `Debug.Log` 1건 |
| 2 부트스트랩·설정 | `[ ]` | 미착수. `TraceForgeSettings`·`Bootstrap` 없음, `SettingsProvider` 는 `EditorPrefs` |
| 3 패리티 | `[ ]` | 미착수. `LogEntry` 5필드, `Categories.Unity` 없음, 카테고리 필터가 전역 우선 |
| 4 성능 계약 | `[ ]` | 미착수. 기준 벤치마크는 `Documentation~/Benchmarks/2026-07-22-file-sink-performance.md` |
| 5 마이그레이션·릴리스 | `[ ]` | 미착수. `package.json` repository 가 `makeitliveforever`(오답, 정답은 `shinjh0380`), CHANGELOG 최신 0.1.0 vs package 0.2.0, CI 없음 |

## 0. 매 세션 시작 전

1. 프로젝트 루트(`C:\Projects\Other\trace-forge`)에서 **trusted** 상태로 새 Codex 태스크를 엽니다.
2. 주 모델을 **GPT-6 Astra** 로 선택합니다. 스킬은 주 모델을 바꿀 수 없으므로 직접 골라야 합니다.
3. 프롬프트 블록을 그대로 붙여넣습니다.

세션 1 전에 한 번만:

- [ ] `C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe` 가 존재. (없으면 설치된 6000.3.x 경로를 세션 1 프롬프트의 경로에 바꿔 넣으세요 — 이후 세션은 세션 1 이 만든 스크립트를 재사용합니다.)
- [ ] `git status` 에서 추적 파일 변경이 없음(미추적 `.agents/`, `.codex/`, `Documentation~/Plans/2026-09-22-debug-log-replacement/` 는 세션 1 이 커밋합니다).
- [ ] Unity Editor 가 이 저장소를 열고 있지 않음(batchmode 와 라이선스 충돌).
- [ ] 세션 6 전까지: GitHub 저장소 `shinjh0380/trace-forge` 의 Settings → Secrets 에 `UNITY_LICENSE`(.ulf 내용), `UNITY_EMAIL`, `UNITY_PASSWORD` 등록. Phase 5 문서 "CI Workflow" 참고. Codex 가 대신 할 수 없습니다.

---

## 세션 1 — 브랜치 수렴 (Phase 0)

작은 세션입니다. 위임 없이 주 에이전트가 직접 합니다. 산출물: 모든 코드가 들어 있는 `dev`, 작업 브랜치 `feature/native-logger`, Unity 테스트 호스트, 통과하는 회귀 기준선.

```
$astra-orchestrator 브랜치 정리 작업이다 — 위임하지 말고 직접 해라. 지도(Documentation~/Plans/2026-09-22-debug-log-replacement/00-orchestration.md)의 "Phase 0: Branch Convergence" 전부.

[먼저 읽을 것]
Documentation~/Plans/2026-09-22-debug-log-replacement/00-orchestration.md 전체 — 특히 "Current State Diagnosis › Branches", "Phase 0", "Project Conventions".
git show dev:CLAUDE.md — 브랜칭 전략(dev 작업, main 배포 전용, dev→main 단방향)과 코딩 컨벤션.
Documentation~/Plans/2026-07-22-async-file-logging.md Task 1 (Step 1~3) — Unity 테스트 호스트 생성과 batchmode 테스트 실행 명령. 경로만 바꿔 재사용한다.

[저장소 현재 상태 — 먼저 git branch -a, git log --oneline -20 --graph --all, git status 로 확인하고 시작해라]
- 루트 체크아웃은 main(3baeb05). dev(de3fafc)는 feature/async-file-sink(f95dcc2)의 조상이다.
- main 에만 있는 콘텐츠 커밋: 8798c94(.meta + .gitattributes), 902843f, ad5d5eb, 5ac6ef1, 61e5420(README), 4cee0ee([HideInCallstack], 0.2.0).
- Tests/ 는 feature/async-file-sink 에만 최신본이 있다. main 은 배포 브랜치라 Tests/ 를 .gitignore 한다.
- 미추적: .agents/, .codex/, AGENTS.md(Codex 툴링), Documentation~/Plans/2026-09-22-debug-log-replacement/(계획 7개).
- main 자체의 .meta 결함 2건: Runtime/Sinks/UnityConsoleSink.cs.meta 가 소스 없이 남아 있고(고아), Editor/AssemblyInfo.cs.meta 가 없다. dev 에서 고치고 main 은 세션 7 release-merge 로 받는다.
- .worktrees/async-file-sink 는 살아 있는 깨끗한 worktree 다(prunable 아님). 브랜치 삭제 전에 worktree remove 가 필요하다.

[범위 — 순서대로]
1. git checkout dev && git merge --ff-only feature/async-file-sink. ff 가 안 되면 멈추고 보고.
2. main 전용 콘텐츠 커밋을 오래된 것부터 cherry-pick: 8798c94 → 902843f → ad5d5eb → 4cee0ee → 5ac6ef1 → 61e5420.
   충돌은 dev 의 Tests/ 와 .claude/·CLAUDE.md 를 유지하는 쪽으로. .gitignore 충돌은 dev 판(Tests/ 를 ignore 하지 않는 판)을 유지.
   Tests/ 아래에도 .meta 가 필요하면(PackageMetaValidationTests 가 요구하는지 읽어서 판단) 해당 테스트가 통과하도록 추가해라.
3. .meta 결함 수정(dev 에서만): Runtime/Sinks/UnityConsoleSink.cs.meta 삭제, Editor/AssemblyInfo.cs.meta 를 다른 스크립트 meta 와 같은 형식·새 GUID 로 추가. 커밋 "chore: fix orphan and missing script meta files".
   지도 "Current State Diagnosis › Branches" 에 이 두 건을 main 의 알려진 결함(0.3.0 release-merge 로 해소)으로 기록.
4. 검증: git diff --stat dev main -- . ':!Tests' ':!Tests.meta' ':!.claude' ':!CLAUDE.md' ':!.agents' ':!.codex' ':!AGENTS.md' ':!.gitignore' ':!Documentation~/Plans/2026-09-22-debug-log-replacement'
   출력이 정확히 3 의 .meta 2건뿐이어야 한다. 다른 항목이 있으면 실제 divergence 다 — 보고하고 멈춰라.
5. Codex 툴링과 계획 문서를 dev 에 커밋: .agents/, .codex/, AGENTS.md, Documentation~/Plans/2026-09-22-debug-log-replacement/(이 세션에서 고친 00-orchestration.md 포함).
   .claude/skills/traceforge-release-merge/SKILL.md 의 "Claude 툴링 파일" 제외 목록에 .agents/, .codex/, AGENTS.md 를 추가하고, CLAUDE.md 브랜칭 전략 절에도 한 줄 추가해라(main 의 .gitignore 갱신은 세션 7 의 release-merge 때 같은 절차로 처리한다 — 지금 main 을 건드리지 마).
6. git worktree remove .worktrees/async-file-sink (깨끗하므로 --force 불필요) → git worktree prune → git branch -d feature/async-file-sink (원격 브랜치는 그대로 둔다). .worktrees/implement-v0.1.0 빈 폴더는 삭제.
7. git checkout -b feature/native-logger (dev 기반, 루트 체크아웃에서. 별도 worktree 를 만들지 마 — 테스트 호스트가 repo 루트를 로컬 패키지로 참조한다).
8. Unity 테스트 호스트: 선행 계획 Task 1 Step 1~2 의 절차로 저장소 내부 C:\Projects\Other\trace-forge\.worktrees\native-logger-test-host 를 만들고 Packages/manifest.json 의 의존성을
   "com.skrawberries.traceforge": "file:C:/Projects/Other/trace-forge", "com.unity.test-framework": "1.6.0", testables 에 패키지 이름. Unity 경로는 C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe.
   같은 폴더에 run-tests.ps1 을 만들어라: 인자 -Platform (EditMode|PlayMode) -Out <이름> 으로 -runTests 를 돌리고 결과 xml 의 total/passed/failed 를 한 줄로 출력. 이후 모든 세션이 이 스크립트를 쓴다. 테스트 호스트 폴더는 .worktrees/ 아래라 커밋되지 않는다.
9. run-tests.ps1 -Platform EditMode 와 -Platform PlayMode 를 각각 실행. 전부 통과해야 한다. 통과 수를 지도의 Phase 0 Step 마지막 줄 옆에 "(EditMode N, PlayMode M, 2026-MM-DD)" 로 기록해라 — 이게 이후 세션의 회귀 기준선이다.
   PackageMetaValidationTests 는 첫 실행에 통과해야 한다. 3 의 두 파일 외의 이유로 실패하면 보고하고 멈춰라 — 테스트를 고쳐서 초록으로 만들지 마.

[통과 기준]
- git branch --show-current 가 feature/native-logger. git status 깨끗함(AGENTS.md 포함 미추적 0건).
- git log --oneline dev -3 이 cherry-pick·meta 수정·툴링 커밋을 포함. git merge-base --is-ancestor dev feature/native-logger 참.
- 위 4 의 diff 출력이 .meta 2건뿐임.
- run-tests.ps1 두 플랫폼 전부 통과, 통과 수가 지도에 기록됨.
- 지도 Phase 0 Steps 전부 [x].

[진행 방식]
dev 커밋: cherry-pick 은 원 메시지 유지. 추가 커밋 3개 — "chore: fix orphan and missing script meta files", "chore: track Codex tooling and native-logger plan on dev", "chore: exclude Codex tooling from release merge".
feature/native-logger 에는 Phase 0 체크박스 갱신 커밋 1개 — "docs: record Phase 0 baseline".
스펙(계획 문서)이 틀렸다고 판단되면 임의로 바꾸지 말고 보고해라.

[정지]
Phase 0 까지만. Phase 1 은 다음 세션이다. Runtime/·Editor/ 는 3 의 .meta 2건 외에 건드리지 마.
```

**끝나고 확인** — `git log --oneline --graph dev feature/native-logger -15` 로 dev 가 main 의 코드를 다 담았는지. Unity Hub 로 `.worktrees\native-logger-test-host` 를 열어(6000.3.8f1) Test Runner 창에서 TraceForge 테스트가 보이고 초록인지 한 번 눈으로 확인. 열었던 Unity 는 닫으세요(다음 세션 batchmode 충돌).

---

## 세션 2 — 에디터 가시성 (Phase 1)

```
$astra-orchestrator Phase 1 문서(Documentation~/Plans/2026-09-22-debug-log-replacement/01-editor-visibility.md)를 구현해줘. Luna 워커 1개, 독립 Astra 리뷰.

[먼저 읽을 것]
지도 00-orchestration.md — "Gap Analysis" G1, "Decisions" D9, "Project Conventions".
Phase 1 문서 전체 — Files, Design Notes, Steps, Tests, Completion Criteria.
Runtime/Logger.cs, Runtime/AssemblyInfo.cs, Runtime/Sinks/RingBufferSink.cs, Editor/TraceForgeLogViewerWindow.cs, Editor/TraceForgeMenuItems.cs, Tests/Runtime/SinkDispatchTests.cs(소스 스캔 테스트가 있는 파일).
.claude/skills/traceforge-api-consistency/api-guidelines.md — 퍼블릭 API 검토 23항목(리뷰어 참고용).

[먼저 확인]
git branch --show-current 가 feature/native-logger, git status 깨끗함, 지도 Phase 0 Steps 전부 [x] 이고 기준선 통과 수가 적혀 있음. 아니면 멈추고 보고.

[워커 계약]
Owned: Runtime/SinkRegistry.cs(신규), Runtime/Logger.cs, Runtime/AssemblyInfo.cs, Editor/TraceForgeLogViewerWindow.cs, Editor/TraceForgeMenuItems.cs, Tests/Editor/LogViewerWindowTests.cs(신규), Tests/Runtime/SinkDispatchTests.cs(소스 스캔 확장만), 새 파일의 .meta.
Acceptance: Phase 1 문서 "Tests" 표 5행 전부와 "Completion Criteria".
제약:
  - SinkRegistry 는 internal. Logger 가 _sinksLock 아래에서 단일 writer. Changed 이벤트는 lock 밖에서 발생.
  - Viewer 는 registry 를 읽기만 한다. 기존 0.25s 폴링 유지.
  - MenuItems 의 Debug.Log 대체는 TF 를 경유하지 않는다(Reset 직후라 sink 가 없다) — ShowNotification 또는 DisplayDialog.
  - 퍼블릭 API 변경 없음. Runtime asmdef 에 참조 추가 금지.
  - 테스트 우선: 새 테스트가 먼저 빨간 뒤 초록.

[통과 기준 — 전부 실제로 실행]
- .worktrees\native-logger-test-host\run-tests.ps1 -Platform EditMode / PlayMode 전부 통과, 통과 수 ≥ 기준선 + 신규 테스트 수.
- rg -n "Debug\.Log" Runtime Editor Samples~ → 0건.
- 테스트 호스트에 Samples~/BasicUsage 를 복사해 넣고 PlayMode 테스트 하나로 "샘플 Awake 실행 후 SinkRegistry.RingBuffers.Length == 1" 을 단정(호스트 전용 테스트, 커밋하지 않음).
- Phase 1 Steps 전부 [x].

[진행 방식]
워커 → 주 에이전트가 diff 와 위 명령을 직접 실행 → 독립 Astra 리뷰(registry 와 Logger 의 잠금 일관성, Viewer 의 stale 참조 해제, api-guidelines 23항목) → 커밋 1개 "feat(editor): wire Log Viewer to RingBufferSink registry (Phase 1)".
Owned 밖 파일이 필요하면 워커가 주 에이전트에게 돌려보내게 해라.

[정지]
Phase 1 까지만. Phase 2 는 시작하지 마. dev 에 머지하지 마 — 머지는 세션 7 이다.
```

**끝나고 확인** — 테스트 호스트를 Unity 로 열고 샘플 씬(빈 씬 + `TraceForgeBasicUsage` 컴포넌트)에서 Play → **Window > TraceForge > Log Viewer** 에 항목이 뜨는지. Play 를 껐다 켜도 새 항목이 보이는지. **Window > TraceForge > Reset Logger** 가 Console 에 아무것도 남기지 않는지. Unity 닫기.

---

## 세션 3 — 부트스트랩 ∥ 에디터 설정 (Phase 2)

가장 큰 세션입니다. Runtime 과 Editor 의 Owned 파일이 안 겹치므로 Luna 워커 2개를 동시에 띄웁니다. 순서 의존이 하나 있습니다(Editor 가 `TraceForgeSettings` 타입을 필요로 함).

```
$astra-orchestrator Phase 2 문서(Documentation~/Plans/2026-09-22-debug-log-replacement/02-bootstrap-settings-lifecycle.md)를 Luna 워커 2개로 동시에 구현해줘. 독립 Astra 리뷰.

[먼저 읽을 것]
지도 — G2·G3·G7, D2·D3·D4·D9·D10, "Resolved Questions" #2, "Risks".
Phase 2 문서 전체 — Settings Schema, Bootstrap Behavior(초기화 순서), Editor Bootstrap, Tests.
Runtime/Logger.cs(Reset 의 SubsystemRegistration), Runtime/SinkRegistry.cs(세션 2 산출), Runtime/Sinks/FileSink.cs(IDisposable 계약), Editor/TraceForgeSettingsProvider.cs, Samples~/BasicUsage/*, README.md, Documentation~/TraceForge.md.
Documentation~/Designs/2026-07-22-async-file-logging-design.md "생명주기와 소유권" — 유지해야 할 소유권 원칙.

[먼저 확인]
feature/native-logger, git status 깨끗, Phase 1 Steps 전부 [x], 세션 2 커밋 존재. 아니면 멈춤.

[순서 의존 — 딱 하나]
워커 B(Editor)는 워커 A 의 TraceForgeSettings 와 StackTracePolicy 타입을 컴파일에 필요로 한다. 워커 A 에게 Runtime/StackTracePolicy.cs 와 Runtime/TraceForgeSettings.cs(Phase 2 문서 Settings Schema 그대로)를 **가장 먼저** 만들고 메시지로 알리게 해라. 워커 B 는 그동안 SettingsProvider 의 EditorPrefs 제거·SerializedObject 편집 골격과 BuildProcessor 의 Preloaded Assets 유틸(에셋 타입에 독립적인 부분)을 먼저 한다.

[Task A — Runtime 부트스트랩]  워커 A
Owned: Runtime/StackTracePolicy.cs, Runtime/TraceForgeSettings.cs, Runtime/Bootstrap.cs(신규), Runtime/Logger.cs, Runtime/LoggerConfig.cs(삭제 또는 재정의 — 사용처를 rg 로 확인해 결정하고 보고), Tests/Runtime/BootstrapTests.cs(신규), 관련 .meta.
Acceptance: Phase 2 "Tests" 표의 Runtime 행 5개(설정 없음 기본값, 설정 있음 적용, quitting 시 Flush+Dispose, Play Mode 3회 반복 시 핸들 누수 없음, TF.Reset 이 사용자 sink 를 dispose 하지 않음).
제약:
  - 초기화 순서: Logger.Reset(SubsystemRegistration) → Bootstrap.Initialize(AfterAssembliesLoaded). Reset 이 Bootstrap.Shutdown() 을 먼저 부른다(D10).
  - 부트스트랩 소유 sink 만 dispose(D9). 사용자 sink 는 절대 dispose 하지 않는다.
  - 릴리스 빌드 기본 sink 없음(D4). Editor/Development 판정은 UNITY_EDITOR || DEVELOPMENT_BUILD.
  - 설정 로드: Preloaded Assets(Player) → 없으면 기본값. Editor 에서 ProjectSettings 에셋을 읽는 코드는 Editor 어셈블리(워커 B)가 제공한다 — Runtime 은 internal static Func<TraceForgeSettings> 훅 하나만 노출.
  - hot path(Logger.Write, IsEnabled) 변경 금지. 부트스트랩은 초기화 시점에만 실행.
  - CaptureUnityLog·StackTracePolicy 필드는 선언만, 동작은 Phase 3.

[Task B — Editor 설정·빌드·Edit Mode]  워커 B
Owned: Editor/TraceForgeSettingsProvider.cs, Editor/TraceForgeBuildProcessor.cs(신규), Editor/TraceForgeEditorBootstrap.cs(신규), Tests/Editor/SettingsAssetTests.cs(신규), Tests/Editor/BuildProcessorTests.cs(신규), Tests/Editor/SettingsProviderTests.cs(기존 — EditorPrefs 단정 제거), Samples~/BasicUsage/TraceForgeBasicUsage.cs, Samples~/BasicUsage/README.md, README.md, Documentation~/TraceForge.md, 관련 .meta.
Acceptance: Phase 2 "Tests" 표의 Editor 행 2개(빌드 전후 Preloaded Assets 추가·원복, 이미 있으면 불변; Provider 편집이 에셋과 Logger 에 즉시 반영) + Edit Mode 부트스트랩이 registry 가 비어 있을 때만 RingBufferSink 를 만든다는 테스트.
제약:
  - 에셋 경로 ProjectSettings/TraceForgeSettings.asset, 첫 열기에 생성. EditorPrefs 참조 0건.
  - Preloaded Assets 는 IPreprocessBuildWithReport 에서 추가, IPostprocessBuildWithReport 에서 이전 목록으로 원복. 빌드 실패 경로에서도 원복되게 try/finally 또는 도메인 재로드 안전한 상태 저장.
  - Edit Mode sink 는 AssemblyReloadEvents.beforeAssemblyReload 에서 해제.
  - 샘플: 수동 RingBuffer 등록 제거, "부트스트랩 기본값 + FileSink 추가 예시" 로 재구성. 문서는 Phase 2 문서 Files 표대로.

[통과 기준 — 전부 실제로 실행]
- run-tests.ps1 EditMode / PlayMode 전부 통과, 통과 수 ≥ 세션 2 결과 + 신규.
- 테스트 호스트에서 Unity.exe -batchmode -quit -executeMethod 로 Windows 개발 빌드 1회(작은 빈 씬 + 샘플). 빌드 로그에 오류 없음, 빌드 후 ProjectSettings 의 Preloaded Assets 가 빌드 전과 같음.
- 빌드 산출물을 실행하고 종료(-batchmode -nographics 로 5초 후 Application.Quit 하는 호스트 전용 씬) → persistentDataPath 의 traceforge.log 에 마지막 항목이 있음(EnableFileSink=true 로 설정한 에셋으로 빌드).
- rg -n "EditorPrefs" Editor → 0건. rg -n "Debug\.Log" Runtime Editor Samples~ → 0건.
- Documentation~/DeferredIssues.md 이슈 3·5 를 "해결됨 (커밋 해시)" 로 갱신.
- Phase 2 Steps 전부 [x].

[진행 방식]
워커 2개 동시 → 각자 검증 → 주 에이전트가 diff 와 위 명령 직접 실행 → 독립 Astra 리뷰(A: Reset/Initialize 순서와 소유권, hot path 무변경; B: Preloaded Assets 원복 경로, Edit Mode 재로드 누수) → 커밋 2개: "feat(runtime): settings asset and bootstrap with owned sinks (Phase 2)", "feat(editor): project settings asset, build injection, edit-mode bootstrap (Phase 2)".
.claude/skills/traceforge-api-consistency/api-guidelines.md 와 traceforge-performance-review/performance-checklist.md 를 리뷰어 입력에 포함해라(새 퍼블릭 타입 2개).

[정지]
Phase 2 까지만. Phase 3 은 시작하지 마. 빌드 산출물과 호스트 전용 씬은 .worktrees/ 아래에 두고 커밋하지 마.
```

**끝나고 확인** — 새 빈 Unity 6000.3 프로젝트를 만들어 `Packages/manifest.json` 에 `"com.skrawberries.traceforge": "file:C:/Projects/Other/trace-forge"` 만 추가. 코드 한 줄 없이 아무 MonoBehaviour 에서 `TF.Info("hello")` → Play → Log Viewer 에 보이는지. **Project Settings > TraceForge** 에서 Min Verbosity 를 Error 로 바꾸면 즉시 Info 가 안 보이는지. 이게 Phase 2 의 완료 조건 그 자체입니다.

---

## 세션 4 — Debug.Log 패리티 (Phase 3)

Runtime 파일이 서로 얽혀 있어 워커 1개로 두 단계(3a → 3b) 순차 진행합니다.

```
$astra-orchestrator Phase 3 문서(Documentation~/Plans/2026-09-22-debug-log-replacement/03-debug-log-parity.md)를 구현해줘. Luna 워커 1개, 3a 커밋 후 3b. 독립 Astra 리뷰 1회(3b 뒤).

[먼저 읽을 것]
지도 — G4·G5·G6·G8, D1·D5(Log → Debug)·D6·D7, "Risks"(LogEntry 크기, logMessageReceivedThreaded 스레드).
Phase 3 문서 전체 — Design Notes 의 UnityLogCapture 코드 스케치, 컨텍스트 오버로드 목록, IsEnabled 새 구현, Tests 표 13행.
Runtime/Logger.cs, Runtime/TF.cs, Runtime/LogEntry.cs, Runtime/Categories.cs, Runtime/Bootstrap.cs, Runtime/Sinks/FileSink.cs(WriteEntry), Editor/TraceForgeLogViewerWindow.cs, Tests/Runtime/CategoryFilteringTests.cs, LogEntryTests.cs, FileSinkTests.cs(할당 회귀 테스트).
.claude/skills/traceforge-api-consistency/api-guidelines.md — 특히 #12(Exception 은 마지막), #14(오버로드 3개 이상), #19(context 는 마지막 optional).

[먼저 확인]
feature/native-logger, git status 깨끗, Phase 2 Steps 전부 [x]. 아니면 멈춤.

[워커 계약]
Owned: Runtime/Logger.cs, Runtime/TF.cs, Runtime/LogEntry.cs, Runtime/Categories.cs, Runtime/UnityLogCapture.cs(신규), Runtime/Bootstrap.cs, Runtime/Sinks/FileSink.cs, Editor/TraceForgeLogViewerWindow.cs, Tests/Runtime/{CategoryFilteringTests,LogEntryTests,FileSinkTests,UnityLogCaptureTests,StackTraceTests}.cs, Documentation~/TraceForge.md, README.md, CHANGELOG.md(Unreleased 에 Breaking 항목), 관련 .meta.

[3a — 필터 의미·LogEntry·스택 정책]  커밋 "feat(runtime): category override, log entry context and stack trace policy (Phase 3a)"
  - Logger.IsEnabled(verbosity, category) 를 Phase 3 문서의 구현으로(카테고리 값이 있으면 전역 무시). IsEnabled(verbosity) 는 전역만. CHANGELOG Unreleased 에 Breaking.
  - LogEntry 에 int ContextInstanceId, string StackTrace 추가. 기존 5인자 생성자 유지 + 확장 생성자.
  - StackTracePolicy 를 Logger 의 static int 로 보관(Bootstrap 이 설정에서 주입). ErrorAndAbove/All 에서 Environment.StackTrace 캡처 후 TraceForge.* 선두 프레임 제거. None 이면 캡처 코드에 진입하지 않는다.
  - Categories.Unity 추가.
  - 테스트: 양방향 override 2건, ClearCategoryVerbosity 복귀, 정책별 StackTrace null/비null, 첫 프레임이 테스트 메서드, None 에서 producer 할당 0B(기존 할당 회귀 테스트 확장).

[3b — 캡처·컨텍스트·출력]  커밋 "feat(runtime): capture Unity log, context overloads, stack output (Phase 3b)"
  - UnityLogCapture: Application.logMessageReceivedThreaded 구독. 매핑 Exception→Fatal, Error/Assert→Error, Warning→Warning, Log→Debug. [ThreadStatic] 재진입 가드. Logger.WriteCaptured 로 Unity 가 준 스택 문자열을 그대로 저장(재캡처 금지).
  - Bootstrap: settings.CaptureUnityLog 면 Start, Shutdown 에서 Stop.
  - TF 오버로드: Log(category, verbosity, message, UnityEngine.Object context), Warning/Error/Fatal(message, context), (category, message, context), Error(message, exception, context) 계열. context 는 항상 마지막. Trace/Debug/Info 에는 추가하지 마.
  - FileSink: StackTrace 있으면 항목 아래 들여쓰기 출력. 스택 없는 항목의 바이트 출력은 Phase 2 와 동일해야 한다(테스트로 단정).
  - Viewer: 항목 클릭 → InstanceIDToObject → PingObject(null 이면 무시). 스택 foldout.
  - 테스트: Phase 3 문서 Tests 표의 나머지 행 전부. Debug.Log* 는 테스트 내부에서만, 반드시 LogAssert.Expect 로 감싼다. 소스 스캔 테스트가 Tests/ 를 제외하는지 확인.

[통과 기준 — 전부 실제로 실행]
- run-tests.ps1 EditMode / PlayMode 전부 통과, 통과 수 ≥ 세션 3 결과 + 신규(≥ 13).
- 할당 회귀 테스트가 StackTracePolicy.None + 캡처 켜짐 상태에서 0B.
- 테스트 호스트 PlayMode 에서 서드파티 흉내 스크립트가 던진 미처리 예외가 traceforge.log 에 스택과 함께 남는다(호스트 전용 테스트).
- rg -n "Debug\.Log" Runtime Editor Samples~ → 0건(Tests 제외).
- Documentation~/DeferredIssues.md 이슈 2 해결됨으로 갱신. Phase 3 Steps 전부 [x].

[진행 방식]
3a 검증·커밋 → 3b 검증·커밋 → 독립 Astra 리뷰(재진입 가드, 스레드 안전성, context 참조 미보관, 오버로드가 api-guidelines #12·#14·#19 를 지키는지, FileSink 출력 하위 호환) → fix 면 1회 수리 후 재리뷰.
PR 설명(커밋 본문)에 "Phase 4 에서 측정할 의심 지점" 을 적어라 — Write 의 정책 읽기, LogEntry 복사 비용.

[정지]
Phase 3 까지만. 성능 튜닝은 하지 마 — Phase 4 다.
```

**끝나고 확인** — 세션 3 의 빈 프로젝트에서 아무 스크립트에 `throw new System.Exception("boom")` 을 넣고 Play → Log Viewer 에 `[Fatal] [Unity]` 항목이 뜨고 펼치면 스택이 보이는지. `TF.Error("x", this.gameObject)` 항목을 클릭하면 Hierarchy 에서 오브젝트가 핑 되는지. Project Settings 에서 Network 카테고리를 Trace 로 두고 전역을 Info 로 두면 `TF.Trace(Categories.Network, …)` 가 보이는지(D1).

---

## 세션 5 — 성능 계약 (Phase 4)

측정 세션입니다. 코드 변경은 작고 벤치마크 실행이 대부분이라 주 에이전트가 Unity 를 직접 돌립니다.

```
$astra-orchestrator Phase 4 문서(Documentation~/Plans/2026-09-22-debug-log-replacement/04-performance-contract.md)를 진행해줘. Luna 워커 1개(IsEnabled 변경 + 테스트 + 문서), 벤치마크 실행은 주 에이전트가 직접. 독립 Astra 리뷰.

[먼저 읽을 것]
지도 — G9, D8, "Risks"(IsEnabled 의미 변경이 기존 테스트를 깨뜨림).
Phase 4 문서 전체 — "What Is Currently Overstated" 표, Strip-Symbol Test Strategy(옵션 2 + CI 옵션 1), Benchmark Protocol(구성 1~4, 5% 기준).
Documentation~/Benchmarks/2026-07-22-file-sink-performance.md — 측정 방법·환경·원시 결과 표 형식. 새 문서는 이 형식을 그대로 따른다.
Documentation~/Plans/2026-07-22-async-file-logging.md Task 5 — 벤치마크 테스트 코드와 5회 반복 PowerShell.
.claude/skills/traceforge-performance-review/performance-checklist.md.
세션 4 커밋 본문의 "Phase 4 에서 측정할 의심 지점".

[먼저 확인]
feature/native-logger, git status 깨끗, Phase 3 Steps 전부 [x]. 아니면 멈춤.

[워커 계약]
Owned: Runtime/TF.cs(IsEnabled 만), Runtime/Logger.cs(IsEnabled 만), Tests/Runtime/VerbosityFilteringTests.cs, Tests/Runtime/CategoryFilteringTests.cs(sink 없는 setup 수정), README.md, Documentation~/TraceForge.md, Documentation~/DeferredIssues.md.
Acceptance:
  - TF.IsEnabled(Trace/Debug) 가 TRACEFORGE_STRIP_TRACE/DEBUG 아래에서 false. TRACEFORGE_DISABLE 은 기존대로.
  - Logger.IsEnabled 가 등록 sink 0개면 false. 배열 길이 읽기 외 비용 추가 금지.
  - 스트립 심볼 검증은 Phase 4 문서 옵션 2(internal static readonly bool IsTraceStripped/IsDebugStripped 상수와 자기일관성 단정). 옵션 1 의 CI 잡은 세션 6.
  - "zero sinks → false" 로 깨지는 기존 테스트 목록을 먼저 뽑아 커밋 본문에 적고 일괄 수정.
  - README Features·Compile-Time Stripping 과 TraceForge.md Performance Notes 를 Phase 4 문서 표의 "Fix" 열대로 다시 쓴다. IsEnabled guard 를 권장 관용구로.
  - DeferredIssues.md: 이슈 4 해결됨, "lazy formatting overloads" 항목 신규 추가(Phase 4 문서 Deferred 절 링크).

[벤치마크 — 주 에이전트 직접]
Phase 4 문서 Benchmark Protocol 그대로. 구성 1(f95dcc2 기준 재현) → 2 → 3 → 4, 각 5회 독립 Unity 프로세스.
구성 1 은 git worktree add .worktrees/bench-baseline f95dcc2 로 별도 체크아웃하고 테스트 호스트 manifest 를 임시로 그 경로로 바꿔 측정한 뒤 원복. 2026-07-22 문서의 중앙값과 10% 이내면 rig 유효.
결과를 Documentation~/Benchmarks/2026-09-XX-native-logger-overhead.md(실제 날짜)에 2026-07-22 문서와 같은 절 구조로 기록: 환경·커밋·측정 방법·5회 원시 결과·중앙값 비교·판정.
구성 2~4 중앙값이 구성 1 대비 5% 초과 회귀면 Logger.Write 를 프로파일(정책 읽기를 static int 로, LogEntry in 전달 확인) → 수정은 워커에게 → 재측정. 수정 없이 기준을 넘기지 못하면 값을 그대로 기록하고 보고.

[통과 기준 — 전부 실제로 실행]
- run-tests.ps1 EditMode / PlayMode 전부 통과.
- 벤치마크 문서 존재, 구성 4개 × 5회, 판정 기록. producer 할당 0, 파일 line 수 100,000.
- 테스트 호스트에 TRACEFORGE_STRIP_TRACE;TRACEFORGE_STRIP_DEBUG 를 Player scripting defines 로 넣고 EditMode 1회 → 스트립 자기일관성 테스트가 "stripped" 경로로 통과. 실행 후 defines 원복.
- README 에 테스트나 벤치마크로 뒷받침되지 않는 성능 문구가 없다 — 리뷰어가 문장 단위로 대조.
- Phase 4 Steps 전부 [x].

[진행 방식]
워커 → 검증 → 벤치마크 → 독립 Astra 리뷰(performance-checklist 전 항목, README 문구 대조) → 커밋 2개: "fix(runtime): IsEnabled reflects stripping and sink presence (Phase 4)", "docs: native logger overhead benchmark and honest performance contract (Phase 4)".
.worktrees/bench-baseline 은 측정 후 git worktree remove.

[정지]
Phase 4 까지만. Phase 5 는 다음 세션이다.
```

**끝나고 확인** — 벤치마크 문서의 중앙값 표를 보고 구성 2~4 가 구성 1 의 5% 이내인지. README 의 Features 절을 읽고 "Zero-allocation" 문구가 어떻게 바뀌었는지 납득되는지 — 이 문구가 패키지의 공개 약속입니다.

---

## 세션 6 — 마이그레이션·메타데이터·CI (Phase 5 전반)

### 사전 준비 (사람)

- [ ] Unity Hub 로 Personal 라이선스를 활성화한 `.ulf` 파일 확보(Windows: `C:\ProgramData\Unity\Unity_lic.ulf`). 내용을 GitHub 저장소 `shinjh0380/trace-forge` Secrets 에 `UNITY_LICENSE` 로, 계정 이메일·비밀번호를 `UNITY_EMAIL`·`UNITY_PASSWORD` 로 등록. Pro 라이선스면 대신 `UNITY_SERIAL`.
- [ ] https://hub.docker.com/r/unityci/editor/tags 에서 `6000.0.x` 와 `6000.3.x` 의 `ubuntu-…-base-3` 태그가 존재하는 패치 버전 2개를 골라 적어 두기 — 프롬프트의 `<6000.0.x>`·`<6000.3.x>` 자리에 넣습니다.

```
$astra-orchestrator Phase 5 문서(Documentation~/Plans/2026-09-22-debug-log-replacement/05-migration-and-release.md)의 릴리스 전 항목 — Migration.md, (선택) 스캐너, CI 워크플로, 메타데이터 정리, CHANGELOG — 를 구현해줘. Luna 워커 1개, 독립 Astra 리뷰. 릴리스(version-bump, release-merge, tag)는 하지 마 — 다음 세션이다.

[먼저 읽을 것]
지도 — G10, "Resolved Questions" #1(shinjh0380 canonical)·#4(CI), "Project Conventions".
Phase 5 문서 전체 — Migration Guide Content(대응표·절 7개), CI Workflow(yaml 초안과 "Checks before enabling" 3개), Metadata Cleanup 표.
package.json, README.md, CHANGELOG.md, Tests/Editor/PackageMetaValidationTests.cs, Documentation~/DeferredIssues.md.
.claude/skills/traceforge-package-audit/checklist.md, traceforge-release-check/release-checklist.md — 이 세션 끝에 항목별로 대조한다.
Phase 4 문서 Strip-Symbol Test Strategy 옵션 1 — CI 의 세 번째 매트릭스 잡.

[먼저 확인]
feature/native-logger, git status 깨끗, Phase 4 Steps 전부 [x]. GitHub Secrets 3개는 사용자가 등록했다고 가정하고, 없으면 워크플로 실행 단계에서 보고.

[워커 계약]
Owned: Documentation~/Migration.md(신규), .github/workflows/test.yml(신규), package.json, README.md, CHANGELOG.md, Tests/Editor/PackageMetaValidationTests.cs, Documentation~/DeferredIssues.md, (선택) Editor/TraceForgeMigrationScanner.cs + Tests/Editor/MigrationScannerTests.cs, 관련 .meta.
Acceptance:
  1. Migration.md — Phase 5 문서의 대응표 11행을 그대로, 절 7개 전부. 코드 예시는 실제 API 시그니처와 일치해야 한다(rg 로 대조).
  2. package.json — documentationUrl/changelogUrl/licensesUrl/repository.url 을 https://github.com/shinjh0380/trace-forge 계열로. author 는 기존 값 유지하되 url 만 shinjh0380. version 은 이번 세션에 올리지 마(0.2.0 유지 — version-bump 스킬이 세션 7 에서 올린다). description 의 "Unity 6.3 LTS" 를 unity 필드(6000.0)와 모순되지 않게.
  3. README — Requirements 를 "minimum 6000.0, tested on 6000.3", Migration.md 링크, 설치 URL 확인.
  4. CHANGELOG — [0.2.0] 항목 소급 추가(비동기 FileSink, UnityConsoleSink 제거, HideInCallstack), [Unreleased] 에 Phase 1~4 항목(Breaking: 카테고리 override, IsEnabled 의미). 버전 번호는 세션 7 이 확정.
  5. PackageMetaValidationTests — repository host/owner 가 shinjh0380, package version 이 CHANGELOG 최상단 릴리스 항목과 일치(Unreleased 제외), 문서 URL 3개가 repository 와 같은 owner.
  6. CI — Phase 5 문서의 yaml 을 기반으로. 매트릭스 3개: <6000.0.x> 기본, <6000.3.x> 기본, <6000.3.x> + TRACEFORGE_STRIP_TRACE;TRACEFORGE_STRIP_DEBUG. checkout path: package, packageMode: true. scripting defines 전달 방식은 game-ci/unity-test-runner@v4 의 README 에서 확인하고 안 되면 Phase 4 옵션 1(defineConstraints 테스트 asmdef)로 대체 — 어느 쪽을 택했는지 워크플로 주석에.
  7. (선택, 위 6개가 끝나고 여유 있을 때만) 마이그레이션 스캐너 — Phase 5 문서 사양대로. Assets/ 만, 주석 제외, 클릭 시 OpenAsset.

[통과 기준 — 전부 실제로 실행]
- run-tests.ps1 EditMode / PlayMode 전부 통과(PackageMetaValidationTests 신규 단정 포함).
- git push origin feature/native-logger 후 Actions 에서 test.yml 이 3개 잡 전부 초록. 라이선스 활성화 실패면 로그를 그대로 보고하고 멈춰라 — 시크릿은 사용자 몫.
- rg -n "makeitliveforever" . --glob '!.git/**' → 0건.
- traceforge-package-audit/checklist.md 와 traceforge-release-check/release-checklist.md 를 항목별로 대조해 결과를 커밋 본문에 표로. 실패 항목이 있으면 고치고, 고칠 수 없는 것은 이유와 함께 보고.
- Phase 5 Steps 중 릴리스 4개(release-check 실행·version-bump·release-merge·fresh install)를 제외한 전부 [x].

[진행 방식]
커밋 순서: "docs: add Debug.Log migration guide", "chore: unify package metadata to shinjh0380/trace-forge", "docs: backfill 0.2.0 changelog and unreleased entries", "ci: run Unity tests on dev via GameCI", (선택) "feat(editor): Debug.Log usage scanner".
독립 Astra 리뷰 — Migration.md 코드 예시의 API 일치, CI 워크플로의 Secrets 노출 여부(로그에 값이 찍히지 않는지), 메타데이터 테스트가 회귀를 실제로 잡는지.

[정지]
여기까지만. 버전을 올리거나 dev/main 에 머지하거나 태그를 만들지 마.
```

**끝나고 확인** — GitHub Actions 에서 3개 잡이 초록인지(첫 실행은 이미지 pull 로 15분 이상 걸릴 수 있음). `Documentation~/Migration.md` 를 열어 대응표의 `TF.Log(Categories.Default, Verbosity.Info, msg, ctx)` 같은 시그니처가 실제로 컴파일되는 것인지 한두 개 IDE 에서 확인.

---

## 세션 7 — 릴리스 v0.3.0 (Phase 5 후반)

작은 세션입니다. 위임 없이 주 에이전트가 직접 합니다. 프로젝트의 릴리스 스킬 절차를 그대로 따릅니다.

```
$astra-orchestrator 릴리스 작업이다 — 위임하지 말고 직접 해라. Phase 5 문서의 남은 Steps 4개: release-check → version-bump 0.3.0 → dev 머지 → release-merge(dev→main) → 태그 → fresh install 검수.

[먼저 읽을 것]
.claude/skills/traceforge-release-check/SKILL.md 와 release-checklist.md.
.claude/skills/traceforge-version-bump/SKILL.md.
.claude/skills/traceforge-release-merge/SKILL.md — 전체 절차. 세션 1 에서 .agents/·.codex/ 가 제외 목록에 추가됐는지 확인.
git show dev:CLAUDE.md 브랜칭 전략.
Phase 5 문서 "Completion Criteria".

[먼저 확인]
feature/native-logger, git status 깨끗, Phase 1~4 Steps 전부 [x], Phase 5 Steps 중 릴리스 4개만 남음, GitHub Actions 최근 실행이 초록. 하나라도 아니면 멈추고 보고.

[순서]
1. release-checklist.md 를 항목별로 실제 실행해 대조. run-tests.ps1 EditMode / PlayMode 전부 통과 재확인. 실패 항목이 있으면 멈추고 보고 — 이 세션에서 기능 수정은 하지 않는다.
2. git checkout dev && git merge --no-ff feature/native-logger -m "feat: replace Unity Debug.Log with native TraceForge logger (Phases 1-5)". 충돌 없어야 한다(dev 는 세션 1 이후 변경이 없다 — 있으면 보고).
3. dev 에서 traceforge-version-bump 절차로 0.3.0: package.json version, CHANGELOG [Unreleased] → [0.3.0] - <오늘 날짜>. PackageMetaValidationTests 가 통과하는지 run-tests.ps1 로 확인. 커밋 "chore: release 0.3.0".
4. traceforge-release-merge 절차로 dev → main. main 의 .gitignore 에 .agents/ 와 .codex/ 가 들어가도록 스킬 절차 안에서 처리. main 에 .claude/, CLAUDE.md, .agents/, .codex/, Tests/ 가 없어야 한다: git ls-tree -r --name-only main | rg "^(\.claude|\.agents|\.codex|Tests)/|^CLAUDE\.md" → 0건.
   Documentation~/Plans/ 와 Benchmarks/ 는 main 에 포함한다(~ 폴더라 Unity 가 무시하며, 기존 2026-07-22 문서도 main 에 있다).
5. git tag -a v0.3.0 -m "TraceForge 0.3.0" (main 의 머지 커밋에). 푸시: git push origin dev main v0.3.0.
6. fresh install 검수: C:\Projects\Other\trace-forge\.worktrees\fresh-install-check 에 새 Unity 프로젝트를 -createProject 로 만들고 manifest 에 "com.skrawberries.traceforge": "https://github.com/shinjh0380/trace-forge.git#v0.3.0" 만 추가. -batchmode 로 열어 컴파일 오류 0, 그리고 호스트 전용 PlayMode 테스트 하나로 "코드 설정 없이 TF.Info 가 SinkRegistry 의 RingBuffer 에 도달" 을 단정. 통과하면 폴더 삭제.
7. Phase 5 Steps 나머지 [x], 지도 상단 Status 를 "Completed 2026-MM-DD (v0.3.0)" 로. 이 커밋은 dev 에("docs: mark native logger plan complete") — main 에는 넣지 않는다(main 은 태그 시점 그대로).

[통과 기준]
- git describe --tags main 이 v0.3.0. main 에 툴링·테스트 파일 0건.
- fresh install 검수 통과.
- GitHub Actions 가 dev 푸시로 다시 초록.

[정지]
여기까지가 계획의 끝이다. 후속(lazy formatting overloads 등 DeferredIssues 잔여)은 새 계획 문서로 시작한다.
```

**끝나고 확인** — 다른 컴퓨터(또는 새 폴더)의 Unity 6000.3 프로젝트에서 Package Manager → Add package from git URL 에 `https://github.com/shinjh0380/trace-forge.git#v0.3.0` 을 넣어 설치되고, `TF.Info("hello")` 한 줄이 Log Viewer 에 보이는지. 이게 "Debug.Log 없이 로그가 보인다" 의 최종 실증입니다. 계획 완료입니다.

---

## 공통 규칙 (프롬프트에 이미 포함된 것들)

| 규칙 | 왜 |
|---|---|
| 각 Phase 문서의 `## Steps` 체크박스 = 진행 표. 완료 커밋에 `[x]` 포함 | 세션이 끊겨도 어디서 이어갈지 문서 한 곳에서 보임 |
| 지도의 D1~D10 과 Resolved Questions 가 결정. 문서끼리 어긋나면 지도가 이김 | 2026-09-22 사용자 확정 사항 |
| 작업은 `feature/native-logger`, `main` 직접 커밋 금지, `dev→main` 은 release-merge 절차로만 | CLAUDE.md 브랜칭 전략. 세션 1 이 복구한 규칙 |
| Owned 파일 밖은 건드리지 않음, 필요하면 주 에이전트로 반환 | astra-orchestrator 계약. ∥ 동시 위임의 전제 |
| 통과 기준은 `run-tests.ps1` 실측(EditMode + PlayMode). 통과 수는 앞 세션 이상 | 회귀 기준선. 자기 보고 금지 |
| `rg "Debug\.Log" Runtime Editor Samples~` → 0건 (Tests 제외, `LogAssert.Expect` 필수) | 계획의 목적 그 자체 |
| hot path(`Logger.Write`, `IsEnabled`) 변경은 Phase 3·4 의 지정된 것만, 할당 회귀 테스트 0B 유지 | 성능 계약(Phase 4) |
| 퍼블릭 API 변경 시 `api-guidelines.md` 23항목, 코어 변경 시 `performance-checklist.md` 를 리뷰어 입력에 | dev 의 프로젝트 스킬. Codex 는 문서로 읽어 항목별 대조 |
| 스펙(계획 문서)이 틀렸다고 판단되면 임의로 바꾸지 말고 보고 | 결정은 지도에 기록한 뒤 문서를 고친다 |
| Unity Editor 를 저장소 위에 열어 둔 채 batchmode 실행 금지 | 라이선스·Library 잠금 충돌 |

## 막혔을 때

세션이 중간에 끊겼으면 같은 세션 프롬프트를 다시 넣지 말고 이렇게 이어가세요.

```
$astra-orchestrator Documentation~/Plans/2026-09-22-debug-log-replacement/ 의 Phase 문서들을 읽고 git log 와 대조해
마지막으로 완료·커밋된 Step 다음부터 이어서 진행해. 규칙은 그대로 유지하고, Phase {N} 까지만 하고 멈춰라.
run-tests.ps1 이 실패한 상태면 어느 테스트가 어떤 값으로 실패했는지 먼저 보고해라.
```

`run-tests.ps1` 자체가 안 돌면(라이선스, Library 잠금, Unity 경로) Codex 가 우회하지 않게 되어 있습니다. Unity Hub 를 열어 라이선스가 살아 있는지, 저장소를 연 Unity 창이 없는지 확인한 뒤 이어가기 프롬프트를 쓰세요. 계획 문서의 결정(D1~D10)이 틀렸다고 판단되면 Codex 가 임의로 바꾸지 말고 보고하게 하고, 지도의 Decisions 표를 고친 뒤 해당 Phase 문서를 고치는 게 맞습니다.
