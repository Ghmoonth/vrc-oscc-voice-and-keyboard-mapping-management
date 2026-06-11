using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OSCC;

public sealed class OscClient : IDisposable
{
    private readonly UdpClient udp = new();
    private string host = "127.0.0.1";
    private int port = 9000;
    private IPEndPoint endpoint = new(IPAddress.Loopback, 9000);

    public OscClient(string host, int port)
    {
        UpdateTarget(host, port);
    }

    public void UpdateTarget(string newHost, int newPort)
    {
        var normalizedHost = string.IsNullOrWhiteSpace(newHost) ? "127.0.0.1" : newHost.Trim();
        if (host == normalizedHost && port == newPort)
        {
            return;
        }

        host = normalizedHost;
        port = newPort;
        endpoint = ResolveEndpoint(host, port);
    }

    public void Send(string parameter, object value, string type)
    {
        var packet = BuildMessage(NormalizeParameter(parameter), value, type);
        udp.Send(packet, packet.Length, endpoint);
    }

    public void SendChatbox(string text, bool sendImmediately = true, bool notify = false)
    {
        var packet = BuildChatboxMessage(text, sendImmediately, notify);
        udp.Send(packet, packet.Length, endpoint);
    }

    private static IPEndPoint ResolveEndpoint(string host, int port)
    {
        var addresses = Dns.GetHostAddresses(host);
        var target = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        return new IPEndPoint(target, port);
    }

    private static string NormalizeParameter(string value)
    {
        value = value.Trim().Replace("\\", "/");
        return value.StartsWith('/') ? value : "/avatar/parameters/" + value;
    }

    private static byte[] BuildMessage(string address, object value, string type)
    {
        using var stream = new MemoryStream();
        WriteString(stream, address);
        switch (type)
        {
            case "bool":
                WriteString(stream, Convert.ToBoolean(value) ? ",T" : ",F");
                break;
            case "int":
                WriteString(stream, ",i");
                WriteBigEndian(stream, BitConverter.GetBytes(Convert.ToInt32(value)));
                break;
            case "float":
                WriteString(stream, ",f");
                WriteBigEndian(stream, BitConverter.GetBytes(Convert.ToSingle(value)));
                break;
            case "string":
                WriteString(stream, ",s");
                WriteString(stream, Convert.ToString(value) ?? "");
                break;
            default:
                throw new InvalidOperationException("Unsupported OSC type: " + type);
        }
        return stream.ToArray();
    }

    private static byte[] BuildChatboxMessage(string text, bool sendImmediately, bool notify)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "/chatbox/input");
        WriteString(stream, ",s" + (sendImmediately ? "T" : "F") + (notify ? "T" : "F"));
        WriteString(stream, text);
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value)
    {
        stream.Write(Encoding.UTF8.GetBytes(value));
        stream.WriteByte(0);
        while (stream.Length % 4 != 0)
        {
            stream.WriteByte(0);
        }
    }

    private static void WriteBigEndian(Stream stream, byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        stream.Write(bytes);
    }

    public void Dispose() => udp.Dispose();
}
