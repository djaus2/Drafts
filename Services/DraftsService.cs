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

        // Event raised when a game changes. Subscribers can call StateHasChanged.
        public event Action<string>? GameUpdated;

        private readonly ConcurrentDictionary<string, DraftsGame> _games = new();

        public DraftsService(ILogger<DraftsService> logger)
        {
            _logger = logger;
        }

        public sealed record GameListItem(
            string Id,
            DateTime CreatedUtc,
            int CreatedByUserId,
            int? Player1UserId,
            int? Player2UserId,
            bool Player1Connected,
            bool Player2Connected,
            bool HadSecondPlayerConnected);

        public List<GameListItem> ListGames()
        {
            // Snapshot view for UI.
            return _games.Values
                .Select(g => new GameListItem(
                    g.Id,
                    g.CreatedUtc,
                    g.CreatedByUserId,
                    g.Player1UserId,
                    g.Player2UserId,
                    g.Player1Connected,
                    g.Player2Connected,
                    g.HadSecondPlayerConnected))
                .ToList();
        }

        public string CreateGame(int userId, int creatorPlayerNumber = 1)
        {
            var id = Guid.NewGuid().ToString("n").Substring(0, 8);
            var game = new DraftsGame(id)
            {
                CreatedByUserId = userId
            };

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

            _games[id] = game;
            _logger.LogInformation("CreateGame: {GameId}", id);
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
                    }
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
                    }
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
            if (senderUserId <= 0) return false;

            text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(text)) return false;

            var game = GetGame(gameId);
            if (game is null) return false;

            lock (game)
            {
                game.ChatMessages.Add(new DraftsGame.ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, text));
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
                    MaybePromote(game, tr, tc);
                    game.CurrentTurn = 3 - player;
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
                    MaybePromote(game, tr, tc);
                    // NOTE: Not implementing multiple-jump forcing — simple single capture.
                    game.CurrentTurn = 3 - player;
                    OnGameUpdated(id);
                    _logger.LogInformation("MakeMove: {GameId} capture applied", id);
                    return (true, null);
                }

                return (false, "Illegal move");
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
    }

    public class DraftsGame
    {
        public string Id { get; }

        public DateTime CreatedUtc { get; } = DateTime.UtcNow;

        // Board representation:
        // 0 empty
        // 1 player1 piece, 3 player1 king
        // 2 player2 piece, 4 player2 king
        public int[,] Board { get; } = new int[8,8];

        public int CurrentTurn { get; set; } = 1;

        public int CreatedByUserId { get; set; }
        public int? Player1UserId { get; set; }
        public int? Player2UserId { get; set; }

        public bool Player1Connected { get; set; } = false;
        public bool Player2Connected { get; set; } = false;

        public bool HadSecondPlayerConnected { get; set; } = false;

        public sealed record ChatMessage(DateTime Utc, int SenderUserId, string SenderName, string Text);

        public List<ChatMessage> ChatMessages { get; } = new();

        public DraftsGame(string id)
        {
            Id = id;
            InitializeBoard();
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

            ChatMessages.Clear();
        }
    }
}