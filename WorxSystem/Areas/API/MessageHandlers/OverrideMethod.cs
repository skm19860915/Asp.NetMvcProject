using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class OverrideMethod : DelegatingHandler
    {
        private const string OverrideHeader = "X-HTTP-Method-Override";

        public OverrideMethod()
        {
        }

        public OverrideMethod(HttpMessageHandler inner)
            : base(inner)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Headers.Contains(OverrideHeader))
            {
                var realverb = request.Headers.GetValues(OverrideHeader).FirstOrDefault();
                if (realverb != null)
                {
                    request.Method = new HttpMethod(realverb);
                }
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}