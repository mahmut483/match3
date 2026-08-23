using System.Collections.Generic;
using Firebase.Firestore;

// Firestore'daki users/{uid} dökümanının karşılığı.
// Firestore eşlemesi ALAN değil PROPERTY ister (get/set şart).
[FirestoreData]
public class UserData
{
    // Döküman kimliği — Firestore'da alan olarak tutulmaz, okurken elle atanır.
    [System.NonSerialized] public string uid;

    [FirestoreProperty] public string displayName { get; set; }
    [FirestoreProperty] public int avatarIndex { get; set; }
    [FirestoreProperty] public bool isLinked { get; set; }   // Google/Apple'a bağlandı mı

    [FirestoreProperty] public Timestamp createdAt { get; set; }
    [FirestoreProperty] public Timestamp lastSeenAt { get; set; }

    [FirestoreProperty] public int highestCompletedLevel { get; set; }
    [FirestoreProperty] public int totalScore { get; set; }
    [FirestoreProperty] public Dictionary<string, int> bestScores { get; set; }

    [FirestoreProperty] public int lives { get; set; }
    [FirestoreProperty] public Timestamp livesUpdatedAt { get; set; }
    [FirestoreProperty] public int gold { get; set; }

    [FirestoreProperty] public string clanId { get; set; }

    // Son can isteği zamanı — bekleme süresi bundan hesaplanır.
    // Sunucuda tutulur ki oyuncu uygulamayı kapatıp açarak süreyi atlatamasın.
    [FirestoreProperty] public Timestamp lastLifeRequestAt { get; set; }
}
