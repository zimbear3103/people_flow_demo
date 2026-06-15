using System.Collections;
using UnityEngine;

public class SortButton : ItemButton
{
    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(DelayInit());
    }

    public void Start()
    {
        SetInfo("Sort","Sort", "Sort the boss's body by color");
    }

    public override void OnButtonLogic()
    {
        StartCoroutine(GamePlayController.Instance.GetBoss().SortBodyPartItem());
        SetStateButton();
    }

    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        StartCoroutine(GamePlayController.Instance.GetBoss().SortBodyPartItem());
    //    }
    //}

    private void SetStateButton()
    {
        if (GamePlayController.Instance.GetBoss() != null)
        {
            if (GamePlayController.Instance.GetBoss().IsInSort)
            {
                ListItemButtons.Instance.OnActiveAllItemButtons(false);
            }
            else
            {
                OnSetEnableButton(true);
            }
        }
    }

    public  IEnumerator DelayInit()
    {
        yield return new WaitUntil(() => (GamePlayController.Instance != null && GamePlayController.Instance.GetBoss()));
        quantity = UserProfile.Instance.SortItem;
        OnUpdateUI();
        SetStateButton();
        enableButton = true;
    }
}
