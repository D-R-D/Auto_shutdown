using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Auto_shutdown
{
    public partial class Form1 : Form
    {
        static ManualResetEvent connection_wait = new ManualResetEvent(false);
        bool st = false, fast = false;

        public Form1()
        {
            InitializeComponent();

            Application.ApplicationExit += new EventHandler(ApplicationExitEvent);

            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = !this.MaximizeBox;
        }
        private void ApplicationExitEvent(object? sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();

            notifyIcon1.Text = "starting...";

            Application.ApplicationExit -= new EventHandler(ApplicationExitEvent);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                tcp_listener();
            });
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            if (!fast)
            {
                this.Hide();
                fast = true;
            }
            this.Focus();
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (st == false) { e.Cancel = true; }
            this.Hide();
            GC.Collect();
        }


        //
        /*フォーム操作*/
        //
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            GC.Collect();
        }
        private void 終了ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            st = true;
            this.Close();
        }
        //
        /*フォーム操作*/
        //


        //
        /*tcp接続*/
        //
        private void tcp_listener()
        {
            int port = 23000;

            IPEndPoint IPE = new IPEndPoint(IPAddress.Any, port);
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sock.Bind(IPE);

            sock.Listen(2);

            Invoke((MethodInvoker)delegate
            {
                richTextBox1.Text += "Connection waiting... IPEndPoint : " + IPE.Address.ToString() + " : " + IPE.Port.ToString() + "\n";
                notifyIcon1.Text = "tcp listener[port:23000] started!!";
            });

            while (true)
            {
                connection_wait.Reset();
                sock.BeginAccept(new AsyncCallback(AcceptCallback), sock);
                connection_wait.WaitOne();
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            connection_wait.Set();

            var sock = (Socket)ar.AsyncState;
            var socket = sock.EndAccept(ar);

            Invoke((MethodInvoker)delegate
            {
                richTextBox1.Text += "client connected : " + socket.RemoteEndPoint + "\n";
            });

            StateObj ln = new StateObj();
            ln.Socket = socket;

            socket.BeginReceive(ln.buffer, 0, ln.buffer.Length, 0, new AsyncCallback(ReceiveCallback), ln);
        }
        private void ReceiveCallback(IAsyncResult ar)
        {
            StateObj ln = (StateObj)ar.AsyncState;
            Socket sock = ln.Socket;

            int bytesize = 0;

            try
            {
                bytesize = sock.EndReceive(ar);
            }
            catch (Exception ex)
            {
                Invoke((MethodInvoker)delegate
                {
                    richTextBox1.Text += ex.Message + "\n";
                });
            }

            if(bytesize <= 0)
            {
                sock.Close();
                return;
            }

            string str = Encoding.UTF8.GetString(ln.buffer, 0, bytesize);

            Invoke((MethodInvoker)delegate
            {
                if (str == "power_off_signal\n")
                {
                    richTextBox1.Text += "Stop signal received.\n";
                    notifyIcon1.Text = "shutdown signal received";
                    shutdowner();
                }
                else
                {
                    richTextBox1.Text += str;
                }
            });

            sock.BeginReceive(ln.buffer, 0, ln.buffer.Length, 0, new AsyncCallback(ReceiveCallback), ln);
        }
        //
        /*tcp接続*/
        //

        //
        /*シャットダウン実行*/
        //とても参考にさせていただいた素晴らしいサイト : https://kuttsun.blogspot.com/2019/11/c-os.html
        private void shutdowner()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                // コマンドライン引数を指定
                Arguments = "/s",
                // ウィンドウを表示しないようにする
                UseShellExecute = false,// シェル機能を使用しない
                CreateNoWindow = true // コンソール・ウィンドウを開かない
            };

            // 開始
            var process = Process.Start(psi);

            notifyIcon1.Text = "waiting shutdown process...";
        }


        //
        /*シャットダウン実行*/
        //
    }

    //tcpソケット用のセット
    public class StateObj
    {
        public const int buffersize = 1024;
        public Socket? Socket = null;
        public byte[] buffer = new byte[buffersize];
    }
}