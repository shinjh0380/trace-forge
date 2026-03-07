---
name: traceforge-api-consistency
description: TraceForge 퍼블릭 API 표면 검사 — 네이밍, 가시성, 메서드 시그니처, 용어 일관성, Unity 정렬, 문서화. API 추가/변경 시 실행.
allowed-tools:
  - Read
  - Grep
  - Glob
---

# TraceForge API Consistency Review

## 목적

퍼블릭 API가 CLAUDE.md에 정의된 컨벤션과 `api-guidelines.md`의 23개 기준을
일관되게 준수하는지 검증합니다.

## 사용 시점

- 새로운 퍼블릭 타입/메서드 추가 후
- 기존 API 시그니처 변경 후
- PR 리뷰 전

## 실행 프로세스

1. `api-guidelines.md`를 읽어 23개 항목 확인
2. Grep으로 퍼블릭 API 표면 추출 (`public`, `interface`, `enum` 키워드)
3. 각 가이드라인 항목 대비 검토
4. 불일치 항목은 구체적인 파일:라인 참조와 함께 기록

## API 표면 추출 예시

```
# 모든 퍼블릭 타입
Grep: "^public (class|interface|struct|enum)" Runtime/

# 퍼블릭 메서드
Grep: "public.*void Log|public.*void Write" Runtime/
```

## 출력 형식

```
## API Consistency Report
날짜: [YYYY-MM-DD]
검토 범위: [추가/변경된 API]

### 위반 (수정 필요)
- [파일:라인] [위반 내용] → 수정: [올바른 형태]

### 경고 (논의 필요)
- [파일:라인] [우려 사항]

### 통과
- [API 이름]: [준수 항목]

**요약**: X/23 기준 충족
```
