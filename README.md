# Ludo AI Plugin for Unity - Complete Guide

![Ludo AI Plugin](https://ludo.ai/logo.png)

> **AI-Powered Asset Generation for Unity**  
> Generate sprites, images, 3D models, and audio directly in your Unity Editor using Ludo AI's powerful API.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Sprites & Images](#sprites--images)
- [3D Models](#3d-models)
- [Audio Generation](#audio-generation)
- [Settings](#settings)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [API & MCP Integration](#api--mcp-integration)
- [FAQ](#faq)

---

## Overview

The **Ludo AI Plugin** is a Unity Editor extension that integrates AI-powered asset generation directly into your workflow. Create game-ready sprites, images, 3D models, sound effects, music, and voice lines without leaving Unity.

### Key Benefits

- **Fast Prototyping**: Generate placeholder assets in seconds
- **No External Tools**: Everything works directly in Unity Editor
- **AI-Powered**: Describe what you want in plain text
- **Multiple Asset Types**: Sprites, images, 3D models, audio
- **Batch Generation**: Create multiple variations at once
- **Direct Import**: Assets save directly to your project

### Compatibility

- **Unity Version**: Unity 2019.4 or later (WebP support requires 2021.2+)
- **Platform**: Unity Editor only (Windows, macOS, Linux)
- **Dependencies**: Included (Editor Coroutines, Newtonsoft.Json, WebP support)

---

## Features

### Sprite & Image Generation
- Generate sprites from text descriptions
- Create game screenshots, icons, UI assets, textures
- 20+ art styles (Pixel Art, Low Poly, Cartoonish, etc.)
- 9 perspective options (Top-Down, Isometric, Side-Scroll, etc.)
- Multiple aspect ratios (1:1, 16:9, 9:16, etc.)
- Batch generation (1-8 images at once)

### Sprite Animation
- Convert static sprites to animated spritesheets
- Control frame count (4-64 frames)
- Adjustable frame sizes (64x64 to Max)
- Export as spritesheet, GIF, or video
- Pixel art filter support

### 3D Model Generation
- Convert 2D images to 3D models
- Adjustable face count (1,000-100,000)
- Multiple texture sizes (1024x1024 to 4096x4096)
- PBR or simple textures
- Export as GLB format

### Audio Generation
- **Sound Effects**: 0-10 second effects
- **Music**: Background music with optional lyrics
- **Voice**: AI-generated voices (human/non-human)
- **Speech (Clone)**: Clone voices from samples
- **Speech (Preset)**: 14 preset voices, 8 emotions, 40+ languages

---

## Installation

### Step 1: Install the Plugin

1. Download the plugin package or clone the repository
2. Copy the `LudoAIPlugin` folder to your Unity project's `Assets/` directory
3. Unity will automatically import the plugin and its dependencies

### Step 2: Get Your API Key

1. Visit [https://ludo.ai](https://ludo.ai)
2. Sign up or log in to your account
3. Navigate to **Settings** → **API Keys**
4. Generate a new API key or copy your existing one

### Step 3: Configure the Plugin

1. In Unity, go to **Menu Bar** → **Ludo AI** → **Ludo AI Plugin**
2. Click the **Settings** tab
3. Paste your API key in the text field
4. Click **Save API Key**
5. You should see "✓ API Key is set"

### Step 4: Verify Setup

1. Go back to the **Sprites & Images** tab
2. Try generating a simple sprite (e.g., "red apple")
3. If successful, you're ready to go!

---

## Quick Start

### Opening the Plugin

**Menu Bar** → **Ludo AI** → **Ludo AI Plugin**

The plugin window has four main tabs:
- **3D Models**: Generate 3D models from images
- **Sprites & Images**: Generate and animate sprites
- **Audio**: Create sound effects, music, and voice
- **Settings**: Manage your API key

### Your First Asset

Let's generate a simple sprite:

1. Open the plugin window
2. Go to **Sprites & Images** tab
3. In the prompt field, enter: `"pixel art treasure chest"`
4. Select **Art Style**: "Pixel Art (16-Bit)"
5. Select **Perspective**: "Top-Down"
6. Set **Quality**: High
7. Click **Generate Sprite**
8. Wait for generation (10-60 seconds)
9. Preview the sprite
10. Click **Save Sprite** to save to `Assets/Sprites/`

---

## Sprites & Images

### Generate Sprite

Perfect for creating game sprites with specific art styles.

**Workflow:**

1. **Enter Description**: Describe your sprite clearly
   - Good: "pixel art sword with blue blade"
   - Bad: "weapon"

2. **Select Art Style**: Choose from dropdown or enter custom
   - Pixel Art (8-Bit, 16-Bit)
   - Low Poly
   - Cartoonish
   - Flat Design
   - And more...

3. **Select Perspective**: Match your game's view
   - Top-Down
   - Isometric
   - Side-Scroll
   - 2.5D

4. **Set Quality**: 
   - **Low**: Faster generation, lower quality
   - **Medium**: Balanced
   - **High**: Best quality, slower

5. **Prompt Augmentation**: Enable to let AI enhance your description

6. **Generate**: Click the button and wait

7. **Save**: Click "Save Sprite" to save to `Assets/Sprites/`

### Generate Images

For screenshots, icons, UI assets, textures, backgrounds, and more.

**Additional Options:**

- **Image Type**: screenshot, icon, art, sprite, ui_asset, texture, etc.
- **Genre**: Hypercasual, Casual, Action, Puzzle, etc. (23 options)
- **Platform**: Mobile, Desktop, Web
- **Aspect Ratio**: Default, 1:1, 16:9, 9:16, etc.
- **Number of Images**: Generate 1-8 images at once

**Use Cases:**
- Game screenshots for marketing
- App icons
- UI elements
- Background art
- Texture generation

### Animate Sprite to Spritesheet

Turn static sprites into animated spritesheets.

**Workflow:**

1. **Select Image**: Choose from previously generated images or enter URL

2. **Motion Description**: Describe the animation
   - Example: "walking animation cycle"
   - Example: "idle breathing animation"

3. **Configure Frames**:
   - **Frame Count**: 4, 9, 16, 25, 36, 49, or 64 frames
   - **Frame Size**: 64x64, 128x128, 256x256, or Max
   - **Loop**: Enable for seamless looping animations

4. **Crop & Margins**:
   - **Crop to Content**: Remove empty space
   - **Margin Mode**: Auto, Manual, or None

5. **Pixel Art Filter**: none, small, medium, large (for pixel art sprites)

6. **Animation Model**:
   - **Standard**: Reliable, tested model
   - **New**: Experimental, different results

7. **Duration**: Adjust animation length (varies by model)

8. **Generate**: Click "Animate Sprite"

9. **Preview**: View spritesheet, GIF, or video preview

10. **Save**: Save spritesheet or GIF to `Assets/Spritesheets/`

**Using Spritesheets in Unity:**

After saving:
1. Select the spritesheet in Project window
2. In Inspector, change **Texture Type** to "Sprite (2D and UI)"
3. Set **Sprite Mode** to "Multiple"
4. Click **Sprite Editor** and use **Slice** → **Grid By Cell Count**
5. Apply changes and use in Animator

---

## 3D Models

Convert 2D concept art or sprites into 3D models.

### Workflow

1. **Provide Image**: 
   - Enter image URL, or
   - Click "Select from Project" to use existing image
   - Or paste base64 encoded image

2. **Target Face Count**: 
   - **1,000-10,000**: Low poly, mobile-friendly
   - **10,000-50,000**: Medium detail
   - **50,000-100,000**: High detail

3. **Texture Size**:
   - **1024x1024**: Low resolution, smaller file
   - **2048x2048**: Medium resolution (recommended)
   - **4096x4096**: High resolution, larger file

4. **Texture Type**:
   - **PBR**: Physically Based Rendering (albedo, normal, roughness, metallic)
   - **Simple**: Basic color texture
   - **None**: No texture

5. **High Detail Shape**: Enable for more geometric detail (slower)

6. **Generate**: Click "Create 3D Model"

7. **Preview**: View snapshot renders from different angles

8. **Download**: Click "Download 3D Model" 
   - Saves to `Assets/3DModels/` as GLB format

**Importing GLB Models:**

Unity supports GLB import natively:
1. Model appears in Project window after download
2. Drag into Scene or Hierarchy
3. Configure materials in Inspector if needed
4. PBR textures work with Standard shader or URP/HDRP

---

## Audio Generation

Generate AI-powered audio assets for your game.

### Sound Effects

**Perfect for**: Button clicks, footsteps, explosions, pickups, etc.

**Workflow:**

1. Enter description (e.g., "sword swing whoosh", "coin pickup sound")
2. Set duration:
   - **0 seconds**: Auto-duration (recommended)
   - **1-10 seconds**: Specific length
3. Enable **Prompt Augmentation** for enhanced descriptions
4. Click **Generate Sound Effect**
5. Preview the audio
6. Click **Save** to save to `Assets/Audio/SoundEffects/`

**Tips:**
- Be specific: "metallic sword slash" vs "sword sound"
- Include material: "wooden door creak"
- Describe intensity: "soft footstep", "loud explosion"

### Music

**Perfect for**: Background music, main themes, menu music

**Workflow:**

1. Enter music description (e.g., "upbeat electronic game music", "fantasy adventure theme")
2. Optionally add lyrics
3. Enable **Prompt Augmentation** for better results
4. Click **Generate Music**
5. Preview and save to `Assets/Audio/Music/`

**Tips:**
- Specify genre: "8-bit chiptune", "orchestral epic"
- Include mood: "relaxing", "intense", "mysterious"
- Mention instruments: "piano and strings"

### Voice

**Perfect for**: Character voices, narration

**Workflow:**

1. Enter voice description (e.g., "deep male warrior voice", "cheerful female child")
2. Enter text to speak (max 200 characters)
3. Select voice type: **Human** or **Non-human**
4. Enable **Prompt Augmentation**
5. Generate and save to `Assets/Audio/Voice/`

### Speech (Clone)

Clone a specific voice from a sample.

**Workflow:**

1. Enter text to speak (max 1000 characters)
2. Provide voice sample:
   - Enter URL to audio file, or
   - Upload/encode base64 audio
3. Click **Generate Speech**
4. Save to `Assets/Audio/Speech/`

**Requirements:**
- Voice sample should be clear
- 5-10 seconds of speech recommended
- Minimal background noise

### Speech (Preset)

Use pre-configured voices with emotion control.

**Workflow:**

1. Enter text to speak (max 1000 characters)
2. Select **Voice Preset** (14 options):
   - Serious woman, Calm woman, Fast-paced woman
   - Patient man, Determined man, Deep voice man
   - Teen boy, Calm teen girl, Sweet girl
   - And more...
3. Select **Emotion** (8 options):
   - Default, Happy, Sad, Angry, Fearful, Disgusted, Surprised, Neutral
4. Select **Language** (40+ languages, or "auto" for detection)
5. Generate and save to `Assets/Audio/SpeechPreset/`

**When to Use:**
- **Preset**: Consistent voice across multiple lines, quick setup
- **Clone**: Specific voice match, custom voice actor

---

## Settings

### Managing API Keys

**Save API Key:**
1. Open Settings tab
2. Paste your API key
3. Click "Save API Key"

**Verification:**
- Green checkmark: "✓ API Key is set"
- Red X: "✗ API Key is NOT set"
- Shows key length for verification

**Clear API Key:**
- Click "Clear API Key" to remove stored key
- Useful when switching accounts

**Storage:**
- API keys are stored in Unity EditorPrefs
- Project-specific (each Unity project needs its own key setup)
- Never committed to version control
- Persists between Unity sessions

**Security:**
- Never share your API key
- Don't commit API keys to repositories
- Regenerate if compromised

---

## Best Practices

### Writing Effective Prompts

**For Sprites & Images:**

✅ **Good Prompts:**
- "pixel art health potion, red liquid, cork top, side view"
- "low poly tree, autumn colors, isometric view"
- "cartoonish treasure chest, open lid, gold coins inside"

❌ **Bad Prompts:**
- "potion" (too vague)
- "make me a sprite" (no description)
- "asdfghjkl" (nonsense)

**Tips:**
- Be specific about style, colors, and details
- Include perspective if important
- Mention materials and textures
- Use descriptive adjectives

**For Audio:**

✅ **Good Prompts:**
- "short metallic sword slash with whoosh sound"
- "soft footsteps on wooden floor"
- "bright cheerful 8-bit victory jingle"

❌ **Bad Prompts:**
- "sound" (too vague)
- "music" (no style or mood)

**Tips:**
- Describe the action or event
- Include material sounds
- Mention intensity (loud, soft, subtle)
- Specify duration expectations

### Asset Organization

**Recommended Folder Structure:**

```
Assets/
├── Sprites/
│   ├── Characters/
│   ├── Items/
│   ├── UI/
│   └── Environment/
├── Spritesheets/
│   └── Animations/
├── Images/
│   ├── Icons/
│   ├── Screenshots/
│   └── Backgrounds/
├── 3DModels/
│   ├── Characters/
│   ├── Props/
│   └── Environment/
└── Audio/
    ├── SoundEffects/
    ├── Music/
    ├── Voice/
    └── Speech/
```

**Naming Conventions:**
- Use descriptive names: `Sprite_HealthPotion_Red.png`
- Include date if versioning: `Sprite_Character_20260209.png`
- Avoid spaces: Use underscores or PascalCase
- Be consistent across your project

### Performance Tips

**Texture Sizes:**
- Mobile games: Use 1024x1024 or 2048x2048
- Desktop games: 2048x2048 or 4096x4096
- Consider texture compression in Unity

**3D Model Face Counts:**
- Mobile: 1,000-10,000 faces
- Desktop: 10,000-50,000 faces
- Only use 50,000+ for hero assets or close-ups

**Batch Generation:**
- Generate multiple image variations at once (1-8 images)
- Compare results and pick the best
- Saves time over individual generations

**API Considerations:**
- Generation timeout: 10 minutes max
- Preview assets before saving to avoid clutter
- Delete unwanted assets to keep project clean

### Workflow Optimization

**Rapid Prototyping:**
1. Generate low-quality assets first for speed
2. Test gameplay with placeholder assets
3. Regenerate high-quality versions later
4. Iterate based on what works

**Iterative Approach:**
1. Start with broad prompts
2. Review results
3. Refine prompts with more specific details
4. Generate variations
5. Pick the best fit

**Preview Before Saving:**
- Always preview generated assets
- Check quality and suitability
- Only save assets you'll actually use
- Reduces project bloat

---

## Troubleshooting

### API Connection Issues

#### 403 Forbidden Error

**Cause:** Invalid, expired, or missing API key

**Solutions:**
1. Verify API key is set in Settings tab
2. Check for typos or extra spaces
3. Regenerate API key from Ludo AI dashboard
4. Ensure key hasn't expired
5. Confirm account has necessary permissions

#### Network Connectivity

**Symptoms:** Timeout errors, "Unable to connect"

**Solutions:**
1. Check internet connection
2. Verify `https://api.ludo.ai` is accessible
3. Check firewall settings
4. Disable VPN temporarily
5. Check proxy settings

### Generation Failures

#### Timeout Issues

**Problem:** Generation takes longer than 10 minutes

**Solutions:**
- Reduce complexity (lower face count, smaller texture size)
- Try again during off-peak hours
- Simplify prompt
- Check Ludo AI status page

#### Invalid Parameters

**Problem:** Error about invalid parameters

**Solutions:**
- Verify all required fields are filled
- Check character limits (speech: 1000 chars, voice: 200 chars)
- Ensure values are within valid ranges
- Use supported formats (GLB for models, etc.)

### Asset Import Issues

#### WebP Format Not Supported

**Problem:** Unity 2021.1 or earlier doesn't support WebP

**Solutions:**
- Update to Unity 2021.2+ (recommended)
- Plugin includes WebP decoder fallback
- Save as PNG instead

#### GLB Import Problems

**Problem:** 3D model doesn't import correctly

**Solutions:**
1. Ensure UnityGLTF package is installed (usually automatic)
2. Check console for specific errors
3. Try reimporting: Right-click → Reimport
4. Verify GLB file isn't corrupted (check file size)

#### Audio Format Issues

**Problem:** Audio file won't import

**Solutions:**
- Check supported formats (WAV, MP3, OGG)
- Verify file isn't corrupted
- Check file size isn't too large
- Reimport asset

### UI/Interface Problems

#### Plugin Window Won't Open

**Solutions:**
1. Check console for errors
2. Reimport plugin: Right-click folder → Reimport
3. Verify dependencies are installed
4. Restart Unity Editor

#### Missing Dropdowns or Options

**Problem:** Art styles or other dropdowns are empty

**Solutions:**
1. Ensure API key is set
2. Plugin fetches options from API on startup
3. Check console for API errors
4. Restart plugin window

#### Refresh Payload Failures

**Problem:** "Cannot GET /api/refresh" error

**Solutions:**
- This is normal if endpoint changed
- Plugin includes fallback default values
- You can still use all features
- Manually type custom values if needed

### Console Errors

**Finding Error Details:**

All plugin operations log to Unity Console:
1. Open **Window** → **General** → **Console**
2. Look for messages starting with `[LudoAIPlugin]`
3. Errors show HTTP status codes and details

**Common Error Codes:**
- **403**: Invalid API key → Check Settings
- **404**: Endpoint not found → Update plugin
- **429**: Rate limited → Wait and retry
- **500**: Server error → Try again later

---

## API & MCP Integration

The Ludo AI Plugin uses the Ludo AI REST API under the hood to communicate with AI services. If you're interested in building custom integrations, automating workflows, or using MCP (Model Context Protocol) with AI assistants like Claude or Cursor, visit the official API documentation.

### Official Documentation

**[Ludo AI API & MCP Integration Documentation](https://ludo.ai/api-mcp-integration)**

The official documentation includes:

- **REST API Reference**: Complete endpoint documentation with request/response examples
- **MCP Integration**: Connect Ludo AI to Claude, Cursor, and other AI assistants
- **Authentication**: API key setup and usage
- **Credit System**: Understand credit costs for different operations
- **Rate Limits**: API quotas and timeout information
- **Code Examples**: Sample implementations and integrations

### Quick Links

- **Base URL**: `https://api.ludo.ai/api/`
- **MCP Server**: `https://mcp.ludo.ai/mcp`
- **Authentication**: `Authentication: ApiKey YOUR_API_KEY`

### Available Features via API

The API provides programmatic access to all plugin features:

- **Images & Animation**: Sprites, icons, UI assets, textures, spritesheet animations
- **Video & 3D**: Motion videos, 2D-to-3D model conversion
- **Audio**: Sound effects, music, voice generation, speech cloning

### Integration Use Cases

**Custom Pipelines**: Automate asset generation in your build pipeline

**Batch Processing**: Generate hundreds of assets via scripts

**AI Assistant Integration**: Use natural language to generate assets in Claude or Cursor

**Game Engine Integration**: Build custom plugins for other engines (Godot, Unreal, etc.)

**Web Applications**: Create web-based asset generation tools

For complete technical details, endpoint specifications, and integration guides, visit:

**[https://ludo.ai/api-mcp-integration](https://ludo.ai/api-mcp-integration)**

---

## FAQ

### General Questions

**Q: How much does the Ludo AI API cost?**  
A: Check the [Ludo AI pricing page](https://ludo.ai/pricing) for current pricing. API usage is typically credit-based.

**Q: What Unity versions are supported?**  
A: Unity 2019.4 or later. WebP image support requires Unity 2021.2+. The plugin uses EditorWindow features available in all modern Unity versions.

**Q: Can I use the plugin offline?**  
A: No, the plugin requires internet connection to communicate with Ludo AI's API servers.

**Q: Is my API key secure?**  
A: The API key is stored locally in Unity EditorPrefs and never transmitted except in API requests. Never share your key or commit it to public repositories.

### Feature Questions

**Q: What's the difference between sprite and image generation?**  
A: Sprite generation is optimized for game sprites with specific art styles and perspectives. Image generation is broader and includes screenshots, icons, UI assets, textures, and more with additional options like genre and platform.

**Q: How long does generation take?**  
A: Typically 10-60 seconds for sprites/images, 1-3 minutes for 3D models, and 10-30 seconds for audio. Complex requests may take longer (up to 10 minutes max).

**Q: Can I generate multiple assets at once?**  
A: Yes! Image generation supports batch generation (1-8 images). Other asset types generate one at a time.

**Q: What file formats are supported?**  
A: 
- Images: PNG, WebP, JPEG
- 3D Models: GLB
- Audio: WAV, MP3, OGG (varies by endpoint)
- Animations: Spritesheet (PNG), GIF, MP4

### Quality & Results

**Q: How do I get better quality results?**  
A:
1. Write specific, detailed prompts
2. Use "High" quality setting for sprites
3. Enable prompt augmentation
4. Try multiple generations and pick the best
5. Provide reference images for 3D model generation

**Q: Why are my results not what I expected?**  
A: AI generation can vary. Tips:
- Make prompts more specific
- Try different art styles or perspectives
- Generate multiple variations (batch generation)
- Iterate: generate → review → refine prompt → regenerate

**Q: Can I edit generated assets?**  
A: Yes! Generated assets are standard image/model/audio files. Edit them with any appropriate tool (Photoshop, Blender, Audacity, etc.) or Unity's built-in tools.

### Technical Questions

**Q: Why do I get WebP format errors?**  
A: Unity 2021.1 and earlier don't natively support WebP. The plugin includes a fallback decoder, but updating to Unity 2021.2+ is recommended for best compatibility.

**Q: How do I update the plugin?**  
A: 
1. Backup your project
2. Delete the old `Assets/LudoAIPlugin` folder
3. Import the new plugin version
4. Reconfigure API key in Settings

**Q: Can I use this in builds?**  
A: No, this is an Editor-only plugin for generating assets during development. Generated assets are regular Unity assets that work in builds.

**Q: Where are my assets stored?**  
A:
- Sprites: `Assets/Sprites/`
- Spritesheets: `Assets/Spritesheets/`
- Images: `Assets/Images/`
- 3D Models: `Assets/3DModels/`
- Audio: `Assets/Audio/{SoundEffects|Music|Voice|Speech}/`

**Q: Does this work with Unity packages/UPM?**  
A: The plugin is currently distributed as an asset folder. Dependencies (Editor Coroutines, Newtonsoft.Json) are included.

---

## Advanced Topics

### Prompt Augmentation

**What is it?**  
Prompt augmentation lets the AI enhance your description with additional relevant details.

**When to use:**
- ✅ Simple prompts: "sword" → "medieval steel sword with ornate hilt"
- ✅ When exploring: Let AI surprise you with enhancements
- ❌ Precise requirements: When you need exact specifications
- ❌ Specific references: When recreating a particular style

**Toggle it:** Available in most generation interfaces

### WebP Format

**What is WebP?**  
WebP is a modern image format with better compression than PNG/JPEG.

**Support:**
- Unity 2021.2+: Native support
- Unity 2021.1 and earlier: Plugin uses fallback decoder

**Benefits:**
- Smaller file sizes
- Faster downloads
- Good quality

**Compatibility:**
The plugin automatically handles WebP decoding with fallbacks.

### Custom Asset Post-Processing

**After generation, you can:**

1. **Batch Process**: Use Unity Editor scripts to process multiple generated assets
2. **Auto-Import**: Hook into `AssetPostprocessor` to automatically configure imported assets
3. **Texture Settings**: Automatically set compression, mipmaps, etc.
4. **Material Creation**: Auto-create materials for 3D models

**Example Hook:**

```csharp
using UnityEditor;
using UnityEngine;

public class LudoAssetPostprocessor : AssetPostprocessor
{
    void OnPostprocessTexture(Texture2D texture)
    {
        // Auto-configure sprites from Ludo AI
        if (assetPath.Contains("Sprites/"))
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
        }
    }
}
```

### Asset Database Integration

All generated assets use Unity's `AssetDatabase` for proper import:

```csharp
// Assets are imported with
AssetDatabase.ImportAsset(path);
AssetDatabase.Refresh();
```

This ensures:
- Proper metadata generation
- Version control integration
- Inspector availability
- Scene usage readiness

---

## Resources & Support

### Official Links

- **Ludo AI Website**: [https://ludo.ai](https://ludo.ai)
- **API & MCP Documentation**: [https://ludo.ai/api-mcp-integration](https://ludo.ai/api-mcp-integration)
- **Pricing**: [https://ludo.ai/pricing](https://ludo.ai/pricing)

### Getting Help

**Plugin Issues:**
1. Check Unity Console for detailed error messages
2. Review this guide's [Troubleshooting](#troubleshooting) section
3. Verify API key is valid and set correctly
4. Try regenerating your API key

**API/Account Issues:**
- Contact Ludo AI support through their website
- Check Ludo AI dashboard for account status
- Review API usage and limits

**Feature Requests:**
- Submit feedback through Ludo AI's official channels
- Check if updates are available

### Community

- Share your generated assets (following Ludo AI's terms)
- Join game development communities
- Provide feedback to improve the plugin

---

## Quick Reference

### Keyboard Shortcuts

- Open Plugin: `Menu Bar → Ludo AI → Ludo AI Plugin`

### File Locations

| Asset Type | Default Location |
|------------|------------------|
| Sprites | `Assets/Sprites/` |
| Spritesheets | `Assets/Spritesheets/` |
| Images | `Assets/Images/` |
| 3D Models | `Assets/3DModels/` |
| Sound Effects | `Assets/Audio/SoundEffects/` |
| Music | `Assets/Audio/Music/` |
| Voice | `Assets/Audio/Voice/` |
| Speech | `Assets/Audio/Speech/` |

### Asset Limits

| Feature | Limit |
|---------|-------|
| Voice Text | 200 characters |
| Speech Text | 1,000 characters |
| Sound Effect Duration | 0-10 seconds |
| Batch Image Generation | 1-8 images |
| 3D Model Face Count | 1,000-100,000 |
| API Timeout | 10 minutes |

### Quality Settings

| Setting | Use Case |
|---------|----------|
| Low Quality | Fast prototyping, placeholders |
| Medium Quality | Balanced development |
| High Quality | Final assets, production |

---

## Appendix: Complete Option Lists

### Art Styles (20 Options)

1. Any style
2. Cartoonish
3. Pixel Art (16-Bit)
4. Low Poly
5. Stylized 3D
6. Flat Design
7. Illustration
8. Cel-Shaded
9. Retro 2D
10. Voxel Art
11. Minimalist
12. Hand-Painted
13. Anime/Manga
14. Vector Art
15. Chibi
16. Retro 3D
17. Comic Book
18. Silhouette
19. Pixel Art (8-Bit)
20. Photorealistic 3D

### Perspectives (9 Options)

1. Any perspective
2. First-Person
3. Third-Person
4. Over-the-Shoulder
5. Top-Down
6. Isometric
7. Side-Scroll
8. Free Camera
9. 2.5D

### Genres (23 Options)

Hypercasual, Casual, Core, Action, Adventure, Arcade, Board, Card, Casino, Education, Family, Fighting, Games for Kids, Music, Puzzle, Racing, Role Playing, Shooter, Simulation, Sports, Strategy, Trivia, Word

### Voice Presets (14 Options)

1. Serious woman
2. Wise woman
3. Calm woman
4. Fast-paced woman
5. Calm young girl
6. Expressive teen girl
7. Calm teen girl
8. Sweet girl
9. Patient man
10. Determined man
11. Young elegant man
12. Teen boy
13. Friendly man
14. Deep voice man

### Emotions (8 Options)

Default, Happy, Sad, Angry, Fearful, Disgusted, Surprised, Neutral

### Languages (40+ Supported)

auto, English, Afrikaans, Arabic, Bulgarian, Catalan, Chinese, Chinese (Yue), Croatian, Czech, Danish, Filipino, Finnish, French, German, Greek, Hebrew, Hindi, Hungarian, Indonesian, Italian, Japanese, Korean, Malay, Norwegian, Nynorsk, Persian, Polish, Portuguese, Romanian, Russian, Slovak, Slovenian, Spanish, Swedish, Tamil, Thai, Turkish, Ukrainian, Vietnamese

### Aspect Ratios (8 Options)

| Label | Value |
|-------|-------|
| Default | default |
| 1:1 | ar_1_1 |
| 4:3 | ar_4_3 |
| 16:9 | ar_16_9 |
| 19:9 | ar_19_9 |
| 3:4 | ar_3_4 |
| 9:16 | ar_9_16 |
| 9:19 | ar_9_19 |

---

## Version Information

**Plugin Version**: 1.0  
**Last Updated**: February 2026  
**Unity Compatibility**: 2019.4+  
**API Version**: Ludo AI API v1

---

## License & Legal

**Generated Assets**: Review Ludo AI's terms of service for usage rights

**Disclaimer**: This plugin connects to Ludo AI's external API service. Functionality depends on API availability and your Ludo AI account status.


---

**Made with ❤️ for Unity Developers**

*Generate amazing game assets with AI - right inside Unity!*

---

For more information, visit [Ludo AI](https://ludo.ai)
