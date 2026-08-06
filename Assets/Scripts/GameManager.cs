using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct OneWayEdge
{
    public HexCoord from;
    public HexCoord to;

    public OneWayEdge(HexCoord from, HexCoord to)
    {
        this.from = from;
        this.to = to;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Referanslar")]
    public GridManager gridManager;
    public GameObject stonePrefab;
    public GameObject wallPrefab;
    public GameObject stickyPrefab;
    public GameObject columnPrefab;
    public GameObject oneWayPrefab; // TEK YÖNLÜ DUVAR PREFABİ
    
    [Header("Oyun Durumu")]
    public List<Stone> activeStones = new List<Stone>();

    [Header("Bölüm Özellikleri (Zeminler)")]
    public List<HexCoord> walls = new List<HexCoord>();
    public List<HexCoord> stickyTiles = new List<HexCoord>();
    public List<HexCoord> columns = new List<HexCoord>();
    public List<HexCoord> dynamicWalls = new List<HexCoord>();
    public List<OneWayEdge> oneWayEdges = new List<OneWayEdge>(); // TEK YÖNLÜ DUVAR LİSTESİ

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // Test Zeminleri
        stickyTiles.Add(new HexCoord(0, 1)); 
        walls.Add(new HexCoord(1, -1));      
        columns.Add(new HexCoord(-1, 0));    
        
        // Tek Yönlü Duvar Testi (0,0'dan 0,-1'e geçiş serbest, geri dönüş yasak)
        oneWayEdges.Add(new OneWayEdge(new HexCoord(0, 0), new HexCoord(0, -1)));

        SpawnVisuals();
        SpawnTestStones();
    }

    void SpawnVisuals()
    {
        foreach (var w in walls) SpawnTile(w, wallPrefab);
        foreach (var s in stickyTiles) SpawnTile(s, stickyPrefab);
        foreach (var c in columns) SpawnTile(c, columnPrefab);
        
        // Tek Yönlü Duvarları Çizdir
        foreach (var edge in oneWayEdges) SpawnOneWayVisual(edge);
    }

    void SpawnTile(HexCoord coord, GameObject prefab)
    {
        if (prefab == null) return;
        Vector2 localPos = gridManager.GetPixelCoords(coord.q, coord.r);
        
        GameObject tile = Instantiate(prefab, gridManager.transform);
        tile.transform.localPosition = localPos;
        tile.transform.localRotation = Quaternion.Euler(0, 0, 30f); 
    }

    void SpawnOneWayVisual(OneWayEdge edge)
    {
        if (oneWayPrefab == null) return;

        Vector2 p1 = gridManager.GetPixelCoords(edge.from.q, edge.from.r);
        Vector2 p2 = gridManager.GetPixelCoords(edge.to.q, edge.to.r);
        
        // Duvarı iki altıgenin tam ortasına yerleştir
        Vector2 midPoint = (p1 + p2) / 2f;
        
        // Okun yönünü hesapla
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        // ÇÖZÜM: Görselin yan durmasını düzeltmek için açıya müdahale ediyoruz.
        // Eğer hala ters veya yan duruyorsa bu değeri +90f, -90f veya 180f yaparak tam oturtabilirsin.
        float angleOffset = -90f; 

        GameObject arrow = Instantiate(oneWayPrefab, gridManager.transform);
        arrow.transform.localPosition = midPoint;
        arrow.transform.localRotation = Quaternion.Euler(0, 0, angle + angleOffset);
    }

    void SpawnTestStones()
    {
        SpawnStone(0, 0, 'T', false, 3);
        SpawnStone(1, -2, 'K', true, -1);
        SpawnStone(-2, 1, 'M', false, -1);
    }

    void SpawnStone(int q, int r, char type, bool isHeavy, int bombTimer)
    {
        Vector2 localPos = gridManager.GetPixelCoords(q, r);
        GameObject stoneObj = Instantiate(stonePrefab, gridManager.transform);
        stoneObj.transform.localPosition = localPos;
        
        Stone stone = stoneObj.GetComponent<Stone>();
        stone.Initialize(q, r, type, isHeavy, bombTimer);
        activeStones.Add(stone);
    }

    public void ProcessMove(int dq, int dr)
    {
        bool movedAnything = false;
        activeStones.Sort((a, b) => (b.q * dq + b.r * dr).CompareTo(a.q * dq + a.r * dr));

        foreach (Stone stone in activeStones)
        {
            if (stone.isDead) continue;

            int currentQ = stone.q;
            int currentR = stone.r;

            while (true)
            {
                int nextQ = currentQ + dq;
                int nextR = currentR + dr;
                HexCoord currentCoord = new HexCoord(currentQ, currentR);
                HexCoord nextCoord = new HexCoord(nextQ, nextR);

                // 1. Izgara sınırı
                if (Mathf.Max(Mathf.Abs(nextQ), Mathf.Abs(nextR), Mathf.Abs(-nextQ - nextR)) > gridManager.gridRadius) break;
                
                // 2. Normal Duvarlar
                if (walls.Contains(nextCoord) || dynamicWalls.Contains(nextCoord)) break;

                // 3. TEK YÖNLÜ DUVAR KONTROLÜ
                if (IsEdgeBlocked(currentCoord, nextCoord)) break;

                bool isSticky = stickyTiles.Contains(nextCoord);
                bool isColumn = columns.Contains(nextCoord);
                Stone targetStone = activeStones.FirstOrDefault(s => s.q == nextQ && s.r == nextR && !s.isDead);
                
                if (targetStone != null)
                {
                    if (Beats(stone.type, targetStone.type))
                    {
                        targetStone.isDead = true;
                        targetStone.gameObject.SetActive(false);
                        
                        currentQ = nextQ;
                        currentR = nextR;
                        movedAnything = true;
                        
                        if (isColumn) HandleColumnDestroyed(nextCoord);
                        if (isSticky || stone.isHeavy) break;
                    }
                    else
                    {
                        break; 
                    }
                }
                else
                {
                    currentQ = nextQ;
                    currentR = nextR;
                    movedAnything = true;
                    
                    if (isColumn) HandleColumnDestroyed(nextCoord);
                    if (isSticky || stone.isHeavy) break;
                }
            }

            if (currentQ != stone.q || currentR != stone.r)
            {
                stone.q = currentQ;
                stone.r = currentR;
                Vector2 targetLocalPos = gridManager.GetPixelCoords(stone.q, stone.r);
                StartCoroutine(SlideStone(stone, targetLocalPos));
            }
        }

        if (movedAnything) ProcessBombTimers();
    }

    // Tek yönlü duvarın geçişi engelleyip engellemediğini test eder
    bool IsEdgeBlocked(HexCoord from, HexCoord to)
    {
        foreach (var edge in oneWayEdges)
        {
            // Eğer iki hücre arasında bir kenar bağlantısı (duvar) varsa
            if ((edge.from == from && edge.to == to) || (edge.from == to && edge.to == from))
            {
                // Eğer hareketimiz "izin verilen yönde (edge.from -> edge.to)" DEĞİLSE, blokla.
                if (!(from == edge.from && to == edge.to))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void HandleColumnDestroyed(HexCoord coord)
    {
        columns.Remove(coord);
        dynamicWalls.Add(coord);
    }

    void ProcessBombTimers()
    {
        foreach (Stone stone in activeStones)
        {
            if (!stone.isDead && stone.bombTimer > 0)
            {
                stone.bombTimer--;
                stone.UpdateVisualModifiers();
                if (stone.bombTimer == 0) Debug.Log("💥 BOMBA PATLADI! GÖREV BAŞARISIZ 💥");
            }
        }
    }

    bool Beats(char a, char b)
    {
        return (a == 'T' && b == 'M') || (a == 'M' && b == 'K') || (a == 'K' && b == 'T');
    }

    IEnumerator SlideStone(Stone stone, Vector2 targetLocalPos)
    {
        float elapsed = 0f;
        float duration = 0.15f;
        Vector2 startPos = stone.transform.localPosition;

        while (elapsed < duration)
        {
            stone.transform.localPosition = Vector2.Lerp(startPos, targetLocalPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        stone.transform.localPosition = targetLocalPos;
    }
}