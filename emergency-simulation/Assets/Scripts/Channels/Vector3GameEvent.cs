using System;
using UnityEngine;

namespace EmergencySim
{
    /// <summary>
    /// Decoupled event channel carrying a world position. The scenario raises it; any
    /// number of listeners (e.g. the rescue sequence) subscribe — no direct references.
    /// This is the clean handoff surface: the teammate's rescue code just listens on the
    /// same asset, with zero edits to ScenarioDirector.
    /// </summary>
    [CreateAssetMenu(menuName = "EmergencySim/Vector3 Game Event", fileName = "Vector3GameEvent")]
    public class Vector3GameEvent : ScriptableObject
    {
        public event Action<Vector3> OnRaised;

        public void Raise(Vector3 value)
        {
            OnRaised?.Invoke(value);
        }
    }
}
