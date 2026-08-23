using System.Collections.Generic;
using TMPro;
using UnityEngine;

// JoinPage: tüm clanların listesi. Satırdaki butona basınca clana katılınır.
public class ClanListPanel : MonoBehaviour
{
    [SerializeField] private ClanRowUI rowPrefab;
    [SerializeField] private Transform rowParent;      // ScrollView > Viewport > Content
    [SerializeField] private int loadLimit = 30;
    [SerializeField] private TMP_Text emptyText;       // opsiyonel: "Clan bulunamadı"

    [Tooltip("Sayfa açılınca tüm clanları yükle. Arama sonuç listesinde KAPALI olmalı — " +
             "yoksa arama yapmadan bütün clanlar listelenir.")]
    [SerializeField] private bool loadOnEnable = true;

    private readonly List<ClanRowUI> rows = new();

    private void OnEnable()
    {
        if (loadOnEnable)
        {
            Reload();
            return;
        }

        // Arama listesi: henüz arama yapılmadı, "sonuç yok" yazısı görünmesin.
        Clear();

        if (emptyText != null) emptyText.gameObject.SetActive(false);
    }

    public void Reload()
    {
        ClanService.LoadClans(loadLimit, Show);
    }

    // Arama sonuçlarını da aynı listede göstermek için dışarıdan çağrılabilir.
    public void Show(List<ClanData> clans)
    {
        Clear();

        foreach (ClanData clan in clans)
        {
            ClanRowUI row = Instantiate(rowPrefab, rowParent);
            row.Setup(clan, Join);
            rows.Add(row);
        }

        if (emptyText != null) emptyText.gameObject.SetActive(clans.Count == 0);
    }

    private void Join(ClanData clan)
    {
        ClanService.JoinClan(clan, (success, message) =>
        {
            if (!success)
            {
                Debug.LogWarning("Katılınamadı: " + message);
                return;
            }

            // Katıldıktan sonra liste tazelenir; üye sayısı güncel görünür.
            Reload();
        });
    }

    // Listeyi boşaltır ama "sonuç yok" yazısını göstermez (arama iptal edildiğinde).
    public void ClearResults()
    {
        Clear();

        if (emptyText != null) emptyText.gameObject.SetActive(false);
    }

    private void Clear()
    {
        foreach (ClanRowUI row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }

        rows.Clear();
    }
}
