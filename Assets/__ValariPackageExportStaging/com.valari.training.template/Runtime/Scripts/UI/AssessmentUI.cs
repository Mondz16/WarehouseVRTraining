using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Valari.Data;
using Valari.Managers;

namespace Valari.UI
{
    public class AssessmentUI : MonoBehaviour
    {
        [Title("Question Container")] [SerializeField]
        private Animator _containerAnimator;
        [SerializeField] private GameObject _questionContainer;
        [SerializeField] private TMP_Text _trainingIDText;
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private Button[] _choiceButtons;
        [SerializeField] private TMP_Text[] _choiceLabels;

        [Title("Result Container")]
        [SerializeField]
        private Animator _resultContainerAnimator;
        [SerializeField] private GameObject _resultContainer;
        [SerializeField] private TMP_Text _trainingTitleText;
        [SerializeField] [Space(5)] private TMP_Text _scoreText;

        private int _selectedIndex = 0;
        private AssessmentManager _assessmentManager;
        private AssessmentData _data;

        public void InjectAssessmentManager(AssessmentManager manager)
        {
            _assessmentManager = manager;
        }

        public void ShowQuestion(TrainingID id, AssessmentData data)
        {
            _questionContainer.SetActive(true);
            _resultContainer.SetActive(false);
            _containerAnimator.Play("show");

            _trainingIDText.text = id.ToString();
            _trainingTitleText.text = id.ToString();

            _data = data;
            _questionText.text = data.Question;
            for (var i = 0; i < data.Choices.Count; i++)
            {
                int index = i;
                _choiceLabels[index].text = data.Choices[index];
                _choiceButtons[index].onClick.RemoveAllListeners();
                _choiceButtons[index].onClick.AddListener(() => OnSelectedAnswer(index));
            }
        }

        [Button]
        public void ShowAnim()
        {
            _questionContainer.SetActive(true);
            _containerAnimator.Play("show");
        }

        [Button]
        public void ShowAnim2()
        {
            _resultContainer.SetActive(true);
            _resultContainerAnimator.Play("show");
        }

        public void ShowResult(int score, int total)
        {
            _questionContainer.SetActive(false);
            _resultContainer.SetActive(true);
            _resultContainerAnimator.Play("show");

            _scoreText.text = $"{score} / {total}";
        }

        public void OnCloseButtonClicked()
        {
            _questionContainer.SetActive(false);
            _resultContainer.SetActive(false);
            _assessmentManager.OnFinishAssessment();
        }

        private void OnSelectedAnswer(int index)
        {
            _selectedIndex = index;
            EventSystem.current.SetSelectedGameObject(null);
            _assessmentManager.SubmitAnswer(_data.Choices[_selectedIndex]);
        }
    }
}

