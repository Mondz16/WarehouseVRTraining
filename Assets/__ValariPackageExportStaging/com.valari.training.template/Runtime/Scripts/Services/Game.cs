using UnityEngine;
using Valari.FoundryTraining.Controller;
using Valari.Manager;
using Valari.Managers;
using Valari.Views;

namespace Valari.Services
{
    public static class Game
    {
        public static readonly ServiceLocator Services = new ServiceLocator();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            GameBindings bindings = Resources.Load<GameBindings>("GameBindings");

            Services.Clear();
            // InitializeUnityServices(bindings);
            // InitializeViewServices(bindings);

            // NOTE: In adding a component to the GameBindings, just add once

            // if not in the GameBindings then use this to find the component in the scene
            Services.Add<GameDataManager>(new UnityComponentServiceProvider<GameDataManager>());
            Services.Add<UIManager>(new UnityComponentServiceProvider<UIManager>());
            Services.Add<TrainerController>(new UnityComponentServiceProvider<TrainerController>());

            // add the objects from GameBindings to the Services
            // Services.Add(bindings.SampleBind);
        }

        /// <summary>
        /// Instantiate the "Services" GameObject in DontDestroyOnLoad
        /// </summary>
        private static void InitializeUnityServices(GameBindings bindings)
        {
            GameObject servicesObject = new GameObject("Services");
            Object.DontDestroyOnLoad(servicesObject);

            // InitializeSampleBind(servicesObject, bindings);
        }

        /// <summary>
        /// This method instantiate the sampleBind prefab to the Services
        /// Add the service object to the ServicesLocator
        /// </summary>
        private static void InitializeSampleBind(GameObject servicesObject, GameBindings bindings)
        {
            SampleManager sampleManager = Object.Instantiate(bindings.SampleManager, servicesObject.transform);
            Services.Add<SampleManager>(sampleManager);
        }

        /// <summary>
        /// This method initialize/add the view collection and container to the services
        /// </summary>
        private static void InitializeViewServices(GameBindings bindings)
        {
            Services.Add<ViewCollection>(bindings.ViewCollection);
            Services.Add<ViewContainer>(new UnityComponentServiceProvider<ViewContainer>());
        }
    }
}

