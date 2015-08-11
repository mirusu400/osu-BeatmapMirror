﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Manager
{
    public partial class Server : ServiceBase
    {
        public Server()
        {
            InitializeComponent();
        }

        private HttpListener Listener;

        protected override void OnStart(string[] args)
        {
            // 동기화 봇
            timer1.Interval = Settings.SyncInterval;
            timer1.Start();

            // 웹소켓 서버
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://" + Settings.Prefix);
            Listener.Prefixes.Add("https://" + Settings.Prefix);
            Listener.Start();

            Listen();
        }

        private object Bot = new object();

        private void timer1_Tick(object sender, EventArgs e)
        {
            lock (Bot)
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(
                            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bot.exe"),
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                process.WaitForExit();
            }
        }

        private async void Listen()
        {
            while (Listener.IsListening)
            {
                var context = await Listener.GetContextAsync();
                if (!context.Request.IsWebSocketRequest ||
                    (Settings.TLSOnly && !context.Request.IsSecureConnection))
                {
                    if (string.IsNullOrEmpty(Settings.Fallback))
                    {
                        context.Response.StatusCode = 400;
                    }
                    else
                    {
                        context.Response.Redirect(Settings.Fallback);
                    }
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                Log.Write(wsContext.WebSocket.GetHashCode() + " AcceptWebSocketAsync");
                new Client(wsContext.WebSocket).Listen();
            }
        }

        protected override void OnStop()
        {
            Listener.Close();
            timer1.Stop();
        }
    }
}
