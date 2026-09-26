using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

using Match3.Backend;

namespace Match3.Menu
{
    // Rank sayfası: en yüksek seviyeli oyuncuları listeler ve
    // alt panelde girişli kullanıcının kendi sırasını gösterir.
    public class LeaderboardPanel : MonoBehaviour
    {
        [Header("Liste")]
        [SerializeField] private LeaderboardRow rowPrefab;
        [SerializeField] private Transform rowParent;      // ScrollView > Viewport > Content
        [SerializeField] private int topCount = 100;

        [Header("Kendi sıran")]
        [SerializeField] private LeaderboardRow ownRow;

        private readonly List<LeaderboardRow> spawnedRows = new();
        private bool isLoading;
        private bool hasLoaded;
        private long ownRank;

        private void OnEnable()
        {
            FirebaseBootstrap.UserReady += HandleUserUpdated;
            Load();
        }

        private void OnDisable()
        {
            FirebaseBootstrap.UserReady -= HandleUserUpdated;
        }

        public void Load()
        {
            FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

            // Veri henüz gelmediyse bir şey yapma; hazır olunca HandleUserUpdated çağıracak.
            if (bootstrap == null || !bootstrap.IsReady) return;

            if (isLoading) return;

            isLoading = true;

            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;

            LoadTopPlayers(db);
            LoadOwnRank(db, bootstrap.User);
        }

        // Oyuncunun verisi değişti (profil kaydı, can, bölüm ilerlemesi).
        //
        // Liste bilerek yeniden ÇEKİLMİYOR: bu olay can yenilenmesiyle dakikada bir de
        // geliyor, her seferinde 100 dökümanlık sorgu atmak kabul edilemez. Yalnızca bu
        // oyuncuya ait satırlar yerinde tazeleniyor — zaten değişen tek veri o. Diğer
        // oyuncuların bilgisi menüye her dönüşte (sahne yeniden yükleniyor) güncelleniyor.
        //
        // Bu tazeleme şart, çünkü sayfalar hiç deaktive edilmiyor: Rank sayfasına
        // kaydırmak OnEnable tetiklemez, liste sahne açıldığı gibi kalır.
        private void HandleUserUpdated(UserData user)
        {
            if (user == null) return;

            // Veri ilk kez hazırsa asıl yükleme şimdi yapılır.
            if (!hasLoaded)
            {
                Load();
                return;
            }

            if (ownRow != null && ownRank > 0) ownRow.Setup(ownRank, user);

            foreach (LeaderboardRow row in spawnedRows)
            {
                if (row != null && row.Uid == user.uid) row.Setup(row.Rank, user);
            }
        }

        // En yüksek seviyeli oyuncular. Tüm koleksiyonu değil, yalnızca ilk topCount kaydı çeker.
        private void LoadTopPlayers(FirebaseFirestore db)
        {
            Query query = db.Collection("users")
                .OrderByDescending("highestCompletedLevel")
                .Limit(topCount);

            query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                isLoading = false;

                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Sıralama alınamadı: " + task.Exception);
                    return;
                }

                ClearRows();
                hasLoaded = true;

                long rank = 1;

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    UserData user = doc.ConvertTo<UserData>();

                    // uid Firestore'da alan değil, döküman kimliği. Satırın kime ait
                    // olduğunu bilmesi için elle taşınır.
                    user.uid = doc.Id;

                    LeaderboardRow row = Instantiate(rowPrefab, rowParent);
                    row.Setup(rank, user);

                    spawnedRows.Add(row);
                    rank++;
                }
            });
        }

        // "Kaçıncıyım?" — Firestore döküman sırasını doğrudan vermez.
        // Benden yüksek seviyeli kaç oyuncu var sayılır, 1 eklenir.
        // Count sorgusu dökümanları tek tek okumaz; 1000 kayıt başına 1 okuma faturalanır.
        private void LoadOwnRank(FirebaseFirestore db, UserData user)
        {
            if (ownRow == null) return;

            Query higher = db.Collection("users")
                .WhereGreaterThan("highestCompletedLevel", user.highestCompletedLevel);

            higher.Count.GetSnapshotAsync(AggregateSource.Server).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Sıra hesaplanamadı: " + task.Exception);
                    return;
                }

                // Sıra saklanır: profil değişince satır yeni sorgu atmadan tazelenir.
                ownRank = task.Result.Count + 1;

                ownRow.Setup(ownRank, user);
            });
        }

        private void ClearRows()
        {
            foreach (LeaderboardRow row in spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }

            spawnedRows.Clear();
        }
    }
}
