using Crestron.SimplSharp;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Web;
using PepperDash.Essentials.Devices.Common.TouchPanel;
using PepperDash.Essentials.WebApiHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;


namespace PepperDash.Essentials
{
    /// <summary>
    /// Represents the behaviour to associate with a UiClient for WebSocket communication
    /// </summary>
    public class UiClient : WebSocketBehavior
    {       
        public MobileControlSystemController Controller { get; set; }

        public string RoomKey { get; set; }

        private DateTime _connectionTime;

        public TimeSpan ConnectedDuration
        {
            get
            {
                if (Context.WebSocket.IsAlive)
                {
                    return DateTime.Now - _connectionTime;
                }
                else
                {
                    return new TimeSpan(0);
                }
            }
        }

        public UiClient()
        {
            
        }        

        protected override void OnOpen()
        {
            base.OnOpen();

            var url = Context.WebSocket.Url;
            Debug.Console(2, Debug.ErrorLogLevel.Notice, "New WebSocket Connection from: {0}", url);

            var match = Regex.Match(url.AbsoluteUri, "(?:ws|wss):\\/\\/.*(?:\\/mc\\/api\\/ui\\/join\\/)(.*)");

            if (match.Success)
            {
                var clientId = match.Groups[1].Value;                               
                
                // Inform controller of client joining
                if (Controller != null)
                {
                    var clientJoined = new MobileControlMessage
                    {
                        Type = "/system/roomKey",
                        ClientId = clientId,
                        Content = RoomKey,
                    };

                    Controller.SendMessageObjectToDirectClient(clientJoined);

                    var bridge = Controller.GetRoomBridge(RoomKey);

                    SendUserCodeToClient(bridge, clientId);

                    bridge.UserCodeChanged += (sender, args) => SendUserCodeToClient((MobileControlEssentialsRoomBridge)sender, clientId);
                }
                else
                {
                    Debug.Console(2, "WebSocket UiClient Controller is null");
                }
            }

            _connectionTime = DateTime.Now;

            // TODO: Future: Check token to see if there's already an open session using that token and reject/close the session 
        }

        private void SendUserCodeToClient(MobileControlBridgeBase bridge, string clientId)
        {            
            var content = new
            {
                userCode = bridge.UserCode,
                qrUrl = bridge.QrCodeUrl,
            };

            var message = new MobileControlMessage
            {
                Type = "/system/userCodeChanged",     
                ClientId = clientId,
                Content = JToken.FromObject(content)
            };

            Controller.SendMessageObjectToDirectClient(message);
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

            Debug.Console(2, Debug.ErrorLogLevel.Notice, "WebSocket UiClient Closing: {0} reason: {1}", e.Code, e.Reason);

        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);

            Debug.Console(2, Debug.ErrorLogLevel.Notice, "WebSocket UiClient Error: {0} message: {1}", e.Exception, e.Message);
        }
    }

    public class MobileControlWebsocketServer : EssentialsDevice
    {
        private string userAppPath = Global.FilePathPrefix + "mcUserApp" + Global.DirectorySeparator;

        private string localConfigFolderName = "_local-config";

        private string appConfigFileName = "_config.local.json";

        /// <summary>
        /// Where the key is the join token and the value is the room key
        /// </summary>
        //private Dictionary<string, JoinToken> _joinTokens;

        private HttpServer _server;

        public HttpServer Server => _server;

        public Dictionary<string, UiClientContext> UiClients { get; private set; }

        private MobileControlSystemController _parent;

        private WebSocketServerSecretProvider _secretProvider;

        private ServerTokenSecrets _secret;

        private static HttpClient LogClient = new HttpClient();

        private string _secretProviderKey
        {
            get
            {
                return string.Format("{0}:{1}-tokens", Global.ControlSystem.ProgramNumber, this.Key);
            }
        }

        /// <summary>
        /// The path for the WebSocket messaging
        /// </summary>
        private string _wsPath = "/mc/api/ui/join/";

        public string WsPath => _wsPath;

        /// <summary>
        /// The path to the location of the files for the user app (single page Angular app)
        /// </summary>
        private string _appPath = string.Format("{0}mcUserApp", Global.FilePathPrefix);

        /// <summary>
        /// The base HREF that the user app uses
        /// </summary>
        private string _userAppBaseHref = "/mc/app";

        /// <summary>
        /// The prot the server will run on
        /// </summary>
        public int Port { get; private set; }

        public string UserAppUrlPrefix 
        {
            get
            {
                return string.Format("http://{0}:{1}{2}?token=",
                    CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                    Port,
                    _userAppBaseHref);

            }
        }

        public int ConnectedUiClientsCount
        {
            get
            {
                var count = 0;

                foreach (var client in UiClients)
                {
                    if (client.Value.Client != null && client.Value.Client.Context.WebSocket.IsAlive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        
        public MobileControlWebsocketServer(string key, int customPort, MobileControlSystemController parent)
            : base(key)
        {
            _parent = parent;

            // Set the default port to be 50000 plus the slot number of the program
            Port = 50000 + (int)Global.ControlSystem.ProgramNumber;

            if (customPort != 0)
            {
                Port = customPort;
            }

            UiClients = new Dictionary<string, UiClientContext>();

            //_joinTokens = new Dictionary<string, JoinToken>();

            if (Global.Platform == eDevicePlatform.Appliance)
            {
                AddConsoleCommands();
            }

            AddPreActivationAction(() => AddWebApiPaths());
        }

        private void AddWebApiPaths()
        {
            var apiServer = DeviceManager.AllDevices.OfType<EssentialsWebApi>().FirstOrDefault();

            if (apiServer == null)
            {
                Debug.Console(0, this, "No API Server available");
                return;
            }

            var routes = new List<HttpCwsRoute>
            {
                new HttpCwsRoute($"devices/{Key}/client")
                {
                    Name = "ClientHandler",
                    RouteHandler = new UiClientHandler(this)
                },
            };

            apiServer.AddRoute(routes);
        }

        private void AddConsoleCommands()
        {
            CrestronConsole.AddNewConsoleCommand(GenerateClientTokenFromConsole, "MobileAddUiClient", "Adds a client and generates a token. ? for more help", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(RemoveToken, "MobileRemoveUiClient", "Removes a client. ? for more help", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand((s) => PrintClientInfo(), "MobileGetClientInfo", "Displays the current client info", ConsoleAccessLevelEnum.AccessOperator);
        }


        public override void Initialize()
        {
            base.Initialize();

            _server = new HttpServer(Port, false);

            _server.OnGet += Server_OnGet;

            _server.OnOptions += Server_OnOptions;

            if (_parent.Config.DirectServer.Logging.EnableRemoteLogging)
            {
                _server.OnPost += Server_OnPost;                
            }

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;

            _server.Start();

            if (_server.IsListening)
            {
                Debug.Console(0, this, "Mobile Control WebSocket Server lisening on port: {0}", _server.Port);
            }

            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStop;

            RetrieveSecret();

            CreateFolderStructure();

            AddClientsForTouchpanels();
        }

        private void AddClientsForTouchpanels()
        {
            var touchpanels = DeviceManager.AllDevices
                .OfType<MobileControlTouchpanelController>().Where(tp => tp.UseDirectServer);
                

            var newTouchpanels = touchpanels.Where(tp => !_secret.Tokens.Any(t => t.Value.TouchpanelKey != null && t.Value.TouchpanelKey.Equals(tp.Key, StringComparison.InvariantCultureIgnoreCase)));


            foreach (var client in newTouchpanels)
            {
                var bridge = _parent.GetRoomBridge(client.DefaultRoomKey);

                if (bridge == null)
                {
                    Debug.Console(0, this, $"Unable to find room with key: {client.DefaultRoomKey}");
                    return;
                }

                var (key, path) = GenerateClientToken(bridge, client.Key);

                if(key == null)
                {
                    Debug.Console(0, this, $"Unable to generate a client for {client.Key}");
                    continue;
                }             
            }

            foreach(var touchpanel in touchpanels.Select(tp =>
            {
                var token = _secret.Tokens.FirstOrDefault((t) => t.Value.TouchpanelKey.Equals(tp.Key, StringComparison.InvariantCultureIgnoreCase));

                var messenger = _parent.GetRoomBridge(tp.DefaultRoomKey);

                return new { token.Key, Touchpanel = tp, Messenger = messenger };
            }))
            {
                if(touchpanel.Key == null)
                {
                    Debug.Console(0, this, $"Token for touchpanel {touchpanel.Touchpanel.Key} not found");
                    continue;
                }

                if (touchpanel.Messenger == null)
                {
                    Debug.Console(2, this, $"Unable to find room messenger for {touchpanel.Touchpanel.DefaultRoomKey}");
                    continue;
                }

                var lanAdapterId = CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter);

                var processorIp = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, lanAdapterId);

                var appUrl = $"http://{processorIp}:{_parent.Config.DirectServer.Port}/mc/app?token={touchpanel.Key}";

                Debug.Console(2, this, $"Sending URL {appUrl}");                

                touchpanel.Messenger.UpdateAppUrl($"http://{processorIp}:{_parent.Config.DirectServer.Port}/mc/app?token={touchpanel.Key}");
            }
        }

        private void OnProgramStop(eProgramStatusEventType programEventType)
        {
            switch (programEventType)
            {
                case eProgramStatusEventType.Stopping:
                    _server.Stop();
                    break;               
            }
        }

        private void CreateFolderStructure()
        {
            if (!Directory.Exists(userAppPath)) {
                Directory.CreateDirectory(userAppPath);
            }

            if (!Directory.Exists($"{userAppPath}{localConfigFolderName}"))
            {
                Directory.CreateDirectory($"{userAppPath}{localConfigFolderName}");
            }

            using(var sw = new StreamWriter(File.Open($"{userAppPath}{localConfigFolderName}{Global.DirectorySeparator}{appConfigFileName}", FileMode.Create, FileAccess.ReadWrite)))
            {
                var config = GetApplicationConfig();

                var contents = JsonConvert.SerializeObject(config, Formatting.Indented);

                sw.Write(contents);
            }
        }

        private MobileControlApplicationConfig GetApplicationConfig()
        {
            MobileControlApplicationConfig config = null;

            var lanAdapterId = CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter);

            var processorIp = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, lanAdapterId);

            try
            {
                if (_parent.Config.ApplicationConfig == null)
                {
                    config = new MobileControlApplicationConfig
                    {
                        ApiPath = string.Format("http://{0}:{1}/mc/api", processorIp, _parent.Config.DirectServer.Port),
                        GatewayAppPath = "",
                        LogoPath = "logo/logo.png",
                        EnableDev = false,
                        IconSet = MCIconSet.GOOGLE,
                        LoginMode = "room-list",
                        Modes = new Dictionary<string, McMode>
                        {
                            {
                                "room-list",
                                new McMode{
                                    ListPageText= "Please select your room",
                                    LoginHelpText = "Please select your room from the list, then enter the code shown on the display.",
                                    PasscodePageText = "Please enter the code shown on this room's display"
                                }
                            }
                        },
                        Logging = _parent.Config.DirectServer.Logging.EnableRemoteLogging,                        
                    };
                }
                else
                {
                    config = new MobileControlApplicationConfig
                    {
                        ApiPath = string.Format("http://{0}:{1}/mc/api", processorIp, _parent.Config.DirectServer.Port),
                        GatewayAppPath = "",
                        LogoPath = _parent.Config.ApplicationConfig.LogoPath ?? "logo/logo.png",
                        EnableDev = _parent.Config.ApplicationConfig.EnableDev ?? false,
                        IconSet = _parent.Config.ApplicationConfig.IconSet ?? MCIconSet.GOOGLE,
                        LoginMode = _parent.Config.ApplicationConfig.LoginMode ?? "room-list",
                        Modes = _parent.Config.ApplicationConfig.Modes ?? new Dictionary<string, McMode>
                        {
                            {
                                "room-list",
                                new McMode {
                                    ListPageText = "Please select your room",
                                    LoginHelpText = "Please select your room from the list, then enter the code shown on the display.",
                                    PasscodePageText = "Please enter the code shown on this room's display"
                                }
                            }
                        },
                        Logging = _parent.Config.ApplicationConfig.Logging
                    };
                }
            } catch(Exception ex)
            {
                Debug.Console(0, this, "Error getting application configuration: {0}", ex.Message);
                Debug.Console(2, this, "Stack Trace: {0}", ex.StackTrace);

                Debug.Console(2, "Config Object: {0} from config: {1}", config, _parent.Config);
            }

            return config;
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
                    UiClients.Add(token.Key, new UiClientContext(token.Value));
                }

                foreach (var client in UiClients)
                {
                    var key = client.Key;
                    var path = _wsPath + key;
                    var roomKey = client.Value.Token.RoomKey;

                    _server.AddWebSocketService(path, () =>
                    {
                        var c = new UiClient();
                        Debug.Console(2, this, "Constructing UiClient with id: {0}", key);
                        c.Controller = _parent;
                        c.RoomKey = roomKey;
                        UiClients[key].SetClient(c);
                        return c;
                    });


                    //_server.WebSocketServices.AddService<UiClient>(path, (c) =>
                    //{
                    //    Debug.Console(2, this, "Constructing UiClient with id: {0}", key);
                    //    c.Controller = _parent;
                    //    c.RoomKey = roomKey;
                    //    UiClients[key].SetClient(c);
                    //});
                }
            }
            else
            {
                Debug.Console(2, this, "No secret found");
            }

            Debug.Console(2, this, "{0} UiClients restored from secrets data", UiClients.Count);
        }

        /// <summary>
        /// Stores secrets to memory to persist through reboot
        /// </summary>
        public void UpdateSecret()
        {
            _secret.Tokens.Clear();

            foreach (var uiClientContext in UiClients)
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
        private void GenerateClientTokenFromConsole(string s)
        {
            if (s == "?" || string.IsNullOrEmpty(s))
            {
                CrestronConsole.ConsoleCommandResponse(@"[RoomKey] [GrantCode] Validates the room key against the grant code and returns a token for use in a UI client");
                return;
            }

            var values = s.Split(' ');
            var roomKey = values[0];
            var grantCode = values[1];

            var bridge = _parent.GetRoomBridge(roomKey);

            if (bridge == null)
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("Unable to find room with key: {0}", roomKey));
                return;
            }

            var (token, path) = ValidateGrantCode(grantCode, bridge);

            if (token == null)
            {
                CrestronConsole.ConsoleCommandResponse("Grant Code is not valid");
                return;
            }

            CrestronConsole.ConsoleCommandResponse($"Added new WebSocket UiClient service at path: {path}");
            CrestronConsole.ConsoleCommandResponse($"Token: {token}");
        }

        public (string, string) ValidateGrantCode(string grantCode, string roomKey) {
            var bridge = _parent.GetRoomBridge(roomKey);

            if (bridge == null)
            {
                Debug.Console(0, this, $"Unable to find room with key: {roomKey}");
                return (null, null);
            }

            return ValidateGrantCode(grantCode, bridge);
        }

        public (string, string) ValidateGrantCode(string grantCode, MobileControlBridgeBase bridge)
        {
            // TODO: Authenticate grant code passed in
            // For now, we just generate a random guid as the token and use it as the ClientId as well
            var grantCodeIsValid = true;            

            if (grantCodeIsValid)
            {
                if (_secret == null)
                {
                    _secret = new ServerTokenSecrets(grantCode);
                }

                return GenerateClientToken(bridge, "");
            }
            else
            {
                return (null, null);
            }
        }

        public (string, string) GenerateClientToken (MobileControlBridgeBase bridge, string touchPanelKey = "")
        {              
            var key = Guid.NewGuid().ToString();

            var token = new JoinToken { Code = bridge.UserCode, RoomKey = bridge.RoomKey, Uuid = _parent.SystemUuid, TouchpanelKey = touchPanelKey };

            UiClients.Add(key, new UiClientContext(token));

            var path = _wsPath + key;

            _server.AddWebSocketService(path, () =>
            {
                var c = new UiClient();
                Debug.Console(2, this, "Constructing UiClient with id: {0}", key);
                c.Controller = _parent;
                c.RoomKey = bridge.RoomKey;
                UiClients[key].SetClient(c);
                return c;
            });

            Debug.Console(0, this, $"Added new WebSocket UiClient service at path: {path}");
            Debug.Console(0, this, $"Token: {key}");         
                
            Debug.Console(2, this, "{0} websocket services present", _server.WebSocketServices.Count);                

            UpdateSecret();

            return (key, path);            
        }

        /// <summary>
        /// Removes a client with the specified token value
        /// </summary>
        /// <param name="s"></param>
        private void RemoveToken(string s)
        {
            if (s == "?" || string.IsNullOrEmpty(s))
            {
                CrestronConsole.ConsoleCommandResponse(@"[token] Removes the client with the specified token value");
                return;
            }

            var key = s;

            if(UiClients.ContainsKey(key))
            { 
                var uiClientContext = UiClients[key];

                if (uiClientContext.Client != null && uiClientContext.Client.Context.WebSocket.IsAlive)
                {
                    uiClientContext.Client.Context.WebSocket.Close(CloseStatusCode.Normal, "Token removed from server");
                }

                var path = _wsPath + key;
                if (_server.RemoveWebSocketService(path))
                {
                    UiClients.Remove(key);

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

            CrestronConsole.ConsoleCommandResponse(string.Format("{0} clients found:\r", UiClients.Count));

            foreach (var client in UiClients)
            {
                CrestronConsole.ConsoleCommandResponse(string.Format("RoomKey: {0} Token: {1}\r", client.Value.Token.RoomKey, client.Key));
            }
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping)
            {
                foreach(var client in UiClients.Values)
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
        private void Server_OnGet(object sender, HttpRequestEventArgs e)
        {
            try
            {
                var req = e.Request;
                var res = e.Response;
                res.ContentEncoding = Encoding.UTF8;

                res.AddHeader("Access-Control-Allow-Origin", "*");

                var path = req.RawUrl;

                Debug.Console(2, this, "GET Request received at path: {0}", path);

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

        private async void Server_OnPost(object sender, HttpRequestEventArgs e)
        {
            try
            {
                var req = e.Request;
                var res = e.Response;

                res.AddHeader("Access-Control-Allow-Origin", "*");

                var path = req.RawUrl;
                var ip = req.RemoteEndPoint.Address.ToString();                

                Debug.Console(2, this, "POST Request received at path: {0} from host {1}", path, ip);                

                var body = new System.IO.StreamReader(req.InputStream).ReadToEnd();

                if(path.StartsWith("/mc/api/log"))
                {
                    res.StatusCode = 200;
                    res.Close();

                    var logRequest = new HttpRequestMessage(HttpMethod.Post, $"http://{_parent.Config.DirectServer.Logging.Host}:{_parent.Config.DirectServer.Logging.Port}/logs")
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    };

                    logRequest.Headers.Add("x-pepperdash-host", ip);

                    await LogClient.SendAsync(logRequest);

                    Debug.Console(2, this, "Log data sent to {0}:{1}", _parent.Config.DirectServer.Logging.Host, _parent.Config.DirectServer.Logging.Port);
                } else
                {
                    res.StatusCode = 404;
                    res.Close();                    
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnPost handler {0}", ex.Message);
                Debug.Console(2, Debug.ErrorLogLevel.Error, "StackTrace: {0}", ex.StackTrace);

                if(ex.InnerException != null)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnGet handler {0}", ex.InnerException.Message);
                    Debug.Console(2, Debug.ErrorLogLevel.Error, "StackTrace: {0}", ex.InnerException.StackTrace);
                }
            }
        }

        private void Server_OnOptions(object sender, HttpRequestEventArgs e)
        {
            try
            {                
                var res = e.Response;

                res.AddHeader("Access-Control-Allow-Origin", "*");
                res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                res.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With, remember-me");

                res.StatusCode = 200;
                res.Close();              
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnPost handler {0}", ex.Message);
                Debug.Console(2, Debug.ErrorLogLevel.Error, "StackTrace: {0}", ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "Caught an exception in the OnGet handler {0}", ex.InnerException.Message);
                    Debug.Console(2, Debug.ErrorLogLevel.Error, "StackTrace: {0}", ex.InnerException.StackTrace);
                }

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

            if (UiClients.TryGetValue(token, out clientContext))
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
                        Port);
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
            foreach (var clientContext in UiClients.Values)
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
            if(clientId == null)
            {
                return;
            }

            if (UiClients.TryGetValue((string)clientId, out clientContext))
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

        public string TouchpanelKey { get; set; } = "";

        public string Token { get; set; } = null;
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
