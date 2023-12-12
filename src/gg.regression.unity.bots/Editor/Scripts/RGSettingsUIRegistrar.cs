using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RegressionGames.Types;
using UnityEngine;
#if UNITY_EDITOR
using RegressionGames.RGBotLocalRuntime;
using UnityEditor;
#endif

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    /**
     * This class is coded strangely.  The editor ui handles a lot of things asynchronously/automatically and thus the refreshing and state is managed externally.
     * Making that interleave cleanly with asynchronous API calls was quite the party trick :)
     */
    public class RGSettingsUIRegistrar
    {

        private static RGServiceManager rgServiceManager = new (); // editor, not game/scene so don't look for one, make one

        private static string token = null;
        private static string priorUser = null;
        private static string priorPassword = null;
        private static string priorHost = null;

        private static RGBot[] bots = null;

        // we use this to only call to redo signin when you haven't typed/updated for 3 seconds
        private static double timeOfLastEdit = 0f;

        [SettingsProvider]
        public static SettingsProvider CreateRGSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Regression Games", SettingsScope.Project)
            {
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = async (searchContext) =>
                {
                    SerializedObject settings = RGSettings.GetSerializedSettings();
                    SerializedObject userSettings = RGUserSettings.GetSerializedUserSettings();
                    EditorGUI.BeginChangeCheck();
                    
                    SerializedProperty hostField = settings.FindProperty("rgHostAddress");
                    hostField.stringValue = EditorGUILayout.TextField("RG Host URL", hostField.stringValue);
                    
                    SerializedProperty emailField = userSettings.FindProperty("email");
                    emailField.stringValue = EditorGUILayout.TextField("RG Email", emailField.stringValue);
                    SerializedProperty passwordField = userSettings.FindProperty("password");
                    passwordField.stringValue = EditorGUILayout.PasswordField("RG Password", passwordField.stringValue);
                    
                    SerializedProperty logLevel = settings.FindProperty("logLevel");
                    logLevel.enumValueIndex = (int)(DebugLogLevel)EditorGUILayout.EnumPopup("Log Level", (DebugLogLevel)logLevel.enumValueIndex);

                    SerializedProperty enableOverlay = settings.FindProperty("enableOverlay");
                    enableOverlay.boolValue =
                        EditorGUILayout.Toggle("Enable Screen Overlay ?", enableOverlay.boolValue);
                    
                    SerializedProperty useSystemSettings = settings.FindProperty("useSystemSettings");
                    useSystemSettings.boolValue =
                        EditorGUILayout.Toggle("Use Global Bot Settings ?", useSystemSettings.boolValue);
                    
                    EditorGUI.BeginDisabledGroup(useSystemSettings.boolValue != true);
                    SerializedProperty numBotsProp = settings.FindProperty("numBots");
                    numBotsProp.intValue = EditorGUILayout.IntSlider("Number Of Bots", numBotsProp.intValue, 0, 7, new GUILayoutOption[] { });
                    SerializedProperty botsSelected = settings.FindProperty("botsSelected");
                    if ((EditorApplication.timeSinceStartup-timeOfLastEdit) > 3f && token == null && priorPassword == null && priorHost == null && priorUser == null && passwordField.stringValue.Length > 4 && emailField.stringValue.Length > 4 && hostField.stringValue.Length > 4)
                    {
                        priorPassword = passwordField.stringValue;
                        priorUser = emailField.stringValue;
                        priorHost = hostField.stringValue;
                        await Login(priorUser, priorPassword);
                    }

                    if (token != null && (bots == null || bots.Length == 0))
                    {
                        List<RGBot> listOfBots = new();
                        // get remote bots
                        await rgServiceManager.GetBotsForCurrentUser(botList =>
                        {
                            foreach (var rgBot in botList)
                            {
                                if (rgBot is { IsUnityBot: true , IsLocal: false})
                                {
                                    listOfBots.Add(rgBot);
                                }
                            }
                        }, () =>
                        {
                            
                        });
                        // get Local bots
                        RGBotAssetsManager.GetInstance()?.RefreshAvailableBots();
                        var localBots = RGBotAssetsManager.GetInstance()?.GetAvailableBots();
                        if (localBots != null)
                        {
                            foreach (var localBot in localBots)
                            {
                                listOfBots.Add(localBot);
                            }
                        }
                        listOfBots.Sort((a,b) => String.Compare(a.UIString, b.UIString, StringComparison.Ordinal));
                        bots = listOfBots.ToArray();
                    }

                    if (bots != null && bots.Length > 0)
                    {
                        List<string> botUIStrings = bots.ToList().ConvertAll(v => v.UIString);
                        int index = 0;
                        Dictionary<long, int> botIndexMap = new();
                        List<int> botIndexes = bots.ToList().ConvertAll(bot =>
                        {
                            botIndexMap[bot.id] = index;
                            return index++;
                        });
                        
                        for (int i = 1; i <= numBotsProp.intValue; i++)
                        {
                            try
                            {
                                if (botsSelected.arraySize < i)
                                {
                                    botsSelected.InsertArrayElementAtIndex(i - 1);
                                }

                                SerializedProperty botSelected = botsSelected.GetArrayElementAtIndex(i - 1);

                                var priorIndex = 0;
                                botIndexMap.TryGetValue(botSelected.longValue, out priorIndex);
                                    var indexSelected = EditorGUILayout.IntPopup($"Bot # {i}",
                                        priorIndex,
                                    botUIStrings.ToArray(), botIndexes.ToArray(),
                                    new GUILayoutOption[] { });
                                if (indexSelected > bots.Length)
                                {
                                    indexSelected = 0;
                                }
                                botSelected.longValue = bots[indexSelected].id;
                            }
                            catch (Exception ex)
                            {
                                // Solve why on first rendering after get this blows up but still renders fine
                                // the answer is that OnGUI calls this multiple times per frame :(
                                //Debug.LogException(ex);
                            }
                        }
                    }

                    if (numBotsProp.intValue != botsSelected.arraySize)
                    {
                        botsSelected.arraySize = numBotsProp.intValue;
                    }

                    settings.ApplyModifiedProperties();
                    userSettings.ApplyModifiedProperties();
                    if (EditorGUI.EndChangeCheck())
                    {
                        timeOfLastEdit = EditorApplication.timeSinceStartup;
                        if (priorHost != hostField.stringValue || priorUser != emailField.stringValue || priorPassword != passwordField.stringValue)
                        {
                            priorHost = null;
                            token = null;
                            priorPassword = null;
                            priorUser = null;
                        }
                        AssetDatabase.SaveAssets();
                        RGSettings.OptionsUpdated();
                        RGUserSettings.OptionsUpdated();
                        RGSettingsDynamicEnabler[] objects = GameObject.FindObjectsOfType<RGSettingsDynamicEnabler>(true);
                        foreach (RGSettingsDynamicEnabler rgSettingsDynamicEnabler in objects)
                        {
                            rgSettingsDynamicEnabler.OptionsUpdated();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[]
                    { "Number Of Bots", "Regression Games", "Email", "Password", "RG", "Bot Selection", "Overlay" })
            };

            return provider;
        }

        public static async Task<bool> Login(string user, string password)
        {
            priorUser = user;
            priorPassword = password;

            var tcs = new TaskCompletionSource<bool>();

            await rgServiceManager.Auth(priorUser, priorPassword, 
                responseToken => 
                {
                    token = responseToken;
                    bots = null;
                    tcs.SetResult(true);
                }, 
                f => 
                {
                    token = null;
                    bots = null;
                    tcs.SetResult(false);
                });

            await tcs.Task;
            return tcs.Task.Result;
        }
    }
#endif
}