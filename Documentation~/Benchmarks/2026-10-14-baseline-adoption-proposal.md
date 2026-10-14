# 새 성능 기준선 채택 정책 제안

## 상태와 기존 판정

**제안이며 채택 승인은 아직 없다.** 이번 수정이나 측정 수치를 새 기준선으로 자동 채택하지 않는다. 기존 Phase 4의 역사적 rig 재현성 ±10%와 producer 회귀 5% 기준은 그대로 유지한다. 기존 보고서의 FAIL과 전체 성능 승인 BLOCKED를 바꾸지 않는다.

[2026-10-13 재검증](2026-10-13-native-logger-performance-revalidation.md)에서 역사적 producer/completion은 -25.7227%/-34.9983%로 재현에 실패했다. 같은 baseline 코드도 공식→교대 실행의 producer 중앙값이 약 **-12.55%**, completion이 약 -8.96% 변했다. 빠른 값이나 한 번의 중앙값 비교로 rig 안정성을 주장할 수 없다. 순서/시간 효과는 관측됐지만 원인은 특정되지 않았다.

## 복원할 수 없는 과거 조건

- 당시 실제 transient benchmark/호스트 원본, 실제 파일 위치와 저장장치 상태.
- 전원 모드, AC/배터리 상태, CPU 클럭·온도·스로틀링, 백그라운드 부하와 파일 캐시 상태.
- 당시 실제 domain reload 설정. 계획 초안과 보고서의 timer/GC 계측·실행 플랫폼 차이도 남아 있다.

이 조건을 추정값으로 메워 과거 환경과 동일하다고 하지 않는다. 기존 수치와 실패 기록은 역사적 자료로 계속 보존한다.

## 새 reference rig에 고정할 조건

1. 승인 대상 commit SHA, runner/test 소스 해시, Unity 정확한 버전·revision, Test Framework/패키지 lock, OS build, CPU/RAM/SSD 모델, scripting backend/아키텍처/defines를 기록한다. 재귀 버그가 있는 commit을 안전성 기준으로 채택하지 않는다.
2. 전용 호스트와 출력 경로, domain/scene reload, queue/entry/message/stack/capture 상태를 명시한다. 테스트용 설정·Assets의 시작/종료 해시를 확인한다. 파일 캐시는 OS 기본 상태로 고정하고 cache flush를 했다고 주장하지 않는다.
3. 전원 구성표와 AC 상태를 고정하고 충전/절전 전환을 피한다. 시작 전 안정화 시간과 CPU 유휴 기준을 정한다(제안: 5분 안정화, 60초 평균 CPU <5%). 온도/클럭 센서가 없으면 측정 불가로 표시한다. 의도하지 않은 업데이트/부하 발생 시 세트를 무효로 표시하되 원본은 보존한다.
4. runner·순서·횟수·중단/재실행 규칙을 첫 실행 전에 고정한다. 성공 결과는 극단값도 전부 포함하고 실패·타임아웃·환경 무효 결과는 이유와 함께 별도 ID로 보존한다.

## 측정 경계와 반복 안정성

- 기존 직접 FileSink 프로토콜은 별도 유지한다: warm-up 1,000, prebuilt entry 100,000, queue 101,024, 실제 파일 100,000행, producer와 Flush 완료 시간을 분리한다. 기존 5% producer 비교를 completion에 적용하지 않는다.
- 추가 reference 후보는 실제 `TF.Info`/`Logger.WriteCaptured` 경로다. filtering/timestamp/policy/entry/dispatch를 포함하고, counting sink와 FileSink를 별도 세트로 둬 dispatch 비용과 I/O를 혼동하지 않는다. None/ErrorAndAbove(Info)/capture 구독 on/off를 분리한다. Unity callback, Error stack, caller formatting, Player/mobile은 별도 측정 없이는 승인 범위 밖이다.
- 최소 2일, 총 3개 세션에서 매 구성 5쌍 이상의 독립 프로세스를 AB/BA로 균형 배치한다. 동일 reference를 세션 첫/중간/끝에 반복해 시간 드리프트를 감시한다. 순서·원시 값·중앙값·범위·쌍별 비율·95% 신뢰구간 산출 방법을 함께 고정한다.
- **승인이 필요한 안정성 조건 제안:** reference block 중앙값 변동 ≤3%, paired producer 비율의 95% 구간 반폭 ≤2%. 초과 시 5% 회귀 판정은 불충분으로 두고 환경/횟수를 다시 설계한다. 12.55% drift가 있던 이번 rig는 이 안정성을 이미 입증한 것으로 볼 수 없다. 새 조건 역시 아직 승인된 gate가 아니다.
- GC.Alloc recorder는 current-thread sample/event proxy로 표기한다. 기존 positive-control 할당 회귀 검증도 병행하며 proxy 0을 모든 경로의 0 bytes로 확대하지 않는다.

## 대체 범위와 승인 조건

채택하려면 사용자가 **정확한 reference SHA/환경/수치·적용 플랫폼/측정 경계·유효 기간·재측정 조건**을 승인해야 한다. 독립 리뷰는 runner와 원시 결과·안정성을 대조한다. 기준선 채택, 코드 안전성, 전체 성능 승인, 릴리스 승인은 별개다.

제안하는 대체 범위는 승인 이후 지정된 Windows Editor rig의 미래 비교에 한한다. 역사적 ±10% 실패를 PASS로 소급 변경하지 않는다. 과거 재현 실패를 릴리스 gate에서 어떻게 처리할지는 별도 명시적 결정(예: 실패를 유지한 채 지정 rig에 한정한 예외와 새 reference 허용)이 필요하다. 현재 ±10% gate가 존재하는 동안 이 문서만으로 우회할 수 없다.

기존 5% 직접 FileSink producer 기준은 유지한다. 실제 TF/Logger 진단에 별도 승인 한계가 필요하면 안정성 증거를 확인한 뒤 정한다. 이번 guard 비용 숫자는 reference 채택 대상 확정이나 이 한계의 통과 판정이 아니다. 새 Unity/OS/하드웨어, 전원 정책, runner/출력 경계 변경 또는 안정성 실패 시 다시 reference 승인 절차를 거친다.
