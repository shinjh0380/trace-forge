# Unity 6000.6 context 호환성 및 성능 검증

## 목적과 변경 범위

시작 `feature/native-logger` / `1393c04`, clean. 사용자가 6000.6.6f1의 CS0619 오류9곳과 UAC0009 경고3곳을 최소 수정하고 재검증하도록 요청했다. 이후 `ContextInstanceId`/확장 생성자/전달·조회 경로의 `int`→`ulong` Breaking 변경, 구버전 signed ID 왕복과 최소 Unity6000.0 유지도 명시적으로 승인했다. 주 에이전트가 직접 구현·실행하며 독립 Astra 리뷰를 받는다. 제품 버전0.2.0은 유지하고 릴리스 작업은 하지 않는다.

`ContextId`는 6000.6+에서 `GetEntityId`/`EntityId.ToULong`을 사용하고, 구버전에서는 `int`의32비트 패턴을 보존한다. Viewer는 6000.6+의 `FromULong`, 6000.3의 int↔EntityId 변환, 6000.0의 instance-ID 조회를 사용한다. 구버전 조회에서 표현할 수 없는 상위 비트는 다른 객체로 잘라서 조회하지 않고 무시한다. 객체 참조를 저장하지 않으며 null/삭제된 context는0, 이미 저장한 값은 객체 삭제 후에도 불변이다. ID는 세션 내에서만 유효하다. 5인자 생성자는 유지된다.

`DEVELOPMENT_BUILD` 대신 초기화 시 `Debug.isDebugBuild`로 기본 정책과 ring buffer를 결정한다. Editor/Development 기본 동작을 유지하고 일반 Write/IsEnabled 경로에 build-flag 조회를 추가하지 않는다. [Unity managed-code variant 문서](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/managed-code-variants)와 설치 DLL의 EntityId 변환 메타데이터를 확인했다. `int`↔EntityId 변환도6000.6에서 오류이므로 호출명만 교체하거나64비트를 int로 자르는 방식은 사용하지 않았다.

## 환경과 대상 코드

- Unity6000.6.6f1 / d20c9ac777da, Windows11 Home build26200, i7-13700HX(16C/24T), RAM32GB급, Samsung NVMe, Balanced/AC 상태2·충전100%.
- 실제 Test Framework **1.8.0 builtin**. Unity가 기존 요청1.6.0을 자동 갱신한 package-update 로그와 lock을 보존한다. 1.6.0 실행으로 표기하지 않는다.
- 기준 `f95dcc2`, 후보 `1393c04`의 미커밋 호환성 패치. `.unity66-fix-20261018/candidate-source.patch` SHA256 **C541F44F4858F4BF996B1CD0FA77AFE0C2C26B61739E8E8A1A555757144A8BE8**와 `source-hashes.csv`로 실제 측정 소스를 식별한다.
- raw/backup/계획/실행기는 ignored `.worktrees/native-logger-test-host/.unity66-fix-20261018/`에 보존한다. 과거 FAIL 보고서는 수정하지 않는다.

## 전체 컴파일과 표준 호스트 실행

`run-tests.ps1 -Platform EditMode -Out unity66-fix-20261018-editmode-1`: **32/32**, failed/skipped0. 제품의 기존9오류/3경고는 해소됐지만 호스트 반사 코드에서 새 UAC0005/0007 경고7곳이 나타났다. 첫 로그를 보존하고 호스트5개 파일의 어셈블리 열거/경로 조회 API만 버전별로 교체했다.

Library/Temp를 보존·이동하고 빈 process-local Bee cache로 재실행한 `...-editmode-2`는 **32/32**, failed/skipped0, 전체 C# 진단0이다. 네 package assembly의 실제 Csc 실행과 CacheHit0을 확인했다. `clean-full-compile-verification.json`에 기록한다. 호스트 fixture의 테스트 선택이나 단정 제거는 없다.

첫 표준 `...-playmode-1`은 domain reload 뒤 missing-script 메시지와 함께 XML을 내지 못했다. 작업 재개 시 Editor는 이미 종료됐고 이전 실행 채널의 exit code는 회수할 수 없어 unavailable로 기록했다. 컨트롤러 타입은 로드되지만 source `MonoScript.GetClass()`는 null이며, 직접 만든 컨트롤러의 script는 asset path가 비어 있다는 진단을 보존했다. 구체적 근본 원인은 확정하지 않는다. 재임포트, 미사용 Editor 참조의 임시 제거, 동일 Framework의 무수정 embedding은 효과가 없어 원복했다. Framework 본문이나 단정은 고치지 않았다.

두 번째 표준 `...-playmode-2-cold`도 Library/Temp와 Bee cache를 새로 만든 뒤 같은 missing-script에서 멈췄다. 300초 제한을 초과한 것을 확인한 시점(414.1초)에 해당 Editor PID만 종료했다. runner exit1 / Editor exit-1, XML 없음, C# 진단0을 기록한다(`playmode-cold-timeout.json`; exit 값은 주 에이전트의 실행 채널 관찰을 `playmode-cold-exit-observation.json`에 별도로 기록). 예상 PlayMode **107case**는 실행 완료되지 않았으므로 통과/실패/스킵 수를 추정하지 않는다. 두 실행 모두 기본 domain/scene reload를 사용했다. 중단 시 남긴 runner scene은 보존·이동하고, fixture와6000.6 정규화 manifest(local package/UTF1.8), defines와 reload 설정을 복구한다.

## 추가 대상 검증과 구버전 범위

Editor executeMethod의 별도 직접 실행에서 기존/확장 테스트20case가 통과했다: 상위32비트, legacy ID0/1/-1/최소·최대 int 왕복과 잘못된 상위 비트 조회 거절, 9개 context 오버로드의 live/null/삭제 객체, Editor 기본 ring 동작, 기존 직접 FileSink 및 실제 TF/Logger 할당 테스트. 첫 직접 실행은18case 이후 NUnit output context 부재로 실패했으며, context를 올바르게 초기화한 재실행과 원본을 모두 보존한다. 이 직접 실행 재시도에서는 테스트 본문/단정을 변경하지 않았다.

할당 positive control은 count91→92/sample1, 직접 sink measured0, Logger 경로는 policyNone/captureenabled에서0이다. 이는 current-thread GC sample/event proxy이며 bytes 보장이 아니다. Unity Editor에서 실제 `sizeof(LogEntry)`는 **56bytes**다. 이전 guard 검증 기록의 int-context 구조체도56bytes였으나 다른 Editor 버전의 역사적 값이라는 제한을 유지한다. 기존 5인자 생성자의 기준f95dcc2 크기는 별도 benchmark에서40bytes다.

이20case 직접 실행을 표준 PlayMode 전체 통과 수로 합산하지 않는다. Viewer의 실제 객체 조회·핑 및 zero/삭제 객체는 표준 Editor 테스트에서 검증됐다. 보관된6000.3.8f1 Player managed 참조로 Runtime의6000.0/6000.3 symbol 분기를 Editor/Player 각각 컴파일했고 진단0이다. 해당 구버전 Editor/Player 전체를 새로 실행한 결과는 아니다. 보조 compiler의 최초 인자 배치 오류/CS2023 경고와 수정 후 결과도 별도 보존한다.

## 벤치마크 방법과 판정

원래 직접 FileSink 프로토콜을 유지한다: prebuilt entry, 별도 warm-up1000, appendfalse 재개방, queue101024,100000 writes, producer와 같은 시작점의 Flush completion 분리, current-thread GC.Alloc sample proxy, 실제 accepted/written/행수 검증. Windows Editor PlayMode 독립 프로세스20개, C1–4 각5회, 기존 고정 교대 순서다. timeout300초/실패 시 중단하며 성공 값은 모두 보존한다. 중앙값≤+5%와 사전 고정 paired log-ratio t95% 보조 판정도 유지한다. 제안서3%/2%/2일·3세션은 채택하지 않는다. timing source는 기존SHA007AD2708D53AA51558A28EB8791FAD3369A20496DD7A84CF670055E3990B2F0 그대로다.

첫 세트는 **2/20 완료 후 B1-C3가300초 timeout/무XML**로 중단됐다. 두 성공 결과와 모든 로그·XML·파일을 보존한다. C3에는 결과 숫자가 없으며 이후 구성은 실행하지 않았다. 다섯 번의 표본이 없으므로 중앙값 비교나5% 회귀 판정은 **불충분/미검증**이다.

| 실행 | Producer ticks | Flush 포함 completion ticks | GC proxy | accepted/written/실제 행수 | Entry bytes |
|---|---:|---:|---:|---|---:|
| B1-C1 (`f95dcc2`) | 237,448 | 7,655,530 | 0 | 100,000 / 100,000 / 100,000 | 40 |
| B1-C2 (후보) | 152,599 | 9,766,936 | 0 | 100,000 / 100,000 / 100,000 | 56 |
| B1-C3 | 미생성 | 미생성 | 미검증 | 미검증 | 미검증 |

타이머 주파수는10,000,000 ticks/s다. C1/C2에서는 compilation을 수반한 package 전환 후 실행됐지만 동일 소스의 C3 시작에서는 controller startup이 다시 멈췄다. 컴파일·캐시 상태와 바인딩 관계는 후속 진단 대상이며 원인을 단정하지 않는다. 최소 Assets가 표준 전체 PlayMode를 대체하지 않는다는 조건을 유지한다. 일부 성공 값만으로 성능PASS를 선언하지 않는다. 과거 ±10% FAIL과 승인된 v0.3.0 rig 예외, guard 비용 수용 범위는 유지한다. 버전 변경·dev/main 머지·태그·푸시·fresh install은 하지 않는다.

## 독립 리뷰와 최종 판정

주 에이전트가 직접 구현·실행했고 Luna 워커는 사용하지 않았다. fresh native `astra_review` 경로(GPT-6 Astra / low 요청), `/root/unity66_compatibility_astra_review`가 실제 diff, performance 체크리스트27항목, API23항목, XML/log/CSV/실제 행수와 소스 해시를 읽기 전용으로 대조했다. 코드·보고서 **accept**, 전체 성능 승인 **blocked**이며 추가 코드 수리 요구는 없었다. usage 계측은 unavailable이다. cold PlayMode 종료·복구 기록의 후속 대조도 accept였다. 단, 두 exit 값은 리뷰 당시 timeout JSON에 없어 리뷰어가 독립 확인한 값은 아니며 주 에이전트의 실행 채널 관찰이다. 최종 fixture56파일 해시 일치, reload0/0, local dependency와UTF1.8 복구를 확인했다. 임시 기준 worktree는 생성된 metas를 보존한 뒤 제거했다.

`Debug.isDebugBuild` getter의 별도 thread-safe annotation은 확인되지 않았다. 통상 bootstrap 초기화 경로 밖의 background-thread first-touch는 별도로 재현하지 않았으며, 리뷰에서 도달 가능한 결함으로 확정하지 않았다. Write/IsEnabled에 native build-flag 조회가 들어가지는 않는다.

| 구분 | 판정 | 근거/남은 조건 |
|---|---|---|
| 원래9오류·3경고 | 해소 | 실제 전체 Csc, 진단0 |
| 표준 EditMode | PASS | 32/32, 실패·스킵0 |
| 추가 개별 검증 | PASS | 직접20case; 전체 PlayMode 대체 아님 |
| 기존 할당 회귀 | 개별 검증 PASS | positive control 유효, sink/Logger GC proxy0 |
| 표준 PlayMode 전체 | BLOCKED | controller 초기화 정지, 두 번 무XML |
| 직접 FileSink 성능5% | 미검증/불충분 | 2/20 완료; C3에서 규칙대로 중단 |
| 전체 성능 승인 | BLOCKED | 표준 PlayMode와 고정 벤치마크 세트 미완료 |
| release-check | 미실행 | performance PASS 선행 조건 미충족 |

다음 작업은 test-runner controller 바인딩 실패 원인을 확인해 표준 PlayMode를 복구하는 것이다. 구체적인 환경 수정 없이 같은 벤치마크를 유리한 값이 나올 때까지 반복하지 않는다. 과거 ±10% FAIL과5% 기준을 유지하며 새 기준선·전체 성능 PASS·릴리스 승인을 선언하지 않는다.
