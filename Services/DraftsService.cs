using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Drafts.Services
{
    // Simple drafts (checkers) game service for server-side interactive components.
    public class DraftsService
    {
        private readonly ILogger<DraftsService> _logger;
        private readonly LobbyChatService _lobbyChat;

        // Event raised when a game changes. Subscribers can call StateHasChanged.
        public event Action<string>? GameUpdated;

        private readonly ConcurrentDictionary<string, DraftsGame> _games = new();

        private string? FindActiveGameIdForUser(int userId)
        {
            if (userId <= 0) return null;

            foreach (var kvp in _games)
            {
                var game = kvp.Value;
                if (game.CreatedByUserId == userId || game.Player1UserId == userId || game.Player2UserId == userId)
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        public string? GetActiveGameIdForUser(int userId)
        {
            return FindActiveGameIdForUser(userId);
        }

        public DraftsService(ILogger<DraftsService> logger, LobbyChatService lobbyChat)
        {
            _logger = logger;
            _lobbyChat = lobbyChat;
        }

        public enum GameState
        {
            New,
            Connected,
            Playing,
            Finished,
            Abandoned
        }

        public sealed record GameListItem(
            string Id,
            DateTime CreatedUtc,
            DateTime StartTimeUtc,
            DateTime LastTimeUtc,
            int CreatedByUserId,
            int? Player1UserId,
            int? Player2UserId,
            bool Player1Connected,
            bool Player2Connected,
            bool HadSecondPlayerConnected,
            bool AdminMode,
            GameState State,
            int Player1PieceCount,
            int Player2PieceCount);

        public List<GameListItem> ListGames()
        {
            // Snapshot view for UI.
            return _games.Values
                .Select(g => new GameListItem(
                    g.Id,
                    g.CreatedUtc,
                    g.StartTimeUtc,
                    g.LastTimeUtc,
                    g.CreatedByUserId,
                    g.Player1UserId,
                    g.Player2UserId,
                    g.Player1Connected,
                    g.Player2Connected,
                    g.HadSecondPlayerConnected,
                    g.AdminMode,
                    g.State,
                    g.Player1PieceCount,
                    g.Player2PieceCount))
                .ToList();
        }

        public string CreateGame(int userId, int creatorPlayerNumber = 1, bool adminMode = false)
        {
            return CreateGame(userId, "", creatorPlayerNumber, adminMode);
        }

        public string CreateGame(int userId, string creatorName, int creatorPlayerNumber = 1, bool adminMode = false)
        {
            var existing = FindActiveGameIdForUser(userId);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                _logger.LogInformation("CreateGame: user {UserId} already has active game {GameId}; returning existing", userId, existing);
                return existing;
            }

            var id = Guid.NewGuid().ToString("n").Substring(0, 8);
            var game = new DraftsGame(id)
            {
                CreatedByUserId = userId
            };

            game.AdminMode = adminMode;

            if (creatorPlayerNumber != 1 && creatorPlayerNumber != 2)
            {
                creatorPlayerNumber = 1;
            }

            if (creatorPlayerNumber == 1)
            {
                game.Player1Connected = true;
                game.Player1UserId = userId;
            }
            else
            {
                game.Player2Connected = true;
                game.Player2UserId = userId;
            }

            game.Touch();

            _games[id] = game;
            _logger.LogInformation("CreateGame: {GameId}", id);

            var displayName = string.IsNullOrWhiteSpace(creatorName) ? $"User {userId}" : creatorName;
            _lobbyChat.AddSystemMessage($"New game started by {displayName}: {id}");

            OnGameUpdated(id);
            return id;
        }

        public DraftsGame? GetGame(string id)
        {
            var ok = _games.TryGetValue(id, out var g);
            _logger.LogInformation("GetGame: {GameId} found={Found}", id, ok);
            return g;
        }

        // Returns 1 or 2 for player number, 0 if cannot join
        public int TryJoinGame(string id, int userId)
        {
            var existing = FindActiveGameIdForUser(userId);
            if (!string.IsNullOrWhiteSpace(existing) && !string.Equals(existing, id, StringComparison.Ordinal))
            {
                _logger.LogWarning("TryJoinGame: user {UserId} already in game {ExistingGameId}; rejecting join to {GameId}", userId, existing, id);
                return -1;
            }

            var game = GetGame(id);
            if (game == null)
            {
                _logger.LogWarning("TryJoinGame: {GameId} not found", id);
                return 0;
            }

            lock (game)
            {
                if (game.Player1UserId == userId) return 1;
                if (game.Player2UserId == userId) return 2;

                if (game.Player1Connected && game.Player2Connected)
                {
                    _logger.LogWarning("TryJoinGame: {GameId} already full (p1={P1} p2={P2})", id, game.Player1Connected, game.Player2Connected);
                    return 0;
                }
                if (!game.Player1Connected)
                {
                    game.Player1Connected = true;
                    game.Player1UserId = userId;
                    if (game.Player2Connected)
                    {
                        game.HadSecondPlayerConnected = true;
                        if (game.State == GameState.New)
                        {
                            game.State = GameState.Connected;
                        }
                    }
                    game.Touch();
                    _logger.LogInformation("TryJoinGame: {GameId} assigned Player1", id);
                    OnGameUpdated(id);
                    return 1;
                }
                if (!game.Player2Connected)
                {
                    game.Player2Connected = true;
                    game.Player2UserId = userId;
                    if (game.Player1Connected)
                    {
                        game.HadSecondPlayerConnected = true;
                        if (game.State == GameState.New)
                        {
                            game.State = GameState.Connected;
                        }
                    }
                    game.Touch();
                    _logger.LogInformation("TryJoinGame: {GameId} assigned Player2", id);
                    OnGameUpdated(id);
                    return 2;
                }
                _logger.LogWarning("TryJoinGame: {GameId} unexpected state", id);
                return 0;
            }
        }

        public int RemoveGamesForUser(int userId)
        {
            var removed = 0;
            foreach (var kvp in _games)
            {
                var game = kvp.Value;
                if (game.CreatedByUserId == userId || game.Player1UserId == userId || game.Player2UserId == userId)
                {
                    if (_games.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                        _logger.LogInformation("RemoveGamesForUser: removed game {GameId} for user {UserId}", kvp.Key, userId);
                        OnGameUpdated(kvp.Key);
                    }
                }
            }

            return removed;
        }

        public bool RemoveGame(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return false;
            if (_games.TryRemove(gameId, out _))
            {
                _logger.LogInformation("RemoveGame: removed game {GameId}", gameId);
                OnGameUpdated(gameId);
                return true;
            }

            return false;
        }

        public bool AddChatMessage(string gameId, int senderUserId, string senderName, string text)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return false;
            if (senderUserId < 0) return false;

            text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(text)) return false;

            var game = GetGame(gameId);
            if (game is null) return false;

            lock (game)
            {
                game.ChatMessages.Add(new DraftsGame.ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, text));
                game.Touch();
            }

            OnGameUpdated(gameId);
            return true;
        }

        // Make a move. Returns (success, message).
        public (bool success, string? message) MakeMove(string id, int player, int fr, int fc, int tr, int tc)
        {
            _logger.LogInformation("MakeMove: {GameId} player={Player} {Fr},{Fc} -> {Tr},{Tc}", id, player, fr, fc, tr, tc);
            var game = GetGame(id);
            if (game == null) return (false, "Game not found");
            lock (game)
            {
                if (game.State == GameState.Finished || game.State == GameState.Abandoned)
                {
                    return (false, $"Game is {game.State}");
                }

                if (game.State == GameState.New)
                {
                    return (false, "Waiting for second player");
                }

                game.Touch();
                if (game.CurrentTurn != player) return (false, "Not your turn");
                if (!IsInside(fr, fc) || !IsInside(tr, tc)) return (false, "Out of bounds");
                var piece = game.Board[fr, fc];
                if (piece == 0) return (false, "No piece at source");

                if (!BelongsToPlayer(piece, player)) return (false, "Not your piece");
                if (game.Board[tr, tc] != 0) return (false, "Target not empty");

                var dr = tr - fr;
                var dc = tc - fc;
                var absdr = Math.Abs(dr);
                var absdc = Math.Abs(dc);

                // Normal move: diagonal by 1
                if (absdr == 1 && absdc == 1 && IsForwardMove(piece, dr))
                {
                    game.Board[tr, tc] = piece;
                    game.Board[fr, fc] = 0;

                    game.LastMoveFromR = fr;
                    game.LastMoveFromC = fc;
                    game.LastMoveToR = tr;
                    game.LastMoveToC = tc;
                    game.LastMoveCapturedSquares.Clear();

                    MaybePromote(game, tr, tc);
                    game.CurrentTurn = 3 - player;

                    if (game.State == GameState.Connected)
                    {
                        game.State = GameState.Playing;
                    }

                    game.RecountPieces();
                    if (game.Player1PieceCount == 0 || game.Player2PieceCount == 0)
                    {
                        MarkFinished(game);
                    }

                    game.Touch();
                    OnGameUpdated(id);
                    _logger.LogInformation("MakeMove: {GameId} move applied", id);
                    return (true, null);
                }

                // Capture: jump by 2 over opponent
                if (absdr == 2 && absdc == 2)
                {
                    var midr = fr + dr / 2;
                    var midc = fc + dc / 2;
                    var mid = game.Board[midr, midc];
                    if (mid == 0 || BelongsToPlayer(mid, player)) return (false, "No opponent to capture");

                    if (!IsForwardMove(piece, dr) && !IsKing(piece)) return (false, "Invalid direction");
                    // perform capture
                    game.Board[tr, tc] = piece;
                    game.Board[fr, fc] = 0;
                    game.Board[midr, midc] = 0;

                    game.LastMoveFromR = fr;
                    game.LastMoveFromC = fc;
                    game.LastMoveToR = tr;
                    game.LastMoveToC = tc;
                    game.LastMoveCapturedSquares.Clear();
                    game.LastMoveCapturedSquares.Add(new DraftsGame.BoardPos(midr, midc));

                    MaybePromote(game, tr, tc);
                    // NOTE: Not implementing multiple-jump forcing — simple single capture.
                    game.CurrentTurn = 3 - player;

                    if (game.State == GameState.Connected)
                    {
                        game.State = GameState.Playing;
                    }

                    game.RecountPieces();
                    if (game.Player1PieceCount == 0 || game.Player2PieceCount == 0)
                    {
                        MarkFinished(game);
                    }

                    game.Touch();
                    OnGameUpdated(id);
                    _logger.LogInformation("MakeMove: {GameId} capture applied", id);
                    return (true, null);
                }

                return (false, "Illegal move");
            }
        }

        // Admin mode move. Moves for the side whose turn it is.
        public (bool success, string? message) MakeMoveAsAdmin(string id, int fr, int fc, int tr, int tc)
        {
            var game = GetGame(id);
            if (game == null) return (false, "Game not found");

            int actingPlayer;
            lock (game)
            {
                if (!game.AdminMode) return (false, "Not an admin-mode game");
                if (game.State == GameState.Finished || game.State == GameState.Abandoned)
                {
                    return (false, $"Game is {game.State}");
                }

                // Allow admin to start making moves immediately for testing.
                // (Normal games block moves in the New state until a second player joins.)
                if (game.State == GameState.New)
                {
                    game.State = GameState.Connected;
                }

                // Act as whoever's turn it is.
                actingPlayer = game.CurrentTurn;
            }

            // Call MakeMove outside the lock to avoid deadlocking on the same game lock.
            return MakeMove(id, actingPlayer, fr, fc, tr, tc);
        }

        // Admin mode delete (right-click). Deletes any piece at a cell.
        public (bool success, string? message) DeletePieceAsAdmin(string id, int r, int c)
        {
            var game = GetGame(id);
            if (game == null) return (false, "Game not found");

            lock (game)
            {
                if (!game.AdminMode) return (false, "Not an admin-mode game");
                if (game.State == GameState.Finished || game.State == GameState.Abandoned)
                {
                    return (false, $"Game is {game.State}");
                }

                if (!IsInside(r, c)) return (false, "Out of bounds");
                if (game.Board[r, c] == 0) return (false, "No piece");

                game.Board[r, c] = 0;
                game.RecountPieces();
                if (game.Player1PieceCount == 0 || game.Player2PieceCount == 0)
                {
                    MarkFinished(game);
                }

                game.Touch();
                OnGameUpdated(id);
                return (true, null);
            }
        }

        public void ClearLastMoveHighlight(string id)
        {
            var game = GetGame(id);
            if (game == null) return;

            lock (game)
            {
                game.LastMoveFromR = null;
                game.LastMoveFromC = null;
                game.LastMoveToR = null;
                game.LastMoveToC = null;
                game.LastMoveCapturedSquares.Clear();
                game.Touch();
            }

            OnGameUpdated(id);
        }

        private static void MarkFinished(DraftsGame game)
        {
            if (game.State == GameState.Finished)
            {
                return;
            }

            game.State = GameState.Finished;

            var winner = 0;
            if (game.Player1PieceCount > 0 && game.Player2PieceCount == 0) winner = 1;
            else if (game.Player2PieceCount > 0 && game.Player1PieceCount == 0) winner = 2;

            if (winner != 0)
            {
                game.WinnerPlayer = winner;
            }

            if (!game.GameOverMessageSent)
            {
                game.GameOverMessageSent = true;
                var text = winner == 0 ? "Game over." : $"Game over. Player {winner} wins.";
                game.ChatMessages.Add(new DraftsGame.ChatMessage(DateTime.UtcNow, 0, "System", text));
            }
        }

        private static bool IsInside(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;

        private static bool BelongsToPlayer(int piece, int player)
        {
            if (player == 1) return piece == 1 || piece == 3;
            return piece == 2 || piece == 4;
        }

        private static bool IsKing(int piece) => piece == 3 || piece == 4;

        private static bool IsForwardMove(int piece, int dr)
        {
            // Player1 (pieces 1/3) move up (dr < 0); Player2 (2/4) move down (dr > 0)
            if (piece == 1) return dr < 0;
            if (piece == 2) return dr > 0;
            // kings can move any direction
            return true;
        }

        private static void MaybePromote(DraftsGame game, int r, int c)
        {
            var p = game.Board[r, c];
            // Promotion rules:
            if (p == 1 && r == 0) game.Board[r, c] = 3;
            if (p == 2 && r == 7) game.Board[r, c] = 4;
            // if already king (3 or 4) nothing to do
        }

        // Event invoker - must be inside the declaring type to invoke the event.
        private void OnGameUpdated(string gameId)
        {
            GameUpdated?.Invoke(gameId);
        }

        public int RemoveGamesExceedingRuntime(TimeSpan maxRuntime)
        {
            if (maxRuntime <= TimeSpan.Zero) return 0;

            var now = DateTime.UtcNow;
            var removed = 0;

            foreach (var kvp in _games)
            {
                var game = kvp.Value;
                var runtime = now - game.StartTimeUtc;
                if (runtime > maxRuntime)
                {
                    if (_games.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                        _logger.LogInformation("RemoveGamesExceedingRuntime: removed {GameId} runtime={Runtime}", kvp.Key, runtime);
                        OnGameUpdated(kvp.Key);
                    }
                }
            }

            return removed;
        }

        public (int removed, int warningsSent) ProcessIdleTimeouts(TimeSpan maxIdle, TimeSpan killGrace, double warningFraction = 0.8)
        {
            if (maxIdle <= TimeSpan.Zero) return (0, 0);
            if (warningFraction <= 0) warningFraction = 0.8;
            if (warningFraction >= 1) warningFraction = 0.8;

            var now = DateTime.UtcNow;
            var warnAt = TimeSpan.FromTicks((long)(maxIdle.Ticks * warningFraction));
            if (killGrace <= TimeSpan.Zero) killGrace = TimeSpan.FromSeconds(1);

            var removed = 0;
            var warningsSent = 0;

            foreach (var kvp in _games)
            {
                var gameId = kvp.Key;
                var game = kvp.Value;

                var remove = false;
                var warn = false;
                var killMsg = false;
                var skipMonitoring = false;

                lock (game)
                {
                    if (game.KillAfterUtc.HasValue && now >= game.KillAfterUtc.Value)
                    {
                        remove = true;
                    }

                    if (!remove && !game.HadSecondPlayerConnected)
                    {
                        skipMonitoring = true;
                    }

                    if (skipMonitoring)
                    {
                        // Game hasn't actually started yet (no second player has connected),
                        // so don't warn/kill it for inactivity.
                        // We still allow removal if KillAfterUtc has already elapsed.
                        continue;
                    }

                    var idle = now - game.LastTimeUtc;
                    if (!remove && idle >= maxIdle)
                    {
                        if (!game.IdleKillMessageSent)
                        {
                            killMsg = true;
                            game.IdleKillMessageSent = true;
                            game.KillAfterUtc = now + killGrace;
                            game.ChatMessages.Add(new DraftsGame.ChatMessage(
                                now,
                                0,
                                "System",
                                "Game timed out due to inactivity and will soon close."));
                        }
                    }
                    else if (idle >= warnAt && !game.IdleWarningSent)
                    {
                        warn = true;
                        game.IdleWarningSent = true;

                        game.ChatMessages.Add(new DraftsGame.ChatMessage(
                            now,
                            0,
                            "System",
                            $"Warning: this game will time out after {Math.Max(1, (int)maxIdle.TotalMinutes)} minutes of inactivity."));
                    }
                }

                if (warn)
                {
                    warningsSent++;
                    OnGameUpdated(gameId);
                }

                if (killMsg)
                {
                    _logger.LogInformation("ProcessIdleTimeouts: final timeout message injected for {GameId}; will remove after {GraceSeconds}s", gameId, killGrace.TotalSeconds);
                    OnGameUpdated(gameId);
                }

                if (remove)
                {
                    if (_games.TryRemove(gameId, out _))
                    {
                        removed++;
                        _logger.LogInformation("ProcessIdleTimeouts: removed {GameId} idle>={MaxIdle}", gameId, maxIdle);
                        OnGameUpdated(gameId);
                    }
                }
            }

            return (removed, warningsSent);
        }
    }

    public class DraftsGame
    {
        public string Id { get; }

        public DateTime CreatedUtc { get; } = DateTime.UtcNow;

        public DateTime StartTimeUtc { get; private set; } = DateTime.UtcNow;

        public DateTime LastTimeUtc { get; private set; } = DateTime.UtcNow;

        public bool IdleWarningSent { get; set; }

        public bool IdleKillMessageSent { get; set; }

        public DateTime? KillAfterUtc { get; set; }

        public DraftsService.GameState State { get; set; } = DraftsService.GameState.New;

        public bool AdminMode { get; set; } = false;

        public int? WinnerPlayer { get; set; }

        public bool GameOverMessageSent { get; set; }

        public int Player1PieceCount { get; private set; }

        public int Player2PieceCount { get; private set; }

        // Board representation:
        // 0 empty
        // 1 player1 piece, 3 player1 king
        // 2 player2 piece, 4 player2 king
        public int[,] Board { get; } = new int[8, 8];

        public int CurrentTurn { get; set; } = 1;

        public int CreatedByUserId { get; set; }
        public int? Player1UserId { get; set; }
        public int? Player2UserId { get; set; }

        public bool Player1Connected { get; set; } = false;
        public bool Player2Connected { get; set; } = false;

        public bool HadSecondPlayerConnected { get; set; } = false;

        public sealed record ChatMessage(DateTime Utc, int SenderUserId, string SenderName, string Text);

        public List<ChatMessage> ChatMessages { get; } = new();

        public sealed record BoardPos(int R, int C);

        public int? LastMoveFromR { get; set; }

        public int? LastMoveFromC { get; set; }

        public int? LastMoveToR { get; set; }

        public int? LastMoveToC { get; set; }

        public List<BoardPos> LastMoveCapturedSquares { get; } = new();

        public DraftsGame(string id)
        {
            Id = id;
            InitializeBoard();
            StartTimeUtc = DateTime.UtcNow;
            LastTimeUtc = StartTimeUtc;
        }

        public void Touch()
        {
            LastTimeUtc = DateTime.UtcNow;
            IdleWarningSent = false;
            IdleKillMessageSent = false;
            KillAfterUtc = null;
        }

        private void InitializeBoard()
        {
            // Standard checkers initial placement on dark squares.
            // We'll place player2 at top (rows 0-2) with pieces value 2, and player1 at bottom (rows 5-7) value 1.
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++) Board[r, c] = 0;
            }

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if ((r + c) % 2 == 1) Board[r, c] = 2;
                }
            }
            for (int r = 5; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if ((r + c) % 2 == 1) Board[r, c] = 1;
                }
            }

            CurrentTurn = 1;
            Player1Connected = false;
            Player2Connected = false;
            Player1UserId = null;
            Player2UserId = null;

            State = DraftsService.GameState.New;
            AdminMode = false;

            WinnerPlayer = null;
            GameOverMessageSent = false;

            LastMoveFromR = null;
            LastMoveFromC = null;
            LastMoveToR = null;
            LastMoveToC = null;
            LastMoveCapturedSquares.Clear();

            ChatMessages.Clear();

            RecountPieces();
        }

        public void RecountPieces()
        {
            var p1 = 0;
            var p2 = 0;

            for (var r = 0; r < 8; r++)
            {
                for (var c = 0; c < 8; c++)
                {
                    var cell = Board[r, c];
                    if (cell == 1 || cell == 3) p1++;
                    else if (cell == 2 || cell == 4) p2++;
                }
            }

            Player1PieceCount = p1;
            Player2PieceCount = p2;
        }
    }
}