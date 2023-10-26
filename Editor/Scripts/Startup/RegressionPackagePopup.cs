using System.IO;
using System.Threading.Tasks;
using RegressionGames;
using RegressionGames.Editor;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public class RegressionPackagePopup : EditorWindow
{
    private const string RGSetupCheck = "rgsetup";
    private Texture2D bannerImage;
    private static RegressionPackagePopup window;
    private static bool loggedIn = false;
    private static string email = "";
    private static string password = "";
    private static bool isSampleImportInProgress = false;
    private static AddRequest addRequest;
    private static ListRequest listRequest;
    private static string samplePath = "Samples~/ThirdPersonDemoURP";

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
            window = (RegressionPackagePopup)EditorWindow.GetWindowWithRect(typeof(RegressionPackagePopup), windowRect, true, "Welcome to Regression Games!");
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
        RenderQuickstartDocs();
        RenderSampleQuickstart();
    }

    private void RenderQuickstartDocs()
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
        GUI.Label(textRect, "Regression Games Setup Guide", h1Style);

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
        Rect demoInfoBoxRect = new Rect(10, 270, 550, 130);
        Color prevColor = GUI.color;
        GUI.color = new Color(100, 100, 100, 0.25f);
        GUI.Box(demoInfoBoxRect, "");
        GUI.color = prevColor;

        // Draw the sample title
        GUI.Label(new Rect(20, 280, 550, 20), "Third Person Demo", titleStyle);

        // Draw the description for the sample
        GUI.Label(new Rect(20, 310, 535, 20), "Explore a third-person character demo using Regression Game’s Unity SDK.", descriptionStyle);

        GUI.Label(new Rect(20, 326, 535, 20), "Ensure your project is set up with URP before opening the sample.", boldDescriptionStyle);

        // Draw the "Open Sample" button. Disable if not using URP
        GUI.enabled = IsURPEnabled();
        if (GUI.Button(new Rect(20, 360, 100, 30), "Open Sample"))
        {
            ImportSample();
            Close();
        }
        GUI.enabled = true;
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

    
    private void ImportSample()
    {
        string packageName = "gg.regression.unity.bots";
        string samplePath = "Samples~/ThirdPersonDemoURP";
        string destinationPath = "Assets/ThirdPersonDemoURP";

        // Construct the path to the sample within the package
        string packagePath = Path.Combine("Packages", packageName, samplePath);

        // Check if the package is an embedded or local package
        if (Directory.Exists(packagePath))
        {
            // The package is local or embedded, copy the sample to the project's Assets folder
            FileUtil.CopyFileOrDirectory(packagePath, destinationPath);
            AssetDatabase.Refresh();

            // Open the specific scene from the sample
            EditorSceneManager.OpenScene(Path.Combine(destinationPath, "Demo/Scenes/Playground.unity"));
        }
        else
        {
            // Handle the case where the package might be installed from the Unity Package Manager registry
            // This part is more complex and depends on how Unity packages and caches downloaded packages
            // For now we're assuming samples are embedded
            RGDebug.LogError("The sample could not be found or is not in an embedded or local package.");
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