using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    public enum QcElement { Label, Seal, VoidFill }

    /// <summary>
    /// Quality review of the pre-packed box (M2_09). Three buttons let the trainee name the faulty element; the right
    /// answer (void fill) only counts once the box has been opened with the box cutter. The box must then be filled to
    /// full and the centre seam re-taped. Completes <see cref="_identifiedTrigger"/> and <see cref="_correctedTrigger"/>
    /// (in order, both arm-aware). Buttons are bound in code because MCP cannot wire UnityEvents.
    /// </summary>
    public class QcReview : MonoBehaviour
    {
        [SerializeField] private PackingCarton _carton;
        [SerializeField] private Button _labelButton;
        [SerializeField] private Button _sealButton;
        [SerializeField] private Button _voidFillButton;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private QcElement _faultyElement = QcElement.VoidFill;
        [SerializeField] private ChallengeTutorialTrigger _identifiedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _correctedTrigger;

        [TextArea] [SerializeField] private string _prompt =
            "Check the label, the seal and the void fill. Which one is wrong?";

        [SerializeField] private Color _neutralColor = Color.white;
        [SerializeField] private Color _goodColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color _badColor = new Color(1f, 0.4f, 0.3f);

        private readonly ArmedLatch _identifiedLatch = new ArmedLatch();
        private readonly ArmedLatch _correctedLatch = new ArmedLatch();

        public PackingCarton Carton => _carton;
        public bool IsIdentified { get; private set; }
        public bool IsCorrected => _carton != null && _carton.IsFull && _carton.IsSealed;
        public string ResultText => _resultText != null ? _resultText.text : string.Empty;

        public Button ButtonFor(QcElement element) =>
            element == QcElement.Label ? _labelButton : element == QcElement.Seal ? _sealButton : _voidFillButton;

        private void Awake()
        {
            Bind(_labelButton, QcElement.Label);
            Bind(_sealButton, QcElement.Seal);
            Bind(_voidFillButton, QcElement.VoidFill);
            if (_carton != null) _carton.Changed += HandleCartonChanged;
            if (_identifiedTrigger != null) _identifiedTrigger.Armed += HandleArmed;
            if (_correctedTrigger != null) _correctedTrigger.Armed += HandleArmed;
            Show(_prompt, _neutralColor);
        }

        private void OnDestroy()
        {
            if (_carton != null) _carton.Changed -= HandleCartonChanged;
            if (_identifiedTrigger != null) _identifiedTrigger.Armed -= HandleArmed;
            if (_correctedTrigger != null) _correctedTrigger.Armed -= HandleArmed;
        }

        private void Bind(Button button, QcElement element)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Answer(element));
        }

        public void Answer(QcElement element)
        {
            if (IsIdentified)
            {
                Show("Fault found - now fix it: fill the box to full, then re-tape the centre seam.", _goodColor);
                return;
            }

            if (element != _faultyElement)
            {
                Show($"The {Name(element)} on this box is correct - check the other elements.", _badColor);
                TutorialManager.OnUpdateMistakeCountEvent?.Invoke();
                return;
            }

            if (element == QcElement.VoidFill && _carton != null && !_carton.HasBeenOpened)
            {
                Show("You can't see the void fill yet - open the box by cutting the centre seam tape with the inspect tool (box cutter).", _badColor);
                return;
            }

            IsIdentified = true;
            Show("Correct - the box is under-filled, so the mugs can shift and break. Add void fill until it is full, then re-tape the centre seam.", _goodColor);
            Offer();
        }

        private static string Name(QcElement element) =>
            element == QcElement.Label ? "label" : element == QcElement.Seal ? "seal" : "void fill";

        private void HandleCartonChanged(PackingCarton carton) => Offer();

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer()
        {
            _identifiedLatch.Offer(IsIdentified, () => _identifiedTrigger == null || _identifiedTrigger.TryComplete());
            if (_correctedLatch.Offer(IsCorrected, () => _correctedTrigger == null || _correctedTrigger.TryComplete()))
                Show("Box corrected and resealed - ready for dispatch.", _goodColor);
        }

        /// <summary>Puts the QC box back in its faulty state (module restart).</summary>
        public void ResetReview()
        {
            IsIdentified = false;
            _identifiedLatch.Reset();
            _correctedLatch.Reset();
            if (_carton != null) _carton.ResetToPrePacked();
            Show(_prompt, _neutralColor);
        }

        private void Show(string message, Color color)
        {
            if (_resultText == null) return;
            _resultText.text = message;
            _resultText.color = color;
        }
    }
}
