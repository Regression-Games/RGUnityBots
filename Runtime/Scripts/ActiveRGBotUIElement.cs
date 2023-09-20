using RegressionGames.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class ActiveRGBotUIElement : MonoBehaviour
    {
        private uint id;

        public TMP_Text text;
        public TMP_Text statusText;
        
        public void Start()
        {
            Button b = GetComponentInChildren<Button>();
            
            b.onClick.AddListener(() =>
            {
                RGOverlayMenu rgOverlayMenu = FindObjectOfType<RGOverlayMenu>();
                rgOverlayMenu.StopBotInstance(id);
            });

            RGBotServerListener.GetInstance().AddUnityBotStateListener(id, UpdateState);
        }
        
        public void PopulateBotEntry(RGBotInstance entry)
        {
            id = (uint) entry.id;
            text.text = $"{entry.bot.id} - {entry.bot.name}  #{entry.id}";
            statusText.text = $"{RGBotServerListener.GetInstance().GetUnityBotState(id)}";
        }

        private void UpdateState(RGUnityBotState state)
        {
            statusText.text = $"{state}";
        }
    }
}
