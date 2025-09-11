using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI computerTextUI;

    [SerializeField]
    private TextMeshProUGUI statusTextUI;

    [SerializeField]
    private TextMeshProUGUI errorTextUI;

    [SerializeField]
    private TextMeshProUGUI aiJudgeTextUI;

    public void UpdateComputerText(string text)
    {
        if (computerTextUI != null)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]"
            );
            computerTextUI.isRightToLeftText = isRtl;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\w+\]", "");
            computerTextUI.text = text;
        }
    }

    public void AppendComputerText(string text)
    {
        if (computerTextUI != null)
        {
            // Detect RTL languages: Hebrew, Arabic, Syriac, Thaana, N'Ko, etc.
            bool isRtl = System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"[\u0590-\u05FF\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\u0700-\u074F\u07C0-\u07FF\uFB50-\uFDFF\uFE70-\uFEFF]"
            );
            computerTextUI.isRightToLeftText = isRtl;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\w+\]", "");
            computerTextUI.text += text;
        }
    }

    public void ClearComputerText()
    {
        if (computerTextUI != null)
        {
            computerTextUI.text = "";
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
        if (computerTextUI != null)
        {
            computerTextUI.text = "";
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

    internal void ClearAIJudgeText()
    {
        if (aiJudgeTextUI != null)
        {
            aiJudgeTextUI.text = "";
        }
    }
}
