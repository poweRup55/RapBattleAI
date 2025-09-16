using System;
using System.Collections;
using System.Collections.Generic;
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
    private GameObject PositiveReactionLocationObject;

    [SerializeField]
    private GameObject NegativeReactionLocationObject;

    [SerializeField]
    private GameObject JudgePanelUI;

    private Queue<PopUpGameText> popupStack = new Queue<PopUpGameText>();

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

    public void AddReaction(string message, bool isPositive)
    {
        GameObject popUpObj = Instantiate(
            Resources.Load<GameObject>("PopUpGameText"),
            isPositive
                ? PositiveReactionLocationObject.transform
                : NegativeReactionLocationObject.transform
        );
        popUpObj.SetActive(false);
        PopUpGameText popUpReaction = popUpObj.GetComponent<PopUpGameText>();
        Color reactionColor = isPositive
            ? new Color(0.2f, 1f, 0.2f) // Green for positive reactions
            : new Color(1f, 0.2f, 0.2f); // Red for negative reactions

        popUpReaction.SetColor(reactionColor);
        popUpReaction.SetText(message);

        popupStack.Enqueue(popUpReaction);
    }

    public PopUpGameText PopNextReaction()
    {
        if (popupStack.Count > 0)
        {
            PopUpGameText nextPopup = popupStack.Dequeue();
            return nextPopup;
        }
        return null;
    }

    public void ClearAllReactions()
    {
        while (popupStack.Count > 0)
        {
            PopUpGameText popup = popupStack.Dequeue();
            Destroy(popup.gameObject);
        }
    }

    public void RemovePlayingReactions()
    {
        foreach (Transform child in PositiveReactionLocationObject.transform)
        {
            Destroy(child.gameObject);
        }
        foreach (Transform child in NegativeReactionLocationObject.transform)
        {
            Destroy(child.gameObject);
        }
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
