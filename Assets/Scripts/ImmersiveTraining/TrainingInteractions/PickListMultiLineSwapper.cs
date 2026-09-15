using UnityEngine;
using UnityEngine.Events;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Swaps the wrist pick list from the single-line view (M1_02–M1_05) to the multi-line view
    /// (M1_06). Place this on the M1_06 tutorial panel: when that step is shown it hides the
    /// single-line row(s) and shows the multi-line rows.
    ///
    /// The tutorial panels are saved active, so this component's OnEnable also fires once at scene
    /// load — before the module runs. To avoid swapping prematurely, the swap is gated on the
    /// OrderPicking training having started (<see cref="TutorialManager.OnTrainingStartedEvent"/>).
    /// That same event resets the list back to the single-line view, so a module restart shows the
    /// correct initial state again.
    /// </summary>
    public class PickListMultiLineSwapper : MonoBehaviour
    {
        [Tooltip("Rows shown in the single-line view (M1_02–05); hidden when the multi-line view is shown.")]
        [SerializeField] private GameObject[] _singleLineRows;

        [Tooltip("Rows of the multi-line order (M1_06); hidden until this step is shown.")]
        [SerializeField] private GameObject[] _multiLineRows;

        [Tooltip("If true, only swaps once per module run.")]
        [SerializeField] private bool _once = true;

        [SerializeField] private UnityEvent _onSwapped;

        private bool _armed;
        private bool _done;

        private void Awake()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
        }

        private void OnDestroy()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
        }

        private void HandleTrainingStarted(TrainingID id)
        {
            if (id != TrainingID.OrderPicking) return;

            // Module (re)started: arm the swap and reset to the single-line view.
            _armed = true;
            _done = false;
            SetView(singleLine: true);
        }

        private void OnEnable()
        {
            if (_once && _done) return;
            if (!_armed) return; // ignore the scene-load / pre-module enable

            SetView(singleLine: false);
            _done = true;
            _onSwapped?.Invoke();
        }

        private void SetView(bool singleLine)
        {
            if (_singleLineRows != null)
                foreach (var go in _singleLineRows)
                    if (go != null) go.SetActive(singleLine);

            if (_multiLineRows != null)
                foreach (var go in _multiLineRows)
                    if (go != null) go.SetActive(!singleLine);
        }
    }
}
