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

    // Difficulty levels: 0=Easy, 1=Moderate, 2=Hard, 3=Random
    public (bool success, string? message, int? fromR, int? fromC, int? toR, int? toC) MakeRandomMove(string gameId, int aiPlayer, int difficultyLevel = 1)
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
                _logger.LogInformation("AI (difficulty {Difficulty}) made random capture move: {FromR},{FromC} -> {ToR},{ToC}", 
                    difficultyLevel, randomCapture.FromR, randomCapture.FromC, randomCapture.ToR, randomCapture.ToC);
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

        // Apply difficulty-based move selection
        var selectedMove = difficultyLevel switch
        {
            0 => SelectEasyMove(game, regularMoves, aiPlayer),      // Easy: avoid being captured
            3 => regularMoves[new Random().Next(regularMoves.Count)], // Random: completely random
            _ => regularMoves[new Random().Next(regularMoves.Count)]  // Moderate/Hard: random for now
        };

        var (moveSuccess, moveMessage) = _draughtsService.MakeMove(gameId, aiPlayer, selectedMove.fromR, selectedMove.fromC, selectedMove.toR, selectedMove.toC);
        
        if (moveSuccess)
        {
            _logger.LogInformation("AI (difficulty {Difficulty}) made regular move: {FromR},{FromC} -> {ToR},{ToC}", 
                difficultyLevel, selectedMove.fromR, selectedMove.fromC, selectedMove.toR, selectedMove.toC);
            return (true, null, selectedMove.fromR, selectedMove.fromC, selectedMove.toR, selectedMove.toC);
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

    // Easy mode: Try to avoid moves that would allow the opponent to capture next turn
    private (int fromR, int fromC, int toR, int toC) SelectEasyMove(
        DraughtsGame game, 
        List<(int fromR, int fromC, int toR, int toC)> allMoves, 
        int aiPlayer)
    {
        var opponentPlayer = aiPlayer == 1 ? 2 : 1;
        var safeMoves = new List<(int fromR, int fromC, int toR, int toC)>();

        foreach (var move in allMoves)
        {
            // Simulate the move to check if it would be vulnerable
            if (!WouldBeVulnerableAfterMove(game, move, aiPlayer, opponentPlayer))
            {
                safeMoves.Add(move);
            }
        }

        // If we have safe moves, pick one randomly; otherwise pick any move
        var movesToChooseFrom = safeMoves.Count > 0 ? safeMoves : allMoves;
        return movesToChooseFrom[new Random().Next(movesToChooseFrom.Count)];
    }

    // Check if a move would leave the piece vulnerable to capture
    private bool WouldBeVulnerableAfterMove(
        DraughtsGame game,
        (int fromR, int fromC, int toR, int toC) move,
        int aiPlayer,
        int opponentPlayer)
    {
        // Simulate the move by checking the destination position
        var piece = game.Board[move.fromR, move.fromC];
        var toR = move.toR;
        var toC = move.toC;

        // Check all opponent pieces to see if any could capture at this position
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var opponentPiece = game.Board[r, c];
                if (opponentPiece == 0) continue;
                if (!BelongsToPlayer(opponentPiece, opponentPlayer)) continue;

                // Check if this opponent piece could jump to capture our piece at (toR, toC)
                if (CanJumpTo(opponentPiece, r, c, toR, toC, opponentPlayer))
                {
                    return true; // This move would be vulnerable
                }
            }
        }

        return false; // Move appears safe
    }

    // Check if a piece at (fromR, fromC) can jump over (toR, toC)
    private bool CanJumpTo(int piece, int fromR, int fromC, int targetR, int targetC, int player)
    {
        var forwardDr = player == 1 ? -1 : 1;
        var drs = IsKing(piece) ? new[] { -1, 1 } : new[] { forwardDr };

        foreach (var dr in drs)
        {
            foreach (var dc in new[] { -1, 1 })
            {
                // Check if target is one diagonal away (potential victim position)
                var victimR = fromR + dr;
                var victimC = fromC + dc;

                if (victimR == targetR && victimC == targetC)
                {
                    // Check if there's a landing spot after the jump
                    var landR = fromR + (dr * 2);
                    var landC = fromC + (dc * 2);

                    if (IsInside(landR, landC))
                    {
                        return true; // Could potentially jump here
                    }
                }
            }
        }

        return false;
    }
}
