using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PepperDash.Essentials
{
    public class AuthorizationResponse
    {
        [JsonProperty("authorized")]
        public bool Authorized { get; set; }

        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
        public string Reason { get; set; } = null;
    }

    public class AuthorizationRequest
    {
        [JsonProperty("grantCode")]
        public string GrantCode { get; set; }
    }
}
