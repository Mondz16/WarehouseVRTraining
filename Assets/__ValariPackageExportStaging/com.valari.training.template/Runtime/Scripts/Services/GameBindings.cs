using UnityEngine;
using Valari.Manager;
using Valari.Views;

namespace Valari.Services
{
    public sealed class GameBindings : ScriptableObject
    {
        [SerializeField]
        private SampleBind _sampleBind;

        public SampleBind SampleBind => _sampleBind;

        [SerializeField]
        private SampleManager _sampleManager;

        public SampleManager SampleManager => _sampleManager;

        [SerializeField]
        private ViewCollection _viewCollection;

        public ViewCollection ViewCollection => _viewCollection;
    }
}

