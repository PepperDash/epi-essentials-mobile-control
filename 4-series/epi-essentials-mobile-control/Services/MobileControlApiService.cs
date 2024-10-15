using PepperDash.Core;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PepperDash.Essentials.Services
{

    public class MobileControlApiService
    {
        private readonly HttpClient _client;

        public MobileControlApiService(string apiUrl)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = (req, cert, certChain, errors) => true
            };

            _client = new HttpClient(handler);
        }

        public async Task<AuthorizationResponse> SendAuthorizationRequest(string apiUrl, string grantCode, string systemUuid)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/system/{systemUuid}/authorize?grantCode={grantCode}");

            Debug.Console(1, $"Sending authorization request to {request.RequestUri}");

            var response = await _client.SendAsync(request);

            var authResponse = new AuthorizationResponse
            {
                Authorized = response.StatusCode == System.Net.HttpStatusCode.OK
            };

            if (authResponse.Authorized)
            {
                return authResponse;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Moved)
            {
                var location = response.Headers.Location;

                authResponse.Reason = $"ERROR: Mobile Control API has moved. Please adjust configuration to \"{location}\"";

                return authResponse;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            switch (responseString)
            {
                case "codeNotFound":
                    authResponse.Reason = $"Authorization failed. Code not found for system UUID {systemUuid}";
                    break;
                case "uuidNotFound":
                    authResponse.Reason = $"Authorization failed. System UUID {systemUuid} not found. Check Essentials configuration.";
                    break;
                default:
                    authResponse.Reason = $"Authorization failed. Response {response.StatusCode}: {responseString}";
                    break;
            }

            return authResponse;
        }
    }
}
