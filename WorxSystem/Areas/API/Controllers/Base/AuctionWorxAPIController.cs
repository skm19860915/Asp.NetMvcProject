using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using RainWorx.FrameWorx.Clients;

using System.Web;
using Microsoft.AspNet.Identity.Owin;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base
{    
    public class RequireHttpsAttribute : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            if (ConfigurationManager.AppSettings["WebAPIAuthScheme"] == "RWX_SECURE")
            {
                base.OnAuthorization(actionContext);
                return;
            }

            if (actionContext.Request.RequestUri.Scheme != Uri.UriSchemeHttps)
            {                
                actionContext.Response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Forbidden,
                    "HTTPS Required");                
            }
            else
            {
                base.OnAuthorization(actionContext);
            }
        }
    }

    public class RequireAuthenticationAttribute : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            if (SkipAuthentication(actionContext) || IsAuthenticated(actionContext))
            {
                base.OnAuthorization(actionContext);
            }
            else
            {
                HttpResponseMessage response = actionContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized,
                    "Unauthorized Access Attempt");
                switch (ConfigurationManager.AppSettings["WebAPIAuthScheme"])
                {
                    case "RWX_BASIC":
                        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("RWX_BASIC"));
                        break;
                    case "RWX_SECURE":
                        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("RWX_SECURE"));
                        break;
                }
                actionContext.Response = response;
            }
        }

        protected virtual bool IsAuthenticated(HttpActionContext actionContext)
        {
            return actionContext.Request.Headers.Authorization != null;
        }

        private static bool SkipAuthentication(HttpActionContext actionContext)
        {
            if (!Enumerable.Any<AllowAnonymousAttribute>((IEnumerable<AllowAnonymousAttribute>)actionContext.ActionDescriptor.GetCustomAttributes<AllowAnonymousAttribute>()))
                return Enumerable.Any<AllowAnonymousAttribute>((IEnumerable<AllowAnonymousAttribute>)actionContext.ControllerContext.ControllerDescriptor.GetCustomAttributes<AllowAnonymousAttribute>());
            else
                return true;
        }
    }    

    //Uncomment the following line, FOR TESTING ONLY, to disable SSL for the entire API.  Also, order matters here: perform the SSL check first, it should be faster than the API Enabled lookup.
    [RequireHttps]        
    [RequireAuthentication]
    public abstract class AuctionWorxAPIController : ApiController
    {
        //protected const int Version = 100;
        protected const int Version = 110; //Added SearshListings

        private AuctionWorxUserManager _userManager;
        public AuctionWorxUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<AuctionWorxUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

    }

}
