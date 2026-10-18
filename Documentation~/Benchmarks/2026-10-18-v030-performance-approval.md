# v0.3.0 Windows Editor 호스트 및 성능 사전검증

## 범위와 승인 정책

2026-10-18, 시작 `feature/native-logger` / `d525209feff597d64d220e292af81f0c7bc6f339`, 작업 트리 clean. 주 에이전트가 직접 호스트 수정·측정·판정을 수행하고 독립 Astra 리뷰를 받는다. Runtime/Editor/Tests/샘플 소스와 버전은 변경하지 않는다. 버전 변경·dev/main 머지·태그·푸시·fresh install은 수행하지 않는다.

사용자 승인에 따라 과거 ±10% 역사적 재현성 FAIL은 보존하되 이번 v0.3.0 지정 Windows Editor rig의 필수 gate에서는 제외한다. 동일 환경에서 `f95dcc2`와 후보를 직접 비교하고 FileSink producer 회귀 ≤5%, GC sample proxy 0, 파일 100,000행을 유지한다. 기존 TF guard 비용 +5.71–8.07% 수용은 해당 수정에 한정한다. 제안서의 3%·2%·2일/3세션 조건은 채택하지 않았다. 이 정책 자체는 성능 PASS나 릴리스 승인이 아니다.

## 정식 호스트 수정과 표준 실행

호스트 `Assets/Phase1SampleVerification/TraceForge.HostSample.Tests.asmdef`에서 미사용 `UnityEditor.TestRunner` 참조 하나를 정식 제거했다. 원본과 normalized fixture를 `.approval-20261018/Assets.before`, `Assets.normalized`, `normalization.json`에 보존한다. 이번 세션 종료 후에도 제거 상태를 유지한다. [재생성 절차](../TestHost.md)를 기록했다. 테스트 본문·선택·단정은 변경하지 않는다.

표준 실행은 host의 `run-tests.ps1 -Platform EditMode/PlayMode -Out approval-20261018-<platform>`다. 각각 전체 **32/32**, **92/92**, 실패·스킵 0. 기본 domain/scene reload와 빈 defines, 로컬 root package/UTF1.6.0을 사용한다. 호스트 참조 보정이 지속되는 정상 설정에서 실행한 결과이며, 과거 임시 보정/복원 실패 기록을 소급 변경하지 않는다.

EditMode 전에 Library/Temp를 보존·이동하고 process-local BEE_CACHE_DIRECTORY를 빈 폴더로 격리했다. 네 package assembly의 실제 Csc 실행, Csc CacheHit0, C# warning0/error0을 확인한다. 정확한 XML·컴파일·할당 결과 대조는 아래 최종 검증에 기록한다.

## 측정 전 고정 계획

세션 디렉터리 `.worktrees/native-logger-test-host/.approval-20261018/`의 `frozen-plan.md`가 첫 timing 전에 작성된 계획이다. 실행기와 benchmark source 해시도 실행 전에 보존한다. 비교 source는 reference `f95dcc2`와 위 후보 SHA이며 새 detached worktree는 `.worktrees/approval-baseline-20261018`이다.

- Unity6000.3.8f1/UTF1.6.0, Windows Editor PlayMode batchmode, 두 코드 모두 기본 domain/scene reload. 동일 host/drive, 독립 Unity 20개 프로세스.
- C1 reference, C2 후보 None/capture off, C3 후보 None/capture 구독 on(실제 callback 없음), C4 후보 ErrorAndAbove/Info/capture off. 직접 FileSink.Write(in prebuilt entry) 경계로 Logger guard/policy/callback/caller formatting 비용을 측정하지 않는다.
- 각 구성 5회. 고정 block 순서: **1,2,3,4 / 2,4,1,3 / 3,1,4,2 / 4,3,2,1 / 1,3,2,4**. 매 block 네 구성을 한 번씩 실행한다.
- 보존된 RevalidationBenchmarks.Measure source를 그대로 사용한다. 별도 sink warm-up1000, appendfalse로 재개방, queue101024, timed100000 writes. Producer와 같은 시작점의 Flush completion을 분리한다. GC.Alloc current-thread/capacity16 Count delta는 sample/event proxy이며 bytes가 아니다. accepted/written/실제 행수 각각100000을 검증한다.
- 모든 성공 결과·극단값을 포함한다. timeout300초, nonzero exit, XML 없음, 실패/스킵, 코드/설정 불일치, GCproxy≠0, 기록 수 불일치면 세트를 중단하고 원본을 보존한다. 통계적 outlier 제외·유리한 숫자를 위한 반복/추가 실행은 없다.
- Producer 중앙값 비율 ≤1.05가 기존 기준이다. 판단 불확실성을 숨기지 않기 위해 동일 block의 log(candidate/reference) 5개에 Student-t 양측95% 구간(exp(mean ±2.776445105×sampleSD/√5))을 사전 고정 보조 검사로 사용한다. 중앙값 비율과 상한 모두 ≤1.05면 PASS, 중앙값 비율과 하한 모두 >1.05면 FAIL, 그 외 불충분이다. 이는 허용 회귀율 변경이나 미승인 3%/2% 조건 채택이 아니다. 독립 block·log ratio의 근사 정규성 가정과 작은 표본의 한계를 명시하고 원시 쌍·범위·baseline 시간대 drift를 함께 검토한다. completion에는 이 gate를 적용하지 않는다.
- OS/editor/source/전원/AC/config 불일치는 전체 비교를 불충분으로 다루며 불리한 실행만 제거하지 않는다. CPU 전후 표본은 진단 정보이며 timed region 연속 profile이 아니다. 다른 사용자 작업·전원 정책을 바꾸지 않는다. manifest/lock/ProjectSettings/Assets는 normalized snapshot으로 복원한다.

## 실제 실행 결과와 환경 차단

새 비교의 **완료된 측정은 0/20**이다. producer/completion·GC proxy·100,000행 원시 값, 중앙값, 신뢰구간은 **미검증/계산하지 않음**이다. 기존 성공 수치로 대체하거나 5% 기준을 통과했다고 판정하지 않는다.

| 단계 | 실제 결과 |
|---|---|
| 표준 `approval-20261018-editmode` | 32/32, failed0/skipped0, C# warning/error0 |
| 표준 `approval-20261018-playmode` | 92/92, failed0/skipped0, C# warning/error0 |
| fresh C# compile | 빈 Bee cache + 보존·이동한 Library/Temp, 네 package assembly 실제 Csc, CacheHit0 |
| 비교 준비 첫 시도 | process-name-only 검사에서 Unity Hub의 `resources/unity.exe`를 Editor로 오인해 거절; 측정 프로세스/결과0. Hub 프로세스는 유지하고 Editor 실행 파일 경로 검사로 보정 |
| 고정 계획 B1-C1 시작 | `Start-Process`가 지정 Editor 파일을 찾지 못해 프로세스를 생성하지 못함. XML/log/entries 및 CSV 값0. 세트 중단, 다른 버전이나 유리한 재실행 없음 |

표준 테스트 로그는 Unity **6000.3.8f1**에서의 실제 실행을 입증한다. 그러나 비교 시작 시 `C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Unity.exe`가 더 이상 존재하지 않았다. 이후 기본 `Editor/` 설치 디렉터리의 하위 버전 폴더는 0개, Unity Hub `editors-v2.json`의 등록 목록도 비어 있음을 확인했다. 원인은 단정하지 않는다. 다른 위치의 같은 버전 설치 여부를 사용자에게 문의했다. 자동 설치나 다른 버전 대체는 수행하지 않는다.

`environment-start.json`은 Windows 11 Home 10.0.26200, i7-13700HX(16C/24T), RAM33,240,568KiB, Samsung MZVL21T0HDLU-00BH1 NVMe/SSD, Balanced, 배터리 상태2/100%를 기록한다. 파일의 Editor 필드는 사전 계획의 버전 문자열이며 **비교 프로세스 실행 증거가 아니다**. 시작/종료 전원 표본은 같지만 측정 프로세스가 없으므로 환경 안정성이나 성능 승인 근거로 사용하지 않는다.

두 준비 실패는 `preparation-first-failure.json`, `benchmark-start-failure.json`으로 구분해 보존한다. `frozen-plan.md/json`, 실행기/benchmark 원본·해시, source 해시, 환경, 빈 `raw/` 및 host 복원 자료도 남긴다. 성공 측정을 제외한 사례나 통계적 outlier 제거는 없다. 측정 자체가 시작되지 않았다는 사실과 “측정이 통과했다”를 구분한다.

## 실제 테스트·할당·코드 계약 검토

`standard-host-verification.json`과 XML로 각 플랫폼 전체 결과·실패/스킵0을 확인했다. `case-set-verification.json`은 직전 Edit32/Play92 case와 이번 case 사이의 **누락0/추가0**을 입증한다. TestRunner 선택 인자나 테스트 본문/단정 변경은 없다. 호스트 asmdef의 참조 제거를 정상 상태로 유지한 표준 실행이다.

기존 `LoggerPath_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`는 `loggerPath=0, policy=None, capture=enabled`, `Write_NonSaturatedProducerPath_ProducesZeroGcAllocEvents`는 control29→30/sample1와 measured0(capacity1024)로 통과했다. 이것은 기존 할당 회귀 테스트의 결과이며 새 직접 FileSink 비교20회나 일반적인 0 bytes 보장을 대체하지 않는다.

성능 체크리스트27항목의 코드 범위는 실제 source와 이번 전체 테스트를 대조한다: 1–2/6–7/10/13–25/27의19개 source/test 계약은 충족한다(22: same-thread guard, 27: 기본 reload). 3/8/11/12는 기존 D8 caller guard·오류 fallback formatting·동적 category·D7 error stack 기본값의 승인 예외4개다. 4/5/9/26은 params/lazy overload/StringBuilder/UnityConsoleSink 부재로 N/A4개다. 이 집계를 새 비교 성능 PASS로 확대하지 않는다.

## 현재 판정 및 재개 조건

| 구분 | 판정 |
|---|---|
| 정식 호스트 수정·표준 전체 테스트 | **PASS**, Edit32/Play92, failed0/skipped0 |
| 실제 전체 C# 컴파일 | **PASS**, Csc CacheHit0, warning0/error0 |
| 기존 producer 할당 회귀 | **PASS**, positive control 및 measured proxy0의 검증 범위 |
| 과거 ±10% 역사적 재현 | **기존 FAIL 보존**. 이번 v0.3.0 지정 rig의 필수 release gate에서는 승인된 범위대로 제외 |
| 현재 f95dcc2 대 후보 producer5%/GC/100,000행 비교 | **미검증**, 측정0/20, 지정 Editor 실행 파일 없음 |
| 전체 성능 리뷰 | **BLOCKED**. 정책 반영·코드 검토·기존 테스트가 미실행 비교를 대신하지 않음 |
| release-check 재실행 | **미실행**. 사용자가 지정한 성능 PASS 선행 조건을 충족하지 못함 |
| 릴리스 승인/후속 작업 | **미승인/미수행** |

지정된 6000.3.8f1 Editor의 동일 경로 또는 검증된 다른 설치 경로가 필요하다. 복구 후 실제 editor revision과 candidate/source hash·설정을 재확인하고 새 실행 ID/디렉터리에서 고정된 계획으로 측정한다. 이번 준비 실패 자료는 보존하고 숫자가 유리할 때까지 반복하지 않는다. 현재 차단 사유는 과거 재현성 FAIL의 자동 재적용이 아니라 **새 필수 비교 미검증/Editor 부재**다.

## 산출물 및 독립 리뷰

호스트의 `.approval-20261018/`에 original/normalized fixtures, 구성·source·실행기 해시, 표준 테스트·컴파일·할당 검증 JSON, 사전 계획, 두 준비 실패 및 복원 자료를 보존한다. `final-restoration.json`은 정식 asmdef 변경 외 host mismatch0, normalized asmdef hash 일치, Assets extra0, production/test source mismatch0 및 manifest 복원을 확인한다. 이전 guard/revalidation 보고서와 원시 산출물은 수정하지 않는다. 임시 baseline worktree는 검토 후 정리한다.

독립 `/root/normalization_policy_astra_review`, `astra_review`(요청 GPT-6 Astra/low), 2026-10-18 최종 판정: **accept — 문서·정식 host 수정 및 보고 정확성 승인, 전체 성능 BLOCKED 유지**. 리뷰어는 정책 범위, asmdef의 참조 하나 제거, runner·source hash, 실제 Edit32/Play92 XML(case 누락/추가0), 네 assembly Csc/CacheWrite 및 CacheHit0, C# 진단0, 할당 positive control/proxy0, 빈 raw와 시작 실패/Editor 부재를 대조했다. 수정 요구 없음. 새 producer5%/GC/100,000행 비교와 release-check 진행 근거는 없다는 판정이다. 주 에이전트 직접 수행, Luna worker 없음, token usage unavailable.
