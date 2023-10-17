using RegressionGames;
using RegressionGames.Editor;
using UnityEngine;
using UnityEditor;

public class RegressionPackagePopup : EditorWindow
{
    static bool hasShown = false;
    private Texture2D bannerImage;
    private static RegressionPackagePopup window;
    private string email = "";
    private string password = "";
    
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
    public static void ShowWindow()
    {
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
        pStyle.fontSize = 12; // Adjust size as needed
        //pStyle.normal.textColor = Color.white; // You can adjust this color if needed
        pStyle.wordWrap = true; // This ensures the text wraps if it's too long

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

    [InitializeOnLoadMethod]
    private static void InitializeOnLoadMethod()
    {
        if (!hasShown)
        { 
            EditorApplication.update += ShowOnStartup;
        }
    }

    private static void ShowOnStartup()
    {
        if (EditorApplication.timeSinceStartup > 3) // 3 seconds delay
        {
            hasShown = true;
            EditorApplication.update -= ShowOnStartup;
            ShowWindow();
        }
    }
    
    private async void Login()
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

        await RGSettingsUIRegistrar.Login(email, password);
    }
}