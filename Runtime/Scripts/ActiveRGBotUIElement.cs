using RegressionGames.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class ActiveRGBotUIElement : MonoBehaviour
    {
        private long id;

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
        }
        
        public void PopulateBotEntry(RGBotInstance entry)
        {
            id = entry.id;
            text.text = $"{entry.bot.id} - {entry.bot.name}  #{entry.id}";
            statusText.text = $"TODO ...";
        }
    }
}
