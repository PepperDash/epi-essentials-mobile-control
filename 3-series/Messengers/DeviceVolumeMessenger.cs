using System;
using System.Net.Mime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class DeviceVolumeMessenger:MessengerBase
    {
        private IBasicVolumeWithFeedback _localDevice;
        private string _deviceKey;

        public DeviceVolumeMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public DeviceVolumeMessenger(string key, string messagePath, string deviceKey, IBasicVolumeWithFeedback device)
            : base(key, messagePath, device as Device)
        {
            _localDevice = device;
            _deviceKey = deviceKey;
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
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            appServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendStatus());

            appServerController.AddAction(MessagePath + "/level", (id, content) => {
                var volume = content.ToObject<MobileControlSimpleContent<ushort>>();

                _localDevice.SetVolume(volume.Value);
            });

            appServerController.AddAction(MessagePath + "/muteToggle", (id, content) => {
                var state = content.ToObject<MobileControlSimpleContent<bool>>();

                if (!state.Value) return;

                _localDevice.MuteToggle();
            });

            _localDevice.MuteFeedback.OutputChange += (sender, args) =>
            {
                var messageObj = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(
                        new {
                            volume = new
                            {
                                muted = args.BoolValue
                            }
                        })
                };

                appServerController.SendMessageObject(messageObj);
            };

            _localDevice.VolumeLevelFeedback.OutputChange += (sender, args) =>
            {
                var messageObj = new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(
                        new {
                            volume = new
                            {
                                level = args.IntValue
                            }
                        }
                    )
                };

                appServerController.SendMessageObject(messageObj);
            };
        }

        #endregion
    }

    public class VolumeStateMessage:DeviceStateMessageBase
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
    }
}