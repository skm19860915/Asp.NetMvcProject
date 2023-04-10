using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaSaver;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class PdfColumnSpec : ColumnSpecBase
    {        
        private string ActingUserName;
        private string WebRoot;

        public PdfColumnSpec(int number, string name, string cultureCode, string actingUserName, string webroot, string notes, string example)
            : base(number, name, CustomFieldType.String, notes, false, cultureCode, example)
        {
            ActingUserName = actingUserName;
            WebRoot = webroot;
        }        

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                bool retVal = true;
                //string uri = csvRow.ColumnData[this.Name];
                foreach (string uriTitlePair in csvRow.ColumnData[this.Name].Split('|'))
                {
                    var uriTitleParts = uriTitlePair.Split('{');
                    string uri = uriTitleParts[0];
                    string friendlyTitle = uriTitleParts.Length > 1 ? uriTitleParts[1].Replace("}", "") : null;
                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(friendlyTitle))
                            {
                                var temp = new Uri(uri);
                                friendlyTitle = temp.Segments[temp.Segments.Length - 1];
                            }
                            WebRequest request = WebRequest.Create(uri);
                            try
                            {
                                WebResponse response = request.GetResponse();

                                //confirm the first 4 bytes of the response data correctly indicate a valid PDF file
                                byte[] fileData = new byte[4] { 0, 0, 0, 0 };
                                response.GetResponseStream().Read(fileData, 0, 4);
                                if (fileData[0] != 0x25 ||
                                    fileData[1] != 0x50 ||
                                    fileData[2] != 0x44 ||
                                    fileData[3] != 0x46)
                                {
                                    throw new Exception("Malformed PDF Binary Signature");
                                }

                            }
                            catch (Exception e)
                            {
                                csvRow.Disposition.Add("[" + this.Name + "] \"" + uri +
                                                       "\" is not a valid PDF format -or- other retrieval issue (" +
                                                       CSV.FlattenExceptionMessages(e) + ")");
                                retVal = false;
                            }
                        }
                        catch (NotSupportedException /*nse*/)
                        {
                            csvRow.Disposition.Add("The request scheme specified in " + uri + " has not been registered.");
                            retVal = false;
                        }
                        catch (SecurityException /*se*/)
                        {
                            csvRow.Disposition.Add("The caller does not have permission to connect to the requested URI, " + uri + ", or a URI that the request is redirected to.");
                            retVal = false;
                        }
                        catch (UriFormatException /*ufe*/)
                        {
                            csvRow.Disposition.Add("The URI specified, " + uri + " is not a valid URI.");
                            retVal = false;
                        }
                        catch (Exception e)
                        {
                            csvRow.Disposition.Add("[" + this.Name + "], " + CSV.FlattenExceptionMessages(e));
                            retVal = false;
                        }
                    }
                }
                return retVal;
            }
            else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            int Order = 0;
            string context = Strings.MediaUploadContexts.UploadFile;
            foreach (string uriTitlePair in csvRow.ColumnData[this.Name].Split('|'))
            {
                var uriTitleParts = uriTitlePair.Split('{');
                string uri = uriTitleParts[0];
                string friendlyTitle = uriTitleParts.Length > 1 ? uriTitleParts[1].Replace("}", "") : null;
                if (!string.IsNullOrWhiteSpace(uri))
                {
                    if (commitIntent)
                    {
                        var temp = new Uri(uri);
                        string fileName = temp.Segments[temp.Segments.Length - 1];
                        if (string.IsNullOrWhiteSpace(friendlyTitle))
                        {
                            friendlyTitle = fileName;
                        }
                        //retrieve the media from the specified URI
                        WebRequest request = WebRequest.Create(uri);
                        WebResponse response = request.GetResponse();
                        Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);
                        if (workflowParams.Count == 0)
                        {
                            throw new ArgumentException("No such context exists");
                        }

                        //Generate the media object
                        IMediaGenerator mediaGenerator = Unity.UnityResolver.Get<IMediaGenerator>(".pdf");
                        Dictionary<string, string> generatorProviderSettings = new Dictionary<string, string>(3);
                        generatorProviderSettings["ContentLength"] = response.ContentLength.ToString(CultureInfo.InvariantCulture);
                        generatorProviderSettings["ContentType"] = response.ContentType;
                        generatorProviderSettings["FileName"] = fileName;

                        Media newMedia = mediaGenerator.Generate(generatorProviderSettings, response.GetResponseStream());

                        //Save the media content
                        string saverString = workflowParams["Saver"];
                        IMediaSaver mediaSaver = Unity.UnityResolver.Get<IMediaSaver>(saverString);
                        Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
                        newMedia.Context = context;
                        newMedia.Saver = saverString;
                        newMedia.Loader = workflowParams["Loader"];
                        if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                        {
                            saverProviderSettings.Add("VirtualFolder", WebRoot);
                        }
                        mediaSaver.Save(saverProviderSettings, newMedia);

                        //Save the media object to the db                        
                        CommonClient.AddMedia(ActingUserName, newMedia);

                        input.Add("media_guid_" + newMedia.GUID.ToString(), newMedia.GUID.ToString());
                        input.Add("media_ordr_" + newMedia.GUID.ToString(), (Order++).ToString(CultureInfo.InvariantCulture));
                        input.Add("media_title_" + newMedia.GUID.ToString(), friendlyTitle);
                    }
                }
            }
        }
    }
}