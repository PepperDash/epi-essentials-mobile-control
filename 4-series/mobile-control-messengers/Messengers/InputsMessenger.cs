using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class InputsMessenger : MessengerBase
    {
        private IHasInputs inputs;
        public InputsMessenger(string key, string messagePath, IHasInputs device) : base(key, messagePath, device as Device)
        {
            inputs = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, context) =>
            {
                var message = new InputsStateMesage
                {
                    CurrentInputKey = inputs.Inputs.FirstOrDefault(x => x.Value.IsSelected).Key,
                    Inputs = inputs.Inputs.ToDictionary(kv => kv.Key, kv => new Input { IsSelected = kv.Value.IsSelected })
                };
            });

            foreach (var input in inputs.Inputs)
            {
                var key = input.Key;
                var localInput = input.Value;

                AddAction($"/{localInput.Key}", (id, content) =>
                {
                    localInput.Select();
                });

                localInput.InputUpdated += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(new
                    {
                        currentInputKey = localInput.IsSelected ? localInput.Key : null,
                        inputs = new Dictionary<string, Input>
                        {
                            {localInput.Key, new Input{IsSelected = localInput.IsSelected } }
                        }
                    }));
                };
            }
        }
    }

    public class InputsStateMesage:DeviceStateMessageBase
    {
        [JsonProperty("currentInputKey", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentInputKey {  get; set; }

        [JsonProperty("inputs")]
        public Dictionary<string, Input> Inputs { get; set; }
    }

    public class Input
    {
        [JsonProperty("isSelected", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSelected { get; set; }
    }
}
