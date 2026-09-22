using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

namespace Match3.Backend
{
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

                // Firestore örneği bilerek burada kurulmaz: kimlik bilgilerini kurulduğu anda
                // alıyor, girişten önce kurulursa tüm istekler kimliksiz gidiyor.
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

            // Giriş tamamlandıktan sonra ilk kez oluşturulur; böylece kimlikli çalışır.
            db = FirebaseFirestore.DefaultInstance;

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
                lives = LifeRules.MaxLives,
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

                NotifyUserUpdated();
                onDone?.Invoke(true);
            });
        }

        // Yerel kullanıcı verisini değiştiren servisler üst bar gibi açık UI'ları
        // aynı UserData örneğiyle anında yenilemek için bunu çağırır.
        public void NotifyUserUpdated()
        {
            if (User != null) UserReady?.Invoke(User);
        }

        // Sayaç dolmuşsa dolan aralık başına bir can ekler (LifeRules), yerel veri hemen,
        // Firestore arkadan güncellenir. Eklenecek can yoksa hiçbir şey yazmaz.
        public void RegenerateLives(Action<bool> onDone = null)
        {
            if (!IsReady || User == null)
            {
                onDone?.Invoke(false);
                return;
            }

            DateTime now = DateTime.UtcNow;
            DateTime updatedAt = User.livesUpdatedAt.ToDateTime();
            int gained = LifeRules.GainedLives(User.lives, updatedAt, now);

            if (gained == 0)
            {
                onDone?.Invoke(true);
                return;
            }

            DateTime nextUpdatedAt = LifeRules.NextUpdatedAt(User.lives, updatedAt, gained, now);
            WriteLives(User.lives + gained, Timestamp.FromDateTime(nextUpdatedAt), "Canlar yenilenirken", onDone);
        }

        // Bölüm kaybedilince/terk edilince bir can düşer. Sayaç yalnızca dolu durumdan
        // düşerken başlar; can zaten eksikse kaldığı yerden devam eder.
        public void SpendLife(Action<bool> onDone = null)
        {
            if (!IsReady || User == null || User.lives <= 0)
            {
                onDone?.Invoke(false);
                return;
            }

            Timestamp updatedAt = User.lives >= LifeRules.MaxLives
                ? Timestamp.FromDateTime(DateTime.UtcNow)
                : User.livesUpdatedAt;

            WriteLives(User.lives - 1, updatedAt, "Can düşülürken", onDone);
        }

        private void WriteLives(int lives, Timestamp updatedAt, string context, Action<bool> onDone)
        {
            User.lives = lives;
            User.livesUpdatedAt = updatedAt;
            NotifyUserUpdated();

            Dictionary<string, object> fields = new Dictionary<string, object>
            {
                { "lives", lives },
                { "livesUpdatedAt", updatedAt }
            };

            db.Collection("users").Document(Uid).UpdateAsync(fields)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError(context + " Firestore güncellenemedi: " + task.Exception);
                        onDone?.Invoke(false);
                        return;
                    }

                    onDone?.Invoke(true);
                });
        }

        // Bölüm kazanılınca ilerleme: en yüksek bölüm, toplam puan, altın, bölüm rekoru.
        // Puan/altın Increment ile yazılır ki başka bir yazma üstüne binmesin; kullanıcı
        // bir clandaysa clanın toplam puanı da aynı miktar artar (üye toplamı olarak tutuluyor).
        public void CompleteLevel(int level, int points, int goldReward, Action<bool> onDone = null)
        {
            if (!IsReady || User == null)
            {
                onDone?.Invoke(false);
                return;
            }

            string levelKey = level.ToString();
            User.bestScores ??= new Dictionary<string, int>();
            User.bestScores.TryGetValue(levelKey, out int previousBest);
            int bestScore = Math.Max(previousBest, points);

            User.highestCompletedLevel = Math.Max(User.highestCompletedLevel, level);
            User.totalScore += points;
            User.gold += goldReward;
            User.bestScores[levelKey] = bestScore;
            NotifyUserUpdated();

            WriteBatch batch = db.StartBatch();

            batch.Update(db.Collection("users").Document(Uid), new Dictionary<FieldPath, object>
            {
                { new FieldPath("highestCompletedLevel"), User.highestCompletedLevel },
                { new FieldPath("totalScore"), FieldValue.Increment(points) },
                { new FieldPath("gold"), FieldValue.Increment(goldReward) },
                { new FieldPath("bestScores", levelKey), bestScore }
            });

            if (!string.IsNullOrEmpty(User.clanId))
            {
                batch.Update(db.Collection("clans").Document(User.clanId), "totalScore", FieldValue.Increment(points));
            }

            batch.CommitAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Bölüm ilerlemesi Firestore'a yazılamadı: " + task.Exception);
                    onDone?.Invoke(false);
                    return;
                }

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
            NotifyUserUpdated();
        }
    }
}
