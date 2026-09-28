using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.EditorCoroutines.Editor;
using System.Text;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WebP;

public class LudoAIPlugin : EditorWindow
{
    // ============================
    // ======= CONFIGURATION =======
    // ============================
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

    private GeneratedSprite selectedSprite = null;
    
    
    // Spritesheet Variables
    private string spritesheetMotionHint = "walking";
    private string spritesheetInitialImageUrl = ""; // Manual URL input for initial image
    private bool spritesheetLoop = true;
    private bool spritesheetCrop = false;
    private int spritesheetFrames = 9;
    private int spritesheetFrameSize = 256;
    private float spritesheetMarginHorizontal = 0.1f;
    private float spritesheetMarginVertical = 0.1f;
    private string spritesheetMarginMode = "auto"; // "auto", "manual", or "none"
    private bool spritesheetGif = false;
    private GeneratedSpritesheet currentSpritesheet = null;
    private Texture2D spritesheetPreviewTexture = null;
    
    // Spritesheet export options
    private readonly int[] frameOptions = { 4, 9, 16, 25, 36, 49, 64 };
    // 0 = maximum resolution, -1 = AI 1.5x upscale, -9 = match the input frame's size and position
    private readonly int[] frameSizeOptions = { 32, 64, 96, 128, 192, 256, 384, 0, -1, -9 };
    private readonly string[] frameSizeLabels = { "32x32", "64x64", "96x96", "128x128", "192x192", "256x256", "384x384", "Max", "AI upscale (1.5x)", "Match input frame" };
    private readonly string[] marginModeOptions = { "auto", "manual", "none" };
    private readonly string[] marginModeLabels = { "Auto", "Manual", "No margin" };
    
    private Vector2 spritesheetScrollPosition;

    // ============================
    // === NEW FEATURE VARIABLES ==
    // ============================

    // Image Generation Variables
    private string imagePrompt = "";
    private string imageType = "screenshot";
    private string imageArtStyle = "Any style";
    private string imagePerspective = "Any perspective";
    private string imageAspectRatio = "default";
    private int imageCount = 1;
    private bool imageAugmentPrompt = true;
    private List<GeneratedImage> generatedImages = new List<GeneratedImage>();
    private GeneratedImage selectedImage = null;
    private Dictionary<string, Texture2D> imagePreviewCache = new Dictionary<string, Texture2D>();
    private Vector2 imagesScrollPosition;

    // Updated Spritesheet Animation Variables
    private string spritesheetModel = "hydra";
    private float spritesheetDuration = 3.0f;
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

    // Dropdown options for new features
    private readonly string[] imageTypeOptions = { "screenshot", "generic", "art", "asset", "sprite", "sprite-vfx", "sprite-tiling-horizontal", "sprite-tiling-vertical", "icon", "item-icon", "logo", "ui_asset", "portrait", "card-art", "splash", "fixed_background", "side_scrolling_background", "vertical_scrolling_background", "parallax_layer", "texture", "tile", "3d" };
    private readonly string[] artStyleOptions = { "Any style", "Cel-Shaded", "Inked Painterly", "Illustration", "Western Cartoon", "Anime/Manga", "Chibi", "8-Bit", "16-Bit", "32-Bit", "Hi-Bit", "Retro 2D", "Hand-Painted", "Digital Painting", "Comic Book", "Block Print", "Sketch", "Watercolor", "Stylized 3D", "Pixar Style", "Low Poly", "Photorealistic 3D", "Voxel Art", "Retro 3D", "Flat Design", "Minimalist", "Silhouette", "Noir", "Neon", "Glitch Art", "Claymation", "Paper Craft", "Textile" };
    private readonly string[] perspectiveOptions = { "Any perspective", "Side-Scroll", "Isometric", "High Angle", "Top-Down", "2.5D", "First-Person", "Third-Person", "Over-the-Shoulder", "Free Camera" };
    private readonly string[] aspectRatioOptions = { "default", "ar_1_1", "ar_4_3", "ar_16_9", "ar_19_9", "ar_3_4", "ar_9_16", "ar_9_19" };
    private readonly string[] aspectRatioLabels = { "Default", "1:1", "4:3", "16:9", "19:9", "3:4", "9:16", "9:19" };
    
    // Current sprite animation models; the API still accepts legacy ones (blitz, eagle, ...)
    // but they are scheduled for removal.
    private readonly string[] spritesheetModelOptions = { "hydra", "forge", "forge-pixel" };
    private readonly string[] spritesheetModelLabels = { "Hydra", "Forge", "Forge Pixel" };
    private readonly string[] spritesheetModelHints =
    {
        "Most capable all-around model, also generates a sound effect. 3 credits/s.",
        "Cost-effective for basic animations and simple sprites; may need a few tries. 1.5 credits/s.",
        "Best for low-res pixel art animations. 1.5 credits/s."
    };
    // Durations (seconds) each model accepts.
    private readonly Dictionary<string, float[]> spritesheetModelDurations = new Dictionary<string, float[]>
    {
        ["hydra"] = new[] { 3f, 3.5f, 4f, 4.5f, 5f },
        ["forge"] = new[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f },
        ["forge-pixel"] = new[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f }
    };
    private readonly string[] spritesheetImageTypeOptions = { "sprite", "sprite-vfx", "item-icon", "ui_asset", "logo", "sprite-tiling-horizontal", "sprite-tiling-vertical", "parallax_layer", "tile", "texture", "portrait", "card-art" };
    
    private readonly int[] textureSizeOptions = { 1024, 2048 };
    private readonly string[] textureTypeOptions = { "pbr", "simple", "none" };
    private readonly string[] model3DRigTypeOptions = { "general", "humanoid", "game", "humanoid_template", "humanoid_template_hands" };
    private readonly string[] model3DJointNamingOptions = { "mixamo", "smpl", "humanik", "unreal", "godot", "rigify", "vroid" };
    private readonly string[] model3DAnimateModeOptions = { "rot_trans", "rot_only" };
    private readonly string[] model3DAnimateModeLabels = { "Rotation + Translation", "Rotation Only (retargeting)" };
    
    private readonly string[] voiceTypeOptions = { "human", "non-human" };
    private readonly string[] voicePresetOptions = { "Serious woman", "Wise woman", "Calm woman", "Fast-paced woman", "Calm young girl", "Expressive teen girl", "Calm teen girl", "Sweet girl", "Patient man", "Determined man", "Young elegant man", "Teen boy", "Friendly man", "Deep voice man" };
    private readonly string[] emotionOptions = { "Default", "Happy", "Sad", "Angry", "Fearful", "Disgusted", "Surprised", "Neutral" };
    private readonly string[] languageOptions = { "auto", "English", "Afrikaans", "Arabic", "Bulgarian", "Catalan", "Chinese", "Chinese,Yue", "Croatian", "Czech", "Danish", "Filipino", "Finnish", "French", "German", "Greek", "Hebrew", "Hindi", "Hungarian", "Indonesian", "Italian", "Japanese", "Korean", "Malay", "Norwegian", "Nynorsk", "Persian", "Polish", "Portuguese", "Romanian", "Russian", "Slovak", "Slovenian", "Spanish", "Swedish", "Tamil", "Thai", "Turkish", "Ukrainian", "Vietnamese" };

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
                EditorUtility.DisplayDialog("Ludo AI Plugin", "API Key saved successfully!\n\nYou can now use the plugin to generate content.", "OK");
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
    // ======= SPRITE TAB UI ======
    // ============================

    private void DrawSpritesTab()
    {
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
        if (currentFrameSizeIndex < 0) currentFrameSizeIndex = Array.IndexOf(frameSizeOptions, 256);
        currentFrameSizeIndex = EditorGUILayout.Popup(currentFrameSizeIndex, frameSizeLabels);
        spritesheetFrameSize = frameSizeOptions[currentFrameSizeIndex];

        GUILayout.Space(5);

        // Margin Mode dropdown
        GUILayout.Label("Margin Mode:");
        int marginModeIndex = Array.IndexOf(marginModeOptions, spritesheetMarginMode);
        if (marginModeIndex < 0) marginModeIndex = 0; // Default to auto
        marginModeIndex = EditorGUILayout.Popup(marginModeIndex, marginModeLabels);
        spritesheetMarginMode = marginModeOptions[marginModeIndex];

        // Margin sliders (as a ratio of the sprite size) only in manual mode
        if (spritesheetMarginMode == "manual")
        {
            spritesheetMarginHorizontal = EditorGUILayout.Slider("Horizontal Margin", spritesheetMarginHorizontal, 0f, 1f);
            spritesheetMarginVertical = EditorGUILayout.Slider("Vertical Margin", spritesheetMarginVertical, 0f, 1f);
        }

        GUILayout.Space(5);

        // Model selection
        GUILayout.Label("Animation Model:");
        int modelIndex = Array.IndexOf(spritesheetModelOptions, spritesheetModel);
        if (modelIndex < 0) modelIndex = 0; // Default to hydra
        modelIndex = EditorGUILayout.Popup(modelIndex, spritesheetModelLabels);
        spritesheetModel = spritesheetModelOptions[modelIndex];
        EditorGUILayout.LabelField(spritesheetModelHints[modelIndex], EditorStyles.helpBox);

        GUILayout.Space(5);

        // Duration: each model offers its own set
        GUILayout.Label("Duration (seconds):");
        float[] durations = spritesheetModelDurations[spritesheetModel];
        int durationIndex = Array.IndexOf(durations, spritesheetDuration);
        if (durationIndex < 0) durationIndex = 0;
        durationIndex = EditorGUILayout.Popup(durationIndex, Array.ConvertAll(durations, d => d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s"));
        spritesheetDuration = durations[durationIndex];

        GUILayout.Space(5);

        // Image Type dropdown
        GUILayout.Label("Image Type:");
        int imageTypeIndex = Array.IndexOf(spritesheetImageTypeOptions, spritesheetImageType);
        if (imageTypeIndex < 0) imageTypeIndex = 0; // Default to sprite
        imageTypeIndex = EditorGUILayout.Popup(imageTypeIndex, spritesheetImageTypeOptions);
        spritesheetImageType = spritesheetImageTypeOptions[imageTypeIndex];

        GUILayout.Space(5);

        spritesheetGif = EditorGUILayout.Toggle("Also create a GIF", spritesheetGif);

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
            if (!string.IsNullOrEmpty(currentSpritesheet.GifB64) && GUILayout.Button("Save GIF Preview", GUILayout.Height(30)))
            {
                SaveGifToFile(currentSpritesheet);
            }
            if (!string.IsNullOrEmpty(currentSpritesheet.AudioUrl) && GUILayout.Button("Save Sound Effect", GUILayout.Height(30)))
            {
                SaveGeneratedAudioToFile(new GeneratedAudio { Url = currentSpritesheet.AudioUrl }, "SpriteSound");
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

        // Margins: per-axis values only in manual mode ("auto"/"none" reject a value)
        requestData["margin_ratio_mode"] = spritesheetMarginMode;
        if (spritesheetMarginMode == "manual")
        {
            requestData["margin_ratio_horizontal"] = spritesheetMarginHorizontal;
            requestData["margin_ratio_vertical"] = spritesheetMarginVertical;
        }

        if (spritesheetGif)
        {
            requestData["gif"] = true;
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
                    AudioUrl = animatedResponse.AudioUrl,
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
        if (!string.IsNullOrEmpty(imageArtStyle)&& imageArtStyle != "Any style")
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
            string extension;
            byte[] finalData = ConvertImageForUnity(www.downloadHandler.data, spritesheet.SpriteSheetB64, out extension);

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

        // GifB64 holds the gif_url returned by the API
        EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAndSaveFile(spritesheet.GifB64, "Save GIF Preview", $"Preview_{DateTime.Now:yyyyMMdd_HHmmss}", "gif", "GIF preview"));
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
            string extension;
            byte[] imageData = ConvertImageForUnity(www.downloadHandler.data, image.Url, out extension);
            string fileName = $"GeneratedImage_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            string savePath = EditorUtility.SaveFilePanel("Save Image", "Assets", fileName, extension);

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
        // The API returns MP3; keep the URL's extension so Unity imports it as what it is.
        string extension = ExtensionFromUrl(audio.Url, "mp3");
        yield return DownloadAndSaveFile(audio.Url, "Save Audio", $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmmss}", extension, "Audio");
    }

    // Downloads `url` and saves it unchanged wherever the user picks.
    private IEnumerator DownloadAndSaveFile(string url, string dialogTitle, string baseName, string extension, string what)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("x-ludo-tool", "unity");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to download {what}: {www.error}");
            EditorUtility.DisplayDialog("Download Error", $"Failed to download {what}: {www.error}", "OK");
            yield break;
        }

        try
        {
            string savePath = EditorUtility.SaveFilePanel(dialogTitle, "Assets", $"{baseName}.{extension}", extension);

            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllBytes(savePath, www.downloadHandler.data);

                if (savePath.StartsWith(Application.dataPath))
                {
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[LudoAIPlugin] {what} saved to: {savePath}");
                EditorUtility.DisplayDialog("Success", $"{what} saved successfully to:\n{savePath}", "OK");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LudoAIPlugin] Failed to save {what}: {e.Message}");
            EditorUtility.DisplayDialog("Save Error", $"Failed to save {what}: {e.Message}", "OK");
        }
    }

    // Generated images arrive as WebP, which Unity does not import; decode (unity.webp)
    // and re-encode as PNG. If that fails, keep the original bytes under their real
    // extension rather than mislabel them.
    private byte[] ConvertImageForUnity(byte[] imageData, string url, out string extension)
    {
        Texture2D texture = DecodeImageBytesToTexture(imageData);
        if (texture != null)
        {
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (png != null && png.Length > 0)
            {
                extension = "png";
                return png;
            }
        }

        extension = ExtensionFromUrl(url, "webp");
        Debug.LogWarning($"[LudoAIPlugin] Could not convert the image to PNG; saving it as .{extension}. Unity cannot import WebP; convert it before use.");
        return imageData;
    }

    private static string ExtensionFromUrl(string url, string fallback)
    {
        try
        {
            string ext = Path.GetExtension(new Uri(url).AbsolutePath).TrimStart('.').ToLowerInvariant();
            return string.IsNullOrEmpty(ext) ? fallback : ext;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    // ============================
    // === INITIAL PAYLOAD CLASSES =
    // ============================

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

        [JsonProperty("audio_url")]
        public string AudioUrl { get; set; }
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