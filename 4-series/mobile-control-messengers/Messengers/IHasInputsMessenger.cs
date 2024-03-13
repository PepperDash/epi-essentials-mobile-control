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
    public class IHasInputsMessenger : MessengerBase
    {
        private IHasInputs inputs;
        public IHasInputsMessenger(string key, string messagePath, IHasInputs device) : base(key, messagePath, device as Device)
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
                    Inputs = inputs.Inputs.ToDictionary(kv => kv.Key, kv => 
                        new Input { IsSelected = kv.Value.IsSelected, Key = kv.Value.Key, Name = kv.Value.Name })
                };

                PostStatusMessage(message);
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
                    var existingInput = inputs.Inputs.FirstOrDefault((i) => i.Key == localInput.Key);


                    PostStatusMessage(JToken.FromObject(new InputsStateMesage()
                    {
                        CurrentInputKey = localInput.IsSelected ? localInput.Key : null,
                        Inputs = inputs.Inputs.ToDictionary(kv => kv.Key, kv =>
                            new Input { IsSelected = kv.Key == localInput.Key ? localInput.IsSelected : kv.Value.IsSelected, Key = kv.Value.Key, Name = kv.Value.Name })
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

    public class Input: IKeyName
    {
        [JsonProperty("isSelected", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSelected { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
