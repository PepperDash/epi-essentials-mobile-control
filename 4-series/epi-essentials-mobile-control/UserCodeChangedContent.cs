using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials.AppServer
{
    public class UserCodeChangedContent
    {
        [JsonProperty("userCode")]
        public string UserCode { get; set; }

        [JsonProperty("qrChecksum", NullValueHandling = NullValueHandling.Include)]
        public string QrChecksum { get; set; }
    }
}
