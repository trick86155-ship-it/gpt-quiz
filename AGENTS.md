# LunaQuiz 배포

- 사용자 요청으로 업데이트를 완료하면 검증 후 기존 공개 저장소 https://github.com/trick86155-ship-it/gpt-quiz 에 소스 변경을 업로드한다.
- 새 배포에서는 VERSION.txt의 패치 버전을 올리고 Publish-Luna.ps1로 LunaQuiz.exe와 체크섬을 GitHub Releases에 게시한다. 이미 배포한 버전의 파일을 덮어쓰지 않는다.
- 현재 실행 중인 LunaQuiz.exe 때문에 빌드가 잠기면 사용자에게 창 종료를 요청한다. 별도 이름의 배포 파일을 만들지 않는다.
- 백업을 만들기 전에는 사용자에게 묻고 명시적 승인을 받는다. 승인된 새 백업을 확인한 뒤 같은 원본의 Codex 백업은 최신 1개만 남긴다.
