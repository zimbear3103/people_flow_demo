using UnityEngine;

public class ListItemButtons : Singleton<ListItemButtons>
{
    public ItemButton[] ItemButtons;

    public void OnActiveAllItemButtons(bool isActive)
    {
        foreach (var button in ItemButtons)
        {
            if(!isActive) 
                button.OnSetEnableButton(isActive);
            else
            {
                if(button.enableButton)
                    button.OnSetEnableButton(isActive);
            }
        }
    }
}
