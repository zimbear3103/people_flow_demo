using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossConfig", menuName = "Scriptable Objects/BossConfig")]
public class BossConfig : ScriptableObject
{
    [Header("Init Boss Data")]
    [SerializeField] public GameObject HeadPrefab;
    [SerializeField] public GameObject BodyPartPrefab;
    [SerializeField] public GameObject TailPrefab;
    [SerializeField] public RandomMode ModeRandom;
                   
    [Header("Stats Boss")]
    [SerializeField] public float InitSpeed = 500f;
    [SerializeField] public float InitTime = 1f;
    [SerializeField] public float StableSpeed = 150f;
    [SerializeField] public float MoveBackSpeed = 500f;
    [SerializeField] public float PartSpacing = 25f;
    [SerializeField] public float CloseSpeed = 100f;        
    [SerializeField] public float WaitForAttackTime = 2.0f;        

    [Header("Random")]
    [Header("Scamble")]
    [SerializeField] public RandomForEachBox[] RuleRandom;
    [SerializeField] public bool isPriories;
    [Header("Dynamic")]
    [Range(0f, 1f)]
    [SerializeField] public float BaseNumber;
    [Range(0f, 1f)]
    [SerializeField] public float IncreaseRate;
    [SerializeField] public bool IsConfigSetColor;
    [Min(1)]
    [SerializeField] public int DefaultNumberPartEachRandomNewColor;
    [Min(1)]
    [SerializeField] public List<int> ListNumberPartEachRandomNewColor;
    [Header("Manual")]
    [SerializeField] public string InputManual;
}
