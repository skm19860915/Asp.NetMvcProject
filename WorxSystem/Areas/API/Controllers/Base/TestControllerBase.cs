using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Mvc;
using System.Net.Http;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base
{
    public class AuthorizeAPIDocsAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            var isAuthorized = base.AuthorizeCore(httpContext);
            if (!isAuthorized)
            {
                return false;
            }

            if (!SiteClient.BoolSetting("EnableWebAPI"))
            {
                return false;
            }

            string userName = httpContext.User.Identity.Name;
            User actingUser = UserClient.GetUserByUserName(userName, userName);

            if (actingUser.WebAPIEnabled)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    
    [AuthorizeAPIDocs]
    public class TestControllerBase : Controller
    {
        protected const int EnumerationCustomFieldID = 322584;
        public const int CategoryID = 195118;
        protected const string ImageRoot = @"H:\Desktop\images";
        protected const string Image1FileName = "bill.jpg";
        protected const string Image2FileName = "emergency.jpg";
        protected const string MediaURI = @"http://rainworx.com/Content/img/rainworx-logo.png";
        protected const string MediaString = "<h1>This is a banner!</h1>";
        public static string MediaGuid = "740DECC9-A613-40A4-9CE1-F650C2286914";
        public static int MediaID = 334364;
        public static int ListingID = 332114;
        protected const string NonAdminUser = "AuctionBob";

        protected HttpClient GetProperClient(string auth)
        {
            HttpClient client = null;
            if (auth == null) auth = "ugly";

            switch (ConfigurationManager.AppSettings["WebAPIAuthScheme"])
            {
                case "RWX_BASIC":
                    switch (auth.ToLower())
                    {
                        case "admin":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new BasicSignRequest("admin", "demo"));
                            return client;
                        case "nonadmin":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new BasicSignRequest(NonAdminUser, "demo"));
                            return client;
                        case "badkey":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new BasicSignRequest(NonAdminUser, "sdfsdsdf"));
                            return client;
                        case "nouser":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new BasicSignRequest("nancy828", "asdadasd"));
                            return client;
                        default:
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate());
                            return client;
                    }
                    //break;
                case "RWX_SECURE":
                    switch (auth.ToLower())
                    {
                        case "admin":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new SecureSignRequest("admin", "1DyAjvxe+F1t1WWgkuGPy6nX0UFTKhRMe/nwlwcBKDw="));
                            return client;
                        case "nonadmin":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new SecureSignRequest(NonAdminUser, "qfEUa60YiGwWw0IdTNTJJxuBWoH2G0EMMcLCwecSgPs="));
                            return client;
                        case "badkey":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new SecureSignRequest(NonAdminUser, "GGMMK8qAUe8gUQK2fjx/ipy+Vw4ndc4ksk+ZNz+FBSo="));
                            return client;
                        case "nouser":
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate(),
                                                                 new SecureSignRequest("nancy828", "KJyvP2MoQSXev9doZKMwp2pQuTzCusDKnVnHU+fRvYg="));
                            return client;
                        default:
                            client = HttpClientFactory.Create(new HttpClientHandler(),
                                                                 new OverrideDate());
                            return client;
                    }
                    //break;
            }
            return null;
        }
	}
}