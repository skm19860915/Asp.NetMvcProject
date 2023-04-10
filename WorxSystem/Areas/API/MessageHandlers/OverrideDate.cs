using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class OverrideDate : DelegatingHandler
    {
        private const string OverrideHeader = "X-HTTP-Date-Override";

        public OverrideDate()
        {
        }

        public OverrideDate(HttpMessageHandler inner)
            : base(inner)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Headers.Contains(OverrideHeader))
            {
                DateTime overrideDateTime;
                if (DateTime.TryParse(request.Headers.GetValues(OverrideHeader).FirstOrDefault() ?? string.Empty, out overrideDateTime))
                {
                    request.Headers.Date = overrideDateTime;
                }                
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}