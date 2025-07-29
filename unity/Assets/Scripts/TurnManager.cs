using UnityEngine;
using System;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public float turnInterval = 0.25f; // saniye cinsinden tur süresi
    private float turnTimer = 0f;

    public int TurnCount { get; private set; } = 0;

    public static event Action OnTurnAdvanced;

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

    public void AdvanceTurn()
    {
        TurnCount++;
        OnTurnAdvanced?.Invoke();
        Debug.Log($"Turn {TurnCount} advanced.");
    }
}
