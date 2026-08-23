using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

// Clan sohbeti ve can istekleri.
public static class ClanChatService
{
    // Bir can isteğine en fazla kaç kişi katkı verebilir.
    public const int LivesPerRequest = 5;

    // İki can isteği arasında beklenmesi gereken süre (saniye).
    public const int RequestCooldownSeconds = 1800;

    // Mesajlar bu süre sonunda Firestore TTL politikasıyla silinir.
    private const int MessageLifetimeDays = 7;

    private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

    private static CollectionReference Messages(string clanId) =>
        Db.Collection("clans").Document(clanId).Collection("messages");

    // Canlı dinleme: yeni mesaj geldiğinde anında tetiklenir.
    // Dönen kayıt sahne kapanırken Stop() ile durdurulmalı.
    public static ListenerRegistration Listen(string clanId, int limit, Action<List<ClanMessage>> onUpdate)
    {
        Query query = Messages(clanId)
            .OrderByDescending("createdAt")
            .Limit(limit);

        return query.Listen(snapshot =>
        {
            List<ClanMessage> messages = new List<ClanMessage>();

            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                ClanMessage message = doc.ConvertTo<ClanMessage>();
                message.id = doc.Id;
                messages.Add(message);
            }

            // Sorgu yeniden eskiye geldi; ekranda eskiden yeniye gösterilecek.
            messages.Reverse();

            onUpdate?.Invoke(messages);
        });
    }

    public static void SendChat(string text, Action<bool> onDone = null)
    {
        string trimmed = (text ?? "").Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            onDone?.Invoke(false);
            return;
        }

        Send(ClanMessageType.Chat, trimmed, onDone);
    }

    // Bekleme süresinden kalan saniye. 0 ise istek atılabilir.
    public static double RemainingRequestCooldown()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady) return 0;

        double elapsed = (DateTime.UtcNow - bootstrap.User.lastLifeRequestAt.ToDateTime()).TotalSeconds;

        return Math.Max(0, RequestCooldownSeconds - elapsed);
    }

    public static void SendLifeRequest(Action<bool> onDone = null)
    {
        // UI'a güvenmiyoruz; kontrol burada da var.
        if (RemainingRequestCooldown() > 0)
        {
            onDone?.Invoke(false);
            return;
        }

        Send(ClanMessageType.LifeRequest, "asking for free lives!", success =>
        {
            if (success) StampRequestTime();

            onDone?.Invoke(success);
        });
    }

    // İstek zamanını hem yerel kopyaya hem sunucuya yazar.
    private static void StampRequestTime()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        Timestamp now = Timestamp.FromDateTime(DateTime.UtcNow);

        bootstrap.User.lastLifeRequestAt = now;

        Db.Collection("users").Document(bootstrap.Uid).UpdateAsync("lastLifeRequestAt", now);
    }

    private static void Send(ClanMessageType type, string text, Action<bool> onDone)
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady || string.IsNullOrEmpty(bootstrap.User.clanId))
        {
            onDone?.Invoke(false);
            return;
        }

        UserData user = bootstrap.User;
        DateTime now = DateTime.UtcNow;

        ClanMessage message = new ClanMessage
        {
            type = (int)type,
            senderUid = bootstrap.Uid,
            senderName = user.displayName,
            senderAvatarIndex = user.avatarIndex,
            text = text,
            createdAt = Timestamp.FromDateTime(now),
            expireAt = Timestamp.FromDateTime(now.AddDays(MessageLifetimeDays)),
            donorUids = new List<string>(),
            claimed = false
        };

        Messages(user.clanId).AddAsync(message).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Mesaj gönderilemedi: " + task.Exception);
                onDone?.Invoke(false);
                return;
            }

            onDone?.Invoke(true);
        });
    }

    // Can bağışı: bağışçı yalnızca kendi uid'ini listeye ekler.
    // Başkasının dökümanına yazamadığı için canı alıcı sonradan kendisi toplar.
    public static void DonateLife(ClanMessage request, Action<bool> onDone = null)
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady || string.IsNullOrEmpty(bootstrap.User.clanId))
        {
            onDone?.Invoke(false);
            return;
        }

        DocumentReference doc = Messages(bootstrap.User.clanId).Document(request.id);

        doc.UpdateAsync("donorUids", FieldValue.ArrayUnion(bootstrap.Uid))
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Can gönderilemedi: " + task.Exception);
                    onDone?.Invoke(false);
                    return;
                }

                onDone?.Invoke(true);
            });
    }

    // Kendi isteğine gelen canları toplar. Yalnızca istek sahibi çağırabilir.
    public static void ClaimLives(ClanMessage request, Action<int> onDone = null)
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        if (bootstrap == null || !bootstrap.IsReady) { onDone?.Invoke(0); return; }
        if (request.senderUid != bootstrap.Uid || request.claimed) { onDone?.Invoke(0); return; }
        if (request.DonorCount == 0) { onDone?.Invoke(0); return; }

        UserData user = bootstrap.User;

        int gained = Mathf.Min(request.DonorCount, LivesPerRequest);
        int newLives = Mathf.Min(user.lives + gained, 5);

        DocumentReference messageDoc = Messages(user.clanId).Document(request.id);
        DocumentReference userDoc = Db.Collection("users").Document(bootstrap.Uid);

        messageDoc.UpdateAsync("claimed", true).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("İstek kapatılamadı: " + task.Exception);
                onDone?.Invoke(0);
                return;
            }

            user.lives = newLives;

            userDoc.UpdateAsync(new Dictionary<string, object>
            {
                { "lives", newLives },
                { "livesUpdatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
            });

            onDone?.Invoke(gained);
        });
    }
}
