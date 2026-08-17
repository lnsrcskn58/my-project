using UnityEngine;
using UnityEngine.UI; // Image bileşenine ulaşmak için bu şart!

public class HexagonButton : MonoBehaviour
{
    void Start()
    {
        // Butonun saydamlık (alpha) eşiğini ayarlıyoruz. 
        // 0.1f demek: "Saydamlığı %10'dan az olan yerlere (köşelerdeki boşluklara) tıklamayı yoksay" demektir.
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
    }
}