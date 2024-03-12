using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;

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

                matrixDevice.Route(request.inputKey, request.outputKey, request.RouteType);
            });

            foreach(var output in matrixDevice.OutputSlots)
            {
                var key = output.Key;
                var outputSlot = output.Value;

                outputSlot.OutputSlotChanged += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(new
                    {
                        outputs = new Dictionary<string, IRoutingOutputSlot> {
                        {key, outputSlot} }
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
                        inputs = new Dictionary<string, IRoutingInputSlot>
                        {
                            {key, inputSlot }
                        }
                    }));
                };
            }
        }
    }

    public class  MatrixStateMessage:DeviceStateMessageBase
    {
        [JsonProperty("outputs")]
        public Dictionary<string, IRoutingOutputSlot> Outputs;

        [JsonProperty("inputs")]
        public Dictionary<string, IRoutingInputSlot> Inputs;
    }

    public class MatrixRouteRequest
    {
        [JsonProperty("outputKey")]
        public string outputKey { get; set; }

        [JsonProperty("inputKey")]
        public string inputKey { get; set; }

        [JsonProperty("routeType")]
        public eRoutingSignalType RouteType { get; set; }
    }
}
