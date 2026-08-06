[System.Serializable]
public struct HexCoord
{
    public int q;
    public int r;

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    // İki koordinatın aynı olup olmadığını kolayca kontrol etmek için
    public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
    public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);
    
    public override bool Equals(object obj) => obj is HexCoord other && this == other;
    public override int GetHashCode() => (q, r).GetHashCode();
}