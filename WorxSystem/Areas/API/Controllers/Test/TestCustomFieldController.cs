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
    public class TestCustomFieldController : TestControllerBase
    {
        //
        // GET: /TestCustomField/

        public ActionResult Index()
        {
            return View();
        }

        public JsonResult GetCustomField(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<CustomField>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult GetCustomFieldByCategoryID(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/ByCategory/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<List<CustomField>>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult CreateCustomField(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            CustomField newCustomField = new CustomField();
            //required
            newCustomField.Name = DateTime.UtcNow.Ticks.ToString();
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

        public JsonResult CreateEnumeration(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            CustomFieldPostEnumerationRequest newEnumeration = new CustomFieldPostEnumerationRequest();            
            //required
            string temp = DateTime.UtcNow.Ticks.ToString();
            newEnumeration.customFieldID = EnumerationCustomFieldID;
            newEnumeration.enabled = true;
            newEnumeration.name = "namefor" + temp;
            newEnumeration.value = "valfor" + temp;

            HttpContent content = new ObjectContent(typeof(CustomFieldPostEnumerationRequest), newEnumeration, new JsonMediaTypeFormatter());

            //create
            Task t = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/Enumeration/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();

            return retVal;
        }

        public JsonResult DeleteEnumeration(int id, string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.DeleteAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/Enumeration/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult DeleteCustomField(int id, string auth)
        {
            JsonResult retVal = null;

            HttpClient client = GetProperClient(auth);

            Task t = client.DeleteAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult UpdateCustomField(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            CustomField newCustomField = new CustomField();
            //required
            newCustomField.ID = EnumerationCustomFieldID;
            newCustomField.Name = "superenum" + DateTime.UtcNow.Ticks.ToString();
            newCustomField.Group = "Item";
            newCustomField.Type = CustomFieldType.Enum;
            newCustomField.DefaultValue = string.Empty;

            HttpContent content = new ObjectContent(typeof(CustomField), newCustomField, new JsonMediaTypeFormatter());

            //create
            Task t = client.PutAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/CustomField/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult();
                });
            t.Wait();

            return retVal;
        }

        public JsonResult CreateAssignment(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            CustomFieldAssignRequest newCategory = new CustomFieldAssignRequest();
            newCategory.categoryID = CategoryID;
            newCategory.customFieldID = EnumerationCustomFieldID;

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
    }
}
