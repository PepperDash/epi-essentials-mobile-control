using System;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;


namespace PepperDash.Essentials.AppServer.Messengers
{

    public class SIMPLRouteMessenger : MessengerBase
    {
        private readonly BasicTriList _eisc;

        private readonly uint _joinStart;

        public class StringJoin
        {
            /// <summary>
            /// 1
            /// </summary>
            public const uint CurrentSource = 1;
        }

        public SIMPLRouteMessenger(string key, BasicTriList eisc, string messagePath, uint joinStart)
            : base(key, messagePath)
        {
            _eisc = eisc;
            _joinStart = joinStart - 1;

            _eisc.SetStringSigAction(_joinStart + StringJoin.CurrentSource, SendRoutingFullMessageObject);
        }

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus",
                new Action(() => SendRoutingFullMessageObject(_eisc.GetString(_joinStart + StringJoin.CurrentSource))));

            appServerController.AddAction(MessagePath + "/source",
                new Action<SourceSelectMessageContent>(
                    c => _eisc.SetString(_joinStart + StringJoin.CurrentSource, c.SourceListItem)));
        }

        public void CustomUnregsiterWithAppServer(IMobileControl3 appServerController)
        {
            appServerController.RemoveAction(MessagePath + "/fullStatus");
            appServerController.RemoveAction(MessagePath + "/source");

            _eisc.SetStringSigAction(_joinStart + StringJoin.CurrentSource, null);
        }

        /// <summary>
        /// Helper method to update full status of the routing device
        /// </summary>
        private void SendRoutingFullMessageObject(string sourceKey)
        {
            if (string.IsNullOrEmpty(sourceKey))
                sourceKey = "none";

            PostStatusMessage(new
            {
                selectedSourceKey = sourceKey
            });
        }
    }
}