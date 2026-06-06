using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencySim
{
    /// <summary>
    /// Scenario-selection menu. Each button calls one of these methods; the scenario scenes are
    /// loaded by name (they must be in Build Settings). Quit exits the build (and stops play in
    /// the editor).
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public void LoadScenario1() => SceneManager.LoadScene("Scenario1");
        public void LoadScenario2() => SceneManager.LoadScene("Scenario2");
        // (LoadScenario3 removed — the Stretcher Rescue button was dropped from the menu.
        //  The Scenario3 scene itself stays in the project and Build Settings.)
        public void LoadScenario4() => SceneManager.LoadScene("Scenario4");

        public void QuitApp()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
