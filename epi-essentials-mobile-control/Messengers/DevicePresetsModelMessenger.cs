using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Presets;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class DevicePresetsModelMessenger:MessengerBase
    {
        private readonly ITvPresetsProvider _presetsDevice;

        public DevicePresetsModelMessenger(string key, string messagePath) : base(key, messagePath)
        {

        }

        public DevicePresetsModelMessenger(string key, string messagePath, ITvPresetsProvider presetsDevice)
            : this(key, messagePath)
        {
            _presetsDevice = presetsDevice;
        }

        private void TvPresetsOnPresetChanged(ISetTopBoxNumericKeypad device, string channel)
        {
            throw new NotImplementedException();
        }

        private void SendPresets()
        {
            PostStatusMessage(new
            {
                favorites = _presetsDevice.TvPresets.PresetsList
            });
        }

        private void RecallPreset(ISetTopBoxNumericKeypad device, string channel)
        {
            _presetsDevice.TvPresets.Dial(channel, device);
        }

        private void SavePresets(List<PresetChannel> presets )
        {

            _presetsDevice.TvPresets.UpdatePresets(presets);
        }
       

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendPresets));
            appServerController.AddAction(MessagePath + "/recall", new Action<PresetChannelMessage>((p) =>
            {
                var dev = DeviceManager.GetDeviceForKey(p.DeviceKey) as ISetTopBoxNumericKeypad;

                if (dev == null)
                {
                    Debug.Console(1, "Unable to find device with key {0}", p.DeviceKey);
                    return;
                }

                RecallPreset(dev, p.Preset.Channel);
            }));
            appServerController.AddAction(MessagePath + "/save", new Action<List<PresetChannel>>(SavePresets));
        }

        #endregion
    }

    public class PresetChannelMessage
    {
        [JsonProperty("preset")]
        public PresetChannel Preset;

        [JsonProperty("deviceKey")]
        public string DeviceKey;
    }
}