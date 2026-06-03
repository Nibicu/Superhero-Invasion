using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SquadButton : MonoBehaviour
{
    public TMP_Text commanderText;

    public TMP_Text membersText;

    public TMP_Text statusText;

    private Squad squad;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(OpenDetails);
    }

    public void Setup(Squad newSquad)
    {
        squad = newSquad;

        commanderText.text =
            squad.commander.heroData.heroName;

        membersText.text =
            "Members: " +
            (squad.members.Count + 1);

        statusText.text =
            "Ready";
    }

    private void OpenDetails()
    {
        UIManager.Instance
            .OpenSquadDetails(squad);
    }
}