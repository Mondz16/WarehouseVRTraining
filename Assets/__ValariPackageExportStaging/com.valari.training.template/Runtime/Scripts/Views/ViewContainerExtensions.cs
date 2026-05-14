using UnityEngine;
using Valari.Services;

namespace Valari.Views
{
    public static class ViewContainerExtensions
    {
        private static SampleView _sampleView;

        public static void OpenSampleView(this ViewContainer viewContainer, SampleView.SampleParams dialogParams)
        {
            ViewCollection viewCollection = Game.Services.Get<ViewCollection>();
            viewContainer.Push(viewCollection.SampleView, dialogParams);
        }

    }
}

