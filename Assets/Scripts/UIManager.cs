using TMPro;
using UnityEngine;

namespace EpicRapBattle.Managers
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI computerTextUI;

        [SerializeField]
        private TextMeshProUGUI statusTextUI;

        [SerializeField]
        private TextMeshProUGUI errorTextUI;

        public void UpdateComputerText(string text)
        {
            if (computerTextUI != null)
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\w+\]", "");
                computerTextUI.text = "NPC: " + text;
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
        }

        public void ShowError(string errorMessage)
        {
            if (errorTextUI != null)
            {
                errorTextUI.text = "Error: " + errorMessage;
            }
            Debug.LogError(errorMessage);
        }
    }
}
