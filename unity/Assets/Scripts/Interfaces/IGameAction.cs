using UnityEngine;
// Bu, oyundaki her türlü eylemin (hareket, saldırı, bomba koyma)
// uyması gereken temel sözleşmedir.
public interface IGameAction
{
    /// <summary>
    /// Bu eylemin hangi birim tarafından gerçekleştirildiğini temsil eder.
    /// </summary>
    GameObject Actor { get; }
    /// <summary>
    /// Bu eylemi gerçekleştirir.
    /// </summary>
    void Execute();

}