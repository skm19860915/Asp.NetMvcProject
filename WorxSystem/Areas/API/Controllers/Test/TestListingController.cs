using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Mvc;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestListingController : TestControllerBase
    {
        //
        // GET: /TestListing/

        public ActionResult Index()
        {
            return View();
        }

        public JsonResult CreateListing(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            UserInput input = new UserInput("admin", "admin", "en-US", "en-US");
            //stuff from page 1
            input.Items.Add("CategoryID", CategoryID.ToString());
            input.Items.Add("RegionID", "");
            input.Items.Add("ListingType", "Auction");
            input.Items.Add("Currency", "USD");
            //input.Items.Add("AllCategories", ""); (optional)
            //stuff from page 2
            input.Items.Add("Title", "A Title: " + DateTime.UtcNow.Ticks.ToString());
            input.Items.Add("Subtitle", "");
            input.Items.Add("Description", "A super description!");
            //Custom fields...
            //...
            //Listing Fields
            input.Items.Add("Price", "39.95");
            input.Items.Add("ReservePrice", "");
            input.Items.Add("FixedPrice", "");
            //Media            
            //input.Items.Add("media_guid_a34ef7f6-69ae-4983-a7b2-7bf94b6db742", "a34ef7f6-69ae-4983-a7b2-7bf94b6db742"); //(optional)
            //input.Items.Add("media_ordr_a34ef7f6-69ae-4983-a7b2-7bf94b6db742", "0"); //(optional)
            //Listing Options
            //input.Items.Add("location_1", "false"); (optional)
            //input.Items.Add("decoration_1", "false"); (optional)
            //input.Items.Add("decoration_2", "false"); (optional)
            //input.Items.Add("decoration_3", "false"); (optional)
            //Shipping
            //input.Items.Add("ship_method_159431", "159431"); //(optional)
            //input.Items.Add("ship_amount_159431", "12.00"); //(optional)
            //Duration Fields
            input.Items.Add("Duration", "3");
            input.Items.Add("AutoRelist", "0");            

            HttpContent content = new ObjectContent(typeof(UserInput), input, new JsonMediaTypeFormatter());            

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/", content)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult();
                                  });
            t.Wait();

            return retVal;
        }

        public JsonResult GetListing(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/" + id)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult<APIListing>();

                                  });
            t.Wait();
            return retVal;
        }

        public JsonResult SearchListings(string auth)
        {
            JsonResult retVal = null;            
            
            HttpClient client = GetProperClient(auth);

            //Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/search/0/10" + "?ListingId=" + ListingID)
            //Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/search/0/10" + "?Status=Active")            
            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/search/0/10" + "?ListingId=" + ListingID)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<Page<APIListing>>();
                    //var bob = response.Result.Content.ReadAsAsync<Page<APIListing>>().Result;   
                    //XmlSerializer x = new XmlSerializer(bob.GetType());
                    //StringBuilder sb = new StringBuilder();
                    //using (XmlWriter writer = XmlWriter.Create(sb))
                    //{
                    //    x.Serialize(writer, bob);
                    //}
                    //string xmlDoc = sb.ToString();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult DeleteListing(int id, string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.DeleteAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/" + id)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult();
                                  });
            t.Wait();
            return retVal;
        }

        public JsonResult UpdateListing(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            UserInput input = new UserInput("admin", "admin", "en-US", "en-US");
            //must indicate listingid
            input.Items.Add("ListingID", ListingID.ToString());
            //stuff from page 2
            input.Items.Add("Title", "An UPDATED Title: " + DateTime.UtcNow.Ticks.ToString());
            input.Items.Add("Subtitle", "");
            input.Items.Add("Description", "A super description!");
            //Custom fields...
            //...
            //Listing Fields
            input.Items.Add("Price", "39.95");
            input.Items.Add("ReservePrice", "");
            input.Items.Add("FixedPrice", "");
            //Media            
            //input.Items.Add("media_guid_a34ef7f6-69ae-4983-a7b2-7bf94b6db742", "a34ef7f6-69ae-4983-a7b2-7bf94b6db742"); //(optional)
            //input.Items.Add("media_ordr_a34ef7f6-69ae-4983-a7b2-7bf94b6db742", "0"); //(optional)
            //Listing Options
            //input.Items.Add("location_1", "false"); (optional)
            //input.Items.Add("decoration_1", "false"); (optional)
            //input.Items.Add("decoration_2", "false"); (optional)
            //input.Items.Add("decoration_3", "false"); (optional)
            //Shipping
            //input.Items.Add("ship_method_159431", "159431"); //(optional)
            //input.Items.Add("ship_amount_159431", "12.00"); //(optional)
            //Duration Fields
            input.Items.Add("Duration", "3");
            input.Items.Add("AutoRelist", "0");

            HttpContent content = new ObjectContent(typeof(UserInput), input, new JsonMediaTypeFormatter());

            //create
            Task t = client.PutAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/", content)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult();
                                  });
            t.Wait();

            return retVal;
        }

        public JsonResult GetListingTypes(string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/types")
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<List<ListingType>>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult GetListingTypeProperties(string name, string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Listing/type/" + name)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<List<CustomProperty>>();
                });
            t.Wait();
            return retVal;
        }
    }
}
