# Creation of Draughts-Checkers Game in Blazor with LAN multiplayer support

## "First game!" Commit
What the app could do (user-visible)
- Display a playable Draughts/checkers board in the browser.
- Allow a single user to select pieces and make legal moves (basic move and capture rules).
- Enforce turn-taking and basic promotion to king.
- Provide minimal UI for creating/joining games (single‑instance play).

Developer notes (implementation)
- Core game UI and logic introduced (component: `Components/DraughtsGame.razor`).
- Initial game state model and move validation implemented (service: `Services/DraughtsService.cs` or equivalent).
- Local-only behavior: game state stored in memory; no network/multi-client support yet.
- Useful for rapid iteration and validating rules/UI interaction.

Testing / verification
- Verify selection, move validation, captures and promotions on the local page.
- Confirm UI shows current turn and selection messages.

---

## "Can play over 2 computers locally" Commit
What the app could do (user-visible)
- Two players can play against each other from two different machines on the same LAN.
- Creation of a shareable JoinLink (URL) and ability to copy/open it in a second window to join a running game.
- Each client sees the same board state; moves from one client are reflected on the other.
- UI shows player number (Player 1 / Player 2) and connection status.

Developer notes (implementation)
- Introduced multi-client synchronization: a singleton game service (in-memory) that holds games and raises updates (e.g., `GameUpdated` event).
- `TryJoinGame` API that assigns player numbers and prevents over-joining.
- Joined-client flow: CreateGame → Navigate to `/Draughts?gameId=...` → BeginJoin(server assigns player).
- Defensive changes to prevent the local browser from occupying both slots (fixed double‑join race).
- Added simple client helpers: "Copy join link" and "Open in new window".

Testing / verification
- Create a game on machine A, open the JoinLink on machine B (use incognito/private to avoid session reuse) and confirm Player 2 joins.
- Confirm moves on A appear on B and vice versa.
- Check server logs for TryJoinGame assignments and GameUpdated events.

---

## "V1.0.0 Works on local network" Commit
What the app could do (user-visible)
- Stable LAN multiplayer baseline suitable for local network play (release-quality for dev/test).
- Reliable join flow and basic diagnostics when join fails (clear "Unable to join" messages).
- Copy/open join link UI, and improved feedback when moves are invalid.
- Better visual pieces (filled discs, kings show crown) and accessible button UI (non-animated, good contrast if applied).

Developer notes (implementation / ops)
- Tagged/stabilized as v1.0.0 baseline — key fixes and polish merged: logging, click diagnostics, join flow hardening.
- Server logging (`ILogger`) added to trace CreateGame, GetGame, TryJoinGame, MakeMove — helps diagnosing join/move problems.
- Guidance and optional changes included to run Kestrel on non‑localhost (e.g., `ListenAnyIP` or `--urls "http://0.0.0.0:5000"`) and firewall tip for LAN testing.
- Temporary client-side click logging added to confirm events reach the browser; recommended removal after verification.

Testing / verification
- Confirm v1.0.0 behavior across two machines on the LAN: creation, join, synchronized moves, and promotion.
- Inspect server logs for join assignments and move application lines.
- Test LAN URL access and firewall rules (HTTP recommended for local dev).

---

## Comments

- Initially was able to play a game in different tabs in same browser,
- Later became an issue when games were user oriented.
- Could then run one player in broswer and second in 
  - Incognito/private window ... this was bit problemaric
  - Was simpler to run one player in Edge and second in Chrome on same desktop
- Later ran the service via Kestral and then could have both player in same network on different machines (LAN).
  - E.g. Two desktops or desktop plus _(Android)_ phone!
