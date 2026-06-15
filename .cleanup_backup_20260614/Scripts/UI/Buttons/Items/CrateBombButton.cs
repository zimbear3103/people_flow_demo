using System.Collections;
using System.Linq;
using UnityEngine;

public class CrateBombButton : ItemButton
{
    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(DelayInit());
    }

    private void Start()
    {
        SetInfo("Crate Bomb","Remove" , "Select any crate to attack the boss");
    }


    private void Update()
    {
        if(!CheckBoxAmmo() && enableButton)
        {
            SetStateButton();
        }
    }

    public override void OnButtonLogic()
    {
        GamePlayController.Instance.OnCrateBomb(true);
        //ReduceSpeedBoss();
        SetStateButton();
    }

    private void SetStateButton()
    {
        if (GamePlayController.Instance != null)
        {
            if(!CheckBoxAmmo())
            {
                OnSetEnableButton(false);
                enableButton = false;
            }
            else if (GamePlayController.Instance.IsActiveCrateBomb())                
            {
                ListItemButtons.Instance.OnActiveAllItemButtons(false);
            }
            else
            {
                OnSetEnableButton(true);
            }
        }
    }

    public IEnumerator DelayInit()
    {
        yield return new WaitUntil(() => (GamePlayController.Instance != null && GamePlayController.Instance.CurrentLevelData != null));
        quantity = UserProfile.Instance.CrateItem;

        OnUpdateUI();
        SetStateButton();
        enableButton = true;
    }

    private bool CheckBoxAmmo()
    {
        var listBoxAmmo = GamePlayController.Instance.AmmoBoxObjects;
        listBoxAmmo = listBoxAmmo.Where(obj => obj != null).ToList();
        if (listBoxAmmo.Any())
            return true;

        return false;
    }
}
