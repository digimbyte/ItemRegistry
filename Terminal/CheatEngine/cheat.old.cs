using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Public, game-facing cheats API (always compiled).
/// In builds, Active=false and dictionaries are empty.
/// Use Cheats.Bool/Int/Float with sensible fallbacks in game code.
/// </summary>
public static class Cheats
{
    /// <summary>True when cheat toggle is ON (Editor only). Always false in builds.</summary>
    public static bool Active { get; internal set; } = false;

    /// <summary>Live values while active. Empty in builds.</summary>
    public static readonly Dictionary<string, bool>  Bools  = new();
    public static readonly Dictionary<string, int>   Ints   = new();
    public static readonly Dictionary<string, float> Floats = new();

    public static bool  Bool(string key, bool  fallback = false) => Bools.TryGetValue(key, out var v)  ? v : fallback;
    public static int   Int (string key, int   fallback = 0)     => Ints.TryGetValue(key,  out var v)  ? v : fallback;
    public static float Float(string key, float fallback = 0f)   => Floats.TryGetValue(key,out var v)  ? v : fallback;
}

#if UNITY_EDITOR
/// <summary>
/// Editor-only toggle controller (excluded from builds).
/// Press F12 to flip between Off (defaults) and On (modified).
/// Shows "Cheats Active" on screen while enabled.
/// </summary>
public class CheatToggleEditor : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.F12;
    public bool startActive = false;

    [Header("Overlay")]
    public bool showOverlay = true;
    [Range(0.5f, 3f)] public float overlayScale = 1.2f;
    [Tooltip("0..1 viewport anchor (x: left->right, y: top->bottom).")]
    public Vector2 overlayAnchor = new(0.5f, 0.05f);

    [System.Serializable]
    public struct BoolEntry  { public string key; public bool  offValue; public bool  onValue; }
    [System.Serializable]
    public struct IntEntry   { public string key; public int   offValue; public int   onValue; }
    [System.Serializable]
    public struct FloatEntry { public string key; public float offValue; public float onValue; }

    [Header("Values: Booleans")]
    public List<BoolEntry> bools = new();

    [Header("Values: Integers")]
    public List<IntEntry> ints = new();

    [Header("Values: Floats")]
    public List<FloatEntry> floats = new();

    void OnEnable()
    {
        // Ensure clean state when entering play mode.
        Cheats.Bools.Clear(); Cheats.Ints.Clear(); Cheats.Floats.Clear();
        Cheats.Active = startActive;
        ApplyAll(Cheats.Active);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Cheats.Active = !Cheats.Active;
            ApplyAll(Cheats.Active);
            Debug.Log($"[Cheats] {(Cheats.Active ? "ON" : "OFF")}");
        }
    }

    void ApplyAll(bool toOn)
    {
        // Populate dictionaries with either Off or On values.
        Cheats.Bools.Clear(); Cheats.Ints.Clear(); Cheats.Floats.Clear();

        foreach (var e in bools)
        {
            if (!string.IsNullOrWhiteSpace(e.key))
                Cheats.Bools[e.key] = toOn ? e.onValue : e.offValue;
        }

        foreach (var e in ints)
        {
            if (!string.IsNullOrWhiteSpace(e.key))
                Cheats.Ints[e.key] = toOn ? e.onValue : e.offValue;
        }

        foreach (var e in floats)
        {
            if (!string.IsNullOrWhiteSpace(e.key))
                Cheats.Floats[e.key] = toOn ? e.onValue : e.offValue;
        }
    }

    void OnGUI()
    {
        if (!showOverlay || !Cheats.Active) return;

        var prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * overlayScale);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter
        };

        var screen = new Vector2(Screen.width / overlayScale, Screen.height / overlayScale);
        var pos = new Vector2(overlayAnchor.x * screen.x, overlayAnchor.y * screen.y);
        var rect = new Rect(pos.x - 200, pos.y, 400, 40);

        // Drop shadow then text.
        var shadow = new GUIStyle(style); shadow.normal.textColor = new Color(0, 0, 0, 0.8f);
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), "Cheats Active", shadow);

        var fg = new GUIStyle(style); fg.normal.textColor = Color.white;
        GUI.Label(rect, "Cheats Active", fg);

        GUI.matrix = prev;
    }
}
#endif
