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
using PepperDash.Essentials.Core.Lighting;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PepperDash.Essentials.Devices.Common.Room;
using IShades = PepperDash.Essentials.Core.Shades.IShades;
using ShadeBase = PepperDash.Essentials.Devices.Common.Shades.ShadeBase;
using PepperDash.Essentials.Devices.Common.TouchPanel;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="room"></param>
        public MobileControlEssentialsRoomBridge(EssentialsRoomBase room) :
            this(string.Format("mobileControlBridge-{0}", room.Key), room.Key, room)
        {
            Room = room;
        }

        public MobileControlEssentialsRoomBridge(IEssentialsRoom room) :
            this(string.Format("mobileControlBridge-{0}", room.Key), room.Key, room as Device)
        {
            Room = room;
        }

        public MobileControlEssentialsRoomBridge(string key, string roomKey, Device room) : base(key, string.Format(@"/room/{0}/status", roomKey), room)
        {
            DefaultRoomKey = roomKey;

            AddPreActivationAction(GetRoom);
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            // we add actions to the messaging system with a path, and a related action. Custom action
            // content objects can be handled in the controller's LineReceived method - and perhaps other
            // sub-controller parsing could be attached to these classes, so that the systemController
            // doesn't need to know about everything.

            Debug.Console(0, this, "Registering Actions with AppServer");

            appServerController.AddAction(string.Format(@"/room/{0}/promptForCode", Room.Key), (id, content) => OnUserPromptedForCode());
            appServerController.AddAction(string.Format(@"/room/{0}/clientJoined", Room.Key), (id, content) => OnClientJoined());

            appServerController.AddAction(string.Format(@"/room/{0}/touchPanels", Room.Key), (id, content) => OnTouchPanelsUpdated(content));

            appServerController.AddAction($@"/room/{Room.Key}/userApp", (id, content) => OnUserAppUpdated(content));

            appServerController.AddAction(string.Format(@"/room/{0}/userCode", Room.Key), (id,content) => {
                var msg = content.ToObject<UserCodeChangedContent>();

                SetUserCode(msg.UserCode, msg.QrChecksum == null ? string.Empty : msg.QrChecksum);
            });


            // Source Changes and room off
            appServerController.AddAction(string.Format(@"/room/{0}/status", Room.Key), (id, content) =>
            {
                SendFullStatusForClientId(id, Room);

            });

            var routeRoom = Room as IRunRouteAction;
            if (routeRoom != null)
                appServerController.AddAction(string.Format(@"/room/{0}/source", Room.Key), (id, content) => {
                    var msg = content.ToObject<SourceSelectMessageContent>();

                    routeRoom.RunRouteAction(msg.SourceListItem, string.Empty);
                });

            var directRouteRoom = Room as IRunDirectRouteAction;
            if (directRouteRoom != null)
            {
                appServerController.AddAction(String.Format("/room/{0}/directRoute", Room.Key), (id, content) => {
                    var msg = content.ToObject<DirectRoute>();

                    directRouteRoom.RunDirectRoute(msg.SourceKey, msg.DestinationKey);
                });
            }


            var defaultRoom = Room as IRunDefaultPresentRoute;
            if (defaultRoom != null)
                appServerController.AddAction(string.Format(@"/room/{0}/defaultsource", Room.Key), (id, content) => defaultRoom.RunDefaultPresentRoute());                    

            var volumeRoom = Room as IHasCurrentVolumeControls;
            if (volumeRoom != null)
            {
                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/level", Room.Key), (id, content) => {
                    var msg = content.ToObject<MobileControlSimpleContent<ushort>>();

                    var basicVolumeWithFeedback = volumeRoom.CurrentVolumeControls as IBasicVolumeWithFeedback;

                    if (basicVolumeWithFeedback != null)
                        basicVolumeWithFeedback.SetVolume(msg.Value);
                });

                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/muteToggle", Room.Key), (id, content) => volumeRoom.CurrentVolumeControls.MuteToggle());

                volumeRoom.CurrentVolumeDeviceChange += Room_CurrentVolumeDeviceChange;

                // Registers for initial volume events, if possible
                var currentVolumeDevice = volumeRoom.CurrentVolumeControls as IBasicVolumeWithFeedback;
                if (currentVolumeDevice != null)
                {
                    currentVolumeDevice.MuteFeedback.OutputChange += MuteFeedback_OutputChange;
                    currentVolumeDevice.VolumeLevelFeedback.OutputChange += VolumeLevelFeedback_OutputChange;
                }
            }

            var sscRoom = Room as IHasCurrentSourceInfoChange;
            if (sscRoom != null)
                sscRoom.CurrentSourceChange += Room_CurrentSingleSourceChange;

            var vcRoom = Room as IHasVideoCodec;
            if (vcRoom != null && vcRoom.VideoCodec != null)
            {
                var key = vcRoom.VideoCodec.Key + "-" + appServerController.Key;

                if (!appServerController.CheckForDeviceMessenger(key))
                {
                    {
                        var vcMessenger = new VideoCodecBaseMessenger(key, vcRoom.VideoCodec, String.Format("/device/{0}", vcRoom.VideoCodec.Key));
                        appServerController.AddDeviceMessenger(vcMessenger);
                    }
                }

                vcRoom.IsSharingFeedback.OutputChange += IsSharingFeedback_OutputChange;
            }

            var acRoom = Room as IHasAudioCodec;
            if (acRoom != null && acRoom.AudioCodec != null)
            {
                var key = acRoom.AudioCodec.Key + "-" + appServerController.Key;

                if (!appServerController.CheckForDeviceMessenger(key))
                {
                    var acMessenger = new AudioCodecBaseMessenger(key, acRoom.AudioCodec,
                        String.Format("/device/{0}", acRoom.AudioCodec.Key));
                    appServerController.AddDeviceMessenger(acMessenger);
                }
            }

            var vtcRoom = Room as IEssentialsHuddleVtc1Room;
            if (vtcRoom != null)
            {
                if (vtcRoom.ScheduleSource != null)
                {
                    var key = vtcRoom.Key + "-" + appServerController.Key;

                    if (!appServerController.CheckForDeviceMessenger(key))
                    {
                        var scheduleMessenger = new IHasScheduleAwarenessMessenger(key, vtcRoom.ScheduleSource,
                            string.Format("/room/{0}/schedule", vtcRoom.Key));
                        appServerController.AddDeviceMessenger(scheduleMessenger);
                    }
                }

                vtcRoom.InCallFeedback.OutputChange += InCallFeedback_OutputChange;
            }

            var privacyRoom = Room as IPrivacy;
            if (privacyRoom != null)
            {
                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/privacyMuteToggle", Room.Key), (id, content) => privacyRoom.PrivacyModeToggle());

                privacyRoom.PrivacyModeIsOnFeedback.OutputChange += PrivacyModeIsOnFeedback_OutputChange;
            }

            //SetupDeviceMessengers();

            var defCallRm = Room as IRunDefaultCallRoute;
            if (defCallRm != null)
            {
                appServerController.AddAction(string.Format(@"/room/{0}/activityVideo", Room.Key), (id, content) => defCallRm.RunDefaultCallRoute());                    
            }

            appServerController.AddAction(string.Format(@"/room/{0}/shutdownStart", Room.Key), (id, content) => Room.StartShutdown(eShutdownType.Manual));

            appServerController.AddAction(string.Format(@"/room/{0}/shutdownEnd", Room.Key), (id, content) => Room.ShutdownPromptTimer.Finish());

            appServerController.AddAction(string.Format(@"/room/{0}/shutdownCancel", Room.Key), (id, content) => Room.ShutdownPromptTimer.Cancel());                

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
            var state = new RoomStateMessage();

            state.IsInCall = e.BoolValue;
            PostStatusMessage(state);
        }

        private void GetRoom()
        {
            if (Room != null)
            {
                Debug.Console(0, this, "Room with key {0} already linked.", DefaultRoomKey);
                return;
            }

            var tempRoom = DeviceManager.GetDeviceForKey(DefaultRoomKey) as IEssentialsRoom;

            if (tempRoom == null)
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
            var techRoom = Room as IEssentialsTechRoom;

            if (techRoom == null)
            {
                return;
            }

            SetTunerActions(techRoom);
            
            CreateScheduleMessenger(techRoom.Key, techRoom as IRoomEventSchedule);

            Parent.AddAction(String.Format("/room/{0}/roomPowerOn", techRoom.Key), (id, content) => techRoom.RoomPowerOn());
            Parent.AddAction(String.Format("/room/{0}/roomPowerOff", techRoom.Key), (id, content) => techRoom.RoomPowerOff());
        }

        private void CreateScheduleMessenger(string roomKey, IRoomEventSchedule techRoom)
        {
            var scheduleMessenger = new RoomEventScheduleMessenger(roomKey + "-schedule",
                String.Format("/room/{0}/schedule", roomKey), techRoom);
            Parent.AddDeviceMessenger(scheduleMessenger);
        }

        private void SetTunerActions(IEssentialsTechRoom techRoom)
        {
            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<ISetTopBoxControls>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<IChannel>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<IColor>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<IDPad>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<INumericKeypad>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<IHasPowerControl>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }

            foreach (var tuner in techRoom.Tuners.Select(t => t.Value).Cast<ITransport>())
            {
                var stb = tuner;
                stb.LinkActions(Parent);
            }
        }

        void PrivacyModeIsOnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            var volumes = new Volumes();

            volumes.Master = new Volume("master");
            volumes.Master.PrivacyMuted = e.BoolValue;

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

            var vcRoom = Room as IHasVideoCodec;
            var srcInfoRoom = Room as IHasCurrentSourceInfoChange;

            if (srcInfoRoom != null && (vcRoom != null && (vcRoom.VideoCodec.SharingContentIsOnFeedback.BoolValue && srcInfoRoom.CurrentSourceInfo != null)))
            {
                shareText = srcInfoRoom.CurrentSourceInfo.PreferredName;
                isSharing = true;
            }
            else
            {
                shareText = "None";
                isSharing = false;
            }

            var state = new RoomStateMessage();

            state.Share = new ShareState();
            state.Share.CurrentShareText = shareText;
            state.Share.IsSharing = isSharing;

            PostStatusMessage(state);
        }

        /// <summary>
        /// Handler for cancelled shutdown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_WasCancelled(object sender, EventArgs e)
        {
            var roomStatus = new JObject {{"state", "wasCancelled"}};
            var message = new MobileControlMessage {
                Type=String.Format("/room/{0}/shutdown/", Room.Key), Content = JToken.FromObject(roomStatus) };
            Parent.SendMessageObject(message);
        }

        /// <summary>
        /// Handler for when shutdown finishes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_HasFinished(object sender, EventArgs e)
        {
            var roomStatus = new JObject {{"state", "hasFinished"}};
            var message = new MobileControlMessage
            {
                Type = String.Format("/room/{0}/shutdown/", Room.Key),
                Content = JToken.FromObject(roomStatus)
            };

            Parent.SendMessageObject(message);
        }

        /// <summary>
        /// Handler for when shutdown starts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShutdownPromptTimer_HasStarted(object sender, EventArgs e)
        {
            var roomStatus = new JObject
            {
                {"state", "hasStarted"},
                {"duration", Room.ShutdownPromptTimer.SecondsToCount}
            };

            var message = new MobileControlMessage
            {
                Type = String.Format("/room/{0}/shutdown/", Room.Key),
                Content = JToken.FromObject(roomStatus)
            };
            Parent.SendMessageObject(message);
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
            var state = new RoomStateMessage();

            state.IsWarmingUp = e.BoolValue;
            PostStatusMessage(state);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsCoolingDownFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            state.IsCoolingDown = e.BoolValue;
            PostStatusMessage(state);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            state.IsOn = e.BoolValue;
            PostStatusMessage(state);
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

            var volumes = new Volumes();

            volumes.Master = new Volume("master", e.BoolValue);

            state.Volumes = volumes;

            PostStatusMessage(state);
        }

        /// <summary>
        /// Handles Volume changes on room
        /// </summary>
        private void VolumeLevelFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            var state = new RoomStateMessage();

            var volumes = new Volumes();

            volumes.Master = new Volume("master", e.IntValue);

            state.Volumes = volumes;

            PostStatusMessage(state);
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
            if (type == ChangeType.WillChange)
            {
                // Disconnect from previous source

                if (info != null)
                {
                    var previousDev = info.SourceDevice;

                    // device type interfaces
                    if (previousDev is ISetTopBoxControls)
                        (previousDev as ISetTopBoxControls).UnlinkActions(Parent);
                    // common interfaces
                    if (previousDev is IChannel)
                        (previousDev as IChannel).UnlinkActions(Parent);
                    if (previousDev is IColor)
                        (previousDev as IColor).UnlinkActions(Parent);
                    if (previousDev is IDPad)
                        (previousDev as IDPad).UnlinkActions(Parent);
                    if (previousDev is IDvr)
                        (previousDev as IDvr).UnlinkActions(Parent);
                    if (previousDev is INumericKeypad)
                        (previousDev as INumericKeypad).UnlinkActions(Parent);
                    if (previousDev is IHasPowerControl)
                        (previousDev as IHasPowerControl).UnlinkActions(Parent);
                    if (previousDev is ITransport)
                        (previousDev as ITransport).UnlinkActions(Parent);
                }
            }
            else // did change
            {
                if (info != null)
                {
                    var dev = info.SourceDevice;

                    if (dev is ISetTopBoxControls)
                        (dev as ISetTopBoxControls).LinkActions(Parent);
                    if (dev is IChannel)
                        (dev as IChannel).LinkActions(Parent);
                    if (dev is IColor)
                        (dev as IColor).LinkActions(Parent);
                    if (dev is IDPad)
                        (dev as IDPad).LinkActions(Parent);
                    if (dev is IDvr)
                        (dev as IDvr).LinkActions(Parent);
                    if (dev is INumericKeypad)
                        (dev as INumericKeypad).LinkActions(Parent);
                    if (dev is IHasPowerControl)
                        (dev as IHasPowerControl).LinkActions(Parent);
                    if (dev is ITransport)
                        (dev as ITransport).LinkActions(Parent);

                    var srcRm = Room as IHasCurrentSourceInfoChange;
                    if (srcRm != null)
                    {
                        var state = new RoomStateMessage();

                        state.SelectedSourceKey = srcRm.CurrentSourceInfoKey;
                        PostStatusMessage(state);
                    }
                }
            }
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
        RoomStateMessage GetFullStatusForClientId(IEssentialsRoom room)
        {
            Debug.Console(2, this, "GetFullStatus");

            var sourceKey = room is IHasCurrentSourceInfoChange ? (room as IHasCurrentSourceInfoChange).CurrentSourceInfoKey : null;

            var rmVc = room as IHasCurrentVolumeControls;
            var volumes = new Volumes();
            if (rmVc != null)
            {
                var vc = rmVc.CurrentVolumeControls as IBasicVolumeWithFeedback;
                if (vc != null)
                {
                    volumes.Master = new Volume("master", vc.VolumeLevelFeedback.UShortValue, vc.MuteFeedback.BoolValue, "Volume", true, "");

                    var privacyRoom = room as IPrivacy;
                    if (privacyRoom != null)
                    {
                        volumes.Master.HasPrivacyMute = true;
                        volumes.Master.PrivacyMuted = privacyRoom.PrivacyModeIsOnFeedback.BoolValue;
                    }
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

            var vtcRoom = room as IEssentialsHuddleVtc1Room;
            if (vtcRoom != null)
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
                Touchpanels = DeviceManager.AllDevices.
                OfType<MobileControlTouchpanelController>()
                .Where((tp) => tp.DefaultRoomKey.Equals(room.Key, StringComparison.InvariantCultureIgnoreCase))
                .Select(tp => tp.Key).ToList()
            };

            var huddleRoom = room as IEssentialsHuddleSpaceRoom;
            if (huddleRoom != null && !string.IsNullOrEmpty(huddleRoom.PropertiesConfig.HelpMessageForDisplay))
            {
                Debug.Console(2, this, "Getting huddle room config");
                configuration.HelpMessage = huddleRoom.PropertiesConfig.HelpMessageForDisplay;
                configuration.UiBehavior = huddleRoom.PropertiesConfig.UiBehavior;
                configuration.DefaultPresentationSourceKey = huddleRoom.PropertiesConfig.DefaultSourceItem;

            }

            var vtc1Room = room as IEssentialsHuddleVtc1Room;
            if (vtc1Room != null && !string.IsNullOrEmpty(vtc1Room.PropertiesConfig.HelpMessageForDisplay))
            {
                Debug.Console(2, this, "Getting vtc room config");
                configuration.HelpMessage = vtc1Room.PropertiesConfig.HelpMessageForDisplay;
                configuration.UiBehavior = vtc1Room.PropertiesConfig.UiBehavior;
                configuration.DefaultPresentationSourceKey = vtc1Room.PropertiesConfig.DefaultSourceItem;
            }

            var techRoom = room as IEssentialsTechRoom;
            if (techRoom != null && !string.IsNullOrEmpty(techRoom.PropertiesConfig.HelpMessage))
            {
                Debug.Console(2, this, "Getting tech room config");
                configuration.HelpMessage = techRoom.PropertiesConfig.HelpMessage;
            }

            var vcRoom = room as IHasVideoCodec;
            if (vcRoom != null)
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

            var acRoom = room as IHasAudioCodec;
            if (acRoom != null)
            {
                if (acRoom.AudioCodec != null)
                {
                    Debug.Console(2, this, "Getting audio codec config");
                    configuration.HasAudioConferencing = true;
                    configuration.AudioCodecKey = acRoom.AudioCodec.Key;
                }
            }

            var envRoom = room as IEnvironmentalControls;
            if(envRoom != null)
            {
                Debug.Console(2, this, "Getting environmental controls config");
                configuration.HasEnvironmentalControls = envRoom.HasEnvironmentalControlDevices;

                if(envRoom.HasEnvironmentalControlDevices)
                {
                    foreach (var dev in envRoom.EnvironmentalControlDevices)
                    {
                        eEnvironmentalDeviceTypes type = eEnvironmentalDeviceTypes.None;

                        if(dev is Devices.Common.Lighting.LightingBase)
                        {
                            type = eEnvironmentalDeviceTypes.Lighting;
                        }
                        else if (dev is ShadeBase)
                        {
                            type = eEnvironmentalDeviceTypes.Shade;
                        }
                        else if (dev is IShades)
                        {
                            type = eEnvironmentalDeviceTypes.ShadeController;
                        }

                        var envDevice = new EnvironmentalDeviceConfiguration(dev.Key, type);

                        configuration.EnvironmentalDevices.Add(envDevice);
                    }
                }
            }

            var defDisplayRoom = room as IHasDefaultDisplay;
            if (defDisplayRoom != null)
            {
                Debug.Console(2, this, "Getting default display config");
                configuration.DefaultDisplayKey = defDisplayRoom.DefaultDisplay.Key;
                configuration.DisplayKeys.Add(defDisplayRoom.DefaultDisplay.Key);
            }

            var multiDisplayRoom = room as IHasMultipleDisplays;
            if (multiDisplayRoom != null)
            {
                Debug.Console(2, this, "Getting multiple display config");

                if (multiDisplayRoom.Displays == null)
                {
                    Debug.Console(2, this, "Displays collection is null");
                }
                else
                {
                    Debug.Console(2, this, "Displays collection exists");

                    foreach (var display in multiDisplayRoom.Displays)
                    {
                        if(display.Value == null)
                        {
                            Debug.Console(2, this, "Value for key {0} is null", display.Key);
                            continue;
                        }
                        configuration.DisplayKeys.Add(display.Value.Key);
                    }
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

            return configuration;
        }
    }

    public class RoomStateMessage: DeviceStateMessageBase
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
        public Volumes Volumes { get; set; }

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

        [JsonProperty("touchpanels", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Touchpanels { get; set; }

        [JsonProperty("videoCodecKey", NullValueHandling = NullValueHandling.Ignore)]
        public string VideoCodecKey { get; set; }
        [JsonProperty("audioCodecKey", NullValueHandling = NullValueHandling.Ignore)]
        public string AudioCodecKey { get; set; }
        [JsonProperty("defaultDisplayKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DefaultDisplayKey { get; set; }
        [JsonProperty("displayKeys", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> DisplayKeys { get; set; }
        [JsonProperty("environmentalDevices", NullValueHandling = NullValueHandling.Ignore)]
        public List<EnvironmentalDeviceConfiguration> EnvironmentalDevices { get; set; }
        [JsonProperty("sourceList", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, SourceListItem> SourceList { get; set; }
        [JsonProperty("defaultPresentationSourceKey", NullValueHandling = NullValueHandling.Ignore)]
        public string DefaultPresentationSourceKey { get; set; }


        [JsonProperty("helpMessage", NullValueHandling = NullValueHandling.Ignore)]
        public string HelpMessage { get; set; }

        [JsonProperty("uiBehavior", NullValueHandling = NullValueHandling.Ignore)]
        public EssentialsRoomUiBehaviorConfig UiBehavior { get; set; }

        [JsonProperty("supportsAdvancedSharing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? SupportsAdvancedSharing { get; set; }
        [JsonProperty("userCanChangeShareMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? UserCanChangeShareMode { get; set; }

        public RoomConfiguration()
        {
            DisplayKeys = new List<string>();
            EnvironmentalDevices = new List<EnvironmentalDeviceConfiguration>();
            SourceList = new Dictionary<string, SourceListItem>();
            Touchpanels = new List<string>();
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