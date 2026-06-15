using System.Collections;
using UnityEngine;

public class GunOverloadButton : ItemButton
{
    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(DelayInit());
    }

    private void Start()
    {
        SetInfo("Gun Overload","Overload" , "Remove a cannon from the launcher");
    }


    public override void OnButtonLogic()
    {
        if(GamePlayController.Instance.GetBoss() && GamePlayController.Instance.GetCanonHolder())
        {
            //ReduceSpeedBoss();
            GamePlayController.Instance.GetCanonHolder().GunOverload = true;
            SetStateButton();
        }
    }

    private void SetStateButton()
    {
        if (GamePlayController.Instance.GetBoss() && GamePlayController.Instance.GetCanonHolder())
        {            
            if (CheckGunOverload())
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
        yield return new WaitUntil(() => (GamePlayController.Instance != null && GamePlayController.Instance.GetBoss()));
        quantity = UserProfile.Instance.GunOverloadItem;

        OnUpdateUI();
        SetStateButton();
        enableButton = true;
        GamePlayController.Instance.GetCanonHolder().GunOverload = false;
    }

    private bool CheckGunOverload()
    {
        return GamePlayController.Instance.GetCanonHolder().GunOverload;
    }
}
