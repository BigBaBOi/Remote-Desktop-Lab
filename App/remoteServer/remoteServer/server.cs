using System;
using System.Net;
using System.Windows.Forms;
using remoteServer.Network;

namespace remoteServer
{
    public partial class server : Form
    {
        private AsyncTcpListener _listener;
        private System.ComponentModel.IContainer components = null;

        public server()
        {
            InitializeComponent();
            Load += Server_Load;
        }

        private void Server_Load(object sender, EventArgs e)
        {
            // Auto-start listener on load
            _listener = new AsyncTcpListener(IPAddress.Any, 8888);
            _listener.Start();
            Text = "Remote Server Running on Port 8888";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 200);
            this.Text = "Server";
        }
    }
}
