using System.Collections.Generic;
using UnityEngine;

// Prefabın animasyonu oluştuğu anda başlar. PotionBoard dışarıdaki
// HammerTravelRoot'u taşırken Animator prefabın içindeki pozları yönetir.
public class HammerStrikeView : MonoBehaviour
{
    [SerializeField] private Animator hammerAnimator;
    [SerializeField] private string strikeStateName = "HammerStrike";

    private Transform travelRoot;
    private SpriteRenderer hammerVisual;
    private ParticleSystem[] impactParticles;
    private Vector3 initialVisualOffset;
    private Vector3 travelPosition;
    private float returnProgress;

    private void Awake()
    {
        PrepareImpactEffect();
    }

    // Prefabdaki bütün alt Particle System'leri otomatik bulur. Play On Awake
    // açık olsa bile prefab oluştuğu karede durdurur; efekt yalnızca darbede oynar.
    public void PrepareImpactEffect()
    {
        ResolveImpactParticles();

        foreach (ParticleSystem particles in impactParticles)
        {
            if (particles == null) continue;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // CFXR_Effect, durmuş bir sistemi birkaç kare sonra Destroy edebilir.
        // Darbe anına kadar objeyi pasif tutunca hem kendi zamanlayıcısı hem de
        // Play On Awake bekler; Hammer potion'a varmadan efekt kaybolmaz.
        foreach (Transform effectRoot in FindEffectRoots())
        {
            if (effectRoot != null && effectRoot != transform)
                effectRoot.gameObject.SetActive(false);
        }
    }

    public void PlayImpactEffect()
    {
        ResolveImpactParticles();

        HashSet<Transform> effectRoots = new();
        float effectLifetime = 0f;

        foreach (ParticleSystem particles in impactParticles)
        {
            if (particles == null) continue;

            effectRoots.Add(EffectRootOf(particles));

            ParticleSystem.MainModule main = particles.main;
            effectLifetime = Mathf.Max(
                effectLifetime,
                main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        foreach (Transform effectRoot in effectRoots)
        {
            if (effectRoot == null) continue;

            effectRoot.SetParent(null, true);
            effectRoot.gameObject.SetActive(true);
        }

        foreach (ParticleSystem particles in impactParticles)
        {
            if (particles == null) continue;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(false);
        }

        foreach (Transform effectRoot in effectRoots)
        {
            if (effectRoot != null) Destroy(effectRoot.gameObject, effectLifetime + 0.25f);
        }

        impactParticles = null;
    }

    public void BeginTravel(Transform parent)
    {
        travelRoot = parent;
        hammerVisual = GetComponentInChildren<SpriteRenderer>(true);
        travelPosition = parent.position;
        initialVisualOffset = VisualPosition() - parent.position;
        returnProgress = 0f;
    }

    public void SetTravelPosition(Vector3 position, float returning)
    {
        travelPosition = position;
        returnProgress = Mathf.Clamp01(returning);
        ApplyTravelPosition();
    }

    private void LateUpdate()
    {
        // Animator bu karenin Bone pozunu yazdıktan sonra telafi edilir.
        ApplyTravelPosition();
    }

    private void ApplyTravelPosition()
    {
        if (travelRoot == null) return;

        // Her karede nominal konumdan başlamak telafinin birikmesini önler.
        travelRoot.position = travelPosition;
        Vector3 animatedOffset = VisualPosition() - travelPosition;
        travelRoot.position += (initialVisualOffset - animatedOffset) * returnProgress;
    }

    private Vector3 VisualPosition()
    {
        if (hammerVisual != null && hammerVisual.sprite != null)
            return hammerVisual.transform.TransformPoint(hammerVisual.sprite.bounds.center);

        return hammerAnimator != null ? hammerAnimator.transform.position : transform.position;
    }

    public void PlayStrike()
    {
        Animator animator = ResolveAnimator();
        if (animator == null) return;

        animator.enabled = true;
        animator.Rebind();
        animator.Play(strikeStateName, 0, 0f);
        animator.Update(0f);
    }

    // HammerBTN UI canvas'ında, potion ise board dünya uzayında. Başlangıç
    // noktasını aynı ekran pikselinde, hedef hücrenin kamera derinliğinde kurar.
    public static Vector3 WorldPointFromScreenPoint(
        Camera camera,
        Vector2 screenPoint,
        Vector3 targetWorldPosition)
    {
        if (camera == null) return targetWorldPosition;

        float targetDepth = camera.WorldToScreenPoint(targetWorldPosition).z;
        return camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, targetDepth));
    }

    private Animator ResolveAnimator()
    {
        if (hammerAnimator == null)
        {
            hammerAnimator = GetComponentInChildren<Animator>(true);
        }

        return hammerAnimator;
    }

    private void ResolveImpactParticles()
    {
        if (impactParticles == null || impactParticles.Length == 0)
        {
            impactParticles = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private HashSet<Transform> FindEffectRoots()
    {
        HashSet<Transform> effectRoots = new();

        foreach (ParticleSystem particles in impactParticles)
        {
            if (particles == null) continue;

            effectRoots.Add(EffectRootOf(particles));
        }

        return effectRoots;
    }

    // BombExploding gibi iç içe sistemlerde en üst Particle System kökü.
    // Alt sistemleri tek tek ayırmak yerine kök taşınır; ışık gibi particle
    // olmayan çocuklar da kökle birlikte darbe noktasında kalır.
    private static Transform EffectRootOf(ParticleSystem particles)
    {
        Transform root = particles.transform;

        while (root.parent != null && root.parent.GetComponent<ParticleSystem>() != null)
        {
            root = root.parent;
        }

        return root;
    }
}
