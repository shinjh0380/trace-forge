---
name: traceforge-performance-review
description: TraceForge 런타임 성능 검사 — disabled log path 할당, 문자열 처리, sink 디스패치 오버헤드, 스레딩. 코어 로직 변경 시 실행.
allowed-tools:
  - Read
  - Grep
  - Glob
---

# TraceForge Performance Review

## 목적

TraceForge의 핵심 성능 원칙인 **zero-allocation on disabled path**가 유지되는지,
그리고 enabled path에서도 불필요한 할당이 최소화되는지 검증합니다.

## 사용 시점

- Logger 코어 (`Logger.cs`, `LogEntry`) 변경 후
- 새로운 sink 추가 후
- 필터링 로직 변경 후
- 성능 회귀가 의심될 때

## 실행 프로세스

1. `performance-checklist.md`를 읽어 27개 항목을 확인
2. 각 항목에 대해 실제 코드를 Grep/Read로 검토
3. 문제 발견 시 구체적인 파일:라인 참조와 함께 기록
4. 수정 제안 포함하여 보고

## 핵심 원칙

```
verbosity 체크 → category 체크 → 문자열 생성 → sink 디스패치
       ↑ 여기서 조기 탈출해야 함 (할당 없이)
```

## 출력 형식

```
## Performance Review Report
날짜: [YYYY-MM-DD]
검토 범위: [변경된 파일 목록]

### 위험: 할당 발생 (즉시 수정)
- [파일:라인] [문제 설명] → 수정: [방법]

### 경고: 최적화 권장
- [파일:라인] [개선 가능 사항]

### 통과
- [항목] — [근거]

**요약**: X/27 통과, Y개 위험, Z개 경고
```
