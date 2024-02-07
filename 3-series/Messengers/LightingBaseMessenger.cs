using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core.Lighting;

using Newtonsoft.Json;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class ILightingScenesMessenger : MessengerBase
    {
        protected ILightingScenes Device { get; private set; }

        public ILightingScenesMessenger(string key, ILightingScenes device, string messagePath)
            : base(key, messagePath, device as Device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            Device = device;
            Device.LightingSceneChange += new EventHandler<LightingSceneChangeEventArgs>(LightingDevice_LightingSceneChange);

           
        }

        void LightingDevice_LightingSceneChange(object sender, LightingSceneChangeEventArgs e)
        {
            var state = new LightingBaseStateMessage();

            state.CurrentLightingScene = e.CurrentLightingScene;

            PostStatusMessage(state);
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(string.Format("{0}/fullStatus", MessagePath), (id, content) => SendFullStatus());

            appServerController.AddAction(string.Format("{0}/selectScene", MessagePath), (id, content) => {
                var s = content.ToObject<LightingScene>();
                Device.SelectScene(s);
            });
        }


        private void SendFullStatus()
        {
            Debug.Console(2, "LightingBaseMessenger GetFullStatus");

            var state = new LightingBaseStateMessage();

            state.Scenes = Device.LightingScenes;
            state.CurrentLightingScene = Device.CurrentLightingScene;

            PostStatusMessage(state);
        }
    }

    public class LightingBaseStateMessage : DeviceStateMessageBase
    {
        [JsonProperty("scenes", NullValueHandling = NullValueHandling.Ignore)]
        public List<LightingScene> Scenes { get; set; }

        [JsonProperty("currentLightingScene", NullValueHandling = NullValueHandling.Ignore)]
        public LightingScene CurrentLightingScene { get; set; }
    }
}