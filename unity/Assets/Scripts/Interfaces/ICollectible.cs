using UnityEngine;

public interface ICollectible
{
    /// <summary>
    /// Bir nesne bu toplanabilir öğenin üzerine geldiğinde çağrılır.
    /// </summary>
    /// <param name="collector">Öğeyi toplayan nesnenin GameObject'i.</param>
    bool OnCollect(GameObject collector);
}