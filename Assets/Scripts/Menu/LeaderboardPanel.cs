using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

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

    private void OnEnable()
    {
        Load();
    }

    public void Load()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady)
        {
            // Veri henüz gelmediyse hazır olunca tekrar dene.
            FirebaseBootstrap.UserReady += OnUserReady;
            return;
        }

        if (isLoading) return;

        isLoading = true;

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;

        LoadTopPlayers(db);
        LoadOwnRank(db, bootstrap.User);
    }

    private void OnUserReady(UserData user)
    {
        FirebaseBootstrap.UserReady -= OnUserReady;

        if (isActiveAndEnabled) Load();
    }

    private void OnDisable()
    {
        FirebaseBootstrap.UserReady -= OnUserReady;
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

            long rank = 1;

            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                UserData user = doc.ConvertTo<UserData>();

                LeaderboardRow row = Instantiate(rowPrefab, rowParent);
                row.Setup(rank, user.displayName, user.highestCompletedLevel);

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

            long rank = task.Result.Count + 1;

            ownRow.Setup(rank, user.displayName, user.highestCompletedLevel);
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
