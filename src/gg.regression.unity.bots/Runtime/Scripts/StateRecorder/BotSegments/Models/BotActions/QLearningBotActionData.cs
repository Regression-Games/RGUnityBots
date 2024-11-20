using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.ActionManager;
using RegressionGames.GenericBots.Experimental;
using RegressionGames.GenericBots.Experimental.Rewards;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions
{
    public class RewardModuleConfig : IStringBuilderWriteable
    {
        public int apiVersion = SdkApiVersion.VERSION_27;



        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);

            stringBuilder.Append("}}");
        }
    }

    [Serializable]
    public class QLearningBotActionData : IBotActionData
    {
        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.QLearning;

        public int apiVersion = SdkApiVersion.VERSION_27;
        public float actionInterval = 0.05f;
        public string modelFilePath = "qbot_segment_model.json";
        public RGActionManagerSettings actionSettings;
        public Dictionary<RewardType, int> rewardTypeRatios;

        public float? maxRuntimeSeconds;

        private float _startTime = -1f;

        // training options
        public bool training;
        public float trainingTimeScale = 1.0f;

        [NonSerialized]
        private bool _isStopped;

        [NonSerialized]
        private QLearningBotLogic _qLearningBot;

        private IRewardModule GetRewardModuleForType(RewardType type)
        {
            switch (type)
            {
                case RewardType.ActionCoverage:
                    return new ActionCoverageRewardModule();
                case RewardType.CameraPosition:
                default:
                    return new CameraPositionRewardModule();
            }
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                var controller = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
                RGActionManager.StartSession(segmentNumber, controller, actionSettings);

                Dictionary<IRewardModule, float> rewardRatios = new();
                var ratioSum = 0;
                foreach (var (_, ratio) in rewardTypeRatios)
                {
                    // configuring less than 1 is invalid and will not be considered
                    if (ratio > 0)
                    {
                        ratioSum += ratio;
                    }
                }

                if (ratioSum < 1)
                {
                    throw new Exception("At least one QLearningBot rewardType must have a ratio > 0");
                }

                // normalize the values to a percentage decimal
                foreach (var (type, ratio) in rewardTypeRatios)
                {
                    // configuring less than 1 is invalid and will not be considered
                    if (ratio > 0)
                    {
                        var nextType = GetRewardModuleForType(type);
                        rewardRatios[nextType] = ratio / (ratioSum * 1.0f);
                    }
                }

                var rewardModule = new CombinationRewardModule()
                {
                    ratios = rewardRatios
                };

                _qLearningBot = new QLearningBotLogic
                {
                    ActionInterval = actionInterval,
                    ModelFilePath = modelFilePath,
                    RewardModule = rewardModule,
                    Training = training,
                    TrainingTimeScale = trainingTimeScale,
                };

                _startTime = Time.unscaledTime;
                _qLearningBot.Start();
                _qLearningBot.OnEpisodeStart(segmentNumber, null);
            }
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            var time = Time.unscaledTime;

            if (maxRuntimeSeconds.HasValue && _startTime > 0 && time - _startTime > maxRuntimeSeconds.Value)
            {
                // stop the bot, it has run the specified time
                AbortAction(segmentNumber);
            }

            if (!_isStopped)
            {
                RGActionRuntimeCoverageAnalysis.SetCurrentSegmentNumber(segmentNumber);
                bool didAnyAction = _qLearningBot.Update(segmentNumber, null, null);
                error = null;
                return didAnyAction;
            }

            error = null;
            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            RGActionManager.StopSession();
            _isStopped = true;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"actionInterval\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, actionInterval);
            stringBuilder.Append(",\"modelFilePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, modelFilePath);
            stringBuilder.Append(",\"maxRuntimeSeconds\":");
            FloatJsonConverter.WriteToStringBuilderNullable(stringBuilder, maxRuntimeSeconds);
            stringBuilder.Append(",\"training\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, training);
            stringBuilder.Append(",\"trainingTimeScale\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, trainingTimeScale);
            stringBuilder.Append(",\"actionSettings\":");
            actionSettings.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"rewardTypeRatios\":{");
            var counter = 0;
            var ratiosCount = rewardTypeRatios.Count;
            foreach (var (key, value) in rewardTypeRatios)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, key.ToString());
                stringBuilder.Append(":");
                IntJsonConverter.WriteToStringBuilder(stringBuilder, value);
                if (++counter < ratiosCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("}}");
        }

        public void ReplayReset()
        {
            _startTime = -1f;
            _isStopped = false;
            _qLearningBot = null;
        }

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
