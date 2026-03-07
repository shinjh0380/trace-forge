---
name: traceforge-release-check
description: TraceForge 릴리스 최종 관문 — 버저닝, changelog, 라이선스, 코드 품질, 테스트, 샘플, Git 상태. 버전 태그 생성 전 반드시 실행.
disable-model-invocation: true
allowed-tools:
  - Read
  - Grep
  - Glob
---

# TraceForge Release Check

## 목적

버전 태그를 생성하기 전, TraceForge 릴리스가 모든 품질 기준을 충족하는지
38개 체크리스트로 최종 검증합니다. **이 스킬이 FAIL을 반환하면 릴리스를 중단합니다.**

## 사용 시점

- `git tag vX.Y.Z` 실행 전
- npm publish / OpenUPM 배포 전
- main 브랜치 머지 후 릴리스 PR 승인 전

## 선행 조건

이 스킬 실행 전에 다음이 완료되어야 합니다:
- `traceforge-package-audit` — PASS
- `traceforge-api-consistency` — PASS
- `traceforge-performance-review` — PASS (있는 경우)

## 실행 프로세스

1. `release-checklist.md`를 읽어 38개 항목 확인
2. Read/Grep/Glob으로 각 항목 검증
3. 하나라도 FAIL이면 릴리스 중단 권고
4. 모두 PASS/N/A면 릴리스 승인

## 출력 형식

```
## Release Check Report
버전: [vX.Y.Z]
날짜: [YYYY-MM-DD]

### FAIL (릴리스 중단)
- [ ] [항목]: [이유]

### PASS
- [x] [항목]

### N/A
- [ ] [항목]: [이유]

---
**판정**: APPROVED / BLOCKED
**통과**: X/38 (Y개 N/A)
```

## 릴리스 차단 조건

다음 카테고리에서 단 하나의 FAIL도 릴리스를 차단합니다:
- 버저닝 불일치
- 테스트 실패
- 컴파일러 경고 존재
- LICENSE.md 없음
