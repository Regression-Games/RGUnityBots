using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
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

        private static RGServiceManager rgServiceManager = new(); // editor, not game/scene so don't look for one, make one

        private static string token = null;
        private static string priorUser = null;
        private static string priorPassword = null;
        private static string priorHost = null;

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

                    // ----------
                    DrawUILine((Color.gray + Color.black) / 2);
                    EditorGUILayout.LabelField("Experimental Features");

                    SerializedProperty featureStateRecordingAndReplay = settings.FindProperty("feature_StateRecordingAndReplay");
                    featureStateRecordingAndReplay.boolValue =
                        EditorGUILayout.Toggle("State Recording & Replay", featureStateRecordingAndReplay.boolValue);

                    if ((EditorApplication.timeSinceStartup - timeOfLastEdit) > 3f && token == null && priorPassword == null && priorHost == null && priorUser == null && passwordField.stringValue.Length > 4 && emailField.stringValue.Length > 4 && hostField.stringValue.Length > 4)
                    {
                        priorPassword = passwordField.stringValue;
                        priorUser = emailField.stringValue;
                        priorHost = hostField.stringValue;
                        await Login(priorUser, priorPassword);
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

        public static void DrawUILine(Color color, int thickness = 2, int verticalPadding = 10, int horizontalPadding = 10)
        {
            var r = EditorGUILayout.GetControlRect(GUILayout.Height(verticalPadding + thickness));
            r.height = thickness;
            r.y += verticalPadding / 2.0f;
            r.x += horizontalPadding / 2.0f;
            r.width -= horizontalPadding;
            EditorGUI.DrawRect(r, color);
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
                    tcs.SetResult(true);
                },
                f =>
                {
                    token = null;
                    tcs.SetResult(false);
                });

            await tcs.Task;
            return tcs.Task.Result;
        }
    }
#endif
}
