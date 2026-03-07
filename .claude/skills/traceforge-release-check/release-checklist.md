# TraceForge Release Checklist

릴리스 버전: `vX.Y.Z`

**판정 규칙**: 버저닝/테스트/라이선스 카테고리에서 단 하나의 FAIL도 릴리스를 차단합니다.

---

## 버저닝 — 5항목

- [ ] 1. `package.json`의 `version`이 semver (`major.minor.patch`) 형식
- [ ] 2. `CHANGELOG.md` 상단에 `## [X.Y.Z] - YYYY-MM-DD` 섹션 추가됨
- [ ] 3. `[Unreleased]` 섹션 내용이 새 버전 섹션으로 이동됨 (Unreleased는 비워짐)
- [ ] 4. `package.json`의 `version`과 CHANGELOG 최신 버전이 일치
- [ ] 5. 이전 버전과 비교 링크가 CHANGELOG 하단에 추가됨
  - 형식: `[X.Y.Z]: https://github.com/.../compare/vA.B.C...vX.Y.Z`

---

## 체인지로그 — 4항목

- [ ] 6. 변경 내용이 `Added`/`Changed`/`Deprecated`/`Removed`/`Fixed`/`Security` 섹션으로 구분됨
- [ ] 7. 브레이킹 체인지가 `**BREAKING**:` 또는 별도 섹션으로 명시됨
- [ ] 8. 내용이 사용자 관점으로 작성됨 (내부 리팩토링 제목 X, 사용자 영향 중심)
- [ ] 9. 브레이킹 체인지가 있는 경우 마이그레이션 가이드가 CHANGELOG 또는 README에 있음

---

## 라이선스 — 3항목 🚫 FAIL 시 차단

- [ ] 10. `LICENSE.md` 또는 `LICENSE` 파일 존재
- [ ] 11. 라이선스 파일의 연도가 현재 연도 포함
- [ ] 12. `package.json`의 `license` 필드와 LICENSE 파일의 라이선스 유형 일치

---

## 메타데이터 — 5항목

- [ ] 13. `package.json`의 모든 필수 필드 완성 (name, version, unity, displayName, description, author, license)
- [ ] 14. `unity` 필드가 최소 지원 버전 정확히 반영 (`6000.3` for Unity 6.3)
- [ ] 15. `keywords`가 현재 기능을 반영하여 최신 상태
- [ ] 16. `repository.url`이 올바른 GitHub URL
- [ ] 17. `documentationUrl`이 설정된 경우 접근 가능한 URL

---

## 문서 — 5항목

- [ ] 18. `README.md`의 설치 가이드가 새 버전 기준으로 정확
- [ ] 19. 새로 추가된 퍼블릭 API가 README 또는 API 문서에 반영됨
- [ ] 20. 브레이킹 체인지가 있는 경우 README에 마이그레이션 섹션 있음
- [ ] 21. `Samples~/BasicUsage/`가 현재 퍼블릭 API로 업데이트됨
- [ ] 22. 새 퍼블릭 API에 XML 문서 주석 (`<summary>`, `<param>`) 완비됨

---

## 코드 품질 — 6항목

- [ ] 23. `TODO`, `FIXME`, `HACK`, `XXX` 주석이 없음 (미해결 작업 없음)
  - Grep: `TODO|FIXME|HACK|XXX` in `Runtime/`, `Editor/`
- [ ] 24. 사용되지 않는 `using` 디렉티브가 없음
- [ ] 25. 컴파일러 경고 수가 0 (빌드 로그 확인)
- [ ] 26. `#if DEBUG` 블록이 릴리스 빌드에 포함되지 않거나 의도된 것
- [ ] 27. `UnityEngine.Debug.Log` 직접 호출이 패키지 코드에 없음
  - Grep: `Debug\.Log[^S]` in `Runtime/` (Debug.LogSink 제외)
- [ ] 28. `UnityEngine.Debug` 사용이 `UnityConsoleSink` 외에 없음

---

## 테스트 — 5항목 🚫 FAIL 시 차단

- [ ] 29. 모든 Edit Mode 테스트 통과 (Unity Test Runner)
- [ ] 30. 모든 Play Mode 테스트 통과 (Unity Test Runner)
- [ ] 31. 이번 릴리스에 추가된 기능에 대한 테스트 존재
- [ ] 32. 엣지 케이스 테스트 존재: null category, empty message, disabled logger, no sinks
- [ ] 33. 성능 회귀 테스트 통과 (있는 경우)

---

## 샘플 — 2항목

- [ ] 34. `Samples~/BasicUsage/`가 현재 API로 컴파일 오류 없이 임포트됨
- [ ] 35. 샘플 코드에 사용법 설명 주석이 있음

---

## Git 상태 — 3항목

- [ ] 36. 작업 트리가 클린 (`git status` — nothing to commit)
- [ ] 37. main 브랜치에 릴리스 커밋이 머지 완료됨
- [ ] 38. 이전 버전 이후 모든 커밋이 이 릴리스에 의도적으로 포함됨
  - `git log vPREV..HEAD --oneline` 으로 확인
