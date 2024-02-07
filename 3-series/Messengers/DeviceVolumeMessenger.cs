using System;
using System.Net.Mime;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class DeviceVolumeMessenger:MessengerBase
    {
        private IBasicVolumeWithFeedback _device;
        private string _deviceKey;

        public DeviceVolumeMessenger(string key, string messagePath) : base(key, messagePath)
        {
        }

        public DeviceVolumeMessenger(string key, string messagePath, string deviceKey, IBasicVolumeWithFeedback device)
            : this(key, messagePath)
        {
            _device = device;
            _deviceKey = deviceKey;
        }

        private void SendStatus()
        {
            var messageObj = new
            {
                volume = new
                {
                    level = _device.VolumeLevelFeedback.IntValue,
                    muted = _device.MuteFeedback.BoolValue,
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

                _device.SetVolume(volume.Value);
            });

            appServerController.AddAction(MessagePath + "/muteToggle", (id, content) => {
                var state = content.ToObject<MobileControlSimpleContent<bool>>();

                if (!state.Value) return;

                _device.MuteToggle();
            });

            _device.MuteFeedback.OutputChange += (sender, args) =>
            {
                var messageObj = new
                {
                    volume = new
                    {
                        muted = args.BoolValue
                    }
                };

                PostStatusMessage(messageObj);
            };

            _device.VolumeLevelFeedback.OutputChange += (sender, args) =>
            {
                var messageObj = new
                {
                    volume = new
                    {
                        level = args.IntValue
                    }
                };

                PostStatusMessage(messageObj);
            };
        }

        #endregion
    }
}