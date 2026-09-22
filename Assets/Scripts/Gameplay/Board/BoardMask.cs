using UnityEngine;

namespace Match3.Gameplay.Board
{
    // Tahtanın dışını ve kapalı hücreleri örten maskeyi level layout'undan üretir.
    // Taşlar "Visible Outside Mask" olduğu için maskenin opak olduğu yerde görünmezler.
    //
    // Elle yerleştirilen dikdörtgen çerçeveler yalnızca dikdörtgen tahtalarda doğruydu;
    // artı/L gibi şekillerde kapalı köşelerin üstündeki spawn taşları açıkta kalıyordu.
    public sealed class BoardMask : MonoBehaviour
    {
        [Tooltip("Şekil maskesini taşıyacak SpriteMask'ler. Her sorting layer için bir tane.")]
        [SerializeField] private SpriteMask[] shapeMasks;

        [Tooltip("Artık kullanılmayan elle yerleştirilmiş çerçeveler; kurulumda kapatılır.")]
        [SerializeField] private GameObject[] legacyFrames;

        // Tahtanın dışını örten çerçeve kalınlığı (hücre cinsinden). Ekranın tamamını
        // kapatacak kadar geniş olmalı.
        private const int OutsidePadding = 24;

        private Sprite generatedSprite;

        // PotionBoard tahtayı kurarken çağırır.
        public void Build(ArrayLayout layout, BoardGeometry geometry, Transform boardPresentation)
        {
            if (layout == null || geometry == null) return;

            foreach (GameObject frame in legacyFrames)
            {
                if (frame != null) frame.SetActive(false);
            }

            ReplaceSprite(CreateShapeSprite(layout, geometry.CellSize));
            PlaceMasks(geometry, boardPresentation);
        }

        private void OnDestroy()
        {
            if (generatedSprite != null) Destroy(generatedSprite.texture);
            if (generatedSprite != null) Destroy(generatedSprite);
        }

        // Her hücre bir piksel: kapalı hücre opak, açık hücre şeffaf.
        // Görünür alanın çevresine opak bir halka eklenir; tahtanın dışını o örter.
        private Sprite CreateShapeSprite(ArrayLayout layout, float cellSize)
        {
            int width = BoardDefinition.VisibleWidth + OutsidePadding * 2;
            int height = BoardDefinition.VisibleHeight + OutsidePadding * 2;

            Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 opaque = new(255, 255, 255, 255);
            Color32 clear = new(255, 255, 255, 0);
            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++) pixels[i] = opaque;

            for (int y = 0; y < BoardDefinition.VisibleHeight; y++)
            {
                bool[] row = layout.rows[y].row;

                for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
                {
                    // layout'ta true = kapalı hücre; orası maskeli kalır.
                    if (row[x]) continue;

                    pixels[(y + OutsidePadding) * width + x + OutsidePadding] = clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false);

            // 1 piksel = 1 hücre; sprite böylece hücre ızgarasına birebir oturur.
            return Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                1f / cellSize,
                0,
                SpriteMeshType.FullRect);
        }

        private void ReplaceSprite(Sprite sprite)
        {
            if (generatedSprite != null)
            {
                Destroy(generatedSprite.texture);
                Destroy(generatedSprite);
            }

            generatedSprite = sprite;

            foreach (SpriteMask mask in shapeMasks)
            {
                if (mask == null) continue;

                mask.sprite = sprite;
                mask.transform.localScale = Vector3.one;
                mask.gameObject.SetActive(true);
            }
        }

        // Maske görünür alanın merkezine oturur ve tahtayla birlikte hareket eder
        // (Cannon sinematiği boardPresentation'ı kaydırıyor).
        private void PlaceMasks(BoardGeometry geometry, Transform boardPresentation)
        {
            Vector2 bottomLeft = geometry.CellToWorld(Vector2Int.zero);
            Vector2 topRight = geometry.CellToWorld(
                new Vector2Int(BoardDefinition.VisibleWidth - 1, BoardDefinition.VisibleHeight - 1));

            Vector2 center = (bottomLeft + topRight) * 0.5f;

            foreach (SpriteMask mask in shapeMasks)
            {
                if (mask == null) continue;

                if (boardPresentation != null && mask.transform.parent != boardPresentation)
                {
                    mask.transform.SetParent(boardPresentation, worldPositionStays: false);
                }

                mask.transform.position = new Vector3(center.x, center.y, mask.transform.position.z);
            }
        }
    }
}
