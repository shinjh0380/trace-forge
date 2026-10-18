# 릴리스 사전검사 — 호스트 복구 후

2026-10-18, `feature/native-logger` / `89f90c4`의 제품 소스를 대상으로 읽기 전용 검사했다. package version은 **0.2.0**, 최소 Unity는 **6000.0**으로 유지한다. 목표 v0.3.0의 버전 확정·dev/main 병합·태그·푸시·fresh install은 이번 작업에 포함하지 않는다.

## 선행 근거와 판정

- [새 전체 검증](2026-10-18-playmode-host-repair-and-performance.md): Unity6000.6.6f1/UTF1.8.0, 새 실제 Csc4/CacheHit0/진단0, 표준 Edit32/32·Play107/107, 실패·스킵0. 동일 rig의 직접 FileSink20/20, producer5%/GCproxy0/실제100,000행은 **범위 한정 PASS**, 독립 Astra accept다.
- API23항목은 [직전 호환성 리뷰](2026-10-18-unity66-context-compatibility.md)의 코드 accept를 유지한다. 이번 tracked package85파일 해시 불일치0이며 새 API 변경은 없다. 승인된 명칭/오버로드·context-last 예외를 일반 규칙 위반으로 새로 해석하지 않는다.
- package audit36항목은 아래와 같이 대조했다. 향후 main 배포 트리 제외 검증은 아직 완료되지 않았으므로 package audit/릴리스 전체 승인으로 보고하지 않는다.
- release-check38항목의 최종 판정은 **BLOCKED**다. 테스트·컴파일·지정 rig 성능 차단은 해소됐지만, 버전/CHANGELOG 확정과 배포 트리·이전 버전 비교 검증은 남아 있다. 이 판정은 후속 릴리스 작업의 실행 승인이 아니다.

원시 근거는 호스트 `.playmode-repair-20261018/`의 `release-preflight-static.json`, 표준 XML/컴파일 영수증과 `.playmode-benchmark-20261018/`의 전체 원시 결과다. 공개 documentationUrl은 [canonical repository](https://github.com/shinjh0380/trace-forge)에 실제 접근됨을 확인했다. 새 후보가 원격 main에 게시됐다는 의미는 아니다.

## Package audit — 36항목

기존 체크리스트의 예시 package명·6000.3 최소값·Settings 하위 폴더는 사용자 승인 명세를 우선한다. `com.skrawberries.traceforge`, 최소6000.0 유지, `Editor/TraceForgeSettingsProvider.cs` 및 Runtime의 Editor loader hook은 이미 승인된 설계다.

| # | 판정 | 근거 |
|---|---|---|
| 1 | PASS | canonical scoped lowercase package명 |
| 2 | PASS | 0.2.0 semver |
| 3 | 승인 예외 | 최소6000.0 유지 지시, 새 실행은6000.6.6f1 |
| 4 | PASS | displayName TraceForge |
| 5 | PASS | Unity6 sink/filter 설명 |
| 6 | PASS | author 세 필드 존재, identity 보존 |
| 7 | PASS | MIT와 LICENSE 일치 |
| 8 | PASS | logging/traceforge/unity keywords |
| 9 | PASS | TraceForge.Runtime asmdef |
| 10 | PASS | TraceForge.Editor asmdef |
| 11 | PASS | Runtime includePlatforms 빈 배열 |
| 12 | PASS | Editor only |
| 13 | PASS | Editor가 Runtime을 이름으로 참조, 실제 컴파일 성공 |
| 14 | PASS | 양쪽 Tests allowUnsafeCode=false |
| 15 | PASS | Runtime references/dependencies 비어 있음 |
| 16 | PASS | Runtime autoReferenced=true, Editor=false |
| 17 | PASS | Runtime의 using UnityEditor 0 |
| 18 | 승인 예외 | Bootstrap의 UNITY_EDITOR hook, UnityEditor 의존성 없음 |
| 19 | PASS | Runtime asmdef에 UnityEditor 참조 없음 |
| 20 | 승인 예외 | 지정된 Editor/TraceForgeSettingsProvider.cs 위치 |
| 21 | PASS | Samples~/BasicUsage |
| 22 | PASS | Tests/Runtime·Tests/Editor 분리 |
| 23 | PASS | 루트 README |
| 24 | PASS | canonical Git 설치 URL |
| 25 | PASS | Quick Start와 sink lifecycle 예시 |
| 26 | PASS | Keep a Changelog 구조 |
| 27 | PASS | Unreleased 존재 |
| 28 | PASS | LICENSE.md 존재 |
| 29 | PASS | public 타입/메서드 summary, 신규 API XML 확인 |
| 30 | PASS | 현재 sample API 실제 컴파일·Awake 검증 |
| 31 | PASS | canonical repository.url |
| 32 | PASS | 루트 package.json, UPM package 구조 |
| 33 | 미검증 | 개발 브랜치는 tooling/Tests 포함; 향후 main 제외 검증 필요 |
| 34 | PASS | AllAssets/NoOrphanedMeta 테스트 통과 |
| 35 | PASS | 유효한 tracked meta52개 GUID, 이 트리 내 중복0; 전 세계 충돌 보장 아님 |
| 36 | PASS | documentationUrl 설정·접근 확인 |

32 PASS, 승인 예외3, 배포 검증 미완료1. Framework 외부 복사본은 ignored 호스트 의존성으로만 사용하며 제품 dependencies에는 추가하지 않았다.

## Release-check — 38항목

| # | 항목 | 판정 | 실제 근거/남은 작업 |
|---|---|---|---|
| 1 | semver | PASS | package0.2.0 |
| 2 | 새 버전 날짜 섹션 | FAIL/보류 | 목표0.3.0 섹션 없음; 기존0.2.0은 존재 |
| 3 | Unreleased 이동·비우기 | FAIL/보류 | 의도적으로 아직 이동하지 않음 |
| 4 | package/최신 릴리스 버전 일치 | PASS | 현 상태0.2.0 일치, 메타 테스트 통과 |
| 5 | 버전 비교 링크 | FAIL/보류 | CHANGELOG 하단 compare 링크 없음 |
| 6 | 변경 분류 | PASS | Added/Changed/Fixed/Removed/Breaking 구분 |
| 7 | Breaking 표시 | PASS | override/IsEnabled/context ulong 명시 |
| 8 | 사용자 영향 중심 | PASS | 필터·API·출력 행동 설명 |
| 9 | 마이그레이션 안내 | PASS | Migration.md와 README context 제한 |
| 10 | LICENSE 존재 | PASS | 루트 LICENSE.md |
| 11 | 현재 연도 | PASS | 2026 |
| 12 | 라이선스 일치 | PASS | MIT |
| 13 | 필수 메타데이터 | PASS | package.json 필드와 신규 메타 테스트 |
| 14 | 최소 Unity 정확성 | PASS | 승인된6000.0 유지; 구버전 전체 재실행을 새로 주장하지 않음 |
| 15 | keywords | PASS | 현재 sink/logging 기능 반영 |
| 16 | repository URL | PASS | github.com/shinjh0380/trace-forge.git |
| 17 | documentationUrl 접근 | PASS | canonical 공개 repository 접근 확인 |
| 18 | 설치 가이드 | PASS | 현재 canonical Git URL, 미생성v0.3.0 설치 성공 주장은 없음 |
| 19 | 신규 public API 문서 | PASS | ulong/session-local/context/설정·성능 제한 |
| 20 | README 마이그레이션 | PASS | Migration 링크와 Breaking context 안내 |
| 21 | sample API | PASS | 표준 호스트의 현재 sample 컴파일/Awake 테스트 |
| 22 | 신규 API XML | PASS | context 오버로드/LogEntry 생성자의 summary·param, 설정 필드 summary |
| 23 | 미해결 TODO 표식 | PASS | Runtime/Editor TODO/FIXME/HACK/XXX 0 |
| 24 | using 검사 | PASS | Runtime/Editor의 namespace 사용 대조, 새 추가 없음 |
| 25 | 컴파일 진단 | PASS | fresh Csc4, CacheHit0, C# 경고·오류0 |
| 26 | DEBUG 조건 | N/A | #if DEBUG 없음 |
| 27 | Unity Debug.Log 호출 | PASS | Runtime/Editor/Samples~ 0 |
| 28 | UnityEngine.Debug 제한 | N/A/승인 예외 | 제거된 UnityConsoleSink 규칙; 승인된 초기화 전용 isDebugBuild3곳만 사용, 로그 호출0 |
| 29 | 전체 EditMode | PASS | 32/32, failed0/skipped0 |
| 30 | 전체 PlayMode | PASS | 107/107, failed0/skipped0 |
| 31 | 새 기능 테스트 | PASS | bootstrap/context/capture/guard/Viewer와 호스트 fixture |
| 32 | edge cases | FAIL/커버리지 | null category/context, Off/zero-sink는 검증됨. 빈 message 전달을 단정하는 명시적 테스트는 찾지 못함; 빈 category/빈 FileSink 경로 테스트를 대체 근거로 세지 않음 |
| 33 | 성능 회귀 | PASS/범위 한정 | 직접 FileSink20/20·기존 Logger 할당; 승인된 역사적 예외와 guard 비용 범위 유지 |
| 34 | sample import 컴파일 | PASS | 호스트 복사 sample과 Awake/Viewer 표준 PlayMode 통과 |
| 35 | sample 설명 | PASS | bootstrap/FileSink 추가·해제·guard 주석 |
| 36 | clean 작업 트리 | 기록 중/보류 | 이 사전검사 시점에는 결과 문서 수정 중; 완료 후 Git 영수증을 별도 보존 |
| 37 | main 릴리스 반영 | FAIL/보류 | main3baeb05 그대로; 수행 금지 범위 |
| 38 | 이전 버전 이후 릴리스 범위 | 미검증 | local tag0, dev..HEAD22개 검토; 최종 main 배포 diff/이전 태그 비교는 미완료 |

검사 시점 집계: **29 PASS / 5 FAIL(준비·배포 보류4, 명시적 테스트 커버리지1) / 2 N/A·승인 예외 / 1 미검증 / 1 기록 중**. #32는 실제 실행 실패나 확인된 runtime 결함이 아니라 체크리스트가 요구하는 빈 message 검증 근거의 부재다. 이번 요청은 테스트 본문·단정 보존이므로 테스트를 추가·수정하지 않고 남긴다. 현재 branch의 새 CI 실행, v0.3.0 Player/fresh install도 이번에 수행하지 않았다. 과거 CI 성공과 이번 로컬 검증을 서로 대체하지 않는다.

코드/호스트/제한된 성능 판정 accept와 **릴리스 BLOCKED**를 구분한다. 이번 세션은 기존 실패 기록·기준을 유지하고 복구 및 검증 문서만 커밋하며 끝낸다.

독립 `/root/playmode_host_repair_astra_review`는 새 추적 문구의 제한된 PASS/릴리스 BLOCKED 구분을 읽기 전용으로 대조해 accept했다. 이어 #32의 빈 message 테스트 근거 부재와 29 PASS/5 FAIL 정정도 직접 source를 확인해 accept했다. 배포 작업을 실행하거나 새 테스트를 만든 리뷰가 아니다. #36은 검사 시점 기록이며, 문서 커밋 후 최종 clean 상태는 별도 `final-git-state.json`과 최종 응답에 남긴다.
