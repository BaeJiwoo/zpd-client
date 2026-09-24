# Game resource credits

Credits are retained for every external asset, including CC0 assets.

| Resource | Creator / source | License / provenance | Used for |
| --- | --- | --- | --- |
| Free 2D Animated Vector Game Character Sprites | Raphael Gonçalves (Rgsdev), https://rgsdev.itch.io/ | CC0, as stated in the bundled [original license](Rgsdev/License.txt) | Lobby character previews and portraits; Defense Char 1, Enemy 1/2/4, weapons R1/R2/R3, bullet and crosshair |
| Cartoon lobby UI atlas | OpenAI image_gen; art direction and integration by Codex for this project | Original AI-generated project artwork; not claimed to be CC0 | Panel, item card, violet and orange buttons, backpack and friends icons |
| LegacyRuntime font | Unity, built-in engine resource | Provided with Unity; not a downloaded CC0 font | Editable UI text |
| Noto Sans KR Regular | Google / Adobe Noto CJK project, [official source](https://github.com/notofonts/noto-cjk/tree/Sans2.004) | SIL OFL 1.1, [license and credit](NotoSansKR/CREDITS.md) | All solo-defense UI, Korean help |
| Last Signal meadow | OpenAI image_gen; Codex art direction using the Rgsdev character as a style reference | Original AI-generated project background, not claimed to be CC0; [exact prompts](../ArtSource/Defense/IMAGEGEN.md) | Full-bleed evacuation-trail field |
| Defense arcade sound effects (11 WAVs) | Codex for this project; editor synthesis in `DefenseSoundBuilder.cs` | Original project waveforms, no external recordings or samples | Shot, scatter, hit, kill, pickup, shop, purchase, hurt, defeat, warning, dash |

Rgsdev's complete attribution is in [Rgsdev/CREDITS.md](Rgsdev/CREDITS.md).
The generated UI atlas is `Assets/Resources/UI/Lobby/cartoon-ui-atlas.png`.
Its exact prompt and generation method are preserved in
[IMAGEGEN.md](../ArtSource/Lobby/IMAGEGEN.md).

The lobby footer also displays **Art: Rgsdev (CC0) | UI: OpenAI / Codex**.
Simple background streaks and Unity UI layout were authored in this project.
Defense crystal artwork was generated using the built-in OpenAI image_gen tool; original project artwork, not claimed to be CC0. [Exact prompt](../ArtSource/Defense/IMAGEGEN.md). Asset: `Assets/Resources/Art/Defense/defense-crystal.png`.
Gold-pile geometry, solid sprite and unlit material were also authored in this project.
Defense reuses the generated cartoon atlas and displays resource credits inside ESC → ? help.
No additional third-party UI pack was downloaded for this update.
