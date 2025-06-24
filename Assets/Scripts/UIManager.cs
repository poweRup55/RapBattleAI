using TMPro;
using UnityEngine;

namespace EpicRapBattle.Managers
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI playerTextUI;

        [SerializeField]
        private TextMeshProUGUI computerTextUI;

        [SerializeField]
        private TextMeshProUGUI statusTextUI;

        public void UpdatePlayerText(string text)
        {
            if (playerTextUI != null)
            {
                playerTextUI.text = "You: " + text;
            }
        }

        public void UpdateComputerText(string text)
        {
            if (computerTextUI != null)
            {
                computerTextUI.text = "NPC: " + text;
            }
        }

        public void UpdateStatus(string message)
        {
            if (statusTextUI != null)
            {
                statusTextUI.text = message;
            }
            Debug.Log(message);
        }

        public void clearText()
        {
            if (playerTextUI != null)
            {
                playerTextUI.text = "";
            }

            if (computerTextUI != null)
            {
                computerTextUI.text = "";
            }
        }
    }
}
