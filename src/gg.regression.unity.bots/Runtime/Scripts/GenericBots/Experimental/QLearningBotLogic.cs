using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager;
using RegressionGames.CodeCoverage;
using RegressionGames.GenericBots.Experimental.Rewards;
using RegressionGames.StateRecorder.BotSegments;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RegressionGames.GenericBots.Experimental
{
        /// <summary>
    /// Represents a single discrete action that can be taken.
    /// </summary>
    public class QAction : IEquatable<QAction>
    {
        public RGGameAction Action;
        public object ParamValue;

        public List<IRGGameActionInstance> CurrentInstances;

        private string identifier;

        public string Identifier => identifier;

        public QAction(RGGameAction action, object paramValue)
        {
            Action = action;
            ParamValue = paramValue;

            StringBuilder identifierStringBuilder = new StringBuilder();
            Action.WriteToStringBuilder(identifierStringBuilder);
            identifierStringBuilder.Append(ParamValue);

            identifier = identifierStringBuilder.ToString();

            CurrentInstances = new List<IRGGameActionInstance>();
        }

        public bool Equals(QAction other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return identifier == other.identifier;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((QAction)obj);
        }

        public override int GetHashCode()
        {
            return (identifier != null ? identifier.GetHashCode() : 0);
        }

        public static bool operator ==(QAction left, QAction right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(QAction left, QAction right)
        {
            return !Equals(left, right);
        }
    }

    class QExperience
    {
        public string State { get; }
        public string ActionKey { get; }
        public float Reward { get; }
        public string NextState { get; }

        public QExperience(string state, string actionKey, float rew, string nextState)
        {
            State = state;
            ActionKey = actionKey;
            Reward = rew;
            NextState = nextState;
        }
    }

    public class QLearningBotLogic
    {
        public bool Training = true; // if disabled, the Q-table is frozen and the game runs at normal speed
        public float ActionInterval = 0.05f; // Interval on which to take actions, expressed in scaled time (seconds). If zero, actions taken every frame.
        public float EpisodeDuration = 30.0f; // Duration of the episode, in scaled time (seconds)
        public float EpsilonDecayPerEpisode = 0.95f; // Exponential decay factor of epsilon during training after each episode
        public float MinEpsilon = 0.05f; // The lowest epsilon value possible
        public float Alpha = 0.001f; // Learning rate
        public float Gamma = 0.6f; // Discount factor
        public int ExperienceBufferSize = 64; // Size of the experience buffer (size of fixed-length queue of the last N experiences)
        public string ModelFilePath = "qbot_model.json"; // Path where to save the trained model (Q-table)
        public float TrainingTimeScale = 1.0f; // the Time.timeScale value to use while training

        public IRewardModule RewardModule = new CameraPositionRewardModule();

        private List<QAction> _actionSpace = new();
        private Dictionary<string, Dictionary<string, float>> _qTable = new();
        private List<QAction> _validActions = new();
        private float _epStartTime;
        private float _epRew;
        private Queue<QExperience> _experienceBuf = new();

        private string _lastState;
        private string _lastActionKey;
        private List<RGActionInput> _lastInputs = new();
        private float? _lastActionTime;
        private float _epsilon;
        private ISet<int> _mouseBtnsBuf = new HashSet<int>();
        private GameSpeedup _speedup; // only active if training

        /// <summary>
        /// Obtain an abstract string representation of the current game state.
        /// The default implementation concatenates all the active component type names
        /// and includes the current keyboard/mouse button state as well.
        /// </summary>
        protected virtual string GetCurrentState()
        {
            // set of enabled component types
            List<string> names = new List<string>(UnityEngine.Object.FindObjectsOfType<Component>()
                .Select(c => c.GetType()).Distinct()
                .Select(type => type.FullName));
            names.Sort();

            // include the current keyboard/mouse button input state as well, since this affects the behavior of the action
            List<string> inputs = new List<string>();
            foreach (var control in Keyboard.current.allControls)
            {
                if (control is KeyControl keyControl)
                {
                    if (keyControl.isPressed)
                    {
                        inputs.Add(keyControl.keyCode.ToString());
                    }
                }
            }
            if (Mouse.current.leftButton.isPressed)
                inputs.Add("mouseLeftButton");
            if (Mouse.current.rightButton.isPressed)
                inputs.Add("mouseRightButton");
            if (Mouse.current.middleButton.isPressed)
                inputs.Add("mouseMiddleButton");
            if (Mouse.current.forwardButton.isPressed)
                inputs.Add("mouseForwardButton");
            if (Mouse.current.backButton.isPressed)
                inputs.Add("mouseBackButton");

            inputs.Sort();
            return string.Join(",", names) + ":" + string.Join(",", inputs);
        }

        /// <summary>
        /// Logic for restarting the game. This defaults to loading the initial scene
        /// that the bot was started with via SceneManager.LoadScene().
        /// </summary>
        protected virtual void RestartGame(string initialSceneName)
        {
            SceneManager.LoadScene(initialSceneName);
        }

        /// <summary>
        /// Obtains a set of discrete parameter values for the given action.
        /// If the action is already discrete, returns each parameter value.
        /// If the action is continuous, a fixed number of discrete values are generated for it.
        /// </summary>
        protected virtual IEnumerable<object> GetDiscreteParamValues(RGGameAction act)
        {
            if (act.ParameterRange is RGDiscreteValueRange discRange)
            {
                for (int i = 0, n = discRange.NumValues; i < n; ++i)
                {
                    var val = discRange[i];
                    yield return val;
                }
            } else if (act.ParameterRange is RGContinuousValueRange contRange)
            {
                var ranges = contRange.Discretize(4);
                for (int i = 0, n = ranges.Length; i < n; ++i)
                {
                    var val = ranges[i].MidPoint;
                    yield return val;
                }
            }
        }

        /// <summary>
        /// Generates a discrete action space for the game.
        /// </summary>
        protected virtual List<QAction> GenerateActionSpace()
        {
            var actionSpace = new List<QAction>();
            foreach (var act in RGActionManager.Actions)
            {
                foreach (var param in GetDiscreteParamValues(act))
                {
                    actionSpace.Add(new QAction(act, param));
                }
            }
            return actionSpace;
        }

        /// <summary>
        /// Bots can define their own custom episode termination condition (besides EpisodeDuration) by overriding this method.
        /// </summary>
        protected virtual bool IsEpisodeFinished()
        {
            return false;
        }

        private void SaveModel()
        {
            JObject result = new JObject();
            result["epsilon"] = _epsilon;
            JObject qtable = new JObject();
            foreach (var stateEntry in _qTable)
            {
                JObject actionVals = new JObject();
                foreach (var actionEntry in stateEntry.Value)
                {
                    actionVals[actionEntry.Key] = actionEntry.Value;
                }
                qtable[stateEntry.Key] = actionVals;
            }
            result["qtable"] = qtable;
            Directory.CreateDirectory(Application.persistentDataPath + "/RegressionGames/Resources/QLearningModels/");
            File.WriteAllText(Application.persistentDataPath + "/RegressionGames/Resources/QLearningModels/" + ModelFilePath, result.ToString());
        }

        private bool LoadModel()
        {
            if (!File.Exists(Application.persistentDataPath + "/RegressionGames/Resources/QLearningModels/" + ModelFilePath))
            {
                return false;
            }
            JObject data = JObject.Parse(System.IO.File.ReadAllText(Application.persistentDataPath + "/RegressionGames/Resources/QLearningModels/" + ModelFilePath));
            _epsilon = data["epsilon"].ToObject<float>();
            JObject qtable = (JObject)data["qtable"];
            _qTable = new Dictionary<string, Dictionary<string, float>>();
            foreach (var entry in qtable)
            {
                Dictionary<string, float> actionVals = new Dictionary<string, float>();
                JObject actionValsObj = (JObject)entry.Value;
                foreach (var actionEntry in actionValsObj)
                {
                    actionVals[actionEntry.Key] = actionEntry.Value.ToObject<float>();
                }
                _qTable.Add(entry.Key, actionVals);
            }
            return true;
        }



        /// <summary>
        /// Calls RGActionManager.GetValidActions() to get the set of valid actions, then transforms
        /// this result into the discrete action space representation used by the Q learning bot.
        /// </summary>
        private void ComputeValidActions()
        {
            var validActions = new Dictionary<RGGameAction, List<IRGGameActionInstance>>();
            foreach (var actionInst in RGActionManager.GetValidActions())
            {
                var act = actionInst.BaseAction;
                List<IRGGameActionInstance> instances;
                if (!validActions.TryGetValue(act, out instances))
                {
                    instances = new List<IRGGameActionInstance>();
                    validActions.Add(act, instances);
                }
                instances.Add(actionInst);
            }

            _validActions.Clear();
            foreach (var qact in _actionSpace)
            {
                var act = qact.Action;
                if (validActions.TryGetValue(act, out var instances))
                {
                    qact.CurrentInstances.Clear();
                    foreach (var inst in instances)
                    {
                        if (inst.IsValidParameter(qact.ParamValue))
                        {
                            qact.CurrentInstances.Add(inst);
                        }
                    }
                    if (qact.CurrentInstances.Count > 0)
                    {
                        _validActions.Add(qact);
                    }
                }
            }
        }

        public void OnDestroy()
        {
            if (Training)
            {
                _speedup.Dispose();
                _speedup = null;
            }
            RewardModule.Dispose();
            RGActionManager.StopSession();
        }

        public void OnEpisodeStart(int segmentNumber, MonoBehaviour context)
        {
            if (context == null)
            {
                context = Object.FindObjectOfType<BotSegmentsPlaybackController>();
            }

            if (Training)
            {
                RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                if (rgSettings.GetFeatureCodeCoverage())
                {
                    RGCodeCoverage.Clear();
                    RGCodeCoverage.StartRecording();
                }

                RGActionRuntimeCoverageAnalysis.Reset();
                RGActionRuntimeCoverageAnalysis.StartRecording(segmentNumber);
            }
            RGActionManager.TryStartSession(segmentNumber, context);
            _actionSpace = GenerateActionSpace();
            _epStartTime = Time.time;
            _epRew = 0.0f;
            _lastState = null;
            _lastActionKey = null;
            _lastActionTime = null;
            RewardModule.Reset();
            _experienceBuf.Clear();
        }

        private void OnEpisodeEnd()
        {
            RGDebug.Log($"Episode reward: {_epRew}, State count: {_qTable.Count}, Epsilon: {_epsilon}");
            _epsilon *= EpsilonDecayPerEpisode;
            if (_epsilon < MinEpsilon)
            {
                _epsilon = MinEpsilon;
            }
            SaveModel();
        }

        private static string ActionKey(QAction action, IRGGameActionInstance actionInst)
        {
            return action.Identifier + ":" + actionInst.TargetObject.name;
        }

        private float ReadQTable(string stateKey, string actionKey)
        {
            if (_qTable.TryGetValue(stateKey, out var actionVals))
            {
                if (actionVals.TryGetValue(actionKey, out var actionVal))
                {
                    return actionVal;
                }
                else
                {
                    return 0.0f;
                }
            }
            else
            {
                return 0.0f;
            }
        }

        private float GetStateQValue(string stateKey)
        {
            if (_qTable.TryGetValue(stateKey, out var actionVals))
            {
                if (actionVals.Count > 0)
                {
                    return actionVals.Max(entry => entry.Value);
                }
            }
            return 0.0f;
        }

        /// <summary>
        /// This method performs heuristic actions depending on the action taken by the policy
        /// on the previous frame. If a heuristic is performed, this returns true.
        /// </summary>
        private bool PerformHeuristics()
        {
            bool didHeuristic = false;

            // If the last set of inputs was a mouse position movement + mouse button press,
            // then release the mouse button over the same location.
            {
                bool haveMousePos = false;
                _mouseBtnsBuf.Clear();
                foreach (var inp in _lastInputs)
                {
                    if (inp is MousePositionInput)
                    {
                        haveMousePos = true;
                    } else if (inp is MouseButtonInput mbInput)
                    {
                        if (mbInput.IsPressed)
                        {
                            _mouseBtnsBuf.Add(mbInput.MouseButton);
                        }
                    } else if (inp is LegacyKeyInput keyInput)
                    {
                        if (keyInput.KeyCode >= KeyCode.Mouse0 && keyInput.KeyCode <= KeyCode.Mouse6)
                        {
                            _mouseBtnsBuf.Add(keyInput.KeyCode - KeyCode.Mouse0);
                        }
                    }
                }

                if (haveMousePos && _mouseBtnsBuf.Count > 0)
                {
                    _lastInputs.Clear();
                    foreach (var btn in _mouseBtnsBuf)
                    {
                        var inp = new MouseButtonInput(btn, false);
                        inp.Perform(0);
                        _lastInputs.Add(inp);
                    }
                    didHeuristic = true;
                }
            }

            return didHeuristic;
        }

        public void Start()
        {
            if (LoadModel())
            {
                RGDebug.Log("Loaded existing model with epsilon " + _epsilon);
            }
            else
            {
                if (!Training)
                {
                    throw new Exception("No existing model is available");
                }
                _qTable = new Dictionary<string, Dictionary<string, float>>();
                _epsilon = 1.0f;
            }

            if (Training)
            {
                _speedup = new GameSpeedup(TrainingTimeScale);
            }
        }

        public bool Update(int segmentNumber, MonoBehaviour context, string initialSceneName = null)
        {
            var now = Time.time;
            if (now - _epStartTime >= EpisodeDuration || IsEpisodeFinished())
            {
                OnEpisodeEnd();
                if (initialSceneName != null)
                {
                    RestartGame(initialSceneName);
                }

                OnEpisodeStart(segmentNumber, context);
            }

            if (_lastActionTime.HasValue && now - _lastActionTime < ActionInterval)
            {
                // Repeat the previous inputs
                foreach (var inp in _lastInputs)
                {
                    inp.Perform(segmentNumber);
                }
                return true;
            }

            if (PerformHeuristics())
            {
                _lastActionTime = Time.time;
                return true;
            }

            string state = GetCurrentState();

            if (Training)
            {
                _speedup.Update();

                if (!_qTable.TryGetValue(state, out _))
                {
                    _qTable.Add(state, new Dictionary<string, float>());
                }

                // Record latest experience
                if (_lastState != null)
                {
                    float rew = RewardModule.GetRewardForLastAction();
                    _experienceBuf.Enqueue(new QExperience(_lastState, _lastActionKey, rew, state));
                    while (_experienceBuf.Count > ExperienceBufferSize)
                        _experienceBuf.Dequeue();
                    _epRew += rew;
                }

                // Update Q-table with experience
                foreach (var exp in _experienceBuf)
                {
                    float oldVal = ReadQTable(exp.State, exp.ActionKey);
                    float nextMax = GetStateQValue(exp.NextState);
                    float newValue = (1.0f - Alpha) * oldVal + Alpha * (exp.Reward + Gamma * nextMax);
                    _qTable[exp.State][exp.ActionKey] = newValue;
                }
            }
            else
            {
                _epRew += RewardModule.GetRewardForLastAction();
            }

            _lastState = null;
            _lastActionKey = null;

            ComputeValidActions();

            if (_validActions.Count == 0)
            {
                // nothing to do
                return false;
            }

            // Select the action to take
            QAction action;
            IRGGameActionInstance actionInst;
            if (Random.Range(0.0f, 1.0f) < _epsilon)
            {
                // Randomly choose an action
                action = _validActions[Random.Range(0, _validActions.Count)];
                actionInst = action.CurrentInstances[Random.Range(0, action.CurrentInstances.Count)];
            }
            else
            {
                // Choose action with the highest Q value
                QAction maxAction = null;
                IRGGameActionInstance maxActionInst = null;
                float maxVal = float.NegativeInfinity;
                foreach (QAction qact in _validActions)
                {
                    foreach (IRGGameActionInstance inst in qact.CurrentInstances)
                    {
                        var actVal = ReadQTable(state, ActionKey(qact, inst));
                        if (actVal > maxVal)
                        {
                            maxAction = qact;
                            maxActionInst = inst;
                            maxVal = actVal;
                        }
                    }
                }
                action = maxAction;
                actionInst = maxActionInst;
            }

            // Perform the chosen action
            _lastInputs.Clear();
            if (actionInst != null)
            {
                foreach (var inp in actionInst.GetInputs(action.ParamValue))
                {
                    _lastInputs.Add(inp);
                    inp.Perform(segmentNumber);
                }
                _lastState = state;
                _lastActionKey = ActionKey(action, actionInst);
                _lastActionTime = now;
                return true;
            }
            // else
            _lastState = null;
            _lastActionKey = null;
            _lastActionTime = now;
            return false;

        }
    }
}
