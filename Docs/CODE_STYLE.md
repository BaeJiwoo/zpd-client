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
- Preserve Unity callback names, serialized field names, public bindings and JSON DTO names. Naming migrations require separate compatibility work.
- Do not change generated protocol code or third-party code to match this style.

`LoginController.LoginAsync` is a reference for separating validation, request handling, session checks and navigation.

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
