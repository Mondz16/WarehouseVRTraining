using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Valari.Views;

public class SampleView : View<SampleView.SampleParams>
{
    [SerializeField]
    private Text _headerText;

    [SerializeField]
    private Text _descriptionText;

    [SerializeField]
    private Button _okButton;

    [Serializable]
    public class SampleParams
    {
        public string Header;
        public string Description;
        public UnityAction OkAction = null;
    }

    public override void Initialize(SampleParams data)
    {
        base.Initialize(data);
        ShowView(data);
    }

    private void ShowView(SampleParams data)
    {
        _headerText.text = data.Header;
        _descriptionText.text = data.Description;
        _okButton.onClick.AddListener(() =>
        {
            ViewContainer.Pop();
            data.OkAction?.Invoke();
        });
    }
}

