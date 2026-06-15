using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class ExportLevelData : Singleton<ExportLevelData>
{
    [SerializeField] GameObject m_fog;
    [SerializeField] GameObject m_ammoBox;
    [SerializeField] GameObject m_conveyBelt;
    List<BoxAmmoData> m_allBoxAmmoData = new List<BoxAmmoData>();
    List<PortalData> m_allPortalData = new List<PortalData>();
    [SerializeField] GameObject m_canonHolder;
    
    public void OnCheckValidate()
    {
        Debug.Log("[ExportLevelData] calll OnCheckValidate.....");
        if (m_ammoBox == null) return;

        bool isActiveConveyBelt = false;
        bool isActiveFog = false;

        foreach (Transform entry in m_ammoBox.transform)
        {
            if (entry.name.StartsWith("CustomLevelData") && entry.gameObject.activeSelf)
            {              
                CustomLevelEditor customLevelEditor = entry.gameObject.GetComponent<CustomLevelEditor>();
                isActiveFog = customLevelEditor.IsUsedFog;
                isActiveConveyBelt = customLevelEditor.IsUsedConveyBelt;                
            }
        }

        m_conveyBelt.SetActive(isActiveConveyBelt);
        m_fog.SetActive(isActiveFog);
    }

    public void ExportAmmoBoxData()
    {
        foreach (Transform entry in m_ammoBox.transform)
        {
            if (entry.name.StartsWith("CustomLevelData") && entry.gameObject.activeSelf)
            {
                m_allBoxAmmoData.Clear();
                m_allPortalData.Clear();

                foreach (Transform child in entry)
                {
                    if (child.name.StartsWith("crate"))
                    {
                        GameObject childObj = child.gameObject;
                        BoxAmmo boxAmmo = childObj.GetComponent<BoxAmmo>();
                        BoxAmmoData boxAmmoData = new BoxAmmoData();

                        boxAmmoData.BoxColor = boxAmmo.BoxColor;
                        boxAmmoData.MovementDirection = boxAmmo.MovementDirection;
                        boxAmmoData.BoxType = boxAmmo.BoxType;
                        boxAmmoData.Speed = boxAmmo.Speed;
                        boxAmmoData.IsInConveyBelt = boxAmmo.IsInConveyBelt;
                        boxAmmoData.SpeedMoveInSpline = boxAmmo.SpeedMoveInSpline;
                        boxAmmoData.TimeRotate = boxAmmo.TimeRotate;
                        boxAmmoData.IsNonStuckAtBegin = boxAmmo.IsNonStuckAtBegin;
                        boxAmmoData.SpeedToBoss = boxAmmo.SpeedToBoss;
                        boxAmmoData.PortalID = boxAmmo.PortalId;

                        boxAmmoData.Position = childObj.transform.localPosition;
                        boxAmmoData.Scale = childObj.transform.localScale;

                        m_allBoxAmmoData.Add(boxAmmoData);
                    }
                    else
                    {
                        GameObject childObj = child.gameObject;
                        Portal portal = childObj.GetComponent<Portal>();
                        PortalData portalData = new PortalData();

                        portalData.Position = childObj.transform.localPosition;
                        portalData.Scale = childObj.transform.localScale;

                        m_allPortalData.Add(portalData);
                    }
                }

                SaveAsset(entry);
            }
        } 
    }

    private void SaveAsset(Transform entry)
    {
        string path = "Assets/Resources_moved/LevelData/" + $"{entry.name}" + ".asset";

        // Đảm bảo folder tồn tại
        if (!Directory.Exists("Assets/Resources_moved"))
        {
            Directory.CreateDirectory("Assets/Resources_moved");
        }
        CustomLevelData boxAmmoLevelData = ScriptableObject.CreateInstance<CustomLevelData>();
        boxAmmoLevelData.AllBoxAmmoData = m_allBoxAmmoData;
        boxAmmoLevelData.AllPortalData = m_allPortalData;

        //fog

        CustomLevelEditor customLevelEditor = entry.gameObject.GetComponent<CustomLevelEditor>();
        m_fog.SetActive(customLevelEditor.IsUsedFog);

        if (m_fog.activeSelf)
        {
            boxAmmoLevelData.FogColor = m_fog.transform.Find("Strength").GetComponent<Image>().color;

            RectTransform parentRect = m_fog.transform.Find("Height").GetComponent<RectTransform>();
            RectTransform fogStrengthRect = m_fog.transform.Find("Strength").GetComponent<RectTransform>();
            RectTransform fogNonShootableRangeRect = m_fog.transform.Find("NonShootableRange").GetComponent<RectTransform>();

            float fogStrengthHeight = fogStrengthRect.rect.height;
            float fogNonShootableRangeHeight = fogNonShootableRangeRect.rect.height;
            float parentHeight = parentRect.rect.height;

            boxAmmoLevelData.FogStrength = Mathf.FloorToInt((fogStrengthHeight / parentHeight) * 100);
            boxAmmoLevelData.FogNonShootableRange = Mathf.FloorToInt((fogNonShootableRangeHeight / parentHeight) * 100);
        }
        else
        {
            boxAmmoLevelData.FogColor = m_fog.transform.Find("Strength").GetComponent<Image>().color;
            boxAmmoLevelData.FogStrength = 0; 
            boxAmmoLevelData.FogNonShootableRange = 0; 
        }    

        //Slot Status        
        List<int> slotActiveHolder = new List<int>();
        foreach (Transform child in m_canonHolder.GetComponent<CanonHolder>().CanonSlotsZone)
        {
            if (child.gameObject.activeSelf)
            {
                CanonSlot canonSlot = child.GetComponent<CanonSlot>();

                if (canonSlot.Data.m_statusSlot == SlotStatus.Lock)
                    slotActiveHolder.Add(1);
                else
                    slotActiveHolder.Add(0);
            }
        }

        boxAmmoLevelData.slotsActive = slotActiveHolder;
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(boxAmmoLevelData, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
        Debug.Log("Đã lưu vào: " + path);
    }
}

