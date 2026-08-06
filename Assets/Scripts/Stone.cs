using UnityEngine;
using TMPro; // Bomba yazısı için gerekli kütüphane

public class Stone : MonoBehaviour
{
    [Header("Grid Koordinatları")]
    public int q;
    public int r;
    
    [Header("Taş Özellikleri")]
    public char type; // 'T', 'K', 'M'
    public bool isDead = false;
    public bool isHeavy = false;
    public int bombTimer = -1; // -1 ise bomba yok demektir

    [Header("Görsel Referanslar")]
    public SpriteRenderer spriteRenderer;
    public Sprite rockSprite;
    public Sprite paperSprite;
    public Sprite scissorsSprite;
    
    [Header("Modifikatör Görselleri")]
    public GameObject heavyIcon; // Ağırlık işareti (Örn: Çapa ikonu)
    public TextMeshPro bombText; // Bomba geri sayım metni

    public void Initialize(int startQ, int startR, char stoneType, bool heavy = false, int bomb = -1)
    {
        q = startQ;
        r = startR;
        type = stoneType;
        isHeavy = heavy;
        bombTimer = bomb;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        switch (type)
        {
            case 'T': spriteRenderer.sprite = rockSprite; break;
            case 'K': spriteRenderer.sprite = paperSprite; break;
            case 'M': spriteRenderer.sprite = scissorsSprite; break;
        }
        
        UpdateVisualModifiers();

        // Taş görsellerinin dik durması için
        transform.localRotation = Quaternion.Euler(0, 0, 30f);
    }

    // Bomba sayacını ve ağırlık ikonunu güncelleyen metod
    public void UpdateVisualModifiers()
    {
        if (heavyIcon != null) 
            heavyIcon.SetActive(isHeavy);
            
        if (bombText != null)
        {
            if (bombTimer > 0)
            {
                bombText.gameObject.SetActive(true);
                bombText.text = bombTimer.ToString();
            }
            else
            {
                bombText.gameObject.SetActive(false);
            }
        }
    }
}