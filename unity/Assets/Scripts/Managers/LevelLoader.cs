using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
[System.Serializable]
public struct TilePrefabEntry
{
    public TileType type;
    public GameObject prefab;
}
public class LevelLoader : MonoBehaviour
{
    public static LevelLoader instance;
    public TilePrefabEntry[] tilePrefabs;
    private Dictionary<TileType, GameObject> prefabMap;
    public int tileSize = 30;
    public Font asciiFont;
    public GameObject playerPrefab;
    public GameObject bombPrefab;
    public GameObject projectilePrefab;

    public int width = 10;
    public int height = 30;

    public GameObject playerObject;
    public GameObject[,] tileObjects;

    public char[,] levelMap;

    void Start()
    {
        GenerateRandomLevel();
        CreateMapVisual();
    }
    void Awake()
    {
        prefabMap = new();
        foreach (var entry in tilePrefabs)
        {
            if (!prefabMap.ContainsKey(entry.type))
                prefabMap.Add(entry.type, entry.prefab);
        }
        if (instance != null && instance != this)
            Destroy(gameObject);
        else
            instance = this;
    }


    void GenerateRandomLevel()
    {
        levelMap = new char[width, height];

        // Hepsini boş yap
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                levelMap[x, y] = TileSymbols.TypeToSymbol(TileType.Empty);

        // Çevre duvarları koy
        for (int x = 0; x < width; x++)
        {
            levelMap[x, 0] = TileSymbols.TypeToSymbol(TileType.Wall);
            levelMap[x, height - 1] = TileSymbols.TypeToSymbol(TileType.Wall);
        }
        for (int y = 0; y < height; y++)
        {
            levelMap[0, y] = TileSymbols.TypeToSymbol(TileType.Wall);
            levelMap[width - 1, y] = TileSymbols.TypeToSymbol(TileType.Wall);
        }

        // Player spawn'u rastgele yerleştir
        int px = Random.Range(1, width - 1);
        int py = Random.Range(1, height - 1);
        levelMap[px, py] = TileSymbols.TypeToSymbol(TileType.PlayerSpawn);

        // Örnek olarak diğer bazı nesneleri rastgele yerleştir
        PlaceRandom(TileType.Coin, 10);
        PlaceRandom(TileType.Breakable, 5);
        PlaceRandom(TileType.Enemy, 4);
        PlaceRandom(TileType.EnemyShooter, 2);
        PlaceRandom(TileType.Bomb, 3);
        PlaceRandom(TileType.Health, 3);
        PlaceRandom(TileType.Gate, 1);
        PlaceRandom(TileType.Stairs, 1);
    }

    void PlaceRandom(TileType type, int count)
    {
        int placed = 0;
        while (placed < count)
        {
            int x = Random.Range(1, width - 1);
            int y = Random.Range(1, height - 1);
            if (levelMap[x, y] == TileSymbols.TypeToSymbol(TileType.Empty))
            {
                levelMap[x, y] = TileSymbols.TypeToSymbol(type);
                placed++;
            }
        }
    }

    void CreateMapVisual()
    {
        tileObjects = new GameObject[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = levelMap[x, y];
                Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
                TileType type = TileSymbols.SymbolToType(c);

                GameObject tileGO = null;

                if (type == TileType.PlayerSpawn)
                {
                    playerObject = Instantiate(playerPrefab, pos, Quaternion.identity, transform);
                     var player = playerObject.GetComponent<PlayerController>();
                    player?.Init(x, y);
                    tileObjects[x, y] = playerObject;
                    continue;
                }

                if (prefabMap.TryGetValue(type, out var prefab))
                {
                    tileGO = Instantiate(prefab, pos, Quaternion.identity, transform);

                    // Instantiate'dan sonra Init varsa çağır
                    var enemyShooter = tileGO.GetComponent<EnemyShooterTile>();
                    if (enemyShooter != null)
                    {
                        enemyShooter.Init(x, y);
                        Debug.Log($"Enemy shooter at ({x},{y}) initialized.");
                    }

                

                    // Gerekirse diğer tile scriptleri için de Init çağrısı ekle
                }
                else
                {
                    tileGO = CreateAsciiTile(c, pos);
                }

                tileObjects[x, y] = tileGO;
            }
        }
    }



    GameObject CreateAsciiTile(char symbol, Vector3 position)
    {
        GameObject tileGO = new GameObject($"Tile_{position.x}_{position.y}");
        tileGO.transform.SetParent(this.transform);
        tileGO.transform.position = position;

        RectTransform rt = tileGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;

        Text text = tileGO.AddComponent<Text>();
        text.text = symbol.ToString();
        text.fontSize = tileSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = asciiFont;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return tileGO;
    }
    public void PlaceBombAt(int x, int y, int range, float duration)
    {
        Vector3 pos = new Vector3(x * tileSize, (height - y - 1) * tileSize, 0);
        GameObject bombGO = Instantiate(bombPrefab, pos, Quaternion.identity, transform);
        
        BombTile bombTile = bombGO.GetComponent<BombTile>();
        bombTile.Init(x, y, range, duration);
        
        levelMap[x, y] = TileSymbols.TypeToSymbol(TileType.Bomb);
        tileObjects[x, y] = bombGO;
    }
}
