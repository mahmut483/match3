using UnityEngine;

namespace Match3.Gameplay.Board
{
    // Hücre → dünya konumu. Cannon board'u kaydırdığında offset'i ekler; taşlar
    // BoardPresentation'ın çocuğu olduğu için bu her zaman görsel hücre merkezidir.
    public sealed class BoardGeometry
    {
        private readonly Transform boardPresentation;
        private readonly float spacingX;
        private readonly float spacingY;

        public float CellSize { get; }
        public Vector3 PresentationHome { get; }

        public BoardGeometry(float cellSize, Transform boardPresentation)
        {
            CellSize = cellSize;
            this.boardPresentation = boardPresentation;
            PresentationHome = boardPresentation != null ? boardPresentation.position : Vector3.zero;
            spacingX = (BoardDefinition.VisibleWidth - 1) / 2f;
            spacingY = (BoardDefinition.TotalHeight / 2) - 2.5f;   // eski hesap: int bölme (15/2 = 7)
        }

        public Vector2 CellToWorld(Vector2Int cell)
        {
            Vector2 world = new((cell.x - spacingX) * CellSize, (cell.y - spacingY) * CellSize);

            if (boardPresentation != null)
            {
                Vector3 offset = boardPresentation.position - PresentationHome;
                world += new Vector2(offset.x, offset.y);
            }

            return world;
        }
    }
}
