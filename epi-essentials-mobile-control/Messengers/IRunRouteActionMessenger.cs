using System;
using PepperDash.Essentials.Core;


namespace PepperDash.Essentials.AppServer.Messengers
{
    public class RunRouteActionMessenger : MessengerBase
    {
        /// <summary>
        /// Device being bridged
        /// </summary>
        public IRunRouteAction RoutingDevice { get; private set; }

        public RunRouteActionMessenger(string key, IRunRouteAction routingDevice, string messagePath)
            : base(key, messagePath)
        {
            if (routingDevice == null)
                throw new ArgumentNullException("routingDevice");

            RoutingDevice = routingDevice;

            var routingSink = RoutingDevice as IRoutingSinkNoSwitching;

            if (routingSink != null)
            {
                routingSink.CurrentSourceChange += routingSink_CurrentSourceChange;
            }
        }

        private void routingSink_CurrentSourceChange(SourceListItem info, ChangeType type)
        {
            SendRoutingFullMessageObject();
        }

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendRoutingFullMessageObject));

            appServerController.AddAction(MessagePath + "/source",
                new Action<SourceSelectMessageContent>(c =>
                {
                    // assume the default source list
                    var sourceListKey = "default";
                    
                    if (!string.IsNullOrEmpty(c.SourceListKey))
                    {
                        // Check for source list in content of message
                        sourceListKey = c.SourceListKey;
                    }

                    RoutingDevice.RunRouteAction(c.SourceListItem,sourceListKey);
                }));

            var sinkDevice = RoutingDevice as IRoutingSinkNoSwitching;
            if (sinkDevice != null)
            {
                sinkDevice.CurrentSourceChange += (o, a) => SendRoutingFullMessageObject();
            }
        }

        /// <summary>
        /// Helper method to update full status of the routing device
        /// </summary>
        private void SendRoutingFullMessageObject()
        {
            var sinkDevice = RoutingDevice as IRoutingSinkNoSwitching;

            if (sinkDevice != null)
            {
                var sourceKey = sinkDevice.CurrentSourceInfoKey;

                if (string.IsNullOrEmpty(sourceKey))
                    sourceKey = "none";

                PostStatusMessage(new
                {
                    selectedSourceKey = sourceKey
                });
            }
        }
    }
}