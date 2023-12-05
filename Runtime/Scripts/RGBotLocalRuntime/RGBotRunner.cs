using System;
using System.Collections.Generic;
using System.Threading;
using RegressionGames.DataCollection;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using Unity.Profiling;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RGBotRunner
    {
        // A marker used to measure the total time spent processing the bot tick, including flushing all actions and validations
        private static readonly ProfilerMarker FullBotTickMarker = new(ProfilerCategory.Ai, "RegressionGames.BotTick.Full");

        // A marker used to measure the time taken within the user's bot for each tick.
        private static readonly ProfilerMarker UserBotTickMarker = new(ProfilerCategory.Ai, "RegressionGames.BotTick.User");

        public readonly RGBotInstance BotInstance;

        private Thread _thread;

        private bool _running;

        // This 'could' be a concurrentQueue, but this is called sequentially for each
        // from the main thread today and thus doesn't need the overhead
        private readonly Queue<RGTickInfoData> _tickInfoQueue = new();

        private readonly RG _rgObject;

        private readonly Action _teardownHook;

        private readonly RGUserBot _userBotCode;

        public RGBotRunner(RGBotInstance botInstance, RGUserBot userBotCode, Action teardownHook)
        {
            this.BotInstance = botInstance;
            this._teardownHook = teardownHook;
            this._userBotCode = userBotCode;
            this._rgObject = new RG(botInstance.id);
        }

        public void StartBot()
        {
            if (_thread == null)
            {
                RGBotServerListener.GetInstance().SetUnityBotState(BotInstance.id, RGUnityBotState.STARTING);
                _thread = new Thread(this.RunBotLoop);
                _thread.Name = $"RGBotRunner-{BotInstance.id}";
                _running = true;
                _thread.Start();
            }
        }

        public void TeardownBot()
        {
            if (_thread != null)
            {
                _tickInfoQueue.Clear();
                _running = false; // this should end the thread loop on next pass
                _teardownHook.Invoke();
            }
        }

        public void ProcessServerHandshake(RGServerHandshake handshake)
        {
            if (handshake.characterConfig != null)
            {
                _rgObject.CharacterConfig = handshake.characterConfig;
            }
        }

        public void QueueTickInfo(RGTickInfoData tickInfo)
        {
            if (_running)
            {
                // TODO (REG-1301): clone the tickInfo to avoid different bots mangling it
                //   which would require that values in the dictionary implement ICloneable
                _tickInfoQueue.Enqueue(tickInfo);
            }
        }

        private void RunBotLoop()
        {
            // configure bot from user code
            _userBotCode.ConfigureBot(_rgObject);

            // before we get into the loop, handle handshakes and such
            RGClientHandshake handshakeMessage = new RGClientHandshake();
            handshakeMessage.unityToken = RGServiceManager.RG_UNITY_AUTH_TOKEN;
            handshakeMessage.rgToken = Guid.NewGuid().ToString();
            handshakeMessage.spawnable = _userBotCode.spawnable;
            handshakeMessage.characterConfig = _rgObject.CharacterConfig;
            handshakeMessage.lifecycle = _userBotCode.lifecycle.ToString();
            handshakeMessage.botName = BotInstance.bot.name;

            // do the 'handshake'  In remote bots they send a message to cause this, but we'll call it directly just after starting
            RGBotServerListener.GetInstance().HandleClientHandshakeMessage(BotInstance.id, handshakeMessage);

            while (_running)
            {
                if (_rgObject.Completed)
                {
                    // teardown
                    RGBotServerListener.GetInstance().EndClientConnection(_rgObject.ClientId);
                }
                else
                {
                    RGTickInfoData tickInfo = null;
                    var tickInfoCount = 0;
                    while (_tickInfoQueue.TryDequeue(out var nextTickInfo))
                    {
                        tickInfo = nextTickInfo;
                        ++tickInfoCount;
                    }

                    if (tickInfoCount > 1)
                    {
                        RGDebug.LogWarning(
                            $"WARNING: Bot instanceId: {BotInstance.id}, botName: {BotInstance.bot.name}, botId: {BotInstance.bot.id} is processing slower than the tick rate.  {tickInfoCount - 1} tick info updates have been discarded... processing latest tick info");
                    }

                    if (tickInfo != null)
                    {
                        _rgObject.SetTickInfo(tickInfo);

                        // Start a profiler marker, which will be automatically ended when it goes out of scope.
                        using var _ = FullBotTickMarker.Auto();

                        // Run User Code
                        try
                        {
                            using(UserBotTickMarker.Auto())
                            {
                                _userBotCode.ProcessTick(_rgObject);
                            }

                            // Flush Actions / Validations
                            List<RGActionRequest> actions = _rgObject.FlushActions();
                            foreach (RGActionRequest rgActionRequest in actions)
                            {
                                RGBotServerListener.GetInstance()
                                    .HandleClientActionRequest(BotInstance.id, rgActionRequest);
                            }

                            List<RGValidationResult> validations = _rgObject.FlushValidations();
                            foreach (RGValidationResult rgValidationResult in validations)
                            {
                                RGBotServerListener.GetInstance()
                                    .HandleClientValidationResult(BotInstance.id, rgValidationResult);
                            }

                            // Save all information from this tick for local bots
                            RGBotServerListener.GetInstance()
                                ?.SaveDataForTick(BotInstance.id, tickInfo, actions, validations);
                        }
                        catch (Exception ex)
                        {
                            RGDebug.LogError(
                                $"ERROR: Bot instanceId: {BotInstance.id}, botName: {BotInstance.bot.name}, botId: {BotInstance.bot.id} crashed due to an uncaught exception. - {ex}");
                            _running = false;
                            RGBotServerListener.GetInstance().SetUnityBotState(BotInstance.id, RGUnityBotState.ERRORED);
                        }
                    }
                    else
                    {
                        // wait up to 10ms for next message
                        // we tried using event wait handles here to signal, but it was more
                        // efficient to just sleep and loop (10ms == 100 updates PER SECOND... which is twice as fast as the default physics tick in Unity)
                        Thread.Sleep(10);
                    }
                }
            }

            if (!Equals(RGBotServerListener.GetInstance().GetUnityBotState(BotInstance.id), RGUnityBotState.ERRORED))
            {
                RGBotServerListener.GetInstance().SetUnityBotState(BotInstance.id, RGUnityBotState.STOPPED);
            }
        }

        public void OnDrawGizmos()
        {
            if (_running)
            {
                _userBotCode.OnDrawGizmos();
            }
        }
    }
}
