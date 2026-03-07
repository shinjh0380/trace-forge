---
name: traceforge-research
description: TraceForge 아키텍처 의사결정을 위한 격리된 딥 리서치. UPM 구조, Unity 6.3 API, sink 아키텍처 대안, 성능 기준 조사. 설계 결정 전 실행.
disable-model-invocation: true
context: fork
agent: Explore
---

# TraceForge Research

## 목적

아키텍처 결정을 내리기 전, 관련 기술/패턴/제약을 체계적으로 조사하여
근거 있는 설계 선택을 지원합니다.

## 사용 시점

- 새로운 sink 타입 설계 전
- 성능 최적화 전략 선택 전
- Unity API 의존성 도입 검토 시
- 서드파티 라이브러리 채택 검토 시
- UPM 배포 구조 변경 검토 시

## 리서치 범위

`unity-upm-notes.md` — Unity 6.3, UPM, sink 아키텍처, 성능 기준 참조 자료

## 실행 프로세스

1. `unity-upm-notes.md`와 `research-output-format.md`를 읽기
2. 조사 질문을 명확하게 정의
3. Glob/Grep으로 기존 코드에서 관련 패턴 수집
4. `research-output-format.md`의 형식으로 결과 작성

## 격리 실행 (context: fork)

이 스킬은 메인 컨텍스트와 격리된 포크에서 실행됩니다.
리서치 중 수집한 대량의 데이터가 주 세션을 오염시키지 않습니다.
**리서치 결과만 메인 세션으로 반환합니다.**

## 출력

`research-output-format.md`의 구조에 따라 작성된 리서치 보고서.
보고서는 직접 의사결정에 사용 가능한 형태여야 합니다.
