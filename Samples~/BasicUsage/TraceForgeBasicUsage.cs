using UnityEngine;
using TraceForge;

/// <summary>
/// Demonstrates TraceForge logging setup and usage patterns.
/// Attach to any GameObject to write logs asynchronously to a file and retain recent entries in memory.
/// </summary>
public class TraceForgeBasicUsage : MonoBehaviour
{
    private FileSink _fileSink;

    private void Awake()
    {
        // Bootstrap provides the Editor ring buffer. Add an extra asynchronous file sink.
        // Use a separate name so it does not share the bootstrap file handle.
        _fileSink = new FileSink(Application.persistentDataPath + "/traceforge-sample.log");
        TF.AddSink(_fileSink);

        TF.Info("TraceForge initialized");
    }

    private void Start()
    {
        // Basic logging
        TF.Trace("Trace: very detailed info");
        TF.Debug("Debug: development info");
        TF.Info("Info: normal event");
        TF.Warning("Warning: unexpected but handled");
        TF.Error("Error: needs attention");

        // Category logging
        TF.Info(Categories.Gameplay, "Player spawned");
        TF.Warning(Categories.Network, "Packet loss detected");
        TF.Error(Categories.UI, "Missing UI element");

        // Custom categories
        var inventoryCategory = new LogCategory("Inventory");
        TF.Info(inventoryCategory, "Items loaded: 42");

        // Expensive formatting — check IsEnabled first
        if (TF.IsEnabled(Verbosity.Trace))
            TF.Trace($"Transform: {transform.position} / {transform.rotation}");

        // Per-category verbosity override
        TF.SetCategoryVerbosity(Categories.Physics, Verbosity.Warning);
        TF.Debug(Categories.Physics, "This will be filtered out");
        TF.Warning(Categories.Physics, "This will appear");

        // Exception logging
        try
        {
            throw new System.InvalidOperationException("Example exception");
        }
        catch (System.Exception ex)
        {
            TF.Error(Categories.Gameplay, "Caught exception", ex);
        }
    }

    private void OnDestroy()
    {
        // Remove, flush, and close file sink when done
        if (_fileSink != null)
        {
            TF.RemoveSink(_fileSink);
            _fileSink.Dispose();
        }
    }
}
