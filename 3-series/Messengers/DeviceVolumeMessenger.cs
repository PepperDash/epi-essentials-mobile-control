using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class DeviceVolumeMessenger : MessengerBase
    {
        private readonly IBasicVolumeWithFeedback _localDevice;

        public DeviceVolumeMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public DeviceVolumeMessenger(string key, string messagePath, IBasicVolumeWithFeedback device)
            : base(key, messagePath, device as Device)
        {
            _localDevice = device;
        }

        private void SendStatus()
        {
            var messageObj = new VolumeStateMessage
            {
                Volume = new Volume
                {
                    Level = _localDevice.VolumeLevelFeedback.IntValue,
                    Muted = _localDevice.MuteFeedback.BoolValue,
                }
            };

            PostStatusMessage(messageObj);
        }

        #region Overrides of MessengerBase

#if SERIES4
        protected override void RegisterActions()
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            AddAction("/fullStatus", (id, content) => SendStatus());

            AddAction("/level", (id, content) =>
            {
                var volume = content.ToObject<MobileControlSimpleContent<ushort>>();

                _localDevice.SetVolume(volume.Value);
            });

            AddAction("/muteToggle", (id, content) =>
            {
                var state = content.ToObject<MobileControlSimpleContent<bool>>();

                if (!state.Value) return;

                _localDevice.MuteToggle();
            });

            AddAction("/muteOn", (id, content) =>
            {
                _localDevice.MuteOn();
            });

            AddAction("/muteOff", (id, content) =>
            {
                _localDevice.MuteOff();
            });

            AddAction("/volumeUp", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => 
            {
                Debug.Console(2, this, "volumeUp: {0}", b);
                _localDevice.VolumeUp(b);
             }));



            AddAction("/volumeDown", (id, content) => PressAndHoldHandler.HandlePressAndHold(content, (b) => _localDevice.VolumeDown(b)));

            _localDevice.MuteFeedback.OutputChange += (sender, args) =>
            {
                PostStatusMessage(JToken.FromObject(
                        new
                        {
                            volume = new
                            {
                                muted = args.BoolValue
                            }
                        })
                );
            };

            _localDevice.VolumeLevelFeedback.OutputChange += (sender, args) =>
            {
                PostStatusMessage(JToken.FromObject(
                        new
                        {
                            volume = new
                            {
                                level = args.IntValue
                            }
                        }
                    )
                );                
            };


        }

        #endregion
    }

    public class VolumeStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("volume", NullValueHandling = NullValueHandling.Ignore)]
        public Volume Volume { get; set; }
    }

    public class Volume
    {
        [JsonProperty("level", NullValueHandling = NullValueHandling.Ignore)]
        public int? Level { get; set; }

        [JsonProperty("muted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Muted { get; set; }

        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
        public string Label { get; set; }

        [JsonProperty("rawValue", NullValueHandling = NullValueHandling.Ignore)]
        public string RawValue { get; set; }

        [JsonProperty("units", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public eVolumeLevelUnits? Units { get; set; }
    }

    public enum eVolumeLevelUnits
    {
        dB,
        Percent,
        Relative,
        Absolute
    }
}