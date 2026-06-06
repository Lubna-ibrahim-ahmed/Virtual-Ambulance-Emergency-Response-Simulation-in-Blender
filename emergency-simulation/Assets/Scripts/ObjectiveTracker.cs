using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencySim
{
    /// <summary>
    /// Reusable "Save the Patient" objective tracker. A small top-left panel whose checklist ticks
    /// off live as the rescue progresses. It reads the SHARED rescue state with ZERO changes to
    /// scenario logic: it finds the RescueSequenceController, subscribes to the RescueRequested /
    /// RescueCompleted channels, and polls CurrentBeat each frame. Works in every scenario that
    /// contains the rescue prefab. Forward-only (a checked item stays checked) with a brief
    /// highlight pop on completion. Styled to match the intro/end cards.
    ///
    /// Item order (index): 0 Emergency reported, 1 Ambulance dispatched (E), 2 Ambulance on scene,
    /// 3 CPR performed (C), 4 Patient secured (F/lift), 5 Patient evacuated.
    /// </summary>
    public class ObjectiveTracker : MonoBehaviour
    {
        [Serializable]
        public class Item
        {
            public string label;
            public Text row;                  // single row: "[x] <label>"
            [HideInInspector] public bool done;
        }

        [Header("Rows (in order)")]
        public Item[] items;

        [Header("Glyphs / colors")]
        public string uncheckedGlyph = "[  ]";
        public string checkedGlyph = "[x]";
        public Color pendingColor = new Color(1f, 1f, 1f, 0.7f);
        public Color doneColor = new Color(0.55f, 0.92f, 0.55f, 1f);

        [Header("Highlight")]
        public float highlightScale = 1.22f;
        public float highlightTime = 0.4f;

        private RescueSequenceController _rescue;
        private Vector3GameEvent _reqCh, _doneCh;
        private bool _reported, _evacuated;

        // Forward-only beat ordering (set by RescueSequenceController.CurrentBeat).
        private static readonly Dictionary<string, int> Order = new Dictionary<string, int>
        {
            { "idle", 0 }, { "dispatch", 1 }, { "arrived", 2 }, { "cpr", 3 },
            { "lift", 4 }, { "load", 5 }, { "depart", 6 }, { "done", 7 }
        };

        private void Awake()
        {
            for (int i = 0; i < items.Length; i++) SetRow(i, false);
        }

        private void Start()
        {
            _rescue = UnityEngine.Object.FindFirstObjectByType<RescueSequenceController>();
            if (_rescue != null)
            {
                _reqCh = _rescue.rescueChannel;
                _doneCh = _rescue.rescueCompletedChannel;
            }
            if (_reqCh != null) _reqCh.OnRaised += OnReported;
            if (_doneCh != null) _doneCh.OnRaised += OnEvacuated;
        }

        private void OnDestroy()
        {
            if (_reqCh != null) _reqCh.OnRaised -= OnReported;
            if (_doneCh != null) _doneCh.OnRaised -= OnEvacuated;
        }

        private void OnReported(Vector3 _) => _reported = true;
        private void OnEvacuated(Vector3 _) => _evacuated = true;

        private void Update()
        {
            int bi = -1;
            if (_rescue != null) Order.TryGetValue(_rescue.CurrentBeat ?? "", out bi);

            Check(0, _reported || bi >= 1);   // Emergency reported (911 handoff)
            Check(1, bi >= 1);                // Ambulance dispatched (E)
            Check(2, bi >= 2);                // Ambulance on scene (parked)
            Check(3, bi >= 3);                // CPR performed (C)
            Check(4, bi >= 4);                // Patient secured (F / lift)
            Check(5, _evacuated || bi >= 6);  // Patient evacuated (depart/done)
        }

        private void Check(int i, bool done)
        {
            if (i < 0 || i >= items.Length) return;
            if (done && !items[i].done)
            {
                items[i].done = true;
                SetRow(i, true);
                if (items[i].row != null) StartCoroutine(Pop(items[i].row.rectTransform));
            }
        }

        private void SetRow(int i, bool done)
        {
            var it = items[i];
            if (it == null || it.row == null) return;
            it.row.text = (done ? checkedGlyph : uncheckedGlyph) + "  " + it.label;
            it.row.color = done ? doneColor : pendingColor;
        }

        private IEnumerator Pop(RectTransform t)
        {
            if (t == null) yield break;
            float e = 0f;
            while (e < highlightTime)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / highlightTime);
                float s = 1f + (highlightScale - 1f) * Mathf.Sin(k * Mathf.PI);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }
    }
}
