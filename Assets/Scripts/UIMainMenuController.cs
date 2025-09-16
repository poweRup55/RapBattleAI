using System.Collections;
using TMPro;
using UnityEngine;

public class UIMainMenuController : MonoBehaviour
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
            StartCoroutine(MicCheck());
            ShowInstructions();
            return;
        }
        this.gameObject.SetActive(false);
        gameComponents.SetActive(true);
        rapBattleConductor.BeginRapBattle();
    }

    private IEnumerator MicCheck()
    {
        Microphone.Start(null, false, 10, 24000);

        while (!Microphone.IsRecording(null))
        {
            yield return null;
        }

        Microphone.End(null);
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
