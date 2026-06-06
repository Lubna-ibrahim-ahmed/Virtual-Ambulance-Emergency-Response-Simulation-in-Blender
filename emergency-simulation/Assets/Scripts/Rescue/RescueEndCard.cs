using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace EmergencySim
{
    /// <summary>
    /// Additive end card. Listens on the RescueCompleted channel; ~3s after it fires it fades in a
    /// full-screen dark overlay with "Patient Evacuated — Scenario Complete" and, below it, a
    /// Return-to-Menu button — shown ONLY if the menu scene is in Build Settings, hidden otherwise.
    /// Fully decoupled: no references to the sequence, just the same RescueCompleted asset.
    /// </summary>
    public class RescueEndCard : MonoBehaviour
    {
        public Vector3GameEvent rescueCompletedChannel;
        public CanvasGroup group;            // the end-card canvas (faded in)
        public Button returnButton;
        public string menuScene = "MainMenu";
        public float delay = 3f;
        public float fadeDuration = 1.2f;

        private bool _shown;

        private void Awake()
        {
            if (group) { group.alpha = 0f; group.gameObject.SetActive(false); }
        }

        private void OnEnable()
        {
            if (rescueCompletedChannel != null) rescueCompletedChannel.OnRaised += OnCompleted;
        }

        private void OnDisable()
        {
            if (rescueCompletedChannel != null) rescueCompletedChannel.OnRaised -= OnCompleted;
        }

        private void OnCompleted(Vector3 _)
        {
            if (_shown) return;       // once-only
            _shown = true;
            StartCoroutine(ShowAfterDelay());
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(delay);

            // Button only if the menu scene is actually in Build Settings.
            bool hasMenu = !string.IsNullOrEmpty(menuScene) && Application.CanStreamedLevelBeLoaded(menuScene);
            if (returnButton)
            {
                returnButton.gameObject.SetActive(hasMenu);
                if (hasMenu)
                {
                    returnButton.onClick.RemoveAllListeners();
                    returnButton.onClick.AddListener(() => SceneManager.LoadScene(menuScene));
                    if (EventSystem.current == null)   // the button needs an EventSystem to be clickable
                        new GameObject("EventSystem", typeof(EventSystem),
                            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                }
            }

            if (group)
            {
                group.gameObject.SetActive(true);
                group.interactable = true;
                group.blocksRaycasts = true;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    group.alpha = Mathf.Clamp01(t / fadeDuration);
                    yield return null;
                }
                group.alpha = 1f;
            }
            Debug.Log("[Rescue] End card shown" + (hasMenu ? " (menu button)" : " (no MainMenu in Build Settings — button hidden)"));
        }
    }
}
