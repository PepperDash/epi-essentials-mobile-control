using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials
{
    public class Response : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            base.OnOpen();
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
        }
    }

    public class MobileControlWebsocketServer : EssentialsDevice
    {
        private HttpServer _server;

        public MobileControlWebsocketServer(string key, int customPort)
            :base(key)
        {
            // Set the default port to be 50000 plus the slot number of the program
            int port = 50000 + (int)Global.ControlSystem.ProgramNumber;

            if (customPort != 0)
            {
                port = customPort;
            }

            _server = new HttpServer(port, true);

            _server.OnGet += _server_OnGet;

            _server.AddWebSocketService<Response>("/");

            _server.Start();

            if(_server.IsListening)
            {
                Debug.Console(0, this, "WebSocket Server lisening on port: {0}", _server.Port);
            }

        }

        private void _server_OnGet(object sender, HttpRequestEventArgs e)
        {
            var req = e.Request;
            var res = e.Response;

            var path = req.RawUrl;

            if (path == "/join")
            {
                // TODO: build request with config
            }
        }

        public void StopServer()
        {
            _server.Stop();
        }


    }
}
