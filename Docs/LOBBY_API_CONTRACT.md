# Lobby HTTP 요청·응답 계약

`LobbyApiService`는 실제 `UnityWebRequest`로 아래 요청을 전송한다. `LobbyController`는 로비 시작 시 서비스를 구성하고 프로필과 인벤토리를 조회한다. 각 메뉴를 열면 해당 정보를 다시 조회한다. 기존 UI 스타일과 MVC 역할은 유지한다.

**이 문서는 현재 클라이언트가 구현한 계약이다. 서버 구현 완료를 뜻하지 않는다.** 기존 서버 저장소에는 이 HTTP API가 없으므로 서버와 아래 필드/경로를 합의하고 구현해야 한다. `API_IMPLEMENTATION.md`의 초안을 프로필 이력, 아이템 표시 정보, 소비 요청으로 확장했다.

## 연결 설정

Login 씬의 `LoginController.apiRoot`를 설정한다. 로비·게임 결과·보상 요청은 로그인 세션에 묶인 이 주소를 공유한다.

- ID/password login and session lifetime follow the [login contract](LOGIN.md).
- 로그인 성공 시 `AuthManager.Instance.SetSession(AccountSession)`을 호출한다. 로비는 세션 변경을 구독하고 씬 재진입 시에도 같은 세션으로 연결한다.
- 로그아웃은 `AuthManager.Instance.Logout()`로 수행한다. `ConfigureService(null)`은 로비 어댑터만 분리하므로 공통 로그아웃 용도로 사용하지 않는다.
- 인증 전에는 계정 요청을 전송하지 않는다. 모든 계정 요청에 `Authorization: Bearer <accessToken>`과 `Accept: application/json`을 포함한다.
- 토큰과 만료 시각은 메모리에만 보관한다. 401/만료 시 세션을 비우고 재로그인을 안내한다. 자동 갱신은 아직 없다.
- HTTPS 인증서 검증을 우회하지 않는다. 에디터/개발 빌드에서만 loopback HTTP를 허용하며 Unity의 Allow downloads over HTTP 설정도 필요하다.

## 플레이어 정보

`GET /api/v1/me` — 요청 본문 없음. 계정은 인증 토큰으로 결정한다.

```json
{
  "data": {
    "revision": "3",
    "displayName": "Player",
    "level": 12,
    "stats": { "wins": 7, "losses": 3 },
    "recentMatches": ["WIN / Solo Defense", "LOSE / Multi Play"],
    "currentCharacterId": "character-instance-id",
    "characterArtKey": "char-1"
  }
}
```

revision, displayName, level, stats.wins/losses, recentMatches는 필수다. 이력이 없으면 `[]`. 캐릭터 필드는 선택이다. `displayName`은 Model의 nickname으로 변환한다. 현재 UI 승률은 `wins / (wins + losses)`이며 무승부를 지원한다면 모델과 계약을 확장해야 한다. 이력 문자열은 표시 데이터로만 취급하며 Rich Text를 해석하지 않는다.

## 인벤토리

`GET /api/v1/me/inventory` — 요청 본문 없음.

```json
{
  "data": {
    "revision": "4",
    "items": [
      {
        "instanceId": "potion-01",
        "name": "회복약",
        "description": "체력을 회복합니다.",
        "artKey": "potion",
        "kind": "consumable",
        "quantity": 5,
        "capacity": 20
      },
      {
        "instanceId": "armor-01",
        "name": "방어구",
        "description": "장착 아이템 정보",
        "artKey": "armor",
        "kind": "equipment",
        "quantity": 1,
        "capacity": 1
      }
    ],
    "nextCursor": null
  }
}
```

revision과 items는 필수다. 아이템 필수 필드는 instanceId, name, kind, quantity, capacity다. kind는 `consumable` 또는 `equipment`. 미보유는 `items: []`. instanceId를 선택/사용 식별자로 쓰고 artKey는 로컬 아이콘에만 매핑한다. 수량과 capacity는 음수가 될 수 없다. UI 슬라이더 최댓값은 서버 capacity와 quantity 중 큰 값이다.

현재 버전은 **전체 인벤토리 스냅샷**을 요구한다. `nextCursor`가 비어 있지 않으면 부분 목록으로 기존 소유 정보를 덮지 않고 오류를 표시한다. 페이징 도입 시 서버 스냅샷 버전을 고정한 전체 페이지 수집을 어댑터에 추가해야 한다.

## 소비 아이템 사용

`POST /api/v1/me/inventory/{escaped-instanceId}/use`

헤더: `Content-Type: application/json`, `Idempotency-Key: <operationId>`.

요청:

```json
{ "quantity": 1 }
```

응답:

```json
{
  "data": {
    "inventory": {
      "revision": "5",
      "items": [
        { "instanceId": "potion-01", "name": "회복약", "kind": "consumable", "quantity": 4, "capacity": 20 }
      ],
      "nextCursor": null
    }
  }
}
```

`inventory`는 사용 후 **전체 목록**이다. profile이 변경되었다면 같은 data에 위 프로필 형식의 `profile`을 추가한다. 서버는 소유권/사용 가능 수량/아이템 종류를 검사하고 멱등 키와 사용 결과를 원자적으로 저장해야 한다. 클라이언트는 장비에 사용 버튼을 표시하지 않고, 서버 응답 전에는 수량을 차감하지 않는다. 소진한 아이템은 전체 응답에서 제외하거나 quantity 0으로 반환한다.

## 오류와 수명

실패는 비정상 HTTP 상태 코드와 아래 본문을 반환한다.

```json
{ "error": { "code": "INSUFFICIENT_QUANTITY", "message": "Not enough items." } }
```

서버 code는 `LobbyServiceException.Code`로 전달한다. UI에는 상태별 클라이언트 메시지를 표시하며 원시 서버 본문/토큰을 로그로 남기지 않는다. 401은 재로그인, 403은 권한 오류로 구분한다. 자동 재전송/토큰 갱신은 수행하지 않는다. 토큰 갱신은 로그인 서비스의 책임이다.

타임아웃/연결 실패/408/5xx 또는 사용 성공 본문 파싱 실패는 사용 결과가 불확실하므로 같은 멱등 키를 보존한다. 재시도는 같은 아이템의 동일 요청으로 수행한다. 리다이렉트는 허용하지 않는다. 계정 전환/비활성화 시 요청 취소와 응답 세대 검사로 이전 계정 응답을 차단한다.

## 검증

`Tools/LobbyValidation/Run.ps1 -Api -Capture`는 격리된 Unity 프로젝트와 loopback TCP HTTP 서버에서 실제 요청을 검증한다. 경로/메서드/본문, Bearer/멱등 헤더, DTO 변환, 화면 데이터 반영, 사용 결과, 401/5xx, 누락 필드/잘못된 JSON, 타임아웃/취소를 검사한다. 테스트 서버는 제품 Assets에 포함하지 않는다. 실제 운영 서버 통합 검증은 별도로 필요하다.


