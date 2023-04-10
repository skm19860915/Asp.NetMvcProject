using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.IO;
using System.Web.Http.Description;
using System.Web.Http.Routing;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaLoader;
using RainWorx.FrameWorx.Providers.MediaSaver;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    /// <summary>
    /// Provides services to Create/Upload, and Get Media
    /// </summary>
    [RoutePrefix("api/media")]
    public class MediaController : AuctionWorxAPIController
    {                
        /// <summary>
        /// Gets Media by ID
        /// </summary>
        /// <param name="id">The ID of the Media to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Media upon success.  Otherwise, HTTP Status code 404 (Not Found) if the Media is not found.</returns>    
        [Route("{id:int}")]
        [ResponseType(typeof(Media))]
        public HttpResponseMessage GetByID(int id)
        {
            Media media = CommonClient.GetMediaByID(Request.GetUserName(), id);

            if (media == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Media not found");
            }
            else
            {
                return Request.CreateResponse<Media>(HttpStatusCode.OK, media);
            }
        }

        /// <summary>
        /// Gets Media by GUID
        /// </summary>
        /// <param name="guid">The GUID of the Media to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Media upon success.  Otherwise, HTTP Status code 404 (Not Found) if the Media is not found.</returns>    
        [Route("{guid:guid}")]
        [ResponseType(typeof(Media))]
        public HttpResponseMessage GetByGuid(Guid guid)
        {
            Media media = CommonClient.GetMediaByGUID(Request.GetUserName(), guid);

            if (media == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Media not found");
            }
            else
            {
                return Request.CreateResponse<Media>(HttpStatusCode.OK, media);
            }
        }

        /// <summary>
        /// Gets the result of a Media Load operation by ID and returns it.
        /// </summary>
        /// <param name="id">The ID of the Media to Load</param>
        /// <param name="variation">The variation to load.  If null or empty, the default shall be loaded.</param>
        /// <returns>An HTTP Status code of 200 (OK) and the string load result upon success.  Otherwise, HTTP Status code 404 (Not Found) if the Media is not found.</returns>        
        [Route("Load/{id:int}/{variation=}")]
        [ResponseType(typeof(string))]        
        public HttpResponseMessage GetLoaderResultByID(int id, string variation)
        {
            Media media = CommonClient.GetMediaByID(Request.GetUserName(), id);

            if (media == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Media not found");
            }

            IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, media.Context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, media, string.IsNullOrEmpty(variation) ? media.DefaultVariationName : variation);
            return Request.CreateResponse(HttpStatusCode.OK, loadResult);
        }

        /// <summary>
        /// Gets the result of a Media Load operation by GUID and returns it.
        /// </summary>
        /// <param name="guid">The GUID of the Media to Load</param>
        /// <param name="variation">The variation to load.  If null or empty, the default shall be loaded.</param>
        /// <returns>An HTTP Status code of 200 (OK) and the string load result upon success.  Otherwise, HTTP Status code 404 (Not Found) if the Media is not found.</returns>        
        [Route("Load/{guid:guid}/{variation=}")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage GetLoaderResultByGUID(Guid guid, string variation)
        {
            Media media = CommonClient.GetMediaByGUID(Request.GetUserName(), guid);

            if (media == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Media not found");
            }

            IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, media.Context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, media, string.IsNullOrEmpty(variation) ? media.DefaultVariationName : variation);
            return Request.CreateResponse(HttpStatusCode.OK, loadResult);
        }

        /// <summary>
        /// Creates Media by retrieving it from a provided URI.
        /// </summary>
        /// <param name="request">The request object containing context and URI</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, WITHOUT the Location response header set.  The Response Body contains the Media created.</returns>        
        [Route("FromURI")]
        [ResponseType(typeof(Media))]
        public Task<HttpResponseMessage> PostFromURI([FromBody] MediaPostURIRequest request)
        {
            //do a quick [Authorize] compatible check... (user exists, good auth token, but no role checking...)
            string userName = Request.GetUserName();

            if (string.IsNullOrEmpty(request.uri))
            {
                ErrorResponse error = new ErrorResponse();
                error.Message = "Missing URI";
                
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(Request.CreateResponse(HttpStatusCode.BadRequest, error));
                return tsc.Task;
            }

            HttpClient client = new HttpClient();            
            var task = client.GetStreamAsync(request.uri)
                .ContinueWith((response) =>
                                  {                                                                              
                                        Dictionary<string, string> webServiceWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", request.context);
                                        if (webServiceWorkflowParams.Count == 0)
                                        {
                                            ErrorResponse error = new ErrorResponse();
                                            error.Message = "Invalid Context";

                                            return Request.CreateResponse(HttpStatusCode.BadRequest, error);
                                        }
                                        string saverString = webServiceWorkflowParams["Saver"];
                                        IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                                        Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, request.context);
                                        //use the context used by the site for everything else
                                        //context = Strings.MediaUploadContexts.UploadListingImage;
                                        //Dictionary<string, string> siteWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
                                        //Generate the media object
                                        IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(webServiceWorkflowParams["Generator"]);
                                        Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, request.context);
                                        Media newMedia = mediaGenerator.Generate(generatorProviderSettings, response.Result);
                                        newMedia.Context = request.context;
                                        newMedia.Saver = saverString;
                                        newMedia.Loader = webServiceWorkflowParams["Loader"];                                        
                                        if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                                        {                                            
                                            //saverProviderSettings.Add("VirtualFolder", @"H:\Desktop\files\uploaded");
                                            saverProviderSettings.Add("VirtualFolder", Utilities.VirtualRoot);
                                        }
                                        mediaSaver.Save(saverProviderSettings, newMedia);
                                        //Save the media object to the db                        
                                        CommonClient.AddMedia(userName, newMedia);

                                        foreach (Variation v in newMedia.Variations.Values)
                                        {
                                            v.Asset.Data = null;
                                        }

                                        return Request.CreateResponse<Media>(HttpStatusCode.Created, newMedia);                                      
                                  });
            return task;
        }

        /// <summary>
        /// Creates Media by from a provided string.
        /// </summary>
        /// <param name="request">The request object containing context and string value</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, WITHOUT the Location response header set.  The Response Body contains the Media created.</returns>
        [Route("FromString")]
        [ResponseType(typeof(Media))]
        public HttpResponseMessage PostString([FromBody] MediaPostStringRequest request)
        {
            //do a quick [Authorize] compatible check... (user exists, good auth token, but no role checking...)
            string userName = Request.GetUserName();

            if (string.IsNullOrEmpty(request.value))
            {
                ErrorResponse error = new ErrorResponse();
                error.Message = "Missing string data";

                return Request.CreateResponse(HttpStatusCode.BadRequest, error);
            }
            
            Dictionary<string, string> webServiceWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", request.context);
            if (webServiceWorkflowParams.Count == 0)
            {
                ErrorResponse error = new ErrorResponse();
                error.Message = "Invalid Context";

                return Request.CreateResponse(HttpStatusCode.BadRequest, error);
            }

            //Read string data into stream
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            sw.Write(request.value);
            sw.Flush();
            ms.Seek(0, SeekOrigin.Begin);

            string saverString = webServiceWorkflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, request.context);

            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                //saverProviderSettings.Add("VirtualFolder", @"H:\Desktop\files\uploaded");
                saverProviderSettings.Add("VirtualFolder", Utilities.VirtualRoot);
            }

            //use the context used by the site for everything else
            //context = Strings.MediaUploadContexts.UploadListingImage;
            //Dictionary<string, string> siteWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(webServiceWorkflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, request.context);
            Media newMedia = mediaGenerator.Generate(generatorProviderSettings, ms);
            newMedia.Context = request.context;
            newMedia.Saver = saverString;
            newMedia.Loader = webServiceWorkflowParams["Loader"];            
            mediaSaver.Save(saverProviderSettings, newMedia);
            //Save the media object to the db                        
            CommonClient.AddMedia(userName, newMedia);

            foreach (Variation v in newMedia.Variations.Values)
            {
                v.Asset.Data = null;
            }

            return Request.CreateResponse<Media>(HttpStatusCode.Created, newMedia);                            
        }

        /// <summary>
        /// Creates Media from Request Body.
        /// 
        /// Accepts MIME multipart content (multipart/form-data) of one or more file "parts", and a single "context" string part.
        /// "context" must have a Content-Type of "text/plain", and it's Content-Disposition, "name," must be "context".
        /// Media files must have a "filename" in their Content-Dispositions ("name" can be anything).
        /// </summary>
        /// <returns>An HTTP Status code of 201 (Created) upon success, WITHOUT the Location response header set.  The Response Body contains a List of Media created.</returns>
        [Route("")]
        [ResponseType(typeof(Media))]        
        public Task<HttpResponseMessage> Post()
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }
                        
            var provider = new MultipartMemoryStreamProvider();

            var task = request.Content.ReadAsMultipartAsync(provider).
                ContinueWith<HttpResponseMessage>(o =>
                                                      {          
                                                          //do a quick [Authorize] compatible check... (user exists, good auth token, but no role checking...)
                                                          string userName = request.GetUserName();

                                                          List<Media> media = new List<Media>();
                                                          HttpContent contextContent = 
                                                              provider.Contents.SingleOrDefault(c => c.Headers.ContentDisposition.Name.Replace("\"", "").
                                                                                                         ToLower() == "context");
                                                          if (contextContent == null)
                                                          {
                                                              ErrorResponse error = new ErrorResponse();
                                                              error.Message = "Missing Context";

                                                              return request.CreateResponse(HttpStatusCode.BadRequest, error);
                                                          }
                                                          string context = contextContent.ReadAsStringAsync().Result;
                                                          if (string.IsNullOrEmpty(context))
                                                          {
                                                              ErrorResponse error = new ErrorResponse();
                                                              error.Message = "Missing Context";

                                                              return request.CreateResponse(HttpStatusCode.BadRequest, error);
                                                          }

                                                          //Prepare Media Workflow
                                                          Dictionary<string, string> webServiceWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
                                                          if (webServiceWorkflowParams.Count == 0)
                                                          {
                                                              ErrorResponse error = new ErrorResponse();
                                                              error.Message = "Invalid Context";

                                                              return request.CreateResponse(HttpStatusCode.BadRequest, error);
                                                          }
                                                          string saverString = webServiceWorkflowParams["Saver"];
                                                          string loaderString = webServiceWorkflowParams["Loader"];
                                                          IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                                                          Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
                                                          IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(webServiceWorkflowParams["Generator"]);
                                                          Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
                                                                                                                    
                                                          if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                                                          {                                                              
                                                              //saverProviderSettings.Add("VirtualFolder", @"H:\Desktop\files\uploaded");
                                                              saverProviderSettings.Add("VirtualFolder", Utilities.VirtualRoot);
                                                          }
                                                          //End Prepare Media Workflow

                                                          foreach (var item in provider.Contents)
                                                          {
                                                              if (!string.IsNullOrEmpty(item.Headers.ContentDisposition.FileName))                                                                                                                            
                                                              {
                                                                  Stream inputStream = item.ReadAsStreamAsync().Result;
                                                                  //string fileName =
                                                                  //    item.Headers.ContentDisposition.FileName.Replace(
                                                                  //        "\"", "");

                                                                  //Add Media Here!                                                                  
                                                                  
                                                                  Media newMedia = mediaGenerator.Generate(generatorProviderSettings, inputStream);
                                                                  newMedia.Context = context;
                                                                  newMedia.Saver = saverString;
                                                                  newMedia.Loader = loaderString;                                                                  
                                                                  mediaSaver.Save(saverProviderSettings, newMedia);
                                                                  //Save the media object to the db                        
                                                                  CommonClient.AddMedia(userName, newMedia);

                                                                  //clear Data property of all assets for response
                                                                  foreach (Variation v in newMedia.Variations.Values)
                                                                  {
                                                                      v.Asset.Data = null;
                                                                  }

                                                                  //End Add Media!                                                                  
                                                                  media.Add(newMedia);
                                                              }
                                                          }

                                                          return Request.CreateResponse<List<Media>>(HttpStatusCode.Created, media);
                                                      }
            );
            return task;
        }

        /// <summary>
        /// Creates non-image Media from Request Body.
        /// 
        /// Accepts MIME multipart content (multipart/form-data) of one or more file "parts", and a single "context" string part.
        /// "context" must have a Content-Type of "text/plain", and it's Content-Disposition, "name," must be "context".
        /// Media files must have a "filename" in their Content-Dispositions ("name" can be anything).
        /// </summary>
        /// <returns>An HTTP Status code of 201 (Created) upon success, WITHOUT the Location response header set.  The Response Body contains a List of Media created.</returns>
        [Route("file")]
        [ResponseType(typeof(Media))]
        public Task<HttpResponseMessage> PostFile()
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            var provider = new MultipartMemoryStreamProvider();

            var task = request.Content.ReadAsMultipartAsync(provider).
                ContinueWith<HttpResponseMessage>(o =>
                {
                    //do a quick [Authorize] compatible check... (user exists, good auth token, but no role checking...)
                    string userName = request.GetUserName();

                    List<Media> media = new List<Media>();
                    HttpContent contextContent =
                        provider.Contents.SingleOrDefault(c => (c.Headers.ContentDisposition.Name ?? "").Replace("\"", "").
                                                                   ToLower() == "context");
                    if (contextContent == null)
                    {
                        ErrorResponse error = new ErrorResponse();
                        error.Message = "Missing Context";

                        return request.CreateResponse(HttpStatusCode.BadRequest, error);
                    }
                    string context = contextContent.ReadAsStringAsync().Result;
                    if (string.IsNullOrEmpty(context))
                    {
                        ErrorResponse error = new ErrorResponse();
                        error.Message = "Missing Context";

                        return request.CreateResponse(HttpStatusCode.BadRequest, error);
                    }

                    foreach (var item in provider.Contents)
                    {
                        if (item.Headers.ContentDisposition.FileName == null)
                            continue;

                        //Prepare Media Workflow
                        Dictionary<string, string> webServiceWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
                        if (webServiceWorkflowParams.Count == 0)
                        {
                            ErrorResponse error = new ErrorResponse();
                            error.Message = "Invalid Context";

                            return request.CreateResponse(HttpStatusCode.BadRequest, error);
                        }
                        string saverString = webServiceWorkflowParams["Saver"];
                        string loaderString = webServiceWorkflowParams["Loader"];
                        IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                        Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);

                        if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                        {
                            //saverProviderSettings.Add("VirtualFolder", @"H:\Desktop\files\uploaded");
                            saverProviderSettings.Add("VirtualFolder", Utilities.VirtualRoot);
                        }
                        //End Prepare Media Workflow

                        string fileName = item.Headers.ContentDisposition.FileName.Replace("\"", "").Replace("\\", "").Trim();
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            string[] fileparts = fileName.Split('.');
                            string fileExtension = string.Format(".{0}", fileparts.Length > 0 ? fileparts[fileparts.Length - 1] : string.Empty);

                            Stream inputStream = item.ReadAsStreamAsync().Result;

                            //Add Media Here!
                            
                            //Generate the media object
                            IMediaGenerator mediaGenerator;
                            try
                            {
                                mediaGenerator = Unity.UnityResolver.Get<IMediaGenerator>(fileExtension.ToLower());
                            }
                            catch (Exception e)
                            {
                                //result.Data = new { error = this.GlobalResourceString("NoGeneratorRegisteredToHandleExtension") };
                                //return result;
                                ErrorResponse error = new ErrorResponse();
                                error.Message = string.Format("No IMediaGenerator registered to handle extension \"{0}\".", fileExtension);
                                return request.CreateResponse(HttpStatusCode.BadRequest, error);
                            }

                            Dictionary<string, string> generatorProviderSettings = new Dictionary<string, string>(3);
                            generatorProviderSettings["ContentLength"] = "unknown";
                            generatorProviderSettings["ContentType"] = item.Headers.ContentType.MediaType;
                            generatorProviderSettings["FileName"] = fileName;

                            Media newMedia = mediaGenerator.Generate(generatorProviderSettings, inputStream);
                            newMedia.Context = context;
                            newMedia.Saver = saverString;
                            newMedia.Loader = loaderString;
                            mediaSaver.Save(saverProviderSettings, newMedia);
                            //Save the media object to the db                        
                            CommonClient.AddMedia(userName, newMedia);

                            //clear Data property of all assets for response
                            foreach (Variation v in newMedia.Variations.Values)
                            {
                                v.Asset.Data = null;
                            }

                            //End Add Media!                                                                  
                            media.Add(newMedia);
                        }
                    }

                    return Request.CreateResponse<List<Media>>(HttpStatusCode.Created, media);
                }
            );
            return task;
        }

    }
}
