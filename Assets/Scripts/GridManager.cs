using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Ayarları")]
    public int gridRadius = 3;
    public float xOffset = 0.88f; 
    public float yOffset = 0.76f;

    [Header("Prefabler")]
    public GameObject hexBackgroundPrefab;

    // Arka plan zeminlerini hafızada tutuyoruz ki daralınca eskileri silebilelim
    private List<GameObject> backgroundHexes = new List<GameObject>();

    void Start()
    {
        // Rotasyonu ayarlıyoruz ama çizim işini artık GameManager'a bırakıyoruz
        transform.rotation = Quaternion.Euler(0, 0, -30f);
    }

    public Vector2 GetPixelCoords(int q, int r)
    {
        float x = xOffset * (q + r / 2f);
        float y = -(yOffset * r);
        return new Vector2(x, y);
    }

    // YENİ: Dışarıdan çağrıldığında mevcut zeminleri silip istenen çapa göre yeniden çizen fonksiyon
    public void RedrawGrid(int radius)
    {
        // Önceki zeminleri temizle
        foreach (var hex in backgroundHexes)
        {
            Destroy(hex);
        }
        backgroundHexes.Clear();

        // Yeni çapa göre zeminleri diz
        for (int q = -radius; q <= radius; q++)
        {
            for (int r = -radius; r <= radius; r++)
            {
                if (Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(-q - r)) <= radius)
                {
                    Vector2 localPos = GetPixelCoords(q, r);
                    
                    GameObject hex = Instantiate(hexBackgroundPrefab, this.transform);
                    hex.transform.localPosition = localPos; 
                    hex.transform.localRotation = Quaternion.Euler(0, 0, 30f); 
                    hex.name = $"Hex_{q}_{r}";
                    
                    backgroundHexes.Add(hex);
                }
            }
        }
    }
}