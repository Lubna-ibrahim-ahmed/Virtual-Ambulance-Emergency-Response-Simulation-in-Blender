using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Additive on-screen prompt for the gas-leak scene. Shows "Press X to trigger the
/// gas leak" from scene start and hides itself the moment X is pressed. It only reads
/// input and toggles its own canvas — it does NOT touch ExplosionSequenceController or
/// any of the existing scene objects/logic. Reads X via the Input System (Keyboard.current),
/// matching ExplosionSequenceController so the prompt hides exactly when the explosion
/// is triggered (the running editor has the Input System handler active).
/// </summary>
public class GasLeakPrompt : MonoBehaviour
{
    [Tooltip("Object to hide once X is pressed. Defaults to this GameObject if left empty.")]
    public GameObject promptRoot;

    private bool _dismissed;

    void Update()
    {
        if (_dismissed) return;

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            _dismissed = true;
            GameObject target = promptRoot != null ? promptRoot : gameObject;
            target.SetActive(false);
        }
    }
}
