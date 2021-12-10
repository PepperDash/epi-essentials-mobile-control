using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;
using WebSocketSharp.Net;
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

        public JoinToken Token { get; set; }

        public UiClient()
        {

        }

        protected override void OnOpen()
        {
            base.OnOpen();

            var uri = Context.RequestUri;

            //_token = 

            // TODO: Future: Check token to see if there's already an open session using that token and reject/close the session 
        }

        public void BroadcastMessage(string message)
        {
            Sessions.Broadcast(message);
        }

        public void SendMessageToSession(string clientId, string message)
        {
            IWebSocketSession session;

            if (Sessions.TryGetSession(clientId, out session))
            {
                if (session.State == WebSocketState.Open)
                {
                    session.Context.WebSocket.Send(message);
                }
            }
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
        //private Dictionary<string, JoinToken> _joinTokens;

        private HttpServer _server;

        private Dictionary<string, UiClient> _uiClients;

        private MobileControlSystemController _parent;

        private int _previousClientId = 0;

        private int _port;

        private string _wsPath = "/mc/api/ui/join/";

        private string _appPath = string.Format("{0}/mcUserApp/", Global.FilePathPrefix);

        public MobileControlWebsocketServer(string key, int customPort, MobileControlSystemController parent)
            : base(key)
        {
            _parent = parent;

            // Set the default port to be 50000 plus the slot number of the program
            _port = 50000 + (int)Global.ControlSystem.ProgramNumber;

            if (customPort != 0)
            {
                _port = customPort;
            }

            _uiClients = new Dictionary<string, UiClient>();

            //_joinTokens = new Dictionary<string, JoinToken>();

            CrestronConsole.AddNewConsoleCommand(GenerateClientToken, "MobileAddUiClient", "Adds a client and generates a token. ? for more help", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(RemoveToken, "MobileRemoveUiClient", "Removes a client. ? for more help", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand((s) => PrintClientInfo(), "MobileGetClientInfo", "Displays the current client info", ConsoleAccessLevelEnum.AccessOperator);
        }

        public override void Initialize()
        {
            base.Initialize();

            _server = new HttpServer(_port, false);

            _server.OnGet += _server_OnGet;

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;

            _server.Start();

            if (_server.IsListening)
            {
                Debug.Console(0, this, "Mobile Control WebSocket Server lisening on port: {0}", _server.Port);
            }
        }

        /// <summary>
        /// Generates a new token based on validating a room key and grant code passed in.  If valid, returns a token and adds a service to the server for that token's path
        /// </summary>
        /// <param name="s"></param>
        private void GenerateClientToken(string s)
        {
            if (s == "?")
            {
                CrestronConsole.ConsoleCommandResponse(@"[RoomKey] [GrantCode] Validates the room key against the grant code and returns a token for use in a UI client");
                return;
            }

            var values = s.Split(' ');
            var roomKey = values[0];
            var grantCode = values[1];

            // TODO: Authenticate grant code passed in
            // For now, we just generate a random guid as the token and use it as the ClientId as well

            var bridge = _parent.GetRoomBridge(roomKey);
            if (bridge != null)
            {
                var guid = Guid.NewGuid().ToString();

                var token = new JoinToken();
                token.RoomKey = roomKey;
                token.Uuid = _parent.SystemUuid;
                token.Code = bridge.UserCode;
                token.ClientId = guid;

                var path = _wsPath + guid;

                _server.AddWebSocketService<UiClient>(path, () => new UiClient() { Controller = _parent, Token = token });

                // TODO: Solve the issue with not having access to the UiClient object

                Debug.Console(0, this, "Added new WebSocket UiClient service at path: {0}", path);

                Debug.Console(2, this, "{0} websocket services present", _server.WebSocketServices.Count);

                CrestronConsole.ConsoleCommandResponse(string.Format("Token: {0}", guid));

                //{
                //    c = new UiClient() { Controller = _parent, Token = token };
                //    //c.Controller = _parent;
                //    //c.Token = token;
                //    _uiClients.Add(guid, c);
                //    Debug.Console(0, this, "Added new WebSocket UiClient service at path: {0}", path);

                //    CrestronConsole.ConsoleCommandResponse(string.Format("Token: {0}", guid));
                //});
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find room with key: {0}", roomKey));
            }
        }

        /// <summary>
        /// Removes a client with the specified token value
        /// </summary>
        /// <param name="s"></param>
        private void RemoveToken(string s)
        {
            if (s == "?")
            {
                CrestronConsole.ConsoleCommandResponse(@"[token] Removes the client with the specified token value");
                return;
            }

            UiClient uiClient;

            if (_uiClients.TryGetValue(s, out uiClient))
            {
                if (uiClient.Context.WebSocket.IsAlive)
                {
                    uiClient.Context.WebSocket.Close();
                }

                var path = _wsPath + uiClient.Token.ClientId;
                if (_server.RemoveWebSocketService(path))
                {
                    _uiClients.Remove(s);

                    CrestronConsole.ConsoleCommandResponse(string.Format("Client removed with token: {0}", s));
                }
                else
                {
                    CrestronConsole.ConsoleCommandResponse(string.Format("Unable to remove client with token : {0}", s));
                }
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find client with token: {0}", s));
            }
        }

        private void PrintClientInfo()
        {
            CrestronConsole.ConsoleCommandResponse("Mobile Control UI Client Info:");

            CrestronConsole.ConsoleCommandResponse(string.Format("{0} clients found:", _uiClients.Count));

            foreach (var client in _uiClients.Values)
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("RoomKey: {0} Token: {1}", client.Token.RoomKey, client.Token.ClientId));
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
            try
            {
                var req = e.Request;
                var res = e.Response;

                var path = req.RawUrl;

                Debug.Console(2, this, "Request received at path: {0}", path);

                if (path.Contains("/mc/api/ui/joinroom"))
                {
                    var qp = req.QueryString;
                    var token = qp["token"];

                    Debug.Console(2, this, "Join Room Request with token: {0}", token);

                    UiClient client = null;

                    if (_uiClients.TryGetValue(token, out client))
                    {
                        var room = _parent.GetRoomBridge(client.Token.RoomKey);

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
                            jRes.RoomUuid = _parent.SystemUuid;
                            jRes.Config = ConfigReader.ConfigObject;
                            jRes.CodeExpires = new DateTime();
                            jRes.UserCode = room.UserCode;
                            jRes.UserAppUrl = string.Format("http://{0}:{1}/mc/app",
                                CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                                _port);
                            jRes.EnableDebug = false;

                            // Serialize to JSON and convert to Byte[]
                            var json = JsonConvert.SerializeObject(jRes);
                            var body = Encoding.UTF8.GetBytes(json);
                            res.ContentLength64 = body.LongLength;

                            // Send the response
                            res.Close(body, false);
                        }
                        else
                        {
                            var message = string.Format("Unable to find bridge with key: {0}", client.Token.RoomKey);
                            res.StatusCode = 404;
                            res.ContentEncoding = Encoding.UTF8;
                            res.ContentType = "application/json";
                            var body = Encoding.UTF8.GetBytes(message);
                            res.ContentLength64 = body.LongLength;
                            res.Close(body, false);
                            Debug.Console(2, this, "{0}", message);
                        }
                    }
                    else
                    {
                        var message = "Token invalid or has expired";
                        res.StatusCode = 401;
                        res.ContentEncoding = Encoding.UTF8;
                        res.ContentType = "application/json";
                        Debug.Console(2, this, "{0}", message);
                        var body = Encoding.UTF8.GetBytes(message);
                        res.ContentLength64 = body.LongLength;
                        res.Close(body, false);
                    }
                }
                else if (path.Contains("/mc/api/version"))
                {
                    res.StatusCode = 200;
                    res.ContentEncoding = Encoding.UTF8;
                    res.ContentType = "application/json";
                    var version = new Version() { ServerVersion = "3.0.0" };
                    var message = JsonConvert.SerializeObject(version);
                    Debug.Console(2, this, "{0}", message);

                    var body = Encoding.UTF8.GetBytes(message);
                    res.ContentLength64 = body.LongLength;
                    res.Close(body, false);
                }
                else if (path.Contains("/mc/app"))
                {
                    string filePath = _appPath;

                    if (path == "/mc/app")
                    {
                        filePath = _appPath + "/index.html";
                    }

                    if (path.EndsWith(".html"))
                    {
                        res.ContentType = "text/html";
                        res.ContentEncoding = Encoding.UTF8;
                        filePath = _appPath + "/index.html";
                    }
                    else if (path.EndsWith(".js"))
                    {
                        res.ContentType = "application/javascript";
                        res.ContentEncoding = Encoding.UTF8;
                        // TODO: Replace URL path prefix with actual path prefix
                    }

                    byte[] contents;
                    if (!e.TryReadFile(filePath, out contents))
                    {
                        res.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    res.ContentLength64 = contents.LongLength;
                    res.Close(contents, true);
                }
                else
                {
                    // All 
                    res.StatusCode = 404;
                    res.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnGet handler {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
            }
        }

        public void StopServer()
        {
            _server.Stop(CloseStatusCode.Normal, "Server Shutting Down");
        }

        /// <summary>
        /// Sends a message to all connectd clients
        /// </summary>
        /// <param name="message"></param>
        public void SendMessageToAllClients(string message)
        {
            foreach (var behavior in _uiClients)
            {
                if (behavior.Value.Context.WebSocket.IsAlive)
                {
                    behavior.Value.Context.WebSocket.Send(message);
                }
            }
        }

        /// <summary>
        /// Sends a message to a specific client
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="message"></param>
        public void SendMessageToClient(object clientId, string message)
        {
            UiClient behavior;

            if (_uiClients.TryGetValue((string)clientId, out behavior))
            {
                var socket = behavior.Context.WebSocket;

                if (socket.IsAlive)
                {
                    socket.Send(message);
                }
            }
            else
            {
                Debug.Console(0, this, "Unable to find client with ID: {0}", clientId);
            }
        }
    }

    public class Version
    {
        [JsonProperty("serverVersion")]
        public string ServerVersion { get; set; }
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
        public string UserAppUrl { get; set; }

        [JsonProperty("enableDebug")]
        public bool EnableDebug { get; set; }

    }
}
