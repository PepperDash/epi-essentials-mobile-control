using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core.Lighting;

using Newtonsoft.Json;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class LightingBaseMessenger : MessengerBase
    {
        protected LightingBase Device { get; private set; }

        public LightingBaseMessenger(string key, LightingBase device, string messagePath)
            : base(key, messagePath, device)
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

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(string.Format("{0}/fullStatus", MessagePath), new Action(SendFullStatus));

            appServerController.AddAction(string.Format("{0}/selectScene", MessagePath), new Action<LightingScene>((s) => Device.SelectScene(s)));
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