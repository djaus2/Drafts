using System;
using System.Collections.Concurrent;
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

        public string CreateGame()
        {
            var id = Guid.NewGuid().ToString("n").Substring(0, 8);
            var game = new DraftsGame(id);
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
        public int TryJoinGame(string id)
        {
            var game = GetGame(id);
            if (game == null)
            {
                _logger.LogWarning("TryJoinGame: {GameId} not found", id);
                return 0;
            }

            lock (game)
            {
                if (game.Player1Connected && game.Player2Connected)
                {
                    _logger.LogWarning("TryJoinGame: {GameId} already full (p1={P1} p2={P2})", id, game.Player1Connected, game.Player2Connected);
                    return 0;
                }
                if (!game.Player1Connected)
                {
                    game.Player1Connected = true;
                    _logger.LogInformation("TryJoinGame: {GameId} assigned Player1", id);
                    OnGameUpdated(id);
                    return 1;
                }
                if (!game.Player2Connected)
                {
                    game.Player2Connected = true;
                    _logger.LogInformation("TryJoinGame: {GameId} assigned Player2", id);
                    OnGameUpdated(id);
                    return 2;
                }
                _logger.LogWarning("TryJoinGame: {GameId} unexpected state", id);
                return 0;
            }
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
        // Board representation:
        // 0 empty
        // 1 player1 piece, 3 player1 king
        // 2 player2 piece, 4 player2 king
        public int[,] Board { get; } = new int[8,8];

        public int CurrentTurn { get; set; } = 1;

        public bool Player1Connected { get; set; } = false;
        public bool Player2Connected { get; set; } = false;

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
        }
    }
}