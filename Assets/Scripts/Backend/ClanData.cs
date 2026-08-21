using Firebase.Firestore;

// Firestore'daki clans/{clanId} dökümanının karşılığı.
[FirestoreData]
public class ClanData
{
    [FirestoreProperty] public string name { get; set; }

    // Aramada kullanılır: isim küçük harfe çevrilmiş hali.
    // Firestore metin araması yapamadığı için ön-ek sorgusu bu alan üzerinden çalışır.
    [FirestoreProperty] public string nameLower { get; set; }

    [FirestoreProperty] public string description { get; set; }
    [FirestoreProperty] public int emblemIndex { get; set; }

    [FirestoreProperty] public string leaderUid { get; set; }
    [FirestoreProperty] public int memberCount { get; set; }
    [FirestoreProperty] public int maxMembers { get; set; }
    [FirestoreProperty] public int totalScore { get; set; }

    // Katılım şartları
    [FirestoreProperty] public int minLevel { get; set; }
    [FirestoreProperty] public int minCup { get; set; }
    [FirestoreProperty] public int joinType { get; set; }   // 0 = herkese açık, 1 = onaylı, 2 = kapalı

    [FirestoreProperty] public Timestamp createdAt { get; set; }

    // Döküman kimliği — Firestore'da alan olarak tutulmaz, okurken elle atanır.
    [System.NonSerialized] public string id;
}
