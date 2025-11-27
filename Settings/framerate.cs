using UnityEngine;

namespace RegionViewer
{
    /// <summary>
    /// Enforces a global 100 FPS cap with VSync disabled by default.
    /// VSync should be opt-in; set QualitySettings.vSyncCount elsewhere if desired.
    /// Also displays FPS in the corner.
    /// </summary>
    public static class FpsLimiter
    {
        private const int TargetFps = 100;

        // Apply in runtime builds and when entering play mode
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0; // VSync OFF by default (opt-in)
            Application.targetFrameRate = TargetFps;
            
            // Create FPS display GameObject
            var fpsDisplay = new GameObject("FPS Display");
            fpsDisplay.AddComponent<FpsDisplay>();
            Object.DontDestroyOnLoad(fpsDisplay);
        }

#if UNITY_EDITOR
        // Also apply in the Editor when domain reloads, so the Game view is capped too
        [UnityEditor.InitializeOnLoadMethod]
        private static void ApplyInEditor()
        {
            QualitySettings.vSyncCount = 0; // VSync OFF by default (opt-in)
            Application.targetFrameRate = TargetFps;
        }
#endif
    }

    /// <summary>
    /// Simple FPS counter that displays in the top-right corner
    /// </summary>
    public class FpsDisplay : MonoBehaviour
    {
        private float deltaTime = 0.0f;
        private GUIStyle style;
        private Rect rect;
        
        void Start()
        {
            // Set up GUI style and position
            style = new GUIStyle();
            style.alignment = TextAnchor.UpperRight;
            style.fontSize = 18;
            style.normal.textColor = Color.white;
            
            // Position in top-right corner with some padding
            rect = new Rect(Screen.width - 100, 10, 90, 30);
        }
        
        void Update()
        {
            // Calculate FPS using smoothed delta time
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            
            // Update rect position in case screen size changes
            rect.x = Screen.width - 100;
        }
        
        void OnGUI()
        {
            float msec = deltaTime * 1000.0f;
            float fps = 1.0f / deltaTime;
            string text = string.Format("{0:0.} FPS\n{1:0.0} ms", fps, msec);
            
            // Draw shadow for better readability
            var shadowRect = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            var shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;
            GUI.Label(shadowRect, text, shadowStyle);
            
            // Draw main text
            GUI.Label(rect, text, style);
        }
    }
}