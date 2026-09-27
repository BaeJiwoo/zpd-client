# 로비·솔로 디펜스 MVC

## 스크립트 폴더

기능별 폴더를 유지하고 그 안에서 역할을 구분한다. 예를 들어 로비는 아래 구조다.

```text
Assets/Scripts/Lobby/
├── Models/       상태, 규칙, 읽기 전용 스냅샷, 플레이어 세션
├── Views/        화면 표시와 UI 참조
├── Controllers/  입력, 상태 변경, 화면·서비스 호출 조정
├── DTOs/         API 요청·응답과 서비스 간 전달 데이터
├── Services/     HTTP 어댑터와 서비스 인터페이스
└── Editor/       씬 생성·스타일 도구
```

`Defense` and `Gameplay` retain feature-role folders. HTTP/authentication classes now live in `Networking`; see [Networking architecture](NETWORKING.md).
`Networking/Tcp`는 별도 어셈블리인 TCP 기반 통신 계층이며, `Generated`의 Protobuf 코드는
생성 도구가 관리하므로 이동하지 않는다. `Development`도 별도 테스트 클라이언트 어셈블리로 유지한다.

DTO는 데이터만 담는다. JSON 필드 이름·기본값·`Serializable` 표시는 API 계약의 일부다.
응답 검증과 변환은 Service, 검증된 상태와 계산은 Model, UI 표시는 View가 담당한다.
`LobbyApiDtos`, `LoginApiDtos`, `ApiErrorDtos`는 내부 HTTP 형식별 묶음이며,
`LobbyProfileData`, `GameRunSnapshot`, `DefenseRunReport` 등 공개 전달 데이터는 개별 파일이다.
`LobbyItemSnapshot`과 `LobbyProfileSnapshot`은 복사·검증·계산을 수행하므로 Models에 둔다.

`DefenseGame.EnemySlot`, `LobbyCharacterPicker.Slot`, `LobbyView.Icon`처럼 Unity 오브젝트를
참조하는 직렬화 구조는 통신 DTO가 아니므로 해당 컴포넌트에 유지한다.
기존 소셜 위젯 중 입력을 처리하는 `LobbyCharacterPicker`, `LobbyHeartAutomation`은 Controllers,
표시 행인 `LobbySocialSlot`은 Views에 둔다. 폴더 정리 과정에서 이 위젯의 동작은 바꾸지 않는다.

기존 스크립트의 namespace·컴포넌트 이름·`.meta` GUID는 유지한다. 따라서 폴더 이동 때문에
씬의 스크립트나 영구 버튼 이벤트를 다시 연결할 필요는 없다.

## 역할

| 영역 | Model | View | Controller |
| --- | --- | --- | --- |
| 로비 | `LobbyModel` | `LobbyView`, `LobbyItemCard` | `LobbyController` + `ILobbyService` |
| 기존 로비 | `LegacyLobbyModel` | `LegacyLobbyView`, `LobbyPanel` | `LegacyLobbyController` |
| 디펜스 | `DefenseModel`, `DefenseWaveRules`, `DefenseCombatStats` | `DefenseView`, `DefenseFeedback` | `DefenseGame` |
| 보급·강화 | `DefenseSuppliesModel`, `DefenseUpgradeCatalog` | `DefenseSuppliesView` | `DefenseSupplies` |

Model은 씬 오브젝트, UI, 입력, HTTP에 의존하지 않는다. 로비 패널 선택, 전투 상태,
체력·회복·대시 제한, 웨이브 구성, 골드·카드 선택 규칙을 관리한다.
Unity의 수학 타입(`Vector2`, `Mathf`)은 사용한다.

View는 에디터에서 저장한 UI 참조를 소유하고 모델 상태를 표시한다. Controller는
입력과 버튼 이벤트를 받아 Model을 변경하고 View를 갱신한다. 전투 Controller는
Unity 씬의 이동·충돌·오브젝트 풀과 기존 적 행동 전략을 조정하고, 기록·오디오·HTTP
서비스를 호출한다. 로비의 서버 미연결 캐릭터·소셜 위젯은 기존 바인딩 컴포넌트를 사용한다.

`DefenseGame`과 `DefenseSupplies` 이름 및 스크립트 GUID는 기존 씬의 영구 버튼 이벤트와
전투 컴포넌트 참조를 유지하기 위해 보존했다. 이 두 컴포넌트는 Controller 역할이다.
기존 `game.healthText`, `controller.profile` 같은 공개 UI 접근자는 View에 위임하는
호환용 프로퍼티이며 실제 직렬화된 참조는 View 컴포넌트에 저장된다.
새 코드는 `controller.View`와 `controller.Model`을 사용한다.

새 로비의 API 응답 기반 상태 관리와 서비스 추가 절차는 [Lobby 서비스 설계](LOBBY_SERVICES.md)에 정리했다.

## 서비스와 화면

`GameResultUploadClient`, `DefenseRewardClient`는 HTTP 요청과 응답 상태를 관리한다.
UI 참조는 없으며 `Changed` 이벤트와 상태 프로퍼티를 제공한다.
`DefenseGame`이 활성화될 때 구독하고 비활성화될 때 해제한다.
응답·실패·재시도 가능 여부는 `DefenseView`에서 표시한다.

API 계약, 실패 처리, 요청별 멱등 키, 서버가 확정해야 하는 보상 정책은 유지한다.

## 씬 이동

1. 빌드 첫 씬은 `Assets/Scenes/Login.unity`다. 플레이어 ID를 입력하면 `Lobby.unity`로 이동한다. `LegacyLobby.unity`는 생성 원본으로 보관하며 빌드에서 제외한다.
2. 로비의 **SOLO DEFENSE >** 버튼이 `LobbyController.EnterSoloDefense`를 호출한다.
3. `SceneNavigation`으로 `SoloDefense.unity`를 열고 준비 화면에서 **전투 시작**을 누른다.
4. 준비·일시정지·결과 화면의 **로비로 돌아가기** 버튼으로 로비에 복귀한다.

두 씬을 Build Settings에 활성화했으며 기존 `ConnectionTest`도 유지한다.
전환 전에 대상 씬의 로드 가능 여부를 확인한다. 데디케이티드 서버 매칭을 시작하는
동작이 아니라 로컬 솔로 모드 진입이다.

진행 중 복귀하면 `returned_to_lobby` 사유로 게임 기록을 확정하여 기존 로컬 대기 파일에
저장하고, 진행 중인 결과·보상 요청과 효과음을 종료한다. 이미 종료된 게임은 다시
정산하지 않는다. `GameSessionTracker`는 씬을 넘어서 유지되고 중복 인스턴스는 제거된다.
복귀 시 취소된 요청의 자동 재전송은 구현하지 않는다. 기존 미확정 로그 파일은 보존한다.

## 씬 작성과 검증

저장된 로비·디펜스 씬의 UI 참조를 View 컴포넌트로 이전했다.
씬 생성기는 새 씬에도 View를 함께 추가한다. 디펜스 씬의 생성·업그레이드 메뉴는
복귀 버튼을 추가하며 반복 실행해도 버튼을 중복 생성하지 않는다.

`Tools/DefenseValidation/Run.ps1`은 격리한 Unity 프로젝트에서 다음을 검증한다.

- 기존 카드·보급·적 공격·탄환·대시·일시정지 회귀 검증
- 씬 없이 생성한 Model의 체력·상태 전환 및 로비 탐색 규칙
- 실제 저장된 로비 버튼으로 디펜스 입장
- 준비·일시정지·결과 화면의 영구 버튼 이벤트로 로비 복귀
- 중도 종료 기록, 서비스 상태의 View 표시, 반복 입장 후 싱글톤 중복 방지

결과는 `Temp/SoloDefenseValidation/validation-result.txt`에 기록한다.
