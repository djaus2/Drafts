# GameVoice library (shared voice code)

Voice chat in this app now comes from the [GameVoice](https://github.com/djaus2/GameVoice) Razor class library,
published as the `GameVoice` NuGet package on GitHub Packages. The same package is used by Draughts and Reversi and
is designed to be adopted by WalkieTalkie and a future Chess app.

## What moved to GameVoice

| Before (in this app) | Now |
|---|---|
| `window.DraughtsVoice` in `wwwroot/js/draughtsGame.js` | `window.GameVoice` in `_content/GameVoice/gameVoice.js` |
| Voice floor, mute and per-user TTS voice fields and methods in `Services/DraughtsService.cs` | `VoiceRoom` (one per game: `game.Voice`) and `VoiceRoomService` |
| Talk / Mute buttons and push-to-talk code in `Components/DraughtsGame.razor` | `<VoiceControls>` component |
| `Services/VoiceCatalog.cs` | `GameVoice.VoiceCatalog` |
| `UsableMsVoiceService` | Still in this app (the `UsableMsVoice` table stays here); it now also implements `GameVoice.IVoiceCatalog` |

Still in this app: text chat (`window.DraughtsChat` in `wwwroot/js/draughtsGame.js`), reading voice messages aloud
(`QueueSpeakNewVoiceMessages` in `DraughtsGame.razor`, calling `GameVoice.speak*`), the voice picker on `Player.razor`,
the admin voice catalogue pages and the per-user voice columns in the `Users` table.

## How it is wired

- `DraughtsService` implements `IVoiceRoomHost`: `FindVoiceRoom(gameId)` returns `game.Voice`, and `OnVoiceRoomChanged`
  touches the game's idle timer, raises `GameUpdated` and, when a preference changes, announces both players' voices.
- `Program.cs` registers `IVoiceRoomHost` (the game service), `IVoiceCatalog` (`UsableMsVoiceService`) and calls
  `AddGameVoice()`.
- `Components/App.razor` loads `_content/GameVoice/gameVoice.js` before `js/draughtsGame.js`.
- `DraughtsGame.razor` renders `<VoiceControls>` and posts each transcript to chat with the `[VOICE] ` prefix.

## Building: GitHub Packages token

`nuget.config` adds the `github-djaus2` source for the `GameVoice` package only. GitHub Packages needs a
token with `read:packages` even for restore. Once per machine (Visual Studio uses the same user config):

```bash
dotnet nuget update source github-djaus2 --username <github-user> --password <PAT> --store-password-in-clear-text --configfile %APPDATA%\NuGet\NuGet.Config
```

If that fails because the source isn't in your user config, use `dotnet nuget add source https://nuget.pkg.github.com/djaus2/index.json --name github-djaus2` with the same options.
Never commit the token. Any build server that restores packages (for example an Azure or GitHub Actions build) needs the same credential.

## Updating GameVoice

Change the library in the GameVoice repo, push a `vX.Y.Z` tag to publish, then bump
`<PackageReference Include="GameVoice" Version="X.Y.Z" />` in `Draughts.csproj`.
