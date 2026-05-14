namespace Valari.Views
{
    public interface IView<T> : IView
    {
        void Initialize(T data);
    }
}

