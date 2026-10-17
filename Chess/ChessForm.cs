namespace Chess
{
    public partial class ChessForm : Form
    {
        private static readonly Color LightSquare = Color.FromArgb(216, 187, 128);
        private static readonly Color DarkSquare = Color.FromArgb(157, 105, 69);
        private static readonly Color FrameColor = Color.FromArgb(43, 29, 21);
        private static readonly Color PanelColor = Color.FromArgb(58, 40, 29);
        private static readonly Color TextColor = Color.FromArgb(244, 229, 196);
        private static readonly Color SelectedSquare = Color.FromArgb(230, 197, 91);
        private static readonly Color LegalSquare = Color.FromArgb(178, 153, 86);
        private static readonly Color LastMoveSquare = Color.FromArgb(202, 169, 82);
        private static readonly Color CheckSquare = Color.FromArgb(193, 83, 64);
        private readonly ChessGame game = new();
        private readonly ChessAi ai = new();
        private readonly Button[,] squareButtons = new Button[8, 8];
        private readonly Button newGameButton = new();
        private readonly Label statusLabel = new();
        private (int File, int Rank)? selectedSquare;
        private IReadOnlyList<ChessMove> highlightedMoves = [];
        private ChessMove? lastMove;
        private bool computerThinking;

        public ChessForm()
        {
            InitializeComponent();
            BuildInterface();
            RefreshBoard();
        }

        private void BuildInterface()
        {
            Text = "Chess";
            MinimumSize = new Size(700, 560);
            ClientSize = new Size(900, 720);
            BackColor = FrameColor;

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PanelColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            Controls.Add(layout);

            TableLayoutPanel board = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 9,
                RowCount = 9,
                BackColor = FrameColor
            };
            for (int index = 0; index < 8; index++)
            {
                board.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, index == 0 ? 4.5f : 11.9375f));
                board.RowStyles.Add(new RowStyle(SizeType.Percent, 11.9375f));
            }
            board.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11.9375f));
            board.RowStyles.Add(new RowStyle(SizeType.Percent, 4.5f));

            for (int rank = 0; rank < 8; rank++)
            {
                Label rankLabel = new()
                {
                    Text = (8 - rank).ToString(),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = TextColor,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Margin = Padding.Empty
                };
                board.Controls.Add(rankLabel, 0, rank);

                for (int file = 0; file < 8; file++)
                {
                    int currentFile = file;
                    int currentRank = rank;
                    Button square = new()
                    {
                        Dock = DockStyle.Fill,
                        Margin = Padding.Empty,
                        FlatStyle = FlatStyle.Flat,
                        UseVisualStyleBackColor = false,
                        Font = new Font("Segoe UI Symbol", 38, FontStyle.Regular),
                        TabStop = false
                    };
                    square.FlatAppearance.BorderSize = 0;
                    square.Click += (_, _) => OnSquareClicked(currentFile, currentRank);
                    squareButtons[file, rank] = square;
                    board.Controls.Add(square, file + 1, rank);
                }
            }

            for (int file = 0; file < 8; file++)
            {
                Label fileLabel = new()
                {
                    Text = ((char)('a' + file)).ToString(),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = TextColor,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Margin = Padding.Empty
                };
                board.Controls.Add(fileLabel, file + 1, 8);
            }

            Panel boardFrame = new()
            {
                Dock = DockStyle.Fill,
                BackColor = FrameColor,
                Padding = new Padding(6),
                Margin = new Padding(14)
            };
            boardFrame.Controls.Add(board);
            layout.Controls.Add(boardFrame, 0, 0);

            FlowLayoutPanel sidebar = new()
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 24, 12, 12),
                BackColor = PanelColor
            };
            Label title = new()
            {
                Text = "Chess",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                AutoSize = true,
                ForeColor = TextColor,
                Margin = new Padding(0, 0, 0, 18)
            };
            statusLabel.AutoSize = false;
            statusLabel.Width = 170;
            statusLabel.Height = 90;
            statusLabel.Font = new Font("Segoe UI", 12, FontStyle.Regular);
            statusLabel.ForeColor = TextColor;
            statusLabel.Margin = new Padding(0, 0, 0, 20);
            newGameButton.Text = "New game";
            newGameButton.AutoSize = true;
            newGameButton.Padding = new Padding(8, 4, 8, 4);
            newGameButton.FlatStyle = FlatStyle.Flat;
            newGameButton.FlatAppearance.BorderSize = 0;
            newGameButton.BackColor = LightSquare;
            newGameButton.ForeColor = FrameColor;
            newGameButton.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            newGameButton.Click += (_, _) => StartNewGame();

            sidebar.Controls.Add(title);
            sidebar.Controls.Add(statusLabel);
            sidebar.Controls.Add(newGameButton);
            layout.Controls.Add(sidebar, 1, 0);
        }

        private void OnSquareClicked(int file, int rank)
        {
            if (computerThinking || game.SideToMove != ChessSide.White ||
                game.Status is ChessGameStatus.Checkmate or ChessGameStatus.Stalemate or ChessGameStatus.Draw)
            {
                return;
            }

            ChessPiece piece = game.Board[file, rank];
            if (selectedSquare is null)
            {
                SelectSquare(file, rank);
                return;
            }

            (int fromFile, int fromRank) = selectedSquare.Value;
            if (fromFile == file && fromRank == rank)
            {
                selectedSquare = null;
                highlightedMoves = [];
                RefreshBoard();
                return;
            }

            if (!piece.IsEmpty && piece.Side == ChessSide.White)
            {
                ChessPiece selectedPiece = game.Board[fromFile, fromRank];
                if (selectedPiece.Type == ChessPieceType.King && piece.Type == ChessPieceType.Rook &&
                    fromFile == 4 && fromRank == rank && file is 0 or 7)
                {
                    int destinationFile = file == 7 ? 6 : 2;
                    ChessMove[] castleMoves = game.GetLegalMoves()
                        .Where(move => move.FromFile == fromFile && move.FromRank == fromRank &&
                            move.ToFile == destinationFile && move.ToRank == rank)
                        .ToArray();
                    if (castleMoves.Length == 1)
                    {
                        _ = PlayHumanMoveAsync(castleMoves[0]);
                        return;
                    }
                }

                SelectSquare(file, rank);
                return;
            }

            ChessMove[] matchingMoves = game.GetLegalMoves()
                .Where(move => move.FromFile == fromFile && move.FromRank == fromRank && move.ToFile == file && move.ToRank == rank)
                .ToArray();
            if (matchingMoves.Length == 0)
            {
                selectedSquare = null;
                highlightedMoves = [];
                RefreshBoard();
            }
            else if (matchingMoves.Length == 1)
            {
                _ = PlayHumanMoveAsync(matchingMoves[0]);
            }
            else
            {
                ShowPromotionMenu(file, rank, matchingMoves);
            }
        }

        private void SelectSquare(int file, int rank)
        {
            if (game.Board[file, rank].IsEmpty || game.Board[file, rank].Side != ChessSide.White)
            {
                selectedSquare = null;
                highlightedMoves = [];
            }
            else
            {
                selectedSquare = (file, rank);
                highlightedMoves = game.GetLegalMoves()
                    .Where(move => move.FromFile == file && move.FromRank == rank)
                    .ToArray();
            }

            RefreshBoard();
        }

        private void ShowPromotionMenu(int file, int rank, IReadOnlyList<ChessMove> moves)
        {
            ContextMenuStrip menu = new();
            foreach (ChessMove move in moves)
            {
                ToolStripMenuItem item = new(move.Promotion.ToString()) { Tag = move };
                item.Click += async (_, _) => await PlayHumanMoveAsync((ChessMove)item.Tag!);
                menu.Items.Add(item);
            }

            menu.Closed += (_, _) => menu.Dispose();
            menu.Show(squareButtons[file, rank], new Point(0, squareButtons[file, rank].Height));
        }

        private async Task PlayHumanMoveAsync(ChessMove move)
        {
            if (!game.TryPlayMove(move)) return;
            lastMove = move;
            selectedSquare = null;
            highlightedMoves = [];
            RefreshBoard();

            if (game.Status is ChessGameStatus.Checkmate or ChessGameStatus.Stalemate or ChessGameStatus.Draw)
            {
                return;
            }

            computerThinking = true;
            SetInputEnabled(false);
            statusLabel.Text = "Computer is thinking…";
            ChessMove computerMove = await Task.Run(() => ai.ChooseMove(game));
            if (game.TryPlayMove(computerMove))
            {
                lastMove = computerMove;
            }

            computerThinking = false;
            SetInputEnabled(true);
            RefreshBoard();
        }

        private void StartNewGame()
        {
            game.Reset();
            selectedSquare = null;
            highlightedMoves = [];
            lastMove = null;
            RefreshBoard();
        }

        private void SetInputEnabled(bool enabled)
        {
            foreach (Button square in squareButtons)
            {
                square.Enabled = enabled;
            }

            newGameButton.Enabled = enabled;
        }

        private void RefreshBoard()
        {
            bool whiteKingInCheck = game.IsInCheck(ChessSide.White);
            bool blackKingInCheck = game.IsInCheck(ChessSide.Black);
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    Button square = squareButtons[file, rank];
                    ChessPiece piece = game.Board[file, rank];
                    square.Text = GetPieceSymbol(piece);
                    square.ForeColor = piece.Side == ChessSide.White ? Color.White : Color.FromArgb(35, 35, 35);
                    square.AccessibleName = $"{SquareName(file, rank)} {piece.Side} {piece.Type}";
                    square.BackColor = (file + rank) % 2 == 0 ? LightSquare : DarkSquare;

                    if (lastMove is ChessMove previousMove &&
                        (previousMove.FromFile == file && previousMove.FromRank == rank ||
                         previousMove.ToFile == file && previousMove.ToRank == rank))
                    {
                        square.BackColor = LastMoveSquare;
                    }

                    if (highlightedMoves.Any(move => move.ToFile == file && move.ToRank == rank))
                    {
                        square.BackColor = LegalSquare;
                    }

                    if (selectedSquare is { } selection && selection.File == file && selection.Rank == rank)
                    {
                        square.BackColor = SelectedSquare;
                    }

                    if (piece.Type == ChessPieceType.King &&
                        (piece.Side == ChessSide.White && whiteKingInCheck || piece.Side == ChessSide.Black && blackKingInCheck))
                    {
                        square.BackColor = CheckSquare;
                    }
                }
            }

            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            statusLabel.Text = game.Status switch
            {
                ChessGameStatus.Check => $"{game.SideToMove} to move — check",
                ChessGameStatus.Checkmate => $"Checkmate. {ChessGame.Opposite(game.SideToMove)} wins.",
                ChessGameStatus.Stalemate => "Draw by stalemate.",
                ChessGameStatus.Draw => "Draw.",
                _ => $"{game.SideToMove} to move\nYou are White."
            };
        }

        private static string GetPieceSymbol(ChessPiece piece)
        {
            if (piece.IsEmpty) return string.Empty;

            return (piece.Side, piece.Type) switch
            {
                (ChessSide.White, ChessPieceType.King) => "♔",
                (ChessSide.White, ChessPieceType.Queen) => "♕",
                (ChessSide.White, ChessPieceType.Rook) => "♖",
                (ChessSide.White, ChessPieceType.Bishop) => "♗",
                (ChessSide.White, ChessPieceType.Knight) => "♘",
                (ChessSide.White, ChessPieceType.Pawn) => "♙",
                (ChessSide.Black, ChessPieceType.King) => "♚",
                (ChessSide.Black, ChessPieceType.Queen) => "♛",
                (ChessSide.Black, ChessPieceType.Rook) => "♜",
                (ChessSide.Black, ChessPieceType.Bishop) => "♝",
                (ChessSide.Black, ChessPieceType.Knight) => "♞",
                (ChessSide.Black, ChessPieceType.Pawn) => "♟",
                _ => string.Empty
            };
        }

        private static string SquareName(int file, int rank) => $"{(char)('a' + file)}{8 - rank}";
    }
}
