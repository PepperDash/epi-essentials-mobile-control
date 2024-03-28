using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;
using System.Collections.Generic;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class IMatrixRouteMessenger : MessengerBase
    {
        private IMatrixRouting matrixDevice;
        public IMatrixRouteMessenger(string key, string messagePath, IMatrixRouting device) : base(key, messagePath, device as Device)
        {
            matrixDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) => {
                PostStatusMessage(new MatrixStateMessage
                {
                    Outputs = matrixDevice.OutputSlots,
                    Inputs = matrixDevice.InputSlots,
                });
            });

            AddAction("/route", (id, content) =>
            {
                var request = content.ToObject<MatrixRouteRequest>();

                matrixDevice.Route(request.InputKey, request.OutputKey, request.RouteType);
            });

            foreach(var output in matrixDevice.OutputSlots)
            {
                var key = output.Key;
                var outputSlot = output.Value;

                outputSlot.OutputSlotChanged += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(new
                    {
                        outputs = matrixDevice.OutputSlots

                    }));
                };
            }

            foreach(var input in matrixDevice.InputSlots)
            {
                var key = input.Key;
                var inputSlot = input.Value;

                inputSlot.VideoSyncChanged += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(new
                    {
                        inputs = matrixDevice.InputSlots
                    }));
                };
            }
        }
    }

    public class  MatrixStateMessage:DeviceStateMessageBase
    {
        [JsonProperty("outputs")]
        public Dictionary<string, RoutingOutputSlotBase> Outputs;

        [JsonProperty("inputs")]
        public Dictionary<string, RoutingInputSlotBase> Inputs;
    }

    public class MatrixRouteRequest
    {
        [JsonProperty("outputKey")]
        public string OutputKey { get; set; }

        [JsonProperty("inputKey")]
        public string InputKey { get; set; }

        [JsonProperty("routeType")]
        public eRoutingSignalType RouteType { get; set; }
    }
}
