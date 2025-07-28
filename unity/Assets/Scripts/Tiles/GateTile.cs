using UnityEngine;
public class GateTile : TileBehavior
{
    public override void OnPlayerEnter()
    {
        Debug.Log("Level geçiliyor...");
        // LevelLoader.instance.LoadNextLevel(); gibi
    }
}
