using UnityEngine;
using Valari.Manager;
using Valari.Views;

public class SampleCaller : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SampleManager.Services.SampleMethod();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            SampleBind.Services.SampleMethod();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            ViewContainer.Services.OpenSampleView(new SampleView.SampleParams
            {
                Header = "Test View",
                Description = "This is a test description"
            });
        }
    }
}

