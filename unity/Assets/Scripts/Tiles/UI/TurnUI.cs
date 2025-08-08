using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class TurnUI : MonoBehaviour
{
    public TextMeshProUGUI turnText;
    private TurnManager turnManager;
    void Start()
    {
        turnManager = TurnManager.Instance;
    }
    void Update()
{
    if (turnText == null)
        Debug.LogError("TurnUI: turnText atanmadı!");
    if (turnManager == null)
        Debug.LogError("TurnUI: turnManager atanmadı!");
    if (turnText != null && turnManager != null)
        turnText.text = "TURN: " + turnManager.TurnCount;
}
}