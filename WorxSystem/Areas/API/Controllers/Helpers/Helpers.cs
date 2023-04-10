using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers
{
    public static class Helpers
    {
        public static HttpResponseMessage Created(this HttpRequestMessage request, int id)
        {
            //should return http 201 created with location header
            var retVal = request.CreateResponse(HttpStatusCode.Created);
            //retVal.Headers.Location = new Uri(request.RequestUri.AbsoluteUri + "/" + id.ToString(CultureInfo.InvariantCulture));
            retVal.Headers.Location = new Uri(request.RequestUri, id.ToString(CultureInfo.InvariantCulture));
            return retVal;
        }

        public static JsonResult PrepareResult<T>(this HttpResponseMessage response)
        {
            JsonResult retVal = new JsonResult();
            if (response.StatusCode.IsSuccess())
            {                
                retVal.Data = new
                {
                    status = response.StatusCode,
                    detail = response.Content.ReadAsAsync<T>().Result
                };                
            }
            else
            {
                retVal.Data = new
                {
                    status = response.StatusCode,
                    detail = response.Content.ReadAsAsync<ErrorResponse>().Result
                };
            }

            return retVal;
        }

        public static JsonResult PrepareResult(this HttpResponseMessage response)
        {
            JsonResult retVal = new JsonResult();
            if (response.StatusCode.IsSuccess())
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    retVal.Data = new
                                      {
                                          status = response.StatusCode,
                                          detail = "No Content"
                                      };
                } else if (response.StatusCode == HttpStatusCode.Created)
                {
                    retVal.Data = new
                                      {
                                          status = response.StatusCode,
                                          detail = response.Headers.Location
                                      };                    
                } else
                {
                    retVal.Data = new
                    {
                        status = response.StatusCode,
                        detail = "Success was not No Content (204) or Created (201)"
                    }; 
                }
            }
            else
            {
                retVal.Data = new
                {
                    status = response.StatusCode,
                    detail = response.Content.ReadAsAsync<ErrorResponse>().Result
                };
            }

            return retVal;
        }

        public static string GetUserName(this HttpRequestMessage request)
        {
            if (request.Headers.Authorization == null) throw new SecurityException();

            char[] signatureSplitter = new char[] { Utilities.SignatureSeparatorCharacter };
            return request.Headers.Authorization.Parameter.Split(signatureSplitter)[0];
        }

        public static bool IsSuccess(this HttpStatusCode code)
        {
            return ((int) code >= 200 && (int) code < 300);
        }        
    }
}