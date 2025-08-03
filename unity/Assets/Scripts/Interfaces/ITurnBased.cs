public interface ITurnBased
{
    bool HasActedThisTurn { get; set; }
    void ResetTurn();
    
    /// <summary>
    /// TurnManager tarafından, bu birimin sırası geldiğinde çağrılır.
    /// Birimin tüm AI/karar verme mantığı bu metodun içinde olmalıdır.
    /// </summary>
    void ExecuteTurn();
}