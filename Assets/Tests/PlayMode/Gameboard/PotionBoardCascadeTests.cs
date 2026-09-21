#if UNITY_EDITOR
using Match3.Gameplay.Board;
using Match3.Gameplay.Potions;
using Match3.Gameplay.Session;
using Match3.Levels;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PotionBoardCascadeTests : PotionBoardTestBase
{
    // Eşzamanlı ikinci bir işlemin refill'i, ilk işlemin henüz "yok edildi"
    // diye beklediği taşı havuzdan çekip yeniden aktif edebilir. Bekleme taşın
    // aktifliğine bağlıysa hiç bitmez: hamle düşmez, tur askıda kalır.
    //
    // Süper eşleşmede taşlar hayatta kalana farklı mesafelerden uçar ve farklı
    // karelerde havuza döner; ilk dönen taş sonuncusu dönmeden yeniden açılırsa
    // pencere yakalanır. Yeniden açma, taş kapandığı karede LateUpdate'te yapılır.
    [UnityTest]
    public IEnumerator SuperMatch_StillCompletesTurn_WhenClearedPotionIsReactivatedDuringResolution()
    {
        yield return SetUpBoard();

        // Alt satır: G G G G x, (4,1) G. (4,0)<->(4,1) takası beşli Green yapar;
        // hayatta kalan (4,0)'daki taş, diğerleri 1..4 hücre uzaktan ona uçar.
        SetType(PotionType.Green, new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 1));

        // En yakın taş (1 hücre) en önce havuza döner; en uzak (4 hücre) en son.
        Potion firstPooled = grid[3, 0].Potion;

        KeepActive keepActive = root.AddComponent<KeepActive>();
        keepActive.target = firstPooled.gameObject;

        Invoke("BeginSwap", grid[4, 0].Potion, grid[4, 1].Potion);

        float deadline = Time.time + 6f;

        while (Time.time < deadline && movesText.text != "4")
        {
            yield return null;
        }

        Assert.That(keepActive.reactivated, Is.True, "Senaryo çalışmadı: taş hiç havuza dönmedi.");
        Assert.That(movesText.text, Is.EqualTo("4"), "Tur tamamlanmadı; hamle düşmedi.");
    }

    // Havuza dönen taşı, eşzamanlı bir refill'in yapacağı gibi aynı karede
    // yeniden açar. LateUpdate coroutine fazından sonra koşar; böylece bir
    // sonraki karedeki WaitUntil taşı hep aktif görür.
    private class KeepActive : MonoBehaviour
    {
        public GameObject target;
        public bool reactivated;

        private void LateUpdate()
        {
            if (target == null || target.activeSelf) return;

            target.SetActive(true);
            reactivated = true;
        }
    }
}
#endif
