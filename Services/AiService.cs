using Microsoft.Extensions.Logging;

namespace Draughts.Services;

public class AiService
{
    private readonly DraughtsService _draughtsService;
    private readonly ILogger<AiService> _logger;

    public AiService(DraughtsService draughtsService, ILogger<AiService> logger)
    {
        _draughtsService = draughtsService;
        _logger = logger;
    }

    public (bool success, string? message, int? fromR, int? fromC, int? toR, int? toC) MakeRandomMove(string gameId, int aiPlayer)
    {
        var game = _draughtsService.GetGame(gameId);
        if (game == null) return (false, "Game not found", null, null, null, null);

        if (game.State != DraughtsService.GameState.Connected && game.State != DraughtsService.GameState.Playing)
        {
            return (false, "Game is not in a playable state", null, null, null, null);
        }

        if (game.CurrentTurn != aiPlayer)
        {
            return (false, "Not AI's turn", null, null, null, null);
        }

        // First, check if there are any capture moves available (must take captures in checkers)
        var captureMoves = _draughtsService.ListJumpOptions(gameId, aiPlayer);
        if (captureMoves.Count > 0)
        {
            // Randomly select a capture move
            var randomCapture = captureMoves[new Random().Next(captureMoves.Count)];
            var (success, message) = _draughtsService.MakeMove(gameId, aiPlayer, randomCapture.FromR, randomCapture.FromC, randomCapture.ToR, randomCapture.ToC);
            
            if (success)
            {
                _logger.LogInformation("AI made random capture move: {FromR},{FromC} -> {ToR},{ToC}", 
                    randomCapture.FromR, randomCapture.FromC, randomCapture.ToR, randomCapture.ToC);
                return (true, null, randomCapture.FromR, randomCapture.FromC, randomCapture.ToR, randomCapture.ToC);
            }
            else
            {
                return (false, message ?? "Failed to make capture move", null, null, null, null);
            }
        }

        // If no captures available, look for regular moves
        var regularMoves = GetRegularMoves(game, aiPlayer);
        if (regularMoves.Count == 0)
        {
            return (false, "No moves available", null, null, null, null);
        }

        // Randomly select a regular move
        var randomMove = regularMoves[new Random().Next(regularMoves.Count)];
        var (moveSuccess, moveMessage) = _draughtsService.MakeMove(gameId, aiPlayer, randomMove.fromR, randomMove.fromC, randomMove.toR, randomMove.toC);
        
        if (moveSuccess)
        {
            _logger.LogInformation("AI made random regular move: {FromR},{FromC} -> {ToR},{ToC}", 
                randomMove.fromR, randomMove.fromC, randomMove.toR, randomMove.toC);
            return (true, null, randomMove.fromR, randomMove.fromC, randomMove.toR, randomMove.toC);
        }
        else
        {
            return (false, moveMessage ?? "Failed to make regular move", null, null, null, null);
        }
    }

    private List<(int fromR, int fromC, int toR, int toC)> GetRegularMoves(DraughtsGame game, int player)
    {
        var moves = new List<(int, int, int, int)>();

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = game.Board[r, c];
                if (piece == 0) continue;
                if (!BelongsToPlayer(piece, player)) continue;

                // Get all possible regular moves for this piece
                var pieceMoves = GetRegularMovesForPiece(game, piece, r, c);
                moves.AddRange(pieceMoves);
            }
        }

        return moves;
    }

    private List<(int fromR, int fromC, int toR, int toC)> GetRegularMovesForPiece(DraughtsGame game, int piece, int fr, int fc)
    {
        var moves = new List<(int, int, int, int)>();
        var player = BelongsToPlayer(piece, 1) ? 1 : 2;
        var forwardDr = player == 1 ? -1 : 1;
        var drs = IsKing(piece) ? new[] { -1, 1 } : new[] { forwardDr };

        foreach (var dr in drs)
        {
            foreach (var dc in new[] { -1, 1 })
            {
                var tr = fr + dr;
                var tc = fc + dc;

                if (IsInside(tr, tc) && game.Board[tr, tc] == 0)
                {
                    moves.Add((fr, fc, tr, tc));
                }
            }
        }

        return moves;
    }

    private static bool IsInside(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;

    private static bool BelongsToPlayer(int piece, int player)
    {
        if (player == 1) return piece == 1 || piece == 3;
        return piece == 2 || piece == 4;
    }

    private static bool IsKing(int piece) => piece == 3 || piece == 4;
}
