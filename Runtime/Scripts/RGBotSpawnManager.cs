using UnityEngine;

namespace RegressionGames
{
    public abstract class RGBotSpawnManager : MonoBehaviour
    {

        protected static RGBotSpawnManager _this = null;

        protected virtual void Awake()
        {
            // only allow 1 of these to be alive
            if( _this != null && this.gameObject != _this.gameObject)
            {
                Destroy(this.gameObject);
                return;
            }
            // keep this thing alive across scenes
            DontDestroyOnLoad(this.gameObject);
            _this = this;
        }

        public static RGBotSpawnManager GetInstance()
        {
            return _this;
        }
        
        public abstract void SpawnBots(bool lateJoin = false);

        public abstract void SpawnBot(bool lateJoin, uint clientId, string botName, string characterType);

        public abstract void TeardownBot(uint clientId);
        
        public abstract void StopGame();

        public abstract int? GetPlayerId(uint clientId);

        public abstract void SeatPlayer(uint clientId, string characterType, string botName);
    }
}
