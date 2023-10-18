using System.Threading.Tasks;
using RegressionGames;
using RegressionGames.Editor;
using UnityEngine;
using UnityEditor;

public class RegressionPackagePopup : EditorWindow
{
    private const string RGSetupCheck = "rgsetup";
    private Texture2D bannerImage;
    private static RegressionPackagePopup window;
    private static bool loggedIn = false;
    private static string email = "";
    private static string password = "";
    
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
    
    [MenuItem("Regression Games/Setup")]
    public static async void ShowWindow()
    {
        // attempt a login with saved credentials
        email = RGSettings.GetOrCreateSettings().GetEmail();
        password = RGSettings.GetOrCreateSettings().GetPassword();
        await Login();
        
        if (window == null)
        {
            Rect windowRect = new Rect(100, 100, 600, 600);
            window = (RegressionPackagePopup)EditorWindow.GetWindowWithRect(typeof(RegressionPackagePopup), windowRect, true, "Welcome to Regression!");
        }
        else
        {
            window.Focus();
        }
    }

    void OnGUI()
    {
        if (!loggedIn)
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
        GUILayout.Label("Welcome to Regression!", h1Style, GUILayout.Width(550));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        // Description
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(
            "Regression Games Unity Bots is an SDK that makes it easy to integrate powerful and useful bots into " +
            "your game. Build bots to QA test your game, act as NPCs, compete against players in multiplayer, and " +
            "determine optimal game balance. Our integration patterns, ready-to-go bots, and samples make it easy to " +
            "get started.", pStyle, GUILayout.Width(550));
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
            Login();
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
        // Get a space in the layout for the banner
        GUILayout.Space(120);

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
        GUI.Label(textRect, "Regression Setup Guide", h1Style);

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
        Rect infoBoxRect = new Rect(10, 130, 550, 130);
        prevColor = GUI.color;
        GUI.color = new Color(100, 100, 100, 0.25f);
        GUI.Box(infoBoxRect, "");
        GUI.color = prevColor;

        // Draw the quick start title
        GUI.Label(new Rect(20, 140, 550, 20), "Quick Start with Local Bots", titleStyle);

        // Draw the description
        GUI.Label(new Rect(20, 140, 550, 100), "Jump into creating your first bot using C# by following this Local Unity Bot guide. Regression Games Bots are flexible and highly customizable. Bots can simulate players, function as NPCs, interact with menus and UIs, validate gameplay, and more.", descriptionStyle);

        // Draw the docs button
        if (GUI.Button(new Rect(20, 220, 100, 30), "View Docs"))
        {
            Application.OpenURL("https://docs.regression.gg/studios/unity/unity-sdk/csharp/configuration");
        }

    }
    
    [InitializeOnLoadMethod]
    private static void InitializeOnLoadMethod()
    {
        bool hasShown = PlayerPrefs.HasKey(RGSetupCheck);
        if (!hasShown)
        { 
            EditorApplication.update += ShowOnStartup;
        }
    }

    private static void ShowOnStartup()
    {
        if (EditorApplication.timeSinceStartup > 3) // 3 seconds delay
        {
            PlayerPrefs.SetInt(RGSetupCheck, 1);
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

        var settings = RGSettings.GetOrCreateSettings();
        settings.SetEmail(email);
        settings.SetPassword(password);
        RGSettings.OptionsUpdated();

        loggedIn = await RGSettingsUIRegistrar.Login(email, password);
        if (window != null)
        {
            window.Repaint();
        }
    }
}