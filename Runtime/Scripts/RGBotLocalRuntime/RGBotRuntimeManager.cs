using System;
using System.Collections.Concurrent;
using RegressionGames.RGBotLocalRuntime.SampleBot;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotRuntimeManager: MonoBehaviour
    {
        private static RGBotRuntimeManager _this = null;
        
        public static RGBotRuntimeManager GetInstance()
        {
            return _this;
        }

        public void Awake()
        {
            // only allow 1 of these to be alive
            if( _this != null && _this.gameObject != this.gameObject)
            {
                Destroy(this.gameObject);
                return;
            }
            // keep this thing alive across scenes
            DontDestroyOnLoad(this.gameObject);
            _this = this;
        }
        
        private readonly ConcurrentDictionary<long, RGBotRunner> _botRunners = new();
        
        public long StartBot(long botId)
        {
            RGBotInstance botInstance = new RGBotInstance();
            
            // TODO: Define unique botInstance Id ; for now, take the first bytes of a UUID... which isn't as unique as it sounds
            botInstance.id = (uint) BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(), 0);
            
            RGBotServerListener.GetInstance().SetUnityBotState((uint) botInstance.id, RGUnityBotState.STARTING);
            
            //TODO: Load this in some 'CORRECT" way based on the incoming botId argument... still TBD on that
            var userBotCode = ScriptableObject.CreateInstance<RGSampleBot>();
            
            var bot = new Types.RGBot();
            bot.id = botId;
            bot.programmingLanguage = "C#"; //TODO: (TBD based abby's work in progress as of 9/28/23) aka Unity , aka Local
            bot.name = $"{userBotCode.botName ?? "RGUnityBot"}";
            
            botInstance.bot = bot;

            botInstance.lobby = null;
            
            RGClientConnection connection = RGBotServerListener.GetInstance().AddClientConnectionForBotInstance(botInstance.id, RGClientConnectionType.LOCAL);
            
            var botRunner = new RGBotRunner(botInstance, userBotCode, () =>
            {
                _botRunners.TryRemove(botInstance.id, out _);
            });
            (connection as RGClientConnection_Local)?.SetBotRunner(botRunner);
            
            _botRunners.TryAdd(botInstance.id, botRunner);
            botRunner.StartBot();

            return botInstance.id;
        }

    }
}