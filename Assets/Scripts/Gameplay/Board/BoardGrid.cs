using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Node[,] dizisinin tek sahibi. Hücre içeriğini ve taşın indekslerini
    // birlikte günceller; tahta kodu diziye doğrudan dokunmaz.
    public sealed class BoardGrid
    {
        private readonly Node[,] cells;

        // Layout'ta true olan hücre kapalıdır (taş almaz).
        public BoardGrid(ArrayLayout layout)
        {
            cells = new Node[BoardDefinition.VisibleWidth, BoardDefinition.TotalHeight];

            for (int y = 0; y < BoardDefinition.TotalHeight; y++)
            {
                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    cells[x, y] = new Node(!layout.rows[y].row[x]);
                }
            }
        }

        public Node this[int x, int y] => cells[x, y];

        public bool IsUsable(Vector2Int cell)
        {
            return BoardDefinition.IsWithinStorage(cell) && cells[cell.x, cell.y].IsUsable;
        }

        // Depo dışı, kapalı veya boş hücre için null.
        public Potion PotionAt(Vector2Int cell)
        {
            return IsUsable(cell) ? cells[cell.x, cell.y].Potion : null;
        }

        public void Place(Potion potion, Vector2Int cell)
        {
            cells[cell.x, cell.y].Potion = potion;
            potion.SetIndices(cell.x, cell.y);
        }

        public void Clear(Vector2Int cell)
        {
            cells[cell.x, cell.y].Potion = null;
        }

        // İki taşın hücrelerini ve indekslerini değiştirir; dünya konumu çağıranın işi.
        public void Swap(Potion a, Potion b)
        {
            Vector2Int cellA = new(a.XIndex, a.YIndex);
            Vector2Int cellB = new(b.XIndex, b.YIndex);

            Place(a, cellB);
            Place(b, cellA);
        }

        public bool AnyPotionMoving()
        {
            foreach (Potion potion in Potions)
            {
                if (potion.IsMoving) return true;
            }

            return false;
        }

        public IEnumerable<Potion> Potions
        {
            get
            {
                foreach (Node node in cells)
                {
                    if (node.Potion != null) yield return node.Potion;
                }
            }
        }
    }
}
