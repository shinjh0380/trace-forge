# TraceForge Package Audit Checklist

패키지 루트: `Packages/com.traceforge.core/`

---

## 메타데이터 (package.json) — 8항목

- [ ] 1. `name` 필드가 `com.traceforge.core` 형식 (scoped, lowercase, dots)
- [ ] 2. `version` 필드가 semver 형식 (`major.minor.patch`, 예: `0.1.0`)
- [ ] 3. `unity` 필드가 `6000.3` 이상 (Unity 6.3 LTS)
- [ ] 4. `displayName`이 사람이 읽을 수 있는 이름 (`TraceForge` 또는 `TraceForge - Logging`)
- [ ] 5. `description`이 패키지 기능을 1–2문장으로 명확히 설명
- [ ] 6. `author.name`, `author.email`, `author.url` 모두 설정됨
- [ ] 7. `license` 필드가 `MIT` (또는 사용 중인 라이선스와 일치)
- [ ] 8. `keywords` 배열에 `logging`, `traceforge`, `unity` 포함

---

## asmdef 설정 — 8항목

- [ ] 9. Runtime asmdef 이름이 `TraceForge.Runtime` (파일명: `TraceForge.Runtime.asmdef`)
- [ ] 10. Editor asmdef 이름이 `TraceForge.Editor` (파일명: `TraceForge.Editor.asmdef`)
- [ ] 11. Runtime asmdef의 `includePlatforms`가 빈 배열 `[]` (모든 플랫폼 지원)
- [ ] 12. Editor asmdef의 `includePlatforms`가 `["Editor"]`
- [ ] 13. Editor asmdef가 `references`에 `TraceForge.Runtime` GUID 포함
- [ ] 14. Tests asmdef의 `allowUnsafeCode`가 `false`
- [ ] 15. Runtime asmdef의 `references`에 외부 패키지 없음 (zero-dependency)
- [ ] 16. `autoReferenced` 설정이 의도에 맞음 (Runtime: `true`, Editor: `false`)

---

## Runtime/Editor 분리 — 6항목

- [ ] 17. `UnityEditor` namespace 사용 코드가 `Editor/` 폴더에만 존재
  - Grep: `using UnityEditor` in `Runtime/`
- [ ] 18. `#if UNITY_EDITOR` 블록이 Runtime 코드에 없음
  - Grep: `UNITY_EDITOR` in `Runtime/`
- [ ] 19. Runtime에서 `UnityEditor.dll` 어셈블리 참조 없음
- [ ] 20. TraceForge Settings window 코드가 `Editor/Settings/` 아래에 있음
- [ ] 21. 샘플 폴더가 `Samples~/` (물결표 포함, UPM 표준)
- [ ] 22. Tests가 `Tests/Runtime/`과 `Tests/Editor/`로 분리됨

---

## 문서 — 8항목

- [ ] 23. `README.md` 존재 + 패키지 루트에 위치
- [ ] 24. `README.md`에 설치 방법 (Git URL 또는 OpenUPM) 포함
- [ ] 25. `README.md`에 Quick Start 코드 예시 포함
- [ ] 26. `CHANGELOG.md` 존재 + Keep a Changelog 형식
- [ ] 27. `CHANGELOG.md`에 `[Unreleased]` 섹션 존재
- [ ] 28. `LICENSE.md` 또는 `LICENSE` 파일 존재
- [ ] 29. 퍼블릭 API (`public` 타입/메서드)에 XML 문서 주석 (`<summary>`) 있음
  - Grep: `public.*\n.*(?!.*///)` (주석 없는 public 선언)
- [ ] 30. `Samples~/BasicUsage/`에 최신 API 사용 예시 있음

---

## 설치성 — 6항목

- [ ] 31. `package.json`의 `repository.url`이 올바른 GitHub URL
- [ ] 32. Git URL 설치 구조 확인 (패키지 루트가 레포 루트 또는 `Packages/com.traceforge.core/`)
- [ ] 33. 불필요한 파일이 배포에서 제외됨 (`.npmignore` 또는 `package.json`의 `files` 필드)
- [ ] 34. 모든 에셋 파일에 `.meta` 파일 존재
  - Glob: `Runtime/**/*.cs` → 대응하는 `.meta` 확인
- [ ] 35. asmdef 파일들의 GUID가 고유하고 다른 프로젝트와 충돌 없음
  - Grep: `guid:` in `.meta` files
- [ ] 36. `package.json`의 `documentationUrl` 설정됨 (있는 경우)
