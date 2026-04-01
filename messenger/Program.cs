using System.Text.RegularExpressions;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.DirectoryServices;
using System.Net;

class RobloxLogHelper
{
    private static string logsRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Roblox",
        "logs"
    );

    public static string[] GetLogs()
    {
        if (!Directory.Exists(logsRoot))
            throw new Exception("Roblox logs folder not found!");

        var logFiles = new DirectoryInfo(logsRoot)
            .GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => f.FullName)
            .ToArray();

        //logFiles[0].FullName];

        return logFiles;
    }

    public static string GetUserId() 
    {
        string[] logFiles = GetLogs();
        Regex userIdRegex = new Regex(@"userid:(\d+)", RegexOptions.IgnoreCase);
        
        for (int i = 0; i < logFiles.Length; i++)
        {
            var allLines = new List<string>();

            using var stream = new FileStream(logFiles[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
                allLines.Add(reader.ReadLine()!);

            string[] lines = allLines.ToArray();
            for (int j = lines.Length - 1; j >= 0; j--)
            {
                Match match = userIdRegex.Match(lines[j]);
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }

        return null; // nothing found
    }


}
class OverlayForm : Form
{
    ClientWebSocket ws = new ClientWebSocket();
    RichTextBox chatBox;
    TextBox inputBox;
    Button sendBtn;

    static HttpClient client = new HttpClient();
    static string userID;
    static string userName;
    static string userDisplayName;
    static Random rng = new Random();
    static string randoColor = $"#{rng.Next(0x1000000):X6}";

    public OverlayForm()
    {
        // Window setup
        TopMost = true;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.White;
        Padding = new Padding(1);
        Opacity = 0.83;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(50, 50);
        Size = new Size(500, 400);
        

        bool dragging = false;
        // Chat display
        chatBox = new RichTextBox
        {
            //Dock = DockStyle.Fill // MESSED WITH SIZING,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 13),
            TabStop = false,
        };
        Point dragStart = chatBox.Location;
        chatBox.MouseDown += (s, e) => {
            dragging = true; 
            dragStart = e.Location;
        };
        chatBox.MouseUp += (s, e) => {
            if (chatBox.SelectedText.Length == 0)
            {
                //inputBox.Focus(); // only refocus if they didn't select anything
                dragging = false;
            }
        };
        chatBox.MouseMove += (s, e) => {
            if (dragging)
            {
                Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y);
            }
        };

        // Input box
        inputBox = new TextBox
        {
            //Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 12),
            Multiline = true,
            WordWrap = true,
            ScrollBars = ScrollBars.None
        };
        inputBox.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Enter) {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SendMessage();
            }
        };
        inputBox.Text = "Press ` to chat";
        inputBox.GotFocus += (s, e) => {
            if (inputBox.Text == "Press ` to chat") {
                inputBox.Text = "";
                inputBox.ForeColor = Color.White;
            }
        };
        inputBox.LostFocus += (s, e) => {
            if (string.IsNullOrWhiteSpace(inputBox.Text)) {
                inputBox.Text = "Press ` to chat";
                inputBox.ForeColor = Color.Gray;
            }
        };

        // Send button
        sendBtn = new Button
        {
            Text = "Send",
            Dock = DockStyle.Right,
            Width = 70,
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        sendBtn.Click += (s, e) => SendMessage();

        // Bottom panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        bottomPanel.Resize += (s, e) =>
        {
            inputBox.Width = bottomPanel.Width - 75;
            sendBtn.Location = new Point(bottomPanel.Width - 70, 0);
            sendBtn.Height = bottomPanel.Height;
            inputBox.Height = bottomPanel.Height;
        };
        bottomPanel.Controls.Add(inputBox);
        bottomPanel.Controls.Add(sendBtn);

        var closeBtn = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 19),
            Size = new Size(28, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Red,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (s, e) => {
            Environment.Exit(0);
        };

        Controls.Add(closeBtn);
        Controls.Add(bottomPanel);
        Controls.Add(chatBox);

        Resize += (s, e) => {
            closeBtn.Location = new Point(this.Width - 36, 4);
            chatBox.Location = new Point(0, 0);
            chatBox.Size = new Size(ClientSize.Width, ClientSize.Height - bottomPanel.Height);
        };
        closeBtn.Location = new Point(this.Width - 36, 4);
        closeBtn.BringToFront();

        //inputBox.Select();
        sendBtn.Select();

        // Connect WebSocket after form completely loaded
        this.Load += async (s, e) => {
            chatBox.Location = new Point(0, 0);
            chatBox.Size = new Size(ClientSize.Width, ClientSize.Height - bottomPanel.Height);
            await ConnectAsync();
        };
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    IntPtr prevWindow;

    // pressing tilde to open it
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(this.Handle, 1, 0, 0xC0); // 0xC0 = backtick VK code
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1)
        {
            prevWindow = GetForegroundWindow();
            ShowWindow(this.Handle, 9);
            SetForegroundWindow(this.Handle);
            inputBox.Focus();
        }
        base.WndProc(ref m);
    }
    // --------------------------------------------------

    async Task setupStatics()
    {
        userID = RobloxLogHelper.GetUserId();
        string userInfoReq = $"https://users.roblox.com/v1/users/{userID}";

        string userInfoData = await client.GetStringAsync(userInfoReq);

        using JsonDocument userJson = JsonDocument.Parse(userInfoData);

        userDisplayName = userJson.RootElement.GetProperty("displayName").GetString();
        userName = userJson.RootElement.GetProperty("name").GetString();
    }

    async Task ConnectAsync()
    {
        await setupStatics();

        await ws.ConnectAsync(new Uri("ws://localhost:8080"), CancellationToken.None);
        // await ws.ConnectAsync(new Uri("ws://[ip]:8080"), CancellationToken.None);


        // print ur arrival locally and then when copnnecting to websocket send hello
        AppendMessage($"Connected to chat as {userDisplayName} (@{userName}) | {userID}");
        
        _ = ReceiveLoop();
    }

    async Task ReceiveLoop()
    {
        // announce arrival:
        var data = new {
            text = "has joined the chat",
            username = $"{userDisplayName} (@{userName})",
            color = randoColor
        };
        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            var raw = Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            using var json = JsonDocument.Parse(raw);
            string username = json.RootElement.GetProperty("username").GetString();
            string text = json.RootElement.GetProperty("text").GetString();
            string color = json.RootElement.GetProperty("color").GetString();

            AppendColoredMessage(username, text, color);
        }
    }

    async void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(inputBox.Text)) return;
        var msg = $"{inputBox.Text}";
        inputBox.Clear();
        
        var data = new {
            text = msg,
            username = userDisplayName,
            color = randoColor
        };
        string json = JsonSerializer.Serialize(data);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
        SetForegroundWindow(prevWindow);
    }

    void AppendMessage(string msg) // client sided message print.
    {
        if (chatBox.InvokeRequired)
            chatBox.Invoke(() => chatBox.AppendText(msg + "\n"));
        else
            chatBox.AppendText(msg + "\n");
    }

    void AppendColoredMessage(string username, string text, string hexColor)
    {
        Action append = () => {
            chatBox.SelectionStart = chatBox.TextLength;
            chatBox.SelectionLength = 0;
            chatBox.SelectionColor = ColorTranslator.FromHtml(hexColor);
            chatBox.AppendText(username + ": ");
            chatBox.SelectionColor = Color.White;
            chatBox.AppendText(text + "\n");
            chatBox.SelectionStart = chatBox.TextLength;
            chatBox.ScrollToCaret();
        };

        if (chatBox.InvokeRequired)
            chatBox.Invoke(append);
        else
            append();
    }

}

class Program
{
    static async Task Main()
    {
        Application.Run(new OverlayForm());
    }
}
