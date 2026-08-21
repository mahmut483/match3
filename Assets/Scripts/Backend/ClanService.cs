using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

// Clan ile ilgili tüm Firestore işlemleri tek yerde.
// Sahneye eklenmez; statik olarak çağrılır.
public static class ClanService
{
    // Şimdilik clan kurmak ücretsiz. Ücretlendirmeye geçilince bu değeri artırmak yeterli.
    public const int CreateCost = 0;

    // Oyuncunun clan durumu değiştiğinde tetiklenir (kurdu / katıldı).
    public static event Action ClanChanged;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    // Listeleme: en güçlü clanlar önce. Tüm koleksiyon değil, yalnızca ilk 'limit' kayıt çekilir.
    public static void LoadClans(int limit, Action<List<ClanData>> onDone)
    {
        Query query = Db.Collection("clans")
            .OrderByDescending("totalScore")
            .Limit(limit);

        Run(query, onDone, "Clan listesi alınamadı");
    }

    // Arama: Firestore metin araması yapamaz, bu yüzden ön-ek sorgusu kullanılır.
    // "ejder" araması "ejderhalar", "ejderler" gibi isimleri bulur; ortadan eşleşme bulunmaz.
    public static void SearchClans(string text, int limit, Action<List<ClanData>> onDone)
    {
        string q = (text ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(q))
        {
            onDone?.Invoke(new List<ClanData>());
            return;
        }

        Query query = Db.Collection("clans")
            .WhereGreaterThanOrEqualTo("nameLower", q)
            .WhereLessThan("nameLower", q + "\uf8ff")
            .Limit(limit);

        Run(query, onDone, "Arama başarısız");
    }

    private static void Run(Query query, Action<List<ClanData>> onDone, string errorLabel)
    {
        query.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError(errorLabel + ": " + task.Exception);
                onDone?.Invoke(new List<ClanData>());
                return;
            }

            List<ClanData> results = new List<ClanData>();

            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                ClanData clan = doc.ConvertTo<ClanData>();
                clan.id = doc.Id;
                results.Add(clan);
            }

            onDone?.Invoke(results);
        });
    }

    // Clan kurma. İsim benzersizliği, clan dökümanı ve kullanıcı güncellemesi
    // TEK transaction içinde yapılır — yarıda kalıp tutarsız veri bırakmaz.
    public static void CreateClan(ClanData clan, Action<bool, string> onDone)
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady)
        {
            onDone?.Invoke(false, "Bağlantı yok.");
            return;
        }

        UserData user = bootstrap.User;

        if (!string.IsNullOrEmpty(user.clanId))
        {
            onDone?.Invoke(false, "Zaten bir clandasın.");
            return;
        }

        if (CreateCost > 0 && user.gold < CreateCost)
        {
            onDone?.Invoke(false, "Yeterli altının yok.");
            return;
        }

        clan.nameLower = clan.name.Trim().ToLowerInvariant();
        clan.leaderUid = bootstrap.Uid;
        clan.memberCount = 1;
        clan.totalScore = user.totalScore;
        clan.createdAt = Timestamp.FromDateTime(DateTime.UtcNow);

        DocumentReference clanDoc = Db.Collection("clans").Document();
        DocumentReference nameDoc = Db.Collection("clanNames").Document(clan.nameLower);
        DocumentReference userDoc = Db.Collection("users").Document(bootstrap.Uid);

        int newGold = user.gold - CreateCost;

        Db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot nameSnapshot = await transaction.GetSnapshotAsync(nameDoc);

            // İsim rezerve edilmişse iptal.
            if (nameSnapshot.Exists) throw new Exception("NAME_TAKEN");

            transaction.Set(nameDoc, new Dictionary<string, object> { { "clanId", clanDoc.Id } });
            transaction.Set(clanDoc, clan);

            Dictionary<string, object> userFields = new Dictionary<string, object>
            {
                { "clanId", clanDoc.Id }
            };

            // Ücret varsa altını da düş.
            if (CreateCost > 0) userFields["gold"] = newGold;

            transaction.Update(userDoc, userFields);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                bool nameTaken = task.Exception != null &&
                                 task.Exception.ToString().Contains("NAME_TAKEN");

                if (!nameTaken) Debug.LogError("Clan kurulamadı: " + task.Exception);

                onDone?.Invoke(false, nameTaken ? "Bu isim alınmış." : "Clan kurulamadı.");
                return;
            }

            // Yerel kopyayı da güncelle.
            clan.id = clanDoc.Id;
            user.clanId = clanDoc.Id;

            if (CreateCost > 0) user.gold = newGold;

            // Kuran kişi otomatik olarak clanın üyesi olur.
            ClanChanged?.Invoke();
            onDone?.Invoke(true, "");
        });
    }

    // Clana katılma: üye sayısı artırılır, kullanıcının clanId'si yazılır.
    public static void JoinClan(ClanData clan, Action<bool, string> onDone)
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady)
        {
            onDone?.Invoke(false, "Bağlantı yok.");
            return;
        }

        UserData user = bootstrap.User;

        if (!string.IsNullOrEmpty(user.clanId))
        {
            onDone?.Invoke(false, "Zaten bir clandasın.");
            return;
        }

        if (user.highestCompletedLevel < clan.minLevel)
        {
            onDone?.Invoke(false, "Seviyen yetersiz.");
            return;
        }

        if (clan.memberCount >= clan.maxMembers)
        {
            onDone?.Invoke(false, "Clan dolu.");
            return;
        }

        DocumentReference clanDoc = Db.Collection("clans").Document(clan.id);
        DocumentReference userDoc = Db.Collection("users").Document(bootstrap.Uid);

        Db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot clanSnapshot = await transaction.GetSnapshotAsync(clanDoc);

            if (!clanSnapshot.Exists) throw new Exception("CLAN_GONE");

            ClanData current = clanSnapshot.ConvertTo<ClanData>();

            // Araya başka biri girip doldurduysa burada yakalanır.
            if (current.memberCount >= current.maxMembers) throw new Exception("CLAN_FULL");

            transaction.Update(clanDoc, new Dictionary<string, object>
            {
                { "memberCount", current.memberCount + 1 },
                { "totalScore", current.totalScore + user.totalScore }
            });

            transaction.Update(userDoc, new Dictionary<string, object>
            {
                { "clanId", clan.id }
            });
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                string message = task.Exception != null && task.Exception.ToString().Contains("CLAN_FULL")
                    ? "Clan dolu."
                    : "Katılınamadı.";

                Debug.LogError("Clana katılma hatası: " + task.Exception);
                onDone?.Invoke(false, message);
                return;
            }

            user.clanId = clan.id;

            ClanChanged?.Invoke();
            onDone?.Invoke(true, "");
        });
    }
}
