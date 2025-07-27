// Grid tabanlı, vertical ilerlemeli arcade oyun temel yapısı
// Unity 2022.3.6f1, URP gerekmez, klasik grafikler

using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int gridWidth = 10;
    public int gridHeight = 20;
    public float cellSize = 1f;

    public GameObject wallPrefab;
    public GameObject breakablePrefab;
    public GameObject playerPrefab;
    public GameObject coinPrefab;
    public GameObject gatePrefab;
    public GameObject stairsPrefab;
    public GameObject enemyPrefab;
    public GameObject enemyShooterPrefab;
    public GameObject bombPrefab;
    public GameObject healthPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Şimdilik hardcoded bir level yüklemesi
        string[] levelData = new string[] {
            "WWWWWWWWWW",
            "W   C   GW",
            "W B   B  W",
            "W   P    W",
            "W B   B  W",
            "W   E   SW",
            "WWWWWWWWWW"
        };
        LoadLevel(levelData);
    }

    void LoadLevel(string[] data)
    {
        for (int y = 0; y < data.Length; y++)
        {
            for (int x = 0; x < data[y].Length; x++)
            {
                Vector3 pos = new Vector3(x * cellSize, y * cellSize, 0);
                char c = data[data.Length - 1 - y][x];
                SpawnByChar(c, pos);
            }
        }
    }

    void SpawnByChar(char c, Vector3 pos)
    {
        switch (c)
        {
            case 'W': Instantiate(wallPrefab, pos, Quaternion.identity); break;
            case 'B': Instantiate(breakablePrefab, pos, Quaternion.identity); break;
            case 'P': Instantiate(playerPrefab, pos, Quaternion.identity); break;
            case 'C': Instantiate(coinPrefab, pos, Quaternion.identity); break;
            case 'G': Instantiate(gatePrefab, pos, Quaternion.identity); break;
            case 'S': Instantiate(stairsPrefab, pos, Quaternion.identity); break;
            case 'E': Instantiate(enemyPrefab, pos, Quaternion.identity); break;
            case 'T': Instantiate(enemyShooterPrefab, pos, Quaternion.identity); break;
            case 'H': Instantiate(healthPrefab, pos, Quaternion.identity); break;
        }
    }
}
