using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// "Assessment not passed" screen shown by <see cref="AssessmentChallengeGate"/> when the learner
    /// scores below the pass mark on the M1_09 quiz. Displays the score and the pass requirement, and a
    /// Retry button that relaunches the assessment from the first question. It toggles its own root and
    /// does not touch the shared AssessmentUI.
    ///
    /// This panel is duplicated from the quiz result screen, so its button carries an inherited
    /// persistent onClick (which closed the quiz). <see cref="Awake"/> disables those inherited
    /// listeners and binds <see cref="OnRetry"/> instead.
    /// </summary>
    public class AssessmentFailPanel : MonoBehaviour
    {
        [Tooltip("Root object toggled on/off (defaults to this GameObject).")]
        [SerializeField] private GameObject _root;

        [Tooltip("Text element the fail message / score is written into.")]
        [SerializeField] private TMP_Text _message;

        [Tooltip("Button that relaunches the assessment.")]
        [SerializeField] private Button _retryButton;

        [Tooltip("Assessment manager to relaunch on retry.")]
        [SerializeField] private AssessmentManager _manager;

        [SerializeField] private UnityEvent _onRetry;

        private void Awake()
        {
            if (_root == null) _root = gameObject;

            if (_retryButton != null)
            {
                // Disable any inherited persistent listeners (this panel is duplicated from the quiz
                // result screen, whose button closed the quiz) and bind our own retry handler.
                int count = _retryButton.onClick.GetPersistentEventCount();
                for (int i = 0; i < count; i++)
                    _retryButton.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(OnRetry);
            }

            // NOTE: no SetActive(false) here — the panel is saved inactive, and Awake runs during the
            // first Show() (as the panel activates), so hiding here would immediately undo the show.
        }

        /// <summary>Shows the fail screen with the score and the pass requirement.</summary>
        public void Show(int score, int total, int passPercent)
        {
            if (_message != null)
            {
                int needed = Mathf.CeilToInt(total * passPercent / 100f);
                _message.text =
                    $"Not passed.\nYou scored {score}/{total} — {passPercent}% ({needed}/{total}) required.\n" +
                    "Press Retake to try again.";
            }

            _root.SetActive(true);
        }

        public void Hide()
        {
            _root.SetActive(false);
        }

        /// <summary>Bound to the Retry button: hides this screen and relaunches the quiz.</summary>
        public void OnRetry()
        {
            Hide();
            if (_manager != null) _manager.RetakeAssessment();
            _onRetry?.Invoke();
        }
    }
}
