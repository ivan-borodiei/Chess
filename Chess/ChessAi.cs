namespace Chess;

internal sealed class ChessAi
{
    private const int MateScore = 100_000;
    private static readonly IReadOnlyDictionary<ChessPieceType, int> PieceValues = new Dictionary<ChessPieceType, int>
    {
        [ChessPieceType.Pawn] = 100,
        [ChessPieceType.Knight] = 320,
        [ChessPieceType.Bishop] = 330,
        [ChessPieceType.Rook] = 500,
        [ChessPieceType.Queen] = 900,
        [ChessPieceType.King] = 20_000
    };

    private readonly ChessSide computerSide;

    public ChessAi(ChessSide computerSide = ChessSide.Black)
    {
        this.computerSide = computerSide;
    }

    public ChessMove ChooseMove(ChessGame game, int depth = 3)
    {
        IReadOnlyList<ChessMove> legalMoves = OrderMoves(game, game.GetLegalMoves());
        if (legalMoves.Count == 0)
        {
            throw new InvalidOperationException("There are no legal moves to choose from.");
        }

        ChessMove bestMove = legalMoves[0];
        int bestScore = int.MinValue;
        int alpha = int.MinValue + 1;
        int beta = int.MaxValue;
        foreach (ChessMove move in legalMoves)
        {
            ChessGame.UndoState undo = game.MakeMove(move);
            int score = Search(game, depth - 1, alpha, beta, 1);
            game.UndoSearchMove(undo);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            alpha = Math.Max(alpha, bestScore);
        }

        return bestMove;
    }

    private int Search(ChessGame game, int depth, int alpha, int beta, int ply)
    {
        IReadOnlyList<ChessMove> legalMoves = game.GetLegalMoves();
        if (legalMoves.Count == 0)
        {
            return game.IsInCheck(game.SideToMove)
                ? game.SideToMove == computerSide ? -MateScore + ply : MateScore - ply
                : 0;
        }

        if (depth <= 0)
        {
            return Evaluate(game);
        }

        bool maximizing = game.SideToMove == computerSide;
        int bestScore = maximizing ? int.MinValue + 1 : int.MaxValue;
        foreach (ChessMove move in OrderMoves(game, legalMoves))
        {
            ChessGame.UndoState undo = game.MakeMove(move);
            int score = Search(game, depth - 1, alpha, beta, ply + 1);
            game.UndoSearchMove(undo);

            if (maximizing)
            {
                bestScore = Math.Max(bestScore, score);
                alpha = Math.Max(alpha, bestScore);
            }
            else
            {
                bestScore = Math.Min(bestScore, score);
                beta = Math.Min(beta, bestScore);
            }

            if (alpha >= beta)
            {
                break;
            }
        }

        return bestScore;
    }

    private int Evaluate(ChessGame game)
    {
        int score = 0;
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                ChessPiece piece = game.Board[file, rank];
                if (piece.IsEmpty) continue;

                int advancement = piece.Side == ChessSide.White ? 6 - rank : rank - 1;
                int centerDistance = Math.Abs(3 - file) + Math.Abs(3 - rank);
                int positional = piece.Type switch
                {
                    ChessPieceType.Pawn => advancement * 7 - centerDistance * 2,
                    ChessPieceType.Knight or ChessPieceType.Bishop => 12 - centerDistance * 5,
                    ChessPieceType.Queen => 6 - centerDistance * 2,
                    _ => 0
                };
                int value = PieceValues[piece.Type] + positional;
                score += piece.Side == computerSide ? value : -value;
            }
        }

        return score;
    }

    private static IReadOnlyList<ChessMove> OrderMoves(ChessGame game, IReadOnlyList<ChessMove> moves)
    {
        return moves.OrderByDescending(move =>
        {
            ChessPiece moving = game.Board[move.FromFile, move.FromRank];
            ChessPiece captured = game.Board[move.ToFile, move.ToRank];
            int score = captured.IsEmpty ? 0 : 10 * PieceValues[captured.Type] - PieceValues[moving.Type];
            if (moving.Type == ChessPieceType.Pawn && Math.Abs(move.ToFile - move.FromFile) == 1 && captured.IsEmpty)
            {
                score += 100;
            }

            if (move.Promotion != ChessPieceType.Queen || moving.Type == ChessPieceType.Pawn && move.ToRank is 0 or 7)
            {
                score += PieceValues[move.Promotion] - PieceValues[ChessPieceType.Pawn];
            }

            return score;
        }).ToArray();
    }
}
