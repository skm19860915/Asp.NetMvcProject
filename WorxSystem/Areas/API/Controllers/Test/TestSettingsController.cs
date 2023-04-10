using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestSettingsController : TestControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }

        public JsonResult GetSiteSettings(string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/settings")
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<List<CustomProperty>>();
                });
            t.Wait();
            return retVal;
        }
    }
}