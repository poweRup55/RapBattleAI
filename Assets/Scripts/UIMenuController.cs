using System.Collections;
using EpicRapBattle.Managers;
using TMPro;
using UnityEngine;

public class UIMenuController : MonoBehaviour
{
    private bool seenInstructions = false;

    [SerializeField]
    private GameObject gameComponents;

    [SerializeField]
    private GameObject mainMenuPanel;

    [SerializeField]
    private GameObject instructionsPanel;

    [SerializeField]
    private GameObject popUpPanel;

    [SerializeField]
    private RapBattleConductor rapBattleConductor;

    // Start is called before the first frame update
    void Start()
    {
        if (gameComponents == null || mainMenuPanel == null || instructionsPanel == null)
        {
            Debug.LogError("UI components are not assigned in the inspector.");
            return;
        }

        gameComponents.SetActive(false);
        mainMenuPanel.SetActive(true);
        instructionsPanel.SetActive(false);
        popUpPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update() { }

    public void StartGame()
    {
        if (!seenInstructions)
        {
            ShowInstructions();
            return;
        }
        this.gameObject.SetActive(false);
        gameComponents.SetActive(true);
        rapBattleConductor.BeginRapBattle();
    }

    public void ShowInstructions()
    {
        mainMenuPanel.SetActive(false);
        instructionsPanel.SetActive(true);
        seenInstructions = true;
    }

    public void HideInstructions()
    {
        instructionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public IEnumerator ShowPopUp(string message)
    {
        popUpPanel.GetComponentInChildren<TextMeshProUGUI>().text = message;

        popUpPanel.SetActive(true);

        yield return new WaitForSeconds(3f);

        popUpPanel.SetActive(false);
    }

    public void ShowMainMenu()
    {
        gameComponents.SetActive(false);
        this.gameObject.SetActive(true);
        mainMenuPanel.SetActive(true);
        instructionsPanel.SetActive(false);
        popUpPanel.SetActive(false);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
