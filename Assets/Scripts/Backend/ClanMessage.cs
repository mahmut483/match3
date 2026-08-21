using System.Collections.Generic;
using Firebase.Firestore;

public enum ClanMessageType
{
    Chat = 0,
    LifeRequest = 1,
    System = 2
}

// clans/{clanId}/messages/{messageId}
// Sohbet mesajı ve can isteği aynı akışta durur — tek liste, tek dinleyici.
[FirestoreData]
public class ClanMessage
{
    [FirestoreProperty] public int type { get; set; }

    // Gönderenin adı ve avatarı mesaja KOPYALANIR.
    // Firestore join yapamadığı için, aksi halde her mesaj başına bir kullanıcı okuması gerekirdi.
    [FirestoreProperty] public string senderUid { get; set; }
    [FirestoreProperty] public string senderName { get; set; }
    [FirestoreProperty] public int senderAvatarIndex { get; set; }

    [FirestoreProperty] public string text { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }

    // Firestore TTL politikası bu alana bakıp eski mesajları otomatik siler.
    [FirestoreProperty] public Timestamp expireAt { get; set; }

    // Yalnız can isteği için
    [FirestoreProperty] public List<string> donorUids { get; set; }
    [FirestoreProperty] public bool claimed { get; set; }

    [System.NonSerialized] public string id;

    public ClanMessageType Type => (ClanMessageType)type;

    public int DonorCount => donorUids != null ? donorUids.Count : 0;
}
