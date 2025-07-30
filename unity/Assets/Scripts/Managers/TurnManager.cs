using UnityEngine;
using System;
using System.Collections.Generic; 
using System.Linq;           

public class TurnManager : MonoBehaviour
{
    // --- Singleton Kurulumu ---
    public static TurnManager Instance { get; private set; }

    // --- Ayarlanabilir Değişkenler ---
    public float turnInterval = 0.1f;
    
    // --- Dahili Durum Değişkenleri ---
    private float turnTimer = 0f;
    public int TurnCount { get; private set; } = 0;

    // **********************************************************
    // ******** İŞTE LİSTENİN YAZILDIĞI YER BURASI ********
    //
    // Bu liste, ITurnBased arayüzünü uygulayan tüm aktif nesnelerin
    // kaydını tutar.
    // 'private' olması önemlidir, çünkü bu listeyi sadece TurnManager'ın
    // kendisi yönetmelidir.
    private List<ITurnBased> turnBasedObjects = new List<ITurnBased>();
    // **********************************************************


    // --- Unity Yaşam Döngüsü Metodları ---
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        turnTimer += Time.deltaTime;
        if (turnTimer >= turnInterval)
        {
            turnTimer = 0f;
            AdvanceTurn();
        }
    }

    // --- Liste Yönetim Metodları (Public) ---
    // Nesnelerin kendilerini bu listeye eklemesi için.
    public void Register(ITurnBased obj)
    {
        if (!turnBasedObjects.Contains(obj))
        {
            turnBasedObjects.Add(obj);
        }
    }

    // Nesnelerin yok olmadan önce kendilerini listeden çıkarması için.
    public void Unregister(ITurnBased obj)
    {
        if (turnBasedObjects.Contains(obj))
        {
            turnBasedObjects.Remove(obj);
        }
    }

    // --- Ana Tur Yönetim Metodu ---
    public static event Action OnTurnAdvanced;
    
    public void AdvanceTurn()
    {
        TurnCount++;

        // 1. HAZIRLIK: Listeyi kullanarak herkese "resetlen" komutu ver.
        // .ToList() kullanmak, döngü sırasında bir nesne kendini listeden çıkarırsa
        // (örneğin patlayan bir bomba) hata almayı önler. Güvenli bir yöntemdir.
        foreach (var obj in turnBasedObjects.ToList()) 
        {
            obj?.ResetTurn(); // Nesne null değilse resetle
        }

        // 2. EYLEM: Herkes hazır olduğuna göre, yeni turun başladığını duyur.
        OnTurnAdvanced?.Invoke();
    }
}