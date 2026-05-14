using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Valari.UI
{
    public class ChallengeToggleUI : MonoBehaviour
    {
        [SerializeField] private Toggle _toggle;
        [SerializeField] private TMP_Text _challengeLabel;

        public void SetChallengeUI(string label)
        {
            UpdateChallengeUI(false);
            _challengeLabel.text = label;
        }

        public string GetChallengeLabel()
        {
            return _challengeLabel.text;
        }

        public void UpdateChallengeUI(bool isOn)
        {
            _toggle.isOn = isOn;
        }
    }
}

