using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace RainWorx.FrameWorx.MVC.Helpers
{
    public class MVCTransferResult : RedirectResult
    {
        public MVCTransferResult(string url)
            : base(url)
        {
        }

		//updated to accept an HttpContextBase as a parameter, so it doesn't depend on HttpContext.Current. testable!
		public MVCTransferResult(object routeValues, HttpContextBase httpContext)
			: base(GetRouteURL(httpContext, routeValues)) {
		}

		private static string GetRouteURL(HttpContextBase httpContext, object routeValues) {
			UrlHelper url = new UrlHelper(new RequestContext(httpContext, new RouteData()), RouteTable.Routes);
			string target = url.RouteUrl(routeValues);
			return url.RouteUrl(routeValues);
		}

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var httpContext = HttpContext.Current;

            // MVC 3 running on IIS 7+
            if (HttpRuntime.UsingIntegratedPipeline)
            {
                httpContext.Server.TransferRequest(this.Url, true);
            }
            else
            {
                // Pre MVC 3
                httpContext.RewritePath(this.Url, false);

                IHttpHandler httpHandler = new MvcHttpHandler();
                httpHandler.ProcessRequest(httpContext);
            }
        }
    }
}