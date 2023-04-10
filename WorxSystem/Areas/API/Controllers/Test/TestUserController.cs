using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestUserController : TestControllerBase
    {
        //
        // GET: /API/TestUser/
        public ActionResult Index()
        {
            return View();
        }
        public JsonResult GetUser(string auth, string user)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/user/" + user)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<APIUser>();
                });
            t.Wait();
            return retVal;
        }
	}
}