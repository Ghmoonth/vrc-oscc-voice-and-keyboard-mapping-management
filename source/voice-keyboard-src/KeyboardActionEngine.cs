using System.Globalization;

namespace OSCC;

public sealed class KeyboardActionEngine
{
    private readonly OscClient osc;
    private readonly Action<string> log;
    private readonly Dictionary<string, Action<KeyboardMapping, ParameterAction>> handlers;

    public KeyboardActionEngine(OscClient osc, Action<string> log)
    {
        this.osc = osc;
        this.log = log;
        handlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["set"] = SetValue,
            ["toggle"] = ToggleValue,
            ["pulse"] = PulseValue,
            ["increment"] = IncrementValue,
            ["decrement"] = DecrementValue
        };
    }

    public void Run(KeyboardMapping mapping)
    {
        var actions = mapping.EffectiveActions();
        foreach (var action in actions.Where(a => !string.IsNullOrWhiteSpace(a.Parameter)))
        {
            RunAction(mapping, action);
        }
    }

    private void RunAction(KeyboardMapping mapping, ParameterAction action)
    {
        if (!handlers.TryGetValue(action.Mode, out var handler))
        {
            log("鏈煡鎺у埗妯″紡: " + action.Mode);
            return;
        }

        try
        {
            handler(mapping, action);
        }
        catch (Exception ex)
        {
            log("鍙戦€佸け璐?" + action.Parameter + ": " + ex.Message);
        }
    }

    private void Send(KeyboardMapping mapping, ParameterAction action, object value)
    {
        var coerced = Coerce(value, action.Type);
        osc.Send(action.Parameter, coerced, action.Type);
        action.State = coerced;
        if (mapping.Actions.Count == 0) mapping.State = coerced;
        log(mapping.Hotkey + " -> " + action.Parameter + " = " + coerced);
    }

    private void SetValue(KeyboardMapping mapping, ParameterAction action) => Send(mapping, action, action.Value);

    private void ToggleValue(KeyboardMapping mapping, ParameterAction action)
    {
        var on = Coerce(action.Value, action.Type);
        var off = Coerce(action.OffValue, action.Type);
        Send(mapping, action, Equals(action.State ?? off, off) ? on : off);
    }

    private async void PulseValue(KeyboardMapping mapping, ParameterAction action)
    {
        Send(mapping, action, action.Value);
        await Task.Delay(Math.Max(1, action.DurationMs));
        Send(mapping, action, action.OffValue);
    }

    private void IncrementValue(KeyboardMapping mapping, ParameterAction action)
    {
        var current = Convert.ToDouble(action.State ?? Coerce(action.Value, action.Type), CultureInfo.InvariantCulture);
        var next = Math.Clamp(current + action.Step, action.Min, action.Max);
        Send(mapping, action, action.Type == "int" ? (int)next : next);
    }

    private void DecrementValue(KeyboardMapping mapping, ParameterAction action)
    {
        var current = Convert.ToDouble(action.State ?? Coerce(action.Value, action.Type), CultureInfo.InvariantCulture);
        var next = Math.Clamp(current - action.Step, action.Min, action.Max);
        Send(mapping, action, action.Type == "int" ? (int)next : next);
    }

    public static object Coerce(object value, string type)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return type.ToLowerInvariant() switch
        {
            "bool" => text.Equals("1", StringComparison.OrdinalIgnoreCase)
                || text.Equals("true", StringComparison.OrdinalIgnoreCase)
                || text.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || text.Equals("on", StringComparison.OrdinalIgnoreCase)
                || text.Equals("寮€", StringComparison.OrdinalIgnoreCase)
                || text.Equals("鏄?, StringComparison.OrdinalIgnoreCase),
            "int" => Convert.ToInt32(double.Parse(text, CultureInfo.InvariantCulture)),
            "float" => double.Parse(text, CultureInfo.InvariantCulture),
            "string" => text,
            _ => text
        };
    }
}
