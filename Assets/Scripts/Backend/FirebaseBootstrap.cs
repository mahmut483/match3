using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

// Oyun açılışında bir kez çalışır: Firebase'i hazırlar, anonim giriş yapar,
// kullanıcının Firestore dökümanını yükler (yoksa oluşturur).
// Sahneler arasında yaşar (DontDestroyOnLoad).
public class FirebaseBootstrap : MonoBehaviour
{
    public static FirebaseBootstrap Instance { get; private set; }

    // Kullanıcı verisi hazır olduğunda tetiklenir. UI bunu dinleyip kendini günceller.
    public static event Action<UserData> UserReady;

    public string Uid { get; private set; }
    public UserData User { get; private set; }
    public bool IsReady { get; private set; }

    [Header("Yeni oyuncu varsayılanları")]
    [SerializeField] private int startingLives = 5;
    [SerializeField] private string defaultNamePrefix = "Oyuncu";

    private FirebaseAuth auth;
    private FirebaseFirestore db;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Android'de Google Play Services eksikse burada düzeltilir.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("Firebase hazır değil: " + task.Result);
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;

            SignIn();
        });
    }

    // Cihazda kayıtlı hesap varsa yeni hesap AÇILMAZ; aynı uid geri gelir.
    private void SignIn()
    {
        if (auth.CurrentUser != null)
        {
            OnSignedIn(auth.CurrentUser.UserId);
            return;
        }

        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Anonim giriş başarısız: " + task.Exception);
                return;
            }

            OnSignedIn(task.Result.User.UserId);
        });
    }

    private void OnSignedIn(string uid)
    {
        Uid = uid;

        DocumentReference doc = db.Collection("users").Document(uid);

        doc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Kullanıcı dökümanı okunamadı: " + task.Exception);
                return;
            }

            if (task.Result.Exists)
            {
                User = task.Result.ConvertTo<UserData>();
                TouchLastSeen(doc);
                Finish();
            }
            else
            {
                CreateUser(doc);
            }
        });
    }

    private void CreateUser(DocumentReference doc)
    {
        Timestamp now = Timestamp.FromDateTime(DateTime.UtcNow);

        // Güvenlik kuralları yeni kullanıcıda totalScore/level/gold = 0 bekliyor.
        User = new UserData
        {
            displayName = defaultNamePrefix + UnityEngine.Random.Range(1000, 9999),
            avatarIndex = 0,
            isLinked = false,
            createdAt = now,
            lastSeenAt = now,
            highestCompletedLevel = 0,
            totalScore = 0,
            bestScores = new Dictionary<string, int>(),
            lives = startingLives,
            livesUpdatedAt = now,
            gold = 0,
            clanId = null
        };

        doc.SetAsync(User).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Kullanıcı oluşturulamadı: " + task.Exception);
                return;
            }

            Debug.Log("Yeni kullanıcı oluşturuldu: " + Uid);
            Finish();
        });
    }

    // Profil bilgilerini günceller. Yalnızca bu iki alana dokunur,
    // ilerleme/ekonomi alanları etkilenmez.
    public void UpdateProfile(string newName, int newAvatarIndex, Action<bool> onDone)
    {
        if (!IsReady)
        {
            onDone?.Invoke(false);
            return;
        }

        DocumentReference doc = db.Collection("users").Document(Uid);

        Dictionary<string, object> fields = new Dictionary<string, object>
        {
            { "displayName", newName },
            { "avatarIndex", newAvatarIndex }
        };

        doc.UpdateAsync(fields).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("Profil güncellenemedi: " + task.Exception);
                onDone?.Invoke(false);
                return;
            }

            // Yerel kopyayı da güncelle ki UI'lar doğru veriyi görsün.
            User.displayName = newName;
            User.avatarIndex = newAvatarIndex;

            UserReady?.Invoke(User);
            onDone?.Invoke(true);
        });
    }

    // Son görülme zamanı — başka bir alana dokunmaz.
    private void TouchLastSeen(DocumentReference doc)
    {
        doc.UpdateAsync("lastSeenAt", Timestamp.FromDateTime(DateTime.UtcNow));
    }

    private void Finish()
    {
        IsReady = true;
        UserReady?.Invoke(User);
    }
}
