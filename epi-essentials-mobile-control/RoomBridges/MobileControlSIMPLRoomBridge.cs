﻿using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.EthernetCommunication;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Devices.Common.Codec;
using PepperDash.Essentials.Devices.Common.Cameras;
using PepperDash.Essentials.Room.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;


namespace PepperDash.Essentials.Room.MobileControl
{
// ReSharper disable once InconsistentNaming
    public class MobileControlSIMPLRoomBridge : MobileControlBridgeBase, IDelayedConfiguration
    {
        /// <summary>
        /// Fires when config is ready to go
        /// </summary>
        public event EventHandler<EventArgs> ConfigurationIsReady;

        public ThreeSeriesTcpIpEthernetIntersystemCommunications Eisc { get; private set; }

        public MobileControlSIMPLRoomJoinMap JoinMap { get; private set; }

        public Dictionary<string, MessengerBase> DeviceMessengers { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public bool ConfigIsLoaded { get; private set; }

        public override string RoomName
        {
            get
            {
                var name = Eisc.StringOutput[JoinMap.ConfigRoomName.JoinNumber].StringValue;
                return string.IsNullOrEmpty(name) ? "Not Loaded" : name;
            }
        }

        private readonly MobileControlSimplDeviceBridge _sourceBridge;

        private SIMPLAtcMessenger _atcMessenger;
        private SIMPLVtcMessenger _vtcMessenger;
        private SimplDirectRouteMessenger _directRouteMessenger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="ipId"></param>
        public MobileControlSIMPLRoomBridge(string key, string name, uint ipId)
            : base(key, name)
        {
            Eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(ipId, "127.0.0.2", Global.ControlSystem);
            var reg = Eisc.Register();
            if (reg != eDeviceRegistrationUnRegistrationResponse.Success)
                Debug.Console(0, this, "Cannot connect EISC at IPID {0}: \r{1}", ipId, reg);

            JoinMap = new MobileControlSIMPLRoomJoinMap(1);

            _sourceBridge = new MobileControlSimplDeviceBridge(key + "-sourceBridge", "SIMPL source bridge", Eisc);
            DeviceManager.AddDevice(_sourceBridge);
        }

        /// <summary>
        /// Finish wiring up everything after all devices are created. The base class will hunt down the related
        /// parent controller and link them up.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            Debug.Console(0, this, "Final activation. Setting up actions and feedbacks");
            SetupFunctions();
            SetupFeedbacks();

            var atcKey = string.Format("atc-{0}-{1}", Key, Parent.Key);
            _atcMessenger = new SIMPLAtcMessenger(atcKey, Eisc, "/device/audioCodec");
            _atcMessenger.RegisterWithAppServer(Parent);

            var vtcKey = string.Format("atc-{0}-{1}", Key, Parent.Key);
            _vtcMessenger = new SIMPLVtcMessenger(vtcKey, Eisc, "/device/videoCodec");
            _vtcMessenger.RegisterWithAppServer(Parent);

            var drKey = String.Format("directRoute-{0}-{1}", Key, Parent.Key);
            _directRouteMessenger = new SimplDirectRouteMessenger(drKey, Eisc, "/room/room1/routing");
            _directRouteMessenger.RegisterWithAppServer(Parent);

            Eisc.SigChange += EISC_SigChange;
            Eisc.OnlineStatusChange += (o, a) =>
            {
                Debug.Console(1, this, "SIMPL EISC online={0}. Config is ready={1}. Use Essentials Config={2}",
                    a.DeviceOnLine, Eisc.BooleanOutput[JoinMap.ConfigIsReady.JoinNumber].BoolValue,
                    Eisc.BooleanOutput[JoinMap.ConfigIsLocal.JoinNumber].BoolValue);

                if (a.DeviceOnLine && Eisc.BooleanOutput[JoinMap.ConfigIsReady.JoinNumber].BoolValue)
                    LoadConfigValues();

                if (a.DeviceOnLine && Eisc.BooleanOutput[JoinMap.ConfigIsLocal.JoinNumber].BoolValue)
                    UseEssentialsConfig();
            };
            // load config if it's already there
            if (Eisc.IsOnline && Eisc.BooleanOutput[JoinMap.ConfigIsReady.JoinNumber].BoolValue)
                // || EISC.BooleanInput[JoinMap.ConfigIsReady].BoolValue)
                LoadConfigValues();

            if (Eisc.IsOnline && Eisc.BooleanOutput[JoinMap.ConfigIsLocal.JoinNumber].BoolValue)
            {
                UseEssentialsConfig();
            }

            CrestronConsole.AddNewConsoleCommand(s =>
            {
                JoinMap.PrintJoinMapInfo();

                _atcMessenger.JoinMap.PrintJoinMapInfo();

                _vtcMessenger.JoinMap.PrintJoinMapInfo();

                // TODO: Update Source Bridge to use new JoinMap scheme
                //_sourceBridge.JoinMap.PrintJoinMapInfo();
            }, "printmobilebridge", "Prints MC-SIMPL bridge EISC data", ConsoleAccessLevelEnum.AccessOperator);

            return base.CustomActivate();
        }
        
        private void UseEssentialsConfig()
        {
            ConfigIsLoaded = false;

            SetupDeviceMessengers();

            Debug.Console(0, this, "******* ESSENTIALS CONFIG: \r{0}",
                JsonConvert.SerializeObject(ConfigReader.ConfigObject, Formatting.Indented));

            var handler = ConfigurationIsReady;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }

            ConfigIsLoaded = true;
        }

        /// <summary>
        /// Setup the actions to take place on various incoming API calls
        /// </summary>
        private void SetupFunctions()
        {
            Parent.AddAction(@"/room/room1/promptForCode",
                new Action(() => Eisc.PulseBool(JoinMap.PromptForCode.JoinNumber)));
            Parent.AddAction(@"/room/room1/clientJoined",
                new Action(() => Eisc.PulseBool(JoinMap.ClientJoined.JoinNumber)));

            Parent.AddAction(@"/room/room1/status", new Action(SendFullStatus));

            Parent.AddAction(@"/room/room1/source", new Action<SourceSelectMessageContent>(c =>
            {
                Eisc.SetString(JoinMap.CurrentSourceKey.JoinNumber, c.SourceListItem);
                Eisc.PulseBool(JoinMap.SourceHasChanged.JoinNumber);
            }));

            Parent.AddAction(@"/room/room1/defaultsource", new Action(() =>
                Eisc.PulseBool(JoinMap.ActivityShare.JoinNumber)));
            Parent.AddAction(@"/room/room1/activityPhone", new Action(() =>
                Eisc.PulseBool(JoinMap.ActivityPhoneCall.JoinNumber)));
            Parent.AddAction(@"/room/room1/activityVideo", new Action(() =>
                Eisc.PulseBool(JoinMap.ActivityVideoCall.JoinNumber)));

            Parent.AddAction(@"/room/room1/volumes/master/level", new Action<ushort>(u =>
                Eisc.SetUshort(JoinMap.MasterVolume.JoinNumber, u)));
            Parent.AddAction(@"/room/room1/volumes/master/muteToggle", new Action(() =>
                Eisc.PulseBool(JoinMap.MasterVolume.JoinNumber)));
            Parent.AddAction(@"/room/room1/volumes/master/privacyMuteToggle", new Action(() =>
                Eisc.PulseBool(JoinMap.PrivacyMute.JoinNumber)));


            // /xyzxyz/volumes/master/muteToggle ---> BoolInput[1]

            var volumeStart = JoinMap.VolumeJoinStart.JoinNumber;
            var volumeEnd = JoinMap.VolumeJoinStart.JoinNumber + JoinMap.VolumeJoinStart.JoinSpan;

            for (uint i = volumeStart; i <= volumeEnd; i++)
            {
                var index = i;
                Parent.AddAction(string.Format(@"/room/room1/volumes/level-{0}/level", index), new Action<ushort>(u =>
                    Eisc.SetUshort(index, u)));
                Parent.AddAction(string.Format(@"/room/room1/volumes/level-{0}/muteToggle", index), new Action(() =>
                    Eisc.PulseBool(index)));
            }

            Parent.AddAction(@"/room/room1/shutdownStart", new Action(() =>
                Eisc.PulseBool(JoinMap.ShutdownStart.JoinNumber)));
            Parent.AddAction(@"/room/room1/shutdownEnd", new Action(() =>
                Eisc.PulseBool(JoinMap.ShutdownEnd.JoinNumber)));
            Parent.AddAction(@"/room/room1/shutdownCancel", new Action(() =>
                Eisc.PulseBool(JoinMap.ShutdownCancel.JoinNumber)));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="devKey"></param>
        private void SetupSourceFunctions(string devKey)
        {
            var sourceJoinMap = new SourceDeviceMapDictionary();

            var prefix = string.Format("/device/{0}/", devKey);

            foreach (var item in sourceJoinMap)
            {
                var join = item.Value;
                Parent.AddAction(string.Format("{0}{1}", prefix, item.Key),
                    new PressAndHoldAction(b => Eisc.SetBool(join, b)));
            }
        }


        /// <summary>
        /// Links feedbacks to whatever is gonna happen!
        /// </summary>
        private void SetupFeedbacks()
        {
            // Power 
            Eisc.SetBoolSigAction(JoinMap.RoomIsOn.JoinNumber, b =>
                PostStatusMessage(new
                {
                    isOn = b
                }));

            // Source change things
            Eisc.SetSigTrueAction(JoinMap.SourceHasChanged.JoinNumber, () =>
                PostStatusMessage(new
                {
                    selectedSourceKey = Eisc.StringOutput[JoinMap.CurrentSourceKey.JoinNumber].StringValue
                }));

            // Volume things
            Eisc.SetUShortSigAction(JoinMap.MasterVolume.JoinNumber, u =>
                PostStatusMessage(new
                {
                    volumes = new
                    {
                        master = new
                        {
                            level = u
                        }
                    }
                }));

            // map MasterVolumeIsMuted join -> status/volumes/master/muted
            // 

            Eisc.SetBoolSigAction(JoinMap.MasterVolume.JoinNumber, b =>
                PostStatusMessage(new
                {
                    volumes = new
                    {
                        master = new
                        {
                            muted = b
                        }
                    }
                }));
            Eisc.SetBoolSigAction(JoinMap.PrivacyMute.JoinNumber, b =>
                PostStatusMessage(new
                {
                    volumes = new
                    {
                        master = new
                        {
                            privacyMuted = b
                        }
                    }
                }));

            var volumeStart = JoinMap.VolumeJoinStart.JoinNumber;
            var volumeEnd = JoinMap.VolumeJoinStart.JoinNumber + JoinMap.VolumeJoinStart.JoinSpan;

            for (uint i = volumeStart; i <= volumeEnd; i++)
            {
                var index = i; // local scope for lambdas
                Eisc.SetUShortSigAction(index, u => // start at join 2
                {
                    // need a dict in order to create the level-n property on auxFaders
                    var dict = new Dictionary<string, object> {{"level-" + index, new {level = u}}};
                    PostStatusMessage(new
                    {
                        volumes = new
                        {
                            auxFaders = dict,
                        }
                    });
                });
                Eisc.SetBoolSigAction(index, b =>
                {
                    // need a dict in order to create the level-n property on auxFaders
                    var dict = new Dictionary<string, object> {{"level-" + index, new {muted = b}}};
                    PostStatusMessage(new
                    {
                        volumes = new
                        {
                            auxFaders = dict,
                        }
                    });
                });
            }

            Eisc.SetUShortSigAction(JoinMap.NumberOfAuxFaders.JoinNumber, u =>
                PostStatusMessage(new
                {
                    volumes = new
                    {
                        numberOfAuxFaders = u,
                    }
                }));

            // shutdown things
            Eisc.SetSigTrueAction(JoinMap.ShutdownCancel.JoinNumber, () =>
                PostMessage("/room/shutdown/", new
                {
                    state = "wasCancelled"
                }));
            Eisc.SetSigTrueAction(JoinMap.ShutdownEnd.JoinNumber, () =>
                PostMessage("/room/shutdown/", new
                {
                    state = "hasFinished"
                }));
            Eisc.SetSigTrueAction(JoinMap.ShutdownStart.JoinNumber, () =>
                PostMessage("/room/shutdown/", new
                {
                    state = "hasStarted",
                    duration = Eisc.UShortOutput[JoinMap.ShutdownPromptDuration.JoinNumber].UShortValue
                }));

            // Config things
            Eisc.SetSigTrueAction(JoinMap.ConfigIsReady.JoinNumber, LoadConfigValues);

            // Activity modes
            Eisc.SetSigTrueAction(JoinMap.ActivityShare.JoinNumber, () => UpdateActivity(1));
            Eisc.SetSigTrueAction(JoinMap.ActivityPhoneCall.JoinNumber, () => UpdateActivity(2));
            Eisc.SetSigTrueAction(JoinMap.ActivityVideoCall.JoinNumber, () => UpdateActivity(3));
        }


        /// <summary>
        /// Updates activity states
        /// </summary>
        private void UpdateActivity(int mode)
        {
            PostStatusMessage(new
            {
                activityMode = mode,
            });
        }

        /// <summary>
        /// Reads in config values when the Simpl program is ready
        /// </summary>
        private void LoadConfigValues()
        {
            Debug.Console(1, this, "Loading configuration from SIMPL EISC bridge");
            ConfigIsLoaded = false;

            var co = ConfigReader.ConfigObject;

            co.Info.RuntimeInfo.AppName = Assembly.GetExecutingAssembly().GetName().Name;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            co.Info.RuntimeInfo.AssemblyVersion = string.Format("{0}.{1}.{2}", version.Major, version.Minor,
                version.Build);

            //Room
            //if (co.Rooms == null)
            // always start fresh in case simpl changed
            co.Rooms = new List<DeviceConfig>();
            var rm = new DeviceConfig();
            if (co.Rooms.Count == 0)
            {
                Debug.Console(0, this, "Adding room to config");
                co.Rooms.Add(rm);
            }
            else
            {
                Debug.Console(0, this, "Replacing Room[0] in config");
                co.Rooms[0] = rm;
            }
            rm.Name = Eisc.StringOutput[JoinMap.ConfigRoomName.JoinNumber].StringValue;
            rm.Key = "room1";
            rm.Type = "SIMPL01";

            var rmProps = rm.Properties == null
                ? new DDVC01RoomPropertiesConfig()
                : JsonConvert.DeserializeObject<DDVC01RoomPropertiesConfig>(rm.Properties.ToString());

            rmProps.Help = new EssentialsHelpPropertiesConfig
            {
                CallButtonText = Eisc.StringOutput[JoinMap.ConfigHelpNumber.JoinNumber].StringValue,
                Message = Eisc.StringOutput[JoinMap.ConfigHelpMessage.JoinNumber].StringValue
            };

            rmProps.Environment = new EssentialsEnvironmentPropertiesConfig(); // enabled defaults to false

            rmProps.RoomPhoneNumber = Eisc.StringOutput[JoinMap.ConfigRoomPhoneNumber.JoinNumber].StringValue;
            rmProps.RoomURI = Eisc.StringOutput[JoinMap.ConfigRoomUri.JoinNumber].StringValue;
            rmProps.SpeedDials = new List<DDVC01SpeedDial>();

            // This MAY need a check 
            if (Eisc.BooleanOutput[JoinMap.ActivityPhoneCallEnable.JoinNumber].BoolValue)
            {
                rmProps.AudioCodecKey = "audioCodec"; 
            }

            if (Eisc.BooleanOutput[JoinMap.ActivityVideoCallEnable.JoinNumber].BoolValue)
            {
                rmProps.VideoCodecKey = "videoCodec";
            }

            // volume control names

            //// use Volumes object or?
            //rmProps.VolumeSliderNames = new List<string>();
            //for(uint i = 701; i <= 700 + volCount; i++)
            //{
            //    rmProps.VolumeSliderNames.Add(EISC.StringInput[i].StringValue);
            //}

            // There should be Mobile Control devices in here, I think...
            if (co.Devices == null)
                co.Devices = new List<DeviceConfig>();

            // clear out previous SIMPL devices
            co.Devices.RemoveAll(d =>
                d.Key.StartsWith("source-", StringComparison.OrdinalIgnoreCase)
                || d.Key.Equals("audioCodec", StringComparison.OrdinalIgnoreCase)
                || d.Key.Equals("videoCodec", StringComparison.OrdinalIgnoreCase)
            || d.Key.StartsWith("destination-", StringComparison.OrdinalIgnoreCase));

            rmProps.SourceListKey = "default";
            rm.Properties = JToken.FromObject(rmProps);

            // Source list! This might be brutal!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            var groupMap = GetSourceGroupDictionary();

            co.SourceLists = new Dictionary<string, Dictionary<string, SourceListItem>>();
            var newSl = new Dictionary<string, SourceListItem>();
            // add "none" source if VTC present

            if (!string.IsNullOrEmpty(rmProps.VideoCodecKey))
            {
                var codecOsd = new SourceListItem
                {
                    Name = "None",
                    IncludeInSourceList = true,
                    Order = 1,
                    Type = eSourceListItemType.Route,
                    SourceKey = ""
                };
                newSl.Add("Source-None", codecOsd);
            }
            // add sources...
            var useSourceEnabled = Eisc.BooleanOutput[JoinMap.UseSourceEnabled.JoinNumber].BoolValue;
            for (uint i = 0; i <= 19; i++)
            {
                var name = Eisc.StringOutput[JoinMap.SourceNameJoinStart.JoinNumber + i].StringValue;
                if (useSourceEnabled
                    && !Eisc.BooleanOutput[JoinMap.SourceIsEnabledJoinStart.JoinNumber + i].BoolValue)
                {
                    continue;
                }

                if (!Eisc.BooleanOutput[JoinMap.UseSourceEnabled.JoinNumber].BoolValue && string.IsNullOrEmpty(name))
                    break;

                var icon = Eisc.StringOutput[JoinMap.SourceIconJoinStart.JoinNumber + i].StringValue;
                var key = Eisc.StringOutput[JoinMap.SourceKeyJoinStart.JoinNumber + i].StringValue;
                var type = Eisc.StringOutput[JoinMap.SourceTypeJoinStart.JoinNumber + i].StringValue;
                var disableShare = Eisc.BooleanOutput[JoinMap.SourceShareDisableJoinStart.JoinNumber + i].BoolValue;

                Debug.Console(0, this, "Adding source {0} '{1}'", key, name);

                var sourceKey = Eisc.StringOutput[JoinMap.SourceControlDeviceKeyJoinStart.JoinNumber + i].StringValue;

                var newSli = new SourceListItem
                {
                    Icon = icon,
                    Name = name,
                    Order = (int) i + 10,
                    SourceKey = string.IsNullOrEmpty(sourceKey) ? key : sourceKey, // Use the value from the join if defined
                    Type = eSourceListItemType.Route,
                    DisableCodecSharing = disableShare,
                };
                newSl.Add(key, newSli);

                var existingSourceDevice = DeviceManager.GetDeviceForKey(newSli.SourceKey);

                // Look to see if this is a device that already exists in Essentials and get it
                if (existingSourceDevice != null)
                {
                    Debug.Console(0, this, "Found device with key: {0} in Essentials.", key);
                }
                else
                {
                    // If not, synthesize the device config
                    var group = "genericsource";
                    if (groupMap.ContainsKey(type))
                    {
                        group = groupMap[type];
                    }

                    // add dev to devices list
                    var devConf = new DeviceConfig
                    {
                        Group = group,
                        Key = key,
                        Name = name,
                        Type = type
                    };

                    if (group.ToLower().StartsWith("settopbox")) // Add others here as needed
                    {
                        SetupSourceFunctions(key);
                    }

                    if (group.ToLower().Equals("simplmessenger"))
                    {
                        if (type.ToLower().Equals("simplcameramessenger"))
                        {
                            var props = new SimplMessengerPropertiesConfig();
                            props.DeviceKey = key;
                            props.JoinMapKey = "";
                            var joinStart = 1000 + (i * 100) + 1; // 1001, 1101, 1201, 1301... etc.
                            props.JoinStart = joinStart;
                            devConf.Properties = JToken.FromObject(props);
                        }
                    }

                    co.Devices.Add(devConf);
                }
            }

            co.SourceLists.Add("default", newSl);

            if (Eisc.BooleanOutput[JoinMap.SupportsAdvancedSharing.JoinNumber].BoolValue)
            {
                CreateDestinationList(co);
            }

            // Build "audioCodec" config if we need
            if (!string.IsNullOrEmpty(rmProps.AudioCodecKey))
            {
                var acFavs = new List<CodecActiveCallItem>();
                for (uint i = 0; i < 4; i++)
                {
                    if (!Eisc.GetBool(JoinMap.SpeedDialVisibleStartJoin.JoinNumber + i))
                    {
                        break;
                    }
                    acFavs.Add(new CodecActiveCallItem
                    {
                        Name = Eisc.GetString(JoinMap.SpeedDialNameStartJoin.JoinNumber + i),
                        Number = Eisc.GetString(JoinMap.SpeedDialNumberStartJoin.JoinNumber + i),
                        Type = eCodecCallType.Audio
                    });
                }

                var acProps = new
                {
                    favorites = acFavs
                };

                const string acStr = "audioCodec";
                var acConf = new DeviceConfig
                {
                    Group = acStr,
                    Key = acStr,
                    Name = acStr,
                    Type = acStr,
                    Properties = JToken.FromObject(acProps)
                };
                co.Devices.Add(acConf);
            }

            // Build Video codec config
            if (!string.IsNullOrEmpty(rmProps.VideoCodecKey))
            {
                // No favorites, for now?
                var favs = new List<CodecActiveCallItem>();

                // cameras
                var camsProps = new List<object>();
                for (uint i = 0; i < 9; i++)
                {
                    var name = Eisc.GetString(i + JoinMap.CameraNearNameStart.JoinNumber);
                    if (!string.IsNullOrEmpty(name))
                    {
                        camsProps.Add(new
                        {
                            name,
                            selector = "camera" + (i + 1),
                        });
                    }
                }
                var farName = Eisc.GetString(JoinMap.CameraFarName.JoinNumber);
                if (!string.IsNullOrEmpty(farName))
                {
                    camsProps.Add(new
                    {
                        name = farName,
                        selector = "cameraFar",
                    });
                }

                var props = new
                {
                    favorites = favs,
                    cameras = camsProps,
                };
                const string str = "videoCodec";
                var conf = new DeviceConfig
                {
                    Group = str,
                    Key = str,
                    Name = str,
                    Type = str,
                    Properties = JToken.FromObject(props)
                };
                co.Devices.Add(conf);
            }

            SetupDeviceMessengers();

            Debug.Console(0, this, "******* CONFIG FROM SIMPL: \r{0}",
                JsonConvert.SerializeObject(ConfigReader.ConfigObject, Formatting.Indented));

            var handler = ConfigurationIsReady;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }

            ConfigIsLoaded = true;
        }

        private void CreateDestinationList(BasicConfig co)
        {
            var useDestEnable = Eisc.BooleanOutput[JoinMap.UseDestinationEnable.JoinNumber].BoolValue;

            var newDl = new Dictionary<string, DestinationListItem>();

            for (uint i = 0; i < 9; i++)
            {
                var name = Eisc.StringOutput[JoinMap.DestinationNameJoinStart.JoinNumber + i].StringValue;
                var routeType = Eisc.StringOutput[JoinMap.DestinationTypeJoinStart.JoinNumber + i].StringValue;
                var key = Eisc.StringOutput[JoinMap.DestinationDeviceKeyJoinStart.JoinNumber + i].StringValue;
                //var order = Eisc.UShortOutput[JoinMap.DestinationOrderJoinStart.JoinNumber + i].UShortValue;
                var enabled = Eisc.UShortOutput[JoinMap.DestinationIsEnabledJoinStart.JoinNumber + i].BoolValue;

                if (useDestEnable && !enabled)
                {
                    continue;
                }

                Debug.Console(0, this, "Adding destination {0} - {1}", key, name);

                eRoutingSignalType parsedType;
                try
                {
                    parsedType = (eRoutingSignalType) Enum.Parse(typeof (eRoutingSignalType), routeType, true);
                }
                catch (Exception e)
                {
                    Debug.Console(0, this, "Error parsing destination type: {0}", routeType);
                    parsedType = eRoutingSignalType.AudioVideo;
                }

                var newDli = new DestinationListItem
                {
                    Name = name,
                    Order = (int) i,
                    SinkKey = key,
                    SinkType = parsedType,
                };

                newDl.Add(key, newDli);

                //add same DestinationListItem to dictionary for messenger in order to allow for correlation by index
                _directRouteMessenger.DestinationList.Add(key, newDli);

                var existingDev = DeviceManager.GetDeviceForKey(newDli.SinkKey);

                if (existingDev != null)
                {
                    Debug.Console(0, this, "Found device with key: {0} in Essentials.", key);
                }
                else
                {
                    // If not, synthesize the device config
                    var devConf = new DeviceConfig
                    {
                        Group = "genericdestination",
                        Key = key,
                        Name = name,
                        Type = "genericdestination"
                    };

                    co.Devices.Add(devConf);
                }
            }

            co.DestinationLists.Add("default", newDl);


        }

        /// <summary>
        /// Iterates device config and adds messengers as neede for each device type
        /// </summary>
        private void SetupDeviceMessengers()
        {
            DeviceMessengers = new Dictionary<string, MessengerBase>();

            try
            {
                foreach (var device in ConfigReader.ConfigObject.Devices)
                {
                    if (device.Group.Equals("simplmessenger"))
                    {
                        var props =
                            JsonConvert.DeserializeObject<SimplMessengerPropertiesConfig>(device.Properties.ToString());

                        var messengerKey = string.Format("device-{0}-{1}", Key, Parent.Key);

                        if (DeviceManager.GetDeviceForKey(messengerKey) != null)
                        {
                            Debug.Console(2, this, "Messenger with key: {0} already exists. Skipping...", messengerKey);
                            continue;
                        }

                        var dev = ConfigReader.ConfigObject.GetDeviceForKey(props.DeviceKey);

                        if (dev == null)
                        {
                            Debug.Console(1, this, "Unable to find device config for key: '{0}'", props.DeviceKey);
                            continue;
                        }

                        var type = device.Type.ToLower();
                        MessengerBase messenger = null;

                        if (type.Equals("simplcameramessenger"))
                        {
                            Debug.Console(2, this, "Adding SIMPLCameraMessenger for: '{0}'", props.DeviceKey);
                            messenger = new SIMPLCameraMessenger(messengerKey, Eisc, "/device/" + props.DeviceKey,
                                props.JoinStart);
                        }
                        else if (type.Equals("simplroutemessenger"))
                        {
                            Debug.Console(2, this, "Adding SIMPLRouteMessenger for: '{0}'", props.DeviceKey);
                            messenger = new SIMPLRouteMessenger(messengerKey, Eisc, "/device/" + props.DeviceKey,
                                props.JoinStart);
                        }

                        if (messenger != null)
                        {
                            DeviceManager.AddDevice(messenger);
                            DeviceMessengers.Add(device.Key, messenger);
                            messenger.RegisterWithAppServer(Parent);
                        }
                        else
                        {
                            Debug.Console(2, this, "Unable to add messenger for device: '{0}' of type: '{1}'",
                                props.DeviceKey, type);
                        }
                    }
                    else
                    {
                        var dev = DeviceManager.GetDeviceForKey(device.Key);

                        if (dev != null)
                        {
                            if (dev is CameraBase)
                            {
                                var camDevice = dev as CameraBase;
                                Debug.Console(1, this, "Adding CameraBaseMessenger for device: {0}", dev.Key);
                                var cameraMessenger = new CameraBaseMessenger(device.Key + "-" + Parent.Key, camDevice,
                                    "/device/" + device.Key);
                                DeviceMessengers.Add(device.Key, cameraMessenger);
                                DeviceManager.AddDevice(cameraMessenger);
                                cameraMessenger.RegisterWithAppServer(Parent);
                                continue;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Error Setting up Device Managers: {0}", e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SendFullStatus()
        {
            if (ConfigIsLoaded)
            {
                var count = Eisc.UShortOutput[JoinMap.NumberOfAuxFaders.JoinNumber].UShortValue;

                Debug.Console(1, this, "The Fader Count is : {0}", count);

                // build volumes object, serialize and put in content of method below

                // Create auxFaders
                var auxFaderDict = new Dictionary<string, Volume>();

                var volumeStart = JoinMap.VolumeJoinStart.JoinNumber;

                for (var i = volumeStart; i <= count; i++)
                {
                    auxFaderDict.Add("level-" + i,
                        new Volume("level-" + i,
                            Eisc.UShortOutput[i].UShortValue,
                            Eisc.BooleanOutput[i].BoolValue,
                            Eisc.StringOutput[i].StringValue,
                            true,
                            "someting.png"));
                }

                var volumes = new Volumes
                {
                    Master = new Volume("master",
                        Eisc.UShortOutput[JoinMap.MasterVolume.JoinNumber].UShortValue,
                        Eisc.BooleanOutput[JoinMap.MasterVolume.JoinNumber].BoolValue,
                        Eisc.StringOutput[JoinMap.MasterVolume.JoinNumber].StringValue,
                        true,
                        "something.png")
                    {
                        HasPrivacyMute = true,
                        PrivacyMuted = Eisc.BooleanOutput[JoinMap.PrivacyMute.JoinNumber].BoolValue
                    },
                    AuxFaders = auxFaderDict,
                    NumberOfAuxFaders = Eisc.UShortInput[JoinMap.NumberOfAuxFaders.JoinNumber].UShortValue
                };

                PostStatusMessage(new
                {
                    activityMode = GetActivityMode(),
                    isOn = Eisc.BooleanOutput[JoinMap.RoomIsOn.JoinNumber].BoolValue,
                    selectedSourceKey = Eisc.StringOutput[JoinMap.CurrentSourceKey.JoinNumber].StringValue,
                    volumes
                });
            }
            else
            {
                PostStatusMessage(new
                {
                    error = "systemNotReady"
                });
            }
        }

        /// <summary>
        /// Returns the activity mode int
        /// </summary>
        /// <returns></returns>
        private int GetActivityMode()
        {
            if (Eisc.BooleanOutput[JoinMap.ActivityPhoneCall.JoinNumber].BoolValue) return 2;
            if (Eisc.BooleanOutput[JoinMap.ActivityShare.JoinNumber].BoolValue) return 1;

            return Eisc.BooleanOutput[JoinMap.ActivityVideoCall.JoinNumber].BoolValue ? 3 : 0;
        }

        /// <summary>
        /// Helper for posting status message
        /// </summary>
        /// <param name="contentObject">The contents of the content object</param>
        private void PostStatusMessage(object contentObject)
        {
            Parent.SendMessageObjectToServer(new
            {
                type = "/room/status/",
                content = contentObject
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="contentObject"></param>
        private void PostMessage(string messageType, object contentObject)
        {
            Parent.SendMessageObjectToServer(new
            {
                type = messageType,
                content = contentObject
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentDevice"></param>
        /// <param name="args"></param>
        private void EISC_SigChange(object currentDevice, SigEventArgs args)
        {
            if (Debug.Level >= 1)
                Debug.Console(1, this, "SIMPL EISC change: {0} {1}={2}", args.Sig.Type, args.Sig.Number,
                    args.Sig.StringValue);
            var uo = args.Sig.UserObject;
            if (uo != null)
            {
                if (uo is Action<bool>)
                    (uo as Action<bool>)(args.Sig.BoolValue);
                else if (uo is Action<ushort>)
                    (uo as Action<ushort>)(args.Sig.UShortValue);
                else if (uo is Action<string>)
                    (uo as Action<string>)(args.Sig.StringValue);
            }
        }

        /// <summary>
        /// Returns the mapping of types to groups, for setting up devices.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> GetSourceGroupDictionary()
        {
            //type, group
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"laptop", "pc"},
                {"pc", "pc"},
                {"wireless", "genericsource"},
                {"iptv", "settopbox"},
                {"simplcameramessenger", "simplmessenger"},
                {"camera", "camera"},

            };
            return d;
        } 

        /// <summary>
        /// updates the usercode from server
        /// </summary>
        protected override void UserCodeChange()
        {
            base.UserCodeChange();

            Eisc.StringInput[JoinMap.UserCodeToSystem.JoinNumber].StringValue = UserCode;
            Eisc.StringInput[JoinMap.ServerUrl.JoinNumber].StringValue = McServerUrl;
            Eisc.StringInput[JoinMap.QrCodeUrl.JoinNumber].StringValue = QrCodeUrl;

        }
    }
}