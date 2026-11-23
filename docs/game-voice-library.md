# GameVoice library (shared voice code)

Voice chat in this app now comes from the [GameVoice](https://github.com/djaus2/GameVoice) Razor class library,
published as the [`GameVoice`](https://www.nuget.org/packages/GameVoice) package on nuget.org. The same package is used by Draughts and Reversi and
is designed to be adopted by WalkieTalkie and a future Chess app.

## What moved to GameVoice

| Before (in this app) | Now |
|---|---|
| `window.DraughtsVoice` in `wwwroot/js/draughtsGame.js` | `window.GameVoice` in `_content/GameVoice/gameVoice.js` |
| Voice floor, mute and per-user TTS voice fields and methods in `Services/DraughtsService.cs` | `VoiceRoom` (one per game: `game.Voice`) and `VoiceRoomService` |
| Talk / Mute buttons and push-to-talk code in `Components/DraughtsGame.razor` | `<VoiceControls>` component |
| `Services/VoiceCatalog.cs` | `GameVoice.VoiceCatalog` |
| `UsableMsVoiceService` | Still in this app (the `UsableMsVoice` table stays here); it now also implements `GameVoice.IVoiceCatalog` |
| `window.DraughtsChat` in `wwwroot/js/draughtsGame.js` and the inline `window.lobbyChat` script in `Components/LobbyChat.razor` (file deleted) | `window.GameChat` in `_content/GameVoice/gameChat.js` |
| `DraughtsGame.ChatMessage` record and `List<ChatMessage> ChatMessages` | `GameVoice.ChatMessage` and `GameVoice.ChatLog` (`game.ChatMessages`); the chat textarea uses `ChatLog.Format` |
| `Services/LobbyChatService.cs` | `GameVoice.LobbyChatService`, registered with `AddGameChat()` |

Still in this app: the chat and lobby page layout (`LobbyChat.razor`, the chat panel in `DraughtsGame.razor`), reading voice messages aloud
(`QueueSpeakNewVoiceMessages` in `DraughtsGame.razor`, calling `GameVoice.speak*`), the voice picker on `Player.razor`,
the admin voice catalogue pages and the per-user voice columns in the `Users` table.

## How it is wired

- `DraughtsService` implements `IVoiceRoomHost`: `FindVoiceRoom(gameId)` returns `game.Voice`, and `OnVoiceRoomChanged`
  touches the game's idle timer, raises `GameUpdated` and, when a preference changes, announces both players' voices.
- `Program.cs` registers `IVoiceRoomHost` (the game service), `IVoiceCatalog` (`UsableMsVoiceService`) and calls
  `AddGameVoice()` and `AddGameChat()`.
- `Components/App.razor` loads `_content/GameVoice/gameVoice.js` and `_content/GameVoice/gameChat.js`.
- `DraughtsGame.razor` renders `<VoiceControls>` and posts each transcript to chat with the `[VOICE] ` prefix.

## Building

`GameVoice` restores from nuget.org like any other package; no extra package source or token is needed.

## Updating GameVoice

Change the library in the GameVoice repo, push a `vX.Y.Z` tag to publish, then bump
`<PackageReference Include="GameVoice" Version="X.Y.Z" />` in `Draughts.csproj`.
