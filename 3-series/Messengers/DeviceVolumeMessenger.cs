using System;
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

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendStatus));
            appServerController.AddAction(MessagePath + "/level", new Action<ushort>(_device.SetVolume));

            appServerController.AddAction(MessagePath + "/muteToggle", new Action<bool>(
                (b) => {
                    if(b){
                        _device.MuteToggle();
                    }}));

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