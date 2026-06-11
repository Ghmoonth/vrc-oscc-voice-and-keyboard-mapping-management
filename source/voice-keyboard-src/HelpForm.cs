using System.Diagnostics;

namespace OSCC;

public sealed class HelpForm : Form
{
    private const string SponsorUrl = "https://www.ifdian.net/a/227722xx?utm_source=copylink&utm_medium=link";

    public HelpForm()
    {
        Text = "甯姪";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 600);
        BackColor = Color.White;
        BuildUi();
        UiHelpers.ApplyControlStyle(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Color.White, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.White };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(BuildAvatar(), 0, 0);
        header.Controls.Add(new Label
        {
            Text = "OSCC 甯姪\n鍒涗綔鑰咃細鐧界煶",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White,
            Font = new Font(Control.DefaultFont.FontFamily, 14, FontStyle.Bold)
        }, 1, 0);
        root.Controls.Add(header, 0, 0);

        var text = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Text =
                "LLM锛氬ぇ璇█妯″瀷锛岀敤鏉ョ悊瑙ｈ闊虫剰鍥俱€佸垽鏂槸鍚﹁Е鍙戝弬鏁帮紝鎴栨妸璇嗗埆鏂囨湰缈昏瘧鎴愮洰鏍囪瑷€銆俓r\n\r\n" +
                "ASR锛氳闊宠瘑鍒ā鍨嬶紝鐢ㄦ潵鎶婇害鍏嬮銆佹壃澹板櫒鎴栫▼搴忓０闊宠瘑鍒垚鏂囧瓧銆侫I 缈昏瘧妯″潡閲岀殑 ASR 棰勮鍋忓悜缈昏瘧杈撳叆銆俓r\n\r\n" +
                "VAD 闃堝€硷細璇煶娲诲姩妫€娴嬬伒鏁忓害銆傛暟鍊艰秺浣庤秺瀹规槗璁や负鏈変汉璇磋瘽锛屽お浣庡彲鑳芥妸鍣０褰撲汉澹帮紱鏁板€艰秺楂樿秺涓嶅鏄撹Е鍙戯紝澶珮鍙兘婕忓瓧銆俓r\n\r\n" +
                "浜哄０寮€濮嬶細杩炵画妫€娴嬪埌浜哄０澶氫箙鍚庡紑濮嬪綍鍒朵竴鍙ヨ瘽銆傚崟浣嶄负 ms銆傚缓璁?150-250ms銆俓r\n\r\n" +
                "闈欓煶缁撴潫锛氬綍鍒朵腑杩炵画闈欓煶澶氫箙鍚庤涓轰竴鍙ヨ瘽缁撴潫銆傚崟浣嶄负 ms銆傚缓璁?700-1000ms銆俓r\n\r\n" +
                "鍓嶇疆缂撳瓨锛氱湡姝ｅ紑濮嬪綍鍒跺墠淇濈暀鐨勯煶棰戦暱搴︼紝鐢ㄦ潵闃叉寮€澶存紡瀛椼€傚崟浣嶄负 ms銆傚缓璁?300-700ms銆俓r\n\r\n" +
                "鍒涗綔鑰咃細鐧界煶"
        };
        root.Controls.Add(text, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.White, Padding = new Padding(0, 12, 0, 0) };
        buttons.Controls.Add(Button("鍏抽棴", () => Close()));
        buttons.Controls.Add(Button("璧炲姪閾炬帴", OpenSponsor));
        root.Controls.Add(buttons, 0, 2);
    }

    private Control BuildAvatar()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "creator_avatar.jpg");
        var box = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        try
        {
            if (File.Exists(path)) box.Image = Image.FromFile(path);
        }
        catch
        {
            box.Dispose();
            return new Label { Text = "鐧界煶", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White, BorderStyle = BorderStyle.None };
        }
        return box;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 92, Height = 30, Margin = new Padding(8, 0, 0, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private static void OpenSponsor()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = SponsorUrl,
            UseShellExecute = true
        });
    }
}
