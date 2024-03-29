using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.IO;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;
using System.Collections.Generic;
using System.Linq;

namespace PepperDash.Essentials.AppServer.Messengers
{
    /// <summary>
    /// Messenger for devices that implment IMatrixRouting
    /// NOTE:  MUST BE INSTANTIATED BY THE IMatrixRouting DEVICE.  CANNOT BE CREATED AUTOMATICALLY. Requires types to be specified.
    /// </summary>
    /// <typeparam name="TInput">Type that implments IRoutingInputSlot</typeparam>
    /// <typeparam name="TOutput">Type that implments IRoutingOutputSlot</typeparam>
    public class IMatrixRouteMessenger<TInput, TOutput> : MessengerBase
    {
        private IMatrixRouting<IRoutingInputSlot, IRoutingOutputSlot> matrixDevice;
        public IMatrixRouteMessenger(string key, string messagePath, IMatrixRouting<IRoutingInputSlot, IRoutingOutputSlot> device) : base(key, messagePath, device as Device)
        {
            matrixDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) =>
            {

                try
                {
                    Debug.LogMessage(Serilog.Events.LogEventLevel.Verbose, this, "InputCount: {inputCount}, OutputCount: {outputCount}", matrixDevice.InputSlots.Count, matrixDevice.OutputSlots.Count);
                    PostStatusMessage(new MatrixStateMessage
                    {
                        Outputs = matrixDevice.OutputSlots,
                        Inputs = matrixDevice.InputSlots,
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogMessage(e, "Exception Getting full status: {@exception}", this, e);
                }
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
        public Dictionary<string, IRoutingOutputSlot> Outputs;

        [JsonProperty("inputs")]
        public Dictionary<string, IRoutingInputSlot> Inputs;
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
