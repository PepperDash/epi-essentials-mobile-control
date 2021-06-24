using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Room.MobileControl;
using PepperDash.Essentials.Devices.Common.VideoCodec;
using PepperDash.Essentials.Devices.Common.AudioCodec;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Devices.Common.SoftCodec;

namespace PepperDash.Essentials
{
    public class MobileControlEssentialsRoomBridge : MobileControlBridgeBase
    {
        public EssentialsRoomBase Room { get; private set; }

        public VideoCodecBaseMessenger VcMessenger { get; private set; }

        public AudioCodecBaseMessenger AcMessenger { get; private set; }

        public Dictionary<string, MessengerBase> DeviceMessengers { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public override string RoomName
        {
            get { return Room.Name; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="room"></param>
        public MobileControlEssentialsRoomBridge(EssentialsRoomBase room) :
            base(string.Format("mobileControlBridge-{0}", room.Key), "Essentials Mobile Control Bridge")
        {
            Room = room;
        }

        /// <summary>
        /// Override of base: calls base to add parent and then registers actions and events.
        /// </summary>
        /// <param name="parent"></param>
        public override void AddParent(MobileControlSystemController parent)
        {
            base.AddParent(parent);

            // we add actions to the messaging system with a path, and a related action. Custom action
            // content objects can be handled in the controller's LineReceived method - and perhaps other
            // sub-controller parsing could be attached to these classes, so that the systemController
            // doesn't need to know about everything.

            Parent.AddAction(string.Format(@"/room/{0}/promptForCode", Room.Key), new Action(OnUserPromptedForCode));
            Parent.AddAction(string.Format(@"/room/{0}/clientJoined", Room.Key), new Action(OnClientJoined));

            // Source Changes and room off
            Parent.AddAction(string.Format(@"/room/{0}/status", Room.Key), new ClientSpecificUpdateRequest(() => GetFullStatus(Room)));

            var routeRoom = Room as IRunRouteAction;
            if (routeRoom != null)
                Parent.AddAction(string.Format(@"/room/{0}/source", Room.Key),
                    new Action<SourceSelectMessageContent>(c =>
                    {
                        var sourceListKey = string.Empty;

                        routeRoom.RunRouteAction(c.SourceListItem, sourceListKey);

                    }));

            var directRouteRoom = Room as IRunDirectRouteAction;
            if (directRouteRoom != null)
            {
                Parent.AddAction(String.Format("/room/{0}/directRoute", Room.Key), new Action<DirectRoute>((d) => directRouteRoom.RunDirectRoute(d.SourceKey, d.DestinationKey)));
            }


            var defaultRoom = Room as IRunDefaultPresentRoute;
            if (defaultRoom != null)
                Parent.AddAction(string.Format(@"/room/{0}/defaultsource", Room.Key),
                    new Action(() => defaultRoom.RunDefaultPresentRoute()));

            var volumeRoom = Room as IHasCurrentVolumeControls;
            if (volumeRoom != null)
            {
                Parent.AddAction(string.Format(@"/room/{0}/volumes/master/level", Room.Key), new Action<ushort>(u =>
                {
                    var basicVolumeWithFeedback = volumeRoom.CurrentVolumeControls as IBasicVolumeWithFeedback;
                    if (basicVolumeWithFeedback != null)
                        basicVolumeWithFeedback.SetVolume(u);
                }));
                Parent.AddAction(string.Format(@"/room/{0}/volumes/master/muteToggle", Room.Key), new Action(() =>
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
                var key = vcRoom.VideoCodec.Key + "-" + parent.Key;
                VcMessenger = new VideoCodecBaseMessenger(key, vcRoom.VideoCodec, "/device/videoCodec");
                VcMessenger.RegisterWithAppServer(Parent);

                vcRoom.IsSharingFeedback.OutputChange += IsSharingFeedback_OutputChange;
            }

            var acRoom = Room as IHasAudioCodec;
            if (acRoom != null && acRoom.AudioCodec != null)
            {
                var key = acRoom.AudioCodec.Key + "-" + parent.Key;
                AcMessenger = new AudioCodecBaseMessenger(key, acRoom.AudioCodec, "/device/audioCodec");
                AcMessenger.RegisterWithAppServer(Parent);
            }

            var privacyRoom = Room as IPrivacy;
            if (privacyRoom != null)
            {
                Parent.AddAction(string.Format(@"/room/{0}/volumes/master/privacyMuteToggle", Room.Key), new Action(privacyRoom.PrivacyModeToggle));

                privacyRoom.PrivacyModeIsOnFeedback.OutputChange += PrivacyModeIsOnFeedback_OutputChange;
            }

            SetupDeviceMessengers();

            var defCallRm = Room as IRunDefaultCallRoute;
            if (defCallRm != null)
            {
                Parent.AddAction(string.Format(@"/room/{0}/activityVideo", Room.Key),
                    new Action(() => defCallRm.RunDefaultCallRoute()));
            }

            Parent.AddAction(string.Format(@"/room/{0}/shutdownStart", Room.Key),
                new Action(() => Room.StartShutdown(eShutdownType.Manual)));
            Parent.AddAction(string.Format(@"/room/{0}/shutdownEnd", Room.Key),
                new Action(() => Room.ShutdownPromptTimer.Finish()));
            Parent.AddAction(string.Format(@"/room/{0}/shutdownCancel", Room.Key),
                new Action(() => Room.ShutdownPromptTimer.Cancel()));

            Room.OnFeedback.OutputChange += OnFeedback_OutputChange;
            Room.IsCoolingDownFeedback.OutputChange += IsCoolingDownFeedback_OutputChange;
            Room.IsWarmingUpFeedback.OutputChange += IsWarmingUpFeedback_OutputChange;

            Room.ShutdownPromptTimer.HasStarted += ShutdownPromptTimer_HasStarted;
            Room.ShutdownPromptTimer.HasFinished += ShutdownPromptTimer_HasFinished;
            Room.ShutdownPromptTimer.WasCancelled += ShutdownPromptTimer_WasCancelled;

            AddTechRoomActions();
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
            DeviceMessengers.Add(scheduleMessenger.Key, scheduleMessenger);
            scheduleMessenger.RegisterWithAppServer(Parent);
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
            PostStatusMessage(new
            {
                volumes = new
                {
                    master = new
                    {
                        privacyMuted = e.BoolValue
                    }
                }
            });
        }

        /// <summary>
        /// Set up the messengers for each device type
        /// </summary>
        private void SetupDeviceMessengers()
        {
            DeviceMessengers = new Dictionary<string, MessengerBase>();

            foreach (var device in DeviceManager.AllDevices)
            {
                Debug.Console(2, this, "Attempting to set up device messenger for device: {0}", device.Key);

                if (device is CameraBase)
                {
                    var camDevice = device as CameraBase;
                    Debug.Console(2, this, "Adding CameraBaseMessenger for device: {0}", device.Key);
                    var cameraMessenger = new CameraBaseMessenger(device.Key + "-" + Parent.Key, camDevice,
                        "/device/" + device.Key);
                    DeviceMessengers.Add(device.Key, cameraMessenger);
                    cameraMessenger.RegisterWithAppServer(Parent);
                }

                if (device is BlueJeansPc)
                {
                    var softCodecDevice = device as BlueJeansPc;
                    Debug.Console(2, this, "Adding IRunRouteActionMessnger for device: {0}", device.Key);
                    var routeMessenger = new RunRouteActionMessenger(device.Key + "-" + Parent.Key, softCodecDevice,
                        "/device/" + device.Key);
                    DeviceMessengers.Add(device.Key, routeMessenger);
                    routeMessenger.RegisterWithAppServer(Parent);
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
                        DeviceMessengers.Add(device.Key, presetsMessenger);
                        presetsMessenger.RegisterWithAppServer(Parent);
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
                    DeviceMessengers.Add(twoWayDisplayMessenger.Key, twoWayDisplayMessenger);
                    twoWayDisplayMessenger.RegisterWithAppServer(Parent);
                }

                if (device is ICommunicationMonitor)
                {
                    var monitor = device as ICommunicationMonitor;
                    Debug.Console(2, this, "Adding CommunicationMonitor for device: {0}", device.Key);
                    var communicationMonitorMessenger = new CommMonitorMessenger(device.Key + "-" + Parent.Key + "-monitor",
                        String.Format("/device/{0}/commMonitor", device.Key), monitor);
                    DeviceMessengers.Add(communicationMonitorMessenger.Key, communicationMonitorMessenger);
                    communicationMonitorMessenger.RegisterWithAppServer(Parent);
                }

                if (device is IBasicVolumeWithFeedback)
                {
                    var deviceKey = device.Key;
                    var volControlDevice = device as IBasicVolumeWithFeedback;
                    Debug.Console(2, this, "Adding IBasicVolumeControlWithFeedback for device: {0}", deviceKey);
                    var messenger = new DeviceVolumeMessenger(deviceKey + "-" + Parent.Key + "-volume",
                        String.Format("/device/{0}/volume", deviceKey), deviceKey, volControlDevice);
                    DeviceMessengers.Add(messenger.Key, messenger);
                    messenger.RegisterWithAppServer(Parent);
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

            PostStatusMessage(new
            {
                share = new
                {
                    currentShareText = shareText,
                    isSharing
                }
            });
        }

        /// <summary>
        /// Helper for posting status message
        /// </summary>
        /// <param name="contentObject">The contents of the content object</param>
        private void PostStatusMessage(object contentObject)
        {
            Parent.SendMessageObjectToServer(JObject.FromObject(new
            {
                type = String.Format("/room/{0}/status/",Room.Key),
                content = contentObject
            }));
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
            Parent.SendMessageObjectToServer(message);
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
            Parent.SendMessageObjectToServer(message);
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
            Parent.SendMessageObjectToServer(message);
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
            PostStatusMessage(new
            {
                isWarmingUp = e.BoolValue
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsCoolingDownFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostStatusMessage(new
            {
                isCoolingDown = e.BoolValue
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostStatusMessage(new
            {
                isOn = e.BoolValue
            });
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
            PostStatusMessage(new
            {
                volumes = new
                {
                    master = new
                    {
                        muted = e.BoolValue
                    }
                }
            });
        }

        /// <summary>
        /// Handles Volume changes on room
        /// </summary>
        private void VolumeLevelFeedback_OutputChange(object sender, FeedbackEventArgs e)
        {
            PostStatusMessage(new
            {
                volumes = new
                {
                    master = new
                    {
                        level = e.IntValue
                    }
                }
            });
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
                        PostStatusMessage(new
                        {
                            selectedSourceKey = srcRm.CurrentSourceInfoKey
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Sends the full status of the room to the server
        /// </summary>
        /// <param name="room"></param>
        private void SendFullStatus(EssentialsRoomBase room)
        {
            Parent.SendMessageObjectToServer(new
            {
                type = String.Format("/room/{0}/status/", Room.Key),
                content = GetFullStatus(room)
            });
        }


        /// <summary>
        /// Gets full room status
        /// </summary>
        /// <param name="room">The room to get status of</param>
        /// <returns>The status response message</returns>
        MobileControlResponseMessage GetFullStatus(EssentialsRoomBase room)
        {
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

            var contentObject = new
            {
                activityMode = 1,
                isOn = room.OnFeedback.BoolValue,
                selectedSourceKey = sourceKey, 
                volumes,
            };

            var messageObject = new MobileControlResponseMessage
            {
                Type = String.Format("/room/{0}/status/",Room.Key),
                Content = contentObject
            };

            return messageObject;
        }
    }



    /// <summary>
    /// 
    /// </summary>
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