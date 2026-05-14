using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

public class UILabelFollower : MonoBehaviour
{
    [SerializeField] private Transform _pointA;
    [SerializeField] private Transform _pointB;
    [SerializeField] private RectTransform _lineImage;
    [SerializeField] private Transform _canvasTransform;
    [SerializeField] private DOTweenAnimation _doTweenAnimation;
    [SerializeField] private float _labelDuration = .2f;
    private float _labelCountdownTimer = 0;
    private Camera _mainCamera;
    private bool _showLabel = false;

    void Start()
    {
        _mainCamera = Camera.main;

        if (_pointA == null || _pointB == null || _lineImage == null || _canvasTransform == null) return;

        Vector3 worldPosA = _pointA.position;
        Vector3 worldPosB = _pointB.position;

        // Midpoint positioning
        // Vector3 midpoint = (worldPosA + worldPosB) / 2f;
        _lineImage.position = worldPosA;

        // Direction and angle
        Vector3 direction = (worldPosB - worldPosA).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        _lineImage.rotation = Quaternion.Euler(0, 0, angle);

        // Adjust for canvas scale (assumes uniform scaling on x)
        float canvasScale = _canvasTransform.lossyScale.x;
        float worldDistance = Vector3.Distance(worldPosA, worldPosB);
        float scaledWidth = worldDistance / canvasScale;

        // Update width while keeping height (y) constant (2.5)
        _lineImage.sizeDelta = new Vector2(scaledWidth, _lineImage.sizeDelta.y);
        Debug.Log($"#{GetType().Name}# Distance -> {scaledWidth}");
    }

    private void Update()
    {
        if (_showLabel)
        {
            if (_labelCountdownTimer > 0)
            {
                _labelCountdownTimer -= Time.deltaTime;
                if (_canvasTransform != null && _mainCamera != null)
                {
                    _canvasTransform.rotation = Quaternion.LookRotation(_canvasTransform.position - _mainCamera.transform.position);
                }
            }
            else
            {
                RewindAnimation();
            }
        }
    }

    [Button()]
    public void PlayAnimation()
    {
        if (!_showLabel)
        {
            _showLabel = true;
            _doTweenAnimation.gameObject.SetActive(true);
            _doTweenAnimation.DORestart();
        }

        _labelCountdownTimer = _labelDuration;
    }

    [Button()]
    public void RewindAnimation()
    {
        if (_showLabel)
        {
            _showLabel = false;
            _doTweenAnimation.DORewind();
            _doTweenAnimation.enabled = false;
        }
    }
}

