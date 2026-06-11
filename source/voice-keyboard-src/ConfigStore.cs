using System.Text.Json;

namespace OSCC;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "vrcvosc_keyboard_config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var created = CreateDefault();
            Save(created);
            return created;
        }

        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Options) ?? CreateDefault();
    }

    public static void Save(AppConfig config)
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
    }

    private static AppConfig CreateDefault() => new()
    {
        Mappings =
        {
            new KeyboardMapping
            {
                Note = "绀轰緥甯冨皵寮€鍏?,
                Parameter = "ExampleBool",
                Hotkey = "Ctrl+Alt+1",
                Type = "bool",
                Mode = "toggle"
            },
            new KeyboardMapping
            {
                Note = "绀轰緥鏁板€煎鍔?,
                Parameter = "ExampleFloat",
                Hotkey = "Ctrl+Alt+Up",
                Type = "float",
                Mode = "increment",
                Value = "0",
                OffValue = "0",
                Step = 0.1,
                Min = 0,
                Max = 1
            }
        }
    };
}
