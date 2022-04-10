using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommunicatorServer
{
    public partial class fCommunicatorServer : Form
    {
        private TcpListener server = null;
        private List<Thread> connections = new List<Thread>();
        private List<TcpClient> clients = new List<TcpClient>();
        private List<string> names = new List<string>();
        private IPAddress ServerAddress = null;
        private NetworkStream cstream;
        private BinaryWriter cwriter;
        private bool active = false;
        private string content = string.Empty;
        private string style = "body{font-family: \"Segoe UI\"; max-width: 100vw; word-break: break-all;}";

        public fCommunicatorServer()
        {
            InitializeComponent();
            string HostName = Dns.GetHostName();
            IPHostEntry IPAddresses = Dns.GetHostEntry(HostName);
            foreach(IPAddress IPAddress in IPAddresses.AddressList)
            {
                if (IPAddress.ToString() == "127.0.0.1")
                    lbConsole.Items.Add("This computer is not connected to any network.");
                else
                    cbxAddress.Items.Add(IPAddress.ToString());
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (btnStart.Text == "Start")
            {
                btnStart.Text = "Stop";
                cbxAddress.Enabled = false;
                nudPort.Enabled = false;
                btnSend.Enabled = true;
                active = true;

                try
                {
                    ServerAddress = IPAddress.Parse(cbxAddress.Text);
                }
                catch
                {
                    lbConsole.Items.Add("Invalid IP address format!");
                    cbxAddress.Text = String.Empty;
                    return;
                }

                lbConsole.Items.Add("Server listening on " + cbxAddress.Text + ":" + nudPort.Value + ".");
                server = new TcpListener(ServerAddress, (int)nudPort.Value);
                bwConnectionHandler.RunWorkerAsync();
            }
            else if (btnStart.Text == "Stop")
            {
                btnStart.Text = "Start";
                cbxAddress.Enabled = true;
                nudPort.Enabled = true;
                btnSend.Enabled = false;
                active = false;

                foreach(TcpClient client in clients)
                {
                    cstream = client.GetStream();
                    cwriter = new BinaryWriter(cstream);
                    cwriter.Write("SERVER_STOPPED");
                }

                foreach(Thread connection in connections)
                {
                    connection.Abort();
                }
                names.Clear();
                connections.Clear();

                server.Stop();
                clients.Clear();
                bwConnectionHandler.CancelAsync();
                lbConsole.Items.Add("Successfully stopped the server.");
            }
        }

        private void bwConnectionHandler_DoWork(object sender, DoWorkEventArgs e)
        {
            while(active)
            {
                try
                {
                    server.Start();
                    clients.Add(server.AcceptTcpClient());
                    lbConsole.Invoke(new MethodInvoker(delegate { lbConsole.Items.Add("Client is trying to connect."); }));
                    Thread connectionThread = new Thread(handleClient);
                    connections.Add(connectionThread);
                    connections.Last().Start();
                } 
                catch
                {
                    server.Stop();
                    clients.Clear();
                    bwConnectionHandler.CancelAsync();
                }
            }
        }

        private void handleClient()
        {
            TcpClient lclient = clients.Last();
            string lname = "";
            NetworkStream ns = lclient.GetStream();
            BinaryReader reading = new BinaryReader(ns);
            BinaryWriter writing = new BinaryWriter(ns);
            string pass = reading.ReadString();
            if (pass == "password")
            {
                lname = reading.ReadString();
                if (!names.Contains(lname))
                {
                    lbConsole.Invoke(new MethodInvoker(delegate { lbConsole.Items.Add("Client has connected successfully"); }));
                    tbMessage.Invoke(new MethodInvoker(delegate { tbMessage.Text = $"<b><i>{lname} has connected!</i></b>"; }));
                    btnSend.Invoke(new MethodInvoker(delegate { btnSend.PerformClick(); }));
                    names.Add(lname);
                    string messageReceived;
                    while ((messageReceived = reading.ReadString()) != "CONNECTION_CANCELLED")
                    {
                        messageReceived = $"<div>[{DateTime.Now.ToString("H:mm")}] <b>{lname}</b>: {messageReceived}</div>";
                        wbMessages.Invoke(new MethodInvoker(delegate { content += messageReceived; displayBrowserHTML(); }));
                        foreach (TcpClient client in clients)
                        {
                            cstream = client.GetStream();
                            cwriter = new BinaryWriter(cstream);
                            cwriter.Write(messageReceived);                        
                        }
                    }
                    lbConsole.Invoke(new MethodInvoker(delegate { lbConsole.Items.Add("Client has disconnected."); }));
                    tbMessage.Invoke(new MethodInvoker(delegate { tbMessage.Text = $"<b><i>{lname} has disconnected!</i></b>"; }));
                    btnSend.Invoke(new MethodInvoker(delegate { btnSend.PerformClick(); }));
                    names.Remove(lname);
                }
                else
                {
                    writing.Write("ERR_NAME_TAKEN");
                    lbConsole.Invoke(new MethodInvoker(delegate { lbConsole.Items.Add("Client chose an existing name."); }));
                }
                clients.Remove(lclient);
            }
            else
            {
                writing.Write("ERR_INVALID_PASSWORD");
                lbConsole.Invoke(new MethodInvoker(delegate { lbConsole.Items.Add("Client has given an invalid password."); }));
                clients.Remove(lclient);
                return;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (tbMessage.Text != String.Empty)
            {
                string outputMessage = $"<div>[{DateTime.Now.ToString("H:mm")}] <b>Server</b>: {tbMessage.Text}</div>";
                content += outputMessage;
                displayBrowserHTML();
                foreach (TcpClient client in clients)
                {
                    cstream = client.GetStream();
                    cwriter = new BinaryWriter(cstream);
                    cwriter.Write(outputMessage);
                }
                tbMessage.Text = String.Empty;
            }
            this.ActiveControl = tbMessage;
        }

        private void tbMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend.PerformClick();
                e.SuppressKeyPress = true;
            }
        }

        private void btnBold_Click(object sender, EventArgs e)
        {
            int selectionStart = tbMessage.SelectionStart;
            int selectionEnd = selectionStart + tbMessage.SelectionLength + 3;

            tbMessage.Text = tbMessage.Text.Insert(selectionStart, "<b>");
            tbMessage.Text = tbMessage.Text.Insert(selectionEnd, "</b>");
            tbMessage.SelectionStart = selectionEnd;
            tbMessage.Focus();
        }

        private void btnItalic_Click(object sender, EventArgs e)
        {
            int selectionStart = tbMessage.SelectionStart;
            int selectionEnd = selectionStart + tbMessage.SelectionLength + 3;

            tbMessage.Text = tbMessage.Text.Insert(selectionStart, "<i>");
            tbMessage.Text = tbMessage.Text.Insert(selectionEnd, "</i>");
            tbMessage.SelectionStart = selectionEnd;
            tbMessage.Focus();
        }

        private void btnStrike_Click(object sender, EventArgs e)
        {
            int selectionStart = tbMessage.SelectionStart;
            int selectionEnd = selectionStart + tbMessage.SelectionLength + 3;

            tbMessage.Text = tbMessage.Text.Insert(selectionStart, "<s>");
            tbMessage.Text = tbMessage.Text.Insert(selectionEnd, "</s>");
            tbMessage.SelectionStart = selectionEnd;
            tbMessage.Focus();
        }

        private void btnUnderline_Click(object sender, EventArgs e)
        {
            int selectionStart = tbMessage.SelectionStart;
            int selectionEnd = selectionStart + tbMessage.SelectionLength + 3;

            tbMessage.Text = tbMessage.Text.Insert(selectionStart, "<u>");
            tbMessage.Text = tbMessage.Text.Insert(selectionEnd, "</u>");
            tbMessage.SelectionStart = selectionEnd;
            tbMessage.Focus();
        }

        private void displayBrowserHTML()
        {
            if (wbMessages.Document == null)
            {
                wbMessages.Navigate("about:blank");
                wbMessages.Document.OpenNew(true).Write($"<html><head><style>{style}</style></head><body>{content}</body></html>");
            }
            else
            {
                wbMessages.Document.OpenNew(false).Write($"<html><head><style>{style}</style></head><body>{content}</body></html>");
            }
            if (wbMessages.Document.Body != null)
            {
                wbMessages.Document.Window.ScrollTo(0, wbMessages.Document.Body.ScrollRectangle.Height);
            }
            this.ActiveControl = tbMessage;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog chatSaveDialog = new SaveFileDialog();
            chatSaveDialog.FileName = $"chatlog-{DateTime.Now.Ticks}";
            chatSaveDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            if (chatSaveDialog.ShowDialog() == DialogResult.OK)
            {
                string log = "Chat saved on: " + DateTime.Now + Environment.NewLine;
                log += Regex.Replace(Regex.Replace(content, "</div>", Environment.NewLine), "<.*?>", string.Empty);
                if (chatSaveDialog.FileName != string.Empty)
                    File.WriteAllText(chatSaveDialog.FileName, log);
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            content = string.Empty;
            displayBrowserHTML();
        }

        private void fCommunicatorServer_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (TcpClient client in clients)
            {
                cstream = client.GetStream();
                cwriter = new BinaryWriter(cstream);
                cwriter.Write("SERVER_STOPPED");
            }

            foreach (Thread connection in connections)
            {
                connection.Abort();
            }
            names.Clear();
            connections.Clear();

            if (server != null)
                server.Stop();
            clients.Clear();
            bwConnectionHandler.CancelAsync();
        }
    }
}
