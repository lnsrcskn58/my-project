using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro; 

[System.Serializable]
public struct OneWayEdge
{
    public HexCoord from;
    public HexCoord to;
    public OneWayEdge(HexCoord from, HexCoord to) { this.from = from; this.to = to; }
}

[System.Serializable]
public struct StoneSnapshot
{
    public int q, r;
    public char type;
    public bool isHeavy;
    public int bombTimer;
    public bool isDead;
}

public class GameStateSnapshot
{
    public int movesMade;
    public List<StoneSnapshot> stones;
    public List<HexCoord> columns;
    public List<HexCoord> dynamicWalls;
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
    public GameObject oneWayPrefab;
    
    [Header("UI (Arayüz) Referansları")]
    public GameObject mainMenuPanel;    
    public GameObject levelSelectPanel; 
    public GameObject gameUIPanel;      
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI movesText;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Dinamik Bölüm Seçimi")]
    public GameObject levelButtonPrefab;   // YENİ: Otomatik üretilecek buton şablonu
    public Transform levelButtonContainer; // YENİ: Butonların dizileceği kutu
    private List<LevelItem> allLevelItems; // JSON'daki tüm bölümlerin listesi
    
    [Header("Oyun Durumu & Kayıt Sistemi")]
    public int currentLevelId = 1;
    public int unlockedLevel = 1;       
    public bool isGameOver = false; 
    public List<Stone> activeStones = new List<Stone>();
    private List<GameObject> spawnedVisuals = new List<GameObject>();
    private Stack<GameStateSnapshot> undoStack = new Stack<GameStateSnapshot>();

    [Header("Oyun Kuralları & Yıldız Sistemi")]
    public int targetMoves = 5; // 3 yıldız almak için gereken hamle sayısı
    public int hardLimitMoves = 7; // Oyunun kaybedileceği asıl sınır
    public int movesMade = 0;
    public bool hasThrone = false;
    public HexCoord targetThrone;

    [Header("Bölüm Özellikleri (Zeminler)")]
    public List<HexCoord> walls = new List<HexCoord>();
    public List<HexCoord> stickyTiles = new List<HexCoord>();
    public List<HexCoord> columns = new List<HexCoord>();
    public List<HexCoord> dynamicWalls = new List<HexCoord>();
    public List<OneWayEdge> oneWayEdges = new List<OneWayEdge>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);
        
        // JSON'u bir kez oku ve hafızada tut
        TextAsset jsonText = Resources.Load<TextAsset>("levels");
        if (jsonText != null)
        {
            LevelList allLevels = JsonUtility.FromJson<LevelList>(jsonText.text);
            allLevelItems = allLevels.levels;
        }

        GenerateLevelButtons(); // Butonları otomatik oluştur
        ShowMainMenu();
    }

    void Update()
    {
        if (!isGameOver && !mainMenuPanel.activeSelf && Input.GetKeyDown(KeyCode.U)) UndoMove();
    }

    // --- MENÜ VE DİNAMİK BUTON SİSTEMİ ---

    public void GenerateLevelButtons()
    {
        if (allLevelItems == null || levelButtonPrefab == null || levelButtonContainer == null) return;

        // Önce konteyner içindeki eski butonları temizle (tekrar güncellendiğinde üst üste binmesin)
        foreach (Transform child in levelButtonContainer) Destroy(child.gameObject);

        foreach (var level in allLevelItems)
        {
            int lvlId = level.id;
            GameObject btnObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            
            // Prefab içindeki 0. Text seviye numarasını, 1. Text yıldızları tutacak
            TextMeshProUGUI[] texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = lvlId.ToString();
            
            // Yıldızları PlayerPrefs'ten çek ve emojilerle göster
            int stars = PlayerPrefs.GetInt("LevelStars_" + lvlId, 0);
            if (texts.Length > 1) 
            {
                if (lvlId > unlockedLevel) texts[1].text = "🔒"; // Kilitli
                else if (stars == 3) texts[1].text = "⭐⭐⭐";
                else if (stars == 2) texts[1].text = "⭐⭐";
                else if (stars == 1) texts[1].text = "⭐";
                else texts[1].text = "Oynanmadı";
            }

            UnityEngine.UI.Button btn = btnObj.GetComponent<UnityEngine.UI.Button>();
            if (lvlId > unlockedLevel) 
            {
                btn.interactable = false; // Kilitliyse tıklanamaz olsun
            }
            else
            {
                btn.interactable = true;
                // Lambda Expression ile butona kendi ID'sini aktarıyoruz
                btn.onClick.AddListener(() => LoadSpecificLevel(lvlId)); 
            }
        }
    }

    public void ShowMainMenu()
    {
        ClearBoard();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void ShowLevelSelect()
    {
        GenerateLevelButtons(); // Girmeden önce listeyi güncelle (Kazanılan yıldızlar görünsün)
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }

    public void StartGameFromMenu() { LoadSpecificLevel(unlockedLevel); }

    public void LoadSpecificLevel(int levelId)
    {
        if (levelId > unlockedLevel) return; 
        currentLevelId = levelId;
        
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (gameUIPanel != null) gameUIPanel.SetActive(true);
        
        LoadLevel(currentLevelId);
    }

    // ---------------------------------

    public void LoadLevel(int levelId)
    {
        ClearBoard();

        if (allLevelItems == null) return;
        LevelItem levelToLoad = allLevelItems.FirstOrDefault(l => l.id == levelId);
        if (levelToLoad == null) return;

        LevelDetails data = levelToLoad.data;

        // YILDIZ KURALLARI GÜNCELLEMESİ
        targetMoves = data.maxMoves; // 3 yıldız hedefi
        hardLimitMoves = targetMoves + 2; // Oyunu kaybetme sınırı
        movesMade = 0;
        
        hasThrone = false; 
        isGameOver = false; 

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (levelText != null) levelText.text = $"Bölüm {levelId}";
        UpdateMovesUI();

        if (data.walls != null) foreach (var w in data.walls) walls.Add(new HexCoord(w.q, w.r));
        if (data.sticky != null) foreach (var s in data.sticky) stickyTiles.Add(new HexCoord(s.q, s.r));
        if (data.column != null) foreach (var c in data.column) columns.Add(new HexCoord(c.q, c.r));
        if (data.oneWayEdges != null)
        {
            foreach (var edge in data.oneWayEdges)
                oneWayEdges.Add(new OneWayEdge(new HexCoord(edge.q1, edge.r1), new HexCoord(edge.q2, edge.r2)));
        }

        DrawBoardVisuals();

        if (data.stones != null)
            foreach (var s in data.stones) SpawnStone(s.q, s.r, s.type[0], s.heavy, s.bomb);
    }

    void UpdateMovesUI()
    {
        if (movesText != null)
        {
            // Ekranda kalan hakkı "Hamle: Yaptığın / Maksimum Sınır" şeklinde gösterelim
            movesText.text = $"Hamle: {movesMade} / {hardLimitMoves}";
            movesText.color = (hardLimitMoves - movesMade <= 1) ? Color.red : Color.white;
        }
    }

    void DrawBoardVisuals()
    {
        foreach (var w in walls) SpawnTile(w, wallPrefab);
        foreach (var dw in dynamicWalls) SpawnTile(dw, wallPrefab); 
        foreach (var s in stickyTiles) SpawnTile(s, stickyPrefab);
        foreach (var c in columns) SpawnTile(c, columnPrefab);
        foreach (var edge in oneWayEdges) SpawnOneWayVisual(edge);
    }

    public void ClearBoard()
    {
        foreach (var stone in activeStones) Destroy(stone.gameObject);
        activeStones.Clear();
        foreach (var visual in spawnedVisuals) Destroy(visual);
        spawnedVisuals.Clear();

        walls.Clear(); stickyTiles.Clear(); columns.Clear(); dynamicWalls.Clear(); oneWayEdges.Clear();
        undoStack.Clear(); 
    }

    void SpawnTile(HexCoord coord, GameObject prefab)
    {
        if (prefab == null) return;
        Vector2 localPos = gridManager.GetPixelCoords(coord.q, coord.r);
        GameObject tile = Instantiate(prefab, gridManager.transform);
        tile.transform.localPosition = localPos;
        tile.transform.localRotation = Quaternion.Euler(0, 0, 30f);
        spawnedVisuals.Add(tile);
    }

    void SpawnOneWayVisual(OneWayEdge edge)
    {
        if (oneWayPrefab == null) return;
        Vector2 p1 = gridManager.GetPixelCoords(edge.from.q, edge.from.r);
        Vector2 p2 = gridManager.GetPixelCoords(edge.to.q, edge.to.r);
        Vector2 midPoint = (p1 + p2) / 2f;
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        GameObject arrow = Instantiate(oneWayPrefab, gridManager.transform);
        arrow.transform.localPosition = midPoint;
        arrow.transform.localRotation = Quaternion.Euler(0, 0, angle - 90f);
        spawnedVisuals.Add(arrow);
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

    public void SaveState()
    {
        GameStateSnapshot snap = new GameStateSnapshot();
        snap.movesMade = movesMade;
        snap.columns = new List<HexCoord>(columns);
        snap.dynamicWalls = new List<HexCoord>(dynamicWalls);
        
        snap.stones = new List<StoneSnapshot>();
        foreach (var s in activeStones)
        {
            snap.stones.Add(new StoneSnapshot {
                q = s.q, r = s.r, type = s.type, isHeavy = s.isHeavy, bombTimer = s.bombTimer, isDead = s.isDead
            });
        }
        undoStack.Push(snap);
    }

    public void UndoMove()
    {
        if (undoStack.Count == 0 || isGameOver) return;
        GameStateSnapshot snap = undoStack.Pop();

        foreach (var stone in activeStones) Destroy(stone.gameObject);
        activeStones.Clear();
        foreach (var visual in spawnedVisuals) Destroy(visual);
        spawnedVisuals.Clear();

        movesMade = snap.movesMade;
        columns = new List<HexCoord>(snap.columns);
        dynamicWalls = new List<HexCoord>(snap.dynamicWalls);

        DrawBoardVisuals();
        foreach (var sSnap in snap.stones)
        {
            if (!sSnap.isDead) SpawnStone(sSnap.q, sSnap.r, sSnap.type, sSnap.isHeavy, sSnap.bombTimer);
        }
        
        UpdateMovesUI();
    }

    public void RestartLevel() { LoadLevel(currentLevelId); }
    
    // YENİ: Sonraki bölüme geçerken UI'ı resetliyoruz
    public void NextLevel() { currentLevelId++; LoadLevel(currentLevelId); }

    public void ProcessMove(int dq, int dr)
    {
        if (isGameOver) return; 

        SaveState(); // O anki durumu hafızaya alıyoruz
        bool movedAnything = false; // Hareket kontrolcüsü
        
        activeStones.Sort((a, b) => (b.q * dq + b.r * dr).CompareTo(a.q * dq + a.r * dr));

        foreach (Stone stone in activeStones)
        {
            if (stone.isDead) continue;
            int currentQ = stone.q; int currentR = stone.r;

            while (true)
            {
                int nextQ = currentQ + dq; int nextR = currentR + dr;
                HexCoord currentCoord = new HexCoord(currentQ, currentR);
                HexCoord nextCoord = new HexCoord(nextQ, nextR);

                if (Mathf.Max(Mathf.Abs(nextQ), Mathf.Abs(nextR), Mathf.Abs(-nextQ - nextR)) > gridManager.gridRadius) break;
                if (walls.Contains(nextCoord) || dynamicWalls.Contains(nextCoord)) break;
                if (IsEdgeBlocked(currentCoord, nextCoord)) break;

                bool isSticky = stickyTiles.Contains(nextCoord);
                bool isColumn = columns.Contains(nextCoord);
                Stone targetStone = activeStones.FirstOrDefault(s => s.q == nextQ && s.r == nextR && !s.isDead);
                
                if (targetStone != null)
                {
                    if (Beats(stone.type, targetStone.type))
                    {
                        targetStone.isDead = true; targetStone.gameObject.SetActive(false);
                        currentQ = nextQ; currentR = nextR; 
                        if (isColumn) HandleColumnDestroyed(nextCoord);
                        if (isSticky || stone.isHeavy) break;
                    }
                    else break; 
                }
                else
                {
                    currentQ = nextQ; currentR = nextR; 
                    if (isColumn) HandleColumnDestroyed(nextCoord);
                    if (isSticky || stone.isHeavy) break;
                }
            }

            // EĞER TAŞIN YERİ DEĞİŞTİYSE GERÇEKTEN HAREKET ETMİŞTİR
            if (currentQ != stone.q || currentR != stone.r)
            {
                stone.q = currentQ; stone.r = currentR;
                Vector2 targetLocalPos = gridManager.GetPixelCoords(stone.q, stone.r);
                StartCoroutine(SlideStone(stone, targetLocalPos));
                movedAnything = true; // En az 1 taş hareket etti!
            }
        }
        
        // --- ASIL DÜZELTME BURADA ---
        if (movedAnything)
        {
            // Sadece bir hareket olduysa hamle say, bombaları düşür ve durumu kontrol et
            movesMade++;
            UpdateMovesUI(); 
            ProcessBombTimers();
            CheckGameState();
        }
        else
        {
            // Eğer hiçbir taş kımıldamadıysa, "boşuna" kaydettiğimiz durumu hafızadan geri siliyoruz.
            // Böylece Geri Al tuşu bozulmamış oluyor.
            if (undoStack.Count > 0) undoStack.Pop();
        }
    }

    bool IsEdgeBlocked(HexCoord from, HexCoord to)
    {
        foreach (var edge in oneWayEdges)
            if ((edge.from == from && edge.to == to) || (edge.from == to && edge.to == from))
                if (!(from == edge.from && to == edge.to)) return true;
        return false;
    }

    void HandleColumnDestroyed(HexCoord coord) { columns.Remove(coord); dynamicWalls.Add(coord); SpawnTile(coord, wallPrefab); }

    void ProcessBombTimers()
    {
        foreach (Stone stone in activeStones)
        {
            if (!stone.isDead && stone.bombTimer > 0)
            {
                stone.bombTimer--; stone.UpdateVisualModifiers();
                if (stone.bombTimer == 0)
                {
                    isGameOver = true;
                    if (losePanel != null) losePanel.SetActive(true);
                }
            }
        }
    }

    bool Beats(char a, char b) { return (a == 'T' && b == 'M') || (a == 'M' && b == 'K') || (a == 'K' && b == 'T'); }

    IEnumerator SlideStone(Stone stone, Vector2 targetLocalPos)
    {
        float elapsed = 0f; float duration = 0.15f;
        Vector2 startPos = stone.transform.localPosition;
        while (elapsed < duration)
        {
            stone.transform.localPosition = Vector2.Lerp(startPos, targetLocalPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        stone.transform.localPosition = targetLocalPos;
    }

    // --- OYUN KAZANMA VE YILDIZ HESAPLAMA SİSTEMİ ---
    void LevelCompleted()
    {
        isGameOver = true;
        
        // Yıldız Hesaplama (İstediğin kurala göre)
        int earnedStars = 0;
        if (movesMade <= targetMoves) earnedStars = 3;
        else if (movesMade == targetMoves + 1) earnedStars = 2;
        else if (movesMade >= targetMoves + 2) earnedStars = 1;

        // Verileri Kaydet
        SaveProgress(earnedStars);

        // Paneli Aç
        if (winPanel != null) 
        {
            winPanel.SetActive(true);
            
            // İstersen WinPanel içine bir Text daha açıp yıldızları yazdırabilirsin:
            // "⭐" "⭐⭐" vb. Bu kısmı UI tasarlarsan kodda ekleyebiliriz.
        }
    }

    void SaveProgress(int starsEarned)
    {
        // En yüksek yıldızı kaydet
        int currentRecord = PlayerPrefs.GetInt("LevelStars_" + currentLevelId, 0);
        if (starsEarned > currentRecord)
        {
            PlayerPrefs.SetInt("LevelStars_" + currentLevelId, starsEarned);
        }

        // Kilit açma
        if (currentLevelId >= unlockedLevel)
        {
            unlockedLevel = currentLevelId + 1;
            PlayerPrefs.SetInt("UnlockedLevel", unlockedLevel);
        }
        
        PlayerPrefs.Save();
        GenerateLevelButtons(); // Arkaplanda buton listesini güncelle
    }

    public void CheckGameState()
    {
        if (isGameOver) return; 

        var aliveStones = activeStones.Where(s => !s.isDead).ToList();
        
        if (aliveStones.Count == 1)
        {
            if (hasThrone)
            {
                if (aliveStones[0].q == targetThrone.q && aliveStones[0].r == targetThrone.r) LevelCompleted();
                else if (losePanel != null) { isGameOver = true; losePanel.SetActive(true); }
            }
            else LevelCompleted();
            return;
        }

        // Kaybetme Sınırı Kontrolü (Hard Limit)
        if (movesMade >= hardLimitMoves)
        {
            isGameOver = true;
            if (losePanel != null) losePanel.SetActive(true);
        }
    }
}