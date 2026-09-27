# Lobby: MVC와 API 서비스 추가 방안

Implementation structure: [Networking architecture](NETWORKING.md). Login is owned by `AuthManager.Instance`; HTTP requests return `ApiResult<T>` from `ApiClient`. Credentials live in `TokenStorage`.

## 기본 원칙

플레이어 닉네임·레벨·전적·현재 캐릭터·인벤토리 소유권·수량은 **API 응답이 유일한 확정 데이터**다. 클라이언트가 저장된 테스트 값으로 채우거나, 버튼을 눌렀다는 이유로 수량을 차감하지 않는다. 창 열기, 필터, 선택한 아이템, 요청 진행 상태는 클라이언트 UI 상태다.

`LobbyApiService` implements HTTP profile/inventory reads and item use. ID/password login establishes the shared account session, which is restored when entering or returning to the lobby. The backend must implement the documented contracts; the test peer exists only under `Tools/LobbyValidation`. See [login](LOGIN.md) and [HTTP requests and responses](LOBBY_API_CONTRACT.md).

## 현재 코드의 책임

| 구성 | 책임 | 하지 않는 일 |
| --- | --- | --- |
| `LobbyModel` | 검증된 응답의 복사본, 리소스 revision, 조회 상태, 창/필터/선택, 아이템 사용 진행·멱등 키 | Unity 참조, UI 수정, HTTP, 로컬 수량 차감 |
| `LobbyProfileSnapshot`, `LobbyItemSnapshot` | 읽기 전용 계정 데이터. 입력 DTO의 배열/가변 필드를 복사 | 원본 응답을 수정하거나 외부에서 값을 덮어쓰기 |
| `LobbyView` | 직렬화된 UI 참조, 그리드 카드 표시, 팝업·포커스·입력 차단, 아이콘 키→Sprite 표시 | API 호출, 계정 상태 결정 |
| `LobbyItemCard` | 아이템 표시와 선택 ID 이벤트 | Controller 직접 참조, 아이템 사용 요청 |
| `LobbyController` | UI 입력→Model→View, 서비스 호출, 중복 요청·오래된 응답·계정 변경·취소 제어 | JSON 파싱, 토큰 보관, 직접 Text/Slider 수정 |
| `ILobbyService` | 인증된 계정의 프로필/인벤토리 조회와 아이템 사용 계약 | Unity UI 접근 |
| `LobbyApiService` | HTTP/DTO 매핑, 인증, 타임아웃, 오류 분류, 재시도·멱등성 | 화면 직접 조작 |

기존 씬의 버튼 대상은 `LobbyController` 그대로 유지한다. `controller.home` 같은 프로퍼티는 기존 씬 빌더와 테스트를 위한 View 위임 접근자다. **실제 UI 필드 직렬화는 `LobbyView`로 이전했다.** 이미 열린 씬에서 스크립트 재로드로 참조가 비는 경우에는 View가 기존 계층에서 누락된 참조만 복구한다. 런타임에 화면 자체를 새로 만들지 않는다. 인벤토리 행은 저장된 카드 템플릿으로 View가 생성한다.

## 구현된 서비스 계약

```csharp
Task<LobbyProfileData> GetProfileAsync(CancellationToken cancellation);
Task<LobbyInventoryData> GetInventoryAsync(CancellationToken cancellation);
Task<LobbyUseItemResult> UseItemAsync(
    string itemId, string operationId, CancellationToken cancellation);
```

- `LobbyProfileData`: revision, nickname, level, wins, losses, recentMatches, characterId, characterArtKey.
- `LobbyInventoryData`: revision, items. 정상적으로 비어 있는 인벤토리는 `items: []`이며 `null`은 잘못된 응답이다.
- 아이템: id, name, description, kind, quantity, capacity, iconKey. 아이콘 키는 View의 로컬 아이콘 목록에서 Sprite로 변환한다. 모델/DTO에 Unity Sprite를 넣지 않는다.
- `LobbyUseItemResult`: 사용 후 전체 inventory와, 필요하면 변경 후 profile. 소비 아이템의 효과로 프로필이 바뀌었다면 profile도 반환하는 계약을 권장한다.
- revision은 리소스별 단조 증가 값이다. 서버 JSON이 십진 문자열이면 어댑터에서 `long`으로 검증·변환한다. 더 큰 범위가 필요하면 계약과 모델을 함께 변경한다.
- `characterArtKey`는 서버 캐릭터 ID를 로컬 아트 목록에 대응시킨 어댑터의 결과다. 정상 프로필의 현재 캐릭터만 기존 `LobbyCharacterPicker`에 바인딩한다. 친구 UI와 캐릭터 표현의 기존 연결은 보존한다.

스냅샷은 음수 값, 누락 ID, 중복 아이템 ID 등을 거절한다. 화면은 미조회/로딩/실패/정상 빈 목록을 구분한다. 같은 계정에서 새로고침에 실패하면 마지막 확정 데이터를 유지하면서 실패를 표시한다. 계정 전환 시에는 이전 계정 데이터를 즉시 지운다.

## 서비스 확장 및 서버 연결 순서

1. 서버 계약을 먼저 확정한다. 인증 방법, 리소스 revision, 아이템 사용 멱등 키와 결과 재조회, 목록 페이징, 오류 코드가 필요하다. 기존 [API 명세 초안](API_IMPLEMENTATION.md)은 제안이며 실제 구현 여부와 혼동하지 않는다.
2. base URL 검증, 인증 헤더, 상태 코드 처리, 취소와 유한 타임아웃은 공통 `ApiClient`에 구현되어 있다. `LobbyApiService`는 로비 DTO를 변환한다. 토큰은 계정 세션에서 제공하며 씬에 저장하지 않는다.
3. 구현된 `LobbyApiService : ILobbyService`가 HTTP envelope를 검증하고 위 DTO로 변환한다. 서버 계약 변경은 이 어댑터에 반영한다.
4. 로그인 완료 후 Unity 메인 스레드에서 서비스를 주입한다.

```csharp
// 로그인 API가 반환한 서버 확인 세션을 공유한다.
AuthManager.Instance.SetSession(authenticatedSession);
// 로그아웃이나 다른 계정으로 전환할 때 이전 상태와 요청을 폐기.
AuthManager.Instance.Logout();
// 다음 계정 로그인 완료 후 새 계정 서비스로 다시 주입.
AuthManager.Instance.SetSession(nextAccountSession);
```

5. 주입 즉시 프로필과 인벤토리를 각각 조회한다. 각 창을 열 때 해당 리소스를 다시 조회한다. 재시도/새로고침 UI를 추가할 때 `RefreshProfileAsync`, `RefreshInventoryAsync`, `RefreshAsync`에 연결한다.
6. 응답은 Controller가 Model에 반영한 후 View에 표시한다. 어댑터가 View를 직접 호출하거나 테스트용 수치를 화면에 넣지 않는다.

호출 진입점은 Unity 메인 스레드다. Controller의 await는 Unity 컨텍스트를 유지한다. 전송/파싱을 작업 스레드에서 수행해도 UI 접근은 하지 않는다. 서비스 인스턴스는 로그인 세션에 귀속시키고, 계정 변경 시 이미 시작된 요청의 인증 토큰을 새 토큰으로 바꾸지 않는다.

## 응답 순서·생명주기·변경 요청

- 프로필과 인벤토리 조회에는 각각 요청 순번이 있다. 나중에 요청한 응답을 먼저 받았다면 이전 응답은 버린다. 리소스 revision도 함께 비교한다.
- 아이템 사용을 시작하면 이전 인벤토리 조회 응답을 무효화한다. 사용 진행 중에는 새 인벤토리 조회를 시작하지 않는다.
- 같은 사용 요청이 진행 중이면 추가 요청은 보내지 않는다. 창을 닫거나 다시 열어도 이 제한은 유지한다.
- 서버가 사용 결과를 확인하기 전에는 아이템 수량을 바꾸지 않는다. 성공 응답의 전체 인벤토리로 교체한다. 기다리는 동안 사용자가 닫은 팝업은 다시 열지 않는다.
- `LobbyServiceException(code, message, outcomeUnknown: false)`는 서버가 확정한 거절이다. 다시 시도하면 새 작업으로 처리할 수 있다.
- 타임아웃·연결 끊김처럼 처리 여부를 모르면 `outcomeUnknown: true`로 보고한다. 알 수 없는 예외도 불확정으로 취급한다. 같은 아이템 재시도에는 기존 operationId를 재사용하며, 미확정 작업이 남아 있으면 다른 소비 아이템 사용을 막는다.
- 서비스를 바꾸거나 Controller가 비활성화되면 요청을 취소하고 세대 번호를 바꾼다. 취소를 무시하고 늦게 반환하는 서비스라도 이전 응답을 새 계정/화면에 적용하지 않는다.
- **취소는 서버 작업을 되돌린다는 뜻이 아니다.** 씬 이동/앱 재시작을 넘는 미확정 사용 작업의 ID·계정·결과 조회/재시도는 세션 서비스가 보관해야 한다. 현재 Model의 멱등 키 보관 범위는 해당 로비 세션이다. 서버 멱등 처리와 세션 단위 작업 기록 없이 운영 소비 API를 연결하지 않는다.
- Controller는 HTTP 타임아웃이나 자동 재시도 정책을 구현하지 않는다. 서비스는 모든 Task가 유한 시간에 완료되도록 하고 CancellationToken을 존중해야 한다. 조회 재시도에는 제한된 backoff를, 변경 재시도에는 동일 멱등 키를 사용한다.

## 나머지 기능의 서비스 추가 계획

| 기능 | 추가할 계약/연결 | 확정 데이터 처리 |
| --- | --- | --- |
| 친구 목록·검색·추천 | `ILobbySocialService.GetFriends/Search/GetSuggestionsAsync` | 목록/검색별 요청 순번·cursor를 관리하고 성공 응답만 `LobbySocialSlot`에 표시 |
| 친구 요청 | `SendFriendRequestAsync(targetId, operationId, token)` | 서버 응답으로 관계/요청 상태 갱신; 클릭만으로 친구 수 증가 금지 |
| 하트 자동 송수신 | `SyncHeartsAsync(operationId, token)` | 서버가 수령/전송 가능 대상을 판정하고 잔액/이력을 반환; 개별 행 수에 의존하지 않음 |
| 현재 캐릭터·변경 | 프로필 조회 + `ChangeCharacterAsync(characterId, operationId, token)` | 서버가 소유권을 검증한 뒤 반환한 현재 캐릭터/프로필로 갱신 |
| Multi Play | 기존 TCP/Protobuf 매칭의 서비스 어댑터 | 서버 매칭 상태/세션 응답 후에만 다음 화면으로 이동. `MultiPlayRequested`에 연결 |
| 전적/보상 | 서버 결과 확정 후 프로필 재조회 | 클라이언트 게임 종료 이벤트만으로 승수/경험치 증가 금지 |

현재 친구 창은 기존 `LegacyLobbyController`/`LegacyLobbyView`/`LobbyHeartAutomation`의 로그 전용 흐름을 재사용한다. 이 부분까지 실제 서비스가 구현됐다는 뜻은 아니다. 소셜 확장 시에도 같은 Model→View 규칙을 적용하고, 기존 로그만 호출하는 위치를 서비스 호출로 교체한다. 기존 [소셜 연결 문서](../Assets/Scripts/Lobby/API_INTEGRATION.md)를 참고하되 UI 바인딩을 서버 성공으로 취급하지 않는다.

## 검증

`Tools/LobbyValidation/Run.ps1`은 실제 저장된 MVC 씬에서 테스트 서비스로 다음을 검증한다.

- 원본 DTO 수정으로 Model이 변하지 않는지, 잘못된/오래된 응답 거절
- 미조회와 정상 빈 목록의 구분, 전적 표시
- API 성공에 의해서만 수량 변경, 중복 사용 차단, 미확정 재시도 키 유지
- 역순 응답, 계정 전환 전 응답, 비활성화 이후 응답 무시
- 재활성화 재조회, 로그아웃 상태 초기화, 창/아이템 표시

`-Pointer` 옵션은 화면 레이캐스트와 Input System UI 모듈을 통해 메뉴·닫기 버튼을 점검한다. 실제 API 어댑터 추가 시에는 서버 contract test, 401/403/409/429/5xx, 타임아웃 후 늦은 성공, 동일 작업 키 재전송, 씬 이동 후 미확정 작업 복구를 추가한다.





