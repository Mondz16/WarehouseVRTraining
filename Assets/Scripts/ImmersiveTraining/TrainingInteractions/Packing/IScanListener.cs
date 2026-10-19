namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Implemented by components on a scannable item that react to being scanned by a <see cref="ScannerTool"/>
    /// (e.g. the M2 shipping label shows its verification readout).
    /// </summary>
    public interface IScanListener
    {
        void OnScanned(ScannerTool scanner, bool verified);
    }
}
