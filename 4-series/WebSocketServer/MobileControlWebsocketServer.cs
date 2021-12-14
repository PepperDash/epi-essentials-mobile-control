using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;
using WebSocketSharp.Net;
using Crestron.SimplSharp;
using System.Text.RegularExpressions;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PepperDash.Essentials
{
    /// <summary>
    /// Represents the behaviour to associate with a UiClient for WebSocket communication
    /// </summary>
    public class UiClient : WebSocketBehavior
    {
        public MobileControlSystemController Controller { get; set; }

        public string RoomKey { get; set; }

        public UiClient()
        {

        }

        protected override void OnOpen()
        {
            base.OnOpen();

            var url = Context.WebSocket.Url;
            Debug.Console(2, "New WebSocket Connection from: {0}", url);

            var match = Regex.Match(url.AbsoluteUri, "(?:ws|wss):\\/\\/.*(?:\\/mc\\/api\\/ui\\/join\\/)(.*)");

            if (match.Success)
            {
                var clientId = match.Groups[1].Value;

                var content = new
                {
                    clientId,
                    roomKey = RoomKey,
                };

                var clientJoined = new MobileControlResponseMessage()
                {
                    Type = "/system/clientJoined",
                    ClientId = clientId,
                    Content = content,
                };

                var msg = JsonConvert.SerializeObject(clientJoined);

                // Inform controller of client joining
                if (Controller != null)
                {
                    Controller.HandleClientMessage(msg);
                }
                else
                {
                    Debug.Console(2, "WebSocket UiClient Controller is null");
                }
            }

            // TODO: Future: Check token to see if there's already an open session using that token and reject/close the session 
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);

            if (e.IsText && e.Data.Length > 0 && Controller != null)
            {
                // Forward the message to the controller to be put on the receive queue
                Controller.HandleClientMessage(e.Data);
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);

            Debug.Console(2, "WebSocket UiClient Closing: {0} reason: {1}", e.Code, e.Reason);

        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);

            Debug.Console(2, "WebSocket UiClient Error: {0} message: {1}", e.Exception, e.Message);
        }
    }

    public class MobileControlWebsocketServer : EssentialsDevice
    {
        /// <summary>
        /// Where the key is the join token and the value is the room key
        /// </summary>
        //private Dictionary<string, JoinToken> _joinTokens;

        private HttpServer _server;

        private Dictionary<string, UiClientContext> _uiClients;

        private MobileControlSystemController _parent;

        private WebSocketServerSecretProvider _secretProvider;

        private ServerTokenSecrets _secret;

        private string _secretProviderKey
        {
            get
            {
                return string.Format("{0}:{1}-tokens", Global.ControlSystem.ProgramNumber, this.Key);
            }
        }

        /// <summary>
        /// The prot the server will run on
        /// </summary>
        private int _port;

        /// <summary>
        /// The path for the WebSocket messaging
        /// </summary>
        private string _wsPath = "/mc/api/ui/join/";

        /// <summary>
        /// The path to the location of the files for the user app (single page Angular app)
        /// </summary>
        private string _appPath = string.Format("{0}mcUserApp", Global.FilePathPrefix);

        /// <summary>
        /// The base HREF that the user app uses
        /// </summary>
        private string _userAppBaseHref = "/mc/app";

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

            _uiClients = new Dictionary<string, UiClientContext>();

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

            RetrieveSecret();
        }

        /// <summary>
        /// Attempts to retrieve secrets previously stored in memory
        /// </summary>
        private void RetrieveSecret()
        {
            // Add secret provider
            _secretProvider = new WebSocketServerSecretProvider(_secretProviderKey);

            // Check for existing secrets
            var secret = _secretProvider.GetSecret(_secretProviderKey);

            if (secret != null)
            {
                Debug.Console(2, this, "Secret successfully retrieved");

                // populate the local secrets object
                _secret = JsonConvert.DeserializeObject<ServerTokenSecrets>(secret.Value.ToString());

                // populate the _uiClient collection
                foreach (var token in _secret.Tokens)
                {
                    _uiClients.Add(token.Key, new UiClientContext(token.Value));
                }

                foreach (var client in _uiClients)
                {
                    var key = client.Key;
                    var path = _wsPath + key;
                    var roomKey = client.Value.Token.RoomKey;

                    _server.WebSocketServices.AddService<UiClient>(path, (c) =>
                    {
                        Debug.Console(2, this, "Constructing UiClient with id: {0}", key);
                        c.Controller = _parent;
                        c.RoomKey = roomKey;
                        _uiClients[key].SetClient(c);
                    });
                }
            }
            else
            {
                Debug.Console(2, this, "No secret found");
            }

            Debug.Console(2, this, "{0} UiClients restored from secrets data", _uiClients.Count);
        }

        /// <summary>
        /// Stores secrets to memory to persist through reboot
        /// </summary>
        private void UpdateSecret()
        {

            _secret.Tokens.Clear();

            foreach (var uiClientContext in _uiClients)
            {
                _secret.Tokens.Add(uiClientContext.Key, uiClientContext.Value.Token);
            }

            var serializedSecret = JsonConvert.SerializeObject(_secret);

            _secretProvider.SetSecret(_secretProviderKey, serializedSecret);
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

            var grantCodeIsValid = true;

            if (grantCodeIsValid)
            {

                if (_secret == null)
                {
                    _secret = new ServerTokenSecrets(grantCode);
                }

                var bridge = _parent.GetRoomBridge(roomKey);
                if (bridge != null)
                {
                    var key = Guid.NewGuid().ToString();

                    var token = new JoinToken() { Code = bridge.UserCode, RoomKey = bridge.RoomKey, Uuid = _parent.SystemUuid };

                    _uiClients.Add(key, new UiClientContext(token));

                    var path = _wsPath + key;

                    _server.WebSocketServices.AddService<UiClient>(path, (c) =>
                    {
                        Debug.Console(2, this, "Constructing UiClient with id: {0}", key);
                        c.Controller = _parent;
                        c.RoomKey = roomKey;
                        _uiClients[key].SetClient(c);
                    });


                    Debug.Console(0, this, "Added new WebSocket UiClient service at path: {0}", path);

                    Debug.Console(2, this, "{0} websocket services present", _server.WebSocketServices.Count);

                    CrestronConsole.ConsoleCommandResponse(string.Format("Token: {0}", key));

                    UpdateSecret();
                }
                else
                {
                    CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find room with key: {0}", roomKey));
                }
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse("Grant Code is not valid");
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

            var key = s;

            if(_uiClients.ContainsKey(key))
            { 
                var uiClientContext = _uiClients[key];

                if (uiClientContext.Client != null && uiClientContext.Client.Context.WebSocket.IsAlive)
                {
                    uiClientContext.Client.Context.WebSocket.Close(CloseStatusCode.Normal, "Token removed from server");
                }

                var path = _wsPath + key;
                if (_server.RemoveWebSocketService(path))
                {
                    _uiClients.Remove(key);

                    UpdateSecret();

                    CrestronConsole.ConsoleCommandResponse(string.Format("Client removed with token: {0}", key));
                }
                else
                {
                    CrestronConsole.ConsoleCommandResponse(string.Format("Unable to remove client with token : {0}", key));
                }
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find client with token: {0}", key));
            }
        }

        /// <summary>
        /// Prints out info about current client IDs
        /// </summary>
        private void PrintClientInfo()
        {
            CrestronConsole.ConsoleCommandResponse("Mobile Control UI Client Info:\r");

            CrestronConsole.ConsoleCommandResponse(string.Format("{0} clients found:\r", _uiClients.Count));

            foreach (var client in _uiClients)
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("RoomKey: {0} Token: {1}\r", client.Value.Token.RoomKey, client.Key));
            }
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                foreach(var client in _uiClients.Values)
                {
                    if (client.Client != null && client.Client.Context.WebSocket.IsAlive)
                    {
                        client.Client.Context.WebSocket.Close(CloseStatusCode.Normal, "Server Shutting Down");
                    }
                }

                StopServer();
            }
        }

        /// <summary>
        /// Handler for GET requests to server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _server_OnGet(object sender, HttpRequestEventArgs e)
        {
            try
            {
                var req = e.Request;
                var res = e.Response;
                res.ContentEncoding = Encoding.UTF8;

                res.AddHeader("Access-Control-Allow-Origin", "*");

                var path = req.RawUrl;

                Debug.Console(2, this, "Request received at path: {0}", path);

                // Call for user app to join the room with a token
                if (path.StartsWith("/mc/api/ui/joinroom"))
                {
                    HandleJoinRequest(req, res);
                }
                // Call to get the server version
                else if (path.StartsWith("/mc/api/version"))
                {
                    HandleVersionRequest(res);
                }
                // Call to serve the Angular user app
                else if (path.StartsWith(_userAppBaseHref))
                {
                    HandleUserAppRequest(req, res, path);
                }
                else
                {
                    // All other paths
                    res.StatusCode = 404;
                    res.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnGet handler {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
            }
        }

        /// <summary>
        /// Handle the request to join the room with a token
        /// </summary>
        /// <param name="req"></param>
        /// <param name="res"></param>
        private void HandleJoinRequest(HttpListenerRequest req, HttpListenerResponse res)
        {
            var qp = req.QueryString;
            var token = qp["token"];

            Debug.Console(2, this, "Join Room Request with token: {0}", token);

            UiClientContext clientContext = null;

            if (_uiClients.TryGetValue(token, out clientContext))
            {
                var bridge = _parent.GetRoomBridge(clientContext.Token.RoomKey);

                if (bridge != null)
                {
                    res.StatusCode = 200;
                    res.ContentType = "application/json";

                    // Construct the response object
                    JoinResponse jRes = new JoinResponse();
                    jRes.ClientId = token;
                    jRes.RoomKey = bridge.RoomKey;
                    jRes.SystemUuid = _parent.SystemUuid;
                    jRes.RoomUuid = _parent.SystemUuid;
                    jRes.Config = _parent.GetConfigWithPluginVersion();
                    jRes.CodeExpires = new DateTime().AddYears(1);
                    jRes.UserCode = bridge.UserCode;
                    jRes.UserAppUrl = string.Format("http://{0}:{1}/mc/app",
                        CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                        _port);
                    jRes.EnableDebug = false;

                    // Serialize to JSON and convert to Byte[]
                    var json = JsonConvert.SerializeObject(jRes);
                    var body = Encoding.UTF8.GetBytes(json);
                    res.ContentLength64 = body.LongLength;

                    // Send the response
                    res.Close(body, true);
                }
                else
                {
                    var message = string.Format("Unable to find bridge with key: {0}", clientContext.Token.RoomKey);
                    res.StatusCode = 404;
                    res.ContentType = "application/json";
                    var body = Encoding.UTF8.GetBytes(message);
                    res.ContentLength64 = body.LongLength;
                    res.Close(body, true);
                    Debug.Console(2, this, "{0}", message);
                }
            }
            else
            {
                var message = "Token invalid or has expired";
                res.StatusCode = 401;
                res.ContentType = "application/json";
                Debug.Console(2, this, "{0}", message);
                var body = Encoding.UTF8.GetBytes(message);
                res.ContentLength64 = body.LongLength;
                res.Close(body, true);
            }
        }

        /// <summary>
        /// Handles a server version request
        /// </summary>
        /// <param name="res"></param>
        private void HandleVersionRequest(HttpListenerResponse res)
        {
            res.StatusCode = 200;
            res.ContentType = "application/json";
            var version = new Version() { ServerVersion = _parent.GetConfigWithPluginVersion().RuntimeInfo.PluginVersion };
            var message = JsonConvert.SerializeObject(version);
            Debug.Console(2, this, "{0}", message);

            var body = Encoding.UTF8.GetBytes(message);
            res.ContentLength64 = body.LongLength;
            res.Close(body, true);
        }

        /// <summary>
        /// Handles requests to serve files for the Angular single page app
        /// </summary>
        /// <param name="req"></param>
        /// <param name="res"></param>
        /// <param name="path"></param>
        private void HandleUserAppRequest(HttpListenerRequest req, HttpListenerResponse res, string path)
        {
            Debug.Console(2, this, "Requesting User app file...");

            var qp = req.QueryString;
            var token = qp["token"];

            // remove the token from the path if found
            string filePath = path.Replace(string.Format("?token={0}", token), "");

            // if there's no file suffix strip any extra path data after the base href
            if (filePath != _userAppBaseHref && !filePath.Contains(".") && (!filePath.EndsWith(_userAppBaseHref) || !filePath.EndsWith(_userAppBaseHref += "/")))
            {
                var suffix = filePath.Substring(_userAppBaseHref.Length, filePath.Length - _userAppBaseHref.Length);
                if (suffix != "/")
                {
                    //Debug.Console(2, this, "Suffix: {0}", suffix);
                    filePath = filePath.Replace(suffix, "");
                }
            }

            // swap the base href prefix for the file path prefix
            filePath = filePath.Replace(_userAppBaseHref, _appPath);

            Debug.Console(2, this, "filepath: {0}", filePath);


            // append index.html if no specific file is specified
            if (!filePath.Contains("."))
            {
                if (filePath.EndsWith("/"))
                {
                    filePath += "index.html";
                }
                else
                {
                    filePath += "/index.html";
                }
            }

            // Set ContentType based on file type
            if (filePath.EndsWith(".html"))
            {
                Debug.Console(2, this, "Client requesting User App...");

                res.ContentType = "text/html";
            }
            else
            {
                if (path.EndsWith(".js"))
                {
                    res.ContentType = "application/javascript";
                }
                else if (path.EndsWith(".css"))
                {
                    res.ContentType = "text/css";
                }
                else if (path.EndsWith(".json"))
                {
                    res.ContentType = "application/json";
                }
            }

            Debug.Console(2, this, "Attempting to serve file: {0}", filePath);

            byte[] contents;
            if (System.IO.File.Exists(filePath))
            {
                Debug.Console(2, this, "File found");
                contents = System.IO.File.ReadAllBytes(filePath);
            }
            else
            {
                Debug.Console(2, this, "File not found: {0}", filePath);
                res.StatusCode = (int)HttpStatusCode.NotFound;
                res.Close();
                return;
            }

            res.ContentLength64 = contents.LongLength;
            res.Close(contents, true);
        }

        public void StopServer()
        {
            Debug.Console(2, this, "Stopping WebSocket Server");
            _server.Stop(CloseStatusCode.Normal, "Server Shutting Down");
        }

        /// <summary>
        /// Sends a message to all connectd clients
        /// </summary>
        /// <param name="message"></param>
        public void SendMessageToAllClients(string message)
        {
            foreach (var clientContext in _uiClients.Values)
            {
                if (clientContext.Client != null && clientContext.Client.Context.WebSocket.IsAlive)
                {
                    clientContext.Client.Context.WebSocket.Send(message);
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
            UiClientContext clientContext;

            if (_uiClients.TryGetValue((string)clientId, out clientContext))
            {
                if (clientContext.Client != null)
                {
                    var socket = clientContext.Client.Context.WebSocket;

                    if (socket.IsAlive)
                    {
                        socket.Send(message);
                    }
                }
            }
            else
            {
                Debug.Console(0, this, "Unable to find client with ID: {0}", clientId);
            }
        }
    }

    /// <summary>
    /// Class to describe the server version info
    /// </summary>
    public class Version
    {
        [JsonProperty("serverVersion")]
        public string ServerVersion { get; set; }

        [JsonProperty("serverIsRunningOnProcessorHardware")]
        public bool ServerIsRunningOnProcessorHardware { get; private set; }

        public Version()
        {
            ServerIsRunningOnProcessorHardware = true;
        }
    }

    /// <summary>
    /// Represents an instance of a UiClient and the associated Token
    /// </summary>
    public class UiClientContext
    {
        public UiClient Client { get; private set; }
        public JoinToken Token { get; private set; }

        public UiClientContext(JoinToken token)
        {
            Token = token;
        }

        public void SetClient(UiClient client)
        {
            Client = client;
        }

    }

    /// <summary>
    /// Represents the data structure for the grant code and UiClient tokens to be stored in the secrets manager
    /// </summary>
    public class ServerTokenSecrets
    {
        public string GrantCode { get; set; }

        public Dictionary<string, JoinToken> Tokens { get; set; }

        public ServerTokenSecrets(string grantCode)
        {
            GrantCode = grantCode;
            Tokens = new Dictionary<string, JoinToken>();
        }
    }

    /// <summary>
    /// Represents a join token with the associated properties
    /// </summary>
    public class JoinToken
    {
        public string Code { get; set; }

        public string RoomKey { get; set; }

        public string Uuid { get; set; }
    }

    /// <summary>
    /// Represents the structure of the join response
    /// </summary>
    public class JoinResponse
    {
        [JsonProperty("clientId")]
        public string ClientId { get; set; }

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
