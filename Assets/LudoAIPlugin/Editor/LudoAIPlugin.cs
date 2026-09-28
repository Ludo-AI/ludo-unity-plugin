using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.EditorCoroutines.Editor;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;
using Newtonsoft.Json.Linq;
using WebP;

public class LudoAIPlugin : EditorWindow
{
    // ============================
    // ======= CONFIGURATION =======
    // ============================
    private bool compilationFinished = false;

    // API key and URL for making requests to Ludo AI
    private string apiKey = ""; // API key loaded from EditorPrefs - user must input their own key
    private string apiUrl = "https://api.ludo.ai/api"; // Base URL for the API

    // Path to the logo image
    private string logoPath = "Assets/LudoAIPlugin/Editor/UnityLogo.png"; // Ensure this path is correct

    // ============================
    // ====== STATE VARIABLES =====
    // ============================

    // Status message displayed in the editor window
    private string statusMessage = "";

    // Flag to indicate if an operation is in progress
    private bool isProcessing = false;

    // Texture for displaying the logo in the editor window
    private Texture2D logoTexture;

    // Scroll positions for different scroll views
    private Vector2 projectsScrollPosition;
    private Vector2 scriptsScrollPosition;
    private Vector2 errorsScrollPosition;

    // List to store retrieved projects from the API
    private List<Project> projects = new List<Project>();

    // Array to hold project names for display in a dropdown list
    private string[] projectNamesArray;

    // Index of the currently selected project in the dropdown list
    private int selectedProjectIndex = -1;

    // Currently selected project
    private Project selectedProject = null;

    // List to store scripts for the selected project
    private List<ProjectCode> projectScripts = new List<ProjectCode>();

    // Prompt entered by the user to generate a new script
    private string scriptGenerationPrompt = "";

    // Flag to control the display of settings
    private bool showSettings = false;

    // List to hold scripts with detected errors
    private List<ScriptErrorInfo> errorsToFix = new List<ScriptErrorInfo>();

    // Flag to indicate if error handling is in progress
    private bool isErrorHandling = false;

    // 3D Models Panel Variables
    private bool show3DModelsPanel = false;
    private ShowcaseGallery showcaseGallery = null;
    private Dictionary<string, Texture2D> modelPreviewCache = new Dictionary<string, Texture2D>();
    private int selectedModelIndex = -1;
    private ThreeDAsset selectedModel = null;


    // Tab system for UI
    private enum TabState { Models, Sprites, Audio, Settings }
    private TabState currentTab = TabState.Sprites;

    // Model tab sub-state
    private enum ModelTabState { Create, Rig, Animate }
    private ModelTabState currentModelTab = ModelTabState.Create;

    // Sprite tab sub-state
    private enum SpriteTabState { Image, ImageToSpritesheet }
    private SpriteTabState currentSpriteTab = SpriteTabState.Image;

    // Audio tab sub-state
    private enum AudioTabState { SoundEffect, Music, Voice, Speech, SpeechPreset }
    private AudioTabState currentAudioTab = AudioTabState.SoundEffect;

    // Sprite Generation Variables
    private string spritePrompt = "";
    private string spriteMotionPrompt = "";
    private string spriteArtStyle = "Any style";
    private string spritePerspective = "Any perspective";
    private string spriteQuality = "high";
    private List<GeneratedSprite> generatedSprites = new List<GeneratedSprite>();
    private int selectedSpriteIndex = -1;
    private GeneratedSprite selectedSprite = null;
    private Dictionary<string, Texture2D> spritePreviewCache = new Dictionary<string, Texture2D>();
    
    // Predefined values from initial payload
    private List<string> availableGenerationStyles = new List<string>();
    private List<string> availablePerspectives = new List<string>();
    private int selectedArtStyleIndex = 0;
    private int selectedPerspectiveIndex = 0;
    private bool isLoadingInitialPayload = false;
    private bool initialPayloadLoaded = false;
    
    // Spritesheet Variables
    private string spritesheetMotionHint = "walking";
    private string spritesheetInitialImageUrl = ""; // Manual URL input for initial image
    private bool spritesheetLoop = true;
    private bool spritesheetCrop = false;
    private int spritesheetFrames = 8;
    private int spritesheetFrameSize = 512;
    private float spritesheetMarginRatio = 0.1f;
    private string spritesheetMarginMode = "auto"; // "auto", "manual", or "none"
    private string spritesheetPixelFilter = "none";
    private GeneratedSpritesheet currentSpritesheet = null;
    private Texture2D spritesheetPreviewTexture = null;
    
    // Spritesheet export options
    private readonly int[] frameOptions = { 4, 9, 16, 25, 36, 49, 64 };
    private readonly int[] frameSizeOptions = { 64, 128, 256, 0 }; // 0 = Max
    private readonly string[] frameSizeLabels = { "64x64", "128x128", "256x256", "Max" };
    private readonly string[] pixelArtFilterOptions = { "none", "small", "medium", "large" };
    private readonly string[] pixelArtFilterLabels = { "No filter", "Small pixels", "Medium size pixels", "Large pixels" };
    private readonly string[] marginModeOptions = { "auto", "manual", "none" };
    private readonly string[] marginModeLabels = { "Auto", "Manual", "No margin" };
    
    private Vector2 spritesScrollPosition;
    private Vector2 spritesheetScrollPosition;

    // ============================
    // === NEW FEATURE VARIABLES ==
    // ============================

    // Image Generation Variables
    private string imagePrompt = "";
    private string imageType = "screenshot";
    private string imageGenre = "Casual";
    private string imagePlatform = "Mobile";
    private string imageArtStyle = "Any style";
    private string imagePerspective = "Any perspective";
    private string imageAspectRatio = "default";
    private int imageCount = 1;
    private bool imageAugmentPrompt = true;
    private List<GeneratedImage> generatedImages = new List<GeneratedImage>();
    private int selectedImageIndex = -1;
    private GeneratedImage selectedImage = null;
    private Dictionary<string, Texture2D> imagePreviewCache = new Dictionary<string, Texture2D>();
    private Vector2 imagesScrollPosition;

    // Updated Spritesheet Animation Variables
    private string spritesheetModel = "standard";
    private float spritesheetDuration = 2.0f;
    private string spritesheetImageType = "sprite";
    private string spritesheetFinalImage = "";
    private bool spritesheetAugmentPrompt = true;

    // 3D Model Generation Variables
    private string model3DImageUrl = "";
    private int model3DTargetFaces = 50000;
    private int model3DTextureSize = 2048;
    private string model3DTextureType = "pbr";
    private Generated3DModel current3DModel = null;
    private List<Texture2D> model3DSnapshotTextures = new List<Texture2D>();
    private Vector2 model3DScrollPosition;

    // 3D Rig Variables
    private string model3DRigModelInput = "";
    private string model3DRigType = "general";
    private string model3DJointNaming = "mixamo";
    private Rigged3DModelResult currentRigged3DModel = null;
    private Vector2 model3DRigScrollPosition;

    // 3D Animate Variables
    private string model3DAnimateModelInput = "";
    private string model3DAnimatePrompt = "walking";
    private string model3DAnimateMode = "rot_trans";
    private int model3DAnimateNumVariants = 4;
    private bool model3DAnimateLoop = true;
    private bool model3DAnimateAugmentPrompt = true;
    private List<AnimationClip3D> current3DAnimations = new List<AnimationClip3D>();
    private Vector2 model3DAnimateScrollPosition;

    // Audio Generation Variables
    private string soundEffectDescription = "";
    private float soundEffectDuration = 0f;
    private bool soundEffectAugment = true;
    private GeneratedAudio currentSoundEffect = null;

    private string musicDescription = "";
    private string musicLyrics = "";
    private bool musicAugment = true;
    private GeneratedAudio currentMusic = null;

    private string voiceDescription = "";
    private string voiceText = "";
    private string voiceType = "human";
    private bool voiceAugment = true;
    private GeneratedAudio currentVoice = null;

    private string speechText = "";
    private string speechSample = "";
    private GeneratedAudio currentSpeech = null;

    private string speechPresetText = "";
    private string speechPresetVoiceId = "Serious woman";
    private string speechPresetEmotion = "Default";
    private string speechPresetLanguage = "auto";
    private GeneratedAudio currentSpeechPreset = null;

    private Vector2 audioScrollPosition;

    // Dropdown options for new features
    private readonly string[] imageTypeOptions = { "screenshot", "icon", "art", "asset", "sprite", "sprite-vfx", "ui_asset", "fixed_background", "texture", "3d", "generic" };
    private readonly string[] genreOptions = { "Hypercasual", "Casual", "Core", "Action", "Adventure", "Arcade", "Board", "Card", "Casino", "Education", "Family", "Fighting", "Games for Kids", "Music", "Puzzle", "Racing", "Role Playing", "Shooter", "Simulation", "Sports", "Strategy", "Trivia", "Word" };
    private readonly string[] platformOptions = { "Mobile", "Desktop", "Web" };
    private readonly string[] artStyleOptions = { "Any style", "Cartoonish", "Pixel Art (16-Bit)", "Low Poly", "Stylized 3D", "Flat Design", "Illustration", "Cel-Shaded", "Retro 2D", "Voxel Art", "Minimalist", "Hand-Painted", "Anime/Manga", "Vector Art", "Chibi", "Retro 3D", "Comic Book", "Silhouette", "Pixel Art (8-Bit)", "Photorealistic 3D" };
    private readonly string[] perspectiveOptions = { "Any perspective", "First-Person", "Third-Person", "Over-the-Shoulder", "Top-Down", "Isometric", "Side-Scroll", "Free Camera", "2.5D" };
    private readonly string[] aspectRatioOptions = { "default", "ar_1_1", "ar_4_3", "ar_16_9", "ar_19_9", "ar_3_4", "ar_9_16", "ar_9_19" };
    private readonly string[] aspectRatioLabels = { "Default", "1:1", "4:3", "16:9", "19:9", "3:4", "9:16", "9:19" };
    
    private readonly string[] spritesheetModelOptions = { "standard", "new" };
    private readonly string[] spritesheetImageTypeOptions = { "sprite", "sprite-vfx", "ui_asset" };
    
    private readonly int[] textureSizeOptions = { 1024, 2048 };
    private readonly string[] textureTypeOptions = { "pbr", "simple", "none" };
    private readonly string[] model3DRigTypeOptions = { "general", "humanoid", "game", "humanoid_template", "humanoid_template_hands" };
    private readonly string[] model3DJointNamingOptions = { "mixamo", "smpl", "humanik", "unreal", "godot", "rigify", "vroid" };
    private readonly string[] model3DAnimateModeOptions = { "rot_trans", "rot_only" };
    private readonly string[] model3DAnimateModeLabels = { "Rotation + Translation", "Rotation Only (retargeting)" };
    
    private readonly string[] voiceTypeOptions = { "human", "non-human" };
    private readonly string[] voicePresetOptions = { "Serious woman", "Wise woman", "Calm woman", "Fast-paced woman", "Calm young girl", "Expressive teen girl", "Calm teen girl", "Sweet girl", "Patient man", "Determined man", "Young elegant man", "Teen boy", "Friendly man", "Deep voice man" };
    private readonly string[] emotionOptions = { "Default", "Happy", "Sad", "Angry", "Fearful", "Disgusted", "Surprised", "Neutral" };
    private readonly string[] languageOptions = { "auto", "English", "Afrikaans", "Arabic", "Bulgarian", "Catalan", "Chinese", "Chinese,Yue", "Croatian", "Czech", "Danish", "English", "Filipino", "Finnish", "French", "German", "Greek", "Hebrew", "Hindi", "Hungarian", "Indonesian", "Italian", "Japanese", "Korean", "Malay", "Norwegian", "Nynorsk", "Persian", "Polish", "Portuguese", "Romanian", "Russian", "Slovak", "Slovenian", "Spanish", "Swedish", "Tamil", "Thai", "Turkish", "Ukrainian", "Vietnamese" };

    // ============================
    // ======= INIT & GUI =========
    // ============================

    // Add a menu item for the Ludo AI plugin in the Unity Editor
    [MenuItem("Ludo AI/Ludo AI Plugin")]
    public static void ShowWindow()
    {
        // Open the editor window with the title "Ludo AI"
        GetWindow<LudoAIPlugin>("Ludo AI");
    }

    // Helper method to check if current Unity version supports WebP
    private bool SupportsWebP()
    {
        string version = Application.unityVersion;
        // Unity 2021.2.0 and later support WebP
        // Parse version string (format: major.minor.patch)
        string[] parts = version.Split('.');
        if (parts.Length >= 2)
        {
            int major = int.Parse(parts[0]);
            int minor = int.Parse(parts[1]);

            // Unity 2021.2+ supports WebP
            if (major > 2021 || (major == 2021 && minor >= 2))
            {
                return true;
            }
        }
        return false;
    }

    // Helper method to decode WebP image data using the unity.webp library
    private Texture2D DecodeWebPImage(byte[] webpData)
    {
        try
        {
            // Use the unity.webp library to decode WebP images
            WebP.Error lError;
            Texture2D texture = Texture2DExt.CreateTexture2DFromWebP(webpData, lMipmaps: false, lLinear: false, lError: out lError, scalingFunction: null, makeNoLongerReadable: false);
            
            if (lError == WebP.Error.Success && texture != null)
            {
                Debug.Log($"[LudoAIPlugin] Successfully decoded WebP using unity.webp library.");
                return texture;
            }
            else
            {
                Debug.LogWarning($"[LudoAIPlugin] unity.webp library failed to decode WebP: {lError}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LudoAIPlugin] unity.webp decode exception: {ex.Message}");
        }
        
        // Fallback: try Unity's built-in LoadImage
        try
        {
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(webpData))
            {
                Debug.Log($"[LudoAIPlugin] Successfully decoded WebP using Unity's built-in decoder.");
                return texture;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LudoAIPlugin] Unity built-in WebP decode also failed: {ex.Message}");
        }
        return null;
    }

    // This method is called when the editor window is enabled
    private void OnEnable()
    {
        // Load the Unity logo image from the specified path
        logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);

        // Load the saved API key from Editor Preferences, if available
        apiKey = EditorPrefs.GetString("LudoAI_API_Key", ""); // Default value is empty

        // Register the callback to listen for console messages
        Application.logMessageReceived += OnLogMessageReceived;

        // Register the callback for assembly compilation finished
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

        // Fetch initial payload with predefined values if API key is set
        if (!string.IsNullOrEmpty(apiKey))
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(FetchInitialPayload());
        }
    }

    // This method is called when the editor window is disabled or closed
    private void OnDisable()
    {
        // Unregister the callbacks when the editor window is closed or disabled
        Application.logMessageReceived -= OnLogMessageReceived;
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
    }

    // Load default art styles and perspectives
    private IEnumerator FetchInitialPayload()
    {
        if (isLoadingInitialPayload || initialPayloadLoaded)
        {
            yield break;
        }

        isLoadingInitialPayload = true;

        availableGenerationStyles = new List<string>
        {
            "pixel art", "hand-drawn", "cartoon", "realistic", "anime",
            "low poly", "isometric", "flat design", "sketch", "watercolor",
            "3D render", "voxel", "minimalist", "retro", "cel-shaded"
        };

        availablePerspectives = new List<string>
        {
            "side view", "top-down", "isometric", "front view", "3/4 view",
            "bird's eye", "perspective", "orthographic", "45-degree"
        };

        int styleIndex = availableGenerationStyles.IndexOf(spriteArtStyle);
        selectedArtStyleIndex = styleIndex >= 0 ? styleIndex : 0;
        if (selectedArtStyleIndex < availableGenerationStyles.Count)
            spriteArtStyle = availableGenerationStyles[selectedArtStyleIndex];

        int perspectiveIndex = availablePerspectives.IndexOf(spritePerspective);
        selectedPerspectiveIndex = perspectiveIndex >= 0 ? perspectiveIndex : 0;
        if (selectedPerspectiveIndex < availablePerspectives.Count)
            spritePerspective = availablePerspectives[selectedPerspectiveIndex];

        initialPayloadLoaded = true;
        isLoadingInitialPayload = false;
        Repaint();

        yield break;
    }

    // This method is called to render the GUI elements in the editor window
    private void OnGUI()
    {
        GUILayout.Space(10);

        // Display the logo if it's loaded successfully
        if (logoTexture)
        {
            GUILayout.Label(logoTexture, GUILayout.Width(200), GUILayout.Height(100));
        }

        GUILayout.Space(10);

        // If API key is not set, display the API key input
        if (string.IsNullOrEmpty(apiKey))
        {
            ShowAPIKeyInput();
        }
        else
        {
            // Display tabs for Scripts, Models, Sprites, and Settings
            DrawTabBar();

            switch (currentTab)
            {
                case TabState.Models:
                    Draw3DModelsTab();
                    break;
                case TabState.Sprites:
                    DrawSpritesTab();
                    break;
                case TabState.Audio:
                    DrawAudioTab();
                    break;
                case TabState.Settings:
                    ShowAPIKeyInput();
                    break;
            }
        }

        GUILayout.FlexibleSpace();

        // Footer button to visit the Ludo AI website
        if (GUILayout.Button("Open Ludo AI Account", GUILayout.Width(200)))
        {
            Application.OpenURL("https://app.ludo.ai");
        }
    }

    // Draw the tab bar for navigation
    private void DrawTabBar()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Toggle(currentTab == TabState.Sprites, "Sprites & Images", EditorStyles.toolbarButton))
            currentTab = TabState.Sprites;

        if (GUILayout.Toggle(currentTab == TabState.Models, "3D Models", EditorStyles.toolbarButton))
            currentTab = TabState.Models;

        if (GUILayout.Toggle(currentTab == TabState.Audio, "Audio", EditorStyles.toolbarButton))
            currentTab = TabState.Audio;

        if (GUILayout.Toggle(currentTab == TabState.Settings, "Settings", EditorStyles.toolbarButton))
            currentTab = TabState.Settings;

        GUILayout.EndHorizontal();

        GUILayout.Space(10);
    }

    // Show API Key input UI
    private void ShowAPIKeyInput()
    {
        GUILayout.Label("Ludo AI API Key Configuration", EditorStyles.boldLabel);
        GUILayout.Space(5);
        
        // Show current API key status
        if (string.IsNullOrEmpty(apiKey))
        {
            EditorGUILayout.HelpBox("⚠️ API Key is not set. You need to enter your API key to use this plugin.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox($"✓ API Key is set (Length: {apiKey.Length} characters)", MessageType.Info);
        }
        
        GUILayout.Space(5);
        
        // Help text
        GUILayout.Label("Get your API key from the Ludo AI dashboard:", EditorStyles.wordWrappedLabel);
        if (GUILayout.Button("Open Ludo AI Website", GUILayout.Height(25)))
        {
            Application.OpenURL("https://ludo.ai");
        }
        
        GUILayout.Space(10);
        
        // Input field for entering the Ludo AI API key
        GUILayout.Label("Enter your Ludo AI API Key:");
        apiKey = EditorGUILayout.TextField(apiKey);

        GUILayout.Space(5);

        // Buttons
        GUILayout.BeginHorizontal();
        
        // Button to save the entered API key
        if (GUILayout.Button("Save API Key", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                EditorUtility.DisplayDialog("Ludo AI Plugin", "Please enter an API key before saving.", "OK");
            }
            else
            {
                // Save the API key in Editor Preferences
                EditorPrefs.SetString("LudoAI_API_Key", apiKey);
                EditorUtility.DisplayDialog("Ludo AI Plugin", "API Key saved successfully!\n\nYou can now use the plugin to retrieve projects and generate content.", "OK");
                showSettings = false; // Hide settings after saving
                Debug.Log("[LudoAIPlugin] API Key saved successfully.");
            }
        }
        
        // Button to clear the API key
        if (GUILayout.Button("Clear API Key", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Ludo AI Plugin", "Are you sure you want to clear the saved API key?", "Yes", "No"))
            {
                apiKey = "";
                EditorPrefs.SetString("LudoAI_API_Key", "");
                Debug.Log("[LudoAIPlugin] API Key cleared.");
            }
        }
        
        GUILayout.EndHorizontal();
    }

    // Draw the Scripts tab content
    // Draw the 3D Models tab content
    private void Draw3DModelsTab()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentModelTab == ModelTabState.Create, "Create 3D", EditorStyles.toolbarButton))
            currentModelTab = ModelTabState.Create;
        if (GUILayout.Toggle(currentModelTab == ModelTabState.Rig, "Rig Model (BETA)", EditorStyles.toolbarButton))
            currentModelTab = ModelTabState.Rig;
        if (GUILayout.Toggle(currentModelTab == ModelTabState.Animate, "Animate 3D (BETA)", EditorStyles.toolbarButton))
            currentModelTab = ModelTabState.Animate;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        switch (currentModelTab)
        {
            case ModelTabState.Create:
                DrawCreate3DModelUI();
                break;
            case ModelTabState.Rig:
                DrawRig3DModelUI();
                break;
            case ModelTabState.Animate:
                DrawAnimate3DModelUI();
                break;
        }
    }

    private void DrawBetaBadge()
    {
        Color prevBg = GUI.backgroundColor;
        Color prevContent = GUI.contentColor;
        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
        GUI.contentColor = Color.black;
        GUILayout.Label(" BETA ", EditorStyles.miniButton);
        GUI.backgroundColor = prevBg;
        GUI.contentColor = prevContent;
    }

    private void DrawCreate3DModelUI()
    {
        GUILayout.Label("Create 3D Model from Image", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Option to select image from project
        GUILayout.BeginHorizontal();
        GUILayout.Label("Image URL or Base64:");
        if (GUILayout.Button("Select from Project", GUILayout.Width(150)))
        {
            string path = EditorUtility.OpenFilePanel("Select Image", "Assets", "png,jpg,jpeg,webp");
            if (!string.IsNullOrEmpty(path))
            {
                string dataUri = LoadImageFileAsDataUri(path);
                if (!string.IsNullOrEmpty(dataUri))
                {
                    model3DImageUrl = dataUri;
                    statusMessage = "Image loaded from project (normalized for API)";
                    Debug.Log($"[LudoAIPlugin] Loaded image from: {path}");
                }
            }
        }
        GUILayout.EndHorizontal();
        
        model3DImageUrl = EditorGUILayout.TextField(model3DImageUrl);

        GUILayout.Space(10);

        GUILayout.Label($"Target Face Count: {model3DTargetFaces}");
        model3DTargetFaces = (int)EditorGUILayout.Slider(model3DTargetFaces, 1000, 200000);

        GUILayout.Space(10);

        if (model3DTextureSize != 1024 && model3DTextureSize != 2048)
        {
            model3DTextureSize = 2048;
        }
        int textureSizeIndex = System.Array.IndexOf(textureSizeOptions, model3DTextureSize);
        if (textureSizeIndex < 0) textureSizeIndex = 1;
        textureSizeIndex = EditorGUILayout.Popup("Texture Size:", textureSizeIndex, new string[] { "1024x1024", "2048x2048" });
        model3DTextureSize = textureSizeOptions[textureSizeIndex];

        GUILayout.Space(10);

        int textureTypeIndex = System.Array.IndexOf(textureTypeOptions, model3DTextureType);
        if (textureTypeIndex < 0) textureTypeIndex = 0;
        textureTypeIndex = EditorGUILayout.Popup("Texture Type:", textureTypeIndex, textureTypeOptions);
        model3DTextureType = textureTypeOptions[textureTypeIndex];

        GUILayout.Space(10);
        GUILayout.Label("API accepts texture sizes 1024 or 2048 only. Generation can take several minutes.", EditorStyles.helpBox);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(model3DImageUrl);
        if (GUILayout.Button("Create 3D Model", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Creating 3D model from image... This can take several minutes.";
            EditorCoroutineUtility.StartCoroutineOwnerless(Create3DModel());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        // Display created 3D model
        if (current3DModel != null)
        {
            GUILayout.Label("Created 3D Model:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");

            GUILayout.Label($"Model URL: {current3DModel.ModelUrl}");

            GUILayout.Space(10);

            // Display snapshot previews if available
            if (model3DSnapshotTextures.Count > 0)
            {
                GUILayout.Label("Preview Snapshots:", EditorStyles.boldLabel);
                
                model3DScrollPosition = GUILayout.BeginScrollView(model3DScrollPosition, GUILayout.Height(200));
                
                GUILayout.BeginHorizontal();
                foreach (var snapshot in model3DSnapshotTextures)
                {
                    GUILayout.Label(snapshot, GUILayout.Width(150), GUILayout.Height(150));
                }
                GUILayout.EndHorizontal();
                
                GUILayout.EndScrollView();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Download 3D Model", GUILayout.Height(30)))
            {
                Save3DModelToFile(current3DModel);
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Use for Rigging", GUILayout.Height(30)))
            {
                model3DRigModelInput = current3DModel.ModelUrl;
                currentModelTab = ModelTabState.Rig;
                statusMessage = "Created model loaded into Rig tab. Choose rig options and click Rig Model.";
            }

            GUILayout.EndVertical();
        }
    }

    private void DrawRig3DModelUI()
    {
        model3DRigScrollPosition = GUILayout.BeginScrollView(model3DRigScrollPosition);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Rig 3D Model", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        DrawBetaBadge();
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "BETA — Rigging is an early preview. Results can vary by mesh shape, and quality is still being improved. Not a final production feature yet.",
            MessageType.Warning);
        GUILayout.Space(5);
        GUILayout.Label(
            "Add a skeleton and skin weights to an existing GLB so it can be animated. Use a model from Create, pick a project GLB, or paste a URL.",
            EditorStyles.helpBox);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Model URL or Base64 GLB:");
        if (GUILayout.Button("Select from Project", GUILayout.Width(150)))
        {
            string loaded = LoadGlbFromProjectAsDataUri();
            if (!string.IsNullOrEmpty(loaded))
            {
                model3DRigModelInput = loaded;
                statusMessage = "GLB loaded from project for rigging";
            }
        }
        GUILayout.EndHorizontal();

        model3DRigModelInput = EditorGUILayout.TextField(model3DRigModelInput);

        if (current3DModel != null && !string.IsNullOrEmpty(current3DModel.ModelUrl))
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Created 3D model ready to use", EditorStyles.helpBox);
            if (GUILayout.Button("Use Created Model", GUILayout.Height(28)))
            {
                model3DRigModelInput = current3DModel.ModelUrl;
                statusMessage = "Using created model URL for rigging";
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(10);

        int rigTypeIndex = Array.IndexOf(model3DRigTypeOptions, model3DRigType);
        if (rigTypeIndex < 0) rigTypeIndex = 0;
        rigTypeIndex = EditorGUILayout.Popup("Rig Type:", rigTypeIndex, model3DRigTypeOptions);
        model3DRigType = model3DRigTypeOptions[rigTypeIndex];

        if (model3DRigType == "humanoid_template" || model3DRigType == "humanoid_template_hands")
        {
            GUILayout.Label("Template rigs suit two-armed, two-legged characters and are required for animation presets.", EditorStyles.helpBox);
        }

        GUILayout.Space(5);

        int namingIndex = Array.IndexOf(model3DJointNamingOptions, model3DJointNaming);
        if (namingIndex < 0) namingIndex = 0;
        namingIndex = EditorGUILayout.Popup("Joint Naming:", namingIndex, model3DJointNamingOptions);
        model3DJointNaming = model3DJointNamingOptions[namingIndex];

        if (model3DJointNaming == "mixamo")
        {
            GUILayout.Label("Mixamo naming maps well to Unity's Humanoid avatar auto-mapper.", EditorStyles.helpBox);
        }

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(model3DRigModelInput);
        if (GUILayout.Button("Rig Model", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Rigging 3D model... This can take several minutes.";
            EditorCoroutineUtility.StartCoroutineOwnerless(Rig3DModel());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentRigged3DModel != null)
        {
            GUILayout.Label("Rigged 3D Model:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");

            GUILayout.Label($"Model URL: {currentRigged3DModel.ModelUrl}");
            GUILayout.Label($"Rigged: {currentRigged3DModel.Rigged}");

            GUILayout.Space(10);

            GUI.enabled = !string.IsNullOrEmpty(currentRigged3DModel.ModelUrl);
            if (GUILayout.Button("Download Rigged Model", GUILayout.Height(30)))
            {
                SaveGlbFromUrl(currentRigged3DModel.ModelUrl, "Rigged3DModel");
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Use for Animation", GUILayout.Height(30)))
            {
                model3DAnimateModelInput = currentRigged3DModel.ModelUrl;
                currentModelTab = ModelTabState.Animate;
                statusMessage = "Rigged model loaded into Animate tab. Enter a motion prompt and click Animate 3D Model.";
            }
            GUI.enabled = true;

            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }

    private void DrawAnimate3DModelUI()
    {
        model3DAnimateScrollPosition = GUILayout.BeginScrollView(model3DAnimateScrollPosition);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Animate 3D Model", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        DrawBetaBadge();
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "BETA — 3D animation is an early preview. Motion quality can be hit-or-miss (multiple candidates are returned so you can pick the best). Not a final production feature yet.",
            MessageType.Warning);
        GUILayout.Space(5);
        GUILayout.Label(
            "Generate text-driven skeletal animations for an already-rigged GLB. Returned clips are animation-only (skeleton + clip, no mesh) — download the rigged model and fuse clips in Unity.",
            EditorStyles.helpBox);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Rigged Model URL or Base64 GLB:");
        if (GUILayout.Button("Select from Project", GUILayout.Width(150)))
        {
            string loaded = LoadGlbFromProjectAsDataUri();
            if (!string.IsNullOrEmpty(loaded))
            {
                model3DAnimateModelInput = loaded;
                statusMessage = "GLB loaded from project for animation";
            }
        }
        GUILayout.EndHorizontal();

        model3DAnimateModelInput = EditorGUILayout.TextField(model3DAnimateModelInput);

        if (currentRigged3DModel != null && !string.IsNullOrEmpty(currentRigged3DModel.ModelUrl))
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Rigged model ready to use", EditorStyles.helpBox);
            if (GUILayout.Button("Use Rigged Model", GUILayout.Height(28)))
            {
                model3DAnimateModelInput = currentRigged3DModel.ModelUrl;
                statusMessage = "Using rigged model URL for animation";
            }
            GUILayout.EndVertical();
        }
        else if (current3DModel != null && !string.IsNullOrEmpty(current3DModel.ModelUrl))
        {
            GUILayout.Label("Tip: Rig your created model first for best results, then animate.", EditorStyles.helpBox);
        }

        GUILayout.Space(10);

        GUILayout.Label("Motion Prompt:");
        model3DAnimatePrompt = EditorGUILayout.TextField(model3DAnimatePrompt);

        GUILayout.Space(5);

        int modeIndex = Array.IndexOf(model3DAnimateModeOptions, model3DAnimateMode);
        if (modeIndex < 0) modeIndex = 0;
        modeIndex = EditorGUILayout.Popup("Mode:", modeIndex, model3DAnimateModeLabels);
        model3DAnimateMode = model3DAnimateModeOptions[modeIndex];

        GUILayout.Space(5);

        GUILayout.Label($"Variants: {model3DAnimateNumVariants}");
        model3DAnimateNumVariants = (int)EditorGUILayout.Slider(model3DAnimateNumVariants, 1, 8);

        GUILayout.Space(5);

        model3DAnimateLoop = EditorGUILayout.Toggle("Loop Animation", model3DAnimateLoop);
        model3DAnimateAugmentPrompt = EditorGUILayout.Toggle("Augment Prompt", model3DAnimateAugmentPrompt);

        if (model3DAnimateLoop)
        {
            GUILayout.Label("Loop mirrors the clip back to the rest pose. Best for one-way motions; cyclic gaits like walking may look odd.", EditorStyles.helpBox);
        }

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(model3DAnimateModelInput) && !string.IsNullOrEmpty(model3DAnimatePrompt);
        if (GUILayout.Button("Animate 3D Model", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Animating 3D model... This can take several minutes.";
            EditorCoroutineUtility.StartCoroutineOwnerless(Animate3DModel());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (current3DAnimations != null && current3DAnimations.Count > 0)
        {
            GUILayout.Label($"Animation Candidates ({current3DAnimations.Count}):", EditorStyles.boldLabel);
            GUILayout.Label(
                "Each candidate is an animation-only GLB. Download the ones you like and use them with your rigged mesh.",
                EditorStyles.helpBox);

            GUILayout.Space(5);

            if (GUILayout.Button("Download All Animation GLBs", GUILayout.Height(28)))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAll3DAnimations());
            }

            GUILayout.Space(5);

            for (int i = 0; i < current3DAnimations.Count; i++)
            {
                AnimationClip3D clip = current3DAnimations[i];
                GUILayout.BeginVertical("box");

                string title = !string.IsNullOrEmpty(clip.ClipName) ? clip.ClipName : $"Variant {i + 1}";
                GUILayout.Label(title, EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(clip.Prompt))
                {
                    GUILayout.Label($"Prompt: {clip.Prompt}");
                }

                if (!string.IsNullOrEmpty(clip.Mode))
                {
                    GUILayout.Label($"Mode: {clip.Mode}");
                }

                if (clip.Seed != 0 || clip.Motion != 0f || clip.FitRmse != 0f)
                {
                    GUILayout.Label($"Seed: {clip.Seed}  |  Motion: {clip.Motion:F3}  |  Fit RMSE: {clip.FitRmse:F3}");
                }

                GUILayout.BeginHorizontal();

                GUI.enabled = !string.IsNullOrEmpty(clip.PreviewUrl);
                if (GUILayout.Button("Open Preview", GUILayout.Height(26)))
                {
                    Application.OpenURL(clip.PreviewUrl);
                }

                GUI.enabled = !string.IsNullOrEmpty(clip.GlbUrl);
                if (GUILayout.Button("Download Animation GLB", GUILayout.Height(26)))
                {
                    string safeName = SanitizeFileName(title);
                    SaveGlbFromUrl(clip.GlbUrl, $"Anim3D_{safeName}");
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        GUILayout.EndScrollView();
    }

    // Coroutine to import the selected generated model
    private IEnumerator ImportSelectedModel()
    {
        if (selectedModel == null || selectedModel.Asset == null)
        {
            statusMessage = "No valid 3D model selected.";
            Debug.LogWarning("[LudoAIPlugin] No valid 3D model selected.");
            isProcessing = false;
            yield break;
        }

        string modelsDirectory = "Assets/3DModels";
        if (!Directory.Exists(modelsDirectory))
        {
            Directory.CreateDirectory(modelsDirectory);
            AssetDatabase.Refresh();
        }

        string endpoint = "/games/generate/download-3d";
        string fullUrl = apiUrl + endpoint;
        string requestId = Guid.NewGuid().ToString();
        string format = "glb";

        var requestData = new Dictionary<string, object>
        {
            ["format"] = format,
            ["request_id"] = requestId,
            ["asset_generation"] = selectedModel.Asset
        };

        var jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData, jsonSettings);
        Debug.Log($"[LudoAIPlugin] Request payload: {jsonPayload}");

        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

        // Create a web request
        UnityWebRequest www = new UnityWebRequest(fullUrl, "POST");
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("Authorization", "ApiKey " + apiKey);
        www.SetRequestHeader("Content-Type", "application/json");
        www.uploadHandler = new UploadHandlerRaw(payloadBytes);
        www.downloadHandler = new DownloadHandlerBuffer();

        // Set a reasonable timeout
        www.timeout = 300;

        statusMessage = "Downloading 3D model...";
        Debug.Log($"[LudoAIPlugin] Sending download request for asset ID: {selectedModel.Asset.Id}");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error downloading 3D model: {www.error}";
            Debug.LogError($"[LudoAIPlugin] Error downloading 3D model: {www.error}");
            if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                Debug.LogError($"[LudoAIPlugin] Response details: {www.downloadHandler.text}");
            }
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to download 3D model.\n\nError: {www.error}", "OK");
        }
        else
        {
            try
            {
                // Get the content type
                string contentType = www.GetResponseHeader("Content-Type") ?? "unknown";
                Debug.Log($"[LudoAIPlugin] Content-Type: {contentType}");

                // Process JSON response
                string jsonResponse = www.downloadHandler.text;

                // Truncate long response for logging
                string logResponse = jsonResponse;
                if (logResponse.Length > 200)
                {
                    logResponse = logResponse.Substring(0, 200) + "...";
                }
                Debug.Log($"[LudoAIPlugin] JSON Response: {logResponse}");

                try
                {
                    // Try to parse as JSON
                    Dictionary<string, object> responseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

                    byte[] modelData = null;

                    // Check if there's a base64-encoded data field
                    if (responseDict.ContainsKey("data"))
                    {
                        string base64Data = responseDict["data"].ToString();

                        // Decode the base64 string to binary data
                        modelData = Convert.FromBase64String(base64Data);
                        Debug.Log($"[LudoAIPlugin] Successfully decoded base64 data, size: {modelData.Length} bytes");

                        // Check if it starts with the GLB magic header "glTF"
                        if (modelData.Length > 4 &&
                            modelData[0] == 'g' && modelData[1] == 'l' &&
                            modelData[2] == 'T' && modelData[3] == 'F')
                        {
                            Debug.Log("[LudoAIPlugin] Data has valid GLB header");
                        }
                        else
                        {
                            // Log the first few bytes for debugging
                            string hexBytes = BitConverter.ToString(modelData.Take(16).ToArray());
                            Debug.LogWarning($"[LudoAIPlugin] Data doesn't have GLB header. First 16 bytes: {hexBytes}");
                        }
                    }
                    else if (responseDict.ContainsKey("download_url") || responseDict.ContainsKey("url"))
                    {
                        // Use download URL if provided
                        string downloadUrl = responseDict.ContainsKey("download_url")
                            ? responseDict["download_url"].ToString()
                            : responseDict["url"].ToString();

                        Debug.Log($"[LudoAIPlugin] Found download URL in response: {downloadUrl}");

                        // Download from URL
                        statusMessage = "Model ready - downloading from URL...";
                        EditorCoroutineUtility.StartCoroutine(DownloadModelFromUrl(downloadUrl, modelsDirectory, format), this);
                        isProcessing = false;
                        yield break;
                    }
                    else if (responseDict.ContainsKey("message"))
                    {
                        // Display message from server
                        string message = responseDict["message"].ToString();
                        statusMessage = $"Server message: {message}";
                        Debug.LogWarning($"[LudoAIPlugin] Server returned message: {message}");
                        EditorUtility.DisplayDialog("Ludo AI Plugin", $"Server message: {message}", "OK");
                        isProcessing = false;
                        yield break;
                    }
                    else
                    {
                        // No recognized fields
                        statusMessage = "Received unexpected JSON response format";
                        Debug.LogWarning("[LudoAIPlugin] JSON response doesn't contain expected fields");
                        EditorUtility.DisplayDialog("Ludo AI Plugin Error", "Received unexpected response format from the server", "OK");
                        isProcessing = false;
                        yield break;
                    }

                    // Save the model data to a file
                    if (modelData != null && modelData.Length > 0)
                    {
                        string modelName = $"Model_{selectedModel.Asset.Id}_{DateTime.Now:yyyyMMdd_HHmmss}";
                        string filePath = Path.Combine(modelsDirectory, $"{modelName}.{format}");

                        File.WriteAllBytes(filePath, modelData);
                        AssetDatabase.Refresh();

                        statusMessage = $"3D model imported successfully to {filePath}";
                        Debug.Log($"[LudoAIPlugin] 3D model imported successfully to {filePath} ({modelData.Length} bytes)");

                        // Select the imported model in the Project window
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                        if (asset != null)
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                            EditorUtility.DisplayDialog("Ludo AI Plugin", "3D model imported successfully!", "OK");
                        }
                        else
                        {
                            Debug.LogWarning($"[LudoAIPlugin] Asset not loaded correctly: {filePath}");
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    statusMessage = "Error parsing server response";
                    Debug.LogError($"[LudoAIPlugin] Error parsing JSON response: {jsonEx.Message}");
                    Debug.LogException(jsonEx);
                    EditorUtility.DisplayDialog("Ludo AI Plugin Error", "Failed to parse server response", "OK");
                }
            }
            catch (Exception e)
            {
                statusMessage = "Error processing response: " + e.Message;
                Debug.LogError($"[LudoAIPlugin] Error processing response: {e.Message}");
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Error processing response: {e.Message}", "OK");
            }
        }

        isProcessing = false;
    }

    // Coroutine to retrieve 3D models gallery
    // Coroutine to retrieve the list of projects from the Ludo AI API
    private IEnumerator RetrieveProjects()
    {
        // Check if API key is set
        if (string.IsNullOrEmpty(apiKey))
        {
            statusMessage = "API Key is not set. Please go to Settings and enter your API key.";
            Debug.LogError("[LudoAIPlugin] API Key is not set. Please configure it in Settings.");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", 
                "API Key is not set.\n\nPlease go to the Settings tab and enter your Ludo AI API Key.\n\nYou can get your API key from the Ludo AI website.", 
                "OK");
            isProcessing = false;
            yield break;
        }

        string endpoint = "/projects";
        string fullUrl = apiUrl + endpoint;

        Debug.Log($"[LudoAIPlugin] Attempting to retrieve projects from: {fullUrl}");
        Debug.Log($"[LudoAIPlugin] Using API Key (first 10 chars): {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");

        // Send a GET request to the projects endpoint
        UnityWebRequest www = UnityWebRequest.Get(fullUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            string errorDetails = $"Error: {www.error}\nHTTP Code: {www.responseCode}\nURL: {fullUrl}";
            
            // Try to get more details from the response
            if (!string.IsNullOrEmpty(www.downloadHandler.text))
            {
                errorDetails += $"\nResponse: {www.downloadHandler.text}";
            }

            statusMessage = $"Error retrieving projects: {www.error} (HTTP {www.responseCode})";
            Debug.LogError($"[LudoAIPlugin] Error retrieving projects:\n{errorDetails}");
            
            string dialogMessage = $"Failed to retrieve projects.\n\n{errorDetails}";
            
            // Provide specific guidance for 403 errors
            if (www.responseCode == 403)
            {
                dialogMessage += "\n\n⚠️ 403 Forbidden Error:\nThis usually means your API key is invalid, expired, or doesn't have the required permissions.\n\nPlease:\n1. Check that your API key is correct\n2. Verify it hasn't expired\n3. Ensure you have the necessary permissions\n4. Try generating a new API key from the Ludo AI dashboard";
            }
            
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", dialogMessage, "OK");
        }
        else
        {
            // Parse the JSON response to retrieve the projects
            try
            {
                string json = www.downloadHandler.text;
                projects = JsonConvert.DeserializeObject<List<Project>>(json);

                if (projects != null && projects.Count > 0)
                {
                    // Create an array of project names for the dropdown list using the new title property
                    projectNamesArray = projects.Select(p => GetProjectTitle(p)).ToArray();
                    selectedProjectIndex = -1; // Reset the selected project index
                    selectedProject = null; // Reset the selected project
                    statusMessage = "Projects retrieved successfully.";
                    Debug.Log("[LudoAIPlugin] Projects retrieved successfully.");
                }
                else
                {
                    statusMessage = "No projects found.";
                    Debug.LogWarning("[LudoAIPlugin] No projects found.");
                    EditorUtility.DisplayDialog("Ludo AI Plugin", "No projects were found in your account.", "OK");
                }
            }
            catch (Exception e)
            {
                statusMessage = "Error parsing projects: " + e.Message;
                Debug.LogError($"[LudoAIPlugin] Error parsing projects: {e.Message}");
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to parse projects.\n\nError: {e.Message}", "OK");
            }
        }

        isProcessing = false;
    }

    // Coroutine to retrieve scripts for the selected project from the API
    private IEnumerator RetrieveScriptsForProject()
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            Debug.LogWarning("[LudoAIPlugin] No project selected.");
            isProcessing = false;
            yield break;
        }

        // Check if API key is set
        if (string.IsNullOrEmpty(apiKey))
        {
            statusMessage = "API Key is not set.";
            Debug.LogError("[LudoAIPlugin] API Key is not set.");
            isProcessing = false;
            yield break;
        }

        string endpoint = $"/project/{selectedProject.Id}/code";
        string fullUrl = apiUrl + endpoint;

        Debug.Log($"[LudoAIPlugin] Retrieving scripts for project '{GetProjectTitle(selectedProject)}' from: {fullUrl}");

        // Send a GET request to retrieve scripts for the selected project
        UnityWebRequest www = UnityWebRequest.Get(fullUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            string errorDetails = $"Error: {www.error}\nHTTP Code: {www.responseCode}\nURL: {fullUrl}";
            
            if (!string.IsNullOrEmpty(www.downloadHandler.text))
            {
                errorDetails += $"\nResponse: {www.downloadHandler.text}";
            }

            statusMessage = $"Error retrieving scripts: {www.error} (HTTP {www.responseCode})";
            Debug.LogError($"[LudoAIPlugin] Error retrieving scripts for project '{GetProjectTitle(selectedProject)}':\n{errorDetails}");
            
            string dialogMessage = $"Failed to retrieve scripts for project '{GetProjectTitle(selectedProject)}'.\n\n{errorDetails}";
            
            if (www.responseCode == 403)
            {
                dialogMessage += "\n\n⚠️ 403 Forbidden: Your API key may be invalid or expired. Please check your API key in Settings.";
            }
            
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", dialogMessage, "OK");
        }
        else
        {
            // Parse the JSON response to retrieve the scripts
            try
            {
                string json = www.downloadHandler.text;
                projectScripts = JsonConvert.DeserializeObject<List<ProjectCode>>(json);

                if (projectScripts == null)
                {
                    projectScripts = new List<ProjectCode>();
                }

                if (projectScripts.Count > 0)
                {
                    statusMessage = $"Retrieved {projectScripts.Count} script(s) for project '{GetProjectTitle(selectedProject)}'.";
                    Debug.Log($"[LudoAIPlugin] Retrieved {projectScripts.Count} script(s) for project '{GetProjectTitle(selectedProject)}'.");
                }
                else
                {
                    statusMessage = $"No scripts found for project '{GetProjectTitle(selectedProject)}'.";
                    Debug.LogWarning($"[LudoAIPlugin] No scripts found for project '{GetProjectTitle(selectedProject)}'.");
                }
            }
            catch (Exception e)
            {
                statusMessage = "Error parsing scripts: " + e.Message;
                Debug.LogError($"[LudoAIPlugin] Error parsing scripts for project '{GetProjectTitle(selectedProject)}': {e.Message}");
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to parse scripts for project '{GetProjectTitle(selectedProject)}'.\n\nError: {e.Message}", "OK");
            }
        }

        isProcessing = false;
    }

    // Coroutine to retrieve 3D models for the selected project
    // Coroutine to load a model preview image
    private IEnumerator LoadModelPreview(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            yield break;
        }

        string imageUrl = $"https://api.ludo.ai/api/images/{imageId}?format=jpeg";

        UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("Authorization", "ApiKey " + apiKey);

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            Repaint(); // Refresh the UI to show the loaded image
        }
        else
        {
            Debug.LogWarning($"[LudoAIPlugin] Failed to load preview image: {imageId}. Error: {www.error}");
        }
    }

    private IEnumerator Retrieve3DModels()
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            Debug.LogWarning("[LudoAIPlugin] No project selected.");
            isProcessing = false;
            yield break;
        }

        string endpoint = $"/project/{selectedProject.Id}/showcase-gallery";
        string fullUrl = apiUrl + endpoint;

        // Send a GET request to retrieve 3D models for the selected project
        UnityWebRequest www = UnityWebRequest.Get(fullUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("Authorization", "ApiKey " + apiKey);

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error retrieving 3D models for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})";
            Debug.LogError($"[LudoAIPlugin] Error retrieving 3D models for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to retrieve 3D models for project '{GetProjectTitle(selectedProject)}'.\n\nError: {www.error}\nURL: {fullUrl}", "OK");
        }
        else
        {
            // Parse the JSON response to retrieve the 3D models
            try
            {
                string json = www.downloadHandler.text;
                showcaseGallery = JsonConvert.DeserializeObject<ShowcaseGallery>(json);

                if (showcaseGallery != null && showcaseGallery.Assets != null && showcaseGallery.Assets.Count > 0)
                {
                    statusMessage = $"Retrieved {showcaseGallery.Assets.Count} 3D model(s) for project '{GetProjectTitle(selectedProject)}'.";
                    Debug.Log($"[LudoAIPlugin] Retrieved {showcaseGallery.Assets.Count} 3D model(s) for project '{GetProjectTitle(selectedProject)}'.");

                    // Clear the cache and preload previews
                    modelPreviewCache.Clear();
                    foreach (var asset in showcaseGallery.Assets)
                    {
                        string imageId = GetImageIdFromAsset(asset);
                        if (!string.IsNullOrEmpty(imageId))
                        {
                            EditorCoroutineUtility.StartCoroutineOwnerless(LoadModelPreview(imageId));
                        }
                    }
                }
                else
                {
                    statusMessage = $"No 3D models found for project '{GetProjectTitle(selectedProject)}'.";
                    Debug.LogWarning($"[LudoAIPlugin] No 3D models found for project '{GetProjectTitle(selectedProject)}'.");
                    showcaseGallery = new ShowcaseGallery { Assets = new List<ThreeDAsset>() };
                }
            }
            catch (Exception e)
            {
                statusMessage = "Error parsing 3D models: " + e.Message;
                Debug.LogError($"[LudoAIPlugin] Error parsing 3D models for project '{GetProjectTitle(selectedProject)}': {e.Message}");
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to parse 3D models for project '{GetProjectTitle(selectedProject)}'.\n\nError: {e.Message}", "OK");
            }
        }

        isProcessing = false;
    }

    // Coroutine to search for 3D models with a prompt
    // Coroutine to download model from a URL
    private IEnumerator DownloadModelFromUrl(string url, string modelsDirectory, string format)
    {
        Debug.Log($"[LudoAIPlugin] Downloading model from URL: {url}");

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("x-ludo-tool", "unity");
            // Set a reasonable timeout
            www.timeout = 300;

            statusMessage = "Downloading 3D model from URL...";
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                statusMessage = $"Error downloading from URL: {www.error}";
                Debug.LogError($"[LudoAIPlugin] Error downloading from URL: {www.error}");
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to download 3D model from URL.\n\nError: {www.error}", "OK");
            }
            else
            {
                try
                {
                    byte[] modelData = www.downloadHandler.data;

                    if (modelData == null || modelData.Length == 0)
                    {
                        statusMessage = "Received empty model data from the URL.";
                        Debug.LogError("[LudoAIPlugin] Received empty model data from the URL.");
                        EditorUtility.DisplayDialog("Ludo AI Plugin Error", "Received empty model data from the URL.", "OK");
                        yield break;
                    }

                    string contentType = www.GetResponseHeader("Content-Type") ?? "unknown";
                    Debug.Log($"[LudoAIPlugin] URL Response Content-Type: {contentType}");

                    string modelName = $"Model_{DateTime.Now:yyyyMMdd_HHmmss}";
                    string filePath = Path.Combine(modelsDirectory, $"{modelName}.{format}");

                    File.WriteAllBytes(filePath, modelData);
                    AssetDatabase.Refresh();

                    statusMessage = $"3D model imported successfully to {filePath}";
                    Debug.Log($"[LudoAIPlugin] 3D model imported successfully to {filePath} ({modelData.Length} bytes)");

                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }

                    EditorUtility.DisplayDialog("Ludo AI Plugin", "3D model downloaded and imported successfully!", "OK");
                }
                catch (Exception e)
                {
                    statusMessage = "Error processing model data from URL: " + e.Message;
                    Debug.LogError($"[LudoAIPlugin] Error processing model data from URL: {e.Message}");
                    EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Error processing model data from URL: {e.Message}", "OK");
                }
            }
        }
    }

    // Helper method to decompress gzipped data
    private byte[] DecompressGZip(byte[] compressedData)
    {
        using (var compressedStream = new MemoryStream(compressedData))
        using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }

    // Helper method to process downloaded model data
    private void ProcessDownloadedModel(byte[] modelData, string format, string modelsDirectory)
    {
        try
        {
            if (modelData == null || modelData.Length == 0)
            {
                statusMessage = "Received empty model data from the server.";
                Debug.LogError("[LudoAIPlugin] Received empty model data from the server.");
                EditorUtility.DisplayDialog("Ludo AI Plugin Error", "Received empty model data from the server.", "OK");
                return;
            }

            // Generate a unique filename with timestamp
            string modelName = $"Model_{DateTime.Now:yyyyMMdd_HHmmss}";
            string filePath = Path.Combine(modelsDirectory, $"{modelName}.{format}");

            // Write to file
            File.WriteAllBytes(filePath, modelData);

            // Refresh AssetDatabase to recognize the new file
            AssetDatabase.Refresh();

            statusMessage = $"3D model imported successfully to {filePath}";
            Debug.Log($"[LudoAIPlugin] 3D model imported successfully to {filePath}");

            // Select the imported model in the Project window
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
        catch (Exception e)
        {
            statusMessage = "Error processing model data: " + e.Message;
            Debug.LogError($"[LudoAIPlugin] Error processing model data: {e.Message}");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Error processing model data: {e.Message}", "OK");
        }
    }


    private IEnumerator GenerateInitialScriptsForProject()
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            isProcessing = false;
            yield break;
        }

        string endpoint = $"/project/{selectedProject.Id}/generate-code";
        string fullUrl = apiUrl + endpoint;

        // Send a GET request to generate initial scripts for the selected project
        UnityWebRequest www = UnityWebRequest.Get(fullUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error generating initial scripts for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})";
            Debug.LogError($"[LudoAIPlugin] Error generating initial scripts for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to generate initial scripts for project '{GetProjectTitle(selectedProject)}'.\n\nError: {www.error}\nURL: {fullUrl}", "OK");
        }
        else
        {
            // Parse the response to retrieve the generated scripts
            string jsonResponse = www.downloadHandler.text;
            projectScripts = JsonConvert.DeserializeObject<List<ProjectCode>>(jsonResponse);

            if (projectScripts != null && projectScripts.Count > 0)
            {
                statusMessage = $"Generated {projectScripts.Count} script(s) for project '{GetProjectTitle(selectedProject)}'. Starting import...";
                Debug.Log($"[LudoAIPlugin] Generated {projectScripts.Count} script(s). Importing them now...");

                // Automatically import the scripts
                CreateScripts();
            }
            else
            {
                statusMessage = "Failed to generate initial scripts or no scripts returned.";
                Debug.LogWarning("[LudoAIPlugin] No initial scripts were returned from generation.");
            }
        }

        isProcessing = false;
    }


    // Coroutine to generate all scripts again
    private IEnumerator GenerateAllScripts()
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            Debug.LogWarning("[LudoAIPlugin] No project selected.");
            isProcessing = false;
            yield break;
        }

        string endpoint = $"/project/{selectedProject.Id}/generate-code";
        string fullUrl = apiUrl + endpoint;

        // Send a GET request to generate all scripts for the selected project
        UnityWebRequest www = UnityWebRequest.Get(fullUrl);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error generating all scripts for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})";
            Debug.LogError($"[LudoAIPlugin] Error generating all scripts for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to generate all scripts for project '{GetProjectTitle(selectedProject)}'.\n\nError: {www.error}\nURL: {fullUrl}", "OK");
        }
        else
        {
            // Parse the response to retrieve the generated scripts
            string jsonResponse = www.downloadHandler.text;
            projectScripts = JsonConvert.DeserializeObject<List<ProjectCode>>(jsonResponse);

            if (projectScripts != null && projectScripts.Count > 0)
            {
                statusMessage = $"Generated {projectScripts.Count} script(s) for project '{GetProjectTitle(selectedProject)}'.";
                Debug.Log($"[LudoAIPlugin] Generated {projectScripts.Count} script(s) for project '{GetProjectTitle(selectedProject)}'.");

                // Automatically import the scripts after generation
                EditorCoroutineUtility.StartCoroutineOwnerless(ImportScriptsAndInstallPackages());
            }
            else
            {
                statusMessage = "Failed to generate scripts or no scripts returned.";
                Debug.LogWarning("[LudoAIPlugin] No scripts were returned from generation.");
                EditorUtility.DisplayDialog("Ludo AI Plugin Warning", "No scripts were returned from generation.", "OK");
            }
        }

        isProcessing = false;
    }

    // Coroutine to generate a new script for the selected project using a prompt
    private IEnumerator GenerateScriptWithPrompt()
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            Debug.LogWarning("[LudoAIPlugin] No project selected.");
            isProcessing = false;
            yield break;
        }

        string endpoint = $"/project/{selectedProject.Id}/generate-code/prompt";
        string fullUrl = apiUrl + endpoint;

        // Send a POST request with the user's prompt to generate a new script
        UnityWebRequest www = new UnityWebRequest(fullUrl, "POST");
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);
        www.SetRequestHeader("Content-Type", "application/json");

        // Create a JSON object with the prompt value
        string jsonData = JsonConvert.SerializeObject(new { value = scriptGenerationPrompt });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error generating script for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})";
            Debug.LogError($"[LudoAIPlugin] Error generating script for project '{GetProjectTitle(selectedProject)}': {www.error} (URL: {fullUrl})");
            EditorUtility.DisplayDialog("Ludo AI Plugin Error", $"Failed to generate script for project '{GetProjectTitle(selectedProject)}'.\n\nError: {www.error}\nURL: {fullUrl}", "OK");
        }
        else
        {
            statusMessage = "Script generated successfully. Retrieving scripts...";
            Debug.Log("[LudoAIPlugin] Script generated successfully. Retrieving scripts...");
            // After generating a new script, retrieve scripts for the project again
            EditorCoroutineUtility.StartCoroutineOwnerless(RetrieveScriptsForProject());
        }

        isProcessing = false;
    }

    // Coroutine to import scripts and install required packages
    private IEnumerator ImportScriptsAndInstallPackages()
    {
        // Install required packages
        yield return EditorCoroutineUtility.StartCoroutineOwnerless(InstallPackages());

        // Create scripts after packages are installed
        CreateScripts();
    }

    // Coroutine to install packages required by scripts
    private IEnumerator InstallPackages()
    {
        // Collect all packages from all scripts
        HashSet<string> packagesToInstall = new HashSet<string>();

        foreach (var code in projectScripts)
        {
            if (code.Packages != null && code.Packages.Count > 0)
            {
                foreach (var package in code.Packages)
                {
                    packagesToInstall.Add(package);
                }
            }
        }

        if (packagesToInstall.Count > 0)
        {
            statusMessage = "Installing required packages...";
            Debug.Log("[LudoAIPlugin] Installing required packages...");
            foreach (var packageName in packagesToInstall)
            {
                // Install the package using Unity Package Manager
                AddRequest request = Client.Add(packageName);
                while (!request.IsCompleted)
                {
                    yield return null;
                }

                if (request.Status == StatusCode.Success)
                {
                    Debug.Log($"[LudoAIPlugin] Successfully installed package: {packageName}");
                }
                else
                {
                    Debug.LogError($"[LudoAIPlugin] Error installing package '{packageName}': {request.Error.message}");
                }
            }

            // Refresh the AssetDatabase after installing packages
            AssetDatabase.Refresh();
            Debug.Log("[LudoAIPlugin] Package installation completed.");
        }
        else
        {
            Debug.Log("[LudoAIPlugin] No packages to install.");
        }
    }

    // Method to create Unity C# scripts from the retrieved project codes
    private void CreateScripts()
    {
        string scriptsFolderPath = "Assets/Scripts";

        // Check if the "Scripts" folder exists, create it if it doesn't
        if (!AssetDatabase.IsValidFolder(scriptsFolderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Scripts");
            Debug.Log("[LudoAIPlugin] Created folder: Assets/Scripts");
        }

        // Iterate through each project code and create a Unity C# script
        foreach (var code in projectScripts)
        {
            if (string.IsNullOrEmpty(code.FileName) || string.IsNullOrEmpty(code.Script))
            {
                Debug.LogWarning($"[LudoAIPlugin] ProjectCode with Id {code.Id} has missing FileName or Script content.");
                continue; // Skip this script if it's missing necessary data
            }

            // Write the script content to a file in the "Scripts" folder
            string filePath = Path.Combine(scriptsFolderPath, code.FileName);
            File.WriteAllText(filePath, code.Script);
            Debug.Log($"[LudoAIPlugin] Imported script: {filePath}");
        }

        // Refresh the AssetDatabase to make Unity recognize the newly created scripts
        AssetDatabase.Refresh();
        statusMessage = "Scripts imported successfully!";
        Debug.Log("[LudoAIPlugin] Scripts imported successfully!");

        // Start the compilation and error checking process
        EditorCoroutineUtility.StartCoroutineOwnerless(WaitForCompilationAndCheckErrors());
    }

    // Coroutine to wait for compilation to finish and then update the status
    private IEnumerator WaitForCompilationAndCheckErrors()
    {
        statusMessage = "Waiting for compilation to finish...";
        Debug.Log("[LudoAIPlugin] Waiting for compilation to finish...");

        // Wait until Unity finishes compiling scripts
        while (EditorApplication.isCompiling)
        {
            yield return null;
        }
        // If `OnAssemblyCompilationFinished` hasn't updated the flag yet, wait a bit more
        while (!compilationFinished)
        {
            yield return null;
        }
        statusMessage = "Compilation finished. Checking for errors...";
        Debug.Log("[LudoAIPlugin] Compilation finished. Checking for errors...");

        // Wait a short time to ensure any errors are logged
        yield return new EditorWaitForSeconds(1f);

        // Reset the flag for the next compilation
        compilationFinished = false;


        // If errors are detected, they would have been added to errorsToFix
        if (errorsToFix.Count > 0)
        {
            statusMessage = "Compilation errors detected.";
            Debug.LogWarning("[LudoAIPlugin] Compilation errors detected.");
        }
        else
        {
            statusMessage = "Compilation successful. No errors detected.";
            Debug.Log("[LudoAIPlugin] Compilation successful. No errors detected.");
        }

        isProcessing = false;
    }

    // ============================
    // ======= ERROR HANDLING ======
    // ============================

    // Callback method for handling log messages from Unity's console
    private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            // Attempt to parse file name and line number from the error log
            Match match = Regex.Match(logString, @"(Assets[\\\/]Scripts[\\\/].*?\.cs)\((\d+)\)");
            if (match.Success)
            {
                string filePath = match.Groups[1].Value.Replace("\\", "/"); // Normalize path
                string lineNumber = match.Groups[2].Value;

                // Ensure the file exists before proceeding
                if (File.Exists(filePath))
                {
                    ScriptErrorInfo errorInfo = new ScriptErrorInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        ErrorMessage = logString,
                        ScriptContent = File.ReadAllText(filePath)
                    };

                    // Avoid adding duplicate errors for the same file and message
                    if (!errorsToFix.Any(e => e.FilePath == errorInfo.FilePath && e.ErrorMessage == errorInfo.ErrorMessage))
                    {
                        errorsToFix.Add(errorInfo);
                        Debug.Log($"[LudoAIPlugin] Detected error in {errorInfo.FileName} at line {lineNumber}.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LudoAIPlugin] File not found: {filePath}");
                }
            }
            else
            {
                Debug.LogWarning($"[LudoAIPlugin] Failed to parse error log: {logString}");
            }
        }
    }

    // Callback method for assembly compilation finished
    private void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages)
    {
        foreach (var message in compilerMessages)
        {
            if (message.type == CompilerMessageType.Error)
            {
                string filePath = message.file.Replace("\\", "/");
                if (File.Exists(filePath))
                {
                    ScriptErrorInfo errorInfo = new ScriptErrorInfo
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        ErrorMessage = message.message,
                        ScriptContent = File.ReadAllText(filePath)
                    };

                    // Avoid adding duplicate errors for the same file and message
                    if (!errorsToFix.Any(e => e.FilePath == errorInfo.FilePath && e.ErrorMessage == errorInfo.ErrorMessage))
                    {
                        errorsToFix.Add(errorInfo);
                        Debug.Log($"[LudoAIPlugin] Detected error in {errorInfo.FileName}.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LudoAIPlugin] File not found: {filePath}");
                }
            }
        }
        // Mark compilation as finished
        compilationFinished = true;

        // Update the status message
        if (errorsToFix.Count > 0)
        {
            statusMessage = "Compilation errors detected.";
            Debug.LogWarning("[LudoAIPlugin] Compilation errors detected.");
        }
        else
        {
            statusMessage = "Compilation successful. No errors detected.";
            Debug.Log("[LudoAIPlugin] Compilation successful. No errors detected.");
        }
    }
    // Coroutine to handle compilation errors
    private IEnumerator HandleCompilationErrors()
    {
        if (isErrorHandling)
        {
            yield break; // Avoid concurrent error handling
        }

        isErrorHandling = true;
        statusMessage = "Handling compilation errors...";
        Debug.Log("[LudoAIPlugin] Handling compilation errors...");

        if (errorsToFix.Count == 0)
        {
            statusMessage = "No compilation errors to fix.";
            Debug.Log("[LudoAIPlugin] No compilation errors to fix.");
            isErrorHandling = false;
            yield break;
        }

        // Group errors by script
        var errorsGroupedByScript = errorsToFix.GroupBy(e => e.FilePath);

        // Counter to track pending requests
        int pendingRequests = errorsGroupedByScript.Count();

        // Callback to decrement the counter when a request completes
        Action onRequestComplete = () =>
        {
            pendingRequests--;
        };

        // Start SendErrorToLudoAI coroutines in parallel for each script
        foreach (var scriptErrors in errorsGroupedByScript)
        {
            var errorInfo = scriptErrors.First(); // Use the first error to get script info
            var errorMessages = scriptErrors.Select(e => e.ErrorMessage).ToList();

            EditorCoroutineUtility.StartCoroutineOwnerless(SendErrorToLudoAI(errorInfo, errorMessages, onRequestComplete));
        }

        // Wait until all requests have completed
        while (pendingRequests > 0)
        {
            yield return null;
        }

        // Clear the errorsToFix list
        errorsToFix.Clear();

        // Wait for a short duration to ensure API processing
        yield return new EditorWaitForSeconds(2f);

        // Delete all scripts in Assets/Scripts
        DeleteAllScripts();

        // Retrieve fixed scripts from Ludo AI
        yield return EditorCoroutineUtility.StartCoroutineOwnerless(RetrieveScriptsForProject());

        // Import scripts
        CreateScripts();

        isErrorHandling = false;
    }



    // Coroutine to fix all scripts with errors by calling the Ludo AI API in parallel
    private IEnumerator FixAllScriptsWithErrors()
    {
        statusMessage = "Fixing all scripts with errors...";
        Debug.Log("[LudoAIPlugin] Starting to fix all scripts with errors.");

        // Clear the errorsToFix list to ensure only current errors are handled
        errorsToFix.Clear();

        // Force a recompile to detect current errors
        CompilationPipeline.RequestScriptCompilation();

        // Wait until Unity finishes compiling scripts
        while (EditorApplication.isCompiling)
        {
            yield return null;
        }

        // Wait a short time to ensure any errors are logged
        yield return new EditorWaitForSeconds(1f);

        if (errorsToFix.Count == 0)
        {
            statusMessage = "No compilation errors detected.";
            Debug.Log("[LudoAIPlugin] No compilation errors detected.");
            isProcessing = false;
            yield break;
        }

        // Handle errors in parallel
        yield return EditorCoroutineUtility.StartCoroutineOwnerless(HandleCompilationErrors());

        statusMessage = "All scripts have been processed for fixes.";
        Debug.Log("[LudoAIPlugin] All scripts have been processed for fixes.");

        isProcessing = false;
    }

    // ============================
    // ======= HELPER METHODS =====
    // ============================

    // Helper method to get the project title from gdd2 or fallback to project.Name
    private string GetProjectTitle(Project project)
    {
        return project?.Gdd2?.Sections != null && project.Gdd2.Sections.Count > 0
        ? project.Gdd2.Sections[0]?.Value?.Title ?? project.Name
        : project.Name;
    }

    // Method to delete all scripts in Assets/Scripts
    private void DeleteAllScripts()
    {
        string scriptsFolderPath = "Assets/Scripts";

        if (Directory.Exists(scriptsFolderPath))
        {
            DirectoryInfo dirInfo = new DirectoryInfo(scriptsFolderPath);

            foreach (FileInfo file in dirInfo.GetFiles("*.cs"))
            {
                try
                {
                    file.Delete();
                    Debug.Log($"[LudoAIPlugin] Deleted script: {file.FullName}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LudoAIPlugin] Failed to delete script {file.FullName}: {ex.Message}");
                }
            }

            // Refresh the AssetDatabase after deletion
            AssetDatabase.Refresh();
            Debug.Log("[LudoAIPlugin] All scripts deleted successfully.");
        }
        else
        {
            Debug.LogWarning($"[LudoAIPlugin] Scripts folder not found at path: {scriptsFolderPath}");
        }
    }

    // Helper method to extract image ID from a URL
    private string GetImageIdFromAsset(ThreeDAsset asset)
    {
        if (asset?.Image == null || string.IsNullOrEmpty(asset.Image.Url))
        {
            return null;
        }

        // Extract the filename from the URL
        string url = asset.Image.Url;
        int lastSlashIndex = url.LastIndexOf('/');

        if (lastSlashIndex >= 0 && lastSlashIndex < url.Length - 1)
        {
            string filename = url.Substring(lastSlashIndex + 1);
            return filename;
        }

        return null;
    }

    // ============================
    // ======= DATA CLASSES ========
    // ============================

    // Classes for API data representation
    [Serializable]
    public class Project
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("gdd2")]
        public Gdd2Data Gdd2 { get; set; }
    }

    [Serializable]
    public class Gdd2Data
    {
        [JsonProperty("sections")]
        public List<Section> Sections { get; set; }
    }

    [Serializable]
    public class Section
    {
        [JsonProperty("value")]
        public SectionValue Value { get; set; }
    }

    [Serializable]
    public class SectionValue
    {
        [JsonProperty("title")]
        public string Title { get; set; }
    }

    [Serializable]
    public class ProjectCode
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        [JsonProperty("project_id")]
        public string ProjectId { get; set; }

        [JsonProperty("date")]
        public long Date { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("filename")]
        public string FileName { get; set; }

        [JsonProperty("overview")]
        public string Overview { get; set; }

        [JsonProperty("main_functions")]
        public string MainFunctions { get; set; }

        [JsonProperty("explanation")]
        public string Explanation { get; set; }

        [JsonProperty("implementation_details")]
        public string ImplementationDetails { get; set; }

        [JsonProperty("script")]
        public string Script { get; set; }

        [JsonProperty("packages")]
        public List<string> Packages { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }
    }

    // 3D Model data classes
    [Serializable]
    public class ShowcaseGallery
    {
        [JsonProperty("assets")]
        public List<ThreeDAsset> Assets { get; set; }

        [JsonProperty("images")]
        public List<GameImage> Images { get; set; }

        [JsonProperty("ideas")]
        public List<GeneratedGame> Ideas { get; set; }
    }

    [Serializable]
    public class ThreeDAsset
    {
        [JsonProperty("asset")]
        public AssetGeneration Asset { get; set; }

        [JsonProperty("image")]
        public GameImage Image { get; set; }
    }

    [Serializable]
    public class AssetGeneration
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("seed")]
        public int Seed { get; set; }

        [JsonProperty("images_b64")]
        public List<string> Images_b64 { get; set; }

        [JsonProperty("params_encrypted")]
        public string Params_encrypted { get; set; }

        [JsonProperty("request_id")]
        public string Request_id { get; set; }

        [JsonProperty("video_b64")]
        public string Video_b64 { get; set; }

        [JsonProperty("mesh_video_b64")]
        public string Mesh_video_b64 { get; set; }

        [JsonProperty("snapshots_b64")]
        public List<string> Snapshots_b64 { get; set; }

        [JsonProperty("model_b64")]
        public string Model_b64 { get; set; }

        [JsonProperty("voxels_b64")]
        public string Voxels_b64 { get; set; }

        [JsonProperty("mask_snapshots_b64")]
        public List<string> Mask_snapshots_b64 { get; set; }

        [JsonProperty("depth_snapshots_b64")]
        public List<string> Depth_snapshots_b64 { get; set; }

        [JsonProperty("normal_snapshots_b64")]
        public List<string> Normal_snapshots_b64 { get; set; }
    }

    [Serializable]
    public class GameImage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("final_score")]
        public float Final_score { get; set; }

        [JsonProperty("nsfw_prob")]
        public float Nsfw_prob { get; set; }

        [JsonProperty("similarity")]
        public float Similarity { get; set; }

        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("game_id")]
        public string Game_id { get; set; }

        [JsonProperty("hosted_filename")]
        public string Hosted_filename { get; set; }

        [JsonProperty("icon")]
        public bool Icon { get; set; }

        [JsonProperty("targetSize")]
        public int TargetSize { get; set; }

        [JsonProperty("embedded")]
        public bool Embedded { get; set; }

        [JsonProperty("analysed")]
        public bool Analysed { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("image_type")]
        public string Image_type { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("request_id")]
        public string Request_id { get; set; }

        [JsonProperty("hints")]
        public List<string> Hints { get; set; }

        [JsonProperty("selected_genres")]
        public List<string> Selected_genres { get; set; }

        [JsonProperty("selected_colors")]
        public List<string> Selected_colors { get; set; }

        [JsonProperty("selected_style")]
        public string Selected_style { get; set; }

        [JsonProperty("selected_perspective")]
        public string Selected_perspective { get; set; }

        [JsonProperty("is_safe")]
        public bool Is_safe { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("loading")]
        public bool Loading { get; set; }

        [JsonProperty("original_hints")]
        public List<string> Original_hints { get; set; }
    }

    [Serializable]
    public class GeneratedGame
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }

    // Class to hold information about scripts with errors
    private class ScriptErrorInfo
    {
        public string FilePath;
        public string FileName;
        public string ErrorMessage;
        public string ScriptContent;
    }

    // ============================
    // ======= CUSTOM YIELD =======
    // ============================

    // Custom yield instruction to wait for a specified duration in editor coroutines
    private class EditorWaitForSeconds : IEnumerator
    {
        private double targetTime;

        public EditorWaitForSeconds(float seconds)
        {
            targetTime = EditorApplication.timeSinceStartup + seconds;
        }

        public bool MoveNext()
        {
            return EditorApplication.timeSinceStartup < targetTime;
        }

        public void Reset()
        {
        }

        public object Current => null;
    }

    // ============================
    // ======= ADDITIONAL METHODS ==
    // ============================
    // Coroutine to send error details to Ludo AI API
    private IEnumerator SendErrorToLudoAI(ScriptErrorInfo errorInfo, List<string> errorMessages, Action onComplete)
    {
        if (selectedProject == null)
        {
            statusMessage = "No project selected.";
            Debug.LogWarning("[LudoAIPlugin] No project selected.");
            onComplete?.Invoke();
            yield break;
        }

        // Find the script ID from projectScripts based on FileName
        var matchingScript = projectScripts.FirstOrDefault(s => s.FileName == errorInfo.FileName);

        if (matchingScript == null)
        {
            Debug.LogWarning($"[LudoAIPlugin] Script '{errorInfo.FileName}' not found in project scripts.");
            onComplete?.Invoke();
            yield break;
        }

        string scriptId = matchingScript.Id;
        string projectId = selectedProject.Id;

        // Prepare the API endpoint
        string endpoint = $"/project/{projectId}/generate-code/prompt?scriptId={scriptId}";
        string fullUrl = apiUrl + endpoint;

        // Create the prompt with all error messages
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Fix the following errors in the script '{errorInfo.FileName}':\n");

        foreach (var errorMessage in errorMessages)
        {
            promptBuilder.AppendLine(errorMessage);
            promptBuilder.AppendLine();
        }

        string prompt = promptBuilder.ToString();

        // Send a POST request with the prompt
        UnityWebRequest www = new UnityWebRequest(fullUrl, "POST");
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("ApiKey", apiKey);
        www.SetRequestHeader("Content-Type", "application/json");

        // Create a JSON object with the prompt value
        string jsonData = JsonConvert.SerializeObject(new { value = prompt });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();

        Debug.Log($"[LudoAIPlugin] Sending error details to Ludo AI API for script '{errorInfo.FileName}'.");

        // Wait for the request to complete
        yield return www.SendWebRequest();

        // Check if the request was successful
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Error sending error details to Ludo AI API for script '{errorInfo.FileName}': {www.error}");
        }
        else
        {
            string response = www.downloadHandler.text;
            Debug.Log($"[LudoAIPlugin] Ludo AI API response for script '{errorInfo.FileName}': {response}");
        }

        // Invoke the completion callback
        onComplete?.Invoke();
    }

    // ============================
    // ======= SPRITE TAB UI ======
    // ============================

    private void DrawSpritesTab()
    {
        // Fetch initial payload if not already loaded or loading
        if (!initialPayloadLoaded && !isLoadingInitialPayload && !string.IsNullOrEmpty(apiKey))
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(FetchInitialPayload());
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentSpriteTab == SpriteTabState.Image, "Generate Sprite", EditorStyles.toolbarButton))
            currentSpriteTab = SpriteTabState.Image;
        if (GUILayout.Toggle(currentSpriteTab == SpriteTabState.ImageToSpritesheet, "Animate Sprite", EditorStyles.toolbarButton))
            currentSpriteTab = SpriteTabState.ImageToSpritesheet;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        switch (currentSpriteTab)
        {
            case SpriteTabState.Image:
                DrawGenerateImageUI();
                break;
            case SpriteTabState.ImageToSpritesheet:
                DrawImageToSpritesheetUI();
                break;
        }
    }

    private void DrawGenerateSpriteUI()
    {
        GUILayout.Label("Generate Sprite from Description", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Sprite Description:");
        spritePrompt = EditorGUILayout.TextArea(spritePrompt, GUILayout.Height(60));

        GUILayout.Space(5);

        GUILayout.Label("Motion Description (optional):");
        spriteMotionPrompt = EditorGUILayout.TextField(spriteMotionPrompt);

        GUILayout.Space(5);

        // Art Style dropdown - use predefined values if available
        GUILayout.Label("Art Style:");
        if (availableGenerationStyles != null && availableGenerationStyles.Count > 0)
        {
            selectedArtStyleIndex = EditorGUILayout.Popup(selectedArtStyleIndex, availableGenerationStyles.ToArray());
            spriteArtStyle = availableGenerationStyles[selectedArtStyleIndex];
        }
        else if (isLoadingInitialPayload)
        {
            EditorGUILayout.LabelField("Loading art styles from API...", EditorStyles.helpBox);
        }
        else
        {
            // Fallback to text field if predefined values not loaded yet
            spriteArtStyle = EditorGUILayout.TextField(spriteArtStyle);
            if (GUILayout.Button("Reload Art Styles", GUILayout.Width(150)))
            {
                initialPayloadLoaded = false;
                EditorCoroutineUtility.StartCoroutineOwnerless(FetchInitialPayload());
            }
        }

        // Perspective dropdown - use predefined values if available
        GUILayout.Label("Perspective:");
        if (availablePerspectives != null && availablePerspectives.Count > 0)
        {
            selectedPerspectiveIndex = EditorGUILayout.Popup(selectedPerspectiveIndex, availablePerspectives.ToArray());
            spritePerspective = availablePerspectives[selectedPerspectiveIndex];
        }
        else if (isLoadingInitialPayload)
        {
            EditorGUILayout.LabelField("Loading perspectives from API...", EditorStyles.helpBox);
        }
        else
        {
            // Fallback to text field if predefined values not loaded yet
            spritePerspective = EditorGUILayout.TextField(spritePerspective);
            if (GUILayout.Button("Reload Perspectives", GUILayout.Width(150)))
            {
                initialPayloadLoaded = false;
                EditorCoroutineUtility.StartCoroutineOwnerless(FetchInitialPayload());
            }
        }

        GUILayout.Label("Quality:");
        int qualityIndex = spriteQuality == "low" ? 0 : spriteQuality == "medium" ? 1 : 2;
        qualityIndex = EditorGUILayout.Popup(qualityIndex, new string[] { "low", "medium", "high" });
        spriteQuality = qualityIndex == 0 ? "low" : qualityIndex == 1 ? "medium" : "high";

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(spritePrompt);
        if (GUILayout.Button("Generate Sprite", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating sprite...";
            EditorCoroutineUtility.StartCoroutineOwnerless(GenerateSprite());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        // Display generated sprites
        if (generatedSprites != null && generatedSprites.Count > 0)
        {
            GUILayout.Label("Generated Sprites:", EditorStyles.boldLabel);
            spritesScrollPosition = GUILayout.BeginScrollView(spritesScrollPosition, GUILayout.Height(300));

            for (int i = 0; i < generatedSprites.Count; i++)
            {
                var sprite = generatedSprites[i];
                GUILayout.BeginHorizontal("box");

                // Show preview image if available
                if (spritePreviewCache.ContainsKey(sprite.Id))
                {
                    Texture2D preview = spritePreviewCache[sprite.Id];
                    GUILayout.Label(preview, GUILayout.Width(100), GUILayout.Height(100));
                }
                else
                {
                    GUILayout.Label("Loading...", GUILayout.Width(100), GUILayout.Height(100));
                }

                GUILayout.BeginVertical();
                GUILayout.Label($"Prompt: {sprite.OriginalPrompt}", EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(sprite.MotionPrompt))
                    GUILayout.Label($"Motion: {sprite.MotionPrompt}", EditorStyles.wordWrappedLabel);
                GUILayout.Label($"Size: {sprite.Image.Width}x{sprite.Image.Height}", EditorStyles.miniLabel);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUILayout.Width(90));
                if (GUILayout.Button("Select"))
                {
                    selectedSpriteIndex = i;
                    selectedSprite = sprite;
                    statusMessage = "Sprite selected. Go to 'Image to Spritesheet' to animate it.";
                }

                if (GUILayout.Button("Save"))
                {
                    SaveSpriteToFile(sprite);
                }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }
    }

    private void DrawGenerateImageUI()
    {
        GUILayout.Label("Generate Sprite", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Image Description:");
        imagePrompt = EditorGUILayout.TextArea(imagePrompt, GUILayout.Height(60));

        GUILayout.Space(10);

        // Image Type dropdown
        int imageTypeIndex = System.Array.IndexOf(imageTypeOptions, imageType);
        imageTypeIndex = EditorGUILayout.Popup("Image Type:", imageTypeIndex, imageTypeOptions);
        imageType = imageTypeOptions[imageTypeIndex];

        // Genre dropdown
        int genreIndex = System.Array.IndexOf(genreOptions, imageGenre);
        genreIndex = EditorGUILayout.Popup("Genre:", genreIndex, genreOptions);
        imageGenre = genreOptions[genreIndex];

        // Platform dropdown
        int platformIndex = System.Array.IndexOf(platformOptions, imagePlatform);
        platformIndex = EditorGUILayout.Popup("Platform:", platformIndex, platformOptions);
        imagePlatform = platformOptions[platformIndex];

        // Art Style dropdown
        int artStyleIndex = System.Array.IndexOf(artStyleOptions, imageArtStyle);
        artStyleIndex = EditorGUILayout.Popup("Art Style:", artStyleIndex, artStyleOptions);
        imageArtStyle = artStyleOptions[artStyleIndex];

        // Perspective dropdown
        int perspectiveIndex = System.Array.IndexOf(perspectiveOptions, imagePerspective);
        perspectiveIndex = EditorGUILayout.Popup("Perspective:", perspectiveIndex, perspectiveOptions);
        imagePerspective = perspectiveOptions[perspectiveIndex];

        // Aspect Ratio dropdown
        int aspectRatioIndex = System.Array.IndexOf(aspectRatioOptions, imageAspectRatio);
        aspectRatioIndex = EditorGUILayout.Popup("Aspect Ratio:", aspectRatioIndex, aspectRatioLabels);
        imageAspectRatio = aspectRatioOptions[aspectRatioIndex];

        GUILayout.Space(10);

        // Number of images slider
        GUILayout.Label($"Number of Images: {imageCount}");
        imageCount = (int)EditorGUILayout.Slider(imageCount, 1, 8);

        GUILayout.Space(10);

        imageAugmentPrompt = EditorGUILayout.Toggle("Augment Prompt", imageAugmentPrompt);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(imagePrompt);
        if (GUILayout.Button("Generate Image(s)", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating image(s)...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateImage());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        // Display generated images
        if (generatedImages.Count > 0)
        {
            GUILayout.Label($"Generated Images ({generatedImages.Count}):", EditorStyles.boldLabel);
            
            imagesScrollPosition = GUILayout.BeginScrollView(imagesScrollPosition, GUILayout.Height(300));
            
            for (int i = 0; i < generatedImages.Count; i++)
            {
                GeneratedImage image = generatedImages[i];
                
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                
                // Display preview if available
                if (imagePreviewCache.ContainsKey(image.Url))
                {
                    Texture2D preview = imagePreviewCache[image.Url];
                    GUILayout.Label(preview, GUILayout.Width(100), GUILayout.Height(100));
                }
                else
                {
                    GUILayout.Label("Loading...", GUILayout.Width(100), GUILayout.Height(100));
                }

                GUILayout.BeginVertical();
                GUILayout.Label($"Size: {image.Width}x{image.Height}", EditorStyles.miniLabel);
                
                if (GUILayout.Button("Save to File", GUILayout.Height(25)))
                {
                    SaveImageToFile(image);
                }
                
                if (GUILayout.Button("Select", GUILayout.Height(25)))
                {
                    selectedImageIndex = i;
                    selectedImage = image;
                    // Also update selectedSprite for backwards compatibility with Animate Sprite tab
                    selectedSprite = new GeneratedSprite
                    {
                        Id = System.Guid.NewGuid().ToString(),
                        Prompt = imagePrompt,
                        OriginalPrompt = imagePrompt,
                        Image = new SpriteImage
                        {
                            Url = image.Url,
                            Width = image.Width,
                            Height = image.Height,
                            Prompt = imagePrompt
                        }
                    };
                    statusMessage = "Sprite selected. Go to 'Animate Sprite' to animate it.";
                }
                GUILayout.EndVertical();
                
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                
                GUILayout.Space(5);
            }
            
            GUILayout.EndScrollView();
        }
    }

    private void DrawImageToSpritesheetUI()
    {
        spritesheetScrollPosition = GUILayout.BeginScrollView(spritesheetScrollPosition);

        GUILayout.Label("Animate Sprite", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Initial Image URL:");
        spritesheetInitialImageUrl = EditorGUILayout.TextField(spritesheetInitialImageUrl);

        GUILayout.Space(5);

        if (selectedSprite != null && selectedSprite.Image != null)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Selected sprite: {selectedSprite.OriginalPrompt}", EditorStyles.helpBox);
            if (GUILayout.Button("Use Selected Sprite", GUILayout.Height(30)))
            {
                spritesheetInitialImageUrl = selectedSprite.Image.Url;
                statusMessage = "Using selected sprite URL";
            }
            GUILayout.EndVertical();
        }
        else if (selectedImage != null)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Selected image ready to use", EditorStyles.helpBox);
            if (GUILayout.Button("Use Selected Image", GUILayout.Height(30)))
            {
                spritesheetInitialImageUrl = selectedImage.Url;
                statusMessage = "Using selected image URL";
            }
            GUILayout.EndVertical();
        }
        else
        {
            GUILayout.Label("No sprite/image selected. Generate one first or enter URL above.", EditorStyles.helpBox);
        }

        GUILayout.Space(10);

        GUILayout.Label("Motion Description:");
        spritesheetMotionHint = EditorGUILayout.TextField(spritesheetMotionHint);

        GUILayout.Space(5);

        spritesheetLoop = EditorGUILayout.Toggle("Loop Animation", spritesheetLoop);
        spritesheetCrop = EditorGUILayout.Toggle("Crop to Content", spritesheetCrop);

        GUILayout.Space(5);

        // Frames dropdown with predefined options
        GUILayout.Label("Frames:");
        int currentFrameIndex = Array.IndexOf(frameOptions, spritesheetFrames);
        if (currentFrameIndex < 0) currentFrameIndex = 1; // Default to 9 frames
        currentFrameIndex = EditorGUILayout.Popup(currentFrameIndex, Array.ConvertAll(frameOptions, x => x.ToString()));
        spritesheetFrames = frameOptions[currentFrameIndex];

        // Frame Size dropdown with predefined options
        GUILayout.Label("Frame Size:");
        int currentFrameSizeIndex = Array.IndexOf(frameSizeOptions, spritesheetFrameSize);
        if (currentFrameSizeIndex < 0) currentFrameSizeIndex = 2; // Default to 256x256
        currentFrameSizeIndex = EditorGUILayout.Popup(currentFrameSizeIndex, frameSizeLabels);
        spritesheetFrameSize = frameSizeOptions[currentFrameSizeIndex];

        GUILayout.Space(5);

        // Margin Mode dropdown
        GUILayout.Label("Margin Mode:");
        int marginModeIndex = Array.IndexOf(marginModeOptions, spritesheetMarginMode);
        if (marginModeIndex < 0) marginModeIndex = 0; // Default to auto
        marginModeIndex = EditorGUILayout.Popup(marginModeIndex, marginModeLabels);
        spritesheetMarginMode = marginModeOptions[marginModeIndex];

        // Show margin ratio slider only if manual mode is selected
        if (spritesheetMarginMode == "manual")
        {
            spritesheetMarginRatio = EditorGUILayout.Slider("Margin Ratio", spritesheetMarginRatio, 0f, 1f);
        }

        GUILayout.Space(5);

        // Pixel Art Filter dropdown with predefined options
        GUILayout.Label("Pixel Art Filter:");
        int filterIndex = Array.IndexOf(pixelArtFilterOptions, spritesheetPixelFilter);
        if (filterIndex < 0) filterIndex = 0; // Default to none
        filterIndex = EditorGUILayout.Popup(filterIndex, pixelArtFilterLabels);
        spritesheetPixelFilter = pixelArtFilterOptions[filterIndex];

        GUILayout.Space(5);

        // Model selection
        GUILayout.Label("Animation Model:");
        int modelIndex = Array.IndexOf(spritesheetModelOptions, spritesheetModel);
        if (modelIndex < 0) modelIndex = 0; // Default to standard
        modelIndex = EditorGUILayout.Popup(modelIndex, spritesheetModelOptions);
        spritesheetModel = spritesheetModelOptions[modelIndex];

        GUILayout.Space(5);

        // Duration based on model
        GUILayout.Label("Duration (seconds):");
        if (spritesheetModel == "standard")
        {
            float[] standardDurations = { 1.2f, 1.5f, 2.0f, 2.5f, 3.0f };
            string[] standardLabels = { "1.2s", "1.5s", "2.0s", "2.5s", "3.0s" };
            int durationIndex = Array.IndexOf(standardDurations, spritesheetDuration);
            if (durationIndex < 0) durationIndex = 2; // Default to 2.0s
            durationIndex = EditorGUILayout.Popup(durationIndex, standardLabels);
            spritesheetDuration = standardDurations[durationIndex];
        }
        else // new model
        {
            spritesheetDuration = 4.0f;
            EditorGUILayout.LabelField("4.0s (fixed for 'new' model)", EditorStyles.helpBox);
        }

        GUILayout.Space(5);

        // Image Type dropdown
        GUILayout.Label("Image Type:");
        int imageTypeIndex = Array.IndexOf(spritesheetImageTypeOptions, spritesheetImageType);
        if (imageTypeIndex < 0) imageTypeIndex = 0; // Default to sprite
        imageTypeIndex = EditorGUILayout.Popup(imageTypeIndex, spritesheetImageTypeOptions);
        spritesheetImageType = spritesheetImageTypeOptions[imageTypeIndex];

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(spritesheetInitialImageUrl) && !string.IsNullOrEmpty(spritesheetMotionHint);
        if (GUILayout.Button("Animate Sprite", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Animating sprite...";
            EditorCoroutineUtility.StartCoroutineOwnerless(ImageToSpritesheet());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        // Display current spritesheet
        if (currentSpritesheet != null)
        {
            GUILayout.Label("Generated Spritesheet:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");

            if (spritesheetPreviewTexture != null)
            {
                GUILayout.Label(spritesheetPreviewTexture, GUILayout.Width(400), GUILayout.Height(200));
            }

            GUILayout.Label($"Frames: {currentSpritesheet.NumFrames}");
            GUILayout.Label($"Frame Size: {currentSpritesheet.TargetFrameSize}");
            GUILayout.Label($"Loop: {currentSpritesheet.Loop}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Spritesheet", GUILayout.Height(30)))
            {
                SaveSpritesheetToFile(currentSpritesheet);
            }
            if (GUILayout.Button("Save GIF Preview", GUILayout.Height(30)))
            {
                SaveGifToFile(currentSpritesheet);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }
    // ============================
    // ======= AUDIO TAB UI =======
    // ============================

    private void DrawAudioTab()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(currentAudioTab == AudioTabState.SoundEffect, "Sound Effect", EditorStyles.toolbarButton))
            currentAudioTab = AudioTabState.SoundEffect;
        if (GUILayout.Toggle(currentAudioTab == AudioTabState.Music, "Music", EditorStyles.toolbarButton))
            currentAudioTab = AudioTabState.Music;
        if (GUILayout.Toggle(currentAudioTab == AudioTabState.Voice, "Voice", EditorStyles.toolbarButton))
            currentAudioTab = AudioTabState.Voice;
        if (GUILayout.Toggle(currentAudioTab == AudioTabState.Speech, "Speech (Clone)", EditorStyles.toolbarButton))
            currentAudioTab = AudioTabState.Speech;
        if (GUILayout.Toggle(currentAudioTab == AudioTabState.SpeechPreset, "Speech (Preset)", EditorStyles.toolbarButton))
            currentAudioTab = AudioTabState.SpeechPreset;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        switch (currentAudioTab)
        {
            case AudioTabState.SoundEffect:
                DrawSoundEffectUI();
                break;
            case AudioTabState.Music:
                DrawMusicUI();
                break;
            case AudioTabState.Voice:
                DrawVoiceUI();
                break;
            case AudioTabState.Speech:
                DrawSpeechUI();
                break;
            case AudioTabState.SpeechPreset:
                DrawSpeechPresetUI();
                break;
        }
    }

    private void DrawSoundEffectUI()
    {
        GUILayout.Label("Generate Sound Effect", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Description:");
        soundEffectDescription = EditorGUILayout.TextArea(soundEffectDescription, GUILayout.Height(60));

        GUILayout.Space(10);

        GUILayout.Label($"Duration (seconds, 0 = auto): {soundEffectDuration:F1}");
        soundEffectDuration = EditorGUILayout.Slider(soundEffectDuration, 0f, 10f);

        GUILayout.Space(10);

        soundEffectAugment = EditorGUILayout.Toggle("Augment Prompt", soundEffectAugment);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(soundEffectDescription);
        if (GUILayout.Button("Generate Sound Effect", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating sound effect...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateSoundEffect());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentSoundEffect != null)
        {
            GUILayout.Label("Generated Sound Effect:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"URL: {currentSoundEffect.Url}");
            if (GUILayout.Button("Save Sound Effect", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(currentSoundEffect, "SoundEffect");
            }
            GUILayout.EndVertical();
        }
    }

    private void DrawMusicUI()
    {
        GUILayout.Label("Generate Music", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Description:");
        musicDescription = EditorGUILayout.TextArea(musicDescription, GUILayout.Height(60));

        GUILayout.Space(10);

        GUILayout.Label("Lyrics (optional):");
        musicLyrics = EditorGUILayout.TextArea(musicLyrics, GUILayout.Height(80));

        GUILayout.Space(10);

        musicAugment = EditorGUILayout.Toggle("Augment Prompt", musicAugment);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(musicDescription);
        if (GUILayout.Button("Generate Music", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating music...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateMusic());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentMusic != null)
        {
            GUILayout.Label("Generated Music:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"URL: {currentMusic.Url}");
            if (GUILayout.Button("Save Music", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(currentMusic, "Music");
            }
            GUILayout.EndVertical();
        }
    }

    private void DrawVoiceUI()
    {
        GUILayout.Label("Generate Voice from Description", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label("Voice Description:");
        voiceDescription = EditorGUILayout.TextArea(voiceDescription, GUILayout.Height(60));

        GUILayout.Space(10);

        GUILayout.Label($"Text to Speak (max 200 chars): {voiceText.Length}/200");
        voiceText = EditorGUILayout.TextArea(voiceText, GUILayout.Height(60));
        if (voiceText.Length > 200)
        {
            voiceText = voiceText.Substring(0, 200);
        }

        GUILayout.Space(10);

        int voiceTypeIndex = System.Array.IndexOf(voiceTypeOptions, voiceType);
        voiceTypeIndex = EditorGUILayout.Popup("Voice Type:", voiceTypeIndex, voiceTypeOptions);
        voiceType = voiceTypeOptions[voiceTypeIndex];

        GUILayout.Space(10);

        voiceAugment = EditorGUILayout.Toggle("Augment Prompt", voiceAugment);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(voiceDescription) && !string.IsNullOrEmpty(voiceText);
        if (GUILayout.Button("Generate Voice", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating voice...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateVoice());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentVoice != null)
        {
            GUILayout.Label("Generated Voice:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"URL: {currentVoice.Url}");
            if (GUILayout.Button("Save Voice", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(currentVoice, "Voice");
            }
            GUILayout.EndVertical();
        }
    }

    private void DrawSpeechUI()
    {
        GUILayout.Label("Generate Speech with Voice Cloning", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label($"Text to Speak (max 1000 chars): {speechText.Length}/1000");
        speechText = EditorGUILayout.TextArea(speechText, GUILayout.Height(80));
        if (speechText.Length > 1000)
        {
            speechText = speechText.Substring(0, 1000);
        }

        GUILayout.Space(10);

        GUILayout.Label("Voice Sample (URL or base64):");
        speechSample = EditorGUILayout.TextField(speechSample);

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(speechText) && !string.IsNullOrEmpty(speechSample);
        if (GUILayout.Button("Generate Speech", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating speech with voice cloning...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateSpeech());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentSpeech != null)
        {
            GUILayout.Label("Generated Speech:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"URL: {currentSpeech.Url}");
            if (GUILayout.Button("Save Speech", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(currentSpeech, "Speech");
            }
            GUILayout.EndVertical();
        }
    }

    private void DrawSpeechPresetUI()
    {
        GUILayout.Label("Generate Speech with Preset Voice", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label($"Text to Speak (max 1000 chars): {speechPresetText.Length}/1000");
        speechPresetText = EditorGUILayout.TextArea(speechPresetText, GUILayout.Height(80));
        if (speechPresetText.Length > 1000)
        {
            speechPresetText = speechPresetText.Substring(0, 1000);
        }

        GUILayout.Space(10);

        int voicePresetIndex = System.Array.IndexOf(voicePresetOptions, speechPresetVoiceId);
        voicePresetIndex = EditorGUILayout.Popup("Voice Preset:", voicePresetIndex, voicePresetOptions);
        speechPresetVoiceId = voicePresetOptions[voicePresetIndex];

        GUILayout.Space(10);

        int emotionIndex = System.Array.IndexOf(emotionOptions, speechPresetEmotion);
        emotionIndex = EditorGUILayout.Popup("Emotion:", emotionIndex, emotionOptions);
        speechPresetEmotion = emotionOptions[emotionIndex];

        GUILayout.Space(10);

        int languageIndex = System.Array.IndexOf(languageOptions, speechPresetLanguage);
        languageIndex = EditorGUILayout.Popup("Language:", languageIndex, languageOptions);
        speechPresetLanguage = languageOptions[languageIndex];

        GUILayout.Space(10);

        GUI.enabled = !isProcessing && !string.IsNullOrEmpty(speechPresetText);
        if (GUILayout.Button("Generate Speech", GUILayout.Height(30)))
        {
            isProcessing = true;
            statusMessage = "Generating speech with preset voice...";
            EditorCoroutineUtility.StartCoroutineOwnerless(CreateSpeechPreset());
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Space(10);

        if (currentSpeechPreset != null)
        {
            GUILayout.Label("Generated Speech:", EditorStyles.boldLabel);
            GUILayout.BeginVertical("box");
            GUILayout.Label($"URL: {currentSpeechPreset.Url}");
            if (GUILayout.Button("Save Speech", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(currentSpeechPreset, "SpeechPreset");
            }
            GUILayout.EndVertical();
        }
    }

    // ============================
    // ===== SPRITE API CALLS =====
    // ============================
    // NOTE: Ludo AI API returns WebP format images by default.
    // Unity supports WebP decoding at runtime since version 2021.2+ (including Unity 6).
    // SOLUTIONS:
    //   1. Contact Ludo AI to request PNG format option in their API
    //   2. Install a Unity WebP decoder package
    //   3. Convert WebP files manually after download
    // ============================

    private IEnumerator GenerateSprite()
    {
        // Use the correct /assets/image endpoint
        string endpoint = "/assets/image";
        string fullUrl = apiUrl + endpoint;

        var requestData = new Dictionary<string, object>
        {
            ["image_type"] = "sprite",
            ["prompt"] = spritePrompt,
            ["n"] = 1,
            ["augment_prompt"] = true
        };

        // Add optional parameters if available
        if (!string.IsNullOrEmpty(spriteArtStyle) && spriteArtStyle != "Any style")
            requestData["art_style"] = spriteArtStyle;
        
        if (!string.IsNullOrEmpty(spritePerspective) && spritePerspective != "Any perspective")
            requestData["perspective"] = spritePerspective;

        string jsonData = JsonConvert.SerializeObject(requestData);
        Debug.Log($"[LudoAIPlugin] Sprite generation request payload: {jsonData}");
        Debug.Log($"[LudoAIPlugin] Full URL: {fullUrl}");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest www = new UnityWebRequest(fullUrl, "POST");
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.SetRequestHeader("Authorization", "ApiKey " + apiKey);
        www.SetRequestHeader("Content-Type", "application/json");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.timeout = 600;

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            statusMessage = $"Error generating sprite: {www.error}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
            {
                Debug.LogError($"[LudoAIPlugin] Response details: {www.downloadHandler.text}");
            }
            EditorUtility.DisplayDialog("Sprite Generation Error", statusMessage, "OK");
        }
        else
        {
            try
            {
                string json = www.downloadHandler.text;
                var images = JsonConvert.DeserializeObject<List<GeneratedImage>>(json);

                if (images != null && images.Count > 0)
                {
                    // Convert GeneratedImage to GeneratedSprite for compatibility with existing UI
                    foreach (var image in images)
                    {
                        var sprite = new GeneratedSprite
                        {
                            Id = System.Guid.NewGuid().ToString(),
                            Prompt = spritePrompt,
                            OriginalPrompt = spritePrompt,
                            Image = new SpriteImage
                            {
                                Url = image.Url,
                                Width = image.Width,
                                Height = image.Height,
                                Prompt = spritePrompt
                            }
                        };
                        generatedSprites.Add(sprite);
                        EditorCoroutineUtility.StartCoroutineOwnerless(LoadSpritePreview(sprite));
                    }
                    
                    statusMessage = $"Generated {images.Count} sprite(s) successfully!";
                    Debug.Log($"[LudoAIPlugin] {statusMessage}");
                }
                else
                {
                    statusMessage = "No sprites generated.";
                    Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
                }
            }
            catch (Exception e)
            {
                statusMessage = $"Error parsing sprite response: {e.Message}";
                Debug.LogError($"[LudoAIPlugin] {statusMessage}");
                Debug.LogError($"[LudoAIPlugin] Response JSON: {www.downloadHandler.text}");
                EditorUtility.DisplayDialog("Sprite Generation Error", statusMessage, "OK");
            }
        }

        isProcessing = false;
    }

    private IEnumerator ImageToSpritesheet()
    {
        // Use the manually entered URL or fall back to selected sprite
        string initialImageUrl = !string.IsNullOrEmpty(spritesheetInitialImageUrl)
            ? spritesheetInitialImageUrl
            : (selectedSprite != null ? selectedSprite.Image.Url : "");

        // Build the request payload according to the new API schema
        var requestData = new Dictionary<string, object>
        {
            ["motion_prompt"] = spritesheetMotionHint,
            ["initial_image"] = initialImageUrl,
            ["loop"] = spritesheetLoop,
            ["crop"] = spritesheetCrop,
            ["frames"] = spritesheetFrames,
            ["frame_size"] = spritesheetFrameSize,
            ["model"] = spritesheetModel,
            ["duration"] = spritesheetDuration,
            ["image_type"] = spritesheetImageType,
            ["augment_prompt"] = spritesheetAugmentPrompt
        };

        // Add margin_ratio based on mode
        if (spritesheetMarginMode == "manual")
        {
            requestData["margin_ratio"] = spritesheetMarginRatio;
            requestData["margin_ratio_mode"] = "manual";
        }
        else if (spritesheetMarginMode == "none")
        {
            requestData["margin_ratio_mode"] = "none";
        }
        else // auto
        {
            requestData["margin_ratio_mode"] = "auto";
        }

        // Add optional parameters
        if (spritesheetPixelFilter != "none")
        {
            requestData["pixel_art_filter"] = spritesheetPixelFilter;
        }

        if (!string.IsNullOrEmpty(spritesheetFinalImage))
        {
            requestData["final_image"] = spritesheetFinalImage;
        }

        var job = new EditorHttpResult();
        yield return RunApiJob("/assets/sprite/animate", requestData, "Animating sprite", job);

        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError("animating sprite", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Sprite Animation Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            var animatedResponse = JsonConvert.DeserializeObject<AnimatedSpriteResponse>(job.Body);

            if (animatedResponse != null && !string.IsNullOrEmpty(animatedResponse.SpritesheetUrl))
            {
                statusMessage = "Sprite animated successfully!";
                Debug.Log($"[LudoAIPlugin] {statusMessage}");

                // For backwards compatibility, also populate currentSpritesheet if needed
                currentSpritesheet = new GeneratedSpritesheet
                {
                    SpriteSheetB64 = animatedResponse.SpritesheetUrl,
                    Video = new VideoInfo { Url = animatedResponse.VideoUrl },
                    GifB64 = animatedResponse.GifUrl,
                    NumFrames = animatedResponse.NumFrames,
                    TargetFrameSize = spritesheetFrameSize,
                    Loop = spritesheetLoop,
                    Duration = animatedResponse.Duration
                };

                // Load preview texture
                EditorCoroutineUtility.StartCoroutineOwnerless(LoadSpritesheetPreview(animatedResponse.SpritesheetUrl));
            }
            else
            {
                statusMessage = "Sprite animation response contained no spritesheet.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage} Body: {job.Body}");
                EditorUtility.DisplayDialog("Sprite Animation Error", statusMessage, "OK");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing sprite animation response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Sprite Animation Error", statusMessage, "OK");
        }

        isProcessing = false;
    }

    // ============================
    // ==== VALIDATION HELPERS ====
    // ============================

    private bool ValidateTextLength(string text, int maxLength, string fieldName)
    {
        if (text.Length > maxLength)
        {
            statusMessage = $"{fieldName} exceeds maximum length of {maxLength} characters";
            Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Validation Error", statusMessage, "OK");
            return false;
        }
        return true;
    }

    private bool ValidateRange(float value, float min, float max, string fieldName)
    {
        if (value < min || value > max)
        {
            statusMessage = $"{fieldName} must be between {min} and {max}";
            Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Validation Error", statusMessage, "OK");
            return false;
        }
        return true;
    }

    private bool ValidateRequired(string value, string fieldName)
    {
        if (string.IsNullOrEmpty(value))
        {
            statusMessage = $"{fieldName} is required";
            Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Validation Error", statusMessage, "OK");
            return false;
        }
        return true;
    }

    // ============================
    // ===== IMAGE API CALLS ======
    // ============================

    private IEnumerator CreateImage()
    {
        // Validate parameters
        if (!ValidateRequired(imagePrompt, "Image prompt"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRange(imageCount, 1, 8, "Image count"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["image_type"] = imageType,
            ["prompt"] = imagePrompt,
            ["n"] = imageCount,
            ["augment_prompt"] = imageAugmentPrompt
        };

        // Add optional parameters
        if (!string.IsNullOrEmpty(imageGenre))
            requestData["genre"] = imageGenre;

        if (!string.IsNullOrEmpty(imagePlatform))
            requestData["platform"] = imagePlatform;

        if (!string.IsNullOrEmpty(imageArtStyle) && imageArtStyle != "Any style")
            requestData["art_style"] = imageArtStyle;

        if (!string.IsNullOrEmpty(imagePerspective) && imagePerspective != "Any perspective")
            requestData["perspective"] = imagePerspective;

        if (!string.IsNullOrEmpty(imageAspectRatio) && imageAspectRatio != "default")
            requestData["aspect_ratio"] = imageAspectRatio;

        var job = new EditorHttpResult();
        yield return RunApiJob("/assets/image", requestData, "Generating image(s)", job);

        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError("generating image", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Image Generation Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            var images = JsonConvert.DeserializeObject<List<GeneratedImage>>(job.Body);

            if (images != null && images.Count > 0)
            {
                generatedImages.AddRange(images);
                statusMessage = $"Generated {images.Count} image(s) successfully!";
                Debug.Log($"[LudoAIPlugin] {statusMessage}");

                // Load preview images
                foreach (var image in images)
                {
                    EditorCoroutineUtility.StartCoroutineOwnerless(LoadImagePreview(image));
                }
            }
            else
            {
                statusMessage = "No images generated.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing image response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("Image Generation Error", statusMessage, "OK");
        }

        isProcessing = false;
    }

    private IEnumerator LoadImagePreview(GeneratedImage image)
    {
        if (string.IsNullOrEmpty(image.Url)) yield break;

        // Check if already cached
        if (imagePreviewCache.ContainsKey(image.Url))
            yield break;

        UnityWebRequest www = UnityWebRequest.Get(image.Url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] imageData = www.downloadHandler.data;
            Texture2D texture = null;
            bool loadSuccess = false;
            bool isWebP = image.Url.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase);
            
            // Method 1: Try Unity's built-in LoadImage first
            try
            {
                texture = new Texture2D(2, 2);
                loadSuccess = texture.LoadImage(imageData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LudoAIPlugin] Unity built-in decoder failed for image preview: {ex.Message}");
                loadSuccess = false;
            }
            
            // Method 2: If Unity's decoder failed and it's a WebP, try unity.webp library
            if (!loadSuccess && isWebP)
            {
                texture = DecodeWebPImage(imageData);
                loadSuccess = (texture != null);
            }
            
            if (loadSuccess && texture != null)
            {
                imagePreviewCache[image.Url] = texture;
                Debug.Log($"[LudoAIPlugin] Successfully loaded image preview");
            }
            else
            {
                Debug.LogWarning($"[LudoAIPlugin] Failed to decode image preview. Format may not be supported.");
            }
        }
        else
        {
            Debug.LogWarning($"[LudoAIPlugin] Failed to load image preview: {www.error}");
        }
    }

    // ============================
    // === 3D MODEL API CALLS =====
    // ============================

    private IEnumerator Create3DModel()
    {
        // Validate parameters
        if (!ValidateRequired(model3DImageUrl, "Image URL"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRange(model3DTargetFaces, 1000, 200000, "Target face count"))
        {
            isProcessing = false;
            yield break;
        }

        if (model3DTextureSize != 1024 && model3DTextureSize != 2048)
        {
            model3DTextureSize = 2048;
        }

        string normalizedImage = NormalizeImageInputForApi(model3DImageUrl);
        if (string.IsNullOrEmpty(normalizedImage))
        {
            statusMessage = "Failed to prepare image for 3D generation.";
            EditorUtility.DisplayDialog("3D Model Creation Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        // Match Create3DModelPayload from API swagger (no high_detail_shape / 4096 texture).
        var requestData = new Dictionary<string, object>
        {
            ["image"] = normalizedImage,
            ["target_num_faces"] = model3DTargetFaces,
            ["texture_size"] = model3DTextureSize,
            ["texture_type"] = model3DTextureType
        };

        Debug.Log($"[LudoAIPlugin] 3D Model creation request (faces={model3DTargetFaces}, texture={model3DTextureSize}, type={model3DTextureType}, image_chars={normalizedImage.Length})");

        var job = new EditorHttpResult();
        yield return RunApiJob("/assets/3d-model", requestData, "Creating 3D model", job);

        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError("creating 3D model", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Creation Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            current3DModel = JsonConvert.DeserializeObject<Generated3DModel>(job.Body);
            model3DSnapshotTextures.Clear();

            if (current3DModel != null && !string.IsNullOrEmpty(current3DModel.ModelUrl))
            {
                statusMessage = "3D Model created successfully! Use 'Use for Rigging' to continue, or download the model.";
                Debug.Log($"[LudoAIPlugin] {statusMessage} URL: {current3DModel.ModelUrl}");

                if (current3DModel.Snapshots != null && current3DModel.Snapshots.Count > 0)
                {
                    foreach (var snapshotUrl in current3DModel.Snapshots)
                    {
                        EditorCoroutineUtility.StartCoroutineOwnerless(Load3DModelSnapshot(snapshotUrl));
                    }
                }
            }
            else
            {
                current3DModel = null;
                statusMessage = "3D model response contained no model.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage} Body: {job.Body}");
                EditorUtility.DisplayDialog("3D Model Creation Error", statusMessage, "OK");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing 3D model response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Creation Error", statusMessage, "OK");
        }

        isProcessing = false;
    }

    // ============================
    // ====== API JOB RUNNER ======
    // ============================
    // Generation endpoints answer 202 with a job ({ id, status, poll_after_ms }) and the
    // work runs on Ludo's queue. The result arrives by polling GET /assets/jobs/{id}
    // until status is succeeded or failed; a succeeded job's `result` is exactly the
    // body the endpoint documents, so callers parse it as before.

    // How long to keep polling one job before handing control back to the user.
    private float jobTimeoutSeconds = 30 * 60;
    // Long-poll: the server holds the status request until the job finishes or this many
    // seconds pass (max 60), so a job is seen the moment it completes.
    private const int JobLongPollSeconds = 30;
    // Consecutive failed status polls (network, 5xx) tolerated before giving up.
    private const int MaxConsecutivePollFailures = 5;

    private class EditorHttpResult
    {
        public bool Done;
        public bool Success;
        public long StatusCode;
        public string Body = "";
        public string Error = "";
        public float RetryAfterSeconds;
    }

    // Submits a generation and follows its job to the end. On success result.Body holds
    // the operation's result JSON; on failure result.Error / result.Body say why.
    private IEnumerator RunApiJob(string endpoint, Dictionary<string, object> payload, string progressLabel, EditorHttpResult result)
    {
        string fullUrl = apiUrl + endpoint;
        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        string jsonData = JsonConvert.SerializeObject(payload, settings);
        Debug.Log($"[LudoAIPlugin] POST {fullUrl} ({Encoding.UTF8.GetByteCount(jsonData)} bytes)");

        // HttpClient rather than UnityWebRequest: UnityWebRequest often fails with HTTP 0 /
        // "Unknown Error" on large base64 JSON uploads (3D models, images from disk).
        var submit = new EditorHttpResult();
        yield return SendJsonWithHttpClient(HttpMethod.Post, fullUrl, jsonData, 600, submit);

        if (!submit.Success)
        {
            CopyHttpResult(submit, result);
            yield break;
        }

        // 200 is a synchronous body (async: false, or an older server); 202 is a job.
        if (submit.StatusCode != 202)
        {
            CopyHttpResult(submit, result);
            yield break;
        }

        ApiJob job = ParseApiJob(submit.Body);
        if (job == null)
        {
            result.Success = false;
            result.StatusCode = submit.StatusCode;
            result.Error = "Unexpected response from the job queue";
            result.Body = submit.Body;
            yield break;
        }
        Debug.Log($"[LudoAIPlugin] Job {job.Id} {job.Status}");

        double started = EditorApplication.timeSinceStartup;
        int consecutiveFailures = 0;
        float waitSeconds = (job.PollAfterMs ?? 2000) / 1000f;

        while (true)
        {
            if (job.Status == "succeeded")
            {
                result.Success = true;
                result.StatusCode = 200;
                result.Error = "";
                result.Body = job.Result != null ? job.Result.ToString(Formatting.None) : "";
                Debug.Log($"[LudoAIPlugin] Job {job.Id} succeeded after {EditorApplication.timeSinceStartup - started:F0}s");
                yield break;
            }

            if (job.Status == "failed" || job.Status == "canceled")
            {
                result.Success = false;
                result.StatusCode = job.Error != null && job.Error.Status.HasValue ? job.Error.Status.Value : 0;
                result.Error = job.Status == "canceled"
                    ? "The job was canceled"
                    : (job.Error != null && !string.IsNullOrEmpty(job.Error.Message) ? job.Error.Message : "Generation failed");
                result.Body = "";
                yield break;
            }

            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed >= jobTimeoutSeconds)
            {
                result.Success = false;
                result.StatusCode = 0;
                result.Error = $"Job {job.Id} is still {job.Status} after {FormatElapsed(elapsed)}. " +
                               "It may still finish: its result will be listed under your API generations.";
                result.Body = "";
                yield break;
            }

            statusMessage = $"{progressLabel}... {job.Status} ({FormatElapsed(elapsed)})";
            Repaint();

            yield return new EditorWaitForSeconds(Mathf.Max(waitSeconds, 0f));

            var poll = new EditorHttpResult();
            string pollUrl = $"{apiUrl}/assets/jobs/{Uri.EscapeDataString(job.Id)}?wait={JobLongPollSeconds}";
            yield return SendJsonWithHttpClient(HttpMethod.Get, pollUrl, null, JobLongPollSeconds + 60, poll);

            ApiJob next = poll.Success ? ParseApiJob(poll.Body) : null;
            if (next != null)
            {
                consecutiveFailures = 0;
                job = next;
                waitSeconds = (job.PollAfterMs ?? 2000) / 1000f;
                continue;
            }

            // 429: the status endpoint is rate-limited per key; wait as told and carry on.
            if (poll.StatusCode == 429)
            {
                waitSeconds = poll.RetryAfterSeconds > 0 ? poll.RetryAfterSeconds : 5f;
                Debug.LogWarning($"[LudoAIPlugin] Job {job.Id}: status polls rate-limited, retrying in {waitSeconds:F0}s");
                continue;
            }

            // Any other 4xx (404: no such job for this key) will not get better by retrying.
            bool transient = poll.Success || poll.StatusCode == 0 || poll.StatusCode == 408 || poll.StatusCode >= 500;
            consecutiveFailures++;
            if (!transient || consecutiveFailures > MaxConsecutivePollFailures)
            {
                CopyHttpResult(poll, result);
                result.Success = false;
                if (string.IsNullOrEmpty(result.Error)) result.Error = "Could not read the job status";
                result.Error = $"{result.Error} (job {job.Id})";
                yield break;
            }

            waitSeconds = Mathf.Min(2f * consecutiveFailures, 10f);
            Debug.LogWarning($"[LudoAIPlugin] Job {job.Id}: status poll failed ({(string.IsNullOrEmpty(poll.Error) ? "HTTP " + poll.StatusCode : poll.Error)}), retrying in {waitSeconds:F0}s");
        }
    }

    private static ApiJob ParseApiJob(string body)
    {
        try
        {
            ApiJob job = JsonConvert.DeserializeObject<ApiJob>(body);
            return job != null && !string.IsNullOrEmpty(job.Id) && !string.IsNullOrEmpty(job.Status) ? job : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void CopyHttpResult(EditorHttpResult from, EditorHttpResult to)
    {
        to.Success = from.Success;
        to.StatusCode = from.StatusCode;
        to.Body = from.Body;
        to.Error = from.Error;
        to.RetryAfterSeconds = from.RetryAfterSeconds;
    }

    private static string FormatElapsed(double seconds)
    {
        int s = (int)seconds;
        return s < 60 ? $"{s}s" : $"{s / 60}m {s % 60:D2}s";
    }

    private IEnumerator SendJsonWithHttpClient(HttpMethod method, string url, string jsonBody, int timeoutSeconds, EditorHttpResult result)
    {
        string authKey = apiKey;
        Task task = Task.Run(async () =>
        {
            try
            {
                using (var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                })
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    using (var request = new HttpRequestMessage(method, url))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "ApiKey " + authKey);
                        request.Headers.TryAddWithoutValidation("x-ludo-tool", "unity");
                        if (jsonBody != null)
                        {
                            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                        }

                        using (var response = await client.SendAsync(request).ConfigureAwait(false))
                        {
                            result.StatusCode = (long)response.StatusCode;
                            result.Body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            result.Success = response.IsSuccessStatusCode;
                            if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                            {
                                result.RetryAfterSeconds = (float)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                            }
                            if (!result.Success && string.IsNullOrEmpty(result.Error))
                            {
                                result.Error = $"HTTP {result.StatusCode}";
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.StatusCode = 0;
                result.Error = e.GetBaseException().Message;
                Debug.LogWarning($"[LudoAIPlugin] HttpClient {method} {url} failed: {e.GetBaseException().Message}");
            }
            finally
            {
                result.Done = true;
            }
        });

        while (!result.Done)
        {
            yield return new EditorWaitForSeconds(0.25f);
        }

        // Ensure task observed
        try { task.Wait(100); } catch { /* logged above */ }
    }

    private string FormatEditorHttpError(string action, EditorHttpResult result)
    {
        // Prefer the API's own explanation ({"message": ...}) over the bare status line.
        string reason = null;
        string extra = null;
        if (!string.IsNullOrEmpty(result.Body))
        {
            string body = result.Body.Trim();
            try
            {
                JObject err = JObject.Parse(body);
                reason = err.Value<string>("message");
                if (string.IsNullOrEmpty(reason))
                {
                    extra = TruncateForUi(body, 500);
                }
            }
            catch
            {
                extra = TruncateForUi(body, 500);
            }
        }
        if (string.IsNullOrEmpty(reason))
        {
            reason = string.IsNullOrEmpty(result.Error) ? "Request failed" : result.Error;
        }

        string details = $"Error {action}: {reason}";
        if (result.StatusCode > 0)
        {
            details += $" (HTTP {result.StatusCode})";
        }
        if (!string.IsNullOrEmpty(extra))
        {
            details += $"\n{extra}";
        }
        return details;
    }

    private static string TruncateForUi(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
        return text.Substring(0, maxLen) + "...";
    }

    private IEnumerator Rig3DModel()
    {
        if (!ValidateRequired(model3DRigModelInput, "Model URL or GLB"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["model"] = model3DRigModelInput,
            ["rig_type"] = model3DRigType,
            ["joint_naming"] = model3DJointNaming
        };

        Debug.Log($"[LudoAIPlugin] 3D Model rig request (rig_type={model3DRigType}, joint_naming={model3DJointNaming}, model_chars={model3DRigModelInput.Length})");

        var job = new EditorHttpResult();
        yield return RunApiJob("/assets/3d-model/rig", requestData, "Rigging 3D model", job);

        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError("rigging 3D model", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Rig Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            currentRigged3DModel = JsonConvert.DeserializeObject<Rigged3DModelResult>(job.Body);

            if (currentRigged3DModel != null && !string.IsNullOrEmpty(currentRigged3DModel.ModelUrl))
            {
                statusMessage = "3D Model rigged successfully! Download it or click 'Use for Animation'.";
                Debug.Log($"[LudoAIPlugin] {statusMessage} URL: {currentRigged3DModel.ModelUrl}");
            }
            else
            {
                currentRigged3DModel = null;
                statusMessage = "Rig response contained no model.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage} Body: {job.Body}");
                EditorUtility.DisplayDialog("3D Model Rig Error", statusMessage, "OK");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing rigged 3D model response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Rig Error", statusMessage, "OK");
        }

        isProcessing = false;
    }

    private IEnumerator Animate3DModel()
    {
        if (!ValidateRequired(model3DAnimateModelInput, "Rigged model URL or GLB"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRequired(model3DAnimatePrompt, "Motion prompt"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRange(model3DAnimateNumVariants, 1, 8, "Number of variants"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["model"] = model3DAnimateModelInput,
            ["prompt"] = model3DAnimatePrompt,
            ["mode"] = model3DAnimateMode,
            ["num_variants"] = model3DAnimateNumVariants,
            ["loop"] = model3DAnimateLoop,
            ["augment_prompt"] = model3DAnimateAugmentPrompt
        };

        Debug.Log($"[LudoAIPlugin] 3D Model animate request (prompt={model3DAnimatePrompt}, variants={model3DAnimateNumVariants}, mode={model3DAnimateMode})");

        var job = new EditorHttpResult();
        yield return RunApiJob("/assets/3d-model/animate", requestData, "Animating 3D model", job);

        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError("animating 3D model", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Animate Error", statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            AnimationCandidates response = JsonConvert.DeserializeObject<AnimationCandidates>(job.Body);

            if (response != null && response.Animations != null && response.Animations.Count > 0)
            {
                current3DAnimations = response.Animations;
                statusMessage = $"Generated {current3DAnimations.Count} animation candidate(s). Open MP4 previews and download the GLB clips you want.";
                Debug.Log($"[LudoAIPlugin] {statusMessage}");
            }
            else
            {
                current3DAnimations = new List<AnimationClip3D>();
                statusMessage = "Animation response contained no candidates.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage}");
                EditorUtility.DisplayDialog("3D Model Animate Error", statusMessage, "OK");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing 3D animation response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog("3D Model Animate Error", statusMessage, "OK");
        }

        isProcessing = false;
    }

    private IEnumerator Load3DModelSnapshot(string snapshotUrl)
    {
        if (string.IsNullOrEmpty(snapshotUrl)) yield break;

        // Download raw bytes — UnityWebRequestTexture fails on WebP (common for Ludo asset URLs).
        using (UnityWebRequest www = UnityWebRequest.Get(snapshotUrl))
        {
            www.SetRequestHeader("x-ludo-tool", "unity");
            www.timeout = 120;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success || www.downloadHandler == null || www.downloadHandler.data == null)
            {
                Debug.LogWarning($"[LudoAIPlugin] Failed to download 3D model snapshot: {www.error}");
                yield break;
            }

            byte[] imageData = www.downloadHandler.data;
            Texture2D texture = null;
            bool loadSuccess = false;

            try
            {
                texture = new Texture2D(2, 2);
                loadSuccess = texture.LoadImage(imageData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LudoAIPlugin] Unity decoder failed for 3D snapshot: {ex.Message}");
                loadSuccess = false;
            }

            bool isWebP = imageData.Length >= 12 &&
                          imageData[0] == (byte)'R' && imageData[1] == (byte)'I' && imageData[2] == (byte)'F' && imageData[3] == (byte)'F' &&
                          imageData[8] == (byte)'W' && imageData[9] == (byte)'E' && imageData[10] == (byte)'B' && imageData[11] == (byte)'P';

            if (!loadSuccess || isWebP)
            {
                Texture2D webpTexture = DecodeWebPImage(imageData);
                if (webpTexture != null)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    texture = webpTexture;
                    loadSuccess = true;
                }
            }

            if (loadSuccess && texture != null)
            {
                model3DSnapshotTextures.Add(texture);
                Repaint();
            }
            else
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                Debug.LogWarning($"[LudoAIPlugin] Failed to decode 3D model snapshot from: {snapshotUrl}");
            }
        }
    }

    // ============================
    // ===== AUDIO API CALLS ======
    // ============================

    private IEnumerator CreateSoundEffect()
    {
        // Validate parameters
        if (!ValidateRequired(soundEffectDescription, "Sound effect description"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRange(soundEffectDuration, 0, 10, "Sound effect duration"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["description"] = soundEffectDescription,
            ["duration"] = soundEffectDuration,
            ["augment_prompt"] = soundEffectAugment
        };

        yield return GenerateAudio("/audio/sound-effect", requestData, "sound effect", "Sound Effect", audio => currentSoundEffect = audio);
    }

    private IEnumerator CreateMusic()
    {
        // Validate parameters
        if (!ValidateRequired(musicDescription, "Music description"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["description"] = musicDescription,
            ["augment_prompt"] = musicAugment
        };

        // Add optional lyrics
        if (!string.IsNullOrEmpty(musicLyrics))
            requestData["lyrics"] = musicLyrics;

        yield return GenerateAudio("/audio/music", requestData, "music", "Music", audio => currentMusic = audio);
    }

    private IEnumerator CreateVoice()
    {
        // Validate parameters
        if (!ValidateRequired(voiceDescription, "Voice description"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRequired(voiceText, "Voice text"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateTextLength(voiceText, 200, "Voice text"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["voice_description"] = voiceDescription,
            ["text"] = voiceText,
            ["type"] = voiceType,
            ["augment_prompt"] = voiceAugment
        };

        yield return GenerateAudio("/audio/voice", requestData, "voice", "Voice", audio => currentVoice = audio);
    }

    private IEnumerator CreateSpeech()
    {
        // Validate parameters
        if (!ValidateRequired(speechText, "Speech text"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateTextLength(speechText, 1000, "Speech text"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateRequired(speechSample, "Voice sample"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["text"] = speechText,
            ["sample"] = speechSample
        };

        yield return GenerateAudio("/audio/speech", requestData, "speech", "Speech", audio => currentSpeech = audio);
    }

    private IEnumerator CreateSpeechPreset()
    {
        // Validate parameters
        if (!ValidateRequired(speechPresetText, "Speech text"))
        {
            isProcessing = false;
            yield break;
        }

        if (!ValidateTextLength(speechPresetText, 1000, "Speech text"))
        {
            isProcessing = false;
            yield break;
        }

        var requestData = new Dictionary<string, object>
        {
            ["text"] = speechPresetText,
            ["voice_preset_id"] = speechPresetVoiceId,
            ["emotion"] = speechPresetEmotion,
            ["language"] = speechPresetLanguage
        };

        yield return GenerateAudio("/audio/speech-preset", requestData, "speech preset", "Speech Preset", audio => currentSpeechPreset = audio);
    }

    // Shared by every audio generator: run the job, then hand the result to `assign`.
    private IEnumerator GenerateAudio(string endpoint, Dictionary<string, object> requestData, string what, string dialogName, Action<GeneratedAudio> assign)
    {
        var job = new EditorHttpResult();
        yield return RunApiJob(endpoint, requestData, $"Generating {what}", job);

        string dialogTitle = $"{dialogName} Generation Error";
        if (!job.Success)
        {
            statusMessage = FormatEditorHttpError($"generating {what}", job);
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog(dialogTitle, statusMessage, "OK");
            isProcessing = false;
            yield break;
        }

        try
        {
            GeneratedAudio audio = JsonConvert.DeserializeObject<GeneratedAudio>(job.Body);

            if (audio != null && !string.IsNullOrEmpty(audio.Url))
            {
                assign(audio);
                statusMessage = $"{char.ToUpper(what[0])}{what.Substring(1)} generated successfully!";
                Debug.Log($"[LudoAIPlugin] {statusMessage}");
            }
            else
            {
                statusMessage = $"The {what} response contained no audio.";
                Debug.LogWarning($"[LudoAIPlugin] {statusMessage} Body: {job.Body}");
                EditorUtility.DisplayDialog(dialogTitle, statusMessage, "OK");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Error parsing {what} response: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] {statusMessage}");
            EditorUtility.DisplayDialog(dialogTitle, statusMessage, "OK");
        }

        isProcessing = false;
    }

    // ============================
    // ==== SPRITE SAVE HELPERS ===
    // ============================

    private void SaveSpriteToFile(GeneratedSprite sprite)
    {
        if (sprite == null || sprite.Image == null) return;

        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveSprite(sprite));
    }

    private IEnumerator DownloadAndSaveSprite(GeneratedSprite sprite)
    {
        // Download sprite image
        UnityWebRequest www = UnityWebRequest.Get(sprite.Image.Url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download sprite: {www.error}");
            EditorUtility.DisplayDialog("Download Error", $"Failed to download sprite: {www.error}", "OK");
        }
        else
        {
            byte[] imageData = www.downloadHandler.data;
            byte[] finalData = null;
            string extension = "png";
            Texture2D texture = null;
            bool loadSuccess = false;
            bool isWebP = sprite.Image.Url.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase);
            
            // Try multiple methods to decode the image
            // Method 1: Try Unity's built-in LoadImage first
            try
            {
                texture = new Texture2D(2, 2);
                loadSuccess = texture.LoadImage(imageData);
                if (loadSuccess)
                {
                    Debug.Log($"[LudoAIPlugin] Successfully loaded image using Unity's built-in decoder.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LudoAIPlugin] Unity built-in decoder failed: {ex.Message}");
                loadSuccess = false;
            }
            
            // Method 2: If Unity's decoder failed and it's a WebP, try unity.webp library
            if (!loadSuccess && isWebP)
            {
                texture = DecodeWebPImage(imageData);
                loadSuccess = (texture != null);
            }
            
            if (loadSuccess && texture != null)
            {
                // Successfully loaded, convert to PNG
                finalData = texture.EncodeToPNG();
                extension = "png";
                Debug.Log($"[LudoAIPlugin] Successfully converted image to PNG format.");
            }
            else
            {
                // All decoding methods failed - save raw data with warning
                finalData = imageData;
                if (isWebP)
                {
                    extension = "png";  // Force PNG extension even for WebP data
                    string unityVersion = Application.unityVersion;

                    Debug.LogError($"[LudoAIPlugin] ========================================");
                    Debug.LogError($"[LudoAIPlugin] WEBP DECODING FAILED!");
                    Debug.LogError($"[LudoAIPlugin] ========================================");
                    Debug.LogError($"[LudoAIPlugin] All WebP decoding methods failed.");
                    Debug.LogError($"[LudoAIPlugin] The WebP file may be corrupted, animated, or in an unsupported format.");
                    Debug.LogError($"[LudoAIPlugin] ");
                    Debug.LogError($"[LudoAIPlugin] File will be saved as .png - you need to convert it manually.");
                    Debug.LogError($"[LudoAIPlugin] ========================================");

                    bool openConverter = EditorUtility.DisplayDialog("WebP Decoding Failed",
                        $"Unity {unityVersion} could not decode this WebP image.\n\n" +
                        $"The WebP file may be corrupted, animated, or in an unsupported format.\n\n" +
                        $"The sprite will be saved as .png file.\n" +
                        $"You MUST convert it to PNG for Unity to use it.\n\n" +
                        $"Would you like to open an online converter?",
                        "Open Converter", "Save Anyway");
                    
                    if (openConverter)
                    {
                        Application.OpenURL("https://cloudconvert.com/webp-to-png");
                        Debug.Log($"[LudoAIPlugin] Opened CloudConvert in browser. Drag your file to convert it.");
                    }
                }
                else if (sprite.Image.Url.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) || 
                         sprite.Image.Url.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
                {
                    extension = "jpg";
                }
            }

            string fileName = $"Sprite_{sprite.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string savePath = EditorUtility.SaveFilePanel("Save Sprite", "Assets", fileName, extension);

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, finalData);
                AssetDatabase.Refresh();
                Debug.Log($"[LudoAIPlugin] Sprite saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"Sprite saved successfully as {extension.ToUpper()} to:\n{savePath}", "OK");
            }
        }
    }

    private IEnumerator LoadSpritePreview(GeneratedSprite sprite)
    {
        if (sprite == null || sprite.Image == null || string.IsNullOrEmpty(sprite.Image.Url))
            yield break;

        // Check if already cached
        if (spritePreviewCache.ContainsKey(sprite.Id))
            yield break;

        UnityWebRequest www = UnityWebRequest.Get(sprite.Image.Url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] imageData = www.downloadHandler.data;
            Texture2D texture = null;
            bool loadSuccess = false;
            bool isWebP = sprite.Image.Url.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase);
            
            // Method 1: Try Unity's built-in LoadImage first
            try
            {
                texture = new Texture2D(2, 2);
                loadSuccess = texture.LoadImage(imageData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LudoAIPlugin] Unity built-in decoder failed for preview: {ex.Message}");
                loadSuccess = false;
            }
            
            // Method 2: If Unity's decoder failed and it's a WebP, try unity.webp library
            if (!loadSuccess && isWebP)
            {
                texture = DecodeWebPImage(imageData);
                loadSuccess = (texture != null);
            }
            
            if (loadSuccess && texture != null)
            {
                // Successfully loaded! Now convert to ensure it's in a format Unity can display
                // Re-encode as PNG then reload to ensure compatibility
                byte[] pngData = texture.EncodeToPNG();
                
                // Create a new texture and load the PNG data
                Texture2D displayTexture = new Texture2D(2, 2);
                if (displayTexture.LoadImage(pngData))
                {
                    spritePreviewCache[sprite.Id] = displayTexture;
                    Debug.Log($"[LudoAIPlugin] Loaded and converted preview for sprite: {sprite.Id}");
                }
                else
                {
                    // Fallback: use original texture if PNG reload fails
                    spritePreviewCache[sprite.Id] = texture;
                    Debug.Log($"[LudoAIPlugin] Loaded preview for sprite: {sprite.Id}");
                }
            }
            else
            {
                string unityVersion = Application.unityVersion;
                Debug.LogWarning($"[LudoAIPlugin] Could not load preview for sprite {sprite.Id}. All decoding methods failed.");
                Debug.LogWarning($"[LudoAIPlugin] The WebP file may be corrupted, animated, or in an unsupported format.");
                Debug.LogWarning($"[LudoAIPlugin] Preview will show placeholder, but image can still be saved.");
                
                // Create a simple placeholder texture with some visual indication
                texture = new Texture2D(128, 128);
                Color[] colors = new Color[128 * 128];
                for (int i = 0; i < colors.Length; i++)
                {
                    // Create a subtle pattern so it's clear this is a placeholder
                    int x = i % 128;
                    int y = i / 128;
                    bool isCheckerboard = ((x / 16) + (y / 16)) % 2 == 0;
                    colors[i] = isCheckerboard ? new Color(0.3f, 0.3f, 0.35f, 1f) : new Color(0.25f, 0.25f, 0.3f, 1f);
                }
                texture.SetPixels(colors);
                texture.Apply();
                spritePreviewCache[sprite.Id] = texture;
            }
        }
        else
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download sprite preview: {www.error}");
        }
    }

    private IEnumerator LoadSpritesheetPreview(string url)
    {
        // Use DownloadHandlerBuffer to handle all image formats including webp
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            byte[] imageData = www.downloadHandler.data;
            Texture2D texture = null;
            bool loadSuccess = false;
            bool isWebP = url.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase);
            
            // Method 1: Try Unity's built-in LoadImage first
            try
            {
                texture = new Texture2D(2, 2);
                loadSuccess = texture.LoadImage(imageData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LudoAIPlugin] Unity built-in decoder failed for spritesheet preview: {ex.Message}");
                loadSuccess = false;
            }
            
            // Method 2: If Unity's decoder failed and it's a WebP, try unity.webp library
            if (!loadSuccess && isWebP)
            {
                texture = DecodeWebPImage(imageData);
                loadSuccess = (texture != null);
            }
            
            if (loadSuccess && texture != null)
            {
                // Successfully loaded! Convert to PNG format for better compatibility
                byte[] pngData = texture.EncodeToPNG();
                
                // Create display texture from PNG data
                spritesheetPreviewTexture = new Texture2D(2, 2);
                if (spritesheetPreviewTexture.LoadImage(pngData))
                {
                    Debug.Log("[LudoAIPlugin] Spritesheet preview loaded and converted successfully.");
                }
                else
                {
                    // Fallback to original texture
                    spritesheetPreviewTexture = texture;
                    Debug.Log("[LudoAIPlugin] Spritesheet preview loaded successfully.");
                }
            }
            else
            {
                Debug.LogError("[LudoAIPlugin] Failed to load spritesheet preview image data. All decoding methods failed.");
            }
        }
        else
        {
            Debug.LogError($"[LudoAIPlugin] Failed to load spritesheet preview: {www.error}");
        }
    }

    private void SaveSpritesheetToFile(GeneratedSpritesheet spritesheet)
    {
        if (spritesheet == null || string.IsNullOrEmpty(spritesheet.SpriteSheetB64)) return;

        // SpriteSheetB64 is actually a URL, so download it
        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveSpritesheet(spritesheet));
    }

    private IEnumerator DownloadAndSaveSpritesheet(GeneratedSpritesheet spritesheet)
    {
        // Use DownloadHandlerBuffer to handle all formats
        UnityWebRequest www = UnityWebRequest.Get(spritesheet.SpriteSheetB64);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.downloadHandler = new DownloadHandlerBuffer();
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Error downloading spritesheet: {www.error}");
            EditorUtility.DisplayDialog("Error", $"Failed to download spritesheet:\n{www.error}", "OK");
        }
        else
        {
            byte[] imageData = www.downloadHandler.data;
            byte[] finalData = null;
            string extension = "png";
            
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageData))
            {
                // Successfully loaded, convert to PNG
                finalData = texture.EncodeToPNG();
                extension = "png";
                Debug.Log($"[LudoAIPlugin] Successfully converted spritesheet to PNG format.");
            }
            else
            {
                // If conversion fails, save as original format
                finalData = imageData;
                extension = spritesheet.SpriteSheetB64.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase) ? "png" : "png";  // Force PNG extension even for WebP data
                Debug.LogWarning($"[LudoAIPlugin] Could not convert spritesheet. Saving as {extension.ToUpper()}.");
            }

            string fileName = $"Spritesheet_{spritesheet.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string savePath = EditorUtility.SaveFilePanel("Save Spritesheet", "Assets", fileName, extension);

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, finalData);
                AssetDatabase.Refresh();
                Debug.Log($"[LudoAIPlugin] Spritesheet saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"Spritesheet saved successfully as {extension.ToUpper()} to:\n{savePath}", "OK");
            }
        }
    }

    private void SaveGifToFile(GeneratedSpritesheet spritesheet)
    {
        if (spritesheet == null || string.IsNullOrEmpty(spritesheet.GifB64)) return;

        try
        {
            byte[] bytes = Convert.FromBase64String(spritesheet.GifB64);
            string fileName = $"Preview_{spritesheet.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.gif";
            string savePath = EditorUtility.SaveFilePanel("Save GIF Preview", "Assets", fileName, "gif");

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, bytes);
                Debug.Log($"[LudoAIPlugin] GIF preview saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"GIF preview saved successfully to:\n{savePath}", "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to save GIF: {e.Message}");
            EditorUtility.DisplayDialog("Save Error", $"Failed to save GIF: {e.Message}", "OK");
        }
    }

    // ============================
    // === NEW SAVE HELPERS =======
    // ============================

    private void SaveImageToFile(GeneratedImage image)
    {
        if (image == null || string.IsNullOrEmpty(image.Url)) return;

        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveImage(image));
    }

    private IEnumerator DownloadAndSaveImage(GeneratedImage image)
    {
        UnityWebRequest www = UnityWebRequest.Get(image.Url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download image: {www.error}");
            EditorUtility.DisplayDialog("Download Error", $"Failed to download image: {www.error}", "OK");
            yield break;
        }

        try
        {
            byte[] imageData = www.downloadHandler.data;
            string fileName = $"GeneratedImage_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string savePath = EditorUtility.SaveFilePanel("Save Image", "Assets", fileName, "png");

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, imageData);
                
                if (savePath.StartsWith(Application.dataPath))
                {
                    AssetDatabase.Refresh();
                }
                
                Debug.Log($"[LudoAIPlugin] Image saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"Image saved successfully to:\n{savePath}", "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to save image: {e.Message}");
            EditorUtility.DisplayDialog("Save Error", $"Failed to save image: {e.Message}", "OK");
        }
    }

    private void Save3DModelToFile(Generated3DModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.ModelUrl)) return;

        SaveGlbFromUrl(model.ModelUrl, "Generated3DModel");
    }

    private void SaveGlbFromUrl(string url, string defaultNamePrefix)
    {
        if (string.IsNullOrEmpty(url)) return;

        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveGlbFromUrl(url, defaultNamePrefix));
    }

    private IEnumerator DownloadAndSave3DModel(Generated3DModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.ModelUrl)) yield break;

        yield return DownloadAndSaveGlbFromUrl(model.ModelUrl, "Generated3DModel");
    }

    private IEnumerator DownloadAndSaveGlbFromUrl(string url, string defaultNamePrefix)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        www.timeout = 300;
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download 3D model: {www.error}");
            EditorUtility.DisplayDialog("Download Error", $"Failed to download 3D model: {www.error}", "OK");
            yield break;
        }

        try
        {
            byte[] modelData = www.downloadHandler.data;
            string extension = url.IndexOf(".fbx", StringComparison.OrdinalIgnoreCase) >= 0 ? "fbx" : "glb";
            string fileName = $"{defaultNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string defaultDir = Path.Combine(Application.dataPath, "3DModels");
            if (!Directory.Exists(defaultDir))
            {
                Directory.CreateDirectory(defaultDir);
            }
            string savePath = EditorUtility.SaveFilePanel("Save 3D Model", defaultDir, fileName, extension);

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, modelData);

                if (savePath.StartsWith(Application.dataPath))
                {
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[LudoAIPlugin] 3D Model saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"3D Model saved successfully to:\n{savePath}", "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to save 3D model: {e.Message}");
            EditorUtility.DisplayDialog("Save Error", $"Failed to save 3D model: {e.Message}", "OK");
        }
    }

    private IEnumerator DownloadAll3DAnimations()
    {
        if (current3DAnimations == null || current3DAnimations.Count == 0)
        {
            yield break;
        }

        string folderPath = EditorUtility.SaveFolderPanel("Save Animation GLBs", "Assets", "3DModels");
        if (string.IsNullOrEmpty(folderPath))
        {
            yield break;
        }

        int savedCount = 0;
        for (int i = 0; i < current3DAnimations.Count; i++)
        {
            AnimationClip3D clip = current3DAnimations[i];
            if (clip == null || string.IsNullOrEmpty(clip.GlbUrl))
            {
                continue;
            }

            statusMessage = $"Downloading animation {i + 1}/{current3DAnimations.Count}...";

            UnityWebRequest www = UnityWebRequest.Get(clip.GlbUrl);
            www.SetRequestHeader("x-ludo-tool", "unity");
            www.timeout = 300;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LudoAIPlugin] Failed to download animation {i + 1}: {www.error}");
                continue;
            }

            try
            {
                string title = !string.IsNullOrEmpty(clip.ClipName) ? clip.ClipName : $"Variant{i + 1}";
                string fileName = $"Anim3D_{SanitizeFileName(title)}_{DateTime.Now:yyyyMMdd_HHmmss}_{i + 1}.glb";
                string savePath = Path.Combine(folderPath, fileName);
                File.WriteAllBytes(savePath, www.downloadHandler.data);
                savedCount++;
                Debug.Log($"[LudoAIPlugin] Animation saved to: {savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LudoAIPlugin] Failed to save animation {i + 1}: {e.Message}");
            }
        }

        if (folderPath.StartsWith(Application.dataPath) || folderPath.Replace('\\', '/').Contains("/Assets"))
        {
            AssetDatabase.Refresh();
        }

        statusMessage = $"Downloaded {savedCount} of {current3DAnimations.Count} animation GLB(s).";
        EditorUtility.DisplayDialog("Download Complete", statusMessage, "OK");
    }

    private string LoadGlbFromProjectAsDataUri()
    {
        string path = EditorUtility.OpenFilePanel("Select GLB Model", "Assets", "glb");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            byte[] modelBytes = File.ReadAllBytes(path);
            string base64 = Convert.ToBase64String(modelBytes);
            Debug.Log($"[LudoAIPlugin] Loaded GLB from: {path}");
            return $"data:model/gltf-binary;base64,{base64}";
        }
        catch (Exception e)
        {
            statusMessage = $"Error loading GLB: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] Error loading GLB: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to load GLB: {e.Message}", "OK");
            return null;
        }
    }

    private string LoadImageFileAsDataUri(string path)
    {
        try
        {
            byte[] imageBytes = File.ReadAllBytes(path);
            string dataUri = CompressImageBytesFor3DApi(imageBytes);
            if (string.IsNullOrEmpty(dataUri))
            {
                statusMessage = "Could not decode image. Use PNG, JPEG, or WebP.";
                EditorUtility.DisplayDialog("Error", statusMessage, "OK");
                return null;
            }

            return dataUri;
        }
        catch (Exception e)
        {
            statusMessage = $"Error loading image: {e.Message}";
            Debug.LogError($"[LudoAIPlugin] Error loading image: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to load image: {e.Message}", "OK");
            return null;
        }
    }

    private string NormalizeImageInputForApi(string imageInput)
    {
        if (string.IsNullOrEmpty(imageInput))
        {
            return null;
        }

        // Remote URLs are passed through unchanged.
        if (imageInput.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageInput.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return imageInput;
        }

        string data = imageInput.Trim();
        string base64Part = data;
        if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int comma = data.IndexOf(',');
            if (comma < 0)
            {
                return null;
            }
            base64Part = data.Substring(comma + 1);
        }

        try
        {
            byte[] raw = Convert.FromBase64String(base64Part);
            return CompressImageBytesFor3DApi(raw);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LudoAIPlugin] Could not normalize image input: {e.Message}");
            return imageInput;
        }
    }

    /// <summary>
    /// Decode any common format, optionally downscale, and encode as RGBA PNG.
    /// The 3D API requires RGBA with transparency — JPEG / flattened RGB is rejected.
    /// Downscaling keeps UnityWebRequest uploads reliable (avoids HTTP 0 transmit failures).
    /// </summary>
    private string CompressImageBytesFor3DApi(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 12)
        {
            return null;
        }

        bool isPng = imageBytes.Length >= 8 &&
                     imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47;

        Texture2D source = DecodeImageBytesToTexture(imageBytes);
        if (source == null)
        {
            return null;
        }

        int srcW = source.width;
        int srcH = source.height;
        Color32[] pixels = source.GetPixels32();
        bool hasTransparency = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a < 255)
            {
                hasTransparency = true;
                break;
            }
        }

        // Prefer original PNG bytes when already RGBA-friendly and reasonably sized —
        // re-encoding can inflate size and Unity may fail the upload (HTTP 0).
        const int maxSide = 1024;
        const int maxPassThroughBytes = 450000;
        if (isPng && hasTransparency && srcW <= maxSide && srcH <= maxSide && imageBytes.Length <= maxPassThroughBytes)
        {
            UnityEngine.Object.DestroyImmediate(source);
            Debug.Log($"[LudoAIPlugin] Using original RGBA PNG for 3D upload ({imageBytes.Length} bytes, {srcW}x{srcH}).");
            return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        }

        Texture2D working = source;
        bool createdScaled = false;
        if (srcW > maxSide || srcH > maxSide)
        {
            float scale = Mathf.Min((float)maxSide / srcW, (float)maxSide / srcH);
            int newW = Mathf.Max(1, Mathf.RoundToInt(srcW * scale));
            int newH = Mathf.Max(1, Mathf.RoundToInt(srcH * scale));
            working = ResizeTexture(source, newW, newH);
            createdScaled = true;
            Debug.Log($"[LudoAIPlugin] Resized 3D source image to {newW}x{newH} for upload reliability.");
        }

        Texture2D rgba = working;
        if (working.format != TextureFormat.RGBA32 && working.format != TextureFormat.ARGB32)
        {
            rgba = new Texture2D(working.width, working.height, TextureFormat.RGBA32, false);
            rgba.SetPixels32(working.GetPixels32());
            rgba.Apply(false, false);
        }

        int outW = rgba.width;
        int outH = rgba.height;
        byte[] pngBytes = rgba.EncodeToPNG();

        if (rgba != working && rgba != source)
        {
            UnityEngine.Object.DestroyImmediate(rgba);
        }
        if (createdScaled)
        {
            UnityEngine.Object.DestroyImmediate(working);
        }
        UnityEngine.Object.DestroyImmediate(source);

        if (pngBytes == null || pngBytes.Length == 0)
        {
            return null;
        }

        if (!hasTransparency)
        {
            Debug.LogWarning("[LudoAIPlugin] Source image has no transparency. The 3D API requires RGBA with alpha — generation may fail. Prefer a PNG with a transparent background.");
        }

        Debug.Log($"[LudoAIPlugin] Prepared 3D upload image as RGBA PNG ({pngBytes.Length} bytes, {outW}x{outH}).");
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    private Texture2D DecodeImageBytesToTexture(byte[] imageBytes)
    {
        bool isWebP = imageBytes.Length >= 12 &&
                      imageBytes[0] == (byte)'R' && imageBytes[1] == (byte)'I' && imageBytes[2] == (byte)'F' && imageBytes[3] == (byte)'F' &&
                      imageBytes[8] == (byte)'W' && imageBytes[9] == (byte)'E' && imageBytes[10] == (byte)'B' && imageBytes[11] == (byte)'P';

        if (isWebP)
        {
            return DecodeWebPImage(imageBytes);
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(imageBytes, false))
        {
            return texture;
        }

        UnityEngine.Object.DestroyImmediate(texture);
        return DecodeWebPImage(imageBytes);
    }

    private static Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private byte[] EnsurePngBytes(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 12)
        {
            return null;
        }

        bool isPng = imageBytes.Length >= 8 &&
                     imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47;
        if (isPng)
        {
            return imageBytes;
        }

        bool isJpeg = imageBytes.Length >= 3 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF;
        if (isJpeg)
        {
            // Re-encode JPEG to PNG so the data URI MIME always matches the bytes.
            Texture2D jpegTex = new Texture2D(2, 2);
            if (!jpegTex.LoadImage(imageBytes))
            {
                UnityEngine.Object.DestroyImmediate(jpegTex);
                return null;
            }
            byte[] jpegAsPng = jpegTex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(jpegTex);
            return jpegAsPng;
        }

        bool isWebP = imageBytes.Length >= 12 &&
                      imageBytes[0] == (byte)'R' && imageBytes[1] == (byte)'I' && imageBytes[2] == (byte)'F' && imageBytes[3] == (byte)'F' &&
                      imageBytes[8] == (byte)'W' && imageBytes[9] == (byte)'E' && imageBytes[10] == (byte)'B' && imageBytes[11] == (byte)'P';

        Texture2D texture = null;
        if (isWebP)
        {
            texture = DecodeWebPImage(imageBytes);
        }
        else
        {
            texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageBytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
        }

        if (texture == null)
        {
            return null;
        }

        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        Debug.Log("[LudoAIPlugin] Converted source image to PNG for 3D API compatibility.");
        return png;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "clip";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "clip" : name;
    }

    private void SaveGeneratedAudioToFile(GeneratedAudio audio, string defaultName)
    {
        if (audio == null || string.IsNullOrEmpty(audio.Url)) return;

        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveGeneratedAudio(audio, defaultName));
    }

    private IEnumerator DownloadAndSaveGeneratedAudio(GeneratedAudio audio, string defaultName)
    {
        UnityWebRequest www = UnityWebRequest.Get(audio.Url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download audio: {www.error}");
            EditorUtility.DisplayDialog("Download Error", $"Failed to download audio: {www.error}", "OK");
            yield break;
        }

        try
        {
            byte[] audioData = www.downloadHandler.data;
            string fileName = $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string savePath = EditorUtility.SaveFilePanel("Save Audio", "Assets", fileName, "wav");

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, audioData);
                
                if (savePath.StartsWith(Application.dataPath))
                {
                    AssetDatabase.Refresh();
                }
                
                Debug.Log($"[LudoAIPlugin] Audio saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"Audio saved successfully to:\n{savePath}", "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to save audio: {e.Message}");
            EditorUtility.DisplayDialog("Save Error", $"Failed to save audio: {e.Message}", "OK");
        }
    }

    // ============================
    // === INITIAL PAYLOAD CLASSES =
    // ============================

    [Serializable]
    public class UserRefreshPayload
    {
        [JsonProperty("generationStyles")]
        public List<string> GenerationStyles { get; set; }

        [JsonProperty("perspectives")]
        public List<string> Perspectives { get; set; }

        [JsonProperty("generationTypes")]
        public List<string> GenerationTypes { get; set; }

        [JsonProperty("generationTypes3d")]
        public List<string> GenerationTypes3d { get; set; }

        [JsonProperty("genres")]
        public List<string> Genres { get; set; }

        [JsonProperty("styles")]
        public List<string> Styles { get; set; }

        [JsonProperty("platforms")]
        public List<string> Platforms { get; set; }

        [JsonProperty("colors")]
        public List<ColorFilter> Colors { get; set; }

        // Add other fields as needed
    }

    [Serializable]
    public class ColorFilter
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    // ============================
    // ===== SPRITE DATA CLASSES ==
    // ============================

    [Serializable]
    public class GeneratedSprite
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("motion_prompt")]
        public string MotionPrompt { get; set; }

        [JsonProperty("original_prompt")]
        public string OriginalPrompt { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("image")]
        public SpriteImage Image { get; set; }
    }

    [Serializable]
    public class SpriteImage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("hints")]
        public List<string> Hints { get; set; }

        [JsonProperty("image_type")]
        public string ImageType { get; set; }

        [JsonProperty("is_safe")]
        public bool IsSafe { get; set; }

        [JsonProperty("nsfw_prob")]
        public float NsfwProb { get; set; }

        [JsonProperty("original_hints")]
        public List<string> OriginalHints { get; set; }

        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("selected_perspective")]
        public string SelectedPerspective { get; set; }

        [JsonProperty("selected_style")]
        public string SelectedStyle { get; set; }
    }

    [Serializable]
    public class GeneratedSpritesheet
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sprite_sheet_b64")]
        public string SpriteSheetB64 { get; set; }

        [JsonProperty("sprite_sheet")]
        public SpritesheetInfo SpriteSheet { get; set; }

        [JsonProperty("gif_b64")]
        public string GifB64 { get; set; }

        [JsonProperty("preview_video_b64")]
        public string PreviewVideoB64 { get; set; }

        [JsonProperty("num_frames")]
        public int NumFrames { get; set; }

        [JsonProperty("target_frame_size")]
        public int TargetFrameSize { get; set; }

        [JsonProperty("loop")]
        public bool Loop { get; set; }

        [JsonProperty("crop")]
        public bool Crop { get; set; }

        [JsonProperty("duration")]
        public float Duration { get; set; }

        [JsonProperty("start_timestamp")]
        public float StartTimestamp { get; set; }

        [JsonProperty("end_timestamp")]
        public float EndTimestamp { get; set; }

        [JsonProperty("pixel_art_filter")]
        public string PixelArtFilter { get; set; }

        [JsonProperty("original_prompt")]
        public string OriginalPrompt { get; set; }

        [JsonProperty("motion_prompt")]
        public string MotionPrompt { get; set; }

        [JsonProperty("image")]
        public SpriteImage Image { get; set; }

        [JsonProperty("video")]
        public VideoInfo Video { get; set; }
    }

    [Serializable]
    public class SpritesheetInfo
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("format_description")]
        public string FormatDescription { get; set; }
    }

    [Serializable]
    public class VideoInfo
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }
    // ============================
    // ===== NEW DATA CLASSES =====
    // ============================

    [Serializable]
    public class GeneratedImage
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    [Serializable]
    public class Generated3DModel
    {
        [JsonProperty("model_url")]
        public string ModelUrl { get; set; }

        [JsonProperty("snapshots")]
        public List<string> Snapshots { get; set; }
    }

    [Serializable]
    public class Rigged3DModelResult
    {
        [JsonProperty("model_url")]
        public string ModelUrl { get; set; }

        [JsonProperty("rigged")]
        public bool Rigged { get; set; }
    }

    [Serializable]
    public class AnimationCandidates
    {
        [JsonProperty("animations")]
        public List<AnimationClip3D> Animations { get; set; }
    }

    [Serializable]
    public class AnimationClip3D
    {
        [JsonProperty("clip_name")]
        public string ClipName { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("preset_id")]
        public string PresetId { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("seed")]
        public int Seed { get; set; }

        [JsonProperty("glb_url")]
        public string GlbUrl { get; set; }

        [JsonProperty("preview_url")]
        public string PreviewUrl { get; set; }

        [JsonProperty("motion")]
        public float Motion { get; set; }

        [JsonProperty("fit_rmse")]
        public float FitRmse { get; set; }
    }

    [Serializable]
    public class GeneratedAudio
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    [Serializable]
    public class AnimatedSpriteResponse
    {
        [JsonProperty("spritesheet_url")]
        public string SpritesheetUrl { get; set; }

        [JsonProperty("video_url")]
        public string VideoUrl { get; set; }

        [JsonProperty("gif_url")]
        public string GifUrl { get; set; }

        [JsonProperty("audio_url")]
        public string AudioUrl { get; set; }

        [JsonProperty("num_frames")]
        public int NumFrames { get; set; }

        [JsonProperty("num_cols")]
        public int NumCols { get; set; }

        [JsonProperty("num_rows")]
        public int NumRows { get; set; }

        [JsonProperty("duration")]
        public float Duration { get; set; }
    }

    // GET /assets/jobs/{id} (and the 202 a generation endpoint answers with).
    [Serializable]
    public class ApiJob
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("task_type")]
        public string TaskType { get; set; }

        // queued | running | succeeded | failed | canceled
        [JsonProperty("status")]
        public string Status { get; set; }

        // Present when succeeded: the operation's documented response body.
        [JsonProperty("result")]
        public JToken Result { get; set; }

        [JsonProperty("error")]
        public ApiJobError Error { get; set; }

        // Present while queued/running: wait at least this long before polling again.
        [JsonProperty("poll_after_ms")]
        public int? PollAfterMs { get; set; }
    }

    [Serializable]
    public class ApiJobError
    {
        [JsonProperty("status")]
        public int? Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}