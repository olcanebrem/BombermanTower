using UnityEngine;
using UnityEngine.UI;

public class AsciiLevelLoader : MonoBehaviour
{
    public float tileSize = 1f;
    public Font asciiFont;

    private string[] map = new string[]
    {
        "╔══════╗",
        "║ P  $ ║",
        "║ ██ ☠ ║",
        "║    ⛩ ║",
        "╚══════╝"
    };

    void Start()
    {
        for (int y = 0; y < map.Length; y++)
        {
            string row = map[y];
            for (int x = 0; x < row.Length; x++)
            {
                char c = row[x];
                CreateAsciiTile(c, x, map.Length - y - 1); // flip Y
            }
        }
    }

    void CreateAsciiTile(char symbol, int x, int y)
    {
        GameObject tileGO = new GameObject($"Tile_{x}_{y}");
        tileGO.transform.position = new Vector3(x * tileSize, y * tileSize, 0);

        Text text = tileGO.AddComponent<Text>();
        text.text = symbol.ToString();
        text.fontSize = 1;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = asciiFont;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        tileGO.transform.SetParent(this.transform);
    }
}
