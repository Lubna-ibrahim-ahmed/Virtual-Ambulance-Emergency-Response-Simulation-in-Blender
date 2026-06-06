using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace EmergencySim
{
    /// <summary>
    /// Reusable additive intro card. On scene load it shows a semi-transparent dark strip with the
    /// scenario title (and, where applicable, the trigger hint beneath it), holds for ~holdDuration
    /// seconds OR until the scene's trigger key is pressed (whichever comes first), then fades out.
    /// Per-scene text/key are set on the prefab instance via the title/hint/dismissKey fields.
    ///
    /// It touches NO scenario logic — it only sets its own labels, reads the dismiss key, and fades
    /// its own CanvasGroup. Styled to match RescueEndCard (dark overlay + alpha fade).
    /// </summary>
    public class IntroCard : MonoBehaviour
    {
        [Header("Per-scene content")]
        [TextArea] public string title = "Scenario";
        [Tooltip("Trigger hint shown beneath the title. Leave empty for scenes with no key trigger.")]
        [TextArea] public string hint = "";

        [Header("Refs")]
        public CanvasGroup group;
        public Text titleText;
        public Text hintText;

        [Header("Timing")]
        public float holdDuration = 4f;
        public float fadeDuration = 0.6f;

        [Header("Early dismiss")]
        [Tooltip("Optional key that dismisses the card early (the scene's trigger key). None = timer only.")]
        public Key dismissKey = Key.None;

        private void Awake()
        {
            if (titleText) titleText.text = title;
            if (hintText)
            {
                hintText.text = hint;
                hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            }
            if (group) group.alpha = 1f;
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            float t = 0f;
            while (t < holdDuration)
            {
                if (dismissKey != Key.None && Keyboard.current != null &&
                    Keyboard.current[dismissKey].wasPressedThisFrame)
                    break;
                t += Time.deltaTime;
                yield return null;
            }

            float a0 = group ? group.alpha : 1f;
            float f = 0f;
            while (f < fadeDuration)
            {
                f += Time.deltaTime;
                if (group) group.alpha = Mathf.Lerp(a0, 0f, f / fadeDuration);
                yield return null;
            }
            if (group) group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
