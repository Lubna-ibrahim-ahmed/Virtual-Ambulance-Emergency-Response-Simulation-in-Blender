using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Verification-only helper: simulates the rescue's keypress gates (E / C / F) via the Input
/// System, since the MCP harness can't send real keystrokes during play. Does not modify any
/// gameplay script.
/// </summary>
public static class RescueDebug
{
    static void Sim(Key k)
    {
        var kb = Keyboard.current;
        if (kb == null) kb = InputSystem.AddDevice<Keyboard>();
        if (kb == null) { Debug.LogError("[RescueDebug] No keyboard device"); return; }
        InputSystem.QueueStateEvent(kb, new KeyboardState(k));   // press (held; next key auto-releases it)
        Debug.Log($"[RescueDebug] Simulated key {k}");
    }

    [MenuItem("Tools/Rescue/DEBUG Press E")] static void E() => Sim(Key.E);
    [MenuItem("Tools/Rescue/DEBUG Press C")] static void C() => Sim(Key.C);
    [MenuItem("Tools/Rescue/DEBUG Press F")] static void F() => Sim(Key.F);
}
