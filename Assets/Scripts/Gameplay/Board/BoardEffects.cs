using UnityEngine;
using Match3.Gameplay.Potions;

namespace Match3.Gameplay.Board
{
    // Tahtaya ait görsel ve ses efektleri: particle prefab'ları, ses kaynakları,
    // efektlerin BoardPresentation altında doğması.
    public sealed class BoardEffects : MonoBehaviour
    {
        private const string BombClipResourcePath = "SFX/superBombSound";
        private const string DoubleRocketClipResourcePath = "SFX/duableRocket";

        [SerializeField] private ParticleSystem destroyParticlesRed;
        [SerializeField] private ParticleSystem destroyParticlesBlue;
        [SerializeField] private ParticleSystem destroyParticlesGreen;
        [SerializeField] private ParticleSystem destroyParticlesYellow;
        [SerializeField] private ParticleSystem explodingPaticles;

        [Tooltip("Süper bombanın kendi patlama efekti. Atanmazsa explodingPaticles kullanılır.")]
        [SerializeField] private ParticleSystem superExplodingParticles;

        [Tooltip("Roket oluşurken bir kez çalışan efekt; taşın köküne bağlanır.")]
        [SerializeField] private ParticleSystem rocketSpawnParticles;

        [Tooltip("Roket ateşlenince bir kez çalışan efekt. Yatay için tasarlandı; dikeyde -90° döner.")]
        [SerializeField] private ParticleSystem rocketFireParticles;

        [Tooltip("İki roket birleşirken DuableRocket animasyonuyla aynı karede doğar.")]
        [SerializeField] private ParticleSystem doubleRocketParticles;

        // Mikser grubu AudioSource'un özelliği, klibin değil — her sesin kendi kaynağı var.
        [SerializeField] private AudioSource matchSource;
        [SerializeField] private AudioSource superMatchSource;
        [SerializeField] private AudioSource explodingSource;

        [SerializeField] private AudioClip matchClip, superMatchClip, explodingClip, bombClip, doubleRocketClip;

        [Tooltip("Hammer'ın taşa vurduğu anda çalan ses.")]
        [SerializeField] private AudioClip hammerClip;

        [Header("Ses seviyeleri")]
        [SerializeField, Range(0f, 1f)] private float matchVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float superMatchVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float explodingVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bombVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float doubleRocketVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float hammerVolume = 1f;

        [Tooltip("Board'a bağlı kırılma/patlama efektlerinin root'u. Boş bırakılırsa BoardPresentation altında çalışma anında oluşturulur.")]
        [SerializeField] private Transform boardVfxRoot;

        private Transform boardPresentation;

        public ParticleSystem ExplodingParticles => explodingPaticles;
        public ParticleSystem SuperExplodingParticles => superExplodingParticles != null ? superExplodingParticles : explodingPaticles;
        public ParticleSystem RocketFireParticles => rocketFireParticles;
        public ParticleSystem DoubleRocketParticles => doubleRocketParticles;

        // Sahne referansı eksikse Resources'taki sabit yoldan yüklenir.
        private void Awake()
        {
            if (bombClip == null) bombClip = Resources.Load<AudioClip>(BombClipResourcePath);
            if (doubleRocketClip == null) doubleRocketClip = Resources.Load<AudioClip>(DoubleRocketClipResourcePath);
        }

        public void Initialize(Transform boardPresentation)
        {
            this.boardPresentation = boardPresentation;
        }

        public void PlayMatch() => matchSource.PlayOneShot(matchClip, matchVolume);
        public void PlaySuperMatch() => superMatchSource.PlayOneShot(superMatchClip, superMatchVolume);
        public void PlayExplosion() => explodingSource.PlayOneShot(explodingClip, explodingVolume);

        // Süper bomba fitili; klip yoksa sessiz kalır.
        public void PlayHammerHit()
        {
            if (hammerClip != null) explodingSource.PlayOneShot(hammerClip, hammerVolume);
        }

        public void PlayBombFuse()
        {
            if (bombClip != null) explodingSource.PlayOneShot(bombClip, bombVolume);
        }

        public void PlayDoubleRocketMerge()
        {
            if (doubleRocketClip != null) explodingSource.PlayOneShot(doubleRocketClip, doubleRocketVolume);
        }

        // Efekt ortak root altında doğar ve local simulation kullanır: BoardPresentation
        // hareket ederken world-space'de asılı kalmaz. Prefab null ise hiçbir şey yapmaz.
        public ParticleSystem SpawnBoardVfx(ParticleSystem prefab, Vector3 worldPosition, Quaternion rotation)
        {
            if (prefab == null) return null;

            Transform parent = GetBoardVfxRoot();
            ParticleSystem effect = parent != null
                ? Instantiate(prefab, worldPosition, rotation, parent)
                : Instantiate(prefab, worldPosition, rotation);

            SetParticleSimulationLocal(effect.gameObject);
            return effect;
        }

        // Taşın rengine göre kırılma efekti; özel taşların (bomb/rocket) efekti yok.
        public void SpawnDestroyParticle(Potion item)
        {
            ParticleSystem effect = item.PotionType switch
            {
                PotionType.Red => destroyParticlesRed,
                PotionType.Blue => destroyParticlesBlue,
                PotionType.Green => destroyParticlesGreen,
                PotionType.Yellow => destroyParticlesYellow,
                _ => null
            };

            SpawnBoardVfx(effect, item.transform.position, Quaternion.identity);
        }

        // Roket oluşma efekti taşın köküne bağlanır: cascade taşı indirirse efekt de iner.
        // worldPositionStays sayesinde taşın ölçeği efekte uygulanmaz.
        public void SpawnRocketParticle(Potion item)
        {
            if (rocketSpawnParticles == null) return;

            ParticleSystem effect = Instantiate(rocketSpawnParticles, item.transform.position, Quaternion.identity);

            effect.transform.SetParent(item.transform, worldPositionStays: true);
            SetParticleSimulationLocal(effect.gameObject);
        }

        public static void SetParticleSimulationLocal(GameObject effectRoot)
        {
            foreach (ParticleSystem system in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        // Root Inspector'da verilmediyse BoardPresentation altında bulunur ya da oluşturulur.
        private Transform GetBoardVfxRoot()
        {
            if (boardVfxRoot != null) return boardVfxRoot;
            if (boardPresentation == null) return null;

            Transform existingRoot = boardPresentation.Find("BoardVFX");
            if (existingRoot != null)
            {
                boardVfxRoot = existingRoot;
                return boardVfxRoot;
            }

            GameObject root = new("BoardVFX");
            boardVfxRoot = root.transform;
            boardVfxRoot.SetParent(boardPresentation, worldPositionStays: false);
            return boardVfxRoot;
        }
    }
}
