---
name: traceforge-package-audit
description: TraceForge UPM 패키지 구조, 메타데이터, asmdef 설정, Runtime/Editor 분리를 감사합니다. 스캐폴딩 후 또는 릴리스 전에 수동으로 실행하세요.
disable-model-invocation: true
allowed-tools:
  - Read
  - Grep
  - Glob
---

# TraceForge Package Audit

## 목적

TraceForge UPM 패키지가 Unity Package Manager 표준과 프로젝트 컨벤션을 만족하는지 체계적으로 검증합니다.

## 사용 시점

- 초기 스캐폴딩 완료 후
- 릴리스 전 (traceforge-release-check 실행 전 선행)
- asmdef 추가/변경 후

## 실행 프로세스

1. `checklist.md`를 읽어 36개 항목을 확인
2. 각 항목에 대해 Read/Grep/Glob 도구로 실제 파일을 검증
3. 각 항목을 **PASS** / **FAIL** / **N/A** 로 표시
4. FAIL 항목에는 파일 경로와 수정 방향을 함께 기록

## 출력 형식

```
## Package Audit Report
날짜: [YYYY-MM-DD]

### PASS
- [ ] [항목 설명]

### FAIL
- [ ] [항목 설명]: [실패 이유] → 수정: [제안]

### N/A
- [ ] [항목 설명]: [해당 없는 이유]

**요약**: X/36 통과 (Y개 N/A)
```

## 패키지 루트 경로

`Packages/com.traceforge.core/` 또는 레포 루트 (UPM git 설치 구조에 따라 다름)
