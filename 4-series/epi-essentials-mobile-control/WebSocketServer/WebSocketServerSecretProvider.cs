using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PepperDash.Essentials.Core;

using Newtonsoft.Json;

namespace PepperDash.Essentials
{
    class WebSocketServerSecretProvider : CrestronLocalSecretsProvider
    {
        public WebSocketServerSecretProvider(string key)
            :base(key)
        {
            Key = key;
        }
    }

    public class WebSocketServerSecret : ISecret
    {
        public ISecretProvider Provider { get; private set; }

        public string Key { get; private set; }

        public object Value { get; private set; }

        public WebSocketServerSecret(string key, object value, ISecretProvider provider)
        {
            Key = key;
            Value = JsonConvert.SerializeObject(value);
            Provider = provider;
        }

        public ServerTokenSecrets DeserializeSecret()
        {
            return JsonConvert.DeserializeObject<ServerTokenSecrets>(Value.ToString());
        }
    }


}
