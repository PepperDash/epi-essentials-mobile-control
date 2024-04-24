using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.Room.Config;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Cameras;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PepperDash.Essentials.Devices.Common.Room;
using IShades = PepperDash.Essentials.Core.Shades.IShades;
using ShadeBase = PepperDash.Essentials.Devices.Common.Shades.ShadeBase;
using PepperDash.Essentials.Devices.Common.TouchPanel;
using Crestron.SimplSharp;
using Volume = PepperDash.Essentials.Room.MobileControl.Volume;
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.Core.Lighting;
using PepperDash.Essentials.Core.Shades;





#if SERIES4
using PepperDash.Essentials.AppServer;
#endif

namespace PepperDash.Essentials
{
    public class MobileControlEssentialsRoomBridge : MobileControlBridgeBase
    {
        private List<JoinToken> _touchPanelTokens = new List<JoinToken>();
        public IEssentialsRoom Room { get; private set; }

        public string DefaultRoomKey { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public override string RoomName
        {
            get { return Room.Name; }
        }

        public override string RoomKey
        {
            get { return Room.Key; }
        }

        public MobileControlEssentialsRoomBridge(IEssentialsRoom room) :
            this($"mobileControlBridge-{room.Key}", room.Key, room)
        {
            Room = room;
        }

        public MobileControlEssentialsRoomBridge(string key, string roomKey, IEssentialsRoom room) : base(key, $"/room/{room.Key}", room as Device)
        {
            DefaultRoomKey = roomKey;

            AddPreActivationAction(GetRoom);
        }

#if SERIES4
        protected override void RegisterActions()
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            // we add actions to the messaging system with a path, and a related action. Custom action
            // content objects can be handled in the controller's LineReceived method - and perhaps other
            // sub-controller parsing could be attached to these classes, so that the systemController
            // doesn't need to know about everything.

            Debug.Console(0, this, "Registering Actions with AppServer");

            AddAction("/promptForCode", (id, content) => OnUserPromptedForCode());
            AddAction("/clientJoined", (id, content) => OnClientJoined());

            AddAction("/touchPanels", (id, content) => OnTouchPanelsUpdated(content));

            AddAction($"/userApp", (id, content) => OnUserAppUpdated(content));

            AddAction("/userCode", (id, content) =>
            {
                var msg = content.ToObject<UserCodeChangedContent>();

                SetUserCode(msg.UserCode, msg.QrChecksum ?? string.Empty);
            });


            // Source Changes and room off
            AddAction("/status", (id, content) =>
            {
                SendFullStatusForClientId(id, Room);
            });

            if (Room is IRunRouteAction routeRoom)
                AddAction("/source", (id, content) =>
                {

                    var msg = content.ToObject<SourceSelectMessageContent>();

                    Debug.Console(2, this, "Received request to route to source: {0} on list: {1}", msg.SourceListItemKey, msg.SourceListKey);

                    routeRoom.RunRouteAction(msg.SourceListItemKey, msg.SourceListKey);
                });

            if (Room is IRunDirectRouteAction directRouteRoom)
            {
                AddAction("/directRoute", (id, content) =>
                {
                    var msg = content.ToObject<DirectRoute>();


                    Debug.Console(2, this, $"Running direct route from {msg.SourceKey} to {msg.DestinationKey} with signal type {msg.SignalType}");

                    directRouteRoom.RunDirectRoute(msg.SourceKey, msg.DestinationKey, msg.SignalType);
                });
            }


            if (Room is IRunDefaultPresentRoute defaultRoom)
                AddAction("/defaultsource", (id, content) => defaultRoom.RunDefaultPresentRoute());

            if (Room is IHasCurrentVolumeControls volumeRoom)
            {
                AddAction("/volumes/master/level", (id, content) =>
                {
                    var msg = content.ToObject<MobileControlSimpleContent<ushort>>();


                    if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback basicVolumeWithFeedback)
                        basicVolumeWithFeedback.SetVolume(msg.Value);
                });

                AddAction("/volumes/master/muteToggle", (id, content) => volumeRoom.CurrentVolumeControls.MuteToggle());

                AddAction("/volumes/master/muteOn", (id, content) =>
                {
                    if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback basicVolumeWithFeedback)
                        basicVolumeWithFeedback.MuteOn();
                });

                AddAction("/volumes/master/muteOff", (id, content) =>
                {
                    if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback basicVolumeWithFeedback)
                        basicVolumeWithFeedback.MuteOff();
                });

                AddAction("/volumes/master/volumeUp", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) =>
                    {
                        if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback basicVolumeWithFeedback)
                        {
                            basicVolumeWithFeedback.VolumeUp(b);
                        }
                    }
                ));

                AddAction("/volumes/master/volumeDown", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) =>
                {
                    if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback basicVolumeWithFeedback)
                    {
                        basicVolumeWithFeedback.VolumeDown(b);
                    }
                }
                ));

                volumeRoom.CurrentVolumeDeviceChange += Room_CurrentVolumeDeviceChange;

                // Registers for initial volume events, if possible
                if (volumeRoom.CurrentVolumeControls is IBasicVolumeWithFeedback currentVolumeDevice)
                {
                    Debug.Console(2, this, "Registering for volume feedback events");

                    currentVolumeDevice.MuteFeedback.OutputChange += MuteFeedback_OutputChange;
                    currentVolumeDevice.VolumeLevelFeedback.OutputChange += VolumeLevelFeedback_OutputChange;
                }
            }

            if (Room is IHasCurrentSourceInfoChange sscRoom)
                sscRoom.CurrentSourceChange += Room_CurrentSingleSourceChange;

            if (Room is IEssentialsHuddleVtc1Room vtcRoom)
            {
                if (vtcRoom.ScheduleSource != null)
                {
                    var key = vtcRoom.Key + "-" + Key;

                    if (!AppServerController.CheckForDeviceMessenger(key))
                    {
                        var scheduleMessenger = new IHasScheduleAwarenessMessenger(key, vtcRoom.ScheduleSource,
                            $"/room/{vtcRoom.Key}");
                        AppServerController.AddDeviceMessenger(scheduleMessenger);
                    }
                }

                vtcRoom.InCallFeedback.OutputChange += InCallFeedback_OutputChange;
            }

            if (Room is IPrivacy privacyRoom)
            {
                AddAction("/volumes/master/privacyMuteToggle", (id, content) => privacyRoom.PrivacyModeToggle());

                privacyRoom.PrivacyModeIsOnFeedback.OutputChange += PrivacyModeIsOnFeedback_OutputChange;
            }

            //SetupDeviceMessengers();

            if (Room is IRunDefaultCallRoute defCallRm)
            {
                AddAction("/activityVideo", (id, content) => defCallRm.RunDefaultCallRoute());
            }

            AddAction("/shutdownStart", (id, content) => Room.StartShutdown(eShutdownType.Manual));

            AddAction("/shutdownEnd", (id, content) => Room.ShutdownPromptTimer.Finish());

            AddAction("/shutdownCancel", (id, content) => Room.ShutdownPromptTimer.Cancel());

            Room.OnFeedback.OutputChange += OnFeedback_OutputChange;
            Room.IsCoolingDownFeedback.OutputChange += IsCoolingDownFeedback_OutputChange;
            Room.IsWarmingUpFeedback.OutputChange += IsWarmingUpFeedback_OutputChange;

            Room.ShutdownPromptTimer.HasStarted += ShutdownPromptTimer_HasStarted;
            Room.ShutdownPromptTimer.HasFinished += ShutdownPromptTimer_HasFinished;
            Room.ShutdownPromptTimer.WasCancelled += ShutdownPromptTimer_WasCancelled;

            AddTechRoomActions();
        }

        private void OnTouchPanelsUpdated(JToken content)
        {
            var message = content.ToObject<ApiTouchPanelToken>();

            _touchPanelTokens = message.TouchPanels;

            UpdateTouchPanelAppUrls(message.UserAppUrl);
        }

        private void UpdateTouchPanelAppUrls(string userAppUrl)
        {
            foreach (var tp in _touchPanelTokens)
            {
                var dev = DeviceManager.AllDevices.OfType<MobileControlTouchpanelController>().FirstOrDefault((tpc) => tpc.Key.Equals(tp.TouchpanelKey, StringComparison.InvariantCultureIgnoreCase));

                if (dev == null)
                {
                    continue;
                }

                var lanAdapterId = CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter);

                var processorIp = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, lanAdapterId);

                UpdateAppUrl($"{userAppUrl}?token={tp.Token}");
            }
        }

        private void OnUserAppUpdated(JToken content)
        {
            var message = content.ToObject<ApiTouchPanelToken>();

            UpdateTouchPanelAppUrls(message.UserAppUrl);
        }

        private void InCallFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage
            {
                IsInCall = e.BoolValue
            };
            PostStatusMessage(state);
        }

        private void GetRoom()
        {
            if (Room != null)
            {
                Debug.Console(0, this, "Room with key {0} already linked.", DefaultRoomKey);
                return;
            }


            if (!(DeviceManager.GetDeviceForKey(DefaultRoomKey) is IEssentialsRoom tempRoom))
            {
                Debug.Console(0, this, "Room with key {0} not found or is not an Essentials Room", DefaultRoomKey);
                return;
            }

            Room = tempRoom;
        }

        protected override void UserCodeChange()
        {
            Debug.Console(1, this, "Server user code changed: {0}", UserCode);

            var qrUrl = string.Format("{0}/rooms/{1}/{3}/qr?x={2}", Parent.Host, Parent.SystemUuid, new Random().Next(), DefaultRoomKey);
            QrCodeUrl = qrUrl;

            Debug.Console(1, this, "Server user code changed: {0} - {1}", UserCode, qrUrl);

            OnUserCodeChanged();
        }

        /*        /// <summary>
                /// Override of base: calls base to add parent and then registers actions and events.
                /// </summary>
                /// <param name="parent"></param>
                public override void AddParent(MobileControlSystemController parent)
                {
                    base.AddParent(parent);

                }*/

        private void AddTechRoomActions()
        {
            if (!(Room is IEssentialsTechRoom techRoom))
            {
                return;
            }

            AddAction("/roomPowerOn", (id, content) => techRoom.RoomPowerOn());
            AddAction("/roomPowerOff", (id, content) => techRoom.RoomPowerOff());
        }

        private void PrivacyModeIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            var volumes = new Dictionary<string, Volume>
            {
                { "master",  new Volume("master")
                    {
                        PrivacyMuted = e.BoolValue
                    } 
                }
            };

            state.Volumes = volumes;

            PostStatusMessage(state);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsSharingFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            // sharing source 
            string shareText;
            bool isSharing;

            if (Room is IHasCurrentSourceInfoChange srcInfoRoom && (Room is IHasVideoCodec vcRoom && (vcRoom.VideoCodec.SharingContentIsOnFeedback.BoolValue && srcInfoRoom.CurrentSourceInfo != null)))
            {
                shareText = srcInfoRoom.CurrentSourceInfo.PreferredName;
                isSharing = true;
            }
            else
            {
                shareText = "None";
                isSharing = false;
            }

            var state = new RoomStateMessage
            {
                Share = new ShareState
                {
                    CurrentShareText = shareText,
                    IsSharing = isSharing
                }
            };

            PostStatusMessage(state);
        }

        /// <summary>
        /// Handler for cancelled shutdown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_WasCancelled(object sender, EventArgs e)
        {
            var roomStatus = new {state = "wasCancelled" };
            
            PostStatusMessage(JToken.FromObject(roomStatus));
        }

        /// <summary>
        /// Handler for when shutdown finishes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_HasFinished(object sender, EventArgs e)
        {
            var roomStatus = new { state= "hasFinished" };            

            PostStatusMessage(JToken.FromObject(roomStatus));
        }

        /// <summary>
        /// Handler for when shutdown starts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_HasStarted(object sender, EventArgs e)
        {
            var roomStatus = new
            {
                state = "hasStarted",
                duration = Room.ShutdownPromptTimer.SecondsToCount
            };

            PostStatusMessage(JToken.FromObject(roomStatus));
            // equivalent JS message:
            //	Post( { type: '/room/status/', content: { shutdown: 'hasStarted', duration: Room.ShutdownPromptTimer.SecondsToCount })
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsWarmingUpFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new 
            {
                isWarmingUp = e.BoolValue
            };

            PostStatusMessage(JToken.FromObject(state));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsCoolingDownFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new 
            {
                isCoolingDown = e.BoolValue
            };
            PostStatusMessage(JToken.FromObject(state));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new 
            {
                isOn = e.BoolValue
            };
            PostStatusMessage(JToken.FromObject(state));
        }

        private void Room_CurrentVolumeDeviceChange(object sender, VolumeDeviceChangeEventArgs e)
        {
            if (e.OldDev is IBasicVolumeWithFeedback)
            {
                var oldDev = e.OldDev as IBasicVolumeWithFeedback;
                oldDev.MuteFeedback.OutputChange -= MuteFeedback_OutputChange;
                oldDev.VolumeLevelFeedback.OutputChange -= VolumeLevelFeedback_OutputChange;
            }

            if (e.NewDev is IBasicVolumeWithFeedback)
            {
                var newDev = e.NewDev as IBasicVolumeWithFeedback;
                newDev.MuteFeedback.OutputChange += MuteFeedback_OutputChange;
                newDev.VolumeLevelFeedback.OutputChange += VolumeLevelFeedback_OutputChange;
            }
        }

        /// <summary>
        /// Event handler for mute changes
        /// </summary>
        private void MuteFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            var volumes = new Dictionary<string, Volume>
            {
                { "master", new Volume("master", e.BoolValue) }
            };

            state.Volumes = volumes;

            PostStatusMessage(state);
        }

        /// <summary>
        /// Handles Volume changes on room
        /// </summary>
        private void VolumeLevelFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {

            var state = new
            {
                volumes = new Dictionary<string, Volume>
                {
                    { "master", new Volume("master", e.IntValue) }
                }
            };
            PostStatusMessage(JToken.FromObject(state));
        }


        private void Room_CurrentSingleSourceChange(SourceListItem info, ChangeType type)
        {
            /* Example message
             * {
                  "type":"/room/status",
                  "content": {
                    "selectedSourceKey": "off",
                  }
                }
             */
            
        }

        /// <summary>
        /// Sends the full status of the room to the server
        /// </summary>
        /// <param name="room"></param>
        private void SendFullStatusForClientId(string id, IEssentialsRoom room)
        {
            //Parent.SendMessageObject(GetFullStatus(room));
            var message = GetFullStatusForClientId(room);
            PostStatusMessage(message, id);
        }


        /// <summary>
        /// Gets full room status
        /// </summary>
        /// <param name="room">The room to get status of</param>
        /// <returns>The status response message</returns>
        private RoomStateMessage GetFullStatusForClientId(IEssentialsRoom room)
        {
            Debug.Console(2, this, "GetFullStatus");

            var sourceKey = room is IHasCurrentSourceInfoChange ? (room as IHasCurrentSourceInfoChange).CurrentSourceInfoKey : null;

            var volumes = new Dictionary<string, Volume>();
            if (room is IHasCurrentVolumeControls rmVc)
            {
                if (rmVc.CurrentVolumeControls is IBasicVolumeWithFeedback vc)
                {
                    var volume = new Volume("master", vc.VolumeLevelFeedback.UShortValue, vc.MuteFeedback.BoolValue, "Volume", true, "");
                    if (room is IPrivacy privacyRoom)
                    {
                        volume.HasPrivacyMute = true;
                        volume.PrivacyMuted = privacyRoom.PrivacyModeIsOnFeedback.BoolValue;
                    }

                    volumes.Add("master", volume);

                }
            }

            var state = new RoomStateMessage
            {
                Configuration = GetRoomConfiguration(room),
                ActivityMode = 1,
                IsOn = room.OnFeedback.BoolValue,
                SelectedSourceKey = sourceKey,
                Volumes = volumes,
                IsWarmingUp = room.IsWarmingUpFeedback.BoolValue,
                IsCoolingDown = room.IsCoolingDownFeedback.BoolValue
            };

            if (room is IEssentialsHuddleVtc1Room vtcRoom)
            {
                state.IsInCall = vtcRoom.InCallFeedback.BoolValue;
            }

            return state;
        }

        /// <summary>
        /// Determines the configuration of the room and the details about the devices associated with the room
        /// <param name="room"></param>
        /// <returns></returns>
        private RoomConfiguration GetRoomConfiguration(IEssentialsRoom room)
        {

            var configuration = new RoomConfiguration
            {
                ShutdownPromptSeconds = room.ShutdownPromptSeconds,
                TouchpanelKeys = DeviceManager.AllDevices.
                OfType<MobileControlTouchpanelController>()
                .Where((tp) => tp.DefaultRoomKey.Equals(room.Key, StringComparison.InvariantCultureIgnoreCase))
                .Select(tp => tp.Key).ToList()
            };

            
            try
            {
                var zrcTp = DeviceManager.AllDevices.OfType<MobileControlTouchpanelController>().SingleOrDefault((tp) => tp.ZoomRoomController);

                configuration.ZoomRoomControllerKey = zrcTp != null ? zrcTp.Key : room.Key;
            }
            catch
            {
                configuration.ZoomRoomControllerKey = room.Key;
            }

            if (room is IEssentialsRoomPropertiesConfig propertiesConfig)
            {
                configuration.HelpMessage = propertiesConfig.PropertiesConfig.HelpMessageForDisplay;
            }

            if (room is IEssentialsHuddleSpaceRoom huddleRoom && !string.IsNullOrEmpty(huddleRoom.PropertiesConfig.HelpMessageForDisplay))
            {
                Debug.Console(2, this, "Getting huddle room config");
                configuration.HelpMessage = huddleRoom.PropertiesConfig.HelpMessageForDisplay;
                configuration.UiBehavior = huddleRoom.PropertiesConfig.UiBehavior;
                configuration.DefaultPresentationSourceKey = huddleRoom.PropertiesConfig.DefaultSourceItem;

            }

            if (room is IEssentialsHuddleVtc1Room vtc1Room && !string.IsNullOrEmpty(vtc1Room.PropertiesConfig.HelpMessageForDisplay))
            {
                Debug.Console(2, this, "Getting vtc room config");
                configuration.HelpMessage = vtc1Room.PropertiesConfig.HelpMessageForDisplay;
                configuration.UiBehavior = vtc1Room.PropertiesConfig.UiBehavior;
                configuration.DefaultPresentationSourceKey = vtc1Room.PropertiesConfig.DefaultSourceItem;
            }

            if (room is IEssentialsTechRoom techRoom && !string.IsNullOrEmpty(techRoom.PropertiesConfig.HelpMessage))
            {
                Debug.Console(2, this, "Getting tech room config");
                configuration.HelpMessage = techRoom.PropertiesConfig.HelpMessage;
            }

            if (room is IHasVideoCodec vcRoom)
            {
                if (vcRoom.VideoCodec != null)
                {
                    Debug.Console(2, this, "Getting codec config");
                    var type = vcRoom.VideoCodec.GetType();

                    configuration.HasVideoConferencing = true;
                    configuration.VideoCodecKey = vcRoom.VideoCodec.Key;
                    configuration.VideoCodecIsZoomRoom = type.Name.Equals("ZoomRoom", StringComparison.InvariantCultureIgnoreCase);
                }
            };

            if (room is IHasAudioCodec acRoom)
            {
                if (acRoom.AudioCodec != null)
                {
                    Debug.Console(2, this, "Getting audio codec config");
                    configuration.HasAudioConferencing = true;
                    configuration.AudioCodecKey = acRoom.AudioCodec.Key;
                }
            }


            if (room is IHasMatrixRouting matrixRoutingRoom)
            {
                Debug.Console(2, this, "Getting matrix routing config");
                configuration.MatrixRoutingKey = matrixRoutingRoom.MatrixRoutingDeviceKey;
                configuration.EndpointKeys = matrixRoutingRoom.EndpointKeys;
            }

            if (room is IEnvironmentalControls envRoom)
            {
                Debug.Console(2, this, "Getting environmental controls config. RoomHasEnvironmentalControls: {0}", envRoom.HasEnvironmentalControlDevices);
                configuration.HasEnvironmentalControls = envRoom.HasEnvironmentalControlDevices;

                if (envRoom.HasEnvironmentalControlDevices)
                {
                    Debug.Console(2, this, "Room Has {0} Environmental Control Devices.", envRoom.EnvironmentalControlDevices.Count);

                    foreach (var dev in envRoom.EnvironmentalControlDevices)
                    {
                        Debug.Console(2, this, "Adding environmental device: {0}", dev.Key);

                        eEnvironmentalDeviceTypes type = eEnvironmentalDeviceTypes.None;

                        if (dev is ILightingScenes || dev is Devices.Common.Lighting.LightingBase)
                        {
                            type = eEnvironmentalDeviceTypes.Lighting;
                        }
                        else if (dev is ShadeBase || dev is IShadesOpenCloseStop || dev is IShadesOpenClosePreset)
                        {
                            type = eEnvironmentalDeviceTypes.Shade;
                        }
                        else if (dev is IShades)
                        {
                            type = eEnvironmentalDeviceTypes.ShadeController;
                        }
                        else if (dev is ISwitchedOutput)
                        {
                            type = eEnvironmentalDeviceTypes.Relay;
                        }

                        Debug.Console(2, this, "Environmental Device Type: {0}", type);

                        var envDevice = new EnvironmentalDeviceConfiguration(dev.Key, type);

                        configuration.EnvironmentalDevices.Add(envDevice);
                    }
                }
                else
                {
                    Debug.Console(2, this, "**************************** Room Has No Environmental Control Devices");
                }
            }

            if (room is IHasDefaultDisplay defDisplayRoom)
            {
                Debug.Console(2, this, "Getting default display config");
                configuration.DefaultDisplayKey = defDisplayRoom.DefaultDisplay.Key;
                configuration.Destinations.Add(eSourceListItemDestinationTypes.defaultDisplay, defDisplayRoom.DefaultDisplay.Key);
            }

            if (room is IHasMultipleDisplays multiDisplayRoom)
            {
                Debug.Console(2, this, "Getting multiple display config");

                if (multiDisplayRoom.Displays == null)
                {
                    Debug.Console(2, this, "Displays collection is null");
                }
                else
                {
                    Debug.Console(2, this, "Displays collection exists");

                    configuration.Destinations = multiDisplayRoom.Displays.ToDictionary(kv => kv.Key, kv => kv.Value.Key);
                }
            }

            var sourceList = ConfigReader.ConfigObject.GetSourceListForKey(room.SourceListKey);
            if (sourceList != null)
            {
                Debug.Console(2, this, "Getting source list config");
                configuration.SourceList = sourceList;
                configuration.HasRoutingControls = true;

                foreach (var source in sourceList)
                {
                    if (source.Value.SourceDevice is Devices.Common.IRSetTopBoxBase)
                    {
                        configuration.HasSetTopBoxControls = true;
                        continue;
                    }
                    else if (source.Value.SourceDevice is CameraBase)
                    {
                        configuration.HasCameraControls = true;
                        continue;
                    }
                }
            }

            var destinationList = ConfigReader.ConfigObject.GetDestinationListForKey(room.DestinationListKey);

            if(destinationList != null)
            {
                configuration.DestinationList = destinationList;
            }
            

            return configuration;
        }
    }

    public class RoomStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("configuration", NullValueHandling = NullValueHandling.Ignore)]
        public RoomConfiguration Configuration { get; set; }

        [JsonProperty("activityMode", NullValueHandling = NullValueHandling.Ignore)]
        public int? ActivityMode { get; set; }
        [JsonProperty("advancedSharingActive", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AdvancedSharingActive { get; set; }
        [JsonProperty("isOn", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsOn { get; set; }
        [JsonProperty("isWarmingUp", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsWarmingUp { get; set; }
        [JsonProperty("isCoolingDown", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCoolingDown { get; set; }
        [JsonProperty("selectedSourceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string SelectedSourceKey { get; set; }
        [JsonProperty("share", NullValueHandling = NullValueHandling.Ignore)]
        public ShareState Share { get; set; }

        [JsonProperty("volumes", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, Volume> Volumes { get; set; }

        [JsonProperty("isInCall", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsInCall { get; set; }
    }

    public class ShareState
    {
        [JsonProperty("currentShareText", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentShareText { get; set; }
        [JsonProperty("enabled", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Enabled { get; set; }
        [JsonProperty("isSharing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSharing { get; set; }
    }

    /// <summary>
    /// Represents the capabilities of the room and the associated device info
    /// </summary>
    public class RoomConfiguration
    {
        [JsonProperty("shutdownPromptSeconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? ShutdownPromptSeconds { get; set; }

        [JsonProperty("hasVideoConferencing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasVideoConferencing { get; set; }
        [JsonProperty("videoCodecIsZoomRoom", NullValueHandling = NullValueHandling.Ignore)]
        public bool? VideoCodecIsZoomRoom { get; set; }
        [JsonProperty("hasAudioConferencing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasAudioConferencing { get; set; }
        [JsonProperty("hasEnvironmentalControls", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasEnvironmentalControls { get; set; }
        [JsonProperty("hasCameraControls", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasCameraControls { get; set; }
        [JsonProperty("hasSetTopBoxControls", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasSetTopBoxControls { get; set; }
        [JsonProperty("hasRoutingControls", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasRoutingControls { get; set; }

        [JsonProperty("touchpanelKeys", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> TouchpanelKeys { get; set; }

        [JsonProperty("zoomRoomControllerKey", NullValueHandling = NullValueHandling.Ignore)]
        public string ZoomRoomControllerKey { get; set; }


        [JsonProperty("videoCodecKey", NullValueHandling = NullValueHandling.Ignore)]
        public string VideoCodecKey { get; set; }
        [JsonProperty("audioCodecKey", NullValueHandling = NullValueHandling.Ignore)]
        public string AudioCodecKey { get; set; }
        [JsonProperty("matrixRoutingKey", NullValueHandling = NullValueHandling.Ignore)]
        public string MatrixRoutingKey { get; set; }
        [JsonProperty("endpointKeys", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> EndpointKeys { get; set; }

        [JsonProperty("defaultDisplayKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DefaultDisplayKey { get; set; }
        [JsonProperty("destinations", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<eSourceListItemDestinationTypes, string> Destinations { get; set; }
        [JsonProperty("environmentalDevices", NullValueHandling = NullValueHandling.Ignore)]
        public List<EnvironmentalDeviceConfiguration> EnvironmentalDevices { get; set; }
        [JsonProperty("sourceList", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, SourceListItem> SourceList { get; set; }

        [JsonProperty("destinationList", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string,  DestinationListItem> DestinationList { get; set;}

        [JsonProperty("defaultPresentationSourceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DefaultPresentationSourceKey { get; set; }


        [JsonProperty("helpMessage", NullValueHandling = NullValueHandling.Ignore)]
        public string HelpMessage { get; set; }

        [JsonProperty("techPassword", NullValueHandling = NullValueHandling.Ignore)]
        public string TechPassword { get; set; }

        [JsonProperty("uiBehavior", NullValueHandling = NullValueHandling.Ignore)]
        public EssentialsRoomUiBehaviorConfig UiBehavior { get; set; }

        [JsonProperty("supportsAdvancedSharing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? SupportsAdvancedSharing { get; set; }
        [JsonProperty("userCanChangeShareMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? UserCanChangeShareMode { get; set; }

        public RoomConfiguration()
        {
            Destinations = new Dictionary<eSourceListItemDestinationTypes, string>();
            EnvironmentalDevices = new List<EnvironmentalDeviceConfiguration>();
            SourceList = new Dictionary<string, SourceListItem>();
            TouchpanelKeys = new List<string>();
        }

    }

    public class EnvironmentalDeviceConfiguration
    {
        [JsonProperty("deviceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DeviceKey { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("deviceType", NullValueHandling = NullValueHandling.Ignore)]
        public eEnvironmentalDeviceTypes DeviceType { get; private set; }

        public EnvironmentalDeviceConfiguration(string key, eEnvironmentalDeviceTypes type)
        {
            DeviceKey = key;
            DeviceType = type;
        }
    }

    public enum eEnvironmentalDeviceTypes
    {
        None,
        Lighting,
        Shade,
        ShadeController,
        Relay,
    }

    public class ApiTouchPanelToken
    {
        [JsonProperty("touchPanels", NullValueHandling = NullValueHandling.Ignore)]
        public List<JoinToken> TouchPanels { get; set; } = new List<JoinToken>();

        [JsonProperty("userAppUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string UserAppUrl { get; set; } = "";
    }

#if SERIES3
    public class SourceSelectMessageContent
    {
        public string SourceListItem { get; set; }
        public string SourceListKey { get; set; }
    }

    public class DirectRoute
    {
        public string SourceKey { get; set; }
        public string DestinationKey { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="b"></param>
    public delegate void PressAndHoldAction(bool b);
#endif
}