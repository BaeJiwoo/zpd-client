# English UI and NEXON Lv.1 Gothic

Game-owned text is English in Login, Lobby, LegacyLobby, SoloDefense and
ConnectionTest, including runtime messages, upgrades, help, results and controls.
Account names and other server/user-provided content are displayed as supplied.

The bundled font is the unmodified official `NEXONLv1GothicRegular.ttf` under
`Assets/Resources/Fonts/NexonLv1`. Scene-authored UGUI labels, scene builders and
the connection test's IMGUI styles use it. The old Noto asset is retained for
compatibility but is no longer assigned to game UI.

The [official FAQ](https://brand.nexon.com/en/ci-brand-guidelines/typeface#section-faq),
checked on 2026-09-25, permits commercial use and software embedding when the
original font copyright notice accompanies the font. Selling the font separately
is prohibited. Attribution is recommended.

The original notice extracted from the font and official download/source URLs
are included in `Assets/StreamingAssets/ThirdParty/NexonLv1/NOTICE.txt`, so they
also accompany player builds. The defense help screen credits the font.

`Tools/LobbyValidation/PresentationChecks.cs` rebuilds the login layout and checks
scene labels for untranslated Hangul while assigning the bundled font. Run this
migration only in the isolated validation project; review scene changes before
copying them back. Normal builds use the saved scene assets directly.
