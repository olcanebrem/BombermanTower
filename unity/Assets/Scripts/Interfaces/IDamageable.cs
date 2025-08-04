using System;

public interface IDamageable
{
    /// <summary>
    /// Nesnenin mevcut canını temsil eder.
    /// </summary>
    int CurrentHealth { get; }

    /// <summary>
    /// Nesnenin maksimum canını temsil eder.
    /// </summary>
    int MaxHealth { get; }

    /// <summary>
    /// Nesne hasar aldığında bu metod çağrılır.
    /// </summary>
    /// <param name="damageAmount">Alınan hasar miktarı.</param>
    void TakeDamage(int damageAmount);

    /// <summary>
    /// Can değiştiğinde tetiklenen olay. UI'ı güncellemek için kullanılır.
    /// </summary>
    event Action OnHealthChanged;
}