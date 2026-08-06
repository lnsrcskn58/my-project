using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Ayarları")]
    public int gridRadius = 3;
    public float xOffset = 0.88f; 
    public float yOffset = 0.76f;

    [Header("Prefabler")]
    public GameObject hexBackgroundPrefab;

    void Start()
    {
        DrawGrid();
        
        // DÜZELTME 1: CSS'deki saat yönü rotasyonunu Unity'de elde etmek için eksi (-) kullanıyoruz.
        transform.rotation = Quaternion.Euler(0, 0, -30f);
    }

    public Vector2 GetPixelCoords(int q, int r)
    {
        float x = xOffset * (q + r / 2f);
        float y = -(yOffset * r);
        return new Vector2(x, y);
    }

    void DrawGrid()
    {
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(-q - r)) <= gridRadius)
                {
                    Vector2 localPos = GetPixelCoords(q, r);
                    
                    // Altıgenleri oluştururken GridManager'ın içine atıyoruz
                    GameObject hex = Instantiate(hexBackgroundPrefab, this.transform);
                    hex.transform.localPosition = localPos; // World position değil, Local position!
                    
                    // DÜZELTME 2: Grid -30 dönünce görsellerin dik durması için +30 veriyoruz
                    hex.transform.localRotation = Quaternion.Euler(0, 0, 30f); 
                    hex.name = $"Hex_{q}_{r}";
                }
            }
        }
    }
}