using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level data")]
public class LevelData : ScriptableObject
{
    [SerializeField] public Color BackgroundColor;
    [SerializeField] public Sprite BackgroundImage;

    [Header("Custom Level Data")]
    [SerializeField] public CustomLevelData BoxAmmoLevelData;

    [Header("Boss configuration")]
    [SerializeField] public BossConfig BossConfig;
    [SerializeField] public GameObject BossPathPrefab;

    [Header("Capy configuration")]
    [SerializeField] public CapyConfig CapyConfig;
    [SerializeField] public GameObject CapyPathPrefab;
}
