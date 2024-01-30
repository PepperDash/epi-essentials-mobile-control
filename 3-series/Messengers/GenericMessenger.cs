using PepperDash.Essentials.Core;
using System;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class GenericMessenger : MessengerBase
    {
        private EssentialsDevice _device;

        public GenericMessenger(string key, EssentialsDevice device, string messagePath):base(key, messagePath, device)
        {
            if(device == null)
            {
                throw new ArgumentNullException("device");
            }

            _device = device;
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            base.CustomRegisterWithAppServer(appServerController);

            appServerController.AddAction(string.Format("{0}/fullStatus", MessagePath), new Action(SendFullStatus));
        }

        private void SendFullStatus()
        {
            var state = new DeviceStateMessageBase();

            PostStatusMessage(state);
        }
    }
}
