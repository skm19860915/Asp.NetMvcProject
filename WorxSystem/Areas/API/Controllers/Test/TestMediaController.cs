using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Mvc;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test
{
    public class TestMediaController : TestControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }        

        public JsonResult UploadFile(string auth)
        {
            JsonResult retVal = null;
            var client = GetProperClient(auth);
            var stream = System.IO.File.Open(Path.Combine(ImageRoot, Image1FileName), FileMode.Open);
            var stream2 = System.IO.File.Open(Path.Combine(ImageRoot, Image2FileName), FileMode.Open);
            var content = new MultipartFormDataContent();
            content.Add(new StreamContent(stream), "toodles", Image1FileName);
            content.Add(new StreamContent(stream2), "toodles", Image2FileName);
            content.Add(new StringContent("UploadListingImage"), "context");


            var task = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/", content)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult<List<Media>>();
                                  });
            task.Wait();            
            return retVal;
        }

        public JsonResult GetMedia(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<Media>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult GetMediaByGuid(Guid guid, string auth)
        {            
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/" + guid)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<Media>();                    
                });
            t.Wait();
            return retVal;
        }

        public JsonResult LoadMedia(int id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);            

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/Load/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult LoadMediaWithVariation(int id, string variation, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/Load/" + id + "/" + variation)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult LoadMediaByGuid(Guid id, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/Load/" + id)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult LoadMediaByGuidWithVariation(Guid id, string variation, string auth)
        {
            JsonResult retVal = null;
            HttpClient client = GetProperClient(auth);

            var t = client.GetAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/Load/" + id + "/" + variation)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<string>();
                });
            t.Wait();
            return retVal;
        }

        public JsonResult UploadFileURI(string auth)
        {
            JsonResult retVal = null;
            var client = GetProperClient(auth);            

            MediaPostURIRequest request = new MediaPostURIRequest();
            request.uri = MediaURI;
            request.context = "UploadListingImage";

            HttpContent content = new ObjectContent(typeof(MediaPostURIRequest), request, new JsonMediaTypeFormatter());                                    

            var task = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/FromURI/", content)
                .ContinueWith((response) =>
                                  {
                                      retVal = response.Result.PrepareResult<Media>();
                                  });
            task.Wait();
            return retVal;
        }

        public JsonResult UploadString(string auth)
        {
            JsonResult retVal = null;
            var client = GetProperClient(auth);

            MediaPostStringRequest request = new MediaPostStringRequest();
            request.value = MediaString;
            request.context = "UploadBannerHtml";

            HttpContent content = new ObjectContent(typeof(MediaPostStringRequest), request, new JsonMediaTypeFormatter());            

            var task = client.PostAsync(Request.Url.Scheme + "://" + Request.Url.Authority + "/api/Media/FromString/", content)
                .ContinueWith((response) =>
                {
                    retVal = response.Result.PrepareResult<Media>();
                });
            task.Wait();
            return retVal;
        }

    }
}
