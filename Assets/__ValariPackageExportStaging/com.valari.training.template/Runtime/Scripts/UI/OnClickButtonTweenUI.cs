using DG.Tweening;
using ObjectiveManagerandQuestEngine;
using UnityEngine;
using UnityEngine.Events;
using Valari.Utilities;

namespace Valari.UI
{
    public class OnClickButtonTweenUI : MonoBehaviour
    {
        public UnityEvent OnButtonPressedEvent;

        public void OnButtonPressed()
        {
            transform.DOPunchScale(-(Vector3.one * .2f), .5f, 5, 0);
            Delay.RunLater(this, .25f, () =>
            {
                OnButtonPressedEvent?.Invoke();
            });
        }

        public void PlayMenuButtonSFX()
        {
            AudioManager.instance.PlayNextSFX();
        }
    }
}

