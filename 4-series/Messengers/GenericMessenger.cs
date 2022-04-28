﻿using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
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