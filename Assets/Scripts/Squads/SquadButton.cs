using TMPro;
using UnityEngine;

public class SquadButton : MonoBehaviour
{
    public TMP_Text commanderText;

    public TMP_Text membersText;

    public TMP_Text statusText;

    public void Setup(Squad squad)
    {
        commanderText.text =
            squad.commander.heroData.heroName;

        membersText.text =
            "Members: " +
            (squad.members.Count + 1);

        statusText.text =
            "Ready";
    }
}