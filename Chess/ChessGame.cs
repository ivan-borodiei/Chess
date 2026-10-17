namespace Chess;

internal enum ChessSide
{
    White,
    Black
}

internal enum ChessPieceType
{
    None,
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

internal readonly record struct ChessPiece(ChessSide Side, ChessPieceType Type)
{
    public bool IsEmpty => Type == ChessPieceType.None;

    public static ChessPiece Empty => new(ChessSide.White, ChessPieceType.None);
}

internal readonly record struct ChessMove(int FromFile, int FromRank, int ToFile, int ToRank, ChessPieceType Promotion = ChessPieceType.Queen);

internal enum ChessGameStatus
{
    InProgress,
    Check,
    Checkmate,
    Stalemate,
    Draw
}

internal sealed class ChessGame
{
    public ChessPiece[,] Board { get; } = new ChessPiece[8, 8];
    public ChessSide SideToMove { get; private set; }
    public ChessGameStatus Status { get; private set; }

    private bool whiteKingSideCastle;
    private bool whiteQueenSideCastle;
    private bool blackKingSideCastle;
    private bool blackQueenSideCastle;
    private int enPassantFile = -1;
    private int enPassantRank = -1;
    private int halfmoveClock;
    private readonly Dictionary<string, int> positionCounts = new();

    internal readonly record struct UndoState(
        ChessMove Move,
        ChessPiece MovingPiece,
        ChessPiece CapturedPiece,
        int CapturedFile,
        int CapturedRank,
        bool WhiteKingSideCastle,
        bool WhiteQueenSideCastle,
        bool BlackKingSideCastle,
        bool BlackQueenSideCastle,
        int EnPassantFile,
        int EnPassantRank,
        int HalfmoveClock,
        ChessSide SideToMove,
        ChessGameStatus Status);

    public ChessGame()
    {
        Reset();
    }

    public void Reset()
    {
        Array.Clear(Board);
        ChessPieceType[] backRank =
        [
            ChessPieceType.Rook, ChessPieceType.Knight, ChessPieceType.Bishop, ChessPieceType.Queen,
            ChessPieceType.King, ChessPieceType.Bishop, ChessPieceType.Knight, ChessPieceType.Rook
        ];

        for (int file = 0; file < 8; file++)
        {
            Board[file, 0] = new ChessPiece(ChessSide.Black, backRank[file]);
            Board[file, 1] = new ChessPiece(ChessSide.Black, ChessPieceType.Pawn);
            Board[file, 6] = new ChessPiece(ChessSide.White, ChessPieceType.Pawn);
            Board[file, 7] = new ChessPiece(ChessSide.White, backRank[file]);
        }

        SideToMove = ChessSide.White;
        Status = ChessGameStatus.InProgress;
        whiteKingSideCastle = whiteQueenSideCastle = true;
        blackKingSideCastle = blackQueenSideCastle = true;
        enPassantFile = enPassantRank = -1;
        halfmoveClock = 0;
        positionCounts.Clear();
        RecordPosition();
    }

    public IReadOnlyList<ChessMove> GetLegalMoves()
    {
        List<ChessMove> legalMoves = [];
        foreach (ChessMove move in GetPseudoLegalMoves())
        {
            UndoState undo = MakeMove(move);
            bool isLegal = !IsInCheck(undo.SideToMove);
            UndoMove(undo);
            if (isLegal)
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    public bool TryPlayMove(ChessMove move)
    {
        if (Status is ChessGameStatus.Checkmate or ChessGameStatus.Stalemate or ChessGameStatus.Draw)
        {
            return false;
        }

        if (!GetLegalMoves().Contains(move))
        {
            return false;
        }

        MakeMove(move);
        RecordPosition();
        UpdateStatus();
        return true;
    }

    internal UndoState MakeMove(ChessMove move)
    {
        ChessPiece movingPiece = Board[move.FromFile, move.FromRank];
        int capturedFile = move.ToFile;
        int capturedRank = move.ToRank;
        if (movingPiece.Type == ChessPieceType.Pawn && move.ToFile == enPassantFile && move.ToRank == enPassantRank)
        {
            capturedFile = move.ToFile;
            capturedRank = move.FromRank;
        }

        ChessPiece capturedPiece = Board[capturedFile, capturedRank];
        UndoState undo = new(
            move, movingPiece, capturedPiece, capturedFile, capturedRank,
            whiteKingSideCastle, whiteQueenSideCastle, blackKingSideCastle, blackQueenSideCastle,
            enPassantFile, enPassantRank, halfmoveClock, SideToMove, Status);

        Board[move.FromFile, move.FromRank] = ChessPiece.Empty;
        Board[capturedFile, capturedRank] = ChessPiece.Empty;
        ChessPiece placedPiece = movingPiece;
        if (movingPiece.Type == ChessPieceType.Pawn && move.ToRank is 0 or 7)
        {
            placedPiece = new ChessPiece(movingPiece.Side, move.Promotion);
        }

        Board[move.ToFile, move.ToRank] = placedPiece;

        if (movingPiece.Type == ChessPieceType.King && Math.Abs(move.ToFile - move.FromFile) == 2)
        {
            int rookFrom = move.ToFile > move.FromFile ? 7 : 0;
            int rookTo = move.ToFile > move.FromFile ? move.ToFile - 1 : move.ToFile + 1;
            Board[rookTo, move.FromRank] = Board[rookFrom, move.FromRank];
            Board[rookFrom, move.FromRank] = ChessPiece.Empty;
        }

        UpdateCastlingRights(movingPiece, move, capturedPiece, capturedFile, capturedRank);
        enPassantFile = enPassantRank = -1;
        if (movingPiece.Type == ChessPieceType.Pawn && Math.Abs(move.ToRank - move.FromRank) == 2)
        {
            enPassantFile = move.FromFile;
            enPassantRank = (move.FromRank + move.ToRank) / 2;
        }

        halfmoveClock = movingPiece.Type == ChessPieceType.Pawn || !capturedPiece.IsEmpty ? 0 : halfmoveClock + 1;
        SideToMove = Opposite(SideToMove);
        return undo;
    }

    internal void UndoMove(UndoState undo)
    {
        ChessMove move = undo.Move;
        Board[move.FromFile, move.FromRank] = undo.MovingPiece;
        Board[move.ToFile, move.ToRank] = ChessPiece.Empty;
        Board[undo.CapturedFile, undo.CapturedRank] = undo.CapturedPiece;

        if (undo.MovingPiece.Type == ChessPieceType.King && Math.Abs(move.ToFile - move.FromFile) == 2)
        {
            int rookFrom = move.ToFile > move.FromFile ? 7 : 0;
            int rookTo = move.ToFile > move.FromFile ? move.ToFile - 1 : move.ToFile + 1;
            Board[rookFrom, move.FromRank] = Board[rookTo, move.FromRank];
            Board[rookTo, move.FromRank] = ChessPiece.Empty;
        }

        whiteKingSideCastle = undo.WhiteKingSideCastle;
        whiteQueenSideCastle = undo.WhiteQueenSideCastle;
        blackKingSideCastle = undo.BlackKingSideCastle;
        blackQueenSideCastle = undo.BlackQueenSideCastle;
        enPassantFile = undo.EnPassantFile;
        enPassantRank = undo.EnPassantRank;
        halfmoveClock = undo.HalfmoveClock;
        SideToMove = undo.SideToMove;
        Status = undo.Status;
    }

    internal void UndoSearchMove(UndoState undo) => UndoMove(undo);

    public bool IsInCheck(ChessSide side)
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                if (Board[file, rank].Side == side && Board[file, rank].Type == ChessPieceType.King)
                {
                    return IsSquareAttacked(file, rank, Opposite(side));
                }
            }
        }

        return true;
    }

    private IEnumerable<ChessMove> GetPseudoLegalMoves()
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                ChessPiece piece = Board[file, rank];
                if (!piece.IsEmpty && piece.Side == SideToMove)
                {
                    foreach (ChessMove move in GetPieceMoves(file, rank, piece))
                    {
                        yield return move;
                    }
                }
            }
        }
    }

    private IEnumerable<ChessMove> GetPieceMoves(int file, int rank, ChessPiece piece)
    {
        switch (piece.Type)
        {
            case ChessPieceType.Pawn:
                foreach (ChessMove move in GetPawnMoves(file, rank, piece.Side)) yield return move;
                break;
            case ChessPieceType.Knight:
                foreach (ChessMove move in GetJumpMoves(file, rank, piece.Side,
                    [(-1, -2), (1, -2), (-2, -1), (2, -1), (-2, 1), (2, 1), (-1, 2), (1, 2)])) yield return move;
                break;
            case ChessPieceType.Bishop:
                foreach (ChessMove move in GetSlidingMoves(file, rank, piece.Side, [(-1, -1), (1, -1), (-1, 1), (1, 1)])) yield return move;
                break;
            case ChessPieceType.Rook:
                foreach (ChessMove move in GetSlidingMoves(file, rank, piece.Side, [(-1, 0), (1, 0), (0, -1), (0, 1)])) yield return move;
                break;
            case ChessPieceType.Queen:
                foreach (ChessMove move in GetSlidingMoves(file, rank, piece.Side,
                    [(-1, -1), (1, -1), (-1, 1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1)])) yield return move;
                break;
            case ChessPieceType.King:
                foreach (ChessMove move in GetJumpMoves(file, rank, piece.Side,
                    [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)])) yield return move;
                foreach (ChessMove move in GetCastlingMoves(file, rank, piece.Side)) yield return move;
                break;
        }
    }

    private IEnumerable<ChessMove> GetPawnMoves(int file, int rank, ChessSide side)
    {
        int direction = side == ChessSide.White ? -1 : 1;
        int startRank = side == ChessSide.White ? 6 : 1;
        int nextRank = rank + direction;
        if (IsInside(file, nextRank) && Board[file, nextRank].IsEmpty)
        {
            foreach (ChessMove move in CreatePawnMoves(file, rank, file, nextRank)) yield return move;
            int doubleRank = rank + direction * 2;
            if (rank == startRank && Board[file, doubleRank].IsEmpty)
            {
                yield return new ChessMove(file, rank, file, doubleRank);
            }
        }

        for (int offset = -1; offset <= 1; offset += 2)
        {
            int targetFile = file + offset;
            if (!IsInside(targetFile, nextRank)) continue;
            ChessPiece target = Board[targetFile, nextRank];
            if ((!target.IsEmpty && target.Side != side && target.Type != ChessPieceType.King) ||
                (targetFile == enPassantFile && nextRank == enPassantRank))
            {
                foreach (ChessMove move in CreatePawnMoves(file, rank, targetFile, nextRank)) yield return move;
            }
        }
    }

    private static IEnumerable<ChessMove> CreatePawnMoves(int fromFile, int fromRank, int toFile, int toRank)
    {
        if (toRank is 0 or 7)
        {
            yield return new ChessMove(fromFile, fromRank, toFile, toRank, ChessPieceType.Queen);
            yield return new ChessMove(fromFile, fromRank, toFile, toRank, ChessPieceType.Rook);
            yield return new ChessMove(fromFile, fromRank, toFile, toRank, ChessPieceType.Bishop);
            yield return new ChessMove(fromFile, fromRank, toFile, toRank, ChessPieceType.Knight);
        }
        else
        {
            yield return new ChessMove(fromFile, fromRank, toFile, toRank);
        }
    }

    private IEnumerable<ChessMove> GetJumpMoves(int file, int rank, ChessSide side, (int File, int Rank)[] offsets)
    {
        foreach ((int fileOffset, int rankOffset) in offsets)
        {
            int targetFile = file + fileOffset;
            int targetRank = rank + rankOffset;
            if (IsInside(targetFile, targetRank) && (Board[targetFile, targetRank].IsEmpty ||
                Board[targetFile, targetRank].Side != side && Board[targetFile, targetRank].Type != ChessPieceType.King))
            {
                yield return new ChessMove(file, rank, targetFile, targetRank);
            }
        }
    }

    private IEnumerable<ChessMove> GetSlidingMoves(int file, int rank, ChessSide side, (int File, int Rank)[] directions)
    {
        foreach ((int fileOffset, int rankOffset) in directions)
        {
            int targetFile = file + fileOffset;
            int targetRank = rank + rankOffset;
            while (IsInside(targetFile, targetRank))
            {
                ChessPiece target = Board[targetFile, targetRank];
                if (target.IsEmpty)
                {
                    yield return new ChessMove(file, rank, targetFile, targetRank);
                }
                else
                {
                    if (target.Side != side && target.Type != ChessPieceType.King)
                    {
                        yield return new ChessMove(file, rank, targetFile, targetRank);
                    }
                    break;
                }

                targetFile += fileOffset;
                targetRank += rankOffset;
            }
        }
    }

    private IEnumerable<ChessMove> GetCastlingMoves(int file, int rank, ChessSide side)
    {
        int homeRank = side == ChessSide.White ? 7 : 0;
        bool kingSide = side == ChessSide.White ? whiteKingSideCastle : blackKingSideCastle;
        bool queenSide = side == ChessSide.White ? whiteQueenSideCastle : blackQueenSideCastle;
        if (file != 4 || rank != homeRank || IsInCheck(side)) yield break;

        if (kingSide && Board[5, homeRank].IsEmpty && Board[6, homeRank].IsEmpty &&
            Board[7, homeRank] == new ChessPiece(side, ChessPieceType.Rook) &&
            !IsSquareAttacked(5, homeRank, Opposite(side)) && !IsSquareAttacked(6, homeRank, Opposite(side)))
        {
            yield return new ChessMove(4, homeRank, 6, homeRank);
        }

        if (queenSide && Board[1, homeRank].IsEmpty && Board[2, homeRank].IsEmpty && Board[3, homeRank].IsEmpty &&
            Board[0, homeRank] == new ChessPiece(side, ChessPieceType.Rook) &&
            !IsSquareAttacked(3, homeRank, Opposite(side)) && !IsSquareAttacked(2, homeRank, Opposite(side)))
        {
            yield return new ChessMove(4, homeRank, 2, homeRank);
        }
    }

    private bool IsSquareAttacked(int file, int rank, ChessSide bySide)
    {
        int pawnRank = rank + (bySide == ChessSide.White ? 1 : -1);
        foreach (int pawnFile in new[] { file - 1, file + 1 })
        {
            if (IsInside(pawnFile, pawnRank) && Board[pawnFile, pawnRank] == new ChessPiece(bySide, ChessPieceType.Pawn)) return true;
        }

        foreach ((int fileOffset, int rankOffset) in new (int, int)[]
        {
            (-1, -2), (1, -2), (-2, -1), (2, -1), (-2, 1), (2, 1), (-1, 2), (1, 2)
        })
        {
            int sourceFile = file + fileOffset;
            int sourceRank = rank + rankOffset;
            if (IsInside(sourceFile, sourceRank) && Board[sourceFile, sourceRank] == new ChessPiece(bySide, ChessPieceType.Knight)) return true;
        }

        foreach ((int fileOffset, int rankOffset) in new (int, int)[]
        {
            (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)
        })
        {
            int sourceFile = file + fileOffset;
            int sourceRank = rank + rankOffset;
            if (IsInside(sourceFile, sourceRank) && Board[sourceFile, sourceRank] == new ChessPiece(bySide, ChessPieceType.King)) return true;
        }

        foreach ((int fileOffset, int rankOffset) in new (int, int)[]
        {
            (-1, -1), (1, -1), (-1, 1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1)
        })
        {
            int sourceFile = file + fileOffset;
            int sourceRank = rank + rankOffset;
            while (IsInside(sourceFile, sourceRank))
            {
                ChessPiece piece = Board[sourceFile, sourceRank];
                if (!piece.IsEmpty)
                {
                    if (piece.Side == bySide && (piece.Type == ChessPieceType.Queen ||
                        (fileOffset == 0 || rankOffset == 0) && piece.Type == ChessPieceType.Rook ||
                        fileOffset != 0 && rankOffset != 0 && piece.Type == ChessPieceType.Bishop)) return true;
                    break;
                }

                sourceFile += fileOffset;
                sourceRank += rankOffset;
            }
        }

        return false;
    }

    private void UpdateCastlingRights(ChessPiece movingPiece, ChessMove move, ChessPiece capturedPiece, int capturedFile, int capturedRank)
    {
        if (movingPiece.Type == ChessPieceType.King)
        {
            if (movingPiece.Side == ChessSide.White) whiteKingSideCastle = whiteQueenSideCastle = false;
            else blackKingSideCastle = blackQueenSideCastle = false;
        }

        if (movingPiece.Type == ChessPieceType.Rook) RemoveCastlingRight(movingPiece.Side, move.FromFile, move.FromRank);
        if (capturedPiece.Type == ChessPieceType.Rook) RemoveCastlingRight(capturedPiece.Side, capturedFile, capturedRank);
    }

    private void RemoveCastlingRight(ChessSide side, int file, int rank)
    {
        if (rank == (side == ChessSide.White ? 7 : 0))
        {
            if (file == 0)
            {
                if (side == ChessSide.White) whiteQueenSideCastle = false;
                else blackQueenSideCastle = false;
            }
            else if (file == 7)
            {
                if (side == ChessSide.White) whiteKingSideCastle = false;
                else blackKingSideCastle = false;
            }
        }
    }

    private void UpdateStatus()
    {
        IReadOnlyList<ChessMove> legalMoves = GetLegalMoves();
        bool inCheck = IsInCheck(SideToMove);
        if (legalMoves.Count == 0)
        {
            Status = inCheck ? ChessGameStatus.Checkmate : ChessGameStatus.Stalemate;
        }
        else if (halfmoveClock >= 100 || positionCounts.GetValueOrDefault(GetPositionKey()) >= 3 || IsInsufficientMaterial())
        {
            Status = ChessGameStatus.Draw;
        }
        else
        {
            Status = inCheck ? ChessGameStatus.Check : ChessGameStatus.InProgress;
        }
    }

    private bool IsInsufficientMaterial()
    {
        List<(ChessPieceType Type, int File, int Rank)> material = [];
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                ChessPiece piece = Board[file, rank];
                if (!piece.IsEmpty && piece.Type != ChessPieceType.King)
                {
                    if (piece.Type is ChessPieceType.Pawn or ChessPieceType.Rook or ChessPieceType.Queen) return false;
                    material.Add((piece.Type, file, rank));
                }
            }
        }

        return material.Count <= 1 || material.All(piece => piece.Type == ChessPieceType.Bishop) &&
            material.Select(piece => (piece.File + piece.Rank) % 2).Distinct().Count() <= 1;
    }

    private void RecordPosition()
    {
        string key = GetPositionKey();
        positionCounts[key] = positionCounts.GetValueOrDefault(key) + 1;
    }

    private string GetPositionKey()
    {
        char[] key = new char[71];
        int index = 0;
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                ChessPiece piece = Board[file, rank];
                key[index++] = piece.IsEmpty ? '.' : (char)('A' + (int)piece.Type + (piece.Side == ChessSide.Black ? 6 : 0));
            }
        }

        key[index++] = SideToMove == ChessSide.White ? 'w' : 'b';
        key[index++] = whiteKingSideCastle ? 'K' : '-';
        key[index++] = whiteQueenSideCastle ? 'Q' : '-';
        key[index++] = blackKingSideCastle ? 'k' : '-';
        key[index++] = blackQueenSideCastle ? 'q' : '-';
        key[index++] = (char)('0' + enPassantFile + 1);
        key[index] = (char)('0' + enPassantRank + 1);
        return new string(key);
    }

    private static bool IsInside(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

    internal static ChessSide Opposite(ChessSide side) => side == ChessSide.White ? ChessSide.Black : ChessSide.White;
}
