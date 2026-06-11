using System.Runtime.InteropServices;

namespace OSCC;

public sealed class NumericWheelBlocker : IMessageFilter
{
    private const int WmMouseWheel = 0x020A;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmMouseWheel) return false;
        var point = Cursor.Position;
        var handle = WindowFromPoint(point);
        if (handle == IntPtr.Zero) return false;
        var control = Control.FromChildHandle(handle);
        while (control is not null)
        {
            if (ShouldBlockWheel(control)) return true;
            control = control.Parent;
        }
        return false;
    }

    private static bool ShouldBlockWheel(Control control)
    {
        return control is NumericUpDown
            or ComboBox
            or ListBox
            or CheckedListBox
            or DataGridView;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
}
