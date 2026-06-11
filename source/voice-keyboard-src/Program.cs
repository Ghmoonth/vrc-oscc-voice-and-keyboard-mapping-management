namespace OSCC;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.AddMessageFilter(new NumericWheelBlocker());
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            var log = Path.Combine(AppContext.BaseDirectory, "oscc_startup_error.log");
            File.WriteAllText(log, ex.ToString());
            MessageBox.Show("OSCC 鍚姩澶辫触锛岄敊璇凡淇濆瓨鍒?\n" + log + "\n\n" + ex.Message, "OSCC 鍚姩澶辫触", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
