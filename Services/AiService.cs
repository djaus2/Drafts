using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Draughts.Services;

public class AiService
{
    private readonly DraughtsService _draughtsService;
    private readonly ILogger<AiService> _logger;

    private readonly Random _rng = new();

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

        Move? selected;
        lock (game)
        {
            var legalMoves = GetLegalMoves(game, aiPlayer);
            if (legalMoves.Count == 0)
            {
                selected = null;
            }
            else
            {
                selected = difficultyLevel switch
                {
                    0 => SelectEasyMove(game, legalMoves, aiPlayer),
                    1 => SelectModerateMove(game, legalMoves, aiPlayer),
                    3 => legalMoves[_rng.Next(legalMoves.Count)],
                    _ => legalMoves[_rng.Next(legalMoves.Count)]
                };
            }
        }

        if (selected is null)
        {
            return (false, "No moves available", null, null, null, null);
        }

        var (moveSuccess, moveMessage) = _draughtsService.MakeMove(gameId, aiPlayer, selected.FromR, selected.FromC, selected.ToR, selected.ToC);
        
        if (moveSuccess)
        {
            _logger.LogInformation("AI (difficulty {Difficulty}) made regular move: {FromR},{FromC} -> {ToR},{ToC}", 
                difficultyLevel, selected.FromR, selected.FromC, selected.ToR, selected.ToC);
            return (true, null, selected.FromR, selected.FromC, selected.ToR, selected.ToC);
        }
        else
        {
            return (false, moveMessage ?? "Failed to make regular move", null, null, null, null);
        }
    }

    private sealed record Move(int FromR, int FromC, int ToR, int ToC, bool IsCapture, int? CaptureR, int? CaptureC);

    private sealed record SimState(int[,] Board, int CurrentTurn, int? ForcedJumpFromR, int? ForcedJumpFromC);

    private Move SelectEasyMove(DraughtsGame game, List<Move> legalMoves, int aiPlayer)
    {
        // Easy retains the old behavior but respects mandatory capture.
        if (legalMoves.Any(m => m.IsCapture))
        {
            var captures = legalMoves.Where(m => m.IsCapture).ToList();
            return captures[_rng.Next(captures.Count)];
        }

        var regularMoves = legalMoves.Select(m => (fromR: m.FromR, fromC: m.FromC, toR: m.ToR, toC: m.ToC)).ToList();
        var chosen = SelectEasyMove(game, regularMoves, aiPlayer);
        return new Move(chosen.fromR, chosen.fromC, chosen.toR, chosen.toC, false, null, null);
    }

    private Move SelectModerateMove(DraughtsGame game, List<Move> legalMoves, int aiPlayer)
    {
        var root = Snapshot(game);
        var bestMove = legalMoves[0];
        var bestScore = int.MinValue;

        foreach (var m1 in legalMoves)
        {
            if (!TryApplyMove(root, aiPlayer, m1, out var s1))
            {
                continue;
            }

            var score = ScoreThreePlies(root, s1, aiPlayer);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = m1;
            }
        }

        return bestMove;
    }

    private int ScoreThreePlies(SimState root, SimState afterAi, int aiPlayer)
    {
        var opponent = 3 - aiPlayer;

        // If the turn didn't change, we're in a forced multi-jump chain.
        // We'll still do 3 half-moves total, so opponent ply is skipped and we evaluate.
        if (afterAi.CurrentTurn != opponent)
        {
            return GameMe(root, afterAi, aiPlayer);
        }

        var opponentMoves = GetLegalMoves(afterAi, opponent);
        if (opponentMoves.Count == 0)
        {
            return 100;
        }

        long sum = 0;
        var count = 0;

        foreach (var m2 in opponentMoves)
        {
            if (!TryApplyMove(afterAi, opponent, m2, out var s2))
            {
                continue;
            }

            var leafScore = ScoreAfterSecondAiMove(root, s2, aiPlayer);
            sum += leafScore;
            count++;
        }

        if (count == 0)
        {
            return GameMe(root, afterAi, aiPlayer);
        }

        return (int)(sum / count);
    }

    private int ScoreAfterSecondAiMove(SimState root, SimState afterOpponent, int aiPlayer)
    {
        if (afterOpponent.CurrentTurn != aiPlayer)
        {
            return GameMe(root, afterOpponent, aiPlayer);
        }

        var aiMoves = GetLegalMoves(afterOpponent, aiPlayer);
        if (aiMoves.Count == 0)
        {
            return -100;
        }

        var best = int.MinValue;
        foreach (var m3 in aiMoves)
        {
            if (!TryApplyMove(afterOpponent, aiPlayer, m3, out var s3))
            {
                continue;
            }

            var score = GameMe(root, s3, aiPlayer);
            if (score > best) best = score;
        }

        return best == int.MinValue ? GameMe(root, afterOpponent, aiPlayer) : best;
    }

    private static SimState Snapshot(DraughtsGame game)
    {
        var b = new int[8, 8];
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                b[r, c] = game.Board[r, c];
            }
        }

        return new SimState(b, game.CurrentTurn, game.ForcedJumpFromR, game.ForcedJumpFromC);
    }

    private List<Move> GetLegalMoves(DraughtsGame game, int player)
    {
        var state = Snapshot(game);
        return GetLegalMoves(state, player);
    }

    private List<Move> GetLegalMoves(SimState state, int player)
    {
        // If a forced multi-jump is active and it's this player's turn, only captures from that piece.
        if (state.CurrentTurn == player && state.ForcedJumpFromR.HasValue && state.ForcedJumpFromC.HasValue)
        {
            var fr = state.ForcedJumpFromR.Value;
            var fc = state.ForcedJumpFromC.Value;
            return GetCaptureMovesForPiece(state.Board, player, fr, fc)
                .Select(m => new Move(fr, fc, m.ToR, m.ToC, true, m.CaptureR, m.CaptureC))
                .ToList();
        }

        // Mandatory capture rule.
        var captures = new List<Move>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = state.Board[r, c];
                if (piece == 0) continue;
                if (!BelongsToPlayer(piece, player)) continue;

                foreach (var m in GetCaptureMovesForPiece(state.Board, player, r, c))
                {
                    captures.Add(new Move(r, c, m.ToR, m.ToC, true, m.CaptureR, m.CaptureC));
                }
            }
        }

        if (captures.Count > 0)
        {
            return captures;
        }

        var moves = new List<Move>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = state.Board[r, c];
                if (piece == 0) continue;
                if (!BelongsToPlayer(piece, player)) continue;

                var forwardDr = player == 1 ? -1 : 1;
                var drs = IsKing(piece) ? new[] { -1, 1 } : new[] { forwardDr };
                foreach (var dr in drs)
                {
                    foreach (var dc in new[] { -1, 1 })
                    {
                        var tr = r + dr;
                        var tc = c + dc;
                        if (IsInside(tr, tc) && state.Board[tr, tc] == 0)
                        {
                            moves.Add(new Move(r, c, tr, tc, false, null, null));
                        }
                    }
                }
            }
        }

        return moves;
    }

    private static IReadOnlyList<(int CaptureR, int CaptureC, int ToR, int ToC)> GetCaptureMovesForPiece(int[,] board, int player, int fr, int fc)
    {
        if (!IsInside(fr, fc)) return Array.Empty<(int, int, int, int)>();

        var piece = board[fr, fc];
        if (piece == 0) return Array.Empty<(int, int, int, int)>();
        if (!BelongsToPlayer(piece, player)) return Array.Empty<(int, int, int, int)>();

        var forwardDr = player == 1 ? -1 : 1;
        var drs = IsKing(piece) ? new[] { -1, 1 } : new[] { forwardDr };
        var list = new List<(int, int, int, int)>();

        foreach (var dr in drs)
        {
            foreach (var dc in new[] { -1, 1 })
            {
                var midr = fr + dr;
                var midc = fc + dc;
                var tr = fr + 2 * dr;
                var tc = fc + 2 * dc;

                if (!IsInside(tr, tc) || board[tr, tc] != 0) continue;
                if (!IsInside(midr, midc)) continue;

                var mid = board[midr, midc];
                if (mid == 0) continue;
                if (BelongsToPlayer(mid, player)) continue;

                list.Add((midr, midc, tr, tc));
            }
        }

        return list;
    }

    private static bool TryApplyMove(SimState state, int player, Move move, out SimState next)
    {
        next = state;

        if (!IsInside(move.FromR, move.FromC) || !IsInside(move.ToR, move.ToC)) return false;

        var piece = state.Board[move.FromR, move.FromC];
        if (piece == 0) return false;
        if (!BelongsToPlayer(piece, player)) return false;
        if (state.Board[move.ToR, move.ToC] != 0) return false;

        var board = (int[,])state.Board.Clone();
        var fr = move.FromR;
        var fc = move.FromC;
        var tr = move.ToR;
        var tc = move.ToC;

        var dr = tr - fr;
        var dc = tc - fc;
        var absdr = Math.Abs(dr);
        var absdc = Math.Abs(dc);

        // Forced multi-jump enforcement.
        if (state.CurrentTurn == player && state.ForcedJumpFromR.HasValue && state.ForcedJumpFromC.HasValue)
        {
            if (state.ForcedJumpFromR.Value != fr || state.ForcedJumpFromC.Value != fc) return false;
            if (!move.IsCapture) return false;
        }

        // Regular move.
        if (!move.IsCapture)
        {
            if (absdr != 1 || absdc != 1) return false;
            if (!IsKing(piece))
            {
                var forwardDr = player == 1 ? -1 : 1;
                if (dr != forwardDr) return false;
            }

            board[tr, tc] = MaybePromotePiece(piece, player, tr);
            board[fr, fc] = 0;
            next = new SimState(board, 3 - player, null, null);
            return true;
        }

        // Capture.
        if (absdr != 2 || absdc != 2) return false;

        var midr = fr + dr / 2;
        var midc = fc + dc / 2;
        if (!IsInside(midr, midc)) return false;

        var mid = board[midr, midc];
        if (mid == 0) return false;
        if (BelongsToPlayer(mid, player)) return false;

        if (!IsKing(piece))
        {
            var forwardDr = player == 1 ? -1 : 1;
            if (dr != 2 * forwardDr) return false;
        }

        board[tr, tc] = MaybePromotePiece(piece, player, tr);
        board[fr, fc] = 0;
        board[midr, midc] = 0;

        // Follow-up captures => forced multi-jump.
        var followUp = GetCaptureMovesForPiece(board, player, tr, tc).Count;
        if (followUp > 0)
        {
            next = new SimState(board, player, tr, tc);
        }
        else
        {
            next = new SimState(board, 3 - player, null, null);
        }

        return true;
    }

    private static int MaybePromotePiece(int piece, int player, int row)
    {
        if (player == 1 && row == 0 && piece == 1) return 3;
        if (player == 2 && row == 7 && piece == 2) return 4;
        return piece;
    }

    private int GameMe(SimState root, SimState leaf, int aiPlayer)
    {
        var opponent = 3 - aiPlayer;

        var (rootAiPieces, rootAiKings, rootOppPieces, rootOppKings) = CountPieces(root.Board, aiPlayer);
        var (leafAiPieces, leafAiKings, leafOppPieces, leafOppKings) = CountPieces(leaf.Board, aiPlayer);

        // Base capture differential (+1 for each opponent piece removed, -1 for each AI piece removed)
        var oppPiecesTaken = Math.Max(0, rootOppPieces - leafOppPieces);
        var aiPiecesLost = Math.Max(0, rootAiPieces - leafAiPieces);
        var score = oppPiecesTaken - aiPiecesLost;

        // Kings
        var aiKingsCreated = Math.Max(0, leafAiKings - rootAiKings);
        var oppKingsTaken = Math.Max(0, rootOppKings - leafOppKings);
        var aiKingsLost = Math.Max(0, rootAiKings - leafAiKings);

        score += 2 * aiKingsCreated;
        score += 2 * oppKingsTaken;
        score -= 2 * aiKingsLost;

        // Losing / concede
        if (leafAiPieces == 0)
        {
            return -100;
        }

        if (leaf.CurrentTurn == aiPlayer)
        {
            var legal = GetLegalMoves(leaf, aiPlayer);
            if (legal.Count == 0) return -100;
        }

        // If opponent has no pieces, treat as a strong win.
        if (leafOppPieces == 0) score += 100;

        return score;
    }

    private static (int aiPieces, int aiKings, int oppPieces, int oppKings) CountPieces(int[,] board, int aiPlayer)
    {
        var opponent = 3 - aiPlayer;
        var aiPieces = 0;
        var aiKings = 0;
        var oppPieces = 0;
        var oppKings = 0;

        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var p = board[r, c];
                if (p == 0) continue;

                if (BelongsToPlayer(p, aiPlayer))
                {
                    aiPieces++;
                    if (IsKing(p)) aiKings++;
                }
                else if (BelongsToPlayer(p, opponent))
                {
                    oppPieces++;
                    if (IsKing(p)) oppKings++;
                }
            }
        }

        return (aiPieces, aiKings, oppPieces, oppKings);
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
