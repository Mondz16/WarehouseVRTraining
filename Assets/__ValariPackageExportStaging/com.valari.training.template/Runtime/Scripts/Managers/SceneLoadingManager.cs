using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Valari.Utilities;

namespace Valari.Manager
{
    public class SceneLoadingManager : MonoBehaviour
    {
        [SerializeField] private Material _startUpSkybox;
        [SerializeField] private float _sceneLoadingDelay;
        public UnityEvent OnLoadScene;

        private void Start()
        {
            RenderSettings.skybox = _startUpSkybox;
            Delay.RunLater(this, _sceneLoadingDelay, () =>
            {
                OnLoadScene?.Invoke();
            });
        }
    }
}

