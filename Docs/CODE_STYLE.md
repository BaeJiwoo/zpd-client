# C# code style

Follow [Microsoft's common C# conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), adapted to Unity's C# 9 support and serialized data contracts.

- Use four spaces, never tabs.
- Put opening and closing braces on separate lines (Allman style).
- Use braces for control-flow bodies, including single-statement guards.
- Write one executable statement per line.
- Separate methods and logical stages with blank lines: validate input, capture request state, call the service, apply the response, and update the view.
- Keep simple auto-properties and short expression-bodied members concise.
- Wrap long calls and conditions at meaningful boundaries.
- Use English for new comments and user-facing messages. API error text belongs in `ApiErrorMessages` and is selected with `ApiErrorCode`.
- Preserve Unity callback names and JSON DTO names. When renaming serialized fields, add `FormerlySerializedAs` and update all code bindings, editor builders and reflection-based checks together.
- Do not change generated protocol code or third-party code to match this style.

`LoginController.LoginAsync` is a reference for separating validation, request handling, session checks and navigation.

## Member names

Use `snake_case` for fields. Component references start with their component type followed by their role, so their purpose is clear at the declaration and at each use.

| Component | Example |
| --- | --- |
| `Button` | `btn_login`, `btn_use_item` |
| `Text` | `txt_status`, `txt_player_name` |
| `InputField` | `input_login_id`, `input_password` |
| `Image` / `Slider` | `img_icon`, `slider_owned_quantity` |
| `Transform` / `Camera` | `transform_player`, `camera_world` |
| `CanvasGroup` | `canvas_group_home` |
| `SpriteRenderer` | `sprite_renderer_health_bar` |
| `AudioSource` / `AudioClip` | `audio_source_combat`, `audio_clip_cues` |
| `GameObject` | `game_object_pause_panel` |
| Project components | `lobby_view`, `defense_game`, `defense_supplies` |

- Use the same names for controller properties that forward scene references to a view.
- Use plural roles for collections, such as `btn_inventory_filters` and `enemy_slots`.
- Give runtime fields meaningful names, such as `account_generation`, `pending_enemy_count` and `request_json`. Boolean names express a condition, such as `is_connected`.
- Include units and distinguish time points from durations: `next_fire_at_seconds`, `invulnerable_until_seconds`, `spawn_delay_seconds`.
- Declare each field separately so its attributes and previous serialized name are unambiguous.
- Keep ordinary public properties, events, methods and constants in `PascalCase`; keep parameters and local variables in `camelCase`.
- Keep JSON DTO fields, generated protocol code and third-party APIs in their original contract naming.

Existing scenes and prefabs may still contain the previous serialized field names. Keep the `FormerlySerializedAs` attributes when saving or regenerating scenes; `LoginView.input_login_id` also retains the older `playerId` alias.

## Formatting

Run from the repository root:

```powershell
dotnet format whitespace Assets/Scripts --folder --exclude Assets/Scripts/Networking/Tcp/Generated
```

Check formatting without modifying files:

```powershell
dotnet format whitespace Assets/Scripts --folder --exclude Assets/Scripts/Networking/Tcp/Generated --verify-no-changes
```

The root `.editorconfig` stores these formatting preferences and the braces diagnostic. Whitespace formatting does not add missing braces or decide logical groupings: retain those during editing and review.
