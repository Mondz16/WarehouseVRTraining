using System;
using UnityEngine;

public class TransformCopy : MonoBehaviour
{
    [SerializeField] private Transform _target;

    private void LateUpdate()
    {
        this.transform.localRotation = _target.transform.localRotation;
    }
}
