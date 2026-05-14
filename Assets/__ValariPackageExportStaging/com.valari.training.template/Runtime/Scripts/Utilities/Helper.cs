using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Valari.Utilities
{
    public static class Helper
    {
        private static Camera _camera;

        public static Camera Camera
        {
            get
            {
                if (_camera == null) _camera = Camera.main;
                return _camera;
            }
        }

        private static readonly Dictionary<float, WaitForSeconds> _waitDictionary = new();

        /// <summary>
        /// Get WaitForSeconds in the dictionary
        /// This method is helpful to reduce the garbage collection of the game
        /// </summary>
        public static WaitForSeconds GetWait(float time)
        {
            if (_waitDictionary.TryGetValue(time, out WaitForSeconds wait)) return wait;

            _waitDictionary[time] = new WaitForSeconds(time);
            return _waitDictionary[time];
        }

        private static PointerEventData _eventDataCurrentPosition;
        private static List<RaycastResult> _results;

        /// <summary>
        /// This method checks if the mouse pos is over a UI element
        /// </summary>
        public static bool IsOverUI()
        {
            _eventDataCurrentPosition = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            _results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(_eventDataCurrentPosition, _results);
            return _results.Count > 0;
        }

        /// <summary>
        /// This method returns the World Position of the Canvas element 
        /// </summary>
        public static Vector2 GetWorldPositionOfCanvasElements(RectTransform element)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(element, element.position, Camera, out var result);
            return result;
        }

        /// <summary>
        /// This method delete all the children of a Transform
        /// </summary>
        public static void DeleteChildren(this Transform t)
        {
            foreach (Transform child in t) Object.Destroy(child.gameObject);
        }
    }
}

