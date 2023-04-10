using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaSaver;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class YouTubeColumnSpec : ColumnSpecBase
    {
        private string ActingUserName;        
        private int Order;

        public YouTubeColumnSpec(int number, string name, string cultureCode, string actingUserName, int youtubeOrder, string notes)
            : base(number, name, CustomFieldType.String, notes, false, cultureCode, @"7j8UquwOpMQ")
        {
            ActingUserName = actingUserName;            
            Order = youtubeOrder;
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                string videoid = csvRow.ColumnData[this.Name];

                if (!string.IsNullOrEmpty(videoid))
                {                    
                    Regex r = new Regex(@"^(?<VideoID>.{11})$");
                    string youtubeVideoID = r.Match(videoid).Groups["VideoID"].Value;
                    if (string.IsNullOrEmpty(youtubeVideoID))
                    {
                        csvRow.Disposition.Add("[" + this.Name + "] \"" + videoid + "\" is not a valid youtube video id (If it's a URL, then it must be just the video id portion)");
                        return false;           
                    }                                     
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {            
            string videoId = csvRow.ColumnData[this.Name];

            if (!string.IsNullOrEmpty(videoId))
            {
                if (commitIntent)
                {
                    string uri = "http://www.youtube.com/watch?v=" + videoId.Trim();
                    MemoryStream ms = new MemoryStream();
                    StreamWriter sw = new StreamWriter(ms);
                    sw.Write(uri);
                    sw.Flush();

                    Dictionary<string, string> webServiceWorkflowParams =
                        CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                    Strings.MediaUploadContexts.
                                                        UploadYouTubeVideoID);
                    if (webServiceWorkflowParams.Count == 0)
                    {
                        throw new ArgumentException("No such context exists");
                    }
                    string saverString = webServiceWorkflowParams["Saver"];
                    IMediaSaver mediaSaver =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                    Dictionary<string, string> saverProviderSettings =
                        CommonClient.GetAttributeData(mediaSaver.TypeName,
                                                    Strings.MediaUploadContexts.
                                                        UploadYouTubeVideoID);
                    //use the context used by the site for everything else
                    //context = Strings.MediaUploadContexts.UploadListingImage;
                    //Dictionary<string, string> siteWorkflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
                    //Generate the media object
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(
                            webServiceWorkflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName,
                                                    Strings.MediaUploadContexts.
                                                        UploadYouTubeVideoID);
                    Media newMedia = mediaGenerator.Generate(generatorProviderSettings,
                                                            ms);
                    sw.Close();
                    ms.Close();
                    newMedia.Context = Strings.MediaUploadContexts.UploadYouTubeVideoID;
                    newMedia.Saver = saverString;
                    newMedia.Loader = webServiceWorkflowParams["Loader"];                    
                    mediaSaver.Save(saverProviderSettings, newMedia);
                    //Save the media object to the db                        
                    CommonClient.AddMedia(ActingUserName, newMedia);

                    input.Add("media_guid_" + newMedia.GUID.ToString(),
                            newMedia.GUID.ToString());
                    input.Add("media_ordr_" + newMedia.GUID.ToString(),
                            (Order - 1).ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}