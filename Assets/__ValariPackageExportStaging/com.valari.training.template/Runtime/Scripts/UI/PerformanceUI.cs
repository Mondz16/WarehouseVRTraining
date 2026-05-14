using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Valari.UI
{
    public class PerformanceUI : MonoBehaviour
    {
        [SerializeField] private GameObject _container;
        [SerializeField] private TMP_Text _trainingIDText;
        [SerializeField] private List<TMP_Text> _performanceTextList;
        [SerializeField] private List<Image> _performanceResultImgList;

        [SerializeField] private float _fillDuration = 1f;
        [SerializeField] private Ease _fillEase = Ease.OutCubic;

        public void ShowPerformanceResult(TrainingID id, float efficiency, float accuracy, float assessment)
        {
            _container.SetActive(true);
            _trainingIDText.text = id.ToString();
            _performanceTextList[0].text = $"{Mathf.FloorToInt(efficiency).ToString()}%";
            _performanceTextList[1].text = $"{Mathf.FloorToInt(accuracy).ToString()}%";
            _performanceTextList[2].text = $"{Mathf.FloorToInt(assessment).ToString()}%";

            AnimateFill(_performanceResultImgList[0], efficiency / 100);
            AnimateFill(_performanceResultImgList[1], accuracy / 100);
            AnimateFill(_performanceResultImgList[2], assessment / 100);
        }

        public void OnCloseButtonClicked()
        {
            _container.SetActive(false);
        }

        private void AnimateFill(Image image, float targetFill)
        {
            image.fillAmount = 0f;
            image.DOFillAmount(targetFill, _fillDuration).SetEase(_fillEase);
        }
    }
}

