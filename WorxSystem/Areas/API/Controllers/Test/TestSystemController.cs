using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestSystemController : TestControllerBase
    {
        //
        // GET: /API/TestAccount/
        public ActionResult Index()
        {
            return View();
        }

        public JsonResult Login(string auth, string user)
        {
            JsonResult retVal = null;
            var client = GetProperClient(auth);

            AccountLoginRequest request = new AccountLoginRequest();

            switch (user)
            {
                case "valid":
                    request.username = "admin";
                    request.password = "demo";
                    break;
                case "notexist":
                    request.username = "sdfsdfsf";
                    request.password = "df845k223";
                    break;
                case "badpass":
                    request.username = "admin";
                    request.password = "df845k223";
                    break;
                case "inactive":
                    request.username = "inactive";
                    request.password = "abc123";
                    break;
                case "notverified":
                    request.username = "notverified";
                    request.password = "abc123";
                    break;
                case "notapproved":
                    request.username = "notapproved";
                    request.password = "abc123";
                    break;
                case "lockedout":
                    request.username = "lockedout";
                    request.password = "abc123";
                    break;
            }            

            HttpContent content = new ObjectContent(typeof(AccountLoginRequest), request, new JsonMediaTypeFormatter());

            var task = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Login/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            task.Wait();
            return retVal;
        }

        public JsonResult Version(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/version")
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult Build(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/build")
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }
	}
}