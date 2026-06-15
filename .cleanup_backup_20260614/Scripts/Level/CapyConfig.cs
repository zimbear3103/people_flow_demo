using UnityEngine;

[CreateAssetMenu(fileName = "CapyConfig", menuName = "Scriptable Objects/CapyConfig")]
public class CapyConfig : ScriptableObject
{
    [Header("Stats Capybara")]
    [Min(1)]
    [SerializeField] public int m_health = 1;
    //[SerializeField] public float m_hitMoveBack = 45f;
    //[SerializeField] public float m_durationMoveBack = 1.0f;
    [SerializeField] public float m_speedRun = 300f;
}
