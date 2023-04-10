using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaLoader;
using RainWorx.FrameWorx.Providers.MediaSaver;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to media-specific MVC requests
    /// </summary>
    public class MediaController : AuctionWorxController
    {
        /// <summary>
        /// Saves the uploaded file as a media asset.        
        /// </summary>        
        /// <returns>JSON encoded string with "guid" &amp; "uri" on success, or "error" on failure</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadListingImage()
        {
            JsonResult result = new JsonResult();

            string context = Strings.MediaUploadContexts.UploadListingImage;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media newImage = mediaGenerator.Generate(generatorProviderSettings, Request.Files[0].InputStream);
            newImage.Context = context;

            //Save the media    
            newImage.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, newImage);

            //Load the media (for thumbnail preview)
            newImage.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, newImage, SiteClient.ThumbnailType);

            //Save the media object to the db                        
            CommonClient.AddMedia("AsyncUploader", newImage);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = newImage.GUID.ToString(),
                uri = loadResult,
            };
            return result;
        }

        /// <summary>
        /// Saves the uploaded file as a media asset.        
        /// </summary>        
        /// <returns>JSON encoded string with "guid" &amp; "uri" on success, or "error" on failure</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadEventImage(string resultstyle)
        {
            JsonResult result = new JsonResult();

            string context = Strings.MediaUploadContexts.UploadEventImage;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media newImage = mediaGenerator.Generate(generatorProviderSettings, Request.Files[0].InputStream);
            newImage.Context = context;

            //Save the media    
            newImage.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, newImage);

            //Load the media (for thumbnail preview)
            newImage.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, newImage, string.IsNullOrEmpty(resultstyle) ? "ThumbCrop" : resultstyle);

            //Save the media object to the db                        
            CommonClient.AddMedia("AsyncUploader", newImage);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = newImage.GUID.ToString(),
                uri = loadResult,
            };
            return result;
        }

        /// <summary>
        /// Saves the uploaded file as a media asset.        
        /// </summary>        
        /// <returns>JSON encoded string with "guid" &amp; "uri" on success, or "error" on failure</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadEventBanner(string resultstyle)
        {
            JsonResult result = new JsonResult();

            string context = Strings.MediaUploadContexts.UploadEventBanner;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media newImage = mediaGenerator.Generate(generatorProviderSettings, Request.Files[0].InputStream);
            newImage.Context = context;

            //Save the media    
            newImage.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, newImage);

            //Load the media (for thumbnail preview)
            newImage.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, newImage, string.IsNullOrEmpty(resultstyle) ? "ThumbCrop" : resultstyle);

            //Save the media object to the db                        
            CommonClient.AddMedia("AsyncUploader", newImage);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = newImage.GUID.ToString(),
                uri = loadResult,
            };
            return result;
        }

        /// <summary>
        /// deletes the specified media
        /// </summary>
        /// <param name="guid">guid of the specified media</param>
        /// <returns>JsonResult with details about the success or failure of this request</returns>
        [HttpPost]
        public JsonResult DeleteMedia(string guid)
        {
            JsonResult result = new JsonResult();

            Guid mediaGuid;
            if (!Guid.TryParse(guid, out mediaGuid))
            {
                result.Data = new
                {
                    Success = false,
                    Message = "Could not parse guid"
                };
                return result;
            }

            Media media = CommonClient.GetMediaByGUID("AsyncUploader", mediaGuid);

            //only actually delete this media if there are exactly 0 or 1 listings associated with it
            if (ListingClient.GetCountOfListingsByMediaId(media.ID) < 2)
            {
                IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(media.Saver);
                Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, media.Context);
                if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                {
                    saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                }
                mediaSaver.Delete(saverProviderSettings, media);
                CommonClient.DeleteMedia("AsyncUploader", mediaGuid);
            }

            result.Data = new
            {
                Success = true,
                Message = guid + " media deleted."
            };
            return result;
        }

        /// <summary>
        /// rotates the specified media 90 degrees
        /// </summary>
        /// <param name="guid">guid of the specified media</param>
        /// <param name="clockwise">true to rotate clockwise, false to rotate counter-clockwise</param>
        /// <param name="resultstyle"></param>
        /// <returns>specifies the media variation to return the url to, default &quot;ThumbCrop&quot;</returns>
        [HttpPost]
        public JsonResult RotateMedia(string guid, bool clockwise, string resultstyle)
        {
            JsonResult result = new JsonResult();

            Guid mediaGuid;
            if (!Guid.TryParse(guid, out mediaGuid))
            {
                result.Data = new
                {
                    Success = false,
                    Message = "Could not parse guid"
                };
                return result;
            }

            Media media = CommonClient.GetMediaByGUID("AsyncUploader", mediaGuid);

            string physicalURI = null;
            if (media.Variations.Any(v => v.Key == "Original"))
            {
                if (!media.Variations["Original"].Asset.MetaData.ContainsKey("PhysicalURI"))
                {
                    result.Data = new
                    {
                        Success = false,
                        Message = "No PhysicalURI (Media possibly created before PhysicalURI was added)"
                    };
                    return result;
                }
                physicalURI = media.Variations["Original"].Asset.MetaData["PhysicalURI"];
            }
            else if (media.Variations.Any(v => v.Key == "LargeSize"))
            {
                if (!media.Variations["LargeSize"].Asset.MetaData.ContainsKey("PhysicalURI"))
                {
                    result.Data = new
                    {
                        Success = false,
                        Message = "No PhysicalURI (Media possibly created before PhysicalURI was added)"
                    };
                    return result;
                }
                physicalURI = media.Variations["LargeSize"].Asset.MetaData["PhysicalURI"];
            }
            if (string.IsNullOrEmpty(physicalURI))
            {
                result.Data = new
                {
                    Success = false,
                    Message = "PhysicalURI not found (Media possibly created before PhysicalURI was added)"
                };
                return result;
            }

            try
            {

                //get original image
                WebRequest request = WebRequest.Create(new Uri(physicalURI));
                WebResponse response = request.GetResponse();

                //rotate image
                Image newImage = Image.FromStream(response.GetResponseStream());
                response.Close();
                newImage.RotateFlip(clockwise ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate270FlipNone);
                MemoryStream ms = new MemoryStream();
                newImage.Save(ms, ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                //save new image
                Dictionary<string, string> workflowParams =
                            CommonClient.GetAttributeData("MediaAsset.Workflow", media.Context);
                if (workflowParams.Count == 0)
                {
                    throw new ArgumentException("No such context exists");
                }
                string saverString = workflowParams["Saver"];
                IMediaSaver mediaSaver =
                    RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                Dictionary<string, string> saverProviderSettings =
                    CommonClient.GetAttributeData(mediaSaver.TypeName, media.Context);

                IMediaGenerator mediaGenerator =
                    RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(
                        workflowParams["Generator"]);
                Dictionary<string, string> generatorProviderSettings =
                    CommonClient.GetAttributeData(mediaGenerator.TypeName, media.Context);
                Media newMedia = mediaGenerator.Generate(generatorProviderSettings, ms);
                newMedia.Context = media.Context;
                newMedia.Saver = saverString;
                newMedia.Loader = workflowParams["Loader"];
                if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                {
                    saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                }
                mediaSaver.Save(saverProviderSettings, newMedia);

                //Load the media (for thumbnail preview)            
                IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newMedia.Loader);
                Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, newMedia.Context);
                string loadResult = mediaLoader.Load(loaderProviderSettings, newMedia, string.IsNullOrEmpty(resultstyle) ? "ThumbCrop" : resultstyle);

                //Save the media object to the db                        
                CommonClient.AddMedia("AsyncUploader", newMedia);

                //disabled -- this causes problems if an "Edit Lot/Listing" form is cancelled without saving
                ////delete old media
                //mediaSaver.Delete(saverProviderSettings, media);
                //CommonClient.DeleteMedia("AsyncUploader", mediaGuid);

                result.Data = new
                {
                    Success = true,
                    Message = guid + " media rotated.",
                    OldGUID = guid,
                    NewGUID = newMedia.GUID,
                    NewURI = loadResult
                };
            }
            catch (Exception e)
            {
                result.Data = new
                {
                    Success = false,
                    Message = "Error: " + e.Message
                };
            }
            return result;
        }

        /// <summary>
        /// Saves the uploaded file as a media asset.        
        /// </summary>        
        /// <returns>JSON encoded string with "guid" &amp; "uri" on success, or "error" on failure</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadFile()
        {
            JsonResult result = new JsonResult();

            string context = Strings.MediaUploadContexts.UploadFile;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            HttpPostedFileBase file = Request.Files[0];
            FileInfo fileInfo = new FileInfo(file.FileName);

            //Generate the media object
            IMediaGenerator mediaGenerator;
            try
            {
                mediaGenerator = Unity.UnityResolver.Get<IMediaGenerator>(fileInfo.Extension.ToLower());
            }
            catch (Exception)
            {
                result.Data = new { error = this.GlobalResourceString("NoGeneratorRegisteredToHandleExtension") };
                return result;
            }

            Dictionary<string, string> generatorProviderSettings = new Dictionary<string, string>(3);
            generatorProviderSettings["ContentLength"] = file.ContentLength.ToString(CultureInfo.InvariantCulture);
            generatorProviderSettings["ContentType"] = file.ContentType;
            generatorProviderSettings["FileName"] = file.FileName;

            Media uploadedFile = mediaGenerator.Generate(generatorProviderSettings, file.InputStream);
            if (uploadedFile == null)
            {
                result.Data = new { error = this.GlobalResourceString(generatorProviderSettings["error"]) };
                return result;
            }
            uploadedFile.Context = context;

            //Save the media    
            uploadedFile.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = Unity.UnityResolver.Get<IMediaSaver>(uploadedFile.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, uploadedFile);

            //Load the media (for thumbnail preview)
            uploadedFile.Loader = workflowParams["Loader"];
            //IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(uploadedFile.Loader);
            //Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            //string loadResult = mediaLoader.Load(loaderProviderSettings, uploadedFile, "Main");

            //Save the media object to the db                        
            CommonClient.AddMedia("AsyncUploadFile", uploadedFile);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = uploadedFile.GUID.ToString(),
                uri = "Media/" + uploadedFile.GUID.ToString(),
                name = Path.GetFileName(file.FileName).ToJavascriptSafeString(),
                size = file.ContentLength,
                type = uploadedFile.Default.MetaData["Type"],
                title = Path.GetFileName(file.FileName).ToJavascriptSafeString()
            };
            return result;
        }

        /// <summary>
        /// Gets a media file
        /// </summary>
        /// <param name="id">the guid or id of the Media to get</param>
        /// <returns>A media File</returns>
        public FileContentResult Get(string id)
        {
            Guid mediaGuid;
            int mediaId;
            Media media = null;
            if (Guid.TryParse(id, out mediaGuid))
            {
                media = CommonClient.GetMediaByGUID(this.FBOUserName(), mediaGuid);
            }
            else if (int.TryParse(id, out mediaId))
            {
                media = CommonClient.GetMediaByID(this.FBOUserName(), mediaId);
            }

            if (media == null)
            {
                throw new HttpException(404, "Media Doesn't Exist");
                //Response.StatusCode = 404;
                //return null;
            }

            IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, Strings.MediaUploadContexts.UploadFile);
            string loadResult = mediaLoader.Load(loaderProviderSettings, media, "Main");

            byte[] fileData = System.IO.File.ReadAllBytes(Server.MapPath(loadResult));

            FileContentResult content = new FileContentResult(fileData, media.Default.MetaData["MIMEType"]);
            content.FileDownloadName = media.Default.MetaData["OriginalFileName"];
            return content;
        }

        /// <summary>
        /// Processes an ajax request to save the requested data as a file
        /// </summary>
        /// <returns>the path the file was saved to</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadLogo()
        {
            JsonResult result = new JsonResult();

            const string context = Strings.MediaUploadContexts.UploadSiteLogo;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media newImage = mediaGenerator.Generate(generatorProviderSettings, Request.Files[0].InputStream);
            newImage.Context = context;

            //Save the media    
            newImage.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, newImage);

            //Load the media (for thumbnail preview)
            newImage.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);

            //Save the media object to the db
            CommonClient.AddMedia("AsyncUploader", newImage);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = newImage.GUID.ToString(),
                uri = loadResult,
            };
            return result;
        }

        /// <summary>
        /// Processes an ajax request to save the requested data as a file
        /// </summary>
        /// <returns>the path the file was saved to</returns>
        [HttpPost]
        [NoAuthenticate]
        public JsonResult AsyncUploadBanner()
        {
            JsonResult result = new JsonResult();

            const string context = Strings.MediaUploadContexts.UploadBannerImage;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media newImage = mediaGenerator.Generate(generatorProviderSettings, Request.Files[0].InputStream);
            newImage.Context = context;

            //Save the media    
            newImage.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            if (!saverProviderSettings.ContainsKey("VirtualFolder"))
            {
                saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
            }
            mediaSaver.Save(saverProviderSettings, newImage);

            //Load the media (for thumbnail preview)
            newImage.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);

            //Save the media object to the db
            CommonClient.AddMedia("AsyncUploader", newImage);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = new
            {
                guid = newImage.GUID.ToString(),
                uri = loadResult,
            };
            return result;
        }

    }
}
