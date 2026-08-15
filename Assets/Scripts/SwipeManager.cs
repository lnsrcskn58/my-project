using UnityEngine;
using UnityEngine.EventSystems; // YENİ: UI (Arayüz) tıklamalarını algılamak için gerekli

public class SwipeManager : MonoBehaviour
{
    private Vector2 startTouchPosition;
    private Vector2 endTouchPosition;
    
    [Tooltip("Kaydırmanın algılanması için gereken minimum piksel mesafesi")]
    public float swipeThreshold = 50f; 

    void Update()
    {
        // 1. KORUMA: Eğer oyuncu bir UI elemanına (Buton, ScrollView, Panel) dokunuyorsa işlemi iptal et
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Mobil dokunmatik için ekstra UI koruması
        if (Input.touchCount > 0 && EventSystem.current != null)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                return;
        }

        // 1. Bilgisayar (Mouse) Girdisi
        if (Input.GetMouseButtonDown(0))
        {
            startTouchPosition = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0))
        {
            endTouchPosition = Input.mousePosition;
            DetectSwipe();
        }

        // 2. Mobil (Dokunmatik) Girdisi
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                endTouchPosition = touch.position;
                DetectSwipe();
            }
        }
    }

    void DetectSwipe()
    {
        // 2. KORUMA: GameManager sahnede yoksa çökmesini engelle
        if (GameManager.Instance == null) return;

        // 3. KORUMA: Eğer Ana Menüde veya Bölüm Seçim ekranındaysak kaydırmayı engelle
        if (GameManager.Instance.mainMenuPanel.activeSelf || GameManager.Instance.levelSelectPanel.activeSelf) 
            return;

        Vector2 swipeDelta = endTouchPosition - startTouchPosition;

        if (swipeDelta.magnitude > swipeThreshold)
        {
            // Unity'de Y ekseni yukarı doğru pozitiftir.
            float angle = Mathf.Atan2(swipeDelta.y, swipeDelta.x) * Mathf.Rad2Deg;
            
            int dq = 0, dr = 0;

            // Unity'nin eksenlerine ve 30 derece döndürülmüş grid'imize göre doğru yön haritası
            if (angle > 60 && angle <= 120)        { dq = 0; dr = -1; }  // Yukarı
            else if (angle > 0 && angle <= 60)     { dq = 1; dr = -1; }  // Sağ Üst
            else if (angle > -60 && angle <= 0)    { dq = 1; dr = 0; }   // Sağ Alt
            else if (angle > -120 && angle <= -60) { dq = 0; dr = 1; }   // Aşağı
            else if (angle > -180 && angle <= -120){ dq = -1; dr = 1; }  // Sol Alt
            else                                   { dq = -1; dr = 0; }  // Sol Üst (angle > 120 veya <= -180)

            GameManager.Instance.ProcessMove(dq, dr);
        }
    }
}