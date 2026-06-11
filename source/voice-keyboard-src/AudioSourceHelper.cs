using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace OSCC;

public static class AudioSourceHelper
{
    public static readonly string[] SourceTypes = { "闊抽鏂囦欢澶?, "楹﹀厠椋?, "鎵０鍣ㄥ洖鐜?, "绋嬪簭澹伴煶" };

    public static string[] GetInputDevices()
    {
        var result = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            result.Add(caps.ProductName);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
    }

    public static string[] GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }

    public static string[] GetAudioProcesses()
    {
        return Process.GetProcesses()
            .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
            .Select(p => p.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }

}
