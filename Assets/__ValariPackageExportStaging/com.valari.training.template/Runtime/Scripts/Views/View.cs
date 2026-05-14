using UnityEngine;

namespace Valari.Views
{
    public abstract class View : MonoBehaviour, IView
    {
        public ViewContainer ViewContainer
        {
            get { return gameObject.GetComponentInParent<ViewContainer>(); }
        }

        public virtual void Initialize()
        {
            gameObject.SetActive(true);
        }

        public virtual void Release()
        {
            gameObject.SetActive(false);
        }
    }
}

