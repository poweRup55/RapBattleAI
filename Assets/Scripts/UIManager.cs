using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    // Cached compiled regex patterns for performance
    private static readonly Regex RtlLanguageRegex = new Regex(
        @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]",
        RegexOptions.Compiled
    );
    private static readonly Regex TagRemovalRegex = new Regex(@"\[\w+\]", RegexOptions.Compiled);

    [SerializeField]
    private TextMeshProUGUI aiRapperTextUI;

    [SerializeField]
    private TextMeshProUGUI statusTextUI;

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

    [SerializeField]
    private GameObject PopUpGameTextPrefab;

    private Queue<PopUpGameText> popupStack = new Queue<PopUpGameText>();

    // Object pool for popups to reduce Instantiate/Destroy overhead
    private Queue<PopUpGameText> popupPool = new Queue<PopUpGameText>();
    private const int maxPoolSize = 10;

    // StringBuilder instances for text accumulation
    private StringBuilder aiRapperTextBuilder = new StringBuilder();
    private StringBuilder aiJudgeTextBuilder = new StringBuilder();

    // UI update throttling
    private string lastAiRapperText = "";
    private string lastStatusText = "";

    public void SetAiText(string text)
    {
        if (aiRapperTextUI != null && text != lastAiRapperText)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = RtlLanguageRegex.IsMatch(text);
            aiRapperTextUI.isRightToLeftText = isRtl;
            text = TagRemovalRegex.Replace(text, "");
            aiRapperTextUI.text = text;
            lastAiRapperText = text;
            aiRapperTextBuilder.Clear();
            aiRapperTextBuilder.Append(text);
        }
    }

    public void AppendToAiText(string text)
    {
        if (aiRapperTextUI != null && !string.IsNullOrEmpty(text))
        {
            text = TagRemovalRegex.Replace(text, "");
            aiRapperTextBuilder.Append(text);
            if (RtlLanguageRegex.IsMatch(text))
            {
                aiRapperTextUI.isRightToLeftText = true;
            }
        }
    }

    public void ClearComputerText()
    {
        if (aiRapperTextUI != null)
        {
            aiRapperTextUI.text = "";
            lastAiRapperText = "";
            aiRapperTextBuilder.Clear();
        }
    }

    public void UpdateStatus(string message)
    {
        if (statusTextUI != null && message != lastStatusText)
        {
            statusTextUI.text = message;
            lastStatusText = message;
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
        if (aiJudgeTextUI != null)
        {
            aiJudgeTextUI.text = "";
        }
    }

    public void UpdateAIJudgeText(string text)
    {
        if (aiJudgeTextUI != null && !string.IsNullOrEmpty(text))
        {
            aiJudgeTextBuilder.Append(text);

            if (RtlLanguageRegex.IsMatch(text))
            {
                aiJudgeTextUI.isRightToLeftText = true;
            }
        }
    }

    public void ClearAIJudgeText()
    {
        if (aiJudgeTextUI != null)
        {
            aiJudgeTextUI.text = "";
            aiJudgeTextBuilder.Clear();
        }
    }

    public void AddReaction(string message, bool isPositive)
    {
        if (PopUpGameTextPrefab == null)
        {
            Debug.LogError("PopUpGameTextPrefab is not assigned in UIManager!");
            return;
        }

        PopUpGameText popUpReaction;

        if (popupPool.Count > 0)
        {
            popUpReaction = popupPool.Dequeue();
            popUpReaction.gameObject.SetActive(false);
        }
        else
        {
            GameObject popUpObj = Instantiate(
                PopUpGameTextPrefab,
                isPositive
                    ? PositiveReactionLocationObject.transform
                    : NegativeReactionLocationObject.transform
            );
            popUpObj.SetActive(false);
            popUpReaction = popUpObj.GetComponent<PopUpGameText>();
        }

        popUpReaction.transform.SetParent(
            isPositive
                ? PositiveReactionLocationObject.transform
                : NegativeReactionLocationObject.transform,
            false
        );
        popUpReaction.SetReaction(message, isPositive);
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
            if (popup != null && popup.gameObject != null)
            {
                ReturnToPool(popup);
            }
        }
    }

    private void ReturnToPool(PopUpGameText popup)
    {
        if (popup == null || popup.gameObject == null)
            return;

        popup.gameObject.SetActive(false);

        if (popupPool.Count < maxPoolSize)
        {
            popupPool.Enqueue(popup);
        }
        else
        {
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
