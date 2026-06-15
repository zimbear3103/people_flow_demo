using UnityEngine;
public class CustomLevelEditor : MonoBehaviour
{
    [SerializeField] bool m_isUsedFog = false;
    [SerializeField] bool m_isUsedConveyBelt = false;
    public bool IsUsedFog => m_isUsedFog;
    public bool IsUsedConveyBelt => m_isUsedConveyBelt;

    public void SetUsedConveyBelt(bool value)
    {
        m_isUsedConveyBelt = value;       
    }

    private void OnValidate()
    {      
        var parent = GetComponentInParent<ExportLevelData>();
        if (parent != null)
        {
            parent.OnCheckValidate();
        }
    }
}

