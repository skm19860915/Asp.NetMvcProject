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
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestUseCaseController : TestControllerBase
    {
        //
        // GET: /TestUseCase/

        public ActionResult Index()
        {
            return View();
        }        

        public JsonResult CreateCategory(string name)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient("admin");

            Category newCategory = new Category();
            newCategory.Name = name;
            newCategory.ParentCategoryID = 9;
            newCategory.Type = "Item";
            newCategory.MVCAction = string.Empty;

            HttpContent content = new ObjectContent(typeof(Category), newCategory, new JsonMediaTypeFormatter());

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();

            return retVal;
        }

        public JsonResult CreateCustomField(string name)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient("admin");

            CustomField newCustomField = new CustomField();
            //required
            newCustomField.Name = name;
            newCustomField.Group = "Item";
            newCustomField.Type = CustomFieldType.String;
            newCustomField.DefaultValue = string.Empty;

            HttpContent content = new ObjectContent(typeof(CustomField), newCustomField, new JsonMediaTypeFormatter());

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();

            return retVal;
        }

        public JsonResult AssignCustomFieldToCategory(int customFieldID, int categoryID)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient("admin");

            CustomFieldAssignRequest newCategory = new CustomFieldAssignRequest();
            newCategory.categoryID = categoryID;
            newCategory.customFieldID = customFieldID;

            HttpContent content = new ObjectContent(typeof(CustomFieldAssignRequest), newCategory, new JsonMediaTypeFormatter());

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/Assign/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();

            return retVal;
        }

        public JsonResult CreateListing(int categoryID, string title, string subtitle, string description, decimal price, int duration, string customFieldName, string customFieldValue)
        {
            JsonResult retVal = null;
            var obj = Request;
            HttpClient client = GetProperClient("admin");

            UserInput input = new UserInput("admin", "admin", "en-US", "en-US");
            //stuff from page 1
            input.Items.Add("CategoryID", categoryID.ToString());
            input.Items.Add("RegionID", "");
            input.Items.Add("ListingType", "Auction");
            input.Items.Add("Currency", "USD");
            //input.Items.Add("AllCategories", ""); (optional)
            //stuff from page 2
            input.Items.Add("Title", title);
            input.Items.Add("Subtitle", subtitle);
            input.Items.Add("Description", description);
            //Custom fields...
            string[] cfNames = customFieldName.Split(',');
            string[] cfValues = customFieldValue.Split(',');
            for (int i = 0; i < cfNames.Length;i++)
            {
                input.Items.Add(cfNames[i], cfValues[i]);
            }
            //Listing Fields
            input.Items.Add("Price", price.ToString());
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
            input.Items.Add("Duration", duration.ToString());
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

    }
}
