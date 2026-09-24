# 로비 화면

Unity 6000.3.11f1에서 `ZPD > Lobby > Create Lobby Scene` 메뉴를 실행하세요.
씬의 모든 GameObject, UI 컴포넌트, 캐릭터 스프라이트, 스크롤 영역과 버튼 이벤트가
에디터에서 생성되어 `Assets/Scenes/Lobby.unity`에 저장됩니다. 같은 이름의 씬이
있으면 고유한 파일명으로 새 씬을 저장하므로 기존 작업을 덮어쓰지 않습니다.
열려 있는 씬이 수정되어 있으면 Unity의 저장 확인창이 표시됩니다.

생성된 씬은 스크립트를 다시 실행하지 않아도 바로 Play할 수 있습니다.
Hierarchy와 Inspector에서 위치, 색상, 문구, 스프라이트, 이벤트를 수정하세요.
기존 ConnectionTest 씬과 Build Settings는 자동 변경하지 않습니다.

## 캐릭터 스타일 UI

굵은 검은 외곽선, 보라색 패널/버튼, 아이보리 아이템 카드, 주황색 전투 버튼과
가방/친구 아이콘을 직접 제작한 UI 원화로 적용했습니다.
`Assets/Resources/UI/Lobby/cartoon-ui-atlas.png`는 OpenAI image_gen으로 만든 원화이며
캐릭터와 무기는 기존 Rgsdev CC0 애셋입니다. 둘의 출처는 별도로 표시합니다.

`ZPD > Lobby > Apply Cartoon Style to Open Lobby`는 현재 씬의 레이아웃과 버튼 연결을
유지하면서 스타일을 적용합니다. 적용 후 씬을 저장하세요. Undo로 되돌릴 수 있고,
반복 적용 시 아이콘과 배경을 중복 생성하지 않습니다. 새 씬 생성 메뉴에도 자동 적용됩니다.
패널/버튼은 9-slice를 사용하고, 문구는 Unity Text로 편집할 수 있습니다.
원화의 정확한 프롬프트는 `Assets/ArtSource/Lobby/IMAGEGEN.md`에 보관했습니다.

## 구성

- `Lobby Camera`
- `Lobby Canvas / Background`
- `Lobby Canvas / Lobby`: 로컬 캐릭터 원화 미리보기, 프로필/친구/인벤토리/전투/캐릭터 변경 버튼, 장비 placeholder
- `Lobby Canvas / Modal Backdrop`: 바깥 영역 클릭으로 닫기
- `Lobby Canvas / Panels`: User Profile, Friends Drawer, Inventory Drawer, Character Selection
- `EventSystem`: Input System UI 모듈

1280×720 기준, CanvasScaler의 Expand 방식으로 전체 레이아웃을 화면 안에 맞춥니다.
친구 창은 오른쪽에서, 인벤토리는 아래에서 슬라이드합니다. 휠/드래그로
4열 아이템 그리드를 스크롤할 수 있습니다. 닫기 버튼, 바깥 클릭, Escape로 닫습니다.
전환은 unscaled time을 사용하므로 timeScale이 0이어도 동작합니다.

## API 연결 전 동작

레벨·전적·친구·접속 시각·하트 잔액·장비·소유 캐릭터는 모두 **미확인 placeholder (`--`)**입니다.
미연결은 빈 목록이나 0으로 표현하지 않습니다. 중앙 캐릭터 그림도 현재 사용 캐릭터가
아닌 `LOCAL ART PREVIEW`이며, 현재 캐릭터 표시는 `CHARACTER: --`입니다.
요청 지점은 `[Lobby API stub]` 로그만 남깁니다. 요청 전송, 성공, 수령 완료, 친구 추가,
장착/캐릭터 변경을 가정하지 않고, 영구 로딩 상태에도 들어가지 않습니다.

- 프로필 열기: `profile.get / self`
- 친구 목록: 이름/상태는 placeholder, 하트는 수동 버튼 없이 `AUTO / --`로 표시
- 추천 목록(SUGGESTED): 최근 접속 유저에게 바로 `ADD FRIEND` 요청을 보내는 용도
- `REFRESH`: 현재 소셜 탭을 다시 조회하는 로그. 기존 대상 ID/가능 여부를 지워 오래된 대상으로 요청하지 않음
- 하트 자동 송수신: 친구 창 열기/새로고침 시 받을 수 있는 하트를 받고, 보낼 수 있는 친구에게 보냄
- 하트함(HEARTS): 읽기 전용 이력 placeholder. 자동 송수신을 위해 이 탭을 열 필요 없음
- 현재는 API 로그만 출력. 서버가 확인한 가능 목록 없이 하트를 처리하거나 잔액을 바꾸지 않음
- 동일 하트/전송 가능 회차 중복 처리와 오래된 응답을 차단
- 검색: 공백을 제거한 검색어가 있을 때만 `friends.search` 출력, 최대 32자
- 전투 입장: `battle.enter`
- 인벤토리: `inventory.get / self`
- 아이템: 12개 placeholder 슬롯만 에디터에서 생성, 데이터가 없으므로 선택 비활성

캐릭터 변경 창은 Rgsdev의 로컬 원화 4종을 미리보기로 보여줍니다. 미리보기 선택은
로비/프로필의 현재 캐릭터를 바꾸지 않습니다. 서버 ID와 소유 여부를 확인한 캐릭터만
적용을 요청할 수 있으며, 로그 출력 자체로 변경되지 않습니다.
API 전송/응답기는 구현하지 않았습니다. 추후 연결할 UI 바인딩 지점과 권한 조건은
[`API_INTEGRATION.md`](API_INTEGRATION.md)에 정리했습니다.

현재 UI 문구는 Unity 내장 폰트의 호환성을 위해 영어로 구성했습니다.
한국어 UI로 변경할 때에는 재배포 가능한 한글 폰트를 추가하고 빌더의 폰트와 문구를
교체하세요. API는 나중에 LobbyController의 로그 지점에 연결하면 됩니다.

## 리소스와 크레딧

중복된 배포 폴더를 `Assets/Resources/Art/Rgsdev`로 정리했습니다.
PNG와 `.meta`를 함께 이동해 기존 GUID/스프라이트 슬라이싱을 보존했습니다.
라이선스와 외부 링크는 빌드용 Resources에서 `Assets/ThirdParty/Rgsdev`로 분리했습니다.
`Assets/ThirdParty/CREDITS.md`에 모든 사용 리소스의 제작자·출처를 정리했습니다.
Rgsdev 크레딧과 원본 `License.txt`도 보존하며, CC0 리소스도 화면 하단에 크레딧을 표시합니다.

## 점검

`ZPD > Lobby > Validate Open Lobby Scene`에서 참조 누락, 버튼의 영구 이벤트 연결,
소셜 탭 4개, 12개 placeholder 슬롯, 캐릭터 미리보기 4개와 초기 비활성 조건,
EventSystem 중복을 점검합니다.
런타임 코드에는 오브젝트 생성, Resources.Load, 네트워크 호출이 없습니다.
