using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Wrist / handheld pick-list panel (M1_02). Displays the current pick list and fires
    /// <see cref="_onFirstOpened"/> the first time the trainee opens it. Wire that event in the
    /// Inspector to the M1_02 ChallengeTutorialTrigger.OnTriggerComplete (TRIGGER_M1_OpenPickList).
    /// Call <see cref="Open"/> / <see cref="Close"/> / <see cref="Toggle"/> from the wrist gesture
    /// or a UI button. Use <see cref="SetLines"/> to swap the list between picks (single-line for
    /// M1_03..05, multi-line for M1_06).
    /// </summary>
    public class WristPickListUI : MonoBehaviour
    {
        [Serializable]
        public struct PickLine
        {
            public string Sku;
            public string Description;
            [Tooltip("Location code, e.g. A-02-03.")]
            public string Location;
            public int Quantity;
        }

        [Header("Display")]
        [Tooltip("Root object toggled on/off when the panel opens/closes.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("Text element the pick list is rendered into.")]
        [SerializeField] private TMP_Text _listText;

        [Tooltip("Optional heading shown above the lines.")]
        [SerializeField] private string _header = "PICK LIST";

        [SerializeField] private List<PickLine> _lines = new List<PickLine>();

        [Header("Events")]
        [Tooltip("Fired once, the first time the panel is opened.")]
        [SerializeField] private UnityEvent _onFirstOpened;

        [SerializeField] private UnityEvent _onOpened;
        

        [Header("Challenge (optional, direct wiring)")]
        [Tooltip("If set, its OnTriggerComplete() is called the first time the panel opens.")]
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;
[SerializeField] private UnityEvent _onClosed;

        private bool _hasOpened;

        /// <summary>True while the panel is visible.</summary>
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Awake()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            Render();
        }

        /// <summary>Replaces the displayed pick list (e.g. between single- and multi-line orders).</summary>
        public void SetLines(IEnumerable<PickLine> lines)
        {
            _lines = new List<PickLine>(lines);
            Render();
        }

        public void Open()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(true);
            Render();

            if (!_hasOpened)
            {
                _hasOpened = true;
                
                if (_challengeTrigger != null) _challengeTrigger.OnTriggerComplete();
_onFirstOpened?.Invoke();
            }
            _onOpened?.Invoke();
        }

        public void Close()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            _onClosed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Render()
        {
            if (_listText == null) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_header))
                sb.AppendLine($"<b>{_header}</b>");

            if (_lines.Count == 0)
            {
                sb.AppendLine("<i>No items.</i>");
            }
            else
            {
                for (int i = 0; i < _lines.Count; i++)
                {
                    PickLine line = _lines[i];
                    sb.AppendLine($"{i + 1}. <b>{line.Sku}</b>  x{line.Quantity}");
                    sb.AppendLine($"   {line.Description}");
                    sb.AppendLine($"   <color=#8ec>Loc: {line.Location}</color>");
                }
            }

            _listText.text = sb.ToString();
        }
    }
}
