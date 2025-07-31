using UnityEngine;
using UnityEngine.UI; // Text bileşeni için bu gerekli

public class TileBase : MonoBehaviour
{
    // [SerializeField] sayesinde bu alanı Unity Inspector'dan atayabileceğiz.
    // Her prefabın kendi Text bileşenini buraya sürükleyeceğiz.
    [SerializeField]
    private Text visualText;

    /// <summary>
    /// Bu tile'ın görselini merkezi TileSymbols'dan gelen karaktere göre ayarlar.
    /// LevelLoader tarafından çağrılacak.
    /// </summary>
    public void SetVisual(char symbol)
    {
        if (visualText != null)
        {
            visualText.text = symbol.ToString();
        }
        else
        {
            Debug.LogError($"'{gameObject.name}' prefabında 'visualText' atanmamış!");
        }
    }
}