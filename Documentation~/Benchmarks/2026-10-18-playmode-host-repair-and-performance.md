# PlayMode 호스트 경로 복구와 동일 rig 성능 검증

## 목적

Unity 6000.6.6f1의 표준 PlayMode missing-script 원인을 재현하고 최소 호스트 수정으로 전체 테스트와 직접 FileSink 벤치마크를 다시 검증한다. 주 에이전트가 직접 수행하며 독립 Astra 리뷰를 받는다. 테스트 본문·선택·단정, 기본 domain/scene reload, 기존 성능 기준을 유지한다. 릴리스 작업은 하지 않는다.

## 환경과 대상 커밋

- 시작: `feature/native-logger`, `89f90c4`, clean. 제품 소스는 `d41dcc9`와 동일하며 이번 작업에서 변경하지 않는다.
- Unity 6000.6.6f1 (`d20c9ac777da`), Windows Editor PlayMode/batch mode, Test Framework 1.8.0.
- Windows 11 Home 10.0.26200, i7-13700HX 16코어/24논리 프로세서, RAM 약32GB, Samsung MZVL21T0HDLU NVMe SSD. 타이머10,000,000ticks/s, Balanced 전원 구성이다.
- 호스트: `.worktrees/native-logger-test-host`; TraceForge는 저장소 루트의 local package다.
- Framework의 설치 원본 1,176파일을 `%LOCALAPPDATA%/TraceForge/UnityTestFramework/6000.6.6f1-1.8.0`로 그대로 복사하고 local dependency로 참조한다. C#/asmdef/.meta 수정0, 원본 해시 불일치0.
- 원시 근거/백업: 호스트의 `.playmode-repair-20261018/`; 새 측정 세트: `.playmode-benchmark-20261018/`. 이전 실패와 불완전 2/20 세트는 덮어쓰거나 합산하지 않는다.

## 원인 재현과 최소 수정

실패한 runner scene은 `PlaymodeTestsController`를 GUID 없는 inline `MonoScript`로 저장했다. 컨트롤러 CLR 타입은 존재하지만, 원본 script의 assembly/namespace가 비고 `MonoScript.GetClass()`는 null이었다. domain reload 뒤 해당 참조가 missing-script가 되어 테스트를 시작하지 못했다.

캐시를 비우고 Framework까지 실제 재컴파일하거나 `UNITY_TESTS_FRAMEWORK`를 잠시 전역 정의해도 복구되지 않았다. 임시 정의는 제거했다. 새 진단 프로젝트에서 다음 A/B/A를 확인했다:

| 조건 | Framework controller의 논리 경로 | GetClass |
|---|---|---|
| Framework만 설치, 새 프로세스 2회 | `Packages/com.unity.test-framework/...` | 정상 |
| 호스트 상위 저장소를 local package로 추가 | `Packages/com.skrawberries.traceforge/.worktrees/runner-binding-isolation/Library/PackageCache/...` | null |
| 상위 package 참조만 제거 | `Packages/com.unity.test-framework/...` | 정상 |

`FileUtil.GetLogicalPath`의 실제 출력으로 경로 오인식을 확인했다. 이는 이 중첩 local-package 구성의 재현 결과이며 모든 6000.6 프로젝트의 Framework 결함으로 일반화하지 않는다. Framework를 상위 패키지 밖에서 참조해 겹치는 경로를 피한다. 설치 폴더 직접 참조도 연결은 복구했으나 Program Files directory watcher 쓰기 실패가 있어, 최종 수정은 사용자 쓰기 가능 캐시의 **무수정 복사본**을 참조하는 manifest/lock 변경이다. 제품 코드나 테스트를 고치지 않는다.

원본 fixture56파일과 tracked package85파일의 SHA256 불일치0. 임시 진단 스크립트는 Assets에서 제거·보존했다. [호스트 재생성 절차](../TestHost.md#nested-host-package-path-correction-60006)에 복구 방법을 기록한다.

## 전체 컴파일과 표준 테스트

표준 `run-tests.ps1 -Platform EditMode` / `-Platform PlayMode`를 사용했다. 필터와 단정 변경이 없으며 reload는 기본값이다.

| 실행 | 통과/전체 | 실패 | 스킵 | 비고 |
|---|---:|---:|---:|---|
| 첫 EditMode | 32/32 | 0 | 0 | 설치 원본 직접 참조 단계 |
| 첫 PlayMode | 107/107 | 0 | 0 | 최종 외부 writable copy |
| 최종 EditMode | 32/32 | 0 | 0 | 새 Library/Temp와 빈 process-local Bee cache |
| 후속 PlayMode | 107/107 | 0 | 0 | 독립 프로세스, warm start + domain reload |

최종 EditMode에서 Runtime/Editor/양쪽 package test assembly의 실제 Csc4회, Csc CacheHit0, C# 경고·오류0을 확인했다. 최초 로그 분석기의 CRLF 정규식 오류는 별도 기록했고, 같은 원본 로그를 행 단위로 다시 읽어 검증했다. 테스트나 소스를 바꿔 통과시킨 것이 아니다. 전체 PlayMode에는 기존 sink/Logger 할당 회귀와 재진입·스레드 테스트, 호스트 샘플·미처리 예외 검증이 포함된다. GC 수치는 current-thread event/sample proxy이며 바이트 수 보장으로 해석하지 않는다.

## 측정 방법

측정 전 `frozen-plan.md`에 C1–C4 각5회, 총20개 독립 프로세스의 순서·중단·판정 규칙을 고정했다. C1은 `f95dcc2`, C2는 후보 None/capture off, C3는 후보 None/capture on/발신 없음, C4는 후보 ErrorAndAbove/Info/capture off다. 두 코드 모두 같은 외부 Framework 복사본을 사용한다.

기존 timing source SHA256 `007AD2708D53AA51558A28EB8791FAD3369A20496DD7A84CF670055E3990B2F0`를 유지한다. 별도 warm-up1,000, append=false 재개방, queue101,024, prebuilt entry로100,000회 직접 `FileSink.Write(in entry)`를 측정한다. Producer와 동일 시작점의 Flush completion을 분리한다. current-thread GC.Alloc proxy, accepted/written/실제100,000행을 검증한다. Logger/TF dispatch·guard·정책 읽기·Unity callback 실행은 측정 범위 밖이다.

순서: `1234 / 2413 / 3142 / 4321 / 1324`. 성공 값 제외·유리한 결과까지 반복은 없다. timeout300초 또는 XML/테스트/할당/파일 검증 실패 시 세트를 중단한다. 각 구성의 producer 중앙값 비율≤1.05와 같은 block의 log-ratio t95% 상한≤1.05를 모두 만족해야 PASS다. 중앙값 비율>1.05와 t95% **하한**>1.05를 모두 만족하면 FAIL, 나머지는 INSUFFICIENT다. 이 불확실성 검사는 이전 고정 계획과 동일하며 허용 회귀율을 새로 늘리지 않는다.

## 5회 원시 결과

| 실행 | 구성 | Producer ticks | Flush 포함 completion ticks | GC proxy | accepted/written/실제 행수 |
|---|---:|---:|---:|---:|---|
| B1-C1 | 1 | 228,022 | 8,593,011 | 0 | 100,000 / 100,000 / 100,000 |
| B1-C2 | 2 | 165,677 | 6,237,513 | 0 | 100,000 / 100,000 / 100,000 |
| B1-C3 | 3 | 185,713 | 6,479,657 | 0 | 100,000 / 100,000 / 100,000 |
| B1-C4 | 4 | 168,286 | 6,931,290 | 0 | 100,000 / 100,000 / 100,000 |
| B2-C2 | 2 | 187,018 | 6,399,173 | 0 | 100,000 / 100,000 / 100,000 |
| B2-C4 | 4 | 172,045 | 5,429,876 | 0 | 100,000 / 100,000 / 100,000 |
| B2-C1 | 1 | 212,082 | 6,231,882 | 0 | 100,000 / 100,000 / 100,000 |
| B2-C3 | 3 | 194,498 | 4,948,864 | 0 | 100,000 / 100,000 / 100,000 |
| B3-C3 | 3 | 214,311 | 8,294,735 | 0 | 100,000 / 100,000 / 100,000 |
| B3-C1 | 1 | 223,282 | 6,895,822 | 0 | 100,000 / 100,000 / 100,000 |
| B3-C4 | 4 | 191,221 | 6,580,093 | 0 | 100,000 / 100,000 / 100,000 |
| B3-C2 | 2 | 161,685 | 7,168,967 | 0 | 100,000 / 100,000 / 100,000 |
| B4-C4 | 4 | 168,669 | 6,671,979 | 0 | 100,000 / 100,000 / 100,000 |
| B4-C3 | 3 | 210,432 | 8,428,434 | 0 | 100,000 / 100,000 / 100,000 |
| B4-C2 | 2 | 171,185 | 9,206,733 | 0 | 100,000 / 100,000 / 100,000 |
| B4-C1 | 1 | 228,622 | 10,134,446 | 0 | 100,000 / 100,000 / 100,000 |
| B5-C1 | 1 | 227,981 | 7,860,451 | 0 | 100,000 / 100,000 / 100,000 |
| B5-C3 | 3 | 201,773 | 7,391,608 | 0 | 100,000 / 100,000 / 100,000 |
| B5-C2 | 2 | 170,726 | 6,198,105 | 0 | 100,000 / 100,000 / 100,000 |
| B5-C4 | 4 | 166,466 | 7,529,389 | 0 | 100,000 / 100,000 / 100,000 |

| 구성 | Producer 중앙값 ticks (ms) | Completion 중앙값 ticks (ms) | Producer 변화 | Paired t95% 비율 구간 | 수치 판정 |
|---|---:|---:|---:|---|---|
| C1 | 227,981 (22.7981) | 7,860,451 (786.0451) | +0.0000% | — | REFERENCE |
| C2 | 170,726 (17.0726) | 6,399,173 (639.9173) | -25.1139% | 0.690131–0.845591 | PASS |
| C3 | 201,773 (20.1773) | 7,391,608 (739.1608) | -11.4957% | 0.831781–0.969530 | PASS |
| C4 | 168,669 (16.8669) | 6,671,979 (667.1979) | -26.0162% | 0.707520–0.844803 | PASS |


총20/20 프로세스가 각각1/1 통과, 실패·스킵0이다. 독립 파일 재계수도 모두100,000행이며, accepted/written100,000, GC proxy0, Entry size C1=40/C2–4=56bytes였다. 원시 XML과 CSV 수치가 일치하고, 기준/후보 총114개 tracked source/asset 해시는 측정 전후 불일치0이다.

## 중앙값과 불확실성 해석

C1 producer 중앙값은227,981ticks(22.7981ms)다. C2/C3/C4의 변화는 각각 **-25.1139% / -11.4957% / -26.0162%**이며, paired t95% 상한은 **0.845591 / 0.969530 / 0.844803**으로 모두1.05 미만이다. 고정한 수치 판정은 세 구성 모두 **PASS**다. 이 결과를 Logger 정책·guard 실행 비용으로 해석하지 않는다.

동일 block의 C1 대비 producer 비율(C2/C3/C4)은 아래와 같다. 범위와 모든 성공 값을 유지했으며 outlier 제외는 없다.

| Block | C2/C1 | C3/C1 | C4/C1 |
|---|---:|---:|---:|
| 1 | 0.726583 | 0.814452 | 0.738025 |
| 2 | 0.881819 | 0.917089 | 0.811219 |
| 3 | 0.724129 | 0.959822 | 0.856410 |
| 4 | 0.748769 | 0.920436 | 0.737764 |
| 5 | 0.748861 | 0.885043 | 0.730175 |

C1 producer 범위212,082–228,622ticks, 첫2회 중앙값220,052 대비 마지막3회 중앙값227,981의 이동은 **+3.6032%**다. 이 변동을 숨기거나 미승인3%·2% 규칙으로 새 합격/실패 기준을 만들지 않는다. 독립 block/정규 log-ratio 가정과 n=5의 제한을 유지한다. Completion에는5% gate가 없으며 별도 중앙값을 표에 모두 기록했다.

## 판정과 제한

프로세스 전후 CPU snapshot 범위는20–100%로, idle/열·클럭 안정성을 보장하는 환경은 아니다. snapshot은 실제 timed region의 연속 profile이 아니다. Balanced 전원 구성, Win32_Battery status2·잔량100은 모든 snapshot에서 동일했다. 초기 driver는 실제 ACLineStatus를 별도로 읽지 않았으므로 status2만으로 AC 연결을 단정하지 않는다. 08:08:43 UTC의 중간 보조 조회와 세트 종료 후 조회에서는 ACLineStatus1/잔량100을 확인했다. 초기 명시적 AC 조회가 없다는 계측 한계를 유지하며, 관측하지 못한 변동이 없었다고 보장하지 않는다. 이 한계와 작은 표본을 독립 리뷰에도 제공한다.

과거 ±10% 재현성 FAIL 기록과 승인된 이번 v0.3.0 Windows rig 예외, 직접 producer5%·할당·파일 기록 기준을 유지한다. 과거 공식→교대 baseline producer 변동 약12.55% 기록도 유지한다. TF guard의 과거 +5.71–8.07% 수용은 해당 안전성 수정에 한정한다. 3%/2%/2일·3세션 제안은 채택하지 않는다. 이번 수치를 자동 새 기준선으로 채택하거나 Player/mobile 성능으로 일반화하지 않는다.

## 독립 리뷰와 최종 판정

주 에이전트가 원인 재현·호스트 수정·전체 실행·분석을 직접 수행했으며 Luna 워커는 사용하지 않았다. fresh native `astra_review` 경로(GPT-6 Astra/low), `/root/playmode_host_repair_astra_review`가 읽기 전용으로 A/B/A 로그, 원본/복사 Framework1,176파일, source114파일, fixture56파일, 실제 Csc/표준 XML, benchmark XML20개와 실제200만 행을 대조했다. 중앙값과 paired 구간의 독립 재계산도 일치했다. token usage는 unavailable이다.

리뷰의 유일한 수정 요청은 문서의 FAIL 판정식이었다. 실제 사전 계획/분석기의 **중앙값>1.05 AND CI하한>1.05**를 명시했고 재리뷰는 **accept**, 추가 수정 요청0이었다. 코드·테스트·원시 값·판정 규칙 변경이나 유리한 재측정은 없었다.

성능 체크리스트27항목은 1–2/6–7/10/13–25/27의19개 source/test 계약 충족, 3/8/11/12의4개 기존 승인 예외(caller guard, sink-failure fallback, 동적 category, ErrorAndAbove 기본값), 4/5/9/26의4개 N/A(params/lazy overload/StringBuilder/UnityConsoleSink 없음)로 구분한다. 특히22의 일반 sink 재진입 guard와27의 기본 domain reload는 이번 표준 전체 실행으로 확인했다. 이 집계를 범용 성능 보장으로 해석하지 않는다.

| 구분 | 최종 판정 |
|---|---|
| missing-script 원인 재현·최소 호스트 복구 | **PASS / 독립 accept** |
| 새 실제 전체 C# 컴파일 | **PASS**, Csc4/CacheHit0, 경고·오류0 |
| 표준 전체 EditMode / PlayMode | **32/32 / 107/107 PASS**, 실패·스킵0 |
| 기존 sink 및 실제 Logger 할당 회귀 | **PASS**, 유효한 positive control과 GC sample proxy0 |
| 동일 Windows Editor rig 직접 FileSink producer5%·GC·파일 | **범위 한정 PASS**,20/20, 구성별 paired 상한≤1.05 |
| 역사적 ±10% 재현성 | **기존 FAIL 보존**, 승인된 v0.3.0 예외 범위 유지 |
| 안정된 rig 보장·새 TF guard 비용·Player/mobile 성능 | **이번 실행으로 검증하지 않음** |
| 릴리스 사전검사 | [38항목 재점검](2026-10-18-release-preflight.md), 릴리스 승인과 구분 |

manifest/lock, ProjectSettings, fixture56파일은 최종 정규화 상태로 복원했다. reload0/0, 빈 defines와 외부 무수정 Framework 참조를 유지하며 표준 runner 해시도 동일하다. 이전 실패 자료는 그대로 보존한다. 기준 worktree는 Unity가 생성한 meta38개를 해시와 함께 보존하고 tracked clean을 확인한 뒤 force 없이 제거했다. 버전 변경·dev/main 머지·태그·푸시·릴리스 fresh install은 수행하지 않았다.
