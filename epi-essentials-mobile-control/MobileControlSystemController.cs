﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.AppServer.Messengers;

namespace PepperDash.Essentials
{
    public class MobileControlSystemController : Device
    {
		//WebSocketClient WSClient;
		WebSocket WSClient2;

		//bool LinkUp;

		/// <summary>
		/// Prevents post operations from stomping on each other and getting lost
		/// </summary>
		CEvent PostLockEvent = new CEvent(true, true);

		CEvent RegisterLockEvent = new CEvent(true, true);

		public MobileControlConfig Config { get; private set; }

		public string Host { get; private set; }

        Dictionary<string, Object> ActionDictionary = new Dictionary<string, Object>(StringComparer.InvariantCultureIgnoreCase);

        Dictionary<string, CTimer> PushedActions = new Dictionary<string, CTimer>();

        public ConfigMessenger ConfigMessenger { get; private set; }

        CTimer ServerHeartbeatCheckTimer;

        long ServerHeartbeatInterval = 20000;

        CTimer ServerReconnectTimer;

        long ServerReconnectInterval = 5000;

        DateTime LastAckMessage;

        public string SystemUuid;

		List<MobileControlBridgeBase> RoomBridges = new List<MobileControlBridgeBase>();

        long ButtonHeartbeatInterval = 1000;

		/// <summary>
		/// Used for tracking HTTP debugging
		/// </summary>
		bool HttpDebugEnabled;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		/// <param name="name"></param>
		/// <param name="config"></param>
        public MobileControlSystemController(string key, string name, MobileControlConfig config) : base(key, name)
        {
            Config = config;

			Host = config.ServerUrl;
			if (!Host.StartsWith("http"))
			{
				Host = "https://" + Host;
			}

            SystemUuid = ConfigReader.ConfigObject.SystemUuid;

			Debug.Console(0, this, "Mobile UI controller initializing for server:{0}", config.ServerUrl);

			CrestronConsole.AddNewConsoleCommand(AuthorizeSystem,
				"mobileauth", "Authorizes system to talk to Mobile Control server", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => ShowInfo(),
				"mobileinfo", "Shows information for current mobile control session", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => { 
					s = s.Trim();
					if(!string.IsNullOrEmpty(s))
					{
						HttpDebugEnabled = (s.Trim() != "0"); 
					}
					CrestronConsole.ConsoleCommandResponse("HTTP Debug {0}", HttpDebugEnabled ? "Enabled" : "Disabled");
				},
				"mobilehttpdebug", "1 enables more verbose HTTP response debugging", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(TestHttpRequest,
			"mobilehttprequest", "Tests an HTTP get to URL given", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(PrintActionDictionaryPaths, "mobileshowactionpaths", 
				"Prints the paths in the Action Dictionary", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => ConnectWebsocketClient(), "mobileconnect", 
				"Forces connect of websocket", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => CleanUpWebsocketClient(), "mobiledisco",
				"Disconnects websocket", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(s => ParseStreamRx(s), "mobilesimulateaction", "Simulates a message from the server", ConsoleAccessLevelEnum.AccessOperator);

            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(CrestronEnvironment_ProgramStatusEventHandler);
			//CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);

            // Config Messenger
            var cmKey = Key + "-config";
            ConfigMessenger = new ConfigMessenger(cmKey, "/config");
            ConfigMessenger.RegisterWithAppServer(this);			
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
        void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType == eProgramStatusEventType.Stopping 
				&& WSClient2 != null
				&& WSClient2.IsAlive)
				//&& WSClient != null
				//&& WSClient.Connected)
            {
				CleanUpWebsocketClient();
            }
        }

        public void PrintActionDictionaryPaths(object o)
        {
            Debug.Console(0, this, "ActionDictionary Contents:");

            foreach (var item in ActionDictionary)
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
            if (!ActionDictionary.ContainsKey(key))
            {
                ActionDictionary.Add(key, action);
            }
            else
            {
                Debug.Console(1, this, "Cannot add action with key '{0}' because key already exists in ActionDictionary.", key);
            }
        }

        /// <summary>
        /// Removes an action from the dictionary
        /// </summary>
        /// <param name="key"></param>
        public void RemoveAction(string key)
        {
            if (ActionDictionary.ContainsKey(key))
                ActionDictionary.Remove(key);
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="bridge"></param>
		public void AddBridge(MobileControlBridgeBase bridge)
		{
			RoomBridges.Add(bridge);
			var b = bridge as IDelayedConfiguration;
			if (b != null)
			{
				Debug.Console(0, this, "Adding room bridge with delayed configuration");
				b.ConfigurationIsReady += new EventHandler<EventArgs>(bridge_ConfigurationIsReady);
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
		void bridge_ConfigurationIsReady(object sender, EventArgs e)
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
        void ReconnectToServerTimerCallback(object o)
        {
            RegisterSystemToServer();
        }

		/// <summary>
		/// Verifies system connection with servers
		/// </summary>
		/// <param name="command"></param>
		void AuthorizeSystem(string code)
		{
			if (string.IsNullOrEmpty(SystemUuid))
			{
				CrestronConsole.ConsoleCommandResponse("System does not have a UUID. Please ensure proper configuration is loaded and restart.");
				return;
			}
			if (string.IsNullOrEmpty(code))
			{
				CrestronConsole.ConsoleCommandResponse("Please enter a user code to authorize a system");
				return;
			}
			if (string.IsNullOrEmpty(Config.ServerUrl))
			{
				CrestronConsole.ConsoleCommandResponse("Mobile control API address is not set.  Check portal configuration");
				return;
			}


			try
			{
				string path = string.Format("/api/system/grantcode/{0}/{1}", code, SystemUuid);
				string url = string.Format("{0}{1}", Host, path);
				Debug.Console(0, this, "Authorizing to: {0}", url);

				if (Host.StartsWith("https:"))
				{
					var req = new HttpsClientRequest();
					req.Url.Parse(url);
					var c = new HttpsClient();

					Debug.Console(0, "  host and peer verification disabled");
					c.HostVerification = false;
					c.PeerVerification = false;
					c.Verbose = true;


					c.DispatchAsync(req, (r, e) =>
					{
						if (e == HTTPS_CALLBACK_ERROR.COMPLETED)
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
									Debug.Console(0, "Authorization failed, code not found for system UUID {0}", SystemUuid);
								}
								else if (r.ContentString.Contains("uuidNotFound"))
								{
									Debug.Console(0, "Authorization failed, uuid {0} not found. Check Essentials configuration is correct",
										SystemUuid);
								}
							}
							else
							{
								Debug.Console(0, "https authorization failed, code {0}: {1}", r.Code, r.ContentString);
							}
						}
						else
						{
							if (r != null)
							{
								Debug.Console(0, this, "Error in https authorization (A) {0}: {1}", r.Code, e);
							}
							else
							{
								Debug.Console(0, this, "Error in https authorization (B) {0}", e);
							}
						}
					});
				}

				else
				{
					var req = new HttpClientRequest();
					req.Url.Parse(url);

					var c = new HttpClient();
					c.AllowAutoRedirect = false;
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
									Debug.Console(0, "Authorization failed, code not found for system UUID {0}", SystemUuid);
								}
								else if (r.ContentString.Contains("uuidNotFound"))
								{
									Debug.Console(0, "Authorization failed, uuid {0} not found. Check Essentials configuration is correct",
										SystemUuid);
								}
							}
							else
							{
								if (r.Code == 301)
								{
									var newUrl = r.Header.GetHeaderValue("Location");
									var newHostValue = newUrl.Substring(0, newUrl.IndexOf(path));
									Debug.Console(0, this, "ERROR: Mobile control API has moved. Please adjust configuration to \"{0}\"", newHostValue);
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
				Debug.Console(0, this, "Error in authorizing (C): {0}", e);
			}
		}

		/// <summary>
		/// Dumps info in response to console command.
		/// </summary>
		void ShowInfo()
		{
			var url = Config != null ? Host : "No config";
			string name;
			string code;
			if (RoomBridges != null && RoomBridges.Count > 0)
			{
				name = RoomBridges[0].RoomName;
				code = RoomBridges[0].UserCode;
			}
			else
			{
				name = "No config";
				code = "Not available";
			}
			//var conn = WSClient == null ? "No client" : (WSClient.Connected ? "Yes" : "No");
			var conn = WSClient2 == null ? "No client" : (WSClient2.IsAlive ? "Yes" : "No");

            var secSinceLastAck = DateTime.Now - LastAckMessage;


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
        /// <param name="url">URL of the server, including the port number, if not 80.  Format: "serverUrlOrIp:port"</param>
        void RegisterSystemToServer()
        {
			ConnectWebsocketClient();
        }

		/// <summary>
		/// Connects the Websocket Client
		/// </summary>
		/// <param name="o"></param>
		void ConnectWebsocketClient()
		{
			CleanUpWebsocketClient();
			var wsHost = Host.Replace("http", "ws");
			var url = string.Format("{0}/system/join/{1}", wsHost, this.SystemUuid);
			WSClient2 = new WebSocket(url);
			WSClient2.Log.Output = (ld, s) => {
				Debug.Console(1, this, "Message from websocket: {0}", ld);
			};
			WSClient2.OnMessage += HandleMessage;
			WSClient2.OnOpen += HandleOpen;
			WSClient2.OnError += this.HandleError;
			WSClient2.OnClose += this.HandleClose;	
			Debug.Console(1, this, "Initializing mobile control client to {0}", url);
			WSClient2.Connect();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void HandleOpen(object sender, EventArgs e)
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
		void HandleMessage(object sender, MessageEventArgs e) 
		{
			if (e.IsText && e.Data.Length > 0)
			{
				ParseStreamRx(e.Data);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void HandleError(object sender, ErrorEventArgs e)
		{
			Debug.Console(1, this, "Websocket error {0}", e.Message);
			StartServerReconnectTimer();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void HandleClose(object sender, CloseEventArgs e)
		{
			Debug.Console(1, this, "Websocket close {0} {1}, clean={2}", e.Code, e.Reason, e.WasClean);
			if (ServerHeartbeatCheckTimer != null)
				ServerHeartbeatCheckTimer.Stop();
			// Start the reconnect timer
			StartServerReconnectTimer();
		}

		/// <summary>
		/// After a "hello" from the server, sends config and stuff
		/// </summary>
		void SendInitialMessage()
		{
			Debug.Console(1, this, "Sending initial join message");
			var confObject = ConfigReader.ConfigObject;
			confObject.Info.RuntimeInfo.AppName = Assembly.GetExecutingAssembly().GetName().Name;
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			confObject.Info.RuntimeInfo.AssemblyVersion = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);

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
			SendMessageToServer(JObject.FromObject(o));
		}

        /// <summary>
        /// Sends a message to the server from a room
        /// </summary>
        /// <param name="room">room from which the message originates</param>
        /// <param name="o">object to be serialized and sent in post body</param>
        public void SendMessageToServer(JObject o)
        {
			if (WSClient2 != null && WSClient2.IsAlive)
			{
                string message = JsonConvert.SerializeObject(o, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

				if (!message.Contains("/system/heartbeat"))
				{
					Debug.Console(1, this, "Message TX: {0}", message);
				}
				WSClient2.Send(message);
            }
			else if (WSClient2 == null)
			{
				Debug.Console(1, this, "Cannot send. No client.");
			}
        }

        /// <summary>
        /// Disconnects the Websocket Client and stops the heartbeat timer
        /// </summary>
        /// <param name="command"></param>
		void CleanUpWebsocketClient()
		{
			if (WSClient2 != null)
			{
				Debug.Console(1, this, "Disconnecting websocket");
				WSClient2.Close();
				WSClient2 = null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dueTime"></param>
		/// <param name="repeatTime"></param>
		void StartServerReconnectTimer()
		{
			StopServerReconnectTimer();
			ServerReconnectTimer = new CTimer(ReconnectToServerTimerCallback, ServerReconnectInterval);
			Debug.Console(1, this, "Reconnect Timer Started.");
		}

		/// <summary>
		/// Does what it says
		/// </summary>
		void StopServerReconnectTimer()
		{
			if (ServerReconnectTimer != null)
			{
				ServerReconnectTimer.Stop();
				ServerReconnectTimer = null;
			}
		}

        /// <summary>
        /// Executes when we don't get a heartbeat message in time.  Triggers reconnect.
        /// </summary>
        /// <param name="o">For CTimer callback. Not used</param>
        void HeartbeatExpiredTimerCallback(object o)
        {
			Debug.Console(1, this, "Heartbeat Timer Expired.");
			if (ServerHeartbeatCheckTimer != null)
            {
                ServerHeartbeatCheckTimer.Stop();
                ServerHeartbeatCheckTimer = null;
            }
			CleanUpWebsocketClient();
            StartServerReconnectTimer();
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="dueTime"></param>
		/// <param name="repeatTime"></param>
        void ResetOrStartHearbeatTimer()
        {
			if (ServerHeartbeatCheckTimer == null)
			{
				ServerHeartbeatCheckTimer = new CTimer(HeartbeatExpiredTimerCallback, null, ServerHeartbeatInterval, ServerHeartbeatInterval);
				Debug.Console(1, this, "Heartbeat Timer Started.");
			}
			else
			{
				ServerHeartbeatCheckTimer.Reset(ServerHeartbeatInterval, ServerHeartbeatInterval);
			}
        }

		/// <summary>
		/// Waits two and goes again
		/// </summary>
		void ReconnectStreamClient()
		{
			new CTimer(o => ConnectWebsocketClient(), 2000);
		}


		/// <summary>
		/// Resets reconnect timer and updates usercode
		/// </summary>
		/// <param name="content"></param>
		void HandleHeartBeat(JToken content)
		{
            SendMessageToServer(JObject.FromObject(new
            {
                type = "/system/heartbeatAck"
            }));

			var code = content["userCode"];
			if(code != null) 
			{
				foreach (var b in RoomBridges)
				{
					b.SetUserCode(code.Value<string>());
				}
			}
			ResetOrStartHearbeatTimer();
		}

		/// <summary>
		/// Outputs debug info when enabled
		/// </summary>
		/// <param name="req"></param>
		/// <param name="r"></param>
		/// <param name="e"></param>
		void CheckHttpDebug(HttpClientResponse r, HTTP_CALLBACK_ERROR e)
		{
			if (HttpDebugEnabled)
			{
				try
				{
					Debug.Console(0, this, "------ Begin HTTP Debug ---------------------------------------");
					if (r != null)
					{
						Debug.Console(0, this, "HTTP Response URL: {0}", r.ResponseUrl != null ? r.ResponseUrl.ToString() : "NONE");
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
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        void ParseStreamRx(string message)
		{
            if(string.IsNullOrEmpty(message))
                return;

			if (!message.Contains("/system/heartbeat"))
			{
				Debug.Console(1, this, "Message RX: {0}", message);
			}
			else
			{
				LastAckMessage = DateTime.Now;
			}

            try
            {
                var messageObj = JObject.Parse(message);

                var type = messageObj["type"].Value<string>();

                if (type == "hello")
                {
					SendInitialMessage();
                    ResetOrStartHearbeatTimer();
                }
				else if (type == "/system/heartbeat")
				{
					HandleHeartBeat(messageObj["content"]);
				}
				else if (type == "raw")
				{
					var wrapper = messageObj["content"].ToObject<DeviceActionWrapper>();
					DeviceJsonApi.DoDeviceAction(wrapper);
				}
				else if (type == "close")
				{
					Debug.Console(1, this, "Received close message from server.");
					// DisconnectWebsocketClient();

					if (ServerHeartbeatCheckTimer != null)
						ServerHeartbeatCheckTimer.Stop();
				}
				else
				{
					// Check path against Action dictionary
					if (ActionDictionary.ContainsKey(type))
					{
						var action = ActionDictionary[type];

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
											if (!PushedActions.ContainsKey(type))
											{
												PushedActions.Add(type, new CTimer(o =>
												{
													(action as PressAndHoldAction)(false);
													PushedActions.Remove(type);
												}, null, ButtonHeartbeatInterval, ButtonHeartbeatInterval));
											}
											// Maybe add an else to reset the timer
											break;
										}
									case "held":
										{
											if (PushedActions.ContainsKey(type))
											{
												PushedActions[type].Reset(ButtonHeartbeatInterval, ButtonHeartbeatInterval);
											}
											return;
										}
									case "false":
										{
											if (PushedActions.ContainsKey(type))
											{
												PushedActions[type].Stop();
												PushedActions.Remove(type);
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
					}
					else
					{
						Debug.Console(1, this, "-- Warning: Incoming message has no registered handler");
					}
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
		void TestHttpRequest(string s)
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
						var resp = new HttpClient().Post(url, new byte[] { });
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

		void PrintTestHttpRequestUsage()
		{
			CrestronConsole.ConsoleCommandResponse("Usage: mobilehttprequest:N get/post url\r");
		}
    }
}