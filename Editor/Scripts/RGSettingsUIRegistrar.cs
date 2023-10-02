using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.Types;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    /**
     * This class is coded strangely.  The editor ui handles a lot of things asynchronously/automatically and thus the refreshing and state is managed externally.
     * Making that interleave cleanly with asynchronous API calls was quite the party trick :)
     */
    public class RGSettingsUIRegistrar
    {

        private static RGServiceManager rgServiceManager = new RGServiceManager(); // editor, not game/scene so don't look for one, make one

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
                    EditorGUI.BeginChangeCheck();
                    
                    SerializedProperty hostField = settings.FindProperty("rgHostAddress");
                    hostField.stringValue = EditorGUILayout.TextField("RG Host URL", hostField.stringValue);
                    
                    SerializedProperty emailField = settings.FindProperty("email");
                    emailField.stringValue = EditorGUILayout.TextField("RG Email", emailField.stringValue);
                    SerializedProperty passwordField = settings.FindProperty("password");
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
                        await rgServiceManager.Auth(priorUser,
                            priorPassword, responseToken =>
                            {
                                token = responseToken;
                                bots = null;
                            }, f =>
                            {
                                token = null;
                                bots = null;
                            });
                    }

                    if (token != null && (bots == null || bots.Length == 0))
                    {
                        await rgServiceManager.GetBotsForCurrentUser(botList =>
                        {
                            bots = botList;
                        }, () =>
                        {
                            bots = null;
                        });
                    }

                    if (bots != null)
                    {
                        List<RGBot> unityBots = bots.ToList().FindAll(bot => bot.platform.Equals("UNITY"));
                        if (unityBots.Count > 0)
                        {
                            List<string> botNames = unityBots.ConvertAll(bot => "" + bot.id + " - " + bot.name);
                            List<int> botIds = unityBots.ConvertAll(bot => int.Parse(bot.id.ToString()));
                            for (int i = 1; i <= numBotsProp.intValue; i++)
                            {
                                try
                                {
                                    if (botsSelected.arraySize < i)
                                    {
                                        botsSelected.InsertArrayElementAtIndex(i - 1);
                                    }

                                    SerializedProperty botSelected = botsSelected.GetArrayElementAtIndex(i - 1);
                                    botSelected.intValue = EditorGUILayout.IntPopup($"Bot # {i}",
                                        botSelected.intValue,
                                        botNames.ToArray(), botIds.ToArray(),
                                        new GUILayoutOption[] { });
                                }
                                catch (Exception ex)
                                {
                                    // Solve why on first rendering after get this blows up but still renders fine
                                    // the answer is that OnGUI calls this multiple times per frame :(
                                    //Debug.LogException(ex);
                                }
                            }
                        }
                    }

                    if (numBotsProp.intValue != botsSelected.arraySize)
                    {
                        botsSelected.arraySize = numBotsProp.intValue;
                    }

                    settings.ApplyModifiedProperties();
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
    }
#endif
}
