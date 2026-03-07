---
name: traceforge-version-bump
description: TraceForge semver 버전 범프 워크플로 — package.json 버전 수정, CHANGELOG.md 업데이트, dev 커밋, main 머지, Git 태그 생성. 버전 업그레이드 시 사용.
disable-model-invocation: true
allowed-tools:
  - Read
  - Edit
  - Bash
  - Glob
---

# TraceForge Version Bump

## 목적

TraceForge의 semver 버전을 안전하게 범프합니다. package.json과 CHANGELOG.md를
함께 업데이트하고, dev 브랜치에 커밋 후 `traceforge-release-merge` 스킬로
main에 병합하고 Git 태그를 생성합니다.

## 사용 시점

- 새 기능 추가 후 minor 버전 범프 (0.1.0 → 0.2.0)
- 버그 수정 후 patch 버전 범프 (0.1.0 → 0.1.1)
- 하위 호환 불가 API 변경 시 major 버전 범프 (0.x.x → 1.0.0)

## 선행 조건

이 스킬 실행 전에 다음이 완료되어야 합니다:

- `traceforge-release-check` — APPROVED
- dev 브랜치 클린 상태 (미커밋 변경사항 없음)
- CHANGELOG.md의 `[Unreleased]` 섹션에 변경사항 기술됨

## 실행 프로세스

### 1. 현재 버전 확인

```bash
git branch --show-current   # dev인지 확인
```

`package.json`의 `version` 필드(L3) 읽기:
```bash
grep '"version"' package.json
```

### 2. 범프 유형 결정

사용자에게 범프 유형 확인 (이미 지정된 경우 생략):

| 유형 | 예시 | 사용 시점 |
|------|------|----------|
| patch | 0.1.0 → 0.1.1 | 버그 수정, 성능 개선 |
| minor | 0.1.0 → 0.2.0 | 하위 호환 새 기능 |
| major | 0.1.0 → 1.0.0 | 하위 호환 불가 변경 |

### 3. package.json 버전 수정

`package.json`의 version 필드를 새 버전으로 수정:
```json
"version": "X.Y.Z",
```

### 4. CHANGELOG.md 업데이트

`CHANGELOG.md`를 읽어 `[Unreleased]` 섹션을 `[X.Y.Z] - YYYY-MM-DD`로 변환:

변경 전:
```markdown
## [Unreleased]

### Added
- 새 기능 설명
```

변경 후:
```markdown
## [Unreleased]

## [X.Y.Z] - YYYY-MM-DD

### Added
- 새 기능 설명
```

새 빈 `[Unreleased]` 섹션은 최상단에 유지.

### 5. dev 브랜치에 커밋

```bash
git add package.json CHANGELOG.md
git commit -m "chore: bump version to X.Y.Z

버전 X.Y.Z 릴리스.
주요 변경사항: [한 줄 요약]"
```

### 6. main 머지 (traceforge-release-merge 스킬 실행)

`traceforge-release-merge` 스킬의 프로세스를 따라 dev → main 머지:
- Claude 파일 제외 확인 필수
- 커밋 메시지: `release: merge dev into main vX.Y.Z`

### 7. Git 태그 생성

main 브랜치에서:
```bash
git tag vX.Y.Z
```

선택적으로 annotated 태그 사용:
```bash
git tag -a vX.Y.Z -m "TraceForge vX.Y.Z"
```

### 8. dev로 복귀

```bash
git checkout dev
```

## 출력 형식

```
## Version Bump Report
이전 버전: vA.B.C
새 버전: vX.Y.Z
범프 유형: patch / minor / major
날짜: YYYY-MM-DD

### 완료된 단계
- [x] package.json 버전 수정
- [x] CHANGELOG.md [Unreleased] → [X.Y.Z] 이동
- [x] dev 브랜치 커밋
- [x] main 머지 (traceforge-release-merge)
- [x] git tag vX.Y.Z

---
**결과**: SUCCESS / ABORTED
**다음 단계**: git push origin main dev --tags (원격 배포 시)
```

## 중단 조건

- dev가 아닌 브랜치에서 실행 시도
- CHANGELOG.md에 `[Unreleased]` 섹션이 없거나 비어 있음
- `traceforge-release-check` APPROVED 확인 불가
- main 머지 중 충돌 발생
