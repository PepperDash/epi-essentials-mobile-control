﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharp.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Monitoring;
using PepperDash.Essentials.Core.Presets;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Room.Config;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.Devices.Common.Codec;
using WebSocketSharp;

namespace PepperDash.Essentials
{
    public class MobileControlSystemController : EssentialsDevice, IMobileControl
    {
        private const long ServerHeartbeatInterval = 20000;
        private const long ServerReconnectInterval = 5000;
        private const long PingInterval = 25000;
        private const long ButtonHeartbeatInterval = 1000;

        private readonly Dictionary<string, Object> _actionDictionary =
            new Dictionary<string, Object>(StringComparer.InvariantCultureIgnoreCase);

        private readonly Dictionary<string, CTimer> _pushedActions = new Dictionary<string, CTimer>();
        private readonly GenericQueue _receiveQueue;
        private readonly List<MobileControlBridgeBase> _roomBridges = new List<MobileControlBridgeBase>();

        private readonly GenericQueue _transmitQueue;

        private IEssentialsRoomCombiner _roomCombiner;

        private bool _disableReconnect;
        private WebSocket _wsClient2;

        private readonly CCriticalSection _wsCriticalSection = new CCriticalSection();

        public string SystemUrl; //set only from SIMPL Bridge!

        public string SystemUuid
        {
            get
            {
                // Check to see if the SystemUuid value is populated. If not populated from configuration, check for value from SIMPL bridge.
                if (!string.IsNullOrEmpty(ConfigReader.ConfigObject.SystemUuid) &&
                    ConfigReader.ConfigObject.SystemUuid != "missing url")
                {
                    return ConfigReader.ConfigObject.SystemUuid;
                }

                Debug.Console(0, this, Debug.ErrorLogLevel.Notice, "No system_url value defined in config.  Checking for value from SIMPL Bridge.");

                if (!string.IsNullOrEmpty(SystemUrl))
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error, "No system_url value defined in config or SIMPL Bridge.  Unable to connect to Mobile Control.");
                    return String.Empty;
                }

                var result = Regex.Match(SystemUrl, @"https?:\/\/.*\/systems\/(.*)\/#.*");
                string uuid = result.Groups[1].Value;
                return uuid;
            }
        }

        public BoolFeedback ApiOnlineAndAuthorized { get; private set; }

        /// <summary>
        /// Used for tracking HTTP debugging
        /// </summary>
        private bool _httpDebugEnabled;


        private bool _isAuthorized;
        /// <summary>
        /// Tracks if the system is authorized to the API server
        /// </summary>
        public bool IsAuthorized
        {
            get
            {
                return _isAuthorized;
            }
            private set
            {
                if (value == _isAuthorized)
                    return;

                _isAuthorized = value;
                ApiOnlineAndAuthorized.FireUpdate();
            }
        }

        private DateTime _lastAckMessage;

        private CTimer _pingTimer;

        /// <summary>
        /// Prevents post operations from stomping on each other and getting lost
        /// </summary>
        private CEvent _postLockEvent = new CEvent(true, true);

        private CEvent _registerLockEvent = new CEvent(true, true);

        private CTimer _serverReconnectTimer;
        private LogLevel _wsLogLevel = LogLevel.Error;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="config"></param>
        public MobileControlSystemController(string key, string name, MobileControlConfig config)
            : base(key, name)
        {
            Config = config;

            // The queue that will collect the incoming messages in the order they are received
            //_receiveQueue = new ReceiveQueue(key, ParseStreamRx);
            _receiveQueue = new GenericQueue(key + "-rxqueue", Crestron.SimplSharpPro.CrestronThread.Thread.eThreadPriority.HighPriority, 25);

            // The queue that will collect the outgoing messages in the order they are received
            _transmitQueue = new GenericQueue(key + "-txqueue", Crestron.SimplSharpPro.CrestronThread.Thread.eThreadPriority.HighPriority, 25);

            Host = config.ServerUrl;
            if (!Host.StartsWith("http"))
            {
                Host = "https://" + Host;
            }

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
            CrestronConsole.AddNewConsoleCommand(s =>
            {
                _disableReconnect = false;
                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "User command: {0}", "mobileConnect");
                ConnectWebsocketClient();
            }, "mobileconnect",
                "Forces connect of websocket", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(s =>
            {
                _disableReconnect = true;
                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "User command: {0}", "mobileDisco");
                CleanUpWebsocketClient();
            }, "mobiledisco",
                "Disconnects websocket", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(ParseStreamRx, "mobilesimulateaction",
                "Simulates a message from the server", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(SetWebsocketDebugLevel, "mobilewsdebug", "Set Websocket debug level",
                ConsoleAccessLevelEnum.AccessProgrammer);

            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;

            // Config Messenger
            var cmKey = Key + "-config";
            ConfigMessenger = new ConfigMessenger(cmKey, "/config");
            ConfigMessenger.RegisterWithAppServer(this);

            ApiOnlineAndAuthorized = new BoolFeedback(() => {
                if(_wsClient2 == null)
                    return false;

                return _wsClient2.IsAlive && IsAuthorized;
            });

            CreateMobileControlRoomBridges();

            AddPreActivationAction(GetRoomCombiner);
        }

        public MobileControlConfig Config { get; private set; }

        public string Host { get; private set; }
        public ConfigMessenger ConfigMessenger { get; private set; }

        private void GetRoomCombiner()
        {
            var combiners = DeviceManager.AllDevices.OfType<IEssentialsRoomCombiner>().ToList();

            if (combiners.Count > 1)
            {
                //TODO: Replace `index 0` with room key
                Debug.Console(0, this, Debug.ErrorLogLevel.Warning, "Multiple room combiners found. Using combiner at index 0");
                _roomCombiner = combiners[0];

                _roomCombiner.RoomCombinationScenarioChanged += RoomCombinerOnRoomCombinationScenarioChanged;
                return;
            }

            if (combiners.Count > 0)
            {
                _roomCombiner = combiners[0];

                _roomCombiner.RoomCombinationScenarioChanged += RoomCombinerOnRoomCombinationScenarioChanged;
                return;
            }

            Debug.Console(0, this, Debug.ErrorLogLevel.Notice, "No room combining configured for this system");
        }

        private void RoomCombinerOnRoomCombinationScenarioChanged(object sender, EventArgs eventArgs)
        {
            foreach (var bridge in _roomBridges.OfType<MobileControlEssentialsRoomBridge>())
            {
                if (!_roomCombiner.CurrentScenario.UiMap.ContainsKey(bridge.Key))
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error,
                        "No room found for {0} in scenario {1}. Please correct configuration!", bridge.Key,
                        _roomCombiner.CurrentScenario.Key);
                    continue;
                }

                var roomKey = _roomCombiner.CurrentScenario.UiMap[bridge.Key];

                var room = DeviceManager.GetDeviceForKey(roomKey) as IEssentialsRoom;

                if (room == null)
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error, "No room with key {0} found!", roomKey);
                    continue;
                }

                bridge.ChangeRoom(room);
            }
        }

        private void CreateMobileControlRoomBridges()
        {
            if (Config.RoomBridges.Count == 0)
            {
                Debug.Console(0, this, "No Room bridges configured explicitly. Bridges will be created for each configured room.");
                return;
            }

            foreach (var bridge in Config.RoomBridges.Select(bridgeConfig => new MobileControlEssentialsRoomBridge(bridgeConfig.Key, bridgeConfig.RoomKey)))
            {
                AddBridgePostActivationAction(bridge);
                DeviceManager.AddDevice(bridge);
            }
        }

        #region IMobileControl Members

        public void CreateMobileControlRoomBridge(IEssentialsRoom room, IMobileControl parent)
        {
            if (Config.RoomBridges.Count > 0)
            {
                Debug.Console(0, this, "Room Bridges configured explicitly. Skipping creation.");
                return;
            }

            var bridge = new MobileControlEssentialsRoomBridge(room);
            AddBridgePostActivationAction(bridge);
            DeviceManager.AddDevice(bridge);
        }

        public static IMobileControl GetAppServer()
        {
            try
            {
                var appServer = DeviceManager.GetDevices().SingleOrDefault(s => s is IMobileControl) as MobileControlSystemController;
                return appServer;
            }
            catch (Exception e)
            {
                Debug.Console(0, "Unable to find MobileControlSystemController in Devices: {0}", e);
                return null;
            }
        }

        /// <summary>
        /// Generates the url and creates the websocket client
        /// </summary>
        private bool CreateWebsocket()
        {
            if (_wsClient2 != null)
            {
                _wsClient2.Close();
                _wsClient2 = null;
            }

            if (String.IsNullOrEmpty(SystemUuid))
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Error, "System UUID not defined. Unable to connect to Mobile Control");
                return false;
            }

            var wsHost = Host.Replace("http", "ws");
            var url = string.Format("{0}/system/join/{1}", wsHost, SystemUuid);

            _wsClient2 = new WebSocket(url)
            {
                Log =
                {
                    Output =
                        (data, s) => Debug.Console(1, Debug.ErrorLogLevel.Notice, "Message from websocket: {0}", data)
                }
            };

            _wsClient2.OnMessage += HandleMessage;
            _wsClient2.OnOpen += HandleOpen;
            _wsClient2.OnError += HandleError;
            _wsClient2.OnClose += HandleClose;

            return true;
        }

        public void LinkSystemMonitorToAppServer()
        {
            if (CrestronEnvironment.DevicePlatform != eDevicePlatform.Appliance)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Notice,
                    "System Monitor does not exist for this platform. Skipping...");
                return;
            }

            var sysMon = DeviceManager.GetDeviceForKey("systemMonitor") as SystemMonitorController;

            var appServer = GetAppServer() as MobileControlSystemController;

            if (sysMon == null || appServer == null)
            {
                return;
            }

            var key = sysMon.Key + "-" + appServer.Key;
            var messenger = new SystemMonitorMessenger(key, sysMon, "/device/systemMonitor");

            messenger.RegisterWithAppServer(appServer);

            DeviceManager.AddDevice(messenger);
        }

        #endregion

        private void SetWebsocketDebugLevel(string cmdparameters)
        {
            if (CrestronEnvironment.ProgramCompatibility == eCrestronSeries.Series4)
            {
                Debug.Console(0, this, "Setting websocket log level not currently allowed on 4 series.");
                return;  // Web socket log level not currently allowed in series4
            }

            if (String.IsNullOrEmpty(cmdparameters))
            {
                Debug.Console(0, this, "Current Websocket debug level: {0}", _wsLogLevel);
                return;
            }

            if (cmdparameters.ToLower().Contains("help") || cmdparameters.ToLower().Contains("?"))
            {
                Debug.Console(0, this, "valid options are:\r\n{0}\r\n{1}\r\n{2}\r\n{3}\r\n{4}\r\n{5}\r\n",
                    LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal);
            }

            try
            {
                var debugLevel = (LogLevel) Enum.Parse(typeof (LogLevel), cmdparameters, true);

                _wsLogLevel = debugLevel;

                if (_wsClient2 != null)
                {
                    _wsClient2.Log.Level = _wsLogLevel;
                }
 

                Debug.Console(0, this, "Websocket log level set to {0}", debugLevel);
            }
            catch
            {
                Debug.Console(0, this, "{0} is not a valid debug level. Valid options are: {1}, {2}, {3}, {4}, {5}, {6}",cmdparameters,
                    LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal);
            }
        }

        private void AddBridgePostActivationAction(MobileControlBridgeBase bridge)
        {
            bridge.AddPostActivationAction(() =>
            {
                Debug.Console(0, bridge, "Linking to parent controller");
                bridge.AddParent(this);
                AddBridge(bridge);
            });
        }

        /// <summary>
        /// If config rooms is empty or null then go
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            if (ConfigReader.ConfigObject.Rooms != null && ConfigReader.ConfigObject.Rooms.Count != 0)
            {
                return base.CustomActivate();
            }

            if (_roomBridges.OfType<IDelayedConfiguration>().ToList().Count > 0)
            {
                return base.CustomActivate();
            }

            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Config contains no rooms.  Registering with Server.");
            RegisterSystemToServer();

            return base.CustomActivate();
        }

        /// <summary>
        /// Sends message to server to indicate the system is shutting down
        /// </summary>
        /// <param name="programEventType"></param>
        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping || _wsClient2 == null || !_wsClient2.IsAlive)
            {
                return;
            }

            _disableReconnect = true;

            StopServerReconnectTimer();
            CleanUpWebsocketClient();
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
            {
                _actionDictionary.Remove(key);
            }
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

            // send the configuration object to the server

            if (_wsClient2 == null)
            {
                RegisterSystemToServer();
            }
            else if (!_wsClient2.IsAlive)
            {
                ConnectWebsocketClient();
            }
            else
            {
                SendInitialMessage();
            }
               
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        private void ReconnectToServerTimerCallback(object o)
        {
            Debug.Console(1, this, "Attempting to reconnect to server...");
            //RegisterSystemToServer();

            ConnectWebsocketClient();
        }

        /// <summary>
        /// Verifies system connection with servers
        /// </summary>
        private void AuthorizeSystem(string code)
        {
            if (string.IsNullOrEmpty(SystemUuid) || SystemUuid.Equals("missing url", StringComparison.OrdinalIgnoreCase))
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
                {
                    DispatchHttpsAuthorizationRequest(url);
                }
                else
                {
                    var req = new HttpClientRequest();
                    req.Url.Parse(url);

                    var c = new HttpClient {AllowAutoRedirect = false};
                    c.DispatchAsync(req, (r, e) =>
                    {
                        CheckHttpDebug(r, e);
                        if (e == HTTP_CALLBACK_ERROR.COMPLETED)
                        {
                            switch (r.Code)
                            {
                                case 200:
                                    Debug.Console(0, "System authorized, sending config.");
                                    RegisterSystemToServer();
                                    break;
                                case 404:
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
                                    break;
                                case 301:
                                {
                                    var newUrl = r.Header.GetHeaderValue("Location");
                                    var newHostValue = newUrl.Substring(0,
                                        newUrl.IndexOf(path, StringComparison.Ordinal));
                                    Debug.Console(0, this,
                                        "ERROR: Mobile control API has moved. Please adjust configuration to \"{0}\"",
                                        newHostValue);
                                }
                                    break;
                                default:
                                    Debug.Console(0, "http authorization failed, code {0}: {1}", r.Code, r.ContentString);
                                    break;
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

            var c = new HttpsClient {HostVerification = false, PeerVerification = false, Verbose = true};

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
                {
                    Debug.Console(0, "content: {0}", r.ContentString);
                }

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
                {
                    Debug.Console(0, "content {0}", r.ContentString);
                }

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
                {
                    Debug.Console(0, "Content {0}", r.ContentString);
                }
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
            var result = CreateWebsocket();

            if (!result)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Error, "Unable to create websocket.");
                return;
            }

            ConnectWebsocketClient();
        }

        /// <summary>
        /// Connects the Websocket Client
        /// </summary>
        private void ConnectWebsocketClient()
        {
            try
            {
                _wsCriticalSection.Enter();


                // set to 99999 to let things work on 4-Series
                if ((CrestronEnvironment.ProgramCompatibility & eCrestronSeries.Series4) == eCrestronSeries.Series4)
                {
                    _wsClient2.Log.Level = (LogLevel) 99999;
                }
                else if ((CrestronEnvironment.ProgramCompatibility & eCrestronSeries.Series3) == eCrestronSeries.Series3)
                {
                    _wsClient2.Log.Level = _wsLogLevel;
                }

                //_wsClient2.Log.Level = _wsLogLevel;

                //This version of the websocket client is TLS1.2 ONLY

                //Fires OnMessage event when PING is received.
                _wsClient2.EmitOnPing = true;

                Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Connecting mobile control client to {0}", _wsClient2.Url);

                TryConnect();
            }
            finally
            {
                _wsCriticalSection.Leave();
            }
        }

        /// <summary>
        /// Attempts to connect the websocket
        /// </summary>
        private void TryConnect()
        {
            try
            {
                IsAuthorized = false;
                _wsClient2.Connect();
            }
            catch (InvalidOperationException)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Maximum retries exceeded. Restarting websocket");
                HandleConnectFailure();
            }
            catch (IOException ex)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Error, "IO Exception\r\n{0}", ex);
                HandleConnectFailure();
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Error on Websocket Connect: {0}\r\nStack Trace: {1}",
                    ex.Message, ex.StackTrace);
                HandleConnectFailure();
            }
        }

        /// <summary>
        /// Gracefully handles conect failures by reconstructing the ws client and starting the reconnect timer
        /// </summary>
        private void HandleConnectFailure()
        {
            _wsClient2 = null;

            var wsHost = Host.Replace("http", "ws");
            var url = string.Format("{0}/system/join/{1}", wsHost, SystemUuid);
            _wsClient2 = new WebSocket(url)
            {
                Log =
                {
                    Output =
                        (data, s) => Debug.Console(1, Debug.ErrorLogLevel.Notice, "Message from websocket: {0}", data)
                }
            };


            _wsClient2.OnMessage -= HandleMessage;
            _wsClient2.OnOpen -= HandleOpen;
            _wsClient2.OnError -= HandleError;
            _wsClient2.OnClose -= HandleClose;

            _wsClient2.OnMessage += HandleMessage;
            _wsClient2.OnOpen += HandleOpen;
            _wsClient2.OnError += HandleError;
            _wsClient2.OnClose += HandleClose;

            StartServerReconnectTimer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleOpen(object sender, EventArgs e)
        {
            StopServerReconnectTimer();
            StartPingTimer();
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Mobile Control API connected");
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
            if (e.IsPing)
            {
                _lastAckMessage = DateTime.Now;
                IsAuthorized = true;
                ResetPingTimer();
                return;
            }

            if (e.IsText && e.Data.Length > 0)
            {
                _receiveQueue.Enqueue(new ProcessStringMessage(e.Data, ParseStreamRx));
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
            IsAuthorized = false;
            StartServerReconnectTimer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleClose(object sender, CloseEventArgs e)
        {
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Websocket close {0} {1}, clean={2}", e.Code, e.Reason, e.WasClean);
            IsAuthorized = false;
            StopPingTimer();

            // Start the reconnect timer only if disableReconnect is false and the code isn't 4200. 4200 indicates system is not authorized;
            if (_disableReconnect || e.Code == 4200)
            {
                return;
            }

            StartServerReconnectTimer();
        }

        /// <summary>
        /// After a "hello" from the server, sends config and stuff
        /// </summary>
        private void SendInitialMessage()
        {
            Debug.Console(1, this, "Sending initial join message");

            // Populate the application name and version number
            var confObject = new MobileControlEssentialsConfig(ConfigReader.ConfigObject);

            confObject.Info.RuntimeInfo.AppName = Assembly.GetExecutingAssembly().GetName().Name;

            var essentialsVersion = Global.AssemblyVersion;
            confObject.Info.RuntimeInfo.AssemblyVersion = essentialsVersion;

            // Populate the plugin version 
            var pluginVersion =
                Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof (AssemblyInformationalVersionAttribute), false);

            var fullVersionAtt = pluginVersion[0] as AssemblyInformationalVersionAttribute;

            if (fullVersionAtt != null)
            {
                var pluginInformationalVersion = fullVersionAtt.InformationalVersion;

                confObject.RuntimeInfo.PluginVersion = pluginInformationalVersion;
            }

            var msg = new
            {
                type = "join",
                content = new
                {
                    config = confObject,
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
            _transmitQueue.Enqueue(new TransmitMessage(o, _wsClient2));
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
                    Debug.Console(2, this, "Message TX: {0}", message);
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
            if (_wsClient2 == null)
            {
                return;
            }

            Debug.Console(1, this, "Disconnecting websocket");

            _wsClient2.Close();
        }

        private void ResetPingTimer()
        {
            // This tells us we're online with the API and getting pings
            _pingTimer.Reset(PingInterval);
        }

        private void StartPingTimer()
        {
            StopPingTimer();
            _pingTimer = new CTimer(PingTimerCallback, null, PingInterval);
        }

        private void StopPingTimer()
        {
            if (_pingTimer == null)
            {
                return;
            }

            _pingTimer.Stop();
            _pingTimer.Dispose();
            _pingTimer = null;
        }

        private void PingTimerCallback(object o)
        {
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Ping timer expired. Closing websocket");

            try
            {
                _wsClient2.Close();
            }
            catch (Exception ex)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Exception closing websocket: {0}\r\nStack Trace: {1}", ex.Message, ex.StackTrace);

                HandleConnectFailure();
            }

            
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
            if (_serverReconnectTimer == null)
            {
                return;
            }
            _serverReconnectTimer.Stop();
            _serverReconnectTimer = null;
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
            if (code == null)
            {
                return;
            }

            foreach (var b in _roomBridges)
            {
                b.SetUserCode(code.Value<string>());
            }
        }

        private void HandleClientJoined(JToken content)
        {
            var clientId = content["clientId"].Value<string>();
            var roomKey = content["roomKey"].Value<string>();

            if (_roomCombiner == null)
            {
                SendMessageObjectToServer(new
                {
                    type = "/system/roomKey",
                    clientId,
                    content = roomKey
                });
                return;
            }

            if (!_roomCombiner.CurrentScenario.UiMap.ContainsKey(roomKey))
            {
                Debug.Console(0, this,
                    "Unable to find correct roomKey for {0} in current scenario. Returning {0} as roomKey", roomKey);

                SendMessageObjectToServer(new
                {
                    type = "/system/roomKey",
                    clientId,
                    content = roomKey
                });
                return;
            }

            var newRoomKey = _roomCombiner.CurrentScenario.UiMap[roomKey];

            SendMessageObjectToServer(new
            {
                type = "/system/roomKey",
                clientId,
                content = newRoomKey
            });
        }

        private void HandleUserCode(JToken content)
        {
            var code = content["userCode"];

            JToken qrChecksum;
            
            try
            {
                qrChecksum = content.SelectToken("qrChecksum", false);
            }
            catch
            {
                qrChecksum = new JValue(String.Empty);
            }

            Debug.Console(1, this, "QR checksum: {0}", qrChecksum == null ? String.Empty : qrChecksum.Value<string>());

            if (code == null)
            {
                return;
            }

            foreach (var bridge in _roomBridges)
            {
                bridge.SetUserCode(code.Value<string>(), qrChecksum.Value<string>());
            }
        }

        /// <summary>
        /// Outputs debug info when enabled
        /// </summary>
        /// <param name="r"></param>
        /// <param name="e"></param>
        private void CheckHttpDebug(HttpClientResponse r, HTTP_CALLBACK_ERROR e)
        {
            if (!_httpDebugEnabled)
            {
                return;
            }

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
            {
                return;
            }

            if (!message.Contains("/system/heartbeat"))
            {
                Debug.Console(2, this, "Message RX: {0}", message);
            }

            try
            {
                var messageObj = JObject.Parse(message);

                var type = messageObj["type"].Value<string>();

                switch (type)
                {
                    case "hello":
                        SendInitialMessage();
                        break;
                    case "/system/heartbeat":
                        HandleHeartBeat(messageObj["content"]);
                        break;
                    case "/system/userCode":
                        HandleUserCode(messageObj["content"]);
                        break;
                    case "/system/clientJoined":
                        HandleClientJoined(messageObj["content"]);
                        break;
                    case "raw":
                    {
                        var wrapper = messageObj["content"].ToObject<DeviceActionWrapper>();
                        DeviceJsonApi.DoDeviceAction(wrapper);
                    }
                        break;
                    case "close":
                        Debug.Console(1, this, "Received close message from server.");
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
                                                    {
                                                        pressAndHoldAction(false);
                                                    }
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

                                var respObj =
                                    (action as ClientSpecificUpdateRequest).ResponseMethod() as
                                        MobileControlResponseMessage;

                                if (respObj != null)
                                {
                                    respObj.ClientId = clientId;

                                    SendMessageObjectToServer(respObj);
                                }
                            }
                            else if (action is Action<PresetChannelMessage>)
                            {
                                (action as Action<PresetChannelMessage>)(
                                    messageObj["content"].ToObject<PresetChannelMessage>());
                            }
                            else if (action is Action<List<PresetChannel>> )
                            {
                                (action as Action<List<PresetChannel>>)(
                                    messageObj["content"].ToObject<List<PresetChannel>>());
                            }
                            else if (action is Action<List<ScheduledEventConfig>>)
                            {
                                (action as Action<List<ScheduledEventConfig>>)(
                                    messageObj["content"].ToObject<List<ScheduledEventConfig>>());
                            }
                            else if (action is Action<DirectRoute>)
                            {
                                (action as Action<DirectRoute>)(messageObj["content"].ToObject<DirectRoute>());
                            }
                            else if (action is Action<PepperDash.Essentials.Devices.Common.Codec.Meeting>)
                            {
                                (action as Action<Meeting>)(messageObj["content"].ToObject<Meeting>());
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
                    switch (tokens[0].ToLower())
                    {
                        case "get":
                        {
                            var resp = new HttpClient().Get(url);
                            CrestronConsole.ConsoleCommandResponse("RESPONSE:\r{0}\r\r", resp);
                        }
                            break;
                        case "post":
                        {
                            var resp = new HttpClient().Post(url, new byte[] {});
                            CrestronConsole.ConsoleCommandResponse("RESPONSE:\r{0}\r\r", resp);
                        }
                            break;
                        default:
                            CrestronConsole.ConsoleCommandResponse("Only get or post supported\r");
                            PrintTestHttpRequestUsage();
                            break;
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
        public ClientSpecificUpdateRequest(Func<object> func)
        {
            ResponseMethod = func;
        }

        public Func<object> ResponseMethod { get; private set; }
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