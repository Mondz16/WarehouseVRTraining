using System.Collections.Generic;
using UnityEngine;
using Valari.Services;

namespace Valari.Views
{
    public sealed class ViewContainer : MonoBehaviour
    {
        public static ViewContainer Services { get { if (_viewContainer == null) _viewContainer = Game.Services.Get<ViewContainer>(); return _viewContainer; } }
        private static ViewContainer _viewContainer;

        public int Count
        {
            get { return _views.Count; }
        }

        public View Peek
        {
            get { return _views.Peek(); }
        }

        [SerializeField] private Transform _container;

        private readonly Stack<View> _views = new Stack<View>();

        public View<T> Switch<T>(View<T> viewPrefab, T data)
        {
            Pop();
            return Push(viewPrefab, data);
        }

        public View Switch(View viewPrefab)
        {
            Pop();
            return Push(viewPrefab);
        }

        public View<T> Push<T>(View<T> viewPrefab, T data)
        {
            View<T> view = InstantiateView(viewPrefab);
            _views.Push(view);
            view.Initialize(data);
            return view;
        }

        public View Push(View viewPrefab)
        {
            View view = InstantiateView(viewPrefab);
            _views.Push(view);
            view.Initialize();
            return view;
        }

        public void Pop()
        {
            if (_views.Count > 0)
            {
                View view = _views.Pop();
                view.Release();
                DestroyView(view);
            }
        }

        public void Clear()
        {
            while (_views.Count > 0)
                Pop();
        }

        private View<T> InstantiateView<T>(View<T> viewPrefab)
        {
            return Instantiate(viewPrefab, _container);
        }

        private View InstantiateView(View viewPrefab)
        {
            return Instantiate(viewPrefab, _container);
        }

        private void DestroyView(View view)
        {
            Destroy(view.gameObject);
        }
    }
}

