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
    public class ISelectableItemsMessenger<TKey> : MessengerBase
    {
        private ISelectableItems<TKey> itemDevice;
        public ISelectableItemsMessenger(string key, string messagePath, ISelectableItems<TKey> device) : base(key, messagePath, device as Device)
        {
            itemDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, context) =>
            {
                PostStatusMessage(JToken.FromObject(itemDevice));
            });

            itemDevice.ItemsUpdated += (sender, args) =>
            {
                PostStatusMessage(JToken.FromObject(new
                {
                    currentItem = itemDevice.Items.FirstOrDefault(x => x.Value.IsSelected).Key.ToString(),
                }));
            };

            foreach (var input in itemDevice.Items)
            {
                var key = input.Key;
                var localItem = input.Value;

                AddAction($"/{localItem.Key}", (id, content) =>
                {
                    localItem.Select();
                });

                localItem.ItemUpdated += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(itemDevice));
                };
            }
        }
    }

}
