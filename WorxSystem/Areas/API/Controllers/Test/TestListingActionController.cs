using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestListingActionController : TestControllerBase
    {
        //
        // GET: /TestListingAction/

        public ActionResult Index()
        {
            return View();
        }        

        public JsonResult CreateListingAction(int listingID, decimal bidAmount, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            UserInput input = new UserInput(NonAdminUser, NonAdminUser, "en-US", "en-US");
            input.Items.Add("ListingID", listingID.ToString());
            input.Items.Add("BidAmount", bidAmount.ToString());

            HttpContent content = new ObjectContent(typeof(UserInput), input, new JsonMediaTypeFormatter());

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/ListingAction/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<ListingActionPostResponse>();
                });
            t.Wait();

            return retVal;
        }

    }
}
