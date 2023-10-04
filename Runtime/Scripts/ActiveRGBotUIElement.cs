using RegressionGames.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class ActiveRGBotUIElement : MonoBehaviour
    {
        private RGBotInstance _entry;

        public TMP_Text text;
        public TMP_Text statusText;

        public void Awake()
        {
            if (_entry != null)
            {
                var state = RGBotServerListener.GetInstance()?.GetUnityBotState((uint)_entry.id);
                statusText.text = $"{state}";
            }
        }
        
        public void Start()
        {
            Button b = GetComponentInChildren<Button>();
            
            b.onClick.AddListener(() =>
            {
                FindObjectOfType<RGOverlayMenu>()?.StopBotInstance((uint)_entry.id);
            });
            RGBotServerListener.GetInstance().AddUnityBotStateListener((uint)_entry.id, UpdateState);
            statusText.text = $"{RGBotServerListener.GetInstance().GetUnityBotState((uint)_entry.id)}";
        }
        
        public void PopulateBotEntry(RGBotInstance entry)
        {
            _entry = entry;
            //TODO: (post reg-988) : Correct this check
            var locationString = entry.bot.id < 0 ? "Local" : "Remote";
            text.text = $"{locationString} - {entry.bot.name} : {entry.bot.id}  #{entry.id}";
            statusText.text = $"{RGBotServerListener.GetInstance().GetUnityBotState((uint)entry.id)}";
        }

        private void UpdateState(RGUnityBotState state)
        {
            statusText.text = $"{state}";
        }
    }
}