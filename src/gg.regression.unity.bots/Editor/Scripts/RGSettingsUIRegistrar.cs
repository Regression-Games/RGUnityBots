using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using RegressionGames.CodeCoverage;
using UnityEditor;
using UnityEditor.Compilation;
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

        [SettingsProvider]
        public static SettingsProvider CreateRGSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Regression Games", SettingsScope.Project)
            {
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    SerializedObject settings = RGSettings.GetSerializedSettings();
                    EditorGUI.BeginChangeCheck();

                    SerializedProperty hostField = settings.FindProperty("rgHostAddress");
                    hostField.stringValue = EditorGUILayout.TextField("RG Host URL", hostField.stringValue);

                    SerializedProperty apiKeyField = settings.FindProperty("apiKey");
                    apiKeyField.stringValue = EditorGUILayout.PasswordField("RG API Key", apiKeyField.stringValue);

                    EditorGUILayout.BeginHorizontal();
                    
                    // links to user account settings and the docs site
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Click here to access API keys on your account", EditorStyles.linkLabel))
                    {
                        Application.OpenURL("https://play.regression.gg/account");
                    }
                    GUILayout.Label("or", EditorStyles.whiteLabel);
                    if (GUILayout.Button("see API Key docs", EditorStyles.linkLabel))
                    {
                        Application.OpenURL("https://docs.regression.gg/core-concepts/authenticating-with-api-keys");
                    }
                    EditorGUILayout.EndHorizontal();

                    SerializedProperty logLevel = settings.FindProperty("logLevel");
                    logLevel.enumValueIndex = (int)(DebugLogLevel)EditorGUILayout.EnumPopup("Log Level", (DebugLogLevel)logLevel.enumValueIndex);

                    // ----------
                    DrawUILine((Color.gray + Color.black) / 2);
                    EditorGUILayout.LabelField("Experimental Features");

                    EditorGUILayout.HelpBox("The Code Coverage feature instruments the game build to enable recording covered code.\n" +
                                                    "This may impact the performance of the game when using the state recording and replay features.",
                        MessageType.Info);

                    SerializedProperty featureCodeCoverage = settings.FindProperty("feature_CodeCoverage");
                    bool featCodeCoverage = EditorGUILayout.Toggle("Code Coverage Recording", featureCodeCoverage.boolValue);
                    if (featCodeCoverage != featureCodeCoverage.boolValue)
                    {
                        // if the code coverage option is changed, request a script re-compilation since this affects the assemblies
                        featureCodeCoverage.boolValue = featCodeCoverage;
                        RGCodeCoverage.ClearMetadata();
                        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                    }

                    EditorGUILayout.HelpBox("REQUIRES: gg.regression.unity.bots.ecs package to be included in this project", MessageType.Info);

                    SerializedProperty featureCaptureEntityState = settings.FindProperty("feature_CaptureEntityState");
                    featureCaptureEntityState.boolValue =
                        EditorGUILayout.Toggle("Capture Entity States", featureCaptureEntityState.boolValue);

                    EditorGUILayout.HelpBox("Capturing component data values from ECS Entities may impact the performance \n" +
                                            "of the game when using the state recording and replay features.\n" +
                                            "REQUIRES: Capture Entity States - ENABLED\n" +
                                            "REQUIRES: gg.regression.unity.bots.ecs package to be included in this project",
                        MessageType.Info);

                    SerializedProperty featureCaptureEntityComponentData = settings.FindProperty("feature_CaptureEntityComponentData");
                    var priorLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 230;
                    featureCaptureEntityComponentData.boolValue =
                        EditorGUILayout.Toggle("Capture Entity Component Data States", featureCaptureEntityState.boolValue && featureCaptureEntityComponentData.boolValue);
                    EditorGUIUtility.labelWidth = priorLabelWidth;

                    settings.ApplyModifiedProperties();
                    if (EditorGUI.EndChangeCheck())
                    {
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

        public static void DrawUILine(Color color, int thickness = 2, int verticalPadding = 10, int horizontalPadding = 10)
        {
            var r = EditorGUILayout.GetControlRect(GUILayout.Height(verticalPadding + thickness));
            r.height = thickness;
            r.y += verticalPadding / 2.0f;
            r.x += horizontalPadding / 2.0f;
            r.width -= horizontalPadding;
            EditorGUI.DrawRect(r, color);
        }
    }
#endif
}
