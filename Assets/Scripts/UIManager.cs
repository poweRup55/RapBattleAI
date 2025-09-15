using System.Collections;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI aiRapperTextUI;

    [SerializeField]
    private TextMeshProUGUI statusTextUI;

    [SerializeField]
    private TextMeshProUGUI errorTextUI;

    [SerializeField]
    private TextMeshProUGUI aiJudgeTextUI;

    [SerializeField]
    private GameObject MainBattleUI;

    [SerializeField]
    private GameObject JudgePanelUI;

    public void SetAiText(string text)
    {
        if (aiRapperTextUI != null)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]"
            );
            aiRapperTextUI.isRightToLeftText = isRtl;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\w+\]", "");
            aiRapperTextUI.text = text;
        }
    }

    public void AppendToAiText(string text)
    {
        if (aiRapperTextUI != null)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]"
            );
            aiRapperTextUI.isRightToLeftText = isRtl;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\w+\]", "");
            aiRapperTextUI.text += text;
        }
    }

    public void ClearComputerText()
    {
        if (aiRapperTextUI != null)
        {
            aiRapperTextUI.text = "";
        }
    }

    public void UpdateStatus(string message)
    {
        if (statusTextUI != null)
        {
            statusTextUI.text = message;
        }
        // Debug.Log(message);
    }

    public void clearText()
    {
        if (aiRapperTextUI != null)
        {
            aiRapperTextUI.text = "";
        }
        if (statusTextUI != null)
        {
            statusTextUI.text = "";
        }
        if (errorTextUI != null)
        {
            errorTextUI.text = "";
        }
        if (aiJudgeTextUI != null)
        {
            aiJudgeTextUI.text = "";
        }
    }

    public void ShowError(string errorMessage)
    {
        if (errorTextUI != null)
        {
            errorTextUI.text = "Error: " + errorMessage;
        }
        Debug.LogError(errorMessage);
    }

    public void UpdateAIJudgeText(string text)
    {
        if (aiJudgeTextUI != null)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]"
            );
            aiJudgeTextUI.isRightToLeftText = isRtl;
            aiJudgeTextUI.text += text;
        }
    }

    public void ClearAIJudgeText()
    {
        if (aiJudgeTextUI != null)
        {
            aiJudgeTextUI.text = "";
        }
    }

    public void ShowPopUp(string message)
    {
        GameObject popUpObj = Instantiate(
            Resources.Load<GameObject>("PopUpGameText"),
            MainBattleUI.transform
        );
        popUpObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            Random.Range(-50f, 50f),
            Random.Range(-50f, 50f)
        );
        PopUpGameText popUpGameText = popUpObj.GetComponent<PopUpGameText>();
        popUpGameText.ShowText(message);
    }

    public IEnumerator ShowJudgePanelTemporarily(float duration)
    {
        if (JudgePanelUI != null)
        {
            JudgePanelUI.SetActive(true);
            MainBattleUI.SetActive(false);
            yield return new WaitForSeconds(duration);
            JudgePanelUI.SetActive(false);
            MainBattleUI.SetActive(true);
        }
    }
}
