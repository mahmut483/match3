using UnityEngine;

namespace Match3.Gameplay.Board
{
    public static class BoardDefinition
    {
        public const int VisibleWidth = 6;
        public const int VisibleHeight = 8;
        public const int SpawnHeight = 7;
        public const int TotalHeight = VisibleHeight + SpawnHeight;

        public static bool IsPlayable(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < VisibleWidth &&
                   cell.y >= 0 && cell.y < VisibleHeight;
        }

        public static bool IsWithinStorage(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < VisibleWidth &&
                   cell.y >= 0 && cell.y < TotalHeight;
        }

        public static string GetLayoutValidationError(ArrayLayout layout)
        {
            int rowCount = layout?.rows?.Length ?? 0;

            if (rowCount != TotalHeight)
            {
                return $"Layout must contain exactly {TotalHeight} rows; found {rowCount}.";
            }

            for (int y = 0; y < TotalHeight; y++)
            {
                bool[] row = layout.rows[y].row;
                int columnCount = row?.Length ?? 0;

                if (columnCount != VisibleWidth)
                {
                    return $"Layout row {y} must contain exactly {VisibleWidth} columns; found {columnCount}.";
                }

                if (y < VisibleHeight) continue;

                for (int x = 0; x < VisibleWidth; x++)
                {
                    if (row[x])
                    {
                        return $"Spawn cell ({x}, {y}) must remain usable.";
                    }
                }
            }

            return null;
        }
    }
}
