using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;
using PepperDash.Core.Web.RequestHandlers;
using System.Collections.Generic;
using System.Linq;

namespace PepperDash.Essentials.WebApiHandlers
{
    public class ActionPathsHandler : WebApiBaseRequestHandler
    {
        private readonly MobileControlSystemController mcController;
        public ActionPathsHandler(MobileControlSystemController controller) : base(true)
        {
            mcController = controller;
        }

        protected override void HandleGet(HttpCwsContext context)
        {
            var response = JsonConvert.SerializeObject(new ActionPathsResponse(mcController));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Add("Content-Type", "application/json");
            context.Response.Write(response, false);
            context.Response.End();
        }
    }

    public class ActionPathsResponse
    {
        [JsonIgnore]
        private readonly MobileControlSystemController mcController;

        [JsonProperty("actionPaths")]
        public List<string> ActionPaths => mcController.ActionDictionary.Keys.ToList();

        public ActionPathsResponse(MobileControlSystemController mcController)
        {
            this.mcController = mcController;
        }
    }
}
