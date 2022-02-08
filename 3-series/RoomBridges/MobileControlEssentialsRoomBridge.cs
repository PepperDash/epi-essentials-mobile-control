using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp.Ssh;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.SoftCodec;
using PepperDash.Essentials.Core.Lighting;
using PepperDash.Essentials.Core.Shades;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PepperDash.Essentials
{
    public class MobileControlEssentialsRoomBridge : MobileControlBridgeBase
    {
        public IEssentialsRoom Room { get; private set; }

        public string DefaultRoomKey
        {
            get; private set; }
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

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            // we add actions to the messaging system with a path, and a related action. Custom action
            // content objects can be handled in the controller's LineReceived method - and perhaps other
            // sub-controller parsing could be attached to these classes, so that the systemController
            // doesn't need to know about everything.

            Debug.Console(0, this, "Registering Actions with AppServer");

            appServerController.AddAction(string.Format(@"/room/{0}/promptForCode", Room.Key), new Action(OnUserPromptedForCode));
            appServerController.AddAction(string.Format(@"/room/{0}/clientJoined", Room.Key), new Action(OnClientJoined));

            appServerController.AddAction(string.Format(@"/room/{0}/userCode", Room.Key),
                new UserCodeChanged(SetUserCode));

            // Source Changes and room off
            appServerController.AddAction(string.Format(@"/room/{0}/status", Room.Key), new ClientSpecificUpdateRequest((id) => SendFullStatusForClientId(id, Room)));

            var routeRoom = Room as IRunRouteAction;
            if (routeRoom != null)
                appServerController.AddAction(string.Format(@"/room/{0}/source", Room.Key),
                    new Action<SourceSelectMessageContent>(c =>
                    {
                        var sourceListKey = string.Empty;

                        routeRoom.RunRouteAction(c.SourceListItem, sourceListKey);

                    }));

            var directRouteRoom = Room as IRunDirectRouteAction;
            if (directRouteRoom != null)
            {
                appServerController.AddAction(String.Format("/room/{0}/directRoute", Room.Key), new Action<DirectRoute>((d) => directRouteRoom.RunDirectRoute(d.SourceKey, d.DestinationKey)));
            }


            var defaultRoom = Room as IRunDefaultPresentRoute;
            if (defaultRoom != null)
                appServerController.AddAction(string.Format(@"/room/{0}/defaultsource", Room.Key),
                    new Action(() => defaultRoom.RunDefaultPresentRoute()));

            var volumeRoom = Room as IHasCurrentVolumeControls;
            if (volumeRoom != null)
            {
                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/level", Room.Key), new Action<ushort>(u =>
                {
                    var basicVolumeWithFeedback = volumeRoom.CurrentVolumeControls as IBasicVolumeWithFeedback;
                    if (basicVolumeWithFeedback != null)
                        basicVolumeWithFeedback.SetVolume(u);
                }));
                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/muteToggle", Room.Key), new Action(() =>
                    volumeRoom.CurrentVolumeControls.MuteToggle()));
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
                    var zr = vcRoom.VideoCodec as PepperDash.Essentials.Devices.Common.VideoCodec.ZoomRoom.ZoomRoom;
                    if (zr != null)
                    {
                        var zrMessenger = new ZoomRoomMessenger(key, zr, String.Format("/device/{0}", vcRoom.VideoCodec.Key));
                        appServerController.AddDeviceMessenger(zrMessenger);
                    }
                    else
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
                appServerController.AddAction(string.Format(@"/room/{0}/volumes/master/privacyMuteToggle", Room.Key), new Action(privacyRoom.PrivacyModeToggle));

                privacyRoom.PrivacyModeIsOnFeedback.OutputChange += PrivacyModeIsOnFeedback_OutputChange;
            }

            SetupDeviceMessengers();

            var defCallRm = Room as IRunDefaultCallRoute;
            if (defCallRm != null)
            {
                appServerController.AddAction(string.Format(@"/room/{0}/activityVideo", Room.Key),
                    new Action(() => defCallRm.RunDefaultCallRoute()));
            }

            appServerController.AddAction(string.Format(@"/room/{0}/shutdownStart", Room.Key),
                new Action(() => Room.StartShutdown(eShutdownType.Manual)));
            appServerController.AddAction(string.Format(@"/room/{0}/shutdownEnd", Room.Key),
                new Action(() => Room.ShutdownPromptTimer.Finish()));
            appServerController.AddAction(string.Format(@"/room/{0}/shutdownCancel", Room.Key),
                new Action(() => Room.ShutdownPromptTimer.Cancel()));

            Room.OnFeedback.OutputChange += OnFeedback_OutputChange;
            Room.IsCoolingDownFeedback.OutputChange += IsCoolingDownFeedback_OutputChange;
            Room.IsWarmingUpFeedback.OutputChange += IsWarmingUpFeedback_OutputChange;

            Room.ShutdownPromptTimer.HasStarted += ShutdownPromptTimer_HasStarted;
            Room.ShutdownPromptTimer.HasFinished += ShutdownPromptTimer_HasFinished;
            Room.ShutdownPromptTimer.WasCancelled += ShutdownPromptTimer_WasCancelled;

            AddTechRoomActions();
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

        /// <summary>
        /// Override of base: calls base to add parent and then registers actions and events.
        /// </summary>
        /// <param name="parent"></param>
        public override void AddParent(MobileControlSystemController parent)
        {
            base.AddParent(parent);

        }

        private void AddTechRoomActions()
        {
            var techRoom = Room as EssentialsTechRoom;

            if (techRoom == null)
            {
                return;
            }

            SetTunerActions(techRoom);

            CreateScheduleMessenger(techRoom);

            Parent.AddAction(String.Format("/room/{0}/roomPowerOn",techRoom.Key), new Action(techRoom.RoomPowerOn));
            Parent.AddAction(String.Format("/room/{0}/roomPowerOff", techRoom.Key), new Action(techRoom.RoomPowerOff));
        }

        private void CreateScheduleMessenger(EssentialsTechRoom techRoom)
        {
            var scheduleMessenger = new RoomEventScheduleMessenger(techRoom.Key + "-schedule",
                String.Format("/room/{0}/schedule", techRoom.Key), techRoom);
            Parent.AddDeviceMessenger(scheduleMessenger);
        }

        private void SetTunerActions(EssentialsTechRoom techRoom)
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
        /// Set up the messengers for each device type
        /// </summary>
        private void SetupDeviceMessengers()
        {
            foreach (var device in DeviceManager.AllDevices)
            {
                Debug.Console(2, this, "Attempting to set up device messenger for device: {0}", device.Key);

                if (device is CameraBase)
                {
                    var camDevice = device as CameraBase;
                    Debug.Console(2, this, "Adding CameraBaseMessenger for device: {0}", device.Key);
                    var cameraMessenger = new CameraBaseMessenger(device.Key + "-" + Parent.Key, camDevice,
                        "/device/" + device.Key);
                    Parent.AddDeviceMessenger(cameraMessenger);
                    
                }

                if (device is BlueJeansPc)
                {
                    var softCodecDevice = device as BlueJeansPc;
                    Debug.Console(2, this, "Adding IRunRouteActionMessnger for device: {0}", device.Key);
                    var routeMessenger = new RunRouteActionMessenger(device.Key + "-" + Parent.Key, softCodecDevice,
                        "/device/" + device.Key);
                    Parent.AddDeviceMessenger(routeMessenger);
                  
                }

                if (device is ITvPresetsProvider)
                {
                    var presetsDevice = device as ITvPresetsProvider;
                    if (presetsDevice.TvPresets == null)
                    {
                        Debug.Console(0, this, "TvPresets is null for device: '{0}'. Skipping DevicePresetsModelMessenger", device.Key);
                    }
                    else
                    {
                        Debug.Console(2, this, "Adding ITvPresetsProvider for device: {0}", device.Key);
                        var presetsMessenger = new DevicePresetsModelMessenger(device.Key + "-" + Parent.Key, String.Format("/device/{0}/presets", device.Key),
                            presetsDevice);
                        Parent.AddDeviceMessenger(presetsMessenger);
                        
                    }
                }

                if (device is DisplayBase)
                {
                    var display = device as DisplayBase;
                    Debug.Console(2, this, "Adding actions for device: {0}", device.Key);

                    display.LinkActions(Parent);
                }

                if (device is TwoWayDisplayBase)
                {
                    var display = device as TwoWayDisplayBase;
                    Debug.Console(2, this, "Adding TwoWayDisplayBase for device: {0}", device.Key);
                    var twoWayDisplayMessenger = new TwoWayDisplayBaseMessenger(device.Key + "-" + Parent.Key,
                        String.Format("/device/{0}", device.Key), display);
                    Parent.AddDeviceMessenger(twoWayDisplayMessenger);
                }

                if (device is ICommunicationMonitor)
                {
                    var monitor = device as ICommunicationMonitor;
                    Debug.Console(2, this, "Adding CommunicationMonitor for device: {0}", device.Key);
                    var communicationMonitorMessenger = new CommMonitorMessenger(device.Key + "-" + Parent.Key + "-monitor",
                        String.Format("/device/{0}/commMonitor", device.Key), monitor);
                    Parent.AddDeviceMessenger(communicationMonitorMessenger);
                    
                }

                if (device is IBasicVolumeWithFeedback)
                {
                    var deviceKey = device.Key;
                    var volControlDevice = device as IBasicVolumeWithFeedback;
                    Debug.Console(2, this, "Adding IBasicVolumeControlWithFeedback for device: {0}", deviceKey);
                    var messenger = new DeviceVolumeMessenger(deviceKey + "-" + Parent.Key + "-volume",
                        String.Format("/device/{0}/volume", deviceKey), deviceKey, volControlDevice);
                    Parent.AddDeviceMessenger(messenger);
                }

                if (device is LightingBase)
                {
                    var deviceKey = device.Key;
                    var lightingDevice = device as LightingBase;
                    Debug.Console(2, this, "Adding LightingBaseMessenger for device: {0}", deviceKey);
                    var messenger = new LightingBaseMessenger(deviceKey + "-" + Parent.Key,
                        lightingDevice, string.Format("/device/{0}", deviceKey));
                    Parent.AddDeviceMessenger(messenger);
                }

                if (device is ShadeBase)
                {
                    var deviceKey = device.Key;
                    var shadeDevice = device as ShadeBase;
                    Debug.Console(2, this, "Adding ShadeBaseMessenger for device: {0}", deviceKey);
                    var messenger = new ShadeBaseMessenger(deviceKey + "-" + Parent.Key,
                        shadeDevice, string.Format("/device/{0}", deviceKey));
                    Parent.AddDeviceMessenger(messenger);
                }
            }
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
            var message = new JObject {{"type", String.Format("/room/{0}/shutdown/", Room.Key)}, {"content", roomStatus}};
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
            var message = new JObject { { "type", String.Format("/room/{0}/shutdown/", Room.Key) }, { "content", roomStatus } };
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
            var message = new JObject {{"type", String.Format("/room/{0}/shutdown/", Room.Key)}, {"content", roomStatus}};
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
            PostStatusMessage(GetFullStatusForClientId(id, room));
        }


        /// <summary>
        /// Gets full room status
        /// </summary>
        /// <param name="room">The room to get status of</param>
        /// <returns>The status response message</returns>
        MobileControlResponseMessage GetFullStatusForClientId(string id, IEssentialsRoom room)
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

            var state = new RoomStateMessage();

            state.Configuration = GetRoomConfiguration(room);
            state.ActivityMode = 1;
            state.IsOn = room.OnFeedback.BoolValue;
            state.SelectedSourceKey = sourceKey;
            state.Volumes = volumes;
            state.IsWarmingUp = room.IsWarmingUpFeedback.BoolValue;
            state.IsCoolingDown = room.IsCoolingDownFeedback.BoolValue;

            var vtcRoom = room as IEssentialsHuddleVtc1Room;
            if (vtcRoom != null)
            {
                state.IsInCall = vtcRoom.InCallFeedback.BoolValue;
            }

            var messageObject = new MobileControlResponseMessage
            {
                Type = MessagePath,
                ClientId = id,
                Content = state
            };

            return messageObject;
        }

        /// <summary>
        /// Determines the configuration of the room and the details about the devices associated with the room
        /// <param name="room"></param>
        /// <returns></returns>
        private RoomConfiguration GetRoomConfiguration(IEssentialsRoom room)
        {
            var configuration = new RoomConfiguration();

            var huddleRoom = room as IEssentialsHuddleSpaceRoom;
            if (huddleRoom != null && !string.IsNullOrEmpty(huddleRoom.PropertiesConfig.HelpMessageForDisplay))
            {
                configuration.HelpMessage = huddleRoom.PropertiesConfig.HelpMessageForDisplay;
            }

            var vtc1Room = room as IEssentialsHuddleVtc1Room;
            if (vtc1Room != null && !string.IsNullOrEmpty(vtc1Room.PropertiesConfig.HelpMessageForDisplay))
            {
                configuration.HelpMessage = vtc1Room.PropertiesConfig.HelpMessageForDisplay;
            }

            var techRoom = room as EssentialsTechRoom;
            if (techRoom != null && !string.IsNullOrEmpty(techRoom.PropertiesConfig.HelpMessage))
            {
                configuration.HelpMessage = techRoom.PropertiesConfig.HelpMessage;
            }

            var vcRoom = room as IHasVideoCodec;
            if (vcRoom != null)
            {
                if (vcRoom.VideoCodec != null)
                {
                    configuration.HasVideoConferencing = true;
                    configuration.VideoCodecKey = vcRoom.VideoCodec.Key;
                    configuration.VideoCodecIsZoomRoom = vcRoom.VideoCodec is Essentials.Devices.Common.VideoCodec.ZoomRoom.ZoomRoom;
                }
            };

            var acRoom = room as IHasAudioCodec;
            if (acRoom != null)
            {
                if (acRoom.AudioCodec != null)
                {
                    configuration.HasAudioConferencing = true;
                    configuration.AudioCodecKey = acRoom.AudioCodec.Key;
                }
            }

            var envRoom = room as IEnvironmentalControls;
            {
                configuration.HasEnvironmentalControls = envRoom.HasEnvironmentalControlDevices;

                if(envRoom.HasEnvironmentalControlDevices)
                {
                    foreach (var dev in envRoom.EnvironmentalControlDevices)
                    {
                        eEnvironmentalDeviceTypes type = eEnvironmentalDeviceTypes.None;

                        if(dev is Essentials.Core.Lighting.LightingBase)
                        {
                            type = eEnvironmentalDeviceTypes.Lighting;
                        }
                        else if (dev is Essentials.Core.Shades.ShadeBase)
                        {
                            type = eEnvironmentalDeviceTypes.Shade;
                        }
                        else if (dev is Essentials.Core.Shades.ShadeController)
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
                configuration.DefaultDisplayKey = defDisplayRoom.DefaultDisplay.Key;
                configuration.DisplayKeys.Add(defDisplayRoom.DefaultDisplay.Key);
            }

            var multiDisplayRoom = room as IHasMultipleDisplays;
            if (multiDisplayRoom != null)
            {
                foreach(var display in multiDisplayRoom.Displays)
                {
                    configuration.DisplayKeys.Add(display.Value.Key);
                }
            }

            var sourceList = ConfigReader.ConfigObject.GetSourceListForKey(room.SourceListKey);
            if (sourceList != null)
            {
                configuration.SourceList = sourceList;
                configuration.HasRoutingControls = true;

                foreach (var source in sourceList)
                {
                    if (source.Value.SourceDevice is PepperDash.Essentials.Devices.Common.IRSetTopBoxBase)
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

            //var cameraDevices = DeviceManager.AllDevices.Where((d) => d is CameraBase);
            //if (cameraDevices != null && cameraDevices.Count() > 0)
            //{
            //    configuration.HasCameraControls = true;
            //}

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
        [JsonProperty("supportsAdvancedSharing", NullValueHandling = NullValueHandling.Ignore)]
        public bool? SupportsAdvancedSharing { get; set; }
        [JsonProperty("userCanChangeShareMode", NullValueHandling = NullValueHandling.Ignore)]
        public bool? UserCanChangeShareMode { get; set; }
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


        [JsonProperty("helpMessage", NullValueHandling = NullValueHandling.Ignore)]
        public string HelpMessage { get; set; }


        public RoomConfiguration()
        {
            DisplayKeys = new List<string>();
            EnvironmentalDevices = new List<EnvironmentalDeviceConfiguration>();
            SourceList = new Dictionary<string, SourceListItem>();
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
}