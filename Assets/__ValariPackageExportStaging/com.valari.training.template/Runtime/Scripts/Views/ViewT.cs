using UnityEngine;

namespace Valari.Views
{
    public abstract class View<T> : View, IView<T>
    {
        public virtual void Initialize(T data)
        {
            base.Initialize();
        }

        public sealed override void Initialize()
        {
            base.Initialize();
        }
    }
}

