using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
 public class RGBotRunner
    {
        private readonly RGBotInstance _botInstance;

        private Thread _thread;

        private bool _running;

        private readonly ConcurrentQueue<RGTickInfoData> _tickInfoQueue = new();

        private readonly RG _rgObject;

        private readonly Action _teardownHook;

        private readonly RGUserBot _userBotCode;

        public RGBotRunner(RGBotInstance botInstance, RGUserBot userBotCode, Action teardownHook)
        {
            this._botInstance = botInstance;
            this._teardownHook = teardownHook;
            this._userBotCode = userBotCode;
            this._rgObject = new RG((uint) botInstance.id);
        }

        public void StartBot()
        {
            if (_thread == null)
            {
                RGBotServerListener.GetInstance().SetUnityBotState((uint) _botInstance.id, RGUnityBotState.STARTING);
                _thread = new Thread(this.RunBotLoop);
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
                Monitor.Enter(_tickInfoQueue);
                Monitor.PulseAll(_tickInfoQueue);
                Monitor.Exit(_tickInfoQueue);
                _teardownHook.Invoke();
            }
        }

        public void ProcessServerHandshake(RGServerHandshake handshake)
        {
            if (handshake.characterConfig != null)
            {
                _rgObject.SetCharacterConfig(handshake.characterConfig);
            }
        }
        
        public void QueueTickInfo(RGTickInfoData tickInfo)
        {
            if (_running)
            {
                // TODO: clone the tickInfo to avoid different bots mangling it
                // TODO: Requires that values in the dictionary implement ICloneable
                _tickInfoQueue.Enqueue(tickInfo);
                Monitor.Enter(_tickInfoQueue);
                Monitor.PulseAll(_tickInfoQueue);
                Monitor.Exit(_tickInfoQueue);
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
            handshakeMessage.botName = _botInstance.bot.name;
                
            // do the 'handshake'  In remote bots they send a message to cause this, but we'll call it directly just after starting
            RGBotServerListener.GetInstance().HandleClientHandshakeMessage((uint)_botInstance.id, handshakeMessage);
            
            while (_running)
            {
                if (_tickInfoQueue.TryDequeue(out var tickInfo))
                {
                    _rgObject.SetTickInfo(tickInfo);

                    // Run User Code
                    _userBotCode.ProcessTick(_rgObject);
                    
                    // Flush Actions / Validations
                    List<RGActionRequest> actions = _rgObject.FlushActions();
                    foreach (RGActionRequest rgActionRequest in actions)
                    {
                        RGBotServerListener.GetInstance().HandleClientActionRequest((uint)_botInstance.id, rgActionRequest);
                    }
                    
                    List<RGValidationResult> validations = _rgObject.FlushValidations();
                    foreach (RGValidationResult rgValidationResult in validations)
                    {
                        RGBotServerListener.GetInstance().HandleClientValidationResult((uint)_botInstance.id, rgValidationResult);
                    }
                }
                else
                {
                    Monitor.Enter(_tickInfoQueue);
                    Monitor.Wait(_tickInfoQueue, 10);
                    Monitor.Exit(_tickInfoQueue);
                    
                }
            }
            RGBotServerListener.GetInstance().SetUnityBotState((uint) _botInstance.id, RGUnityBotState.STOPPED);
        }

    }
}