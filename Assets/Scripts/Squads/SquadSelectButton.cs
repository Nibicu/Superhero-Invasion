using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SquadSelectButton : MonoBehaviour
{
    public TMP_Text squadNameText;

    private Squad squad;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(SelectSquad);
    }

    public void Setup(Squad targetSquad)
    {
        squad = targetSquad;

        squadNameText.text =
            squad.commander.heroData.heroName;
    }

    private void SelectSquad()
    {
        UIManager.Instance.AssignSquadToSelectedObject(
            squad
        );
    }
}