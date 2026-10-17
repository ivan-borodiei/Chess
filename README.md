# Chess

A Windows Forms chess game built with .NET 10. Play as White against a built-in computer opponent as Black.

## Requirements

- Windows
- .NET 10 SDK

## Run

Open `Chess.slnx` in Visual Studio and press **F5**, or run from a terminal:

```powershell
dotnet run --project Chess/Chess.csproj
```

## How to play

- Click a white piece, then click one of its highlighted legal destinations.
- To castle, move the king two squares or select the king and click the rook. Castling is only available when all chess rules permit it.
- Choose a promotion piece from the menu when a pawn reaches the far rank.
- Use **New game** to restart.

The game includes check, checkmate, stalemate, castling, en passant, promotion, and draw detection. The computer uses a lightweight depth-limited search.
