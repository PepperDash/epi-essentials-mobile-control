using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Core.Web.RequestHandlers;
using PepperDash.Essentials.Core.Web;
using System;
using System.Threading.Tasks;

namespace PepperDash.Essentials.WebApiHandlers
{
    public class MobileAuthRequestHandler : WebApiBaseRequestAsyncHandler
    {
        private readonly MobileControlSystemController mcController;

        public MobileAuthRequestHandler(MobileControlSystemController controller) : base(true)
        {
            mcController = controller;
        }        

        protected override async Task HandlePost(HttpCwsContext context)
        {
            var requestBody = EssentialsWebApiHelpers.GetRequestBody(context.Request);

            var grantCode = JsonConvert.DeserializeObject<AuthorizationRequest>(requestBody);

            if (string.IsNullOrEmpty(grantCode?.GrantCode))
            {
                context.Response.StatusCode = 400;
                context.Response.StatusDescription = "Missing grant code";
                context.Response.End();
                return;
            }

            var response = await mcController.ApiService.SendAuthorizationRequest(mcController.Host, grantCode.GrantCode, mcController.SystemUuid);

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Response Received: {@response}",null, response);

            if (response.Authorized)
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Information, "System authorized. Registering with server", null, null);
                mcController.RegisterSystemToServer();
            }

            try
            {
                context.Response.StatusCode = 200;
                var responseBody = JsonConvert.SerializeObject(response, Formatting.None);
                context.Response.ContentType = "application/json";
                context.Response.Headers.Add("Content-Type", "application/json");
                context.Response.Write(responseBody, false);
                context.Response.End();
            }
            catch (Exception ex)
            {
                Debug.LogMessage(ex, "Error getting authorization: {Exception}", null, ex);
            }
        }
    }
}
