# zpd-client

Unity 6000.3.11f1 TCP 클라이언트와 FIFO 매칭 예제입니다.

## API 구현 문서

- [API 구현 명세 초안](Docs/API_IMPLEMENTATION.md): 로비·소셜·자동 하트·캐릭터·장비 라우트, 기존 TCP 매칭 연결, 사망 시 킬 수 기반 경험치 정산 및 미결정 정책.
- [클라이언트 작업 목록](Docs/CLIENT_TASKS.md): 현재 코드 기준의 미구현 작업, 우선순위, 수정 위치와 완료 기준.

HTTP API와 신규 전투/정산 이벤트는 제안 단계이며 아직 구현되지 않았습니다.

## 로비

`Assets/Scenes/Lobby.unity`를 열어 Play하거나, `ZPD > Lobby > Create Lobby Scene` 메뉴로
로비 씬을 새로 생성할 수 있습니다. 중앙 캐릭터, 프로필·전적, 친구/검색/최근 접속 유저,
하단 인벤토리, 전투 입장 및 캐릭터 변경 버튼을 제공합니다. 최근 접속 추천 목록에서는
친구 요청을 보낼 수 있고 새로고침을 지원합니다. 친구 창에서 가능한 하트는 자동으로
수령·전송하는 흐름이며, 현재는 API 로그만 출력합니다. 화면은 에디터에서
생성·저장하며 API 연동 전 데이터는 모두 placeholder입니다. 작업 지점은 로그만 출력하고,
서버 대상/가능 여부/소유 정보가 없는 동작은 비활성입니다. 영어 UI를 사용합니다.

자세한 사용법은 [로비 가이드](Assets/Scripts/Lobby/README.md),
외부 리소스와 직접 제작한 UI 원화의 출처는 [크레딧](Assets/ThirdParty/CREDITS.md)을 확인하세요.

## 실행

1. 업데이트된 `../zpd-server/out/build/windows-x64/Debug/zpd-server.exe`를 실행합니다. 이전 서버가 실행 중이면 종료 후 새로 실행합니다.
2. Unity에서 `Assets/Scenes/ConnectionTest.unity`를 열고 Play → Connect를 누릅니다.
3. 연결 후 자동으로 매칭 요청을 보내고 Waiting 상태로 대기합니다.
4. 다른 클라이언트에서도 Connect를 누릅니다. 먼저 대기한 2명이 같은 Session ID와 참가자 목록을 받습니다.

두 클라이언트는 Unity Editor와 Standalone 빌드를 함께 실행하면 됩니다. 주소와 초기 포트는 ConnectionTest Inspector에서 설정합니다. 실행 중에는 화면의 Port 입력창에서 1~65535 범위의 포트를 입력한 뒤 Connect를 누릅니다. 연결 중에는 포트를 변경할 수 없으며 Disconnect 후 변경할 수 있습니다. 서버가 실제로 사용하는 포트와 같아야 합니다. 서로 다른 PC라면 같은 서버 IP를 사용합니다. 기본값은 `127.0.0.1:20000`입니다. WebGL은 지원하지 않습니다.

## 매칭

- 연결 → MatchRequest → 서버 대기 큐 → TryMatchPlayers → MatchFound → 세션 참가.
- TCP 연결만으로는 대기 큐에 들어가지 않습니다. 클라이언트가 연결 이벤트에서 자동으로 매칭 요청을 보냅니다.
- Cancel match는 대기 큐에서 빠집니다. Find match는 큐의 맨 뒤에 다시 들어갑니다.
- Leave session은 현재 세션에서 나갑니다. 다시 매칭하려면 Find match를 누릅니다.
- 대기 중 연결 종료는 큐에서 제거합니다. 세션에서 연결이 끊기면 남은 참가자에게 퇴장 알림을 보냅니다.
- 세션은 참가자가 모두 나가면 삭제됩니다. 빈 자리를 자동으로 채우거나 남은 참가자를 자동 재매칭하지 않습니다.
- 세션 ID와 플레이어 ID는 서버 실행 동안만 유효합니다. 인증이나 세션 복구 기능은 없습니다.

매칭 인원은 서버 `server/include/ServerLimits.hpp`의 `PlayersPerSession`으로 설정합니다. 현재는 2명이며 2~16명 범위로 바꾸고 서버를 다시 빌드합니다. 매칭 정책은 `server/src/PacketHandler.Matchmaking.cpp`의 `TryMatchPlayers`에서 변경합니다.

## 코드 구조

- `Assets/Scripts/Networking/NetworkClient.cs`: 비동기 TCP 송수신, 연결별 요청 ID, 이벤트 큐.
- `Assets/Scripts/Networking/MatchmakingClient.cs`: 매칭 요청/응답, 세션 상태, 참가자 목록.
- `Assets/Scripts/Development/PacketHandler.cs`: HandleConnected, HandlePacketReceived, HandleDisconnected.
- `Assets/Scripts/Development/ConnectionTest.cs`: 큰 글씨의 테스트 UI와 메인 스레드 이벤트 처리.
- 서버 `PacketHandler.cpp`: 연결 및 메시지 분기. 매칭 처리는 `PacketHandler.Matchmaking.cpp`로 분리합니다.

기존 Send message는 서버와 UTF-8 에코를 확인하는 기능입니다. 같은 세션 참가자에게 텍스트를 전달하는 채팅 기능은 아직 아닙니다. 매칭 전용 메시지는 별도 Protobuf 본문을 사용하며 수동 텍스트 전송에서 예약 코드를 차단합니다.

## 프로토콜

8바이트 big-endian 헤더와 최대 4088바이트 본문을 유지합니다. 매칭 본문은 서버 `common/proto/matchmaking.proto`가 원본입니다.

| 요청 | 응답 | 용도 |
|---|---|---|
| 16 MatchRequest | 144 MatchResponse | 대기 큐 참가, player_id 반환 |
| 17 CancelMatchRequest | 145 CancelMatchResponse | 매칭 대기 취소 |
| 18 LeaveSessionRequest | 146 LeaveSessionResponse | 세션 퇴장 |
| — | 208 MatchFound | session_id와 전체 player_ids, requestId 0 |
| — | 209 SessionPlayerLeft | 퇴장한 player_id, requestId 0 |

응답은 원래 requestId를 유지합니다. 오류 2는 잘못된 본문, 3은 잘못된 요청 헤더, 23은 중복 대기·이미 세션 참가 등 현재 상태에서 불가능한 요청입니다. 매칭과 취소가 경합하면 서버에서 먼저 처리한 순서를 따릅니다. 매칭이 먼저 완료되면 취소는 거절되며 클라이언트는 배정된 세션을 유지합니다.

클라이언트는 요청당 5초를 기다립니다. 응답이 없거나 프로토콜이 맞지 않으면 연결을 종료합니다. 자동 재시도하지 않습니다. 대기 큐에서 상대방을 기다리는 시간에는 이 제한을 적용하지 않습니다.

## 빌드와 검증

서버 루트에서:

```powershell
cmake --build out/build/windows-x64 --config Debug --target zpd-server zpd-server-tests -- /p:VcpkgEnabled=false
ctest --test-dir out/build/windows-x64 -C Debug -R server-integration --output-on-failure
```

클라이언트 루트에서:

```powershell
dotnet run --project Tools/Verification -- ../zpd-server/out/build/windows-x64/Debug/zpd-server.exe
```

실제 서버에 여러 클라이언트를 연결해 FIFO 매칭, 세션 분리, 취소, 퇴장, 재매칭, 연결 종료 정리, 잘못된 요청을 검증합니다. 기존 에코/프레이밍 테스트도 유지합니다. Unity 참조 기반 컴파일을 확인했으며, Unity Play 화면을 직접 조작한 검증은 별도입니다.

프로토콜을 바꾼 뒤에는 `Tools/Generate-Protocol.ps1`로 C# 메시지를 갱신합니다.

## 솔로 디펜스

`Assets/Scenes/SoloDefense.unity`를 열고 Play → START DEFENSE.
WASD/방향키 이동, 마우스 조준·왼쪽 버튼 사격, Space 대시, Esc 일시정지.
웨이브 종료마다 1/2/3으로 무료 카드 1장을 골라 발사체 수·피해·발사 속도를 누적 강화합니다.
선택 전에는 준비 시간이 멈추고, 선택 후 8초간 준비 시간이 흐릅니다.
4 체력 회복, 5 비콘 수리, Enter 다음 웨이브, M 효과음 음소거.
추격병·비콘 공격병을 저지하고, 사격병의 조준선을 읽어 분홍색 투사체를 피합니다.
적은 웨이브에 따라 강화되며 사격·명중·처치·대시에 시각 피드백을 제공합니다.
`ZPD → Defense → Create Solo Defense Scene` 메뉴로 씬 전체를 에디터에서 생성할 수 있습니다.

게임 모드는 `dedicated_battle`과 `solo_defense`이며 공통 싱글톤이 플레이 정보를 추적합니다.
현재 새로 구현한 플레이는 솔로 디펜스입니다. 종료 시 게임 로그 저장과 보상을 실제 API로 요청하고,
서버가 없는 상태에서는 각각 실패 화면을 표시합니다. 상세 실행법과 데이터 계약은
[Defense README](Assets/Scripts/Defense/README.md), [API 명세](Docs/API_IMPLEMENTATION.md),
[남은 클라이언트 작업](Docs/CLIENT_TASKS.md)을 참고하세요.
