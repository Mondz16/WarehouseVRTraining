using TMPro;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// One line of packing-station feedback (M2): green for progress, red for a rule the trainee broke. The line
    /// clears after a few seconds.
    /// </summary>
    public class PackingFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _holdSeconds = 6f;
        [SerializeField] private Color _goodColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color _badColor = new Color(1f, 0.4f, 0.3f);

        private float _clearAt;

        /// <summary>The last message shown (also read by the Play-mode test driver).</summary>
        public string LastMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null) _text.text = string.Empty;
        }

        public void Show(string message, bool good)
        {
            LastMessage = message ?? string.Empty;
            Debug.Log($"#{nameof(PackingFeedback)}# {(good ? "OK" : "NO")}: {LastMessage}");
            if (_text == null) return;

            _text.text = LastMessage;
            _text.color = good ? _goodColor : _badColor;
            _clearAt = Time.time + _holdSeconds;
        }

        private void Update()
        {
            if (_clearAt <= 0f || Time.time < _clearAt) return;
            if (_text != null) _text.text = string.Empty;
            _clearAt = 0f;
        }
    }
}
