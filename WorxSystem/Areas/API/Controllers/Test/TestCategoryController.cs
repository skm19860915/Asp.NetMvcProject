using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestCategoryController : TestControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }

        public JsonResult GetCategory(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);            

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/" + id)
                .ContinueWith((response) => 
                                {                
                                    retVal = response.Result.PrepareResult<Category>();                                           
                                });
            t.Wait();
            return retVal;
        }

        public JsonResult GetChildCategories(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/Children/" + id)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult<List<Category>>();
                                  });
            t.Wait();
            return retVal;
        }

        public JsonResult GetRoot(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Task t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/")
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult<Category>();                                                  
                                  });
            t.Wait();
            return retVal;
        }

        public JsonResult CreateCategory(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);  

            Category newCategory = new Category();
            newCategory.Name = DateTime.UtcNow.Ticks.ToString();
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

        //private HttpClient GetProperClient(string auth)
        //{
        //    HttpClient client = null;
        //    if (auth == null) auth = "ugly";
        //    switch (auth.ToLower())
        //    {
        //        case "admin":
        //            client = HttpClientFactory.Create(new HttpClientHandler(),                        
        //                                                 new OverrideDate(),
        //                                                 new SignRequest("admin", "QvUeZy5XL2mS8Zne8ncmitg7g3NZr0LzZyHQmT25rSA="));
        //            return client;
        //        case "nonadmin":
        //            client = HttpClientFactory.Create(new HttpClientHandler(),                        
        //                                                 new OverrideDate(),
        //                                                 new SignRequest("test", "KJyvP2MoQSXev9doZKMwp2pQuTzCusDKnVnHU+fRvYg="));
        //            return client;
        //        case "badkey":
        //            client = HttpClientFactory.Create(new HttpClientHandler(),
        //                                                 new OverrideDate(),
        //                                                 new SignRequest("test", "GGMMK8qAUe8gUQK2fjx/ipy+Vw4ndc4ksk+ZNz+FBSo="));
        //            return client;
        //        case "nouser":
        //            client = HttpClientFactory.Create(new HttpClientHandler(),
        //                                                 new OverrideDate(),
        //                                                 new SignRequest("nancy828", "KJyvP2MoQSXev9doZKMwp2pQuTzCusDKnVnHU+fRvYg="));
        //            return client;
        //        default:
        //            client = HttpClientFactory.Create(new HttpClientHandler(),                        
        //                                                 new OverrideDate());
        //            return client;                    
        //    }
        //}

        public JsonResult UpdateCategory(string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            Category newCategory = new Category();
            newCategory.ID = TestControllerBase.CategoryID;
            newCategory.Name = DateTime.UtcNow.Ticks.ToString();
            newCategory.ParentCategoryID = 9;
            newCategory.Type = "Item";
            newCategory.MVCAction = string.Empty;

            HttpContent content = new ObjectContent(typeof(Category), newCategory, new JsonMediaTypeFormatter());

            //create
            Task t = client.PutAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/", content)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult();
                                  });
            t.Wait();

            return retVal;
        }

        public JsonResult DeleteCategory(int id, string auth)
        {
            JsonResult retVal = null;            
            
            HttpClient client = GetProperClient(auth);

            Task t = client.DeleteAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Category/" + id)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult();                                                  
                                  });
            t.Wait();
            return retVal;
        }

    }
}
