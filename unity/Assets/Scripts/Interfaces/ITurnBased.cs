public interface ITurnBased
{
    bool HasActedThisTurn { get; set; }
    void ResetTurn(); // Tur başında durumu sıfırlamak için
}
