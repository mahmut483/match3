using System.Collections.Generic;
using Firebase.Firestore;

// Firestore'daki users/{uid} dökümanının karşılığı.
// Firestore eşlemesi ALAN değil PROPERTY ister (get/set şart).
[FirestoreData]
public class UserData
{
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
}
