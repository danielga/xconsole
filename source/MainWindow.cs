using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace xconsole
{
    public partial class MainWindow : Form
    {
        private const int BUFFER_SIZE = 8192;
        private readonly byte[] buffer = new byte[BUFFER_SIZE];
        private Thread thread = null;
        private bool newline = true;
        private readonly string[] SpewTypeString = {
            "Message: ",
            "Warning: ",
            "Assert: ",
            "Error: ",
            "Log: "
        };

        public static void Main()
        {
            MainWindow window = new();
            window.ShowDialog();
        }

        public MainWindow()
        {
            InitializeComponent();
            Shown += MainWindow_Shown;
            FormClosing += MainWindow_FormClosing;
        }

        private delegate void SetConnectionStatusDelegate(bool connected);
        private void SetConnectionStatus(bool connected)
        {
            if (connected)
                connectionStatus.Text = "Connected";
            else
                connectionStatus.Text = "Disconnected";
        }

        private static string ReadString(BinaryReader reader)
        {
            List<byte> list = new();
            byte ch;
            while ((ch = reader.ReadByte()) != 0)
                list.Add(ch);

            return Encoding.UTF8.GetString(list.ToArray());
        }

        private void AppendText(Color color, string message)
        {
            richTextBox.SuspendPainting();

            richTextBox.SelectionStart = richTextBox.TextLength;
            richTextBox.SelectionLength = 0;

            Color oldcolor = richTextBox.SelectionColor;
            richTextBox.SelectionColor = color;
            richTextBox.AppendText(message);
            richTextBox.SelectionColor = oldcolor;

            richTextBox.ResumePainting();
        }

        private delegate void AppendMessageDelegate(
            int type,
            int level,
            string group,
            Color color,
            string message
        );
        private void AppendMessage(
            int type,
            int level,
            string group,
            Color color,
            string message
        )
        {
            if (newline)
                AppendText(Color.Black, SpewTypeString[type]);

            newline = message.EndsWith("\n");
            AppendText(color, message);
        }

        private void ClientThread()
        {
            try
            {
                using NamedPipeClientStream pipe = new(
                    ".",
                    "garrysmod_console",
                    PipeDirection.In,
                    PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Anonymous,
                    HandleInheritability.None
                );
                while (true)
                {
                    pipe.Connect();

                    Invoke(new SetConnectionStatusDelegate(SetConnectionStatus), true);

                    while (pipe.IsConnected)
                    {
                        pipe.ReadMode = PipeTransmissionMode.Message;

                        int read = pipe.Read(buffer, 0, BUFFER_SIZE);
                        if (read == 0)
                            continue;

                        using BinaryReader reader = new(
                            new MemoryStream(buffer, 0, read)
                        );
                        int type = reader.ReadInt32();
                        int level = reader.ReadInt32();
                        string group = ReadString(reader);
                        Color color = Color.FromArgb(
                            reader.ReadByte(),
                            reader.ReadByte(),
                            reader.ReadByte()
                        );
                        reader.ReadByte(); // alpha doesn't work on the RichTextBox
                        string msg = ReadString(reader);
                        Invoke(
                            new AppendMessageDelegate(AppendMessage),
                            type,
                            level,
                            group,
                            color,
                            msg
                        );
                    }

                    Invoke(new SetConnectionStatusDelegate(SetConnectionStatus), false);
                }
            }
            catch (ThreadInterruptedException)
            { }
        }

        private void MainWindow_Shown(Object sender, EventArgs e)
        {
            thread = new(ClientThread);
            thread.Name = "Console messages receiver";
            thread.Start();
        }

        private void MainWindow_FormClosing(Object sender, FormClosingEventArgs e)
        {
            thread.Interrupt();
        }
    }

    public static class RichTextBoxExtensions
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, Int32 wMsg, Int32 wParam, ref Point lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, Int32 wMsg, Int32 wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x0b;
        private const int WM_USER = 0x400;
        private const int EM_GETEVENTMASK = WM_USER + 59;
        private const int EM_SETEVENTMASK = WM_USER + 69;
        private const int EM_GETSCROLLPOS = WM_USER + 221;
        private const int EM_SETSCROLLPOS = WM_USER + 222;

        private static Point _ScrollPoint;
        private static IntPtr _EventMask;
        private static int _SuspendIndex = 0;
        private static int _SuspendLength = 0;

        public static void SuspendPainting(this RichTextBox rtb)
        {
            _SuspendIndex = rtb.SelectionStart;
            _SuspendLength = rtb.SelectionLength;
            SendMessage(rtb.Handle, EM_GETSCROLLPOS, 0, ref _ScrollPoint);
            SendMessage(rtb.Handle, WM_SETREDRAW, 0, IntPtr.Zero);
            _EventMask = SendMessage(rtb.Handle, EM_GETEVENTMASK, 0, IntPtr.Zero);
        }

        public static void ResumePainting(this RichTextBox rtb)
        {
            rtb.Select(_SuspendIndex, _SuspendLength);
            SendMessage(rtb.Handle, EM_SETSCROLLPOS, 0, ref _ScrollPoint);
            SendMessage(rtb.Handle, EM_SETEVENTMASK, 0, _EventMask);
            SendMessage(rtb.Handle, WM_SETREDRAW, 1, IntPtr.Zero);
            rtb.Invalidate();
        }
    }
}
