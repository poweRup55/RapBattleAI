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
            if (computerTextUI != null)
            {
                computerTextUI.text = "";
            }
            if (statusTextUI != null)
            {
                statusTextUI.text = "";
            }
        }
    }
}
