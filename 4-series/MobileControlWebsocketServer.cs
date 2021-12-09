using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

using Newtonsoft.Json;

namespace PepperDash.Essentials
{
    public class UiClient : WebSocketBehavior
    {
        public MobileControlSystemController Controller { get; set; }

        public UiClient()
        {

        }

        protected override void OnOpen()
        {
            base.OnOpen();
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);

            if (e.IsText && e.Data.Length > 0 && Controller != null)
            {
                Controller.HandleClientMessage(e.Data);
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
        }


    }

    public class MobileControlWebsocketServer : EssentialsDevice
    {
        /// <summary>
        /// Where the key is the join token and the value is the room key
        /// </summary>
        private Dictionary<string, JoinToken> _joinTokens;

        private HttpServer _server;

        private MobileControlSystemController _parent;

        private int _previousClientId = 0;

        private int _port;

        public MobileControlWebsocketServer(string key, int customPort, MobileControlSystemController parent)
            :base(key)
        {
            _parent = parent;

            // Set the default port to be 50000 plus the slot number of the program
            _port = 50000 + (int)Global.ControlSystem.ProgramNumber;

            if (customPort != 0)
            {
                _port = customPort;
            }

            _joinTokens = new Dictionary<string, JoinToken>();

            CrestronConsole.AddNewConsoleCommand(GenerateClientToken, "MobileGenerateUiClientToken", "Generates a client token. ? for more help", ConsoleAccessLevelEnum.AccessOperator);
        }

        public override void Initialize()
        {
            base.Initialize();

            _server = new HttpServer(_port, true);

            _server.OnGet += _server_OnGet;

            _server.AddWebSocketService<UiClient>("/", () => new UiClient() { Controller = _parent });

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;

            _server.Start();

            if (_server.IsListening)
            {
                Debug.Console(0, this, "WebSocket Server lisening on port: {0}", _server.Port);
            }
        }

        private void GenerateClientToken(string s)
        {
            if(s == "?")
            {
                CrestronConsole.ConsoleCommandResponse(@"[RoomKey] [GrantCode] Validates the room key against the grant code and returns a token for use in a UI client");
                return;
            }

            var values = s.Split(' ');
            var roomKey = values[0];
            var grantCode = values[1];

            // TODO: Authenticate grant code passed in

            var bridge = _parent.GetRoomBridge(roomKey);
            if (bridge != null)
            { 
                var token = new JoinToken();

                token.RoomKey = roomKey;
                token.Uuid = _parent.SystemUuid;
                token.Code = bridge.UserCode;

                var guid = Guid.NewGuid().ToString();

                _joinTokens.Add(guid, token);

                CrestronConsole.ConsoleCommandResponse(string.Format("Token: {0}", guid));
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find room with key: {0}", roomKey));
            }
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                StopServer();
            }
        }

        private int GetNextClientId()
        {
            return _previousClientId++;
        }

        private void _server_OnGet(object sender, HttpRequestEventArgs e)
        {
            var req = e.Request;
            var res = e.Response;

            var path = req.RawUrl;

            if (path == "/joinroom")
            {
                var qp = req.QueryString;
                var token = qp["token"];

                JoinToken jToken = null;

                if (_joinTokens.TryGetValue(token, out jToken))
                {
                    var room = _parent.GetRoomBridge(jToken.RoomKey);

                    if (room != null)
                    {
                        res.StatusCode = 200;
                        res.ContentEncoding = Encoding.UTF8;
                        res.ContentType = "application/json";

                        // Construct the response object
                        JoinResponse jRes = new JoinResponse();
                        jRes.ClientId = GetNextClientId();
                        jRes.RoomKey = room.RoomKey;
                        jRes.SystemUuid = _parent.SystemUuid;
                        jRes.RoomUuid = "";
                        jRes.Config = ConfigReader.ConfigObject;
                        jRes.CodeExpires = new DateTime();
                        jRes.UserCode = room.UserCode;
                        jRes.userAppUrl = string.Format("http://{0}:{1}/mc/app",
                            CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                            _port);
                        jRes.EnableDebug = false;

                        // Serialize to JSON and convert to Byte[]
                        var json = JsonConvert.SerializeObject(jRes);
                        var body = Encoding.UTF8.GetBytes(json);

                        // Send the response
                        res.Close(body, false);
                    }
                    else
                    {
                        var message = string.Format("Unable to find bridge with key: {0}", jToken.RoomKey);
                        res.StatusCode = 404;
                        res.ContentEncoding = Encoding.UTF8;
                        res.ContentType = "application/json";
                        var body = Encoding.UTF8.GetBytes(message);
                        res.Close(body, false);
                        Debug.Console(2, this, message);
                    }
                }
                else
                {
                    var message = "Token invalid or has expired";
                    res.StatusCode = 401;
                    res.ContentEncoding = Encoding.UTF8;
                    res.ContentType = "application/json";
                    var body = Encoding.UTF8.GetBytes(message);
                    res.Close(body, false);
                    Debug.Console(0, this, message);
                }
            }
            else if (path == "/mc/app")
            {
                // TODO: Return the user app
            }
        }

        public void StopServer()
        {
            _server.Stop();
        }

        public void SendMessageToAllClients(string message)
        {
            // TODO: Implement sending message as broadcast or to specific client
        }

    }

    public class JoinToken
    {
        public string Code { get; set; }
        public string RoomKey { get; set; }
        public string Uuid { get; set; }
        public string ClientId { get; set; }
    }

    public class JoinResponse
    {
        [JsonProperty("clientId")]
        public int ClientId { get; set; }

        [JsonProperty("roomKey")]
        public string RoomKey { get; set; }

        [JsonProperty("systemUUid")]
        public string SystemUuid { get; set; }

        [JsonProperty("roomUUid")]
        public string RoomUuid { get; set; }

        [JsonProperty("config")]
        public object Config { get; set; }

        [JsonProperty("codeExpires")]
        public DateTime CodeExpires { get; set; }

        [JsonProperty("userCode")]
        public string UserCode { get; set; }

        [JsonProperty("userAppUrl")]
        public string userAppUrl { get; set; }

        [JsonProperty("enableDebug")]
        public bool EnableDebug { get; set; }

    }
}
