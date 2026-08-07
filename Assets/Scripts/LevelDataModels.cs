using System.Collections.Generic;

[System.Serializable]
public class LevelList
{
    public List<LevelItem> levels;
}

[System.Serializable]
public class LevelItem
{
    public int id;
    public LevelDetails data;
}

[System.Serializable]
public class LevelDetails
{
    public int maxMoves;
    public List<StoneData> stones = new List<StoneData>();
    public List<HexCoordData> walls = new List<HexCoordData>();
    public List<HexCoordData> sticky = new List<HexCoordData>();
    public List<HexCoordData> column = new List<HexCoordData>();
    public List<OneWayEdgeData> oneWayEdges = new List<OneWayEdgeData>();
}

[System.Serializable]
public class StoneData
{
    public int q;
    public int r;
    public string type;
    public bool heavy = false;
    public int bomb = -1; // -1 ise bomba yok demektir
}

[System.Serializable]
public class HexCoordData
{
    public int q;
    public int r;
}

[System.Serializable]
public class OneWayEdgeData
{
    public int q1;
    public int r1;
    public int q2;
    public int r2;
}