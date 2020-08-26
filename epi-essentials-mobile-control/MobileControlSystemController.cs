using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using WebSocketSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Monitoring;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.AppServer.Messengers;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace PepperDash.Essentials
{
    public class MobileControlSystemController : EssentialsDevice, IMobileControl
    {
        //WebSocketClient WSClient;
        private WebSocket _wsClient2;

        private Thread ReceiveThread;

        private Thread TransmitThread;

        private CrestronQueue<string> ReceiveQueue;

        private CrestronQueue<object> TransmitQueue;

        //bool LinkUp;

        /// <summary>
        /// Prevents post operations from stomping on each other and getting lost
        /// </summary>
        private CEvent _postLockEvent = new CEvent(true, true);

        private CEvent _registerLockEvent = new CEvent(true, true);

        public MobileControlConfig Config { get; private set; }

        public string Host { get; private set; }

        private readonly Dictionary<string, Object> _actionDictionary =
            new Dictionary<string, Object>(StringComparer.InvariantCultureIgnoreCase);

        private readonly Dictionary<string, CTimer> _pushedActions = new Dictionary<string, CTimer>();

        public ConfigMessenger ConfigMessenger { get; private set; }

        private CTimer _serverHeartbeatCheckTimer;

        private const long ServerHeartbeatInterval = 20000;

        private CTimer _serverReconnectTimer;

        private const long ServerReconnectInterval = 5000;

        private DateTime _lastAckMessage;

        public string SystemUuid;

        private readonly List<MobileControlBridgeBase> _roomBridges = new List<MobileControlBridgeBase>();

        private const long ButtonHeartbeatInterval = 1000;

        /// <summary>
        /// Used for tracking HTTP debugging
        /// </summary>
        private bool _httpDebugEnabled;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="config"></param>
        public MobileControlSystemController(string key, string name, MobileControlConfig config) : base(key, name)
        {
            Config = config;

            // The queue that will collect the incoming messages in the order they are received
            ReceiveQueue = new CrestronQueue<string>(25);

            // The queue that will collect the outgoing messages in the order they are received
            TransmitQueue = new CrestronQueue<object>(25);

            // The thread responsible for dequeuing and processing the messages
            ReceiveThread = new Thread((o) => ProcessRecieveQueue(), null);
            ReceiveThread.Priority = Thread.eThreadPriority.HighPriority;

            TransmitThread = new Thread((o) => ProcessTransmitQueue(), null);
            TransmitThread.Priority = Thread.eThreadPriority.HighPriority;

            Host = config.ServerUrl;
            if (!Host.StartsWith("http"))
            {
                Host = "https://" + Host;
            }

            SystemUuid = ConfigReader.ConfigObject.SystemUuid;

            Debug.Console(0, this, "Mobile UI controller initializing for server:{0}", config.ServerUrl);

            CrestronConsole.AddNewConsoleCommand(AuthorizeSystem,
                "mobileauth", "Authorizes system to talk to Mobile Control server",
                ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => ShowInfo(),
                "mobileinfo", "Shows information for current mobile control session",
                ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s =>
            {
                s = s.Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    _httpDebugEnabled = (s.Trim() != "0");
                }
                CrestronConsole.ConsoleCommandResponse("HTTP Debug {0}", _httpDebugEnabled ? "Enabled" : "Disabled");
            },
                "mobilehttpdebug", "1 enables more verbose HTTP response debugging",
                ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(TestHttpRequest,
                "mobilehttprequest", "Tests an HTTP get to URL given", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(PrintActionDictionaryPaths, "mobileshowactionpaths",
                "Prints the paths in the Action Dictionary", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => ConnectWebsocketClient(), "mobileconnect",
                "Forces connect of websocket", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s => CleanUpWebsocketClient(), "mobiledisco",
                "Disconnects websocket", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(ParseStreamRx, "mobilesimulateaction",
                "Simulates a message from the server", ConsoleAccessLevelEnum.AccessOperator);

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;
            //CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);

            // Config Messenger
            var cmKey = Key + "-config";
            ConfigMessenger = new ConfigMessenger(cmKey, "/config");
            ConfigMessenger.RegisterWithAppServer(this);
        }

        /// <summary>
        /// Runs in it's own thread to dequeue messages in the order they were received to be processed
        /// </summary>
        /// <returns></returns>
        object ProcessRecieveQueue()
        {
            try
            {
                while (true)
                {
                    var message = ReceiveQueue.Dequeue();

                    ParseStreamRx(message);
                }
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Error Processing Queue: {0}", e);
            }

            return null;
        }

        /// Runs in it's own thread to dequeue messages in the order they were received to be processed
        /// </summary>
        /// <returns></returns>
        object ProcessTransmitQueue()
        {
            try
            {
                while (true)
                {
                    var message = TransmitQueue.Dequeue();

                    SendMessageToServer(JObject.FromObject(message));
                }
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Error Processing Queue: {0}", e);
            }

            return null;
        }



        public void CreateMobileControlRoomBridge(EssentialsRoomBase room)
        {
            var bridge = new MobileControlEssentialsRoomBridge(room);
            AddBridgePostActivationAction(bridge);
            DeviceManager.AddDevice(bridge);
        }

        public void LinkSystemMonitorToAppServer()
        {
            var sysMon = DeviceManager.GetDeviceForKey("systemMonitor") as SystemMonitorController;

            var appServer = DeviceManager.GetDeviceForKey("appServer") as MobileControlSystemController;


            if (sysMon == null || appServer == null) return;

            var key = sysMon.Key + "-" + appServer.Key;
            var messenger = new SystemMonitorMessenger(key, sysMon, "/device/systemMonitor");

            messenger.RegisterWithAppServer(appServer);

            DeviceManager.AddDevice(messenger);
        }

        private void AddBridgePostActivationAction(MobileControlBridgeBase bridge)
        {
            bridge.AddPostActivationAction(() =>
            {
                var parent =
                    DeviceManager.AllDevices.SingleOrDefault(dev => dev.Key == "appServer") as
                        MobileControlSystemController;

                if (parent == null)
                {
                    Debug.Console(0, bridge,
                        "ERROR: Cannot connect app server room bridge. System controller not present");
                    return;
                }

                Debug.Console(0, bridge, "Linking to parent controller");
                bridge.AddParent(parent);
                parent.AddBridge(bridge);
            });
        }

        /// <summary>
        /// If config rooms is empty or null then go
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            if (ConfigReader.ConfigObject.Rooms == null || ConfigReader.ConfigObject.Rooms.Count == 0)
            {
                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Config contains no rooms.  Registering with Server.");
                RegisterSystemToServer();
            }

            return base.CustomActivate();
        }


//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="ethernetEventArgs"></param>
//        void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs args)
//        {
//            Debug.Console(1, this, Debug.ErrorLogLevel.Warning, "Ethernet status change, port {0}: {1}",
//                args.EthernetAdapter, args.EthernetEventType);

//#warning See if this is even necessary for this new client
//            //if (args.EthernetEventType == eEthernetEventType.LinkDown && WSClient != null && args.EthernetAdapter == WSClient.EthernetAdapter)
//            //{
//            //    CleanUpWebsocketClient();
//            //}
//        }

        /// <summary>
        /// Sends message to server to indicate the system is shutting down
        /// </summary>
        /// <param name="programEventType"></param>
        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping
                && _wsClient2 != null
                && _wsClient2.IsAlive)
                //&& WSClient != null
                //&& WSClient.Connected)
            {
                _wsClient2.OnClose -= HandleClose;

                ReceiveQueue.Clear();
                TransmitQueue.Clear();
                _serverHeartbeatCheckTimer.Stop();
                StopServerReconnectTimer();
                CleanUpWebsocketClient();            
            }

        }

        public void PrintActionDictionaryPaths(object o)
        {
            Debug.Console(0, this, "ActionDictionary Contents:");

            foreach (var item in _actionDictionary)
            {
                Debug.Console(0, this, "{0}", item.Key);
            }
        }

        /// <summary>
        /// Adds an action to the dictionary
        /// </summary>
        /// <param name="key">The path of the API command</param>
        /// <param name="action">The action to be triggered by the commmand</param>
        public void AddAction(string key, object action)
        {
            if (!_actionDictionary.ContainsKey(key))
            {
                _actionDictionary.Add(key, action);
            }
            else
            {
                Debug.Console(1, this,
                    "Cannot add action with key '{0}' because key already exists in ActionDictionary.", key);
            }
        }

        /// <summary>
        /// Removes an action from the dictionary
        /// </summary>
        /// <param name="key"></param>
        public void RemoveAction(string key)
        {
            if (_actionDictionary.ContainsKey(key))
                _actionDictionary.Remove(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bridge"></param>
        public void AddBridge(MobileControlBridgeBase bridge)
        {
            _roomBridges.Add(bridge);
            var b = bridge as IDelayedConfiguration;
            if (b != null)
            {
                Debug.Console(0, this, "Adding room bridge with delayed configuration");
                b.ConfigurationIsReady += bridge_ConfigurationIsReady;
            }
            else
            {
                Debug.Console(0, this, "Adding room bridge and sending configuration");
                //SystemUuid = ConfigReader.ConfigObject.SystemUuid;
                RegisterSystemToServer();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bridge_ConfigurationIsReady(object sender, EventArgs e)
        {
            Debug.Console(1, this, "Bridge ready.  Registering");
            //SystemUuid = ConfigReader.ConfigObject.SystemUuid;
            // send the configuration object to the server
            RegisterSystemToServer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        private void ReconnectToServerTimerCallback(object o)
        {
            RegisterSystemToServer();
        }

        /// <summary>
        /// Verifies system connection with servers
        /// </summary>
        private void AuthorizeSystem(string code)
        {
            if (string.IsNullOrEmpty(SystemUuid))
            {
                CrestronConsole.ConsoleCommandResponse(
                    "System does not have a UUID. Please ensure proper configuration is loaded and restart.");
                return;
            }
            if (string.IsNullOrEmpty(code))
            {
                CrestronConsole.ConsoleCommandResponse("Please enter a user code to authorize a system");
                return;
            }
            if (string.IsNullOrEmpty(Config.ServerUrl))
            {
                CrestronConsole.ConsoleCommandResponse(
                    "Mobile control API address is not set.  Check portal configuration");
                return;
            }


            try
            {
                string path = string.Format("/api/system/grantcode/{0}/{1}", code, SystemUuid);
                string url = string.Format("{0}{1}", Host, path);
                Debug.Console(0, this, "Authorizing to: {0}", url);

                if (Host.StartsWith("https:"))
                    DispatchHttpsAuthorizationRequest(url);
                else
                {
                    var req = new HttpClientRequest();
                    req.Url.Parse(url);

                    var c = new HttpClient { AllowAutoRedirect = false };
                    c.DispatchAsync(req, (r, e) =>
                    {
                        CheckHttpDebug(r, e);
                        if (e == HTTP_CALLBACK_ERROR.COMPLETED)
                        {
                            if (r.Code == 200)
                            {
                                Debug.Console(0, "System authorized, sending config.");
                                RegisterSystemToServer();
                            }
                            else if (r.Code == 404)
                            {
                                if (r.ContentString.Contains("codeNotFound"))
                                {
                                    Debug.Console(0, "Authorization failed, code not found for system UUID {0}",
                                        SystemUuid);
                                }
                                else if (r.ContentString.Contains("uuidNotFound"))
                                {
                                    Debug.Console(0,
                                        "Authorization failed, uuid {0} not found. Check Essentials configuration is correct",
                                        SystemUuid);
                                }
                            }
                            else
                            {
                                if (r.Code == 301)
                                {
                                    var newUrl = r.Header.GetHeaderValue("Location");
                                    var newHostValue = newUrl.Substring(0,
                                        newUrl.IndexOf(path, StringComparison.Ordinal));
                                    Debug.Console(0, this,
                                        "ERROR: Mobile control API has moved. Please adjust configuration to \"{0}\"",
                                        newHostValue);
                                }
                                else
                                {
                                    Debug.Console(0, "http authorization failed, code {0}: {1}", r.Code, r.ContentString);
                                }
                            }
                        }
                        else
                        {
                            if (r != null)
                            {
                                Debug.Console(0, this, "Error in http authorization (A) {0}: {1}", r.Code, e);
                            }
                            else
                            {
                                Debug.Console(0, this, "Error in http authorization (B) {0}", e);
                            }
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Error in authorizing (C): {0}", e.Message);
            }
        }

        /// <summary>
        /// Dispatchs and handles an Https Authorization Request
        /// </summary>
        /// <param name="url">Url to dispatch request to</param>
        private void DispatchHttpsAuthorizationRequest(string url)
        {
            var req = new HttpsClientRequest();
            req.Url.Parse(url);

            var c = new HttpsClient();
            c.HostVerification = false;
            c.PeerVerification = false;
            c.Verbose = true;

            c.DispatchAsync(req, (r, e) =>
            {
                if (e == HTTPS_CALLBACK_ERROR.COMPLETED)
                {
                    ProcessAuthorizationResponse(r);
                }
                else
                {
                    if (r != null)
                    {
                        Debug.Console(0, this, "Error in http authorization (A) {0}: {1}", r.Code, e);
                    }
                    else
                    {
                        Debug.Console(0, this, "Error in http authorization (B) {0}", e);
                    }
                }
            });
        }

        /// <summary>
        /// Processes HttpsClientResponse and registers system to server as necessary
        /// </summary>
        /// <param name="r">Response from authorization request</param>
        private void ProcessAuthorizationResponse(HttpsClientResponse r)
        {
            if (r.Code == 200)
            {
                Debug.Console(0, "System authorized, sending config.");
                RegisterSystemToServer();
            }
            else if (r.Code == 404 && String.IsNullOrEmpty(r.ContentString))
            {
                Debug.Console(0, "https authorization failed, code {0}", r.Code);
                if (String.IsNullOrEmpty(r.ContentString))
                    Debug.Console(0, "content: {0}", r.ContentString);

                if (r.ContentString.Contains("codeNotFound"))
                {
                    Debug.Console(0, "code not found for system UUID {0}",
                        SystemUuid);
                }
                else if (r.ContentString.Contains("uuidNotFound"))
                {
                    Debug.Console(0,
                        "uuid {0} not found. Check Essentials configuration is correct",
                        SystemUuid);
                }
            }
            else if (r.Code == 301 && r.Header != null)
            {
                Debug.Console(0, "https authorization failed, code {0}", r.Code);
                if (String.IsNullOrEmpty(r.ContentString))
                    Debug.Console(0, "content {0}", r.ContentString);

                var newUrl = r.Header.GetHeaderValue("Location");
                var newHostValue = newUrl.Substring(0,
                    newUrl.IndexOf(r.ResponseUrl, StringComparison.Ordinal));
                Debug.Console(0, this,
                    "ERROR: Mobile control API has moved. Please adjust configuration to \"{0}\"",
                    newHostValue);
            }
            else
            {
                Debug.Console(0, "https authorization failed, code {0}", r.Code);
                if (String.IsNullOrEmpty(r.ContentString))
                    Debug.Console(0, "Content {0}", r.ContentString);
            }
        }

        /// <summary>
        /// Dumps info in response to console command.
        /// </summary>
        private void ShowInfo()
        {
            var url = Config != null ? Host : "No config";
            string name;
            string code;
            if (_roomBridges != null && _roomBridges.Count > 0)
            {
                name = _roomBridges[0].RoomName;
                code = _roomBridges[0].UserCode;
            }
            else
            {
                name = "No config";
                code = "Not available";
            }
            //var conn = WSClient == null ? "No client" : (WSClient.Connected ? "Yes" : "No");
            var conn = _wsClient2 == null ? "No client" : (_wsClient2.IsAlive ? "Yes" : "No");

            var secSinceLastAck = DateTime.Now - _lastAckMessage;


            CrestronConsole.ConsoleCommandResponse(@"Mobile Control Information:
	Server address: {0}
	System Name: {1}
    System URL: {2}
	System UUID: {3}
	System User code: {4}
	Connected?: {5}
    Seconds Since Last Ack: {6}"
                , url, name, ConfigReader.ConfigObject.SystemUrl, SystemUuid,
                code, conn, secSinceLastAck.Seconds);
        }

        /// <summary>
        /// Registers the room with the server
        /// </summary>
        private void RegisterSystemToServer()
        {
            ConnectWebsocketClient();
        }

        /// <summary>
        /// Connects the Websocket Client
        /// </summary>
        private void ConnectWebsocketClient()
        {
            CleanUpWebsocketClient();
            var wsHost = Host.Replace("http", "ws");
            var url = string.Format("{0}/system/join/{1}", wsHost, SystemUuid);

            CleanUpWebsocketClient();
            
            _wsClient2 = new WebSocket(url)
            {
                Log = { Output = (ld, s) => Debug.Console(1, this, "Message from websocket: {0}", ld) }
            };

            _wsClient2.OnMessage += HandleMessage;
            _wsClient2.OnOpen += HandleOpen;
            _wsClient2.OnError += HandleError;
            _wsClient2.OnClose += HandleClose;
            Debug.Console(1, this, "Initializing mobile control client to {0}", url);
            _wsClient2.Connect();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleOpen(object sender, EventArgs e)
        {
            StopServerReconnectTimer();
            Debug.Console(1, this, "Mobile Control API connected");
            SendMessageObjectToServer(new
            {
                type = "hello"
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleMessage(object sender, MessageEventArgs e)
        {
            if (e.IsText && e.Data.Length > 0)
            {
                ReceiveQueue.Enqueue(e.Data);

                // If the receive thread has for some reason stopped, this will restart it
                if (ReceiveThread.ThreadState != Thread.eThreadStates.ThreadRunning)
                    ReceiveThread.Start();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleError(object sender, ErrorEventArgs e)
        {
            Debug.Console(1, this, "Websocket error {0}", e.Message);
            StartServerReconnectTimer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleClose(object sender, CloseEventArgs e)
        {
            Debug.Console(1, this, "Websocket close {0} {1}, clean={2}", e.Code, e.Reason, e.WasClean);
            if (_serverHeartbeatCheckTimer != null)
                _serverHeartbeatCheckTimer.Stop();
            // Start the reconnect timer
            StartServerReconnectTimer();
        }

        /// <summary>
        /// After a "hello" from the server, sends config and stuff
        /// </summary>
        private void SendInitialMessage()
        {
            Debug.Console(1, this, "Sending initial join message");
            var confObject = ConfigReader.ConfigObject;
            confObject.Info.RuntimeInfo.AppName = Assembly.GetExecutingAssembly().GetName().Name;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            confObject.Info.RuntimeInfo.AssemblyVersion = string.Format("{0}.{1}.{2}", version.Major, version.Minor,
                version.Build);

            var msg = new
            {
                type = "join",
                content = new
                {
                    config = confObject
                }
            };
            SendMessageObjectToServer(msg);
        }

        /// <summary>
        /// Sends any object type to server
        /// </summary>
        /// <param name="o"></param>
        public void SendMessageObjectToServer(object o)
        {
            TransmitQueue.Enqueue(o);

            // If the receive thread has for some reason stopped, this will restart it
            if (TransmitThread.ThreadState != Thread.eThreadStates.ThreadRunning)
            {
                TransmitThread.Start();
            }

            //SendMessageToServer(JObject.FromObject(o));
        }

        /// <summary>
        /// Sends a message to the server from a room
        /// </summary>
        /// <param name="o">object to be serialized and sent in post body</param>
        private void SendMessageToServer(JObject o)
        {
            if (_wsClient2 != null && _wsClient2.IsAlive)
            {
                string message = JsonConvert.SerializeObject(o, Formatting.None,
                    new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});

                if (!message.Contains("/system/heartbeat"))
                {
                    Debug.Console(1, this, "Message TX: {0}", message);
                }
                _wsClient2.Send(message);
            }
            else if (_wsClient2 == null)
            {
                Debug.Console(1, this, "Cannot send. No client.");
            }
        }

        /// <summary>
        /// Disconnects the Websocket Client and stops the heartbeat timer
        /// </summary>
        private void CleanUpWebsocketClient()
        {
            if (_wsClient2 == null) return;

            Debug.Console(1, this, "Disconnecting websocket");

            _wsClient2.OnMessage -= HandleMessage;
            _wsClient2.OnOpen -= HandleOpen;
            _wsClient2.OnError -= HandleError;
            _wsClient2.OnClose -= HandleClose;

            _wsClient2.Close();
            _wsClient2 = null;

            //try
            //{
            //    ReceiveThread.Abort();
            //    TransmitThread.Abort();
            //}
            //catch (Exception e)
            //{
            //    Debug.Console(2, this, "Error aborting threads: {0}", e);
            //}
        }

        /// <summary>
        /// 
        /// </summary>
        private void StartServerReconnectTimer()
        {
            StopServerReconnectTimer();
            _serverReconnectTimer = new CTimer(ReconnectToServerTimerCallback, ServerReconnectInterval);
            Debug.Console(1, this, "Reconnect Timer Started.");
        }

        /// <summary>
        /// Does what it says
        /// </summary>
        private void StopServerReconnectTimer()
        {
            if (_serverReconnectTimer != null)
            {
                _serverReconnectTimer.Stop();
                _serverReconnectTimer = null;
            }
        }

        /// <summary>
        /// Executes when we don't get a heartbeat message in time.  Triggers reconnect.
        /// </summary>
        /// <param name="o">For CTimer callback. Not used</param>
        private void HeartbeatExpiredTimerCallback(object o)
        {
            Debug.Console(1, this, "Heartbeat Timer Expired.");
            if (_serverHeartbeatCheckTimer != null)
            {
                _serverHeartbeatCheckTimer.Stop();
                _serverHeartbeatCheckTimer = null;
            }
            CleanUpWebsocketClient();
            StartServerReconnectTimer();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetOrStartHearbeatTimer()
        {
            if (_serverHeartbeatCheckTimer == null)
            {
                _serverHeartbeatCheckTimer = new CTimer(HeartbeatExpiredTimerCallback, null, ServerHeartbeatInterval,
                    ServerHeartbeatInterval);
                Debug.Console(1, this, "Heartbeat Timer Started.");
            }
            else
            {
                _serverHeartbeatCheckTimer.Reset(ServerHeartbeatInterval, ServerHeartbeatInterval);
            }
        }

        /// <summary>
        /// Waits two and goes again
        /// </summary>
        private void ReconnectStreamClient()
        {
            var timer = new CTimer(o => ConnectWebsocketClient(), 2000);
        }


        /// <summary>
        /// Resets reconnect timer and updates usercode
        /// </summary>
        /// <param name="content"></param>
        private void HandleHeartBeat(JToken content)
        {
            SendMessageObjectToServer(new
            {
                type = "/system/heartbeatAck"
            });

            var code = content["userCode"];
            if (code != null)
            {
                foreach (var b in _roomBridges)
                {
                    b.SetUserCode(code.Value<string>());
                }
            }
            ResetOrStartHearbeatTimer();
        }

        /// <summary>
        /// Outputs debug info when enabled
        /// </summary>
        /// <param name="r"></param>
        /// <param name="e"></param>
        private void CheckHttpDebug(HttpClientResponse r, HTTP_CALLBACK_ERROR e)
        {
            if (!_httpDebugEnabled) return;

            try
            {
                Debug.Console(0, this, "------ Begin HTTP Debug ---------------------------------------");
                if (r != null)
                {
                    Debug.Console(0, this, "HTTP Response URL: {0}", r.ResponseUrl ?? "NONE");
                    Debug.Console(0, this, "HTTP Response code: {0}", r.Code);
                    Debug.Console(0, this, "HTTP Response content: \r{0}", r.ContentString);
                }
                else
                {
                    Debug.Console(0, this, "No HTTP response");
                }
                Debug.Console(0, this, "HTTP Response 'error' {0}", e);
                Debug.Console(0, this, "------ End HTTP Debug -----------------------------------------");
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "HttpDebugError: {0}", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void ParseStreamRx(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (!message.Contains("/system/heartbeat"))
            {
                Debug.Console(1, this, "Message RX: {0}", message);
            }
            else
            {
                _lastAckMessage = DateTime.Now;
            }

            try
            {
                var messageObj = JObject.Parse(message);

                var type = messageObj["type"].Value<string>();

                switch (type)
                {
                    case "hello":
                        SendInitialMessage();
                        ResetOrStartHearbeatTimer();
                        break;
                    case "/system/heartbeat":
                        HandleHeartBeat(messageObj["content"]);
                        break;
                    case "raw":
                    {
                        var wrapper = messageObj["content"].ToObject<DeviceActionWrapper>();
                        DeviceJsonApi.DoDeviceAction(wrapper);
                    }
                        break;
                    case "close":
                        Debug.Console(1, this, "Received close message from server.");
                        if (_serverHeartbeatCheckTimer != null)
                            _serverHeartbeatCheckTimer.Stop();
                        break;
                    default:
                        if (_actionDictionary.ContainsKey(type))
                        {
                            var action = _actionDictionary[type];

                            if (action is Action)
                            {
                                (action as Action)();
                            }
                            else if (action is PressAndHoldAction)
                            {
                                var stateString = messageObj["content"]["state"].Value<string>();

                                // Look for a button press event
                                if (!string.IsNullOrEmpty(stateString))
                                {
                                    switch (stateString)
                                    {
                                        case "true":
                                        {
                                            if (!_pushedActions.ContainsKey(type))
                                            {
                                                _pushedActions.Add(type, new CTimer(o =>
                                                {
                                                    var pressAndHoldAction = action as PressAndHoldAction;
                                                    if (pressAndHoldAction != null)
                                                        pressAndHoldAction(false);
                                                    _pushedActions.Remove(type);
                                                }, null, ButtonHeartbeatInterval, ButtonHeartbeatInterval));
                                            }
                                            // Maybe add an else to reset the timer
                                            break;
                                        }
                                        case "held":
                                        {
                                            if (_pushedActions.ContainsKey(type))
                                            {
                                                _pushedActions[type].Reset(ButtonHeartbeatInterval,
                                                    ButtonHeartbeatInterval);
                                            }
                                            return;
                                        }
                                        case "false":
                                        {
                                            if (_pushedActions.ContainsKey(type))
                                            {
                                                _pushedActions[type].Stop();
                                                _pushedActions.Remove(type);
                                            }
                                            break;
                                        }
                                    }

                                    (action as PressAndHoldAction)(stateString == "true");
                                }
                            }
                            else if (action is Action<bool>)
                            {
                                var stateString = messageObj["content"]["state"].Value<string>();

                                if (!string.IsNullOrEmpty(stateString))
                                {
                                    (action as Action<bool>)(stateString == "true");
                                }
                            }
                            else if (action is Action<ushort>)
                            {
                                (action as Action<ushort>)(messageObj["content"]["value"].Value<ushort>());
                            }
                            else if (action is Action<string>)
                            {
                                (action as Action<string>)(messageObj["content"]["value"].Value<string>());
                            }
                            else if (action is Action<SourceSelectMessageContent>)
                            {
                                (action as Action<SourceSelectMessageContent>)(messageObj["content"]
                                    .ToObject<SourceSelectMessageContent>());
                            }
                            else if (action is ClientSpecificUpdateRequest)
                            {
                                var clientId = Int32.Parse(messageObj["clientId"].ToString());

                                var respObj = (action as ClientSpecificUpdateRequest).ResponseMethod() as MobileControlResponseMessage;

                                respObj.ClientId = clientId;

                                SendMessageObjectToServer(respObj);
                            }
                        }
                        else
                        {
                            Debug.Console(1, this, "-- Warning: Incoming message has no registered handler");
                        }
                        break;
                }
            }
            catch (Exception err)
            {
                //Debug.Console(1, "SseMessageLengthBeforeFailureCount: {0}", SseMessageLengthBeforeFailureCount);
                //SseMessageLengthBeforeFailureCount = 0;
                Debug.Console(1, this, "Unable to parse message: {0}", err);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        private void TestHttpRequest(string s)
        {
            {
                s = s.Trim();
                if (string.IsNullOrEmpty(s))
                {
                    PrintTestHttpRequestUsage();
                    return;
                }
                var tokens = s.Split(' ');
                if (tokens.Length < 2)
                {
                    CrestronConsole.ConsoleCommandResponse("Too few paramaters\r");
                    PrintTestHttpRequestUsage();
                    return;
                }

                try
                {
                    var url = tokens[1];
                    if (tokens[0].ToLower() == "get")
                    {
                        var resp = new HttpClient().Get(url);
                        CrestronConsole.ConsoleCommandResponse("RESPONSE:\r{0}\r\r", resp);
                    }
                    else if (tokens[0].ToLower() == "post")
                    {
                        var resp = new HttpClient().Post(url, new byte[] {});
                        CrestronConsole.ConsoleCommandResponse("RESPONSE:\r{0}\r\r", resp);
                    }

                    else
                    {
                        CrestronConsole.ConsoleCommandResponse("Only get or post supported\r");
                        PrintTestHttpRequestUsage();
                    }
                }
                catch (HttpException e)
                {
                    CrestronConsole.ConsoleCommandResponse("Exception in request:\r");
                    CrestronConsole.ConsoleCommandResponse("Response URL: {0}\r", e.Response.ResponseUrl);
                    CrestronConsole.ConsoleCommandResponse("Response Error Code: {0}\r", e.Response.Code);
                    CrestronConsole.ConsoleCommandResponse("Response body: {0}\r", e.Response.ContentString);
                }
            }
        }

        private void PrintTestHttpRequestUsage()
        {
            CrestronConsole.ConsoleCommandResponse("Usage: mobilehttprequest:N get/post url\r");
        }
       
    }

    public class ClientSpecificUpdateRequest
    {
        public Func<object> ResponseMethod { get; private set; }

        public ClientSpecificUpdateRequest(Func<object> func)
        {
            ResponseMethod = func;
        }
    }

    public class MobileControlResponseMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("clientId")]
        public int ClientId { get; set; }
        [JsonProperty("content")]
        public object Content { get; set; }
    }
}