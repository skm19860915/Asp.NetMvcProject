using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using RainWorx.FrameWorx.Clients;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class APIEnabled : DelegatingHandler
    {
        public APIEnabled() {}

        public APIEnabled(HttpMessageHandler inner)
            : base(inner) {}

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
        {

            bool isEnabled = SiteClient.BoolSetting("EnableWebAPI");

            if (isEnabled)
            {
                return base.SendAsync(request, cancellationToken);
            }
            else
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized,
                    "Unauthorized Access Attempt");
                tsc.SetResult(response);
                return tsc.Task;
            }
        }
    }
}