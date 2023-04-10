using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Security;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaSaver;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class ImageColumnSpec : ColumnSpecBase
    {        
        private string ActingUserName;
        private string WebRoot;
        private int Order;

        public ImageColumnSpec(int number, string name, string cultureCode, string actingUserName, string webroot, int imageOrder, string notes, string example)
            : base(number, name, CustomFieldType.String, notes, false, cultureCode, example)
        {
            ActingUserName = actingUserName;
            WebRoot = webroot;
            Order = imageOrder;
        }        

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                string uri = csvRow.ColumnData[this.Name];

                if (!string.IsNullOrEmpty(uri))
                {
                    try
                    {
                        WebRequest request = WebRequest.Create(uri);
                        try
                        {
                            WebResponse response = request.GetResponse();
                            Image originalImage = Image.FromStream(response.GetResponseStream());
                            return true;
                        }
                        catch (Exception e)
                        {
                            csvRow.Disposition.Add("[" + this.Name + "] \"" + uri +
                                                   "\" is not a valid image format -or- other retrieval issue (" +
                                                   CSV.FlattenExceptionMessages(e) + ")");
                            return false;
                        }
                    }
                    catch (NotSupportedException /*nse*/)
                    {
                        csvRow.Disposition.Add("The request scheme specified in " + uri + " has not been registered.");
                        return false;
                    }
                    catch (SecurityException /*se*/)
                    {
                        csvRow.Disposition.Add("The caller does not have permission to connect to the requested URI, " + uri + ", or a URI that the request is redirected to.");
                        return false;
                    }
                    catch (UriFormatException /*ufe*/)
                    {
                        csvRow.Disposition.Add("The URI specified, " + uri + " is not a valid URI.");
                        return false;
                    }
                    catch (Exception e)
                    {                        
                        csvRow.Disposition.Add("[" + this.Name + "], " + CSV.FlattenExceptionMessages(e));
                        return false;
                    }
                }
                return true;
            } else
            {
                return false;
            }            
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {            
            string uri = csvRow.ColumnData[this.Name];

            if (!string.IsNullOrEmpty(uri))
            {
                if (commitIntent)
                {
                    WebRequest request = WebRequest.Create(uri);
                    WebResponse response = request.GetResponse();
                    Dictionary<string, string> webServiceWorkflowParams =
                        CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                    Strings.MediaUploadContexts.
                                                        UploadListingImage);
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
                                                        UploadListingImage);
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
                                                        UploadListingImage);
                    Media newMedia = mediaGenerator.Generate(generatorProviderSettings,
                                                            response.GetResponseStream());
                    newMedia.Context = Strings.MediaUploadContexts.UploadListingImage;
                    newMedia.Saver = saverString;
                    newMedia.Loader = webServiceWorkflowParams["Loader"];
                    if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                    {
                        saverProviderSettings.Add("VirtualFolder", WebRoot);
                    }
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