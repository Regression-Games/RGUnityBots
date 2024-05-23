using System;
using System.IO;
using System.Threading.Tasks;
using RegressionGames;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using RegressionGames.Editor;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
#endif

#if UNITY_EDITOR
public class RegressionPackagePopup : EditorWindow
{

    private const string RGUnityCheck = "rgunitycheck";
    private const string RGWindowCheck = "rgwindowcheck";
    private const string RGGuestCheck = "rgguest";
    private Texture2D bannerImage;
    private static RegressionPackagePopup window;
    private static bool loggedIn = false;
    private static string email = "";
    private static string password = "";
    private static bool requestingLogin = false;
    private static AddRequest addRequest;
    private static ListRequest listRequest;
    
    void OnEnable()
    {
        string packagePath = "Packages/gg.regression.unity.bots/Editor/Images/banner.png";
        string assetsPath = "Assets/Editor/Images/banner.png";

        bannerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath);

        if (bannerImage == null)
        {
            bannerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(assetsPath);
        }

        if (bannerImage == null)
        {
            Debug.LogWarning("Failed to load banner image");
        }
    }

    public static void ShowWindow(bool shouldShowLogin)
    {
        ShowWindow();
        if (shouldShowLogin)
        {
            requestingLogin = true;
        }
    }

    [MenuItem("Regression Games/Getting Started")]
    public static void ShowWindow()
    {
        if (window == null)
        {
            Rect windowRect = new Rect(100, 100, 600, 600);
            window = (RegressionPackagePopup)GetWindowWithRect(typeof(RegressionPackagePopup), windowRect, true, "Welcome to Regression Games!");
        }
        else
        {
            window.Focus();
        }
    }

    void OnGUI()
    {
        bool isGuest = PlayerPrefs.HasKey(RGGuestCheck);
        if ((!loggedIn && !isGuest) || requestingLogin)
        {
            RenderLoginScreen();
        }
        else
        {
            RenderWelcomeScreen();
        }
    }

    private void RenderLoginScreen()
    {
        if (bannerImage != null)
        {
            GUILayout.Box(bannerImage, GUILayout.ExpandWidth(true), GUILayout.Height(200));
        }

        // H1
        GUIStyle h1Style = new GUIStyle(EditorStyles.largeLabel);
        h1Style.fontSize = 20;
        h1Style.fontStyle = FontStyle.Bold;
        h1Style.normal.textColor = Color.white;

        // P
        GUIStyle pStyle = new GUIStyle(EditorStyles.label);
        pStyle.fontSize = 12;
        pStyle.wordWrap = true;

        // Title
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Welcome to Regression Games!", h1Style, GUILayout.Width(550));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        // Description
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            "This Regression Games SDK makes it easy to integrate powerful and useful bots into " +
            "your game. Build bots to QA test your game, act as NPCs, compete against players in multiplayer, and " +
            "determine optimal game balance, and more. Our integration patterns, ready-to-go bots (coming soon), and samples " +
            "will help you get started quickly.", pStyle, GUILayout.Width(550));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(20);
        // Email field
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        email = EditorGUILayout.TextField("Email:", email, GUILayout.Width(550));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Password field
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        password = EditorGUILayout.PasswordField("Password:", password, GUILayout.Width(550));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Login button
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Login", GUILayout.Width(550), GUILayout.Height(30)))
        {
            _ = Login();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // Continue as guest button
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Continue as Guest", GUILayout.Width(550), GUILayout.Height(30)))
        {
            ContinueAsGuest();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(20);

        // Create Account and Forgot Password Links
        GUIStyle linkStyle = new GUIStyle(EditorStyles.label);
        linkStyle.fontSize = 12;

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create Account", linkStyle))
        {
            Application.OpenURL("https://play.regression.gg/signup");
        }

        GUILayout.Space(10);

        // For mouse hover effect
        if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
        }

        if (GUILayout.Button("Forgot Password", linkStyle))
        {
            Application.OpenURL("https://play.regression.gg/forgot-password");
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // For mouse hover effect
        if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
        }
    }

    private void RenderWelcomeScreen()
    {
        // set flags on render as extra check against duplicated popups
        PlayerPrefs.SetInt(RGGuestCheck, 1);

        // render window info
        RenderBanner();
        RenderAlwaysShowOnStartupCheckbox();
        RenderQuickstartDocs();
        RenderSampleQuickstart();
        RenderLoginButtonOption();
        
    }

    private void RenderBanner()
    {
        Rect sourceRect = new Rect(0, 0.25f, 1, 0.6f);
        Rect destRect = new Rect(0, 0, 600, 120);

        GUI.DrawTextureWithTexCoords(destRect, bannerImage, sourceRect);

        // Overlay the banner with a faded black box
        Color prevColor = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, 600, 120), EditorGUIUtility.whiteTexture);
        GUI.color = prevColor;

        // Define the text style
        GUIStyle h1Style = new GUIStyle(EditorStyles.largeLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        // Determine the position and size for the label
        Rect textRect = new Rect(40, 50, 600, 25);

        // Draw the title text
        GUI.Label(textRect, "Regression Games Setup Guide", h1Style);
    }

    private void RenderAlwaysShowOnStartupCheckbox()
    {
        GUILayout.Space(120);

        // Get the current value from PlayerPrefs
        bool alwaysShowOnStartup = PlayerPrefs.GetInt(RGWindowCheck, 1) == 1;

        // Define the style for the label
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };

        // Define the position and size for the label
        Rect labelRect = new Rect(20, 130, 200, 20);

        // Draw the label
        GUI.Label(labelRect, "Always Show On Startup", labelStyle);

        // Calculate the position and size for the checkbox based on the label dimensions
        Rect checkboxRect = new Rect(labelRect.x + labelRect.width + 5, 130, 20, 20);

        // Draw the checkbox
        if (GUI.Button(checkboxRect, "", GUI.skin.box))
        {
            // Toggle the value when the checkbox is clicked
            alwaysShowOnStartup = !alwaysShowOnStartup;
            PlayerPrefs.SetInt(RGWindowCheck, alwaysShowOnStartup ? 1 : 0);
        }

        // Draw the checkmark if the checkbox is checked
        if (alwaysShowOnStartup)
        {
            GUI.Label(checkboxRect, " ✔", labelStyle);
        }

        // Add some spacing after the checkbox
        GUILayout.Space(30);
    }

    private void RenderQuickstartDocs()
    {
        // Get a space in the layout for the banner
        GUILayout.Space(120);

        // Define the styles
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        GUIStyle descriptionStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true
        };

        // Define the background box
        Rect infoBoxRect = new Rect(10, 160, 580, 130);
        Color prevColor = GUI.color;
        GUI.color = new Color(100, 100, 100, 0.25f);
        GUI.Box(infoBoxRect, "");
        GUI.color = prevColor;

        // Draw the quick start title
        GUI.Label(new Rect(20, 170, 580, 20), "Quick Start with Automated Recordings", titleStyle);

        // Draw the description
        GUI.Label(new Rect(20, 170, 580, 100), "Jump into creating your first first automated test using our Gameplay Session recording and playback system. Record gameplay, automatically extract game state, and use our web UI to build functional tests for your game.", descriptionStyle);

        // Draw the docs button
        if (GUI.Button(new Rect(20, 250, 100, 30), "View Docs"))
        {
            Application.OpenURL("https://docs.regression.gg/tutorials/building-your-first-bot");
        }
    }

    private void RenderSampleQuickstart()
    {
        // Define the styles for the sample section
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        GUIStyle descriptionStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true
        };

        GUIStyle boldDescriptionStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            fontStyle = FontStyle.Bold
        };

        // Define the background box for the new section
        Rect demoInfoBoxRect = new Rect(10, 300, 580, 130);
        Color prevColor = GUI.color;
        GUI.color = new Color(100, 100, 100, 0.25f);
        GUI.Box(demoInfoBoxRect, "");
        GUI.color = prevColor;

        // Draw the sample title
        GUI.Label(new Rect(20, 310, 580, 20), "Third Person Demo", titleStyle);

        // Draw the description for the sample
        GUI.Label(new Rect(20, 340, 565, 20), "Explore a third-person character demo using Regression Game’s Unity SDK.", descriptionStyle);

        GUI.Label(new Rect(20, 356, 565, 20), "Ensure your project is set up with URP before opening the sample.", boldDescriptionStyle);

        // Draw the "Open Sample" button. Disable if not using URP
        GUI.enabled = IsURPEnabled();
        if (GUI.Button(new Rect(20, 390, 100, 30), "Open Sample"))
        {
            ImportSample("ThirdPersonDemoURP");
            Close();
        }
        GUI.enabled = true;
    }

    private void RenderLoginButtonOption()
    {
        // Define the styles for the login button section
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        GUIStyle descriptionStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true
        };

        // Define the background box for the new section
        Rect demoInfoBoxRect = new Rect(10, 440, 580, 110);
        Color prevColor = GUI.color;
        GUI.color = new Color(100, 100, 100, 0.25f);
        GUI.Box(demoInfoBoxRect, "");
        GUI.color = prevColor;

        // Draw the login title
        GUI.Label(new Rect(20, 450, 580, 20), "Login", titleStyle);

        // Draw the description for logging in
        GUI.Label(new Rect(20, 480, 565, 20), "Login to Regression Games to utilize our features.", descriptionStyle);
        
        // Draw the "Login" button
        if (GUI.Button(new Rect(20, 510, 170, 30), "Login to Regression Games"))
        {
            requestingLogin = true;
        }
    }

    private bool IsURPEnabled()
    {
        var renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
        if (renderPipelineAsset != null)
        {
            var typeName = renderPipelineAsset.GetType().FullName;
            if (typeName.Contains("UniversalRenderPipelineAsset"))
            {
                return true;
            }
        }
        return false;
    }

    private void ImportSample(string sampleName)
    {
        string packageName = "gg.regression.unity.bots";
        string sampleDirectoryName = "Samples~";
        string assetsDirectoryName = Application.dataPath;
        string destinationDirectoryName = sampleName;

        // Construct the path to the sample within the package
        string packagePath = Path.Combine("Packages", packageName, sampleDirectoryName, sampleName).Replace("\\", "/");
        string destinationPath = Path.Combine(assetsDirectoryName, destinationDirectoryName).Replace("\\", "/");

        // Check if the package is an embedded or local package
        if (Directory.Exists(packagePath))
        {
            // The package is local or embedded, copy the sample to the project's Assets folder
            try
            {
                // Replaces sample during reimports
                if (Directory.Exists(destinationPath))
                {
                    FileUtil.DeleteFileOrDirectory(destinationPath);
                }

                // Copy Sample into Assets
                FileUtil.CopyFileOrDirectory(packagePath, destinationPath);
                AssetDatabase.Refresh();

                // Open Sample
                string scenePath = Path.Combine(destinationPath, "Demo", "Scenes", "Playground.unity").Replace("\\", "/");
                EditorSceneManager.OpenScene(scenePath);
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, "Failed to import sample");
            }
        }
        else
        {
            // Handle the case where the package might be installed from the Unity Package Manager registry
            RGDebug.LogError("The sample could not be found or is not in an embedded or local package.");
        }
    }

    [InitializeOnLoadMethod]
    private static void InitializeOnLoadMethod()
    {
        if (!SessionState.GetBool(RGUnityCheck, false))
        {
            SessionState.SetBool(RGUnityCheck, true);
            bool showOnStartup = PlayerPrefs.GetInt(RGWindowCheck, 1) == 1;
            if (showOnStartup)
            {
                EditorApplication.update += ShowOnStartup;
            }
        }
    }

    private static void ShowOnStartup()
    {
        if (EditorApplication.timeSinceStartup > 3)
        {
            EditorApplication.update -= ShowOnStartup;
            ShowWindow();
        }
    }

    private static async Task Login()
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("Email or password is empty.");
            return;
        }

        loggedIn = await RGSettingsUIRegistrar.Login(email, password);
        if (loggedIn)
        {
            requestingLogin = false;
        }
        if (window != null)
        {
            window.Repaint();
        }
    }

    private static void ContinueAsGuest()
    {

        requestingLogin = false;
        // check if already logged in
        if (loggedIn && window != null)
        {
            window.Repaint();
            return;
        }

        // continue without log in
        PlayerPrefs.SetInt(RGGuestCheck, 1);
        if (window != null)
        {
            window.Repaint();
        }
    }

}
#endif
