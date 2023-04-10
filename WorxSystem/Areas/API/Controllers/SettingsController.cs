using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.OData.Query;
using Microsoft.Ajax.Utilities;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    [RoutePrefix("api/settings")]
    public class SettingsController : AuctionWorxAPIController
    {
        /// <summary>
        /// Retrieves all Site Settings
        /// </summary>
        /// <returns>A list of CustomProperties for site settings</returns>
        [Route("")]
        [ResponseType(typeof(List<CustomProperty>))]
        public HttpResponseMessage GetSettings()
        {
            List<CustomProperty> retVal = new List<CustomProperty>();
            SiteClient.Properties.CopyItemsTo(retVal);
            PruneSettingsVisbility(ref retVal);
            return Request.CreateResponse(HttpStatusCode.OK, retVal);
        }

        private CustomFieldAccess GetCustomFieldVisbilityForSettings()
        {
            string userName = Request.GetUserName();

            if (string.IsNullOrEmpty(userName)) return CustomFieldAccess.Anonymous;

            if (userName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            User user = UserClient.GetUserByUserName(User.Identity.Name, userName);
            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;                                    

            return CustomFieldAccess.Authenticated;            
        }

        private void PruneSettingsVisbility(ref List<CustomProperty> settings)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForSettings();
            settings = settings.Where(
                p => (int)p.Field.Visibility >= (int)access).ToList();
        }
    }
}