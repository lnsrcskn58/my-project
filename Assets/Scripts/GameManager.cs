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
    public int score;
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

    [Header("Genel Menü Referansları")]
    public GameObject mainMenuPanel;
    public GameObject levelSelectPanel;
    public GameObject winPanel;
    public GameObject losePanel;
    public TextMeshProUGUI loseReasonText;

    [Header("Level Modu UI Referansları")]
    public GameObject levelModeUIPanel;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI levelMovesText;

    [Header("Sonsuz Mod UI Referansları")]
    public GameObject endlessModeUIPanel;
    public TextMeshProUGUI endlessRoundText;
    public TextMeshProUGUI endlessMovesText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI comboText;

    [Header("Dinamik Bölüm Seçimi")]
    public GameObject levelButtonPrefab;
    public Transform levelButtonContainer;
    private List<LevelItem> allLevelItems;

    [Header("Oyun Durumu & Kayıt Sistemi")]
    public int currentLevelId = 1;
    public int unlockedLevel = 1;
    public bool isGameOver = false;
    public int currentPlayRadius; 
    public List<Stone> activeStones = new List<Stone>();
    private List<GameObject> spawnedVisuals = new List<GameObject>();
    private Stack<GameStateSnapshot> undoStack = new Stack<GameStateSnapshot>();

    [Header("Oyun Kuralları & Yıldız Sistemi")]
    public int targetMoves = 5;
    public int hardLimitMoves = 7;
    public int movesMade = 0;
    public bool hasThrone = false;
    public HexCoord targetThrone;

    [Header("Bölüm Özellikleri (Zeminler)")]
    public List<HexCoord> walls = new List<HexCoord>();
    public List<HexCoord> stickyTiles = new List<HexCoord>();
    public List<HexCoord> columns = new List<HexCoord>();
    public List<HexCoord> dynamicWalls = new List<HexCoord>();
    public List<OneWayEdge> oneWayEdges = new List<OneWayEdge>();

    [Header("Sonsuz Mod Durumu")]
    public bool isEndlessMode = false;
    public int score = 0;
    public int comboCount = 0;
    public int endlessLevelCleared = 0;
    public float timeLeft = 0f;
    private bool isTimerRunning = false;
    private bool isGenerating = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // NORMAL KAYIT SİSTEMİ (Şimdilik yorum satırı yaptık, başına // koyduk)
        // unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);
        
        // YENİ TEST SATIRI: Oyunun kilidini çok yüksek bir sayıya sabitliyoruz
        unlockedLevel = 999; 

        TextAsset jsonText = Resources.Load<TextAsset>("levels");
        if (jsonText != null)
        {
            LevelList allLevels = JsonUtility.FromJson<LevelList>(jsonText.text);
            allLevelItems = allLevels.levels;
        }

        gridManager.RedrawGrid(gridManager.gridRadius);
        GenerateLevelButtons();
        ShowMainMenu();
    }

    void Update()
    {
        if (!isGameOver && !mainMenuPanel.activeSelf && Input.GetKeyDown(KeyCode.U)) UndoMove();

        if (isEndlessMode && isTimerRunning && !isGameOver && !isGenerating)
        {
            timeLeft -= Time.deltaTime;
            UpdateEndlessUI();

            if (timeLeft <= 0)
            {
                timeLeft = 0;
                EndlessGameOver("Süre Bitti! ⏳");
            }
        }
    }

    // --- MENÜ SİSTEMİ ---
    public void GenerateLevelButtons()
    {
        if (allLevelItems == null || levelButtonPrefab == null || levelButtonContainer == null) return;
        foreach (Transform child in levelButtonContainer) Destroy(child.gameObject);

        foreach (var level in allLevelItems)
        {
            int lvlId = level.id;
            GameObject btnObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            TextMeshProUGUI[] texts = btnObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = lvlId.ToString();

            int stars = PlayerPrefs.GetInt("LevelStars_" + lvlId, 0);
            if (texts.Length > 1)
            {
                if (lvlId > unlockedLevel) texts[1].text = "🔒";
                else if (stars == 3) texts[1].text = "⭐⭐⭐";
                else if (stars == 2) texts[1].text = "⭐⭐";
                else if (stars == 1) texts[1].text = "⭐";
                else texts[1].text = "Oynanmadı";
            }

            UnityEngine.UI.Button btn = btnObj.GetComponent<UnityEngine.UI.Button>();
            if (lvlId > unlockedLevel) btn.interactable = false;
            else btn.onClick.AddListener(() => LoadSpecificLevel(lvlId));
        }
    }

    public void ShowMainMenu()
    {
        isEndlessMode = false;
        isTimerRunning = false;
        ClearBoard();

        gridManager.RedrawGrid(gridManager.gridRadius);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (levelModeUIPanel != null) levelModeUIPanel.SetActive(false);
        if (endlessModeUIPanel != null) endlessModeUIPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void ShowLevelSelect()
    {
        GenerateLevelButtons();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }

public void StartGameFromMenu() 
    { 
        // 999 hilesini değil, oyuncunun gerçekten kaldığı bölümü hafızadan çekiyoruz
        int savedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);

        // Eğer kaydedilen bölüm, JSON'daki toplam bölüm sayısını aşıyorsa (oyun bittiyse), son bölümü aç
        if (allLevelItems != null && savedLevel > allLevelItems.Count)
        {
            savedLevel = allLevelItems.Count;
        }

        LoadSpecificLevel(savedLevel); 
    }
    public void LoadSpecificLevel(int levelId)
    {
        if (levelId > unlockedLevel) return;
        isEndlessMode = false;
        currentLevelId = levelId;
        
        currentPlayRadius = gridManager.gridRadius;
        gridManager.RedrawGrid(currentPlayRadius);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (levelModeUIPanel != null) levelModeUIPanel.SetActive(true);
        if (endlessModeUIPanel != null) endlessModeUIPanel.SetActive(false);

        LoadLevel(currentLevelId);
    }

    // --- SONSUZ MOD SİSTEMİ ---
    public void StartEndlessMode()
    {
        isEndlessMode = true;
        score = 0;
        comboCount = 0;
        endlessLevelCleared = 0;
        timeLeft = 30f;
        isTimerRunning = true;
        isGenerating = false;

        currentPlayRadius = Mathf.Max(1, gridManager.gridRadius - 1);
        gridManager.RedrawGrid(currentPlayRadius);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (levelModeUIPanel != null) levelModeUIPanel.SetActive(false);
        if (endlessModeUIPanel != null) endlessModeUIPanel.SetActive(true);
        if (comboText != null) comboText.gameObject.SetActive(false);

        GenerateNextEndlessRound(null);
    }

    void GenerateNextEndlessRound(StoneSnapshot? keptStone)
    {
        isGenerating = true;
        ClearBoard();

        bool solvable = false;
        int minMoves = 0;
        int attempts = 0;
        int stoneCount = 3 + (endlessLevelCleared / 5);
        if (stoneCount > 5) stoneCount = 5;

        // Engel Zorluk Seviyeleri
        bool useWalls = endlessLevelCleared >= 5;
        bool useSticky = endlessLevelCleared >= 10;
        bool useOneWay = endlessLevelCleared >= 12; // YENİ: Tek yönlü duvarlar eklendi
        bool useColumn = endlessLevelCleared >= 15;
        bool useHeavy = endlessLevelCleared >= 20;
        bool useBomb = endlessLevelCleared >= 25;

        List<StoneSnapshot> candidateStones = new List<StoneSnapshot>();
        List<HexCoord> candidateWalls = new List<HexCoord>();
        List<HexCoord> candidateSticky = new List<HexCoord>();
        List<HexCoord> candidateColumn = new List<HexCoord>();
        List<OneWayEdge> candidateOneWays = new List<OneWayEdge>();

        int[][] dirs = { new[] { 0, -1 }, new[] { 1, -1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 1 }, new[] { -1, 0 } };

        while (!solvable)
        {
            attempts++;
            if (attempts > 50) { useWalls = false; useSticky = false; useOneWay = false; useColumn = false; useHeavy = false; useBomb = false; }

            candidateStones.Clear();
            candidateWalls.Clear();
            candidateSticky.Clear();
            candidateColumn.Clear();
            candidateOneWays.Clear();

            List<HexCoord> emptyCells = new List<HexCoord>();
            
            for (int q = -currentPlayRadius; q <= currentPlayRadius; q++)
            {
                for (int r = -currentPlayRadius; r <= currentPlayRadius; r++)
                {
                    if (Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(-q - r)) <= currentPlayRadius)
                        emptyCells.Add(new HexCoord(q, r));
                }
            }

            if (keptStone.HasValue)
            {
                StoneSnapshot k = keptStone.Value;
                k.isDead = false; k.isHeavy = false; k.bombTimer = -1;
                candidateStones.Add(k);
                emptyCells.RemoveAll(c => c.q == k.q && c.r == k.r);
            }

            emptyCells = emptyCells.OrderBy(x => Random.value).ToList();
            char[] types = { 'T', 'K', 'M' };

            for (int i = 0; i < stoneCount; i++)
            {
                HexCoord cell = emptyCells[i];
                char type = types[Random.Range(0, types.Length)];
                bool heavy = useHeavy && (Random.value < 0.20f);
                int bomb = useBomb && (Random.value < 0.15f) ? Random.Range(4, 7) : -1;

                candidateStones.Add(new StoneSnapshot { q = cell.q, r = cell.r, type = type, isDead = false, isHeavy = heavy, bombTimer = bomb });
            }

            int obsIndex = stoneCount;
            if (useWalls) { int c = Random.Range(0, 3); for (int i = 0; i < c && obsIndex < emptyCells.Count; i++) candidateWalls.Add(emptyCells[obsIndex++]); }
            if (useSticky) { int c = Random.Range(0, 2); for (int i = 0; i < c && obsIndex < emptyCells.Count; i++) candidateSticky.Add(emptyCells[obsIndex++]); }
            if (useColumn) { int c = Random.Range(0, 2); for (int i = 0; i < c && obsIndex < emptyCells.Count; i++) candidateColumn.Add(emptyCells[obsIndex++]); }

            // YENİ: Tek Yönlü Duvarları Rastgele Üretme
            if (useOneWay)
            {
                int edgeCount = Random.Range(0, 3);
                for (int i = 0; i < edgeCount; i++)
                {
                    int rq = Random.Range(-currentPlayRadius, currentPlayRadius + 1);
                    int rr = Random.Range(-currentPlayRadius, currentPlayRadius + 1);
                    if (Mathf.Max(Mathf.Abs(rq), Mathf.Abs(rr), Mathf.Abs(-rq - rr)) > currentPlayRadius) continue;
                    
                    int[] d = dirs[Random.Range(0, dirs.Length)];
                    int nq = rq + d[0];
                    int nr = rr + d[1];
                    if (Mathf.Max(Mathf.Abs(nq), Mathf.Abs(nr), Mathf.Abs(-nq - nr)) > currentPlayRadius) continue;
                    
                    candidateOneWays.Add(new OneWayEdge(new HexCoord(rq, rr), new HexCoord(nq, nr)));
                }
            }

            // Çözülebilirlik Testine (Solver) candidateOneWays listesi de gönderiliyor
            int? calculatedMinMoves = FindMinMovesBFS(candidateStones, candidateWalls, candidateSticky, candidateColumn, candidateOneWays, currentPlayRadius);
            if (calculatedMinMoves.HasValue && calculatedMinMoves.Value > 0)
            {
                minMoves = calculatedMinMoves.Value;
                solvable = true;
            }
        }

        if (endlessLevelCleared > 0) timeLeft += 8f;

        targetMoves = minMoves;
        int bonus = endlessLevelCleared < 5 ? 4 : endlessLevelCleared < 15 ? 3 : endlessLevelCleared < 30 ? 2 : endlessLevelCleared < 50 ? 1 : 0;
        hardLimitMoves = minMoves + bonus;
        movesMade = 0;
        hasThrone = false;
        isGameOver = false;

        walls = candidateWalls;
        stickyTiles = candidateSticky;
        columns = candidateColumn;
        dynamicWalls.Clear();
        oneWayEdges = candidateOneWays; // YENİ: Başarıyla üretilen duvarları oyuna ekle

        if (endlessRoundText != null) endlessRoundText.text = $"Sonsuz Mod - Raunt {endlessLevelCleared + 1}";
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        UpdateMovesUI();
        UpdateEndlessUI();
        DrawBoardVisuals();

        foreach (var s in candidateStones) SpawnStone(s.q, s.r, s.type, s.isHeavy, s.bombTimer);

        isGenerating = false;
    }

    void UpdateEndlessUI()
    {
        if (!isEndlessMode) return;
        if (scoreText != null) scoreText.text = $"Skor: {score}";
        if (timerText != null)
        {
            timerText.text = $"⏱️ {Mathf.CeilToInt(timeLeft)}s";
            timerText.color = timeLeft <= 10f ? Color.red : new Color(1f, 0.6f, 0f); 
        }
    }

    void EndlessRoundWon()
    {
        isGenerating = true;
        int excessMoves = movesMade - targetMoves;
        int roundScore = 0;

        if (excessMoves <= 0)
        {
            comboCount++;
            roundScore = comboCount >= 3 ? (int)(100 * comboCount * 0.5f) : 100;
        }
        else
        {
            comboCount = 0;
            roundScore = 100 - (20 * excessMoves);
            if (roundScore < 0) roundScore = 0;
        }

        score += roundScore;
        endlessLevelCleared++;

        if (comboText != null)
        {
            if (comboCount >= 3)
            {
                comboText.gameObject.SetActive(true);
                comboText.text = $"🔥 {comboCount}x Kombo!";
            }
            else comboText.gameObject.SetActive(false);
        }

        UpdateEndlessUI();

        Invoke(nameof(NextEndlessWrapper), 0.4f);
    }

    void NextEndlessWrapper()
    {
        StoneSnapshot kept = new StoneSnapshot();
        var lastAlive = activeStones.FirstOrDefault(s => !s.isDead);
        if (lastAlive != null) { kept.q = lastAlive.q; kept.r = lastAlive.r; kept.type = lastAlive.type; }
        GenerateNextEndlessRound(lastAlive != null ? kept : (StoneSnapshot?)null);
    }

    void EndlessGameOver(string reason)
    {
        isGameOver = true;
        isTimerRunning = false;
        if (losePanel != null) losePanel.SetActive(true);
        if (loseReasonText != null) loseReasonText.text = $"{reason}\nToplam Skor: {score}";
    }

    // --- NORMAL SEVİYE SİSTEMİ ---
    public void LoadLevel(int levelId)
    {
        ClearBoard();

        if (allLevelItems == null) return;
        LevelItem levelToLoad = allLevelItems.FirstOrDefault(l => l.id == levelId);
        if (levelToLoad == null) return;

        LevelDetails data = levelToLoad.data;

        targetMoves = data.maxMoves;
        hardLimitMoves = targetMoves + 2;
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
        if (isEndlessMode)
        {
            if (endlessMovesText != null)
            {
                endlessMovesText.text = $"Hamle: {movesMade} / {hardLimitMoves}";
                endlessMovesText.color = (hardLimitMoves - movesMade <= 1) ? Color.red : Color.white;
            }
        }
        else
        {
            if (levelMovesText != null)
            {
                levelMovesText.text = $"Hamle: {movesMade} / {hardLimitMoves}";
                levelMovesText.color = (hardLimitMoves - movesMade <= 1) ? Color.red : Color.white;
            }
        }
    }

    void DrawBoardVisuals()
    {
        foreach (var w in walls) SpawnTile(w, wallPrefab);
        foreach (var dw in dynamicWalls) SpawnTile(dw, wallPrefab);
        foreach (var s in stickyTiles) SpawnTile(s, stickyPrefab);
        
        // Sütunlara özel isim veriyoruz ki tetiklendiğinde sahnede bulabilelim
        foreach (var c in columns) 
        {
            GameObject colObj = SpawnTile(c, columnPrefab);
            if (colObj != null) colObj.name = $"Column_{c.q}_{c.r}";
        }
        
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

    GameObject SpawnTile(HexCoord coord, GameObject prefab)
    {
        if (prefab == null) return null;
        Vector2 localPos = gridManager.GetPixelCoords(coord.q, coord.r);
        GameObject tile = Instantiate(prefab, gridManager.transform);
        tile.transform.localPosition = localPos;
        tile.transform.localRotation = Quaternion.Euler(0, 0, 30f);
        spawnedVisuals.Add(tile);
        return tile;
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
        snap.score = score;

        snap.stones = new List<StoneSnapshot>();
        foreach (var s in activeStones)
        {
            snap.stones.Add(new StoneSnapshot
            {
                q = s.q, r = s.r, type = s.type, isHeavy = s.isHeavy, bombTimer = s.bombTimer, isDead = s.isDead
            });
        }
        undoStack.Push(snap);
    }

    public void UndoMove()
    {
        if (undoStack.Count == 0 || isGameOver || isGenerating) return;
        GameStateSnapshot snap = undoStack.Pop();

        foreach (var stone in activeStones) Destroy(stone.gameObject);
        activeStones.Clear();
        foreach (var visual in spawnedVisuals) Destroy(visual);
        spawnedVisuals.Clear();

        movesMade = snap.movesMade;
        columns = new List<HexCoord>(snap.columns);
        dynamicWalls = new List<HexCoord>(snap.dynamicWalls);
        score = snap.score;

        DrawBoardVisuals();
        foreach (var sSnap in snap.stones)
        {
            if (!sSnap.isDead) SpawnStone(sSnap.q, sSnap.r, sSnap.type, sSnap.isHeavy, sSnap.bombTimer);
        }

        UpdateMovesUI();
        UpdateEndlessUI();
    }

    public void RestartLevel()
    {
        if (isEndlessMode) StartEndlessMode();
        else LoadLevel(currentLevelId);
    }

    public void NextLevel() { currentLevelId++; LoadLevel(currentLevelId); }

    public void ProcessMove(int dq, int dr)
    {
        if (isGameOver || isGenerating) return;

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

                if (Mathf.Max(Mathf.Abs(nextQ), Mathf.Abs(nextR), Mathf.Abs(-nextQ - nextR)) > currentPlayRadius) break;
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

        if (movedAnything)
        {
            movesMade++;
            UpdateMovesUI();
            ProcessBombTimers();
            CheckGameState();
        }
        else if (undoStack.Count > 0) undoStack.Pop();
    }

    bool IsEdgeBlocked(HexCoord from, HexCoord to)
    {
        foreach (var edge in oneWayEdges)
            if ((edge.from == from && edge.to == to) || (edge.from == to && edge.to == from))
                if (!(from == edge.from && to == edge.to)) return true;
        return false;
    }

void HandleColumnDestroyed(HexCoord coord) 
    { 
        columns.Remove(coord); 
        dynamicWalls.Add(coord); 

        GameObject colObj = spawnedVisuals.FirstOrDefault(v => v != null && v.name == $"Column_{coord.q}_{coord.r}");
        if (colObj != null)
        {
            Animator anim = colObj.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger("Yuksel");
            
            colObj.name = $"RisenColumn_{coord.q}_{coord.r}"; 
        }
    }
    void ProcessBombTimers()
    {
        foreach (Stone stone in activeStones)
        {
            if (!stone.isDead && stone.bombTimer > 0)
            {
                stone.bombTimer--; stone.UpdateVisualModifiers();
                if (stone.bombTimer == 0)
                {
                    if (isEndlessMode) EndlessGameOver("Bomba Patladı! 💥");
                    else
                    {
                        isGameOver = true;
                        if (losePanel != null) losePanel.SetActive(true);
                        if (loseReasonText != null) loseReasonText.text = "BOMBA PATLADI!";
                    }
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

    void LevelCompleted()
    {
        isGameOver = true;
        int earnedStars = 0;
        if (movesMade <= targetMoves) earnedStars = 3;
        else if (movesMade == targetMoves + 1) earnedStars = 2;
        else if (movesMade >= targetMoves + 2) earnedStars = 1;

        SaveProgress(earnedStars);

        if (winPanel != null) winPanel.SetActive(true);
    }

    void SaveProgress(int starsEarned)
    {
        int currentRecord = PlayerPrefs.GetInt("LevelStars_" + currentLevelId, 0);
        if (starsEarned > currentRecord) PlayerPrefs.SetInt("LevelStars_" + currentLevelId, starsEarned);

        if (currentLevelId >= unlockedLevel)
        {
            unlockedLevel = currentLevelId + 1;
            PlayerPrefs.SetInt("UnlockedLevel", unlockedLevel);
        }
        PlayerPrefs.Save();
        GenerateLevelButtons();
    }

    public void CheckGameState()
    {
        if (isGameOver) return;

        var aliveStones = activeStones.Where(s => !s.isDead).ToList();

        if (aliveStones.Count == 1)
        {
            if (isEndlessMode) { EndlessRoundWon(); return; }

            if (hasThrone)
            {
                if (aliveStones[0].q == targetThrone.q && aliveStones[0].r == targetThrone.r) LevelCompleted();
                else
                {
                    isGameOver = true;
                    if (losePanel != null) losePanel.SetActive(true);
                    if (loseReasonText != null) loseReasonText.text = "Hedef tahta ulaşamadın!";
                }
            }
            else LevelCompleted();
            return;
        }

        int rC = aliveStones.Count(s => s.type == 'T');
        int pC = aliveStones.Count(s => s.type == 'K');
        int sC = aliveStones.Count(s => s.type == 'M');

        bool isDeadlock = (rC > 1 && pC == 0) || (pC > 1 && sC == 0) || (sC > 1 && rC == 0);

        if (isDeadlock)
        {
            if (isEndlessMode) EndlessGameOver("Çözümsüz Durum! (Hatalı Hamle) 💀");
            else
            {
                isGameOver = true;
                if (losePanel != null) losePanel.SetActive(true);
                if (loseReasonText != null) loseReasonText.text = "Çıkmaz Sokak! Kazanmak imkansız.";
            }
            return;
        }

        if (movesMade >= hardLimitMoves)
        {
            if (isEndlessMode) EndlessGameOver("Hamle Hakkın Bitti! 💀");
            else
            {
                isGameOver = true;
                if (losePanel != null) losePanel.SetActive(true);
                if (loseReasonText != null) loseReasonText.text = "Hamle sınırını aştın!";
            }
        }
    }

    // --- BFS SOLVER (SONSUZ MOD İÇİN) ---
    private class SolverState
    {
        public List<StoneSnapshot> stones;
        public List<HexCoord> columns;
        public List<HexCoord> walls;
    }

    private int? FindMinMovesBFS(List<StoneSnapshot> initStones, List<HexCoord> statWalls, List<HexCoord> sticky, List<HexCoord> initCols, List<OneWayEdge> oneWays, int playRadius)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<KeyValuePair<SolverState, int>>();

        var startState = new SolverState { stones = new List<StoneSnapshot>(initStones), columns = new List<HexCoord>(initCols), walls = new List<HexCoord>() };
        queue.Enqueue(new KeyValuePair<SolverState, int>(startState, 0));
        visited.Add(GetStateHash(startState));

        int[][] dirs = { new[] { 0, -1 }, new[] { 1, -1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 1 }, new[] { -1, 0 } };

        while (queue.Count > 0)
        {
            if (visited.Count > 3000) return null; 
            var current = queue.Dequeue();
            var state = current.Key;
            int moves = current.Value;

            if (moves > 10) return null;
            if (state.stones.Count == 1) return moves;

            foreach (var d in dirs)
            {
                var nextState = SimulateSolverMove(state, d[0], d[1], statWalls, sticky, oneWays, playRadius);
                if (nextState != null)
                {
                    string hash = GetStateHash(nextState);
                    if (!visited.Contains(hash))
                    {
                        visited.Add(hash);
                        queue.Enqueue(new KeyValuePair<SolverState, int>(nextState, moves + 1));
                    }
                }
            }
        }
        return null;
    }

    private SolverState SimulateSolverMove(SolverState state, int dq, int dr, List<HexCoord> statWalls, List<HexCoord> sticky, List<OneWayEdge> oneWays, int playRadius)
    {
        var newStones = state.stones.Select(s => new StoneSnapshot { q = s.q, r = s.r, type = s.type, isHeavy = s.isHeavy, bombTimer = s.bombTimer, isDead = s.isDead }).ToList();
        var newCols = new List<HexCoord>(state.columns);
        var newWalls = new List<HexCoord>(state.walls);
        bool moved = false;

        newStones.Sort((a, b) => (b.q * dq + b.r * dr).CompareTo(a.q * dq + a.r * dr));

        for (int i = 0; i < newStones.Count; i++)
        {
            var stone = newStones[i];
            if (stone.isDead) continue;
            int cq = stone.q, cr = stone.r;

            while (true)
            {
                int nq = cq + dq, nr = cr + dr;
                if (Mathf.Max(Mathf.Abs(nq), Mathf.Abs(nr), Mathf.Abs(-nq - nr)) > playRadius) break;
                if (statWalls.Exists(w => w.q == nq && w.r == nr) || newWalls.Exists(w => w.q == nq && w.r == nr)) break;

                bool edgeBlocked = false;
                foreach (var edge in oneWays)
                {
                    if ((edge.from.q == cq && edge.from.r == cr && edge.to.q == nq && edge.to.r == nr) ||
                        (edge.from.q == nq && edge.from.r == nr && edge.to.q == cq && edge.to.r == cr))
                    {
                        if (!(cq == edge.from.q && cr == edge.from.r && nq == edge.to.q && nr == edge.to.r)) edgeBlocked = true;
                    }
                }
                if (edgeBlocked) break;

                bool isSticky = sticky.Exists(s => s.q == nq && s.r == nr);
                bool isColumn = newCols.Exists(c => c.q == nq && c.r == nr);
                var target = newStones.FirstOrDefault(s => s.q == nq && s.r == nr && !s.isDead);

                if (target.type != '\0')
                {
                    if (Beats(stone.type, target.type))
                    {
                        var tIndex = newStones.FindIndex(s => s.q == nq && s.r == nr && !s.isDead);
                        var temp = newStones[tIndex]; temp.isDead = true; newStones[tIndex] = temp;
                        cq = nq; cr = nr; moved = true;
                        if (isColumn) { newCols.RemoveAll(c => c.q == nq && c.r == nr); newWalls.Add(new HexCoord(nq, nr)); }
                        if (isSticky || stone.isHeavy) break;
                    }
                    else break;
                }
                else
                {
                    cq = nq; cr = nr; moved = true;
                    if (isColumn) { newCols.RemoveAll(c => c.q == nq && c.r == nr); newWalls.Add(new HexCoord(nq, nr)); }
                    if (isSticky || stone.isHeavy) break;
                }
            }
            stone.q = cq; stone.r = cr; newStones[i] = stone;
        }

        if (moved)
        {
            bool exploded = false;
            for (int i = 0; i < newStones.Count; i++)
            {
                if (!newStones[i].isDead && newStones[i].bombTimer > 0)
                {
                    var temp = newStones[i];
                    temp.bombTimer--;
                    if (temp.bombTimer == 0) exploded = true;
                    newStones[i] = temp;
                }
            }
            if (exploded) return null; 
            return new SolverState { stones = newStones.Where(s => !s.isDead).ToList(), columns = newCols, walls = newWalls };
        }
        return null;
    }

    private string GetStateHash(SolverState state)
    {
        var alive = state.stones.OrderBy(s => s.q).ThenBy(s => s.r).Select(s => $"{s.q},{s.r},{s.type},{s.bombTimer}");
        var cols = state.columns.OrderBy(c => c.q).ThenBy(c => c.r).Select(c => $"{c.q},{c.r}");
        var w = state.walls.OrderBy(x => x.q).ThenBy(x => x.r).Select(x => $"{x.q},{x.r}");
        return string.Join(";", alive) + "|" + string.Join(",", cols) + "|" + string.Join(",", w);
    }
}