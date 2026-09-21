using System.Collections.Generic;
using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Tahtadaki eşleşmeleri bulur. Durumsuz; her çağrı grid'i yeniden okur.
    public sealed class MatchFinder
    {
        private readonly BoardGrid grid;

        public MatchFinder(BoardGrid grid)
        {
            this.grid = grid;
        }

        // Görünür alandaki tüm grupları bulur. Havadaki taşlar eşleşmeye girmez;
        // bir gruba girmiş taş ikinci kez taranmaz ve başka grubun koluna katılmaz.
        public List<MatchResult> FindAll()
        {
            List<MatchResult> groups = new();
            HashSet<Potion> matched = new();

            for (int x = 0; x < BoardDefinition.VisibleWidth; x++)
            {
                for (int y = 0; y < BoardDefinition.VisibleHeight; y++)
                {
                    Potion potion = grid[x, y].Potion;

                    if (potion == null || potion.IsMoving || matched.Contains(potion)) continue;

                    MatchResult line = IsConnected(potion, matched);

                    if (line.ConnectedPotions.Count < 3) continue;

                    MatchResult group = SuperMatch(line, matched);

                    if (group.IsSuperMatch) group.ProtectedPotion = ChooseProtected(group, null);

                    groups.Add(group);
                    matched.UnionWith(group.ConnectedPotions);
                }
            }

            return groups;
        }

        // Yalnızca takas edilen iki taşın gruplarını bulur; tahtanın geri kalanı
        // cascade'in işidir. Süper eşleşmede eşleşmeyi yapan takas taşı korunur.
        public List<MatchResult> FindAround(Potion first, Potion second)
        {
            List<MatchResult> groups = new();
            HashSet<Potion> matched = new();

            foreach (Potion potion in new[] { first, second })
            {
                if (potion == null || matched.Contains(potion)) continue;

                MatchResult line = IsConnected(potion, matched);

                if (line.ConnectedPotions.Count < 3) continue;

                MatchResult group = SuperMatch(line, matched);

                if (group.IsSuperMatch) group.ProtectedPotion = ChooseProtected(group, potion);

                groups.Add(group);
                matched.UnionWith(group.ConnectedPotions);
            }

            return groups;
        }

        // Tercih edilen taş gruptaysa o, değilse rastgele bir üye.
        private static Potion ChooseProtected(MatchResult group, Potion preferred)
        {
            if (preferred != null && group.ConnectedPotions.Contains(preferred)) return preferred;

            return group.ConnectedPotions[Random.Range(0, group.ConnectedPotions.Count)];
        }

        // Taşın içinde bulunduğu düz hat: önce yatay, üçe ulaşmazsa dikey.
        // Üçlü düz hat normal eşleşme, dört ve üstü "Long" (roket).
        private MatchResult IsConnected(Potion potion, HashSet<Potion> matched)
        {
            List<Potion> line = CollectLine(potion, Vector2Int.right, matched);

            if (line.Count >= 3)
            {
                return new MatchResult
                {
                    ConnectedPotions = line,
                    Direction = line.Count == 3 ? MatchDirection.Horizontal : MatchDirection.LongHorizontal
                };
            }

            line = CollectLine(potion, Vector2Int.up, matched);

            return new MatchResult
            {
                ConnectedPotions = line,
                Direction = line.Count < 3 ? MatchDirection.None
                          : line.Count == 3 ? MatchDirection.Vertical
                          : MatchDirection.LongVertical
            };
        }

        // Taşın kendisi + verilen eksende iki yöne uzanan aynı renkli komşular.
        private List<Potion> CollectLine(Potion potion, Vector2Int axis, HashSet<Potion> matched)
        {
            List<Potion> line = new() { potion };

            CheckDirection(potion, axis, line, matched);
            CheckDirection(potion, -axis, line, matched);

            return line;
        }

        // Düz hattaki bir taştan dik yönde en az iki aynı renkli taş daha uzanıyorsa
        // (T/L şekli) hat ve kol birlikte tek "Super" (bomba) grubu olur. Yalnızca
        // ilk bulunan kol alınır; ikinci bir kol (H şekli) tahtada kalır.
        private MatchResult SuperMatch(MatchResult line, HashSet<Potion> matched)
        {
            bool horizontal = line.Direction == MatchDirection.Horizontal ||
                              line.Direction == MatchDirection.LongHorizontal;
            Vector2Int perpendicular = horizontal ? Vector2Int.up : Vector2Int.right;

            foreach (Potion pot in line.ConnectedPotions)
            {
                List<Potion> arm = new();

                CheckDirection(pot, perpendicular, arm, matched);
                CheckDirection(pot, -perpendicular, arm, matched);

                if (arm.Count < 2) continue;

                arm.AddRange(line.ConnectedPotions);

                return new MatchResult { ConnectedPotions = arm, Direction = MatchDirection.Super };
            }

            return line;
        }

        // Verilen yönde, aynı renkli ve bu taramada başka gruba girmemiş komşuları
        // toplar. Özel taşlar eşleşmeye katılmaz; havadaki veya boş hücrede durur.
        private void CheckDirection(Potion pot, Vector2Int direction, List<Potion> connectedPotions, HashSet<Potion> matched)
        {
            PotionType potionType = pot.PotionType;

            if (potionType == PotionType.Bomb || potionType == PotionType.Rocket) return;

            Vector2Int cell = new(pot.XIndex + direction.x, pot.YIndex + direction.y);

            while (BoardDefinition.IsPlayable(cell))
            {
                Potion neighbour = grid.PotionAt(cell);

                if (neighbour == null || neighbour.IsMoving) break;
                if (matched.Contains(neighbour) || neighbour.PotionType != potionType) break;

                connectedPotions.Add(neighbour);
                cell += direction;
            }
        }
    }
}
