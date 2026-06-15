using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class UnlockButton : ItemButton
{
    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(DelayInit());
    }

    public override void OnButtonLogic()
    {
        GamePlayController.Instance.GetCanonHolder().OnUnlockSlot();
        SetStateButton();
    }

    private void SetStateButton()
    {
        if (GamePlayController.Instance.GetCanonHolder() != null)
        {
            if (GamePlayController.Instance.GetCanonHolder().OnCheckLockSlot())
            {
                OnSetEnableButton(true);
            }
            else
            {
                OnSetEnableButton(false);
                enableButton = false;
            }
        }
    }

    private IEnumerator DelayInit()
    {
        yield return new WaitUntil(()=> (GamePlayController.Instance && GamePlayController.Instance.GetCanonHolder()));
        quantity = UserProfile.Instance.UnlockItem;
        OnUpdateUI();
        SetStateButton();
        enableButton = true;
    }    
}
