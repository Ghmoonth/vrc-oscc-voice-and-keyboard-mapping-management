namespace OSCC;

public sealed class ModuleContext
{
    public required Form Owner { get; init; }
    public required AppConfig Config { get; init; }
    public required OscClient Osc { get; init; }
    public required Action<string> Log { get; init; }
    public required Action SaveConfig { get; init; }
}

public interface IOsccModule : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    bool IsRunning { get; }
    Control View { get; }
    void Initialize(ModuleContext context);
    void Start();
    void Stop();
    bool HandleMessage(ref Message message);
}

public sealed class ModuleHost : IDisposable
{
    private readonly List<IOsccModule> modules = new();

    public IReadOnlyList<IOsccModule> Modules => modules;

    public void Register(IOsccModule module, ModuleContext context)
    {
        module.Initialize(context);
        modules.Add(module);
    }

    public void StartAll()
    {
        foreach (var module in modules)
        {
            module.Start();
        }
    }

    public void StopAll()
    {
        foreach (var module in modules)
        {
            module.Stop();
        }
    }

    public bool HandleMessage(ref Message message)
    {
        foreach (var module in modules)
        {
            if (module.HandleMessage(ref message))
            {
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        foreach (var module in modules)
        {
            module.Dispose();
        }
        modules.Clear();
    }
}
