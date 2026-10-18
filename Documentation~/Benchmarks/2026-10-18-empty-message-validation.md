# 빈 message 전달 테스트와 릴리스 사전검사

## 범위와 구현

2026-10-18, `feature/native-logger` / `6be3dc3`, 시작 작업 트리 clean. 주 에이전트가 직접 `Tests/Runtime/SinkDispatchTests.cs`에 13줄·parameterized 2case를 추가했다. 테스트 커밋은 `9cb9468`이다.

`EmptyMessage_IsDispatchedUnchanged(bool captured)`는 기존 `WriteVia` helper로 공개 `TF.Info` 및 내부 `Logger.WriteCaptured`에 `string.Empty`를 보낸다. 실제 등록한 sink에 **정확히 1회 전달**되고 `LogEntry.Message`가 빈 문자열로 보존됨을 각각 단정한다. entry 생성자만 직접 검사하는 테스트가 아니다. 기존 테스트 본문·단정·선택은 변경하지 않았다. 추가 블록을 제거한 파일이 이전 파일과 동일함도 확인했다.

제품 Runtime/Editor/Samples, public API, package version0.2.0, 최소Unity6000.0, CHANGELOG, CI, 호스트 runner와 성능 코드/기준 변경0. 기존 동작의 커버리지 추가이므로 첫 실행에서 새 테스트가 통과했고, 인위적인 production 실패나 red run은 만들지 않았다.

## 실제 전체 실행

Unity6000.6.6f1, 무수정 외부 UTF1.8.0 복사본과 기존 local package 호스트를 사용한다. 표준 명령이며 별도 test filter는 없다.

| 실행 | Passed/Total | Failed | Skipped | 기존 case 대조 |
|---|---:|---:|---:|---|
| `run-tests.ps1 -Platform EditMode -Out empty-message-20261018-editmode` | 32/32 | 0 | 0 | 이전32개 차이0 |
| `run-tests.ps1 -Platform PlayMode -Out empty-message-20261018-playmode` | 109/109 | 0 | 0 | 이전107개 모두 유지, 새2case만 추가 |

새 양 플랫폼 로그의 C#/UAC 경고·오류0. 이번은 변경된 테스트 assembly 재컴파일이며 full4assembly cold compile을 새로 수행했다고 주장하지 않는다. 불변 제품 코드의 앞선 fresh Csc4/CacheHit0/진단0 근거는 [호스트 복구 보고서](2026-10-18-playmode-host-repair-and-performance.md)에 보존한다.

기존 할당 회귀2case도 PASS다: positive control27→28/sample1, 직접 FileSink proxy0, 실제 Logger proxy0(`policy=None`, `capture=enabled`). 이는 GC event/sample proxy이며 바이트 무할당 보장으로 확대하지 않는다.

## 호스트 보존과 검증 한계

시작 설정은 reload0/0이고 PlayMode 로그에 실제 domain reload가 있다. 실행 후 integrity 검사에서 `EditorSettings.asset`의 `m_EnterPlayModeOptionsEnabled`만0→1로 저장된 차이를 발견했다. `m_EnterPlayModeOptions`는0(DisableDomainReload/DisableSceneReload 플래그 없음)이었다. 최초 검사 실패 및 변경 파일을 보존하고, 시작 hash와 일치하는 기존 백업을 확인해 reload0/0으로 복원했다. 최종 host81파일/source88파일·runner 해시 불일치0이다. 테스트 본문이나 Framework를 변경한 복구가 아니다.

새 timing은 실행하지 않았다. 직접 FileSink 프로토콜/제품 소스 불변이므로 앞선20/20의 **해당 Windows Editor 측정 경계에 한정한 PASS**를 유지한다. 기존 producer5%·GCproxy0·100,000행 기준, 역사적±10% FAIL, 승인된 v0.3.0 rig 예외 및 guard 비용 수용 범위를 바꾸지 않았다. 새 기준선이나 Player/mobile/TF 비용 보장을 채택하지 않는다.

ripwire는 Windows 경로 거절 후 WSL 경로의 응답 지연으로 quality gate **unavailable**이었다. 이를 PASS로 세지 않으며 실제 diff 검사, Unity 전체 실행 및 독립 리뷰로 대조했다. tool 계측 한계와 테스트 실패를 구분한다.

## Release-check — 38항목

현재 metadata/asmdefs/금지 소스 scan/GUID/tag/ref를 새 `release-check-static.json`으로 대조했다. 제품·API·문서는 불변이므로 직전 package audit36/API23/범위 한정 performance review의 근거를 유지하고, 테스트/커버리지 항목을 이번 실제 결과로 갱신했다. documentationUrl의 당일 실제 접근 근거는 [직전 사전검사](2026-10-18-release-preflight.md)를 인용하며 재접속한 것으로 세지 않는다. package audit의 main 배포 제외 검증 미완료도 유지한다.
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
| 25 | 컴파일 진단 | PASS | 새 양 플랫폼 로그 C#/UAC 진단0; 제품 코드 불변, 앞선 fresh Csc4/CacheHit0 증거 유지 |
| 26 | DEBUG 조건 | N/A | #if DEBUG 없음 |
| 27 | Unity Debug.Log 호출 | PASS | Runtime/Editor/Samples~ 0 |
| 28 | UnityEngine.Debug 제한 | N/A/승인 예외 | 제거된 UnityConsoleSink 규칙; 승인된 초기화 전용 isDebugBuild3곳만 사용, 로그 호출0 |
| 29 | 전체 EditMode | PASS | 새 표준 실행32/32, failed0/skipped0, 기존 case 누락/추가0 |
| 30 | 전체 PlayMode | PASS | 새 표준 실행109/109, failed0/skipped0, 기존107개 유지+empty message2개 |
| 31 | 새 기능 테스트 | PASS | bootstrap/context/capture/guard/Viewer와 호스트 fixture |
| 32 | edge cases | PASS | 새 EmptyMessage_IsDispatchedUnchanged(False/True)가 정확히1회 전달과 원문 보존 단정; 기존 null/Off/no-sink 유지 |
| 33 | 성능 회귀 | PASS/범위 한정 | 직접 FileSink20/20·기존 Logger 할당; 승인된 역사적 예외와 guard 비용 범위 유지 |
| 34 | sample import 컴파일 | PASS | 호스트 복사 sample과 Awake/Viewer 표준 PlayMode 통과 |
| 35 | sample 설명 | PASS | bootstrap/FileSink 추가·해제·guard 주석 |
| 36 | clean 작업 트리 | PASS | 테스트 커밋9cb9468 직후 clean 확인, test-commit-clean.json; 문서 커밋 후 final 영수증 별도 보존 |
| 37 | main 릴리스 반영 | FAIL/보류 | main3baeb05 그대로; 수행 금지 범위 |
| 38 | 이전 버전 이후 릴리스 범위 | 미검증 | local tag0; 최종 main 배포 diff/이전 태그 비교 미완료 |

최종 사전검사 집계: **31 PASS / 4 FAIL·보류 / 2 N/A·승인 예외 / 1 미검증**. #32의 빈 message 커버리지 차단은 해소됐다. 남은 FAIL #2/3/5/37은 새 버전 날짜 섹션·Unreleased 이동·compare 링크·main 반영이며, #38과 최종 배포 트리 검증도 미완료다. **릴리스 BLOCKED**이며 이번 테스트 통과가 릴리스 승인은 아니다. 버전 변경·merge·tag·push·fresh install은 수행하지 않았다.

## 독립 리뷰와 산출물

구현·실행은 주 에이전트 직접, Luna worker0명. fresh native `astra_review`(요청 GPT-6 Astra/low), `/root/empty_message_astra_review`가 읽기 전용으로 실제13줄 diff, helper/제품 경로, 양 플랫폼 XML과 기존 case 집합, 할당·컴파일 로그, host81/source88파일 재해시 및 설정 복원을 대조했다. **accept**, 수정 요청0건, token usage unavailable. #36은 실제 clean 영수증을 확인한 후 PASS로 기록했다.

원시 XML/log는 호스트 루트의 `empty-message-20261018-*`, 세부 영수증은 ignored `.empty-message-20261018/`에 보존한다. `existing-tests-preserved.json`, 플랫폼별 verification, `empty-message-and-allocation-cases.json`, `post-run-integrity-first-failure.json`, 변경된 EditorSettings 사본/복원 영수증, `host-and-source-after.json`, source/host hash 목록, review 및 commit/final Git 영수증을 포함한다. 이전 실패 보고서와 원시 벤치마크는 수정하지 않았다.