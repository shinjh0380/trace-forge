---
name: traceforge-release-merge
description: dev→main 안전 머지 워크플로 — Claude 툴링 파일(.claude/, CLAUDE.md) 제외 후 UPM 클린 상태로 main에 병합. 릴리스 또는 main 업데이트 시 사용.
disable-model-invocation: true
allowed-tools:
  - Bash
  - Read
  - Glob
---

# TraceForge Release Merge

## 목적

dev 브랜치의 변경사항을 main에 병합할 때, Claude 툴링 파일이 main에 포함되지 않도록
안전하게 처리합니다. **이 스킬 없이 main에 직접 머지하면 .claude/, CLAUDE.md가 노출됩니다.**

## 사용 시점

- `dev → main` 머지가 필요할 때
- 버전 릴리스 (traceforge-version-bump 스킬이 내부적으로 호출)
- main 브랜치 수동 업데이트 시

## 브랜칭 규칙

- **main**: UPM 패키지 전용 — Claude 파일 없음, `.gitignore`에 `.claude/`, `CLAUDE.md` 포함
- **dev**: 작업 브랜치 — 모든 파일 포함, main보다 일반적으로 1+ 커밋 앞에 있음
- 머지 방향: `dev → main` 단방향 (main → dev 역머지 금지)

## 실행 프로세스

### 사전 확인

1. **현재 브랜치 확인**: dev에 있는지 확인
   ```bash
   git branch --show-current
   ```

2. **dev 클린 상태 확인**: 미커밋 변경사항 없는지 확인
   ```bash
   git status
   ```
   - 미커밋 변경사항 있으면 중단. 먼저 커밋 또는 스태시.

3. **main 최신 상태 확인**: (원격이 있는 경우)
   ```bash
   git fetch origin main
   git log origin/main..main --oneline
   ```

### 머지 실행

4. **main으로 전환**:
   ```bash
   git checkout main
   ```

5. **dev를 no-commit no-ff로 머지**:
   ```bash
   git merge dev --no-commit --no-ff
   ```

6. **Claude 파일 unstage** (핵심 단계):
   ```bash
   git reset HEAD -- CLAUDE.md .claude/
   ```

7. **main의 .gitignore 복원** (dev의 .gitignore로 덮어쓰여졌을 경우):
   ```bash
   git checkout main -- .gitignore
   ```

### 검증

8. **staged 내용에 Claude 파일 없는지 확인**:
   ```bash
   git diff --cached --name-only
   ```
   - `.claude/` 또는 `CLAUDE.md` 포함 시 즉시 중단, 해당 파일 unstage 후 재시도

9. **staged 내용 최종 확인**: 변경사항이 올바른지 검토

### 커밋

10. **커밋 생성**:
    ```bash
    git commit -m "release: merge dev into main vX.Y.Z"
    ```
    - 커밋 메시지에 버전 명시 (알고 있는 경우)

### 선택적: 태그 생성

11. 버전 릴리스인 경우:
    ```bash
    git tag vX.Y.Z
    ```

12. **dev로 복귀**:
    ```bash
    git checkout dev
    ```

## 출력 형식

```
## Release Merge Report
날짜: [YYYY-MM-DD]
머지: dev → main

### 단계별 결과
- [x] dev 클린 상태 확인
- [x] git merge dev --no-commit --no-ff
- [x] Claude 파일 unstage (CLAUDE.md, .claude/)
- [x] .gitignore 복원
- [x] staged에 Claude 파일 없음 확인
- [x] 커밋 완료

### 커밋
SHA: [hash]
메시지: [커밋 메시지]

---
**결과**: SUCCESS / ABORTED
```

## 중단 조건

다음 상황에서 즉시 중단하고 사용자에게 알립니다:

- dev에 미커밋 변경사항 존재
- 머지 충돌 발생 (수동 해결 후 재시도)
- 8단계 검증에서 `.claude/` 또는 `CLAUDE.md`가 staged 상태
- main의 .gitignore에 Claude 제외 규칙이 없는 경우
