using UnityEngine;

// Her tile'a özel davranışlar eklemek için kullanılan temel sınıf
public abstract class TileBehavior : MonoBehaviour
{
    // Oyuncu bu tile'a ilk adım attığında çalışır
    public virtual void OnPlayerEnter() { }

    // Oyuncu bu tile üzerindeyken her frame çağrılır (opsiyonel)
    public virtual void OnPlayerStay() { }

    // Oyuncu bu tile'dan çıktığında çalışır (opsiyonel)
    public virtual void OnPlayerExit() { }
}
