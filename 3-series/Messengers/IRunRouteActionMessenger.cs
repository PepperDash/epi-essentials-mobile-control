using System;
using PepperDash.Essentials.Core;
using PepperDash.Core;


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

            var routingSink = RoutingDevice as IRoutingSink;

            if (routingSink != null)
            {
                routingSink.CurrentSourceChange += routingSink_CurrentSourceChange;
            }
        }

        private void routingSink_CurrentSourceChange(SourceListItem info, ChangeType type)
        {
            SendRoutingFullMessageObject();
        }

        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
        {
            appServerController.AddAction(MessagePath + "/fullStatus", new Action(SendRoutingFullMessageObject));

            appServerController.AddAction(MessagePath + "/source",
                new Action<SourceSelectMessageContent>(c =>
                {
                    // assume no sourceListKey
                    var sourceListKey = string.Empty;
                    
                    if (!string.IsNullOrEmpty(c.SourceListKey))
                    {
                        // Check for source list in content of message
                        Debug.Console(1, this, "sourceListKey found in message");
                        sourceListKey = c.SourceListKey;
                    }

                    RoutingDevice.RunRouteAction(c.SourceListItem,sourceListKey);
                }));

            var sinkDevice = RoutingDevice as IRoutingSink;
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
            var sinkDevice = RoutingDevice as IRoutingSink;

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