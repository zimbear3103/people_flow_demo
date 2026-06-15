using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "GameLevels", menuName = "Scriptable Objects/All level data")]
public class GameLevels : ScriptableObject
{
    [SerializeField] public List<LevelData> Datas = new List<LevelData>();
}
