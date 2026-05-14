using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Valari.Utilities
{
    [Serializable]
    public class PoolItem
    {
        public GameObject PoolObject;
        public int PoolAmount;
        public bool Growable; // whether the pool can grow in numbers if needed.
    }

    /// <summary>
    ///     Singleton
    /// </summary>
    public sealed class PoolingManager : MonoBehaviour
    {
        [SerializeField]
        private PoolItem[] _poolItems;
        private readonly Dictionary<int, bool> _growableBool = new Dictionary<int, bool>();
        private readonly Dictionary<int, Transform> _parents = new Dictionary<int, Transform>();

        private readonly Dictionary<int, Queue<GameObject>> _poolQueue = new Dictionary<int, Queue<GameObject>>();

        private GameObject _poolGroup;

        private void Awake()
        {
            InitializePool();
        }

        public void DestroyPool()
        {
            if (_poolGroup != null)
                _poolGroup.SetActive(false);
        }

        /// <summary>
        ///     Instantiate objects to pool.
        /// </summary>
        private void InitializePool()
        {
            _poolGroup = new GameObject("Pool Group");

            for (int i = 0; i < _poolItems.Length; i++)
            {
                GameObject uniquePool = new GameObject(_poolItems[i].PoolObject.name + "Group"); // create pool group game object to keep the pooled items.
                uniquePool.transform.SetParent(_poolGroup.transform); // set as parent

                int objectId = _poolItems[i].PoolObject.GetInstanceID(); //
                _poolItems[i].PoolObject.SetActive(false); // initially set to false.

                _poolQueue.Add(objectId, new Queue<GameObject>());
                _growableBool.Add(objectId, _poolItems[i].Growable);
                _parents.Add(objectId, uniquePool.transform);

                for (int j = 0; j < _poolItems[i].PoolAmount; j++)
                {
                    GameObject temp = Instantiate(_poolItems[i].PoolObject, uniquePool.transform);
                    _poolQueue[objectId].Enqueue(temp); // make sure the object is added to the queue.
                }
            }
        }


        /// <summary>
        ///     Use an object to pool. Works similar to instantiate but just a pooled version.
        /// </summary>
        public GameObject UseObject(GameObject obj, Vector3 position, Quaternion rotation)
        {
            int objectId = obj.GetInstanceID();

            GameObject temp = _poolQueue[objectId].Dequeue();

            if (temp.activeInHierarchy)
            {
                if (_growableBool[objectId])
                {
                    _poolQueue[objectId].Enqueue(temp);
                    temp = Instantiate(obj, _parents[objectId]);
                    temp.transform.position = position;
                    temp.transform.rotation = rotation;
                    temp.SetActive(true);
                }
                else
                    temp = null;
            }
            else
            {
                temp.transform.position = position;
                temp.transform.rotation = rotation;
                temp.SetActive(true);
            }

            _poolQueue[objectId].Enqueue(temp);
            return temp;
        }

        /// <summary>
        ///     Release object. Equivalent to DestroyObject but in pooled manner.
        /// </summary>
        public void ReturnObject(GameObject obj, float delay = 0f)
        {
            if (delay == 0f)
                obj.SetActive(false);
            else
                StartCoroutine(IEDelayReturn(obj, delay));
        }

        /// <summary>
        ///     In any case we want to add a delay on returning an object.
        /// </summary>
        private IEnumerator IEDelayReturn(GameObject obj, float delay)
        {
            while (delay > 0f)
            {
                delay -= Time.deltaTime;
                yield return null;
            }

            obj.SetActive(false);
        }
    }
}

