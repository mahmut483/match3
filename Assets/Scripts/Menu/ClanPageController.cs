using UnityEngine;

// Clan sayfasının iki halini yönetir:
// clanı yoksa OutOfClan (katıl/ara/kur), varsa InOfClan (clan içi ekran).
public class ClanPageController : MonoBehaviour
{
    [SerializeField] private GameObject outOfClan;
    [SerializeField] private GameObject inOfClan;

    private void OnEnable()
    {
        FirebaseBootstrap.UserReady += OnUserReady;
        ClanService.ClanChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        FirebaseBootstrap.UserReady -= OnUserReady;
        ClanService.ClanChanged -= Refresh;
    }

    private void OnUserReady(UserData user)
    {
        Refresh();
    }

    public void Refresh()
    {
        FirebaseBootstrap bootstrap = FirebaseBootstrap.Instance;

        // Veri gelmeden karar veremeyiz; gelene kadar OutOfClan gösterilir.
        bool hasClan = bootstrap != null &&
                       bootstrap.IsReady &&
                       !string.IsNullOrEmpty(bootstrap.User.clanId);

        if (outOfClan != null) outOfClan.SetActive(!hasClan);
        if (inOfClan != null) inOfClan.SetActive(hasClan);
    }
}
