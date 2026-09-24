# 로비·소셜·전투 보상 API 구현 명세 초안

작성일: 2026-09-22. 대상: 현재 zpd-client와 향후 게임/API 서버 구현.
이 문서는 구현 계약 **제안**이다. 확정 요구사항과 미결정을 아래에서 구분한다.
서버 HTTP 라우트와 인증은 아직 구현되지 않았다. 솔로 디펜스에는 아래 10절의
프로토타입 DTO와 실제 HTTP 요청/실패 처리가 추가되어 있다.

관련 문서: [클라이언트 작업 목록](CLIENT_TASKS.md),
[현재 로비 바인딩 설명](../Assets/Scripts/Lobby/API_INTEGRATION.md).

## 1. 확정된 요구사항

- 프로필에서 레벨·전적을 확인한다.
- 친구 목록, 유저 검색, 최근 접속자 기반 추천 목록을 제공한다.
- 추천 목록은 검색 없이 **해당 유저에게 친구 요청**을 보내기 위한 기능이다. 다른 친구에게 소개 메시지를 보내는 기능이 아니다.
- 추천 목록을 포함한 소셜 목록을 새로고침할 수 있다.
- 친구 창을 열거나 새로고침하면 받을 수 있는 하트를 받고, 보낼 수 있는 친구에게 자동으로 보낸다. 별도 수동 하트 버튼은 없다.
- 캐릭터·인벤토리·장비의 소유 및 변경 결과는 서버가 결정한다.
- **전투 중 사망 시, 서버가 확정한 킬 수에 따라 경험치를 정산한다.**
- API 응답이 없는 UI는 `--` placeholder로 표시한다. 미확인을 0, 빈 목록, 성공으로 대신 표시하지 않는다.
- 게임 오브젝트와 UI는 Unity Editor 메뉴로 생성·저장한다. API 응답 시 런타임에 씬이나 행을 생성하지 않는다.

## 2. 현재 구현과 제안 구조

| 영역 | 현재 | 구현 제안 |
| --- | --- | --- |
| 로비 조회·변경 | 로그와 UI 바인딩만 존재 | HTTPS JSON API + 클라이언트 서비스 계층 |
| 매칭 | TCP/Protobuf 구현 | 기존 프로토콜 유지, 인증·전투 시작 정보 추가 |
| 전투 플레이·사망·킬 | 싱글 씬 솔로 디펜스 로컬 구현 | 데디케이티드 서버 권위 전투 및 운영 솔로 검증 구현 |
| 경험치·전적 저장 | 없음 | 서버 영속 저장, 정산 원장, 결과 이벤트·조회 |
| 인증·계정 복구 | 없음 | 인증 제공자/토큰 정책 확정 후 구현 |

기존 매칭 요청 코드는 16/17/18, 응답은 144/145/146, 알림은 208/209다.
`MatchFound`는 현재 session ID와 player ID 목록만 제공한다. 이것만으로 인증 계정,
영속 전투 참여 ID, 사망 결과를 알 수 없다. 신규 메시지 번호는 이 문서에서 임의 배정하지 않는다.

TCP 프레임은 헤더 8바이트, 본문 최대 4088바이트다. 신규 Protobuf도 이 제한과
요청/응답 request ID 규칙을 지킨다. JSON DTO를 기존 TCP에 그대로 넣는 방식은 사용하지 않는다.
인증 없이 발급되는 현재 `PlayerId`를 영속 `userId`로 간주하면 안 된다.

## 3. 공통 계약 제안

### 3.1 인증과 식별자

- 예시 API prefix: `/api/v1`. 아래 표는 이 prefix를 생략한다.
- `/me`는 서버가 검증한 인증 주체다. 클라이언트가 본문에 보낸 account ID로 대체하지 않는다.
- 인증 수단은 Bearer access token을 제안하되, 로그인 제공자·갱신 경로·토큰 수명은 미결정이다.
- 영속 user/character/item/battle/participation ID는 JSON에서 불투명 문자열로 전달한다.
- TCP 접속도 검증된 계정에 연결하는 handshake 또는 단기 전투 티켓이 필요하다. 형식은 서버와 별도 확정한다.
- 서버 내부 정산 호출은 신뢰된 전투 서버만 할 수 있어야 한다. 일반 유저 토큰으로 호출할 수 없다.

### 3.2 응답·페이징·버전

공통 성공 envelope 제안:

```json
{
  "requestId": "trace-id",
  "serverTime": "2026-09-22T09:00:00Z",
  "data": {}
}
```

- 시각은 UTC ISO 8601. 클라이언트 시각으로 접속 순위·하트 가능 여부·정산 순서를 판정하지 않는다.
- 목록의 `data`는 `{ "items": [], "nextCursor": null }` 형식이다. cursor는 불투명 값이다.
- query의 `limit` 범위와 기본값은 서버 계약에서 확정한다. 추천은 최근 접속 순서와 안정적인 동률 기준을 가진다.
- 프로필·장비·캐릭터·하트 상태에는 해당 리소스의 단조 증가 `revision`을 포함한다.
  JSON 정밀도 문제를 피하려면 revision도 10진 문자열로 전달한다. 숫자로 비교하고 사전식 비교하지 않는다.
- 미확인 UI와 정상 응답의 `items: []`, `balance: 0`은 서로 다른 상태다.
- 변경 응답은 변경 후 authoritative state를 반환한다. 클라이언트가 성공을 추정해서 수치를 더하지 않는다.

### 3.3 오류와 멱등성

```json
{
  "requestId": "trace-id",
  "error": {
    "code": "CHARACTER_NOT_OWNED",
    "message": "The character is not owned.",
    "retryable": false
  }
}
```

| 상황 | HTTP 제안 | 클라이언트 처리 |
| --- | --- | --- |
| 잘못된 입력 | 400 | 입력 안내, 자동 재시도 안 함 |
| 인증 만료 | 401 | 갱신 정책에 따라 처리, 실패 시 계정 상태 초기화 |
| 권한·소유 조건 불충족 | 403 | 버튼 상태/소유 정보를 다시 조회 |
| 존재하지 않는 대상 | 404 | 대상 제거 또는 목록 새로고침 |
| 이미 처리됨·상태 충돌 | 409 | 상세 code에 따라 최신 상태 반영; 성공으로 일괄 취급하지 않음 |
| 요청 한도 | 429 | 서버 지정 대기 시간 이후 재시도 |
| 서버 일시 오류 | 5xx | 실패 상태 표시, 안전한 재시도 정책 적용 |

친구 요청·하트 sync에는 `Idempotency-Key`를 사용한다. 서버는 인증 계정+작업+키에 대해
요청 내용과 결과를 저장한다. 같은 키·같은 요청은 기존 결과를 반환하고,
같은 키·다른 요청은 충돌로 거절한다. 보관 기간은 재시도 가능 기간을 포함하도록 별도 확정한다.
타임아웃은 서버의 미처리를 뜻하지 않는다. 재시도할 때 새 키를 만들지 않는다.
캐릭터·장비의 PUT은 같은 상태 설정을 재시도할 수 있지만, 오래된 요청이 최신 선택을 덮지 않도록
`expectedRevision`을 요구하는 방식을 제안한다.

## 4. 라우트와 데이터 계약

### 4.1 프로필

`GET /me`

`data`: `userId`, `displayName`, `level`, `totalExperience`, `experienceInLevel`,
`experienceToNextLevel`, `stats{matches,wins,losses,kills,deaths}`, `currentCharacterId`, `revision`.
최대 레벨에서는 다음 레벨 필요 경험치를 `null`로 표현하는 방식을 제안한다.
숫자의 자료형·상한, 최대 레벨 정책은 확정 필요.
승률은 서버가 내려주거나 wins/matches로 표시하되 0경기 표시 정책을 정한다.
사망과 패배를 동일하게 취급하지 않는다. 게임 승패 확정 전에는 wins/losses를 갱신하지 않는다.

### 4.2 친구·추천·요청

| 라우트 | 입력 | 주요 data |
| --- | --- | --- |
| `GET /me/friends` | cursor, limit | items: userId, displayName, level, presence, lastOnlineAt; nextCursor |
| `GET /users` | query, cursor, limit | items: userId, displayName, relationship, canRequestFriend; nextCursor |
| `GET /me/friend-suggestions` | cursor, limit | 최근 접속 기반 items: userId, displayName, lastOnlineAt, relationship, canRequestFriend |
| `POST /me/friend-requests` | targetUserId + 멱등 키 | friendRequestId, targetUserId, status, createdAt |
| `GET /me/friend-requests` | direction=incoming 또는 outgoing, cursor, limit | 요청 ID, 상대 유저, status, createdAt |
| `POST /me/friend-requests/{id}/accept` | 멱등 키 | 최종 요청 상태와 friendship |
| `POST /me/friend-requests/{id}/reject` | 멱등 키 | 최종 요청 상태 |

`relationship`: none / outgoing_pending / incoming_pending / friends 등 서버 계약 enum으로 통일한다.
검색의 빈 문자열은 거절한다. 길이·유니코드 정규화·검색 규칙은 서버와 맞춘다.
추천 목록은 자신, 이미 친구인 유저, 처리 중인 요청 대상, 차단/제한된 대상을 제외한다.
추천 조회 직후 관계가 바뀔 수 있으므로 전송 시 서버에서 다시 검증한다.
최근 접속의 기간과 추천 순위 규칙은 미결정이다. 새로고침 전용 mutation은 만들지 않고 첫 페이지를 다시 조회한다.
반대 방향 동시 요청과 수락/거절 중복은 서버에서 원자적으로 처리한다.

### 4.3 하트 자동 송수신

**권장 계약: `POST /me/hearts/sync` 한 번으로 서버가 일괄 처리한다.**
친구 창 열기·새로고침이 트리거다. 현재 화면의 3개 행이나 현재 탭에 처리 대상을 제한하지 않는다.
단순 탭 전환은 추가 송수신 트리거가 아니다.

- 본문: `{}`. 인증 계정은 `/me`로 결정하며 수령/전송 가능 목록은 서버가 조회한다.
- 같은 시점 중복 클릭은 클라이언트에서 한 요청으로 합친다. 불명확한 실패의 재시도는 같은 멱등 키를 사용한다.
- 서버는 수령 가능한 하트를 먼저 반영하고, 전송 가능한 친구에게 전송한다.
- 전송 자원 소모 여부는 아직 미결정이다. 소모한다면 수령 후 잔액·대상 처리 순서도 함께 규칙화한다.
- 선물/수령 원장과 잔액 갱신은 원자적이어야 한다. 서버 내부 고유 제약으로 서로 다른 sync 요청 간 중복도 막는다.
- 클라이언트의 로컬 HashSet만으로 중복 지급/전송을 방지하지 않는다.

성공 `data` 제안:

```json
{
  "syncId": "sync-id",
  "state": "completed",
  "balance": 0,
  "receivedCount": 0,
  "sentCount": 0,
  "skippedCount": 0,
  "revision": "42"
}
```

위 0은 **실제 성공 응답에서 처리할 항목이 없을 때의 예시**다. 미연결 기본값이 아니다.
count는 처리 항목 수로 정의하며 잔액 증감량과 혼용하지 않는다.
대상별 상세 정보는 `GET /me/hearts?cursor=...&limit=...`로 조회한다.
`data`: balance, revision, items[{heartEventId,direction,peerUserId,amount,status,occurredAt}], nextCursor.

서버 처리량 때문에 sync를 여러 배치로 나누어야 한다면 `processing` 상태와
`GET /me/hearts/syncs/{syncId}` 조회 계약을 **추가 합의**한다. 부분 처리 중 응답을 completed로 표시하지 않는다.
기본 초안은 completed 응답을 반환하는 동기 처리다. 임의의 클라이언트 타이머로 완료시키지 않는다.

현재 `LobbyHeartAutomation.ApplyEligibility`는 개별 `hearts.receive/send` 로그를 순서대로 출력하는
연결 지점이다. **일괄 sync API 채택 시 이 경로에서 실제 개별 전송을 추가로 실행하면 안 된다.**
클라이언트는 sync 결과만 받아 잔액/이력을 갱신하도록 리팩터링한다.
개별 API 방식을 채택하려면 별도 계약이 필요하며 두 방식을 동시에 사용하지 않는다.

### 4.4 인벤토리·장비

| 라우트 | 입력 | 주요 data |
| --- | --- | --- |
| `GET /me/inventory` | cursor, limit | items: instanceId, itemDefinitionId, artKey, quantity; equipment; nextCursor; revision |
| `PUT /me/equipment/{slot}` | itemInstanceId 또는 null, expectedRevision | equipment, revision |

서버가 소유권, 아이템 종류/슬롯 호환성, 수량, 잠금 상태, 전투 중 변경 가능 여부를 검증한다.
로컬 스프라이트 이름을 아이템 인스턴스 ID로 사용하지 않는다. 모르는 artKey는 대체 그림으로 표시한다.
장착과 보유 슬롯의 표시는 같은 응답 버전을 기준으로 함께 갱신한다.

### 4.5 캐릭터

| 라우트 | 입력 | 주요 data |
| --- | --- | --- |
| `GET /me/characters` | 필요 시 cursor, limit | items: characterId, artKey, displayName, owned; currentCharacterId; revision |
| `PUT /me/character` | characterId, expectedRevision | currentCharacterId, revision |

`char-1`~`char-4`는 현재 클라이언트의 로컬 원화 키다. 영속 characterId와 별개로 매핑한다.
미리보기는 로컬 동작이고 적용은 서버 작업이다. 미소유/알 수 없는 캐릭터, 전투 중 변경 제한,
revision 충돌을 서버가 검증한다. 성공 응답으로만 로비·프로필을 변경한다.
초기 데이터도 카탈로그/소유 정보와 현재 캐릭터가 함께 확인된 뒤 표시한다.

## 5. 매칭에서 실제 전투로 연결

| 단계 | 현재/제안 계약 | 구현 시 주의 |
| --- | --- | --- |
| 인증 연결 | 신규 handshake/티켓, 미구현 | 계정 userId와 연결 PlayerId 연결 |
| 매칭 참가 | 기존 MatchRequest / MatchResponse | 전투 버튼을 누른 경우에만 참가 |
| 취소 | 기존 CancelMatchRequest / CancelMatchResponse | 매칭 성사와 경합 시 서버 상태 우선 |
| 매칭 성사 | 기존 MatchFound 확장 또는 신규 BattleAssigned | battleId, participationId, 실제 전투 접속/시작 정보 필요 |
| 퇴장 | 기존 LeaveSessionRequest / LeaveSessionResponse | 퇴장과 보상 정산을 같은 작업으로 간주하지 않음 |
| 사망 확정 | 신규 PlayerDied 이벤트 | 서버 확정 eventId, battleId, participationId, deathEventId, occurredAt |
| 정산 완료 | 신규 BattleRewardSettled 이벤트 | settlementId와 확정 결과 또는 조회 식별자 |

현재 `MatchmakingClient.HandleConnected()`는 접속 직후 자동으로 RequestMatch를 호출한다.
로비 서비스 연결만으로 전투 대기열에 들어가지 않도록 이 동작을 변경해야 한다.
현재 `MatchFound`에는 실제 전투 월드 정보가 없으므로 새로운 이벤트/DTO 합의 없이
곧바로 전투 씬에 입장시키지 않는다. 신규 패킷은 Protobuf 원본에서 생성한다.

## 6. 사망 시 킬 수 기반 경험치 정산

### 6.1 권한과 계산

- 전투 서버가 사망과 킬 귀속을 확정한다. 일반 클라이언트는 사망 여부, 킬 수, 보상량을 정산 입력으로 제출하지 않는다.
- 정산 대상은 사망한 플레이어의 해당 전투 참여 기록이다. 킬 수는 서버가 확정한 기록에서 집계한다.
- 보상 함수는 `earnedExperience = F(confirmedKills, rewardPolicyVersion)`으로 정의한다.
  선형 방식을 택한다면 `confirmedKills × experiencePerKill`이지만 **단가/보상표는 아직 미결정**이다.
- 다중 레벨 상승, 최대 레벨 처리, 경험치 상한/overflow를 서버에서 처리한다.
- 경험치 증가와 전투 승패는 구분한다. 사망했다고 자동으로 1패를 기록하지 않는다.

### 6.2 내부 정산 경로 제안

`POST /internal/battle-participations/{participationId}/settle`

- 신뢰된 전투 서버 전용. 클라이언트 공개 라우트가 아니다.
- 입력: deathEventId, reason=`death`, battleId, rewardPolicyVersion, 멱등 키.
- userId와 킬 수는 서버의 참여 기록/확정 이벤트에서 조회한다. 임의 보상량을 입력받지 않는다.
- 전투 서버와 정산 서비스가 분리되어 있으면 신뢰된 이벤트 원장 또는 서버 간 증명된 기록을 사용한다.
- 정상 확정 사망 기록이 아직 저장되지 않았으면 정산하지 않고 재처리 가능한 상태로 남긴다.

논리 처리 순서:

1. 참여자·사망 이벤트·최종 킬 집계 시점과 보상 정책 버전을 검증한다.
2. 정산 고유 키로 원장을 조회/잠근다. 이미 정산되었으면 원래 결과를 반환한다.
3. 경험치와 레벨을 계산한다.
4. 정산 원장, 유저 성장 값, 확정 가능한 전적을 하나의 트랜잭션으로 반영한다.
5. 정산 완료 이벤트를 영속 outbox에 함께 기록한다.
6. 커밋 후 BattleRewardSettled를 전달한다. 이벤트가 중복 전달되어도 지급은 중복되지 않는다.

**중복 기준의 미결정:** 부활이 없는 참여당 1회 정산이면 `participationId` unique를 권장한다.
부활이 있다면 `lifeId/deathSequence`와 이미 정산된 킬 구간을 계약에 추가해야 한다.
그 경우 누적 킬을 사망마다 전부 다시 보상하면 안 된다. 이 결정 전에는 unique 키를 확정하지 않는다.
동시 사망/마지막 킬의 포함 여부는 서버 이벤트 순서로 결정하며, 클라이언트 도착 순서를 사용하지 않는다.

### 6.3 결과 조회와 알림

`GET /me/battle-results/{participationId}` — 인증 유저 자신의 참여 결과만 조회 가능.
클라이언트가 조회를 호출해도 지급을 새로 수행하지 않는다.

pending `data`:

```json
{
  "battleId": "battle-id",
  "participationId": "participation-id",
  "status": "pending",
  "reason": "death"
}
```

settled `data` 필수 필드:

| 필드 | 의미 |
| --- | --- |
| battleId, participationId, settlementId | 전투·참여·정산 식별자 |
| status=`settled`, reason=`death`, settledAt | 확정 상태/사유/서버 시각 |
| confirmedKills, earnedExperience, rewardPolicyVersion | 확정 킬 수, 획득 경험치, 적용 정책 |
| progressionBefore | level, totalExperience |
| progressionAfter | level, totalExperience, experienceInLevel, experienceToNextLevel |
| profileRevision | 정산으로 변경된 프로필 버전 |
| stats | 정산 시점의 확정 전적; 승패가 미확정이면 승패 증가를 가정하지 않음 |

`BattleRewardSettled`는 동일한 식별자/결과를 제공하거나, 프레임 크기를 고려해 식별자와
profileRevision만 제공한 뒤 HTTP 조회하게 한다. 둘 중 하나를 서버와 확정한다.
클라이언트는 settlementId로 결과 연출 중복을 막고, 프로필 갱신에는 revision을 사용한다.
이벤트와 조회가 역순으로 도착해도 최신 상태를 오래된 상태로 덮지 않는다.

이벤트를 놓친 재접속 복구를 위해 `GET /me/battle-results?cursor=...&limit=...`도 제공한다.
items는 참여 ID·정산 ID·status·시각의 요약이고 개별 결과는 위 라우트로 조회한다.
참여 ID를 이미 아는 경우 개별 조회, 로컬 참여 상태를 잃은 경우 목록 조회로 복구한다.
미정산 결과는 pending으로 표시하고 서버 권장 간격/한도에 맞춰 재조회한다.
존재하지 않는 참여 ID(404)를 경험치 0인 완료 결과로 표시하지 않는다.

## 7. 서버 데이터와 고유 제약 체크리스트

- 인증 계정 ↔ TCP 연결 ↔ 영속 전투 참여 매핑.
- 프로필 성장/전적 및 리소스 revision.
- 친구 관계 쌍 고유성, 방향별 진행 중 친구 요청 중복 방지.
- 하트 선물/수령 원장과 잔액. sender/receiver/서버 회차 키 고유성.
- 캐릭터/아이템 소유와 장착 상태.
- 전투 참여, 서버 확정 킬·사망 이벤트, 정산 원장, 보상 정책 버전.
- 정산 원장의 합의된 고유 키, 동일 사망 이벤트 중복 방지, 이벤트 outbox.
- HTTP 멱등 요청 결과 저장. 재시작·동시 서버 인스턴스에서도 고유 제약 유지.

## 8. 구현 전 결정할 사항

| 결정 | 미결정 내용 | 영향 |
| --- | --- | --- |
| 인증 | 로그인 제공자, 토큰 갱신/만료, TCP 인증 연결 | 모든 영속 API |
| 경험치 | 킬당 단가 또는 보상표, 0킬 보상, 레벨 곡선, 최대 레벨 | 정산 수치·UI |
| 부활 | 참여당 한 번 사망인지, 사망마다 정산하는지 | 정산 고유 키·킬 구간 |
| 비사망 종료 | 이탈, 연결 종료, 생존 종료, 서버 중단 시 보상 | 미정산/악용 방지 |
| 킬 귀속 | 동시 사망, 환경사, 자살, 어시스트 등 | 확정 킬 집계 |
| 전적 | matches/wins/losses 확정 시점, 무효 경기 | 프로필 갱신 |
| 하트 | 전송/수령량, 전송 비용, 한도, 쿨다운/리셋 시각, 만료 | sync 트랜잭션 |
| 추천 | 최근 접속 기간, 노출 정책, 최대 친구 수 | 목록·요청 검증 |
| 전투 메시지 | 신규 코드/DTO, 이벤트 payload 또는 결과 조회 방식 | TCP 확장 |

## 9. 통합 검증 완료 기준

- 미인증·타 계정 접근·미소유 캐릭터/장비 변경을 거절한다.
- 친구 요청 중복/양방향 경합, 추천 후 관계 변경이 일관되게 처리된다.
- 친구 창 반복 열기와 동시 sync, 응답 유실 후 재시도에도 하트를 중복 지급/전송하지 않는다.
- 화면에 보이지 않는 친구도 서버의 하트 자동 처리 대상에 포함된다.
- 사망 이벤트 중복/동시 정산/서버 재시작에도 동일 정산 경험치는 한 번만 증가한다.
- 클라이언트가 임의 킬 수·경험치를 보내도 정산에 사용되지 않는다.
- 0킬, 다중 레벨 상승, 최대 레벨, 마지막 킬과 사망 경합을 확정된 정책대로 처리한다.
- 정산 커밋 후 이벤트 전달 실패도 조회로 복구한다. 이벤트+조회 중복으로 연출/지급하지 않는다.
- 재접속 시 참여 기록과 정산 결과를 복구하고, pending/실패/완료를 구분한다.

## 10. 두 모드의 게임 로그 저장과 솔로 프로토타입

게임 모드는 `dedicated_battle`(데디케이티드 전투 서버)과 `solo_defense`(클라이언트 단독 디펜스)다.
`GameSessionTracker`는 두 모드를 공통으로 추적한다. 현재 실제 플레이 연결은 솔로만 완료되었다.
이 절의 두 프로토타입 전송기는 평면 JSON 응답을 읽는다. 3.2절의 운영 공통 `data` envelope를
적용할 때는 서버와 함께 전송기의 파서를 변경해야 한다.

### 10.1 게임 결과 로그 저장

`POST /api/v1/me/game-results`, 멱등 키 `{runId}:game-result`.
이 API는 분석용 플레이 로그 저장이며 경험치 정산과 별도다.

본문은 `Assets/Scripts/Gameplay/GameSessionTracker.cs`의 `GameRunSnapshot`이다.

| 필드 | 의미 |
| --- | --- |
| schemaVersion, runId, gameMode, source | 현재 버전 1, 클라이언트 UUID(N), 위 두 모드, `client_unverified` |
| battleId, participationId | 데디케이티드 모드의 서버 발급 ID, 솔로 프로토타입에서는 미지정 |
| startedAt, endedAt, clientVersion | UTC ISO 8601 시각, 클라이언트 버전 |
| endReason | `death`, `beacon_destroyed`, `restarted`, `application_quit` |
| playedSeconds, kills, reachedWave | 일시정지 제외 시뮬레이션 시간, 처치 수, 도달 웨이브 |
| shotsFired, hits, damageTaken, beaconDamage | 발사/명중/실제 감소 체력 집계 |
| playerHealth, beaconHealth | 종료 시 체력, 데디케이티드의 비콘 체력은 0 |
| goldCollected, goldSpent, equippedWeapon | 판 내 골드 획득/사용량과 장비 식별자(현재 솔로는 `SIGNAL_BLASTER`, 이전 기록은 `PISTOL`/`CARBINE`/`SCATTERGUN`); 계정 재화/소유권과 무관 |
| events, droppedLogEvents | 상세 이벤트 목록 및 상한 초과 누락 수 |

이벤트는 `sequence`, `elapsedSeconds`, `type`, `value`, `x`, `y`를 가진다.
타입은 `run_started`, `position`, `wave_started`, `enemy_spawned`, `shot`, `hit`,
`kill`, `player_damage`, `beacon_damage`, `dash`, `paused`, `resumed`, `run_ended`다.
위치는 약 1초 간격, 상세 기록은 최대 2,048개 + 종료 이벤트다. 드롭 시 sequence에 간격이 생긴다.
완전한 입력 리플레이가 아니며 시각과 수치 모두 클라이언트 주장이다.

솔로 확장 이벤트: `gold_dropped`, `gold_collected`, `gold_spent`의 value는 금액,
`healed`, `beacon_repaired`의 value는 실제 회복량이다.
`upgrade_projectiles`, `upgrade_damage`, `upgrade_fire_rate`는 카드 선택이며 value는 선택한 웨이브다.
`shop_opened`, `shop_closed`, `wave_cleared`의 value는 현재 웨이브다.
이전 버전 로그의 `weapon_bought`, `weapon_equipped`는 0=권총/1=카빈/2=산탄총이며 현재판에서는 발생하지 않는다.
이전 버전 `shop_opened`는 킬 수, `shop_closed`는 0이므로 클라이언트 버전과 함께 해석한다.
현재 `shotsFired`는 개별 탄환 수다. 발사체가 7개로 강화되었다면 한 번 사격 시 7로 기록된다.
이 필드들은 schemaVersion 1의 추가 선택 필드다. 기존 로그에 없으면 미기록으로 취급한다.
상점 골드는 로컬 판의 기획 자원이며 계정 지갑 API를 호출하거나 영구 재화를 지급하지 않는다.

저장 완료 응답은 `{"status":"stored","runId":"요청 ID","resultId":"서버 저장 ID"}`.
서버는 인증 주체와 runId에 고유 제약을 두고, 동일 본문 재요청에 동일 저장 결과를 반환한다.
같은 키의 다른 본문은 409로 거절한다. 이벤트 수/본문 크기/수치 유효성을 제한하며,
인증 주체가 해당 battleId/participationId에 속하는지 검증한다. 보상 원장으로 사용하지 않는다.
프로토타입 전송기에는 아직 인증이 없으므로 운영 연결 시 Bearer 토큰을 추가해야 한다.

클라이언트는 종료 시 불변 스냅샷을 먼저 로컬 저장하고 실제 요청한다.
실패 UI와 수동 재시도는 구현되어 있다. 저장 확인 때만 해당 로컬 백업을 제거한다.
앱 재시작 시 백업 자동 재전송, 계정별 큐 분리, 보관 기간/용량 한도는 후속 작업이다.

### 10.2 솔로 보상 요청

현재 클라이언트 전송 경로는 `POST /api/v1/prototype/defense-runs/{runId}/rewards`다.
멱등 키는 runId. 본문은 `runId`, `mode=solo_defense`, `reason`, `claimedKills`,
`reachedWave`, `survivalSeconds`다. 로그 저장 요청과 독립적으로 전송한다.

응답은 `status=settled`, 동일 `runId`, 비어 있지 않은 `settlementId`,
0 이상의 정수 `earnedExperience`가 필요하다. 클라이언트는 표시만 하며 프로필을 수정하지 않는다.
서버가 없는 기본 주소는 `https://127.0.0.1:18080`, 요청 타임아웃은 5초다.
서버 부재/HTTP 오류/미확정 응답은 실패로 표시하며 경험치를 임의 지급하지 않는다.

이 경로는 개발용 제안이다. 클라이언트 킬 수를 그대로 신뢰하는 운영 보상 API로 배포하면 안 된다.
운영 솔로에는 서버 발급 실행 세션, 검증 또는 제한 보상 정책 및 멱등 정산 원장을 먼저 정의한다.
플레이어 사망 시 킬 수로 경험치를 정산한다는 원칙은 유지하되 계산식은 미결정이다.
비콘 파괴에서도 프로토타입은 종료 요청을 보내며 실제 지급 여부는 서버 정책으로 확정한다.
데디케이티드 모드는 기존 6절대로 신뢰된 전투 서버의 사망/킬 결과를 기준으로 정산한다.
