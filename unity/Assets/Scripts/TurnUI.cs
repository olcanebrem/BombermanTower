using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class TurnUI : MonoBehaviour
{
    public TextMeshProUGUI turnText;
    void Update()
    {
        turnText.text = "TURN: " + TurnManager.Instance.TurnCount;
    }
}