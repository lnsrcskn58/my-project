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
    public GameObject mainMenuPanel;    // YENİ: Ana Menü
    public GameObject levelSelectPanel; // YENİ: Bölüm Seçim Ekranı
    public GameObject gameUIPanel;      // YENİ: Oyun İçi HUD (TopBar)
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI movesText;
    public GameObject winPanel;
    public GameObject losePanel;
    
    [Header("Oyun Durumu & Kayıt Sistemi")]
    public int currentLevelId = 1;
    public int unlockedLevel = 1;       // YENİ: Kilidi açılmış en son bölüm
    public bool isGameOver = false; 
    public List<Stone> activeStones = new List<Stone>();
    private List<GameObject> spawnedVisuals = new List<GameObject>();
    private Stack<GameStateSnapshot> undoStack = new Stack<GameStateSnapshot>();

    [Header("Oyun Kuralları")]
    public int maxMoves = 5;
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
        // Kayıtlı ilerlemeyi yükle (Kayıt yoksa 1. bölümden başlar)
        unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);
        
        // Oyun ilk açıldığında Ana Menüyü göster
        ShowMainMenu();
    }

    void Update()
    {
        // Oyun bitmediyse ve ana menüde değilsek geri al tuşu çalışsın
        if (!isGameOver && !mainMenuPanel.activeSelf && Input.GetKeyDown(KeyCode.U))
        {
            UndoMove();
        }
    }

    // --- MENÜ SİSTEMİ FONKSİYONLARI ---

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
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }

    public void StartGameFromMenu()
    {
        // En son kilidi açılan (kaldığı) bölümden başlat
        LoadSpecificLevel(unlockedLevel);
    }

    public void LoadSpecificLevel(int levelId)
    {
        // Kilitli bir bölüme girmeye çalışırsa engelle
        if (levelId > unlockedLevel) 
        {
            Debug.Log("Bu bölüm henüz kilitli!");
            return; 
        }
        
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

        TextAsset jsonText = Resources.Load<TextAsset>("levels");
        if (jsonText == null) return;

        LevelList allLevels = JsonUtility.FromJson<LevelList>(jsonText.text);
        LevelItem levelToLoad = allLevels.levels.FirstOrDefault(l => l.id == levelId);
        
        if (levelToLoad == null) return;

        LevelDetails data = levelToLoad.data;

        maxMoves = data.maxMoves;
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
            movesText.text = $"Hamle: {movesMade} / {maxMoves}";
            movesText.color = (maxMoves - movesMade <= 1) ? Color.red : Color.white;
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
    public void NextLevel() { currentLevelId++; LoadLevel(currentLevelId); }

    public void ProcessMove(int dq, int dr)
    {
        if (isGameOver) return; 

        SaveState(); 
        bool movedAnything = false;
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
                        currentQ = nextQ; currentR = nextR; movedAnything = true;
                        if (isColumn) HandleColumnDestroyed(nextCoord);
                        if (isSticky || stone.isHeavy) break;
                    }
                    else break; 
                }
                else
                {
                    currentQ = nextQ; currentR = nextR; movedAnything = true;
                    if (isColumn) HandleColumnDestroyed(nextCoord);
                    if (isSticky || stone.isHeavy) break;
                }
            }

            if (currentQ != stone.q || currentR != stone.r)
            {
                stone.q = currentQ; stone.r = currentR;
                Vector2 targetLocalPos = gridManager.GetPixelCoords(stone.q, stone.r);
                StartCoroutine(SlideStone(stone, targetLocalPos));
            }
        }
        
        movesMade++;
        UpdateMovesUI(); 
        if (movedAnything) ProcessBombTimers();
        CheckGameState();
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
    
    // YENİ: İlerlemeyi kaydeden metot
    void SaveProgress()
    {
        if (currentLevelId >= unlockedLevel)
        {
            unlockedLevel = currentLevelId + 1;
            PlayerPrefs.SetInt("UnlockedLevel", unlockedLevel);
            PlayerPrefs.Save();
        }
    }

    public void CheckGameState()
    {
        if (isGameOver) return; 

        var aliveStones = activeStones.Where(s => !s.isDead).ToList();
        
        if (aliveStones.Count == 1)
        {
            isGameOver = true;
            if (hasThrone)
            {
                if (aliveStones[0].q == targetThrone.q && aliveStones[0].r == targetThrone.r)
                {
                    SaveProgress(); // Bölüm geçildi, kaydet!
                    if (winPanel != null) winPanel.SetActive(true);
                }
                else
                {
                    if (losePanel != null) losePanel.SetActive(true);
                }
            }
            else
            {
                SaveProgress(); // Bölüm geçildi, kaydet!
                if (winPanel != null) winPanel.SetActive(true);
            }
            return;
        }

        if (movesMade >= maxMoves)
        {
            isGameOver = true;
            if (losePanel != null) losePanel.SetActive(true);
        }
    }
}