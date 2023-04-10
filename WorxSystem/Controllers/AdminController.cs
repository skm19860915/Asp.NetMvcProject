using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.MVC.Models.CSV;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaLoader;
using RainWorx.FrameWorx.Providers.MediaSaver;
using RainWorx.FrameWorx.Strings;
using System.ServiceModel;
using System.Web;
using RainWorx.FrameWorx.Utility;
using LogEntry = RainWorx.FrameWorx.DTO.LogEntry;

using System.Data;
using System.Configuration;

using Microsoft.AspNet.Identity.Owin;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to admin-specific MVC requests
    /// </summary>
    [GoSecure]
    [Authenticate]
    public class AdminController : AuctionWorxController
    {
        private AuctionWorxUserManager _userManager;

        //for feedback importing
        private Dictionary<int, int> _importedUIDs = new Dictionary<int, int>();
        private Dictionary<int, string> _importedUNs = new Dictionary<int, string>();

        #region Constructors

        /// <summary>
        /// This constructor is used by the MVC framework to instantiate the controller using
        /// the default Asp.Net Identity user management components.
        /// </summary>
        public AdminController()
        {
        }

        /// <summary>
        /// This constructor is not used by the MVC framework but is instead provided for ease
        /// of unit testing this type.
        /// </summary>
        /// <param name="userManager">alternate implementation of the AuctionWorxUserManager type, which inherits Microsoft.AspNet.Identity.UserManager&lt;TUser, TKey&gt;</param>
        public AdminController(AuctionWorxUserManager userManager)
        {
            UserManager = userManager;
        }

        /// <summary>
        /// Get/Set and instance of the user manager
        /// </summary>
        public AuctionWorxUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<AuctionWorxUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        #endregion

        #region Languages

        /// <summary>
        /// Processes request to edit culture properties
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CultureManagement()
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {
                    //Save
                    //List<CustomProperty> propsToUpdate =
                    //    SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.SiteCulture).ToList();
                    //propsToUpdate[0].Value = Request[Strings.Fields.DefaultCulture];
                    //SiteClient.UpdateSettings(User.Identity.Name, propsToUpdate);
                    string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
                    SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.SiteCulture, Request[Strings.Fields.DefaultCulture], cultureCode);
                    SiteClient.Reset();
                    PrepareSuccessMessage("CultureManagement", MessageType.Method);
                }
            }
            catch
            {
                PrepareErrorMessage("CultureManagement", MessageType.Method);
            }
            return View();
        }

        /// <summary>
        /// Processes request to edit language properties
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult LanguageManagement()
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {
                    //Save                    
                    SiteClient.SetLanguages(User.Identity.Name, Request[Strings.Fields.Languages]);
                    SiteClient.Reset();
                    PrepareSuccessMessage("LanguageManagement", MessageType.Method);
                }
            }
            catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                //TODO mostly deprecated by FunctionResult
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                //TODO mostly deprecated by FunctionResult?
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
            }

            return View();
        }

        /// <summary>
        /// Processes request to enable/disable one or more countries
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CountryManagement()
        {
            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var DisabledCountryIds = new List<int>();
                    if (!string.IsNullOrEmpty(Request["DisabledCountryIds"]))
                    {
                        foreach (string possibleCountryId in Request["DisabledCountryIds"].Split(','))
                        {
                            int countryId;
                            if (int.TryParse(possibleCountryId, out countryId))
                            {
                                DisabledCountryIds.Add(countryId);
                            }
                        }
                    }
                    foreach (var enabledCountry in SiteClient.Countries.Where(c => c.Enabled))
                    {
                        if (DisabledCountryIds.Any(cid => cid == enabledCountry.ID))
                        {
                            SiteClient.SetCountryEnabled(User.Identity.Name, enabledCountry.ID, false);
                        }
                    }
                    foreach (var disabledCountry in SiteClient.Countries.Where(c => !c.Enabled))
                    {
                        if (!DisabledCountryIds.Any(cid => cid == disabledCountry.ID))
                        {
                            SiteClient.SetCountryEnabled(User.Identity.Name, disabledCountry.ID, true);
                        }
                    }
                    PrepareSuccessMessage("CountryManagement", MessageType.Method);
                    this.FlushCountriesAndStates();
                }
                catch (Exception e)
                {
                    PrepareErrorMessage("CountryManagement", e);
                }
            }
            return View();
        }

        /// <summary>
        /// Displays Form/Processes request to view/add/edit/delete states/regions for the specified country
        /// </summary>
        /// <param name="id">the integer id of the specified country</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult StatesRegionsManagement(int id)
        {
            if (!SiteClient.Countries.Any(c => c.ID == id && c.Enabled))
            {
                PrepareErrorMessage("SelectEnabledCountry", MessageType.Message);
                return RedirectToAction(Strings.MVC.CountryManagementAction);
            }
            ViewData["SelectedCountryName"] = this.LocalizeCountry(SiteClient.Countries.First(c => c.ID == id).Name);
            List<State> statesForSelectedCountry;
            statesForSelectedCountry = SiteClient.States.Where(s => (int)s.CountryID == id && s.Enabled).ToList();
            ViewData["id"] = id;
            return View(statesForSelectedCountry);
        }

        /// <summary>
        /// Processes request to add one or more new states/regions to the specifeid country
        /// </summary>
        /// <param name="id">the integer id of the specified country</param>
        /// <returns>Redirect to "StatesRegionsManagement" on success</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddNewStatesRegions(int id)
        {
            int addedCount = 0;
            int updatedCount = 0;

            //IN (populate UserInput and prepare ModelState for output)                        
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
            input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });
            try
            {
                //convert user input into a list of states with the "Code" and "Name" properties populated
                var newStatesToAdd = SiteClient.ParseNewStateData(User.Identity.Name, input);

                //get all existing states for the specified country, enabled and disabled
                var statesForSelectedCountry = SiteClient.States.Where(s => s.CountryID == id).ToList();
                foreach (var state in newStatesToAdd)
                {
                    if (state.ID > 0)
                    {
                        SiteClient.UpdateState(User.Identity.Name, state);
                        updatedCount++;
                    }
                    else
                    {
                        var newState = state.Clone();
                        SiteClient.AddState(User.Identity.Name, newState);
                        state.ID = newState.ID;
                        addedCount++;
                    }
                }
                PrepareSuccessMessage("AddNewStatesRegions", MessageType.Method);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                var selectedCountry = SiteClient.Countries.FirstOrDefault(c => c.ID == id);
                if (selectedCountry != null)
                {
                    ViewData["SelectedCountryName"] = selectedCountry.Name;
                }
                return View(Strings.MVC.StatesRegionsManagementAction, SiteClient.States.Where(s => (int)s.CountryID == id && s.Enabled).ToList());
            }
            catch (Exception)
            {
                PrepareErrorMessage("AddNewStatesRegions", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.StatesRegionsManagementAction, new { id });
        }

        /// <summary>
        /// Processes request to add one or more new states/regions to the specifeid country
        /// </summary>
        /// <returns>Redirect to "StatesRegionsManagement"</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteStatesRegions()
        {
            var stateIdsToDisable = new List<int>();
            int? returnCountryId = null;
            if (!string.IsNullOrEmpty(Request["StateID"]))
            {
                foreach (string possibleCountryId in Request["StateID"].Split(','))
                {
                    int stateId;
                    if (int.TryParse(possibleCountryId, out stateId))
                    {
                        stateIdsToDisable.Add(stateId);
                    }
                }
                if (stateIdsToDisable.Count > 0)
                {
                    if (SiteClient.States.Any(s => s.ID == stateIdsToDisable.First()))
                    {
                        returnCountryId = SiteClient.States.First(s => s.ID == stateIdsToDisable.First()).CountryID;
                    }
                    foreach (int stateId in stateIdsToDisable)
                    {
                        SiteClient.SetStateEnabled(User.Identity.Name, stateId, false);
                    }
                    PrepareSuccessMessage("DeleteStatesRegions", MessageType.Method);
                }
            }
            return RedirectToAction(Strings.MVC.StatesRegionsManagementAction, new { id = returnCountryId });
        }

        /// <summary>
        /// Processes request to update the code and/or name of the specified state/region
        /// </summary>
        /// <param name="id">integer id of the specified state/region</param>
        /// <param name="code">new code for the specified state/region -- blank value ignored</param>
        /// <param name="name">new name for the specified state/region -- blank value ignored</param>
        /// <returns>Redirect to "StatesRegionsManagement"</returns>
        [Authorize(Roles = Roles.Admin)]
        public string UpdateStateRegion(int id, string code, string name)
        {
            if (SiteClient.States.Any(s => s.ID == id))
            {
                var stateToUpdate = SiteClient.States.First(s => s.ID == id);
                code = code.Trim();
                name = name.Trim();
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name))
                {
                    stateToUpdate.Code = code.ToUpper();
                    stateToUpdate.Name = name;
                    try
                    {
                        //IN (populate UserInput and prepare ModelState for output)                        
                        string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
                        UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
                        input.Items.Add("id", stateToUpdate.CountryID.ToString());
                        input.Items.Add("NewStates", code + "," + name);
                        SiteClient.ParseNewStateData(User.Identity.Name, input);

                        SiteClient.UpdateState(User.Identity.Name, stateToUpdate);
                        return "OK";
                    }
                    catch (FaultException<ValidationFaultContract> vfc)
                    {
                        //display validation errors
                        string errorMessage = string.Empty;
                        foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                        {
                            errorMessage += this.ResourceString("Validation," + issue.Message) + " ";
                        }
                        return errorMessage;
                    }
                    catch (Exception e)
                    {
                        return "ERROR: " + e.Message;
                    }
                }
                else
                {
                    return "ERROR: Value required";
                }
            }
            return "ERROR: Not found";
        }

        /// <summary>
        /// Sets the specified country as the site default country
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetDefaultCountry(int id)
        {
            try
            {
                string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
                SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.SiteDefaultCountry, id.ToString(), cultureCode);
                SiteClient.Reset();
                PrepareSuccessMessage(Strings.MVC.SetDefaultCountryAction, MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage(Strings.MVC.SetDefaultCountryAction, MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CountryManagementAction);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Processes request to verify basic site functionality
        /// </summary>
        /// <returns>View(List&lt;SiteCheck&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CheckSite()
        {
            List<SiteCheck> checks = new List<SiteCheck>();

            System.Xml.XmlDocument doc = null;

            try
            {
                doc = new XmlDocument();
                doc.Load(Server.MapPath(@"..\web.config"));
            }
            catch (Exception e)
            {
                SiteCheck error = new SiteCheck("Loading web.config");
                error.AddCheckItem(new CheckItem("Error Loading web.config", "Error loading web.config", ResultDisposition.Fail, e.ToString()));
                checks.Add(error);
            }

            //Check Licence File Exists
            SiteCheck licence = new SiteCheck("License");

            //--- Valid?
            try
            {
                LicenseInfo info = ListingClient.GetLicenseInfo();
                if (info.Valid)
                {
                    licence.AddCheckItem(new CheckItem("License Key Valid",
                                                       "Checking to see if the license key is valid.",
                                                       ResultDisposition.Pass));
                }
                else
                {
                    licence.AddCheckItem(new CheckItem("Licence Key Valid",
                                                       "Checking to see if the license key is valid.",
                                                       ResultDisposition.Fail,
                                                       "License result is [" + info.Reason +
                                                       "].  Contact RainWorx for more information."));
                }
            }
            catch (Exception e)
            {
                licence.AddCheckItem(new CheckItem("Error Checking Site License", "Error checking site license", ResultDisposition.Fail, e.ToString()));
            }

            checks.Add(licence);

            //Check Listing Image Save/Load
            SiteCheck listingImageSaveLoad = new SiteCheck("Listing Image Save/Load");

            string ListingURI = string.Empty;
            string ListingURI_ThumbCrop = string.Empty;
            string ListingURI_ThumbFit = string.Empty;
            string listingguid = Guid.NewGuid().ToString("D");
            try
            {
                // --- Save                                            
                try
                {
                    string context = Strings.MediaUploadContexts.UploadListingImage;

                    //Get workflow for uploading an image
                    Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                                                              context);

                    //Generate the media object
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
                    Media newImage = mediaGenerator.Generate(generatorProviderSettings,
                                                             DiskFileStore.CreateTempImageAsStream(listingguid));
                    newImage.Context = context;

                    //Save the media    
                    newImage.Saver = workflowParams["Saver"];
                    IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
                    Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(
                        mediaSaver.TypeName,
                        context);
                    if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                    {
                        saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                    }
                    mediaSaver.Save(saverProviderSettings, newImage);

                    //Load the media (for thumbnail preview)
                    newImage.Loader = workflowParams["Loader"];
                    IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
                    Dictionary<string, string> loaderProviderSettings =
                        CommonClient.GetAttributeData(mediaLoader.TypeName,
                                                      context);
                    ListingURI = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);
                    ListingURI_ThumbCrop = mediaLoader.Load(loaderProviderSettings, newImage, "ThumbFit");
                    ListingURI_ThumbFit = mediaLoader.Load(loaderProviderSettings, newImage, "ThumbCrop");
                    listingImageSaveLoad.AddCheckItem(new CheckItem("Save Listing Image",
                                                                    "Saving listing image \"" + ListingURI + "\".",
                                                                    ResultDisposition.Pass));
                }
                catch
                {
                    listingImageSaveLoad.AddCheckItem(new CheckItem("Save Listing Image",
                                                                    "Saving listing image \"" + ListingURI + "\".",
                                                                    ResultDisposition.Fail,
                                                                    "Check \"UploadListingImage\" Media Asset Provider workflow."));
                }
            }
            catch (Exception e)
            {
                listingImageSaveLoad.AddCheckItem(new CheckItem("Error Saving Listing Image", "Error saving listing image", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                //Load                
                try
                {
                    if (DiskFileStore.ReadTempImage(SiteClient.Settings[SiteProperties.URL] + "/" + ListingURI) != "Format24bppRgb")
                    {
                        listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image",
                                                                        "Loading listing image \"" + ListingURI + "\".",
                                                                        ResultDisposition.Fail,
                                                                        "Check \"UploadListingImage\" Media Asset Provider workflow."));
                    }
                    else
                    {
                        listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image",
                                                                        "Loading listing image \"" + ListingURI + "\".",
                                                                        ResultDisposition.Pass));
                    }
                }
                catch
                {
                    try
                    {
                        if (DiskFileStore.ReadTempImage(ListingURI) != "Format24bppRgb")
                        {
                            listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image",
                                                                            "Loading listing image \"" + ListingURI +
                                                                            "\".", ResultDisposition.Fail,
                                                                            "Check \"UploadListingImage\" Media Asset Provider workflow."));
                        }
                        else
                        {
                            listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image",
                                                                            "Loading listing image \"" + ListingURI +
                                                                            "\".", ResultDisposition.Pass));
                        }
                    }
                    catch
                    {
                        listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image",
                                                                        "Loading listing image \"" + ListingURI + "\".",
                                                                        ResultDisposition.Warning,
                                                                        "Check \"UploadListingImage\" Media Asset Provider workflow.  Additionally, the failure to load the image may only indicate that the web server itself cannot load the image based on hostname, etc..."));
                    }
                }
            }
            catch (Exception e)
            {
                listingImageSaveLoad.AddCheckItem(new CheckItem("Error Loading Listing Image", "Error loading listing image", ResultDisposition.Fail, e.ToString()));
            }

            // --- View
            listingImageSaveLoad.AddCheckItem(new CheckItem("View Listing Image", "Loading listing image \"" + ListingURI + "\".", ResultDisposition.Inconclusive, "If this text \"<span style=\"font-family:Monospace;font-size:16px\">" + listingguid + "</span>\" is contained in the following images, listing image saves/loads are working properly! <br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + ListingURI + "\" /><br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + ListingURI_ThumbCrop + "\" /><br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + ListingURI_ThumbFit + "\" /><br/>"));

            checks.Add(listingImageSaveLoad);

            //// --- Save            
            //string guid = Guid.NewGuid().ToString("D");
            //try
            //{
            //    DiskFileStore.WriteTempImage(guid + ".png", guid + " rendered @ " + DateTime.Now.ToString());
            //    listingImageSaveLoad.AddCheckItem(new CheckItem("Save Listing Image", "Saving listing image \"" + guid + ".png" + "\" to " + DiskFileStore._uploadsFolder + ".", ResultDisposition.Pass));
            //}
            //catch
            //{
            //    listingImageSaveLoad.AddCheckItem(new CheckItem("Save Listing Image", "Saving listing image \"" + guid + ".png" + "\" to " + DiskFileStore._uploadsFolder + ".", ResultDisposition.Fail, "Check \"ImageSavePath\" in web.config and its permissions (write)."));
            //}

            //// --- Load
            //try
            //{
            //    string loadUrl;
            //    if (ConfigurationManager.AppSettings["ImageLoadURI"].StartsWith("http"))
            //    {
            //        loadUrl = ConfigurationManager.AppSettings["ImageLoadURI"];
            //    }
            //    else
            //    {
            //        loadUrl = SiteClient.Settings[SiteProperties.URL] + "/" + ConfigurationManager.AppSettings["ImageLoadURI"];
            //    }
            //    if (DiskFileStore.ReadTempImage(loadUrl + guid + ".png") != "Format24bppRgb")
            //    {
            //        listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image", "Loading listing image \"" + guid + ".png\" from \"" + ConfigurationManager.AppSettings["ImageLoadURI"] + guid + ".png\".", ResultDisposition.Fail, "Check \"ImageLoadURI\" in web.config and its permissions (read).  Additionally, the image  may have loaded, but not as a valid image file."));
            //    }
            //    else
            //    {
            //        listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image", "Loading listing image \"" + guid + ".png\" from \"" + ConfigurationManager.AppSettings["ImageLoadURI"] + guid + ".png\".", ResultDisposition.Pass));
            //    }
            //}
            //catch
            //{
            //    listingImageSaveLoad.AddCheckItem(new CheckItem("Load Listing Image", "Loading listing image \"" + guid + ".png\" from \"" + ConfigurationManager.AppSettings["ImageLoadURI"] + guid + ".png\".", ResultDisposition.Warning, "Check \"ImageLoadURI\" in web.config and its permissions (read).  Additionally, the failure to load the image may only indicate that the web server itself cannot load the image based on hostname, etc..."));
            //}

            //// --- View
            //listingImageSaveLoad.AddCheckItem(new CheckItem("View Listing Image", "Displaying listing image \"" + guid + ".png\" from \"" + ConfigurationManager.AppSettings["ImageLoadURI"] + guid + ".png\".", ResultDisposition.Inconclusive, "If this text \"<span style=\"font-family:Monospace;font-size:16px\">" + guid + "</span>\" is contained in the following image, listing image saves/loads are working properly! <img style=\"border:1px solid Black;padding:4px;\" src=\"" + ConfigurationManager.AppSettings["ImageLoadURI"] + guid + ".png\" />"));

            //checks.Add(listingImageSaveLoad);

            //****************New Logo Save/Load

            ////Check Logo Image Save/Load
            SiteCheck logoImageSaveLoad = new SiteCheck("Logo Image Save/Load");

            string logoguid = Guid.NewGuid().ToString("D");
            string logoURI = string.Empty;
            try
            {
                // --- Save                            
                try
                {
                    string context = Strings.MediaUploadContexts.UploadSiteLogo;

                    //Get workflow for uploading an image
                    Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                                                              context);

                    //Generate the media object
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
                    Media newImage = mediaGenerator.Generate(generatorProviderSettings,
                                                             DiskFileStore.CreateTempImageAsStream(logoguid));
                    newImage.Context = context;

                    //Save the media    
                    newImage.Saver = workflowParams["Saver"];
                    IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
                    Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(
                        mediaSaver.TypeName,
                        context);
                    if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                    {
                        saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                    }
                    mediaSaver.Save(saverProviderSettings, newImage);

                    //Load the media (for thumbnail preview)
                    newImage.Loader = workflowParams["Loader"];
                    IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
                    Dictionary<string, string> loaderProviderSettings =
                        CommonClient.GetAttributeData(mediaLoader.TypeName,
                                                      context);
                    logoURI = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);
                    logoImageSaveLoad.AddCheckItem(new CheckItem("Save Logo Image",
                                                                 "Saving logo image \"" + logoURI + "\".",
                                                                 ResultDisposition.Pass));
                }
                catch
                {
                    logoImageSaveLoad.AddCheckItem(new CheckItem("Save Logo Image",
                                                                 "Saving logo image \"" + logoURI + "\".",
                                                                 ResultDisposition.Fail,
                                                                 "Check \"UploadSiteLogo\" Media Asset Provider workflow."));
                }
            }
            catch (Exception e)
            {
                logoImageSaveLoad.AddCheckItem(new CheckItem("Error Saving Logo Image", "Error saving logo image", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                //Load
                try
                {
                    if (DiskFileStore.ReadTempImage(SiteClient.Settings[SiteProperties.URL] + "/" + logoURI) != "Format24bppRgb")
                    {
                        logoImageSaveLoad.AddCheckItem(new CheckItem("Load Logo Image",
                                                                     "Loading logo image \"" + logoURI + "\".",
                                                                     ResultDisposition.Fail,
                                                                     "Check \"UploadSiteLogo\" Media Asset Provider workflow."));
                    }
                    else
                    {
                        logoImageSaveLoad.AddCheckItem(new CheckItem("Load Logo Image",
                                                                     "Loading logo image \"" + logoURI + "\".",
                                                                     ResultDisposition.Pass));
                    }
                }
                catch
                {
                    try
                    {
                        if (DiskFileStore.ReadTempImage(logoURI) != "Format24bppRgb")
                        {
                            logoImageSaveLoad.AddCheckItem(new CheckItem("Load Logo Image",
                                                                         "Loading logo image \"" + logoURI + "\".",
                                                                         ResultDisposition.Fail,
                                                                         "Check \"UploadSiteLogo\" Media Asset Provider workflow."));
                        }
                        else
                        {
                            logoImageSaveLoad.AddCheckItem(new CheckItem("Load Logo Image",
                                                                         "Loading logo image \"" + logoURI + "\".",
                                                                         ResultDisposition.Pass));
                        }
                    }
                    catch
                    {
                        logoImageSaveLoad.AddCheckItem(new CheckItem("Load Logo Image",
                                                                     "Loading logo image \"" + logoURI + "\".",
                                                                     ResultDisposition.Warning,
                                                                     "Check \"UploadSiteLogo\" Media Asset Provider workflow.  Additionally, the failure to load the image may only indicate that the web server itself cannot load the image based on hostname, etc..."));
                    }
                }
            }
            catch (Exception e)
            {
                logoImageSaveLoad.AddCheckItem(new CheckItem("Error Loading Logo Image", "Error loading logo image", ResultDisposition.Fail, e.ToString()));
            }

            // --- View
            logoImageSaveLoad.AddCheckItem(new CheckItem("View Logo Image", "Loading logo image \"" + logoURI + "\".", ResultDisposition.Inconclusive, "If this text \"<span style=\"font-family:Monospace;font-size:16px\">" + logoguid + "</span>\" is contained in the following image, logo image saves/loads are working properly! <br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + logoURI + "\" />"));

            checks.Add(logoImageSaveLoad);

            //****************End Logo Save/Load

            //*** Begin Banner Checks

            ////Check Logo Image Save/Load
            SiteCheck bannerImageSaveLoad = new SiteCheck("Banner Image Save/Load");

            string bannerGuid = Guid.NewGuid().ToString("D");
            string bannerURI = string.Empty;
            try
            {
                // --- Save                            
                try
                {
                    string context = Strings.MediaUploadContexts.UploadBannerImage;

                    //Get workflow for uploading an image
                    Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                                                              context);

                    //Generate the media object
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
                    Media newImage = mediaGenerator.Generate(generatorProviderSettings,
                                                             DiskFileStore.CreateTempImageAsStream(bannerGuid));
                    newImage.Context = context;

                    //Save the media    
                    newImage.Saver = workflowParams["Saver"];
                    IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
                    Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(
                        mediaSaver.TypeName,
                        context);
                    if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                    {
                        saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                    }
                    mediaSaver.Save(saverProviderSettings, newImage);

                    //Load the media (for thumbnail preview)
                    newImage.Loader = workflowParams["Loader"];
                    IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
                    Dictionary<string, string> loaderProviderSettings =
                        CommonClient.GetAttributeData(mediaLoader.TypeName,
                                                      context);
                    bannerURI = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);
                    bannerImageSaveLoad.AddCheckItem(new CheckItem("Save Banner Image",
                                                                   "Saving banner image \"" + bannerURI + "\".",
                                                                   ResultDisposition.Pass));
                }
                catch
                {
                    bannerImageSaveLoad.AddCheckItem(new CheckItem("Save Banner Image",
                                                                   "Saving banner image \"" + bannerURI + "\".",
                                                                   ResultDisposition.Fail,
                                                                   "Check \"UploadBannerImage\" Media Asset Provider workflow."));
                }
            }
            catch (Exception e)
            {
                bannerImageSaveLoad.AddCheckItem(new CheckItem("Error Saving Banner Image", "Error saving banner image", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                //Load
                try
                {
                    if (DiskFileStore.ReadTempImage(SiteClient.Settings[SiteProperties.URL] + "/" + bannerURI) != "Format24bppRgb")
                    {
                        bannerImageSaveLoad.AddCheckItem(new CheckItem("Load Banner Image",
                                                                       "Loading banner image \"" + bannerURI + "\".",
                                                                       ResultDisposition.Fail,
                                                                       "Check \"UploadBannerImage\" Media Asset Provider workflow."));
                    }
                    else
                    {
                        bannerImageSaveLoad.AddCheckItem(new CheckItem("Load Banner Image",
                                                                       "Loading banner image \"" + bannerURI + "\".",
                                                                       ResultDisposition.Pass));
                    }
                }
                catch
                {
                    try
                    {
                        if (DiskFileStore.ReadTempImage(bannerURI) != "Format24bppRgb")
                        {
                            bannerImageSaveLoad.AddCheckItem(new CheckItem("Load Banner Image",
                                                                           "Loading banner image \"" + bannerURI + "\".",
                                                                           ResultDisposition.Fail,
                                                                           "Check \"UploadBannerImage\" Media Asset Provider workflow."));
                        }
                        else
                        {
                            bannerImageSaveLoad.AddCheckItem(new CheckItem("Load Banner Image",
                                                                           "Loading banner image \"" + bannerURI + "\".",
                                                                           ResultDisposition.Pass));
                        }
                    }
                    catch
                    {
                        bannerImageSaveLoad.AddCheckItem(new CheckItem("Load Banner Image",
                                                                       "Loading banner image \"" + bannerURI + "\".",
                                                                       ResultDisposition.Warning,
                                                                       "Check \"UploadBannerImage\" Media Asset Provider workflow.  Additionally, the failure to load the image may only indicate that the web server itself cannot load the image based on hostname, etc..."));
                    }
                }
            }
            catch (Exception e)
            {
                bannerImageSaveLoad.AddCheckItem(new CheckItem("Error Loading Banner Image", "Error loading banner image", ResultDisposition.Fail, e.ToString()));
            }

            // --- View
            bannerImageSaveLoad.AddCheckItem(new CheckItem("View Banner Image", "Loading banner image \"" + bannerURI + "\".", ResultDisposition.Inconclusive, "If this text \"<span style=\"font-family:Monospace;font-size:16px\">" + bannerGuid + "</span>\" is contained in the following image, banner image saves/loads are working properly! <br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + bannerURI + "\" />"));

            checks.Add(bannerImageSaveLoad);

            //***End Banner Checks

            //*** Begin Event Image Checks

            //Check Event Image Save/Load
            SiteCheck eventImageSaveLoad = new SiteCheck("Event Image Save/Load");

            string eventImageGuid = Guid.NewGuid().ToString("D");
            string eventImageURI = string.Empty;
            try
            {
                // --- Save                            
                try
                {
                    string context = Strings.MediaUploadContexts.UploadEventImage;

                    //Get workflow for uploading an image
                    Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow",
                                                                                              context);

                    //Generate the media object
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
                    Media newImage = mediaGenerator.Generate(generatorProviderSettings,
                                                             DiskFileStore.CreateTempImageAsStream(eventImageGuid));
                    newImage.Context = context;

                    //Save the media    
                    newImage.Saver = workflowParams["Saver"];
                    IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newImage.Saver);
                    Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(
                        mediaSaver.TypeName,
                        context);
                    if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                    {
                        saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                    }
                    mediaSaver.Save(saverProviderSettings, newImage);

                    //Load the media (for thumbnail preview)
                    newImage.Loader = workflowParams["Loader"];
                    IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newImage.Loader);
                    Dictionary<string, string> loaderProviderSettings =
                        CommonClient.GetAttributeData(mediaLoader.TypeName,
                                                      context);
                    eventImageURI = mediaLoader.Load(loaderProviderSettings, newImage, newImage.DefaultVariationName);
                    eventImageSaveLoad.AddCheckItem(new CheckItem("Save Event Image",
                                                                   "Saving Event image \"" + eventImageURI + "\".",
                                                                   ResultDisposition.Pass));
                }
                catch
                {
                    eventImageSaveLoad.AddCheckItem(new CheckItem("Save Event Image",
                                                                   "Saving Event image \"" + eventImageURI + "\".",
                                                                   ResultDisposition.Fail,
                                                                   "Check \"UploadEventImage\" Media Asset Provider workflow."));
                }
            }
            catch (Exception e)
            {
                eventImageSaveLoad.AddCheckItem(new CheckItem("Error Saving Event Image", "Error saving event image", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                //Load
                try
                {
                    if (DiskFileStore.ReadTempImage(SiteClient.Settings[SiteProperties.URL] + "/" + eventImageURI) != "Format24bppRgb")
                    {
                        eventImageSaveLoad.AddCheckItem(new CheckItem("Load Event Image",
                                                                       "Loading event image \"" + eventImageURI + "\".",
                                                                       ResultDisposition.Fail,
                                                                       "Check \"UploadEventImage\" Media Asset Provider workflow."));
                    }
                    else
                    {
                        eventImageSaveLoad.AddCheckItem(new CheckItem("Load Event Image",
                                                                       "Loading event image \"" + eventImageURI + "\".",
                                                                       ResultDisposition.Pass));
                    }
                }
                catch
                {
                    try
                    {
                        if (DiskFileStore.ReadTempImage(eventImageURI) != "Format24bppRgb")
                        {
                            eventImageSaveLoad.AddCheckItem(new CheckItem("Load Event Image",
                                                                           "Loading event image \"" + eventImageURI + "\".",
                                                                           ResultDisposition.Fail,
                                                                           "Check \"UploadEventImage\" Media Asset Provider workflow."));
                        }
                        else
                        {
                            eventImageSaveLoad.AddCheckItem(new CheckItem("Load Event Image",
                                                                           "Loading event image \"" + eventImageURI + "\".",
                                                                           ResultDisposition.Pass));
                        }
                    }
                    catch
                    {
                        eventImageSaveLoad.AddCheckItem(new CheckItem("Load Event Image",
                                                                       "Loading event image \"" + eventImageURI + "\".",
                                                                       ResultDisposition.Warning,
                                                                       "Check \"UploadEventImage\" Media Asset Provider workflow.  Additionally, the failure to load the image may only indicate that the web server itself cannot load the image based on hostname, etc..."));
                    }
                }
            }
            catch (Exception e)
            {
                eventImageSaveLoad.AddCheckItem(new CheckItem("Error Loading Event Image", "Error event banner image", ResultDisposition.Fail, e.ToString()));
            }

            // --- View
            eventImageSaveLoad.AddCheckItem(new CheckItem("View Event Image", "Loading event image \"" + eventImageURI + "\".", ResultDisposition.Inconclusive, "If this text \"<span style=\"font-family:Monospace;font-size:16px\">" + eventImageGuid + "</span>\" is contained in the following image, banner image saves/loads are working properly! <br/><img style=\"border:1px solid Black;padding:4px;\" src=\"" + eventImageURI + "\" />"));

            checks.Add(eventImageSaveLoad);

            //***End Banner Checks

            ////Check Templates
            //SiteCheck templates = new SiteCheck("Email Templates");

            //string templatesFolder = string.Empty;
            //try
            //{
            //    //--- Exists?
            //    templatesFolder =
            //        doc.SelectSingleNode(
            //            "//setting[@name='NotificationTemplatesFolder']/value")
            //            .InnerText;

            //    //exists?
            //    if (Directory.Exists(templatesFolder))
            //    {
            //        templates.AddCheckItem(new CheckItem("Templates Folder Exists",
            //                                             "Checking to see if the Templates folder, \"" + templatesFolder +
            //                                             ",\" exists.", ResultDisposition.Pass));
            //    }
            //    else
            //    {
            //        templates.AddCheckItem(new CheckItem("Templates Folder Exists",
            //                                             "Checking to see if the Templates folder, \"" + templatesFolder +
            //                                             ",\" exists.", ResultDisposition.Fail,
            //                                             "Check \"NotificationTemplatesFolder\" in web.config and its permissions (read)."));
            //    }
            //}
            //catch (Exception e)
            //{
            //    templates.AddCheckItem(new CheckItem("Error Checking Templates Folder Exists", "Error checking templates folder exists", ResultDisposition.Fail, e.ToString()));
            //}

            //try
            //{
            //    //can read?
            //    try
            //    {
            //        FileStream fs = System.IO.File.OpenRead(Path.Combine(templatesFolder, "user_verification_body-en"));
            //        fs.Close();
            //        templates.AddCheckItem(new CheckItem("Load Template Files",
            //                                             "Checking to see if the email templates can be loaded from \"" +
            //                                             templatesFolder + ".\"", ResultDisposition.Pass));
            //    }
            //    catch (Exception e)
            //    {
            //        templates.AddCheckItem(new CheckItem("Load Template Files",
            //                                             "Checking to see if the email templates can be loaded from \"" +
            //                                             templatesFolder + ".\"", ResultDisposition.Fail,
            //                                             "Check \"NotificationTemplatesFolder\" in web.config and its permissions (read)."));
            //    }
            //}
            //catch (Exception e)
            //{
            //    templates.AddCheckItem(new CheckItem("Error Loading Template Files", "Error loading template files", ResultDisposition.Fail, e.ToString()));
            //}

            //checks.Add(templates);

            //Check Emails
            SiteCheck emails = new SiteCheck("Outbound Email");

            string emailhost = string.Empty;

            try
            {
                emailhost =
                    doc.SelectSingleNode(
                        "//system.net/mailSettings/smtp/network").Attributes["host"].Value;

                var mailMessage = Utilities.NewMailMessage("checksite");
                mailMessage.From = new System.Net.Mail.MailAddress("checksite@rainworx.com");
                mailMessage.To.Add(new System.Net.Mail.MailAddress("checksite@rainworx.com"));
                mailMessage.Subject = "AuctionWorx: Check Site Email";
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("AuctionWorx: Check Site Email");
                foreach (KeyValuePair<string, string> pair in SiteClient.Settings)
                {
                    bool includeProp = false;
                    switch (pair.Key)
                    {
                        case SiteProperties.SiteTitle:
                        case SiteProperties.FriendlySiteName:
                        case SiteProperties.LegalSiteName:
                        case SiteProperties.AdministratorEmail:
                        case SiteProperties.URL:
                        case SiteProperties.SecureURL:
                        case SiteProperties.SystemEmailAddress:
                            includeProp = true;
                            break;
                    }
                    if (includeProp)
                    {
                        sb.AppendLine(pair.Key + ": " + pair.Value);
                    }
                }
                mailMessage.Body = sb.ToString();
                var client = new System.Net.Mail.SmtpClient();
                client.Send(mailMessage);
                emails.AddCheckItem(new CheckItem("Send Email",
                                                         "Checking to see if an email can be sent through the configured mail server \"" +
                                                         emailhost + ".\"", ResultDisposition.Pass));
            }
            catch (Exception e)
            {
                emails.AddCheckItem(new CheckItem("Send Email", "Checking to see if an email can be sent through the configured mail server \"" +
                                                         emailhost + ".\"", ResultDisposition.Fail, e.ToString()));
            }

            checks.Add(emails);

            SiteCheck connectionString = new SiteCheck("Database Connection String");
            string connectionStringSetting = string.Empty;
            try
            {
                //Check Connection String                
                connectionStringSetting = System.Configuration.ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

                //test open
                try
                {
                    SqlConnection conn = new SqlConnection(connectionStringSetting);
                    conn.Open();
                    conn.Close();
                    connectionString.AddCheckItem(new CheckItem("Open SQL Database",
                                                                "Checking to see if the SQL database can be opened.",
                                                                ResultDisposition.Pass));
                }
                catch (InvalidOperationException)
                {
                    connectionString.AddCheckItem(new CheckItem("Open SQL Database",
                                                                "Checking to see if the SQL database can be opened.",
                                                                ResultDisposition.Fail,
                                                                "Cannot open a connection without specifying a data source or server."));
                }
                catch (SqlException sqe)
                {
                    if (sqe.Number == 18487 || sqe.Number == 18488)
                    {
                        connectionString.AddCheckItem(new CheckItem("Open SQL Database",
                                                                    "Checking to see if the SQL database can be opened.",
                                                                    ResultDisposition.Fail,
                                                                    "The specified password has expired or must be reset."));
                    }
                    else
                    {
                        connectionString.AddCheckItem(new CheckItem("Open SQL Database",
                                                                    "Checking to see if the SQL database can be opened.",
                                                                    ResultDisposition.Fail,
                                                                    "A connection-level error occurred while opening the connection."));
                    }
                }
                catch
                {
                    connectionString.AddCheckItem(new CheckItem("Open SQL Database",
                                                                "Checking to see if the SQL database can be opened.",
                                                                ResultDisposition.Fail,
                                                                "Couldn't open the SQL Database for unknown reasons.  Check SQL permissions (db owner), network connectivity, and \"SQL authentication\" (Windows Mode vs. Mixed Mode)."));
                }
            }
            catch (Exception e)
            {
                connectionString.AddCheckItem(new CheckItem("Error Opening SQL Database", "Error opening sql database", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                //test execute            
                try
                {
                    SqlConnection conn = new SqlConnection(connectionStringSetting);
                    conn.Open();
                    SqlCommand command = new SqlCommand();
                    command.Connection = conn;
                    command.CommandText =
                        "select top 1 ID, DateStamp = CONVERT(varchar, DateStamp), VersionNumber, Status from rwx_version order by ID desc";
                    SqlDataReader reader = command.ExecuteReader();
                    reader.Read();

                    StringBuilder sqlResult = new StringBuilder();
                    sqlResult.Append("<table cellpadding=\"3\"><tr><th>ID</th><td>");
                    sqlResult.Append(reader["ID"]);
                    sqlResult.Append("</td></tr><tr><th>DateStamp</th><td>");
                    sqlResult.Append(reader["DateStamp"]);
                    sqlResult.Append("</td></tr><tr><th>Version</th><td>");
                    sqlResult.Append(reader["VersionNumber"]);
                    sqlResult.Append("</td></tr><tr><th>Status</th><td>");
                    sqlResult.Append(reader["Status"]);
                    sqlResult.Append("</td></tr></table>");

                    // Call Close when done reading.
                    reader.Close();
                    conn.Close();

                    connectionString.AddCheckItem(new CheckItem("Execute Query",
                                                                "Checking to see if commands can be executed.",
                                                                ResultDisposition.Pass, sqlResult.ToString()));
                }
                catch
                {
                    connectionString.AddCheckItem(new CheckItem("Execute Query",
                                                                "Checking to see if commands can be executed.",
                                                                ResultDisposition.Fail,
                                                                "Couldn't execute commands for unknown reasons.  Check SQL permissions (db owner, Execute), network connectivity, and \"SQL authentication\" (Windows Mode vs. Mixed Mode)."));
                }
            }
            catch (Exception e)
            {
                connectionString.AddCheckItem(new CheckItem("Error Executing Query", "Error executing query", ResultDisposition.Fail, e.ToString()));
            }

            checks.Add(connectionString);


            var _queueManager = RainWorx.FrameWorx.Unity.UnityResolver.Get<RainWorx.FrameWorx.Queueing.IQueueManager>();
            bool usingSSB = _queueManager.GetType() == typeof(RainWorx.FrameWorx.Queueing.SQLServiceBroker) || _queueManager.GetType() == typeof(RainWorx.FrameWorx.Queueing.SimpleSSB);

            if (usingSSB)
            {
                //Service Broker Start
                SiteCheck sqlSSB = new SiteCheck("SQL Service Broker");
                try
                {
                    //test Broker Enabled            
                    try
                    {
                        SqlConnection conn = new SqlConnection(connectionStringSetting);
                        conn.Open();
                        SqlCommand command = new SqlCommand();
                        command.Connection = conn;
                        command.CommandText =
                            "select is_broker_enabled from sys.databases where database_id = db_id()";
                        SqlDataReader reader = command.ExecuteReader();
                        reader.Read();

                        bool ssbEnabled = (bool)reader[0];

                        // Call Close when done reading.
                        reader.Close();
                        conn.Close();

                        if (ssbEnabled)
                        {
                            sqlSSB.AddCheckItem(new CheckItem("Broker Enabled",
                                "Checking to see if SQL Service Broker is enabled.",
                                ResultDisposition.Pass));
                        }
                        else
                        {
                            sqlSSB.AddCheckItem(new CheckItem("Broker Enabled",
                                "Checking to see if SQL Service Broker is enabled.",
                                ResultDisposition.Fail,
                                "SQL Service Broker is disabled.  Please enable SQL Service Broker."));
                        }
                    }
                    catch
                    {
                        sqlSSB.AddCheckItem(new CheckItem("Broker Enabled",
                                "Checking to see if SQL Service Broker is enabled.",
                                ResultDisposition.Fail,
                                "Couldn't determine if SQL Service Broker is enabled for unknown reasons."));
                    }
                }
                catch (Exception e)
                {
                    sqlSSB.AddCheckItem(new CheckItem("Error Checking Broker Enabled", "Error checking if broker is enabled", ResultDisposition.Fail, e.ToString()));
                }

                try
                {
                    //test Disabled Queues            
                    try
                    {
                        SqlConnection conn = new SqlConnection(connectionStringSetting);
                        conn.Open();
                        SqlCommand command = new SqlCommand();
                        command.Connection = conn;
                        command.CommandText =
                            "select name from sys.service_queues where is_receive_enabled = 0 or is_enqueue_enabled = 0";
                        SqlDataReader reader = command.ExecuteReader();

                        if (reader.Read())
                        {
                            StringBuilder sqlResult = new StringBuilder();
                            sqlResult.Append("<table cellpadding=\"3\"><tr><th>Queue Name</th>");

                            do
                            {
                                sqlResult.Append("<tr><td>");
                                sqlResult.Append(reader["name"]);
                                sqlResult.Append("</td></tr>");
                            } while (reader.Read());

                            sqlResult.Append("</table>");

                            sqlSSB.AddCheckItem(new CheckItem("Queues Disabled",
                                "Checking to see if any SQL Service Broker queues are disabled.",
                                ResultDisposition.Fail, sqlResult.ToString()));
                        }
                        else
                        {
                            sqlSSB.AddCheckItem(new CheckItem("Queues Disabled",
                                "Checking to see if any SQL Service Broker queues are disabled.",
                                ResultDisposition.Pass));
                        }

                        // Call Close when done reading.
                        reader.Close();
                        conn.Close();

                    }
                    catch
                    {
                        sqlSSB.AddCheckItem(new CheckItem("Queues Disabled",
                                "Checking to see if any SQL Service Broker queues are disabled.",
                                ResultDisposition.Fail,
                                "Couldn't determine if SQL Service Broker queues are disabled unknown reasons."));
                    }
                }
                catch (Exception e)
                {
                    sqlSSB.AddCheckItem(new CheckItem("Error Checking Queued Disabled", "Error checking if SQL Service Broker queues are disabled are disabled", ResultDisposition.Fail, e.ToString()));
                }

                checks.Add(sqlSSB);

                //Service Broker End
            }

            //Check logging
            SiteCheck logsFolders = new SiteCheck("Logging");

            try
            {
                LogManager.WriteLog("Executing CheckSite", "CheckSite", Strings.FunctionalAreas.Site, TraceEventType.Information,
                                    User.Identity.Name);

                logsFolders.AddCheckItem(new CheckItem("Write to DB Event Log",
                                                               "Writing to DB Event Log", ResultDisposition.Inconclusive,
                                                               "This result is inconclusive because writing to the DB Event Log happens in a seperate thread."));

            }
            catch (Exception e)
            {
                logsFolders.AddCheckItem(new CheckItem("Error Writing to DB Event Log", "Error writing to DB Event Log", ResultDisposition.Fail, e.ToString()));
            }

            try
            {
                SqlConnection conn = new SqlConnection(connectionStringSetting);
                conn.Open();
                SqlCommand command = new SqlCommand();
                command.Connection = conn;
                command.CommandText =
                    "select top 1 * from RWX_LogEntries order by id desc";
                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows)
                {
                    logsFolders.AddCheckItem(new CheckItem("Read from DB Event Log", "Reading from DB Event Log", ResultDisposition.Pass));
                }
                else
                {
                    logsFolders.AddCheckItem(new CheckItem("Read from DB Event Log", "Reading from DB Event Log", ResultDisposition.Inconclusive, "DB Event Log table doesn't have any records."));
                }

                //Call Close when done reading.
                reader.Close();
                conn.Close();

            }
            catch (Exception e)
            {
                logsFolders.AddCheckItem(new CheckItem("Error Reading from DB Event Log", "Error reading from DB Event Log", ResultDisposition.Fail, e.ToString()));
            }

            checks.Add(logsFolders);

            //**************
            //Check outbound web access
            SiteCheck webAccess = new SiteCheck("Outbound HTTP");

            //--- Valid?
            try
            {
                HttpClient client = new HttpClient();
                var t = client.GetAsync("https://secure.authorize.net")
                .ContinueWith((response) =>
                              {
                                  response.Result.EnsureSuccessStatusCode();
                              });
                t.Wait();

                webAccess.AddCheckItem(new CheckItem("Load https://secure.authorize.net",
                                                       "Checking to see if HTTP requests outside of this domain are possible.",
                                                       ResultDisposition.Pass));
            }
            catch (Exception e)
            {
                webAccess.AddCheckItem(new CheckItem("Error Loading https://secure.authorize.net",
                                                       "Count not access https://secure.authorize.net.  AuctionWorx features that require HTTP requests outside of this domain may not work properly.",
                                                       ResultDisposition.Warning, e.ToString()));
            }

            checks.Add(webAccess);
            //**************

            return View(checks);
        }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        /// <returns>Redirect to /Admin/VersionInfo</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ClearCache()
        {
            CommonClient.ClearEntireCache();
            SiteClient.Reset();
            UserClient.Reset();
            ListingClient.Reset();

            PrepareSuccessMessage("ClearCache", MessageType.Method);

            return RedirectToAction(Strings.MVC.MaintenanceAction);
        }

        /// <summary>
        /// Ends all SQL Service Broker conversations and re-initializes the GetCurrentTime Timer Service
        /// </summary>
        /// <returns>Redirect to /Admin/VersionInfo</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ResetSignalR()
        {
            CommonClient.CleanupSignalRConversations();

            PrepareSuccessMessage("ResetSignalR", MessageType.Method);

            return RedirectToAction(Strings.MVC.MaintenanceAction);
        }

        #endregion

        #region Summary

        /// <summary>
        /// Displays default "Admin" view (normally "Admin > Summary")
        /// </summary>
        /// <returns>Transfer to Summary View</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Index()
        {
            //return new MVCTransferResult(new { controller = "Admin", action = "Summary" }, this.HttpContext);
            return Summary();
        }

        /// <summary>
        /// Displays various site stats (e.g. # of users, # of listings, etc)
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Summary()
        {
            var summaryCounts = SiteClient.GetAdminSummaryCounts(User.Identity.Name);
            var allCounts = new Dictionary<string, int>();

            //TODO: find an alternate way to get this count after replacing Membership Provider with AspNet Identity
            //allCounts.Add("RecentlySignedInUsers", MembershipService.UsersOnlineCount); // this is a count of logged on users who's LastActivityDate is less than 15 minutes old, by default
            foreach (string key in summaryCounts.Keys)
            {
                allCounts.Add(key, summaryCounts[key]);
            }
            
            ViewData[Strings.Fields.AdminSummaryCounts] = allCounts;

            return View(Strings.MVC.SummaryAction);
        }

        #endregion

        #region Logo / FavIcon

        /// <summary>
        /// Displays form to upload site logo
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult LogoUploader()
        {
            return View();
        }

        /// <summary>
        /// Displays form to upload non-mobil site logo
        /// </summary>
        /// <param name="fileName">the name of the file being uploaded</param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult LogoUploader(string fileName)
        {
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.SiteLogoFileName, fileName, cultureCode);
            SiteClient.Reset();

            return View();
        }

        /// <summary>
        /// Displays form to upload a mobile site logo
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult MobileLogoUploader()
        {
            return View();
        }

        /// <summary>
        /// Processes a request upload a mobile site logo
        /// </summary>
        /// <param name="fileName">the name of the file being uploaded</param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult MobileLogoUploader(string fileName)
        {
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.MobileLogoFileName, fileName, cultureCode);
            SiteClient.Reset();

            return View();
        }

        /// <summary>
        /// Displays form to upload a site favicon
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult FavIconUploader()
        {
            return View();
        }

        /// <summary>
        /// Processes a request upload a site favicon
        /// </summary>
        /// <param name="fileName">the name of the file being uploaded</param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost]
        public ActionResult FavIconUploader(string fileName)
        {
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.SiteFavIconFileName, fileName, cultureCode);
            SiteClient.Reset();

            return View();
        }

        #endregion

        #region Field Assignment to Categories

        /// <summary>
        /// Processes request to associate the specified custom listing field and 
        /// the specified listing categories
        /// </summary>
        /// <param name="CustomFieldID">ID of the requested custom listing field</param>
        /// <param name="Categories">List of ID's of the requested categories</param>
        /// <returns>Redirect to /Admin/EditField view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AssignField(int CustomFieldID, string[] Categories)
        {
            if (Categories == null) return RedirectToAction(Strings.MVC.EditFieldAction, new { id = CustomFieldID });

            int[] categoryIds = new int[Categories.Length];
            for (int index = 0; index < Categories.Length; ++index)
            {
                categoryIds[index] = int.Parse(Categories[index]);
            }

            CommonClient.AssignFieldToCategory(User.Identity.Name, CustomFieldID, categoryIds);

            SiteClient.Reset();

            PrepareSuccessMessage("AssignField", MessageType.Method);

            return RedirectToAction(Strings.MVC.EditFieldAction, new { id = CustomFieldID });
        }

        /// <summary>
        /// Processes request to disassociate the specified custom listing field and 
        /// the specified listing categories
        /// </summary>
        /// <param name="CustomFieldID">ID of the requested custom listing field</param>
        /// <param name="Categories">List of ID's of the requested categories</param>
        /// <returns>Redirect to /Admin/EditField view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UnassignField(int CustomFieldID, string[] Categories)
        {
            if (Categories == null) return RedirectToAction(Strings.MVC.EditFieldAction, new { id = CustomFieldID });

            int[] categoryIds = new int[Categories.Length];
            for (int index = 0; index < Categories.Length; ++index)
            {
                categoryIds[index] = int.Parse(Categories[index]);
            }

            CommonClient.UnassignFieldFromCategory(User.Identity.Name, CustomFieldID, categoryIds);

            SiteClient.Reset();

            PrepareSuccessMessage("UnassignField", MessageType.Method);

            return RedirectToAction(Strings.MVC.EditFieldAction, new { id = CustomFieldID });
        }

        /// <summary>
        /// Processes request to associate the specified custom listing field and 
        /// the specified listing category
        /// </summary>
        /// <param name="CustomFieldID">ID of the requested custom listing field</param>
        /// <param name="categoryID">ID of the requested listing category</param>
        /// <param name="Inherit">when true, the assignment will also be applied to descendant categories</param>
        /// <returns>Redirect to /Admin/CategoryDetail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AssignFieldFromCategoryDetail(int CustomFieldID, int categoryID, bool Inherit)
        {
            if (Inherit)
            {
                CommonClient.AssignFieldToCategoryAndDescendants(User.Identity.Name, CustomFieldID, categoryID);
            }
            else
            {
                CommonClient.AssignFieldToCategory(User.Identity.Name, CustomFieldID, new int[] { categoryID });
            }

            //SiteClient.Reset();

            PrepareSuccessMessage("AssignFieldFromCategoryDetail", MessageType.Method);

            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID = categoryID });
        }

        /// <summary>
        /// Processes request to disassociate the specified custom listing field and 
        /// the specified listing category
        /// </summary>
        /// <param name="CustomFieldID">ID of the requested custom listing field</param>
        /// <param name="categoryID">ID of the requested listing category</param>
        /// <param name="Inherit">when true, the assignment will also be applied to descendant categories</param>
        /// <returns>Redirect to /Admin/CategoryDetail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UnassignFieldFromCategoryDetail(int CustomFieldID, int categoryID, bool Inherit)
        {
            if (Inherit)
            {
                CommonClient.UnassignFieldFromCategoryAndDescendants(User.Identity.Name, CustomFieldID, categoryID);
            }
            else
            {
                CommonClient.UnassignFieldFromCategory(User.Identity.Name, CustomFieldID, new int[] { categoryID });
            }

            //SiteClient.Reset();

            PrepareSuccessMessage("UnassignFieldFromCategoryDetail", MessageType.Method);

            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID = categoryID });
        }

        #endregion

        #region Category Management

        /// <summary>
        /// Displays form to add/edit/delete listing categories
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CategoryEditor()
        {
            return View();
        }

        /// <summary>
        /// Displays form to edit system categories (developer only)
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Strings.Roles.Developer)]
        public ActionResult RootCategoryEditor()
        {
            return View();
        }

        /// <summary>
        /// Displays system component version and license details
        /// </summary>
        /// <returns>View(LicenseInfo)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult VersionInfo()
        {
            return View(ListingClient.GetLicenseInfo());
        }

        /// <summary>
        /// Displays form to add/edit/delete listing regions
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RegionEditor()
        {
            return View();
        }

        /// <summary>
        /// Displays form to edit listing type and custom field associations with the specified category
        /// </summary>
        /// <param name="CategoryID">ID of the requested category</param>
        /// <returns>View(Category)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CategoryDetail(int CategoryID)
        {
            //disable browser XSS detection for this specific page because it can randomly break the HTML editor 
            //  if the content being saved legitimately contains javascript also contained in the editor library.
            Response.AddHeader("X-XSS-Protection", "0");

            Category category = CommonClient.GetCategoryByID(CategoryID);

            ViewData[Strings.MVC.ViewData_AssignedCustomFields] = new SelectList(
                CommonClient.GetFieldsByCategoryID(CategoryID), Strings.Fields.ID, Strings.Fields.Name);

            ViewData[Strings.MVC.ViewData_UnassignedCustomFields] = new SelectList(
                CommonClient.GetCustomFields(category.Type, 0, 0, null, false).List.Except(
                    CommonClient.GetFieldsByCategoryID(CategoryID), new CustomFieldComparer()), Strings.Fields.ID, Strings.Fields.Name);

            ViewData[Strings.MVC.ViewData_AssignedListingTypes] = new SelectList(
                ListingClient.GetValidListingTypesForCategory(CategoryID), Strings.Fields.ID, Strings.Fields.Name);

            ViewData[Strings.MVC.ViewData_UnassignedListingTypes] = new SelectList(
                ListingClient.ListingTypes.Except(ListingClient.GetValidListingTypesForCategory(CategoryID),
                    new ListingClient.ListingTypeEquality()).Where(lt => lt.Enabled), Strings.Fields.ID, Strings.Fields.Name);

            ViewData[Strings.MVC.LineageString] = CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].ToLocalizedLineageString(
                this, Strings.MVC.LineageSeperator, new string[] { Strings.CategoryNames.Root, Strings.CategoryNames.Items });

            return View(category);
        }

        /// <summary>
        /// Displays form to edit the specified region
        /// </summary>
        /// <param name="CategoryID">ID of the requested region</param>
        /// <returns>View(Category)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RegionDetail(int CategoryID)
        {
            Category category = CommonClient.GetCategoryByID(CategoryID);

            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].ToLocalizedLineageString(
                    this, Strings.MVC.LineageSeperator, new string[] { Strings.CategoryNames.Root, Strings.CategoryNames.Regions });

            return View(category);
        }

        /// <summary>
        /// Process request to rename the specified region to the specified name
        /// </summary>
        /// <param name="CategoryID">if of the specified region</param>
        /// <param name="Name">new name for the specified region</param>
        /// <returns>redirect to region detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RenameRegion(int CategoryID, string Name)
        {
            try
            {
                CommonClient.SetCategoryName(User.Identity.Name, CategoryID, Name);
                PrepareSuccessMessage("RenameRegion", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("RenameRegion", MessageType.Method);
            }
            return RedirectToAction("RegionDetail", new { CategoryID });
        }

        /// <summary>
        /// Process request to rename the specified category to the specified name
        /// </summary>
        /// <param name="CategoryID">if of the specified category</param>
        /// <param name="Name">new name for the specified category</param>
        /// <returns>redirect to category detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RenameCategory(int CategoryID, string Name)
        {
            try
            {
                CommonClient.SetCategoryName(User.Identity.Name, CategoryID, Name);
                PrepareSuccessMessage("RenameCategory", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("RenameCategory", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID });
        }

        /// <summary>
        /// Processes request to update the meta keywords and meta description for a specific category
        /// </summary>
        /// <param name="MetaKeywords">desired meta keywords</param>
        /// <param name="MetaDescription">desired meta description</param>
        /// <param name="CategoryID">ID of the category to be updated</param>
        /// <param name="PageTitle">the meta title to be used when displaying the specified category</param>
        /// <param name="PageContent">the meta description to be used when displaying the specified category</param>
        /// <returns>Redirect to category detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult SetCategoryMetaData(string MetaKeywords, string MetaDescription, string PageTitle, string PageContent, int CategoryID)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            try
            {
                CommonClient.SetCategoryMetaData(User.Identity.Name, CategoryID, MetaKeywords, MetaDescription, PageTitle, PageContent);
                PrepareSuccessMessage("SetCategoryMetaData", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetCategoryMetaData", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID });
        }

        #endregion

        #region Field Management

        /// <summary>
        /// Displays a page of a list of fields in the specified group
        /// </summary>
        /// <param name="GroupName">Name of the requested group</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <returns>View(Page&lt;CustomField&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Fields(string GroupName, int? page)
        {
            if (string.IsNullOrEmpty(GroupName)) return RedirectToAction("Index");

            if (GroupName != Strings.CustomFieldGroups.Item &&
                GroupName != Strings.CustomFieldGroups.User &&
                GroupName != Strings.CustomFieldGroups.Event) return RedirectToAction("Index");

            Page<CustomField> fields = CommonClient.GetCustomFields(GroupName, (page ?? 0), SiteClient.PageSize, "DisplayOrder", false);
            ViewData[Strings.MVC.ViewData_SortDescending] = false;
            ViewData[Strings.Fields.Group] = GroupName;
            return View(fields);
        }

        /// <summary>
        /// Displays form to create a new field in the requested group
        /// </summary>
        /// <param name="GroupName">Name of the requested group</param>
        /// <param name="returnUrl"></param>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CreateField(string GroupName, string returnUrl)
        {
            //if (TempData["ViewData"] != null)
            //{
            //    ViewData = (ViewDataDictionary)TempData["ViewData"];
            //}

            Dictionary<int, string> types = new Dictionary<int, string>(6);
            types.Add((int)CustomFieldType.String, this.AdminResourceString(CustomFieldType.String.ToString()));
            types.Add((int)CustomFieldType.Boolean, this.AdminResourceString(CustomFieldType.Boolean.ToString()));
            types.Add((int)CustomFieldType.DateTime, this.AdminResourceString("Date")); //CustomFieldType.DateTime.ToString()));
            types.Add((int)CustomFieldType.Decimal, this.AdminResourceString(CustomFieldType.Decimal.ToString()));
            types.Add((int)CustomFieldType.Enum, this.AdminResourceString(CustomFieldType.Enum.ToString()));
            types.Add((int)CustomFieldType.Int, this.AdminResourceString(CustomFieldType.Int.ToString()));
            ViewData[Strings.Fields.Type] = new SelectList(types, Strings.Fields.Key, Strings.Fields.Value);

            Dictionary<int, string> access = new Dictionary<int, string>(7);

            //CreateField()
            if (GroupName != CustomFieldGroups.User)
            {
                access.Add((int)CustomFieldAccess.None, CustomFieldAccess.None.ToString());
                access.Add((int)CustomFieldAccess.System, CustomFieldAccess.System.ToString());
            }

            access.Add((int)CustomFieldAccess.Admin, CustomFieldAccess.Admin.ToString());
            access.Add((int)CustomFieldAccess.Owner, CustomFieldAccess.Owner.ToString());
            access.Add((int)CustomFieldAccess.Purchaser, CustomFieldAccess.Purchaser.ToString());
            access.Add((int)CustomFieldAccess.Authenticated, CustomFieldAccess.Authenticated.ToString());
            access.Add((int)CustomFieldAccess.Anonymous, CustomFieldAccess.Anonymous.ToString());

            if (string.IsNullOrEmpty(GroupName))
            {
                Dictionary<string, string> groups = new Dictionary<string, string>(4);
                groups.Add("Item", "Item");
                groups.Add("Listing", "Listing");
                groups.Add("Site", "Site");
                groups.Add("User", "User");
                groups.Add("Event", "Event");
                ViewData[Strings.Fields.GroupName] = new SelectList(groups, Strings.Fields.Key, Strings.Fields.Value);
                ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value);
                ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value);
            }
            else
            {
                if (ViewData["Visibility"] == null)
                {
                    switch (GroupName)
                    {
                        case "Item":
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Anonymous);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Owner);
                            break;
                        case "Listing":
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Admin);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Admin);
                            break;
                        case "Site":
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Admin);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Admin);
                            break;
                        case "User":
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Owner);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Owner);
                            break;
                        case "Event":
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Anonymous);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.Owner);
                            break;
                        default:
                            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.None);
                            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)CustomFieldAccess.None);
                            break;
                    }
                }
            }

            ViewData[Strings.Fields.Group] = GroupName;
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;

            if (ViewData["DisplayOrder"] == null)
            {
                Page<CustomField> fields = CommonClient.GetCustomFields(GroupName, 0, 1, "DisplayOrder", true);
                if (fields.List.Count > 0)
                {
                    ViewData["DisplayOrder"] = fields.List[0].DisplayOrder + 1;
                }
                else
                {
                    ViewData["DisplayOrder"] = 0;
                }
            }

            return View();
        }

        /// <summary>
        /// Processes request to create a new custom field in the requested group
        /// </summary>
        /// <param name="Name">Requested name for the new custom field</param>
        /// <param name="Type">Integer value of the corresponding CustomFieldType enum (e.g. String = 1)</param>
        /// <param name="Required">Indicates whether this custom field will be required when available</param>
        /// <param name="Default">Default value to be pre-filled in the appropriate form(s)</param>
        /// <param name="GroupName">Name of the group to which the custom field will be assigned</param>
        /// <param name="Deferred">Indicates whether this custom field will be available in applicable "Create" forms, versus "Edit" forms</param>
        /// <param name="DisplayOrder">Integer value which determines the order this custom field will be rendered relative to other custom fields</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <param name="AssignToAllCategories">If true, assigns custom field to all categorys (Item only)</param>
        /// <param name="Visibility">an integer value used to specify the level of authority required to view this field value</param>
        /// <param name="Mutability">an integer value used to specify the level of authority required to change this field value</param>
        /// <param name="IncludeOnInvoice">bool value used to specify if this custom field should be displayed on sale invoice line items</param>
        /// <param name="IncludeInSalesReport">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports</param>
        /// <param name="IncludeOnInvoiceAsSeller">bool value used to specify if this custom field should be displayed on sale invoice as seller (User fields)</param>
        /// <param name="IncludeOnInvoiceAsBuyer">bool value used to specify if this custom field should be displayed on sale invoice as buyer (User fields)</param>
        /// <param name="IncludeInSalesReportAsSeller">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports as seller (User fields)</param>
        /// <param name="IncludeInSalesReportAsBuyer">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports as buyer (User fields)</param>
        /// <returns>
        /// Redirect to /Admin/Fields/[GroupName] (success);
        /// Redirect to /Admin/CreateField/[GroupName] (failure)
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CreateField(string Name, int Type, bool Required, string Default, string GroupName, bool Deferred,
            string DisplayOrder, string returnUrl, bool? AssignToAllCategories, int Visibility, int Mutability, bool IncludeOnInvoice, bool IncludeInSalesReport,
            bool IncludeOnInvoiceAsSeller, bool IncludeOnInvoiceAsBuyer, bool IncludeInSalesReportAsSeller, bool IncludeInSalesReportAsBuyer)
        {
            if (string.IsNullOrEmpty(Name))
            {
                ModelState.AddModelError("Name", "Name_Required");
            }

            if (!string.IsNullOrEmpty(Default))
            {
                switch (Type)
                {
                    case (int)CustomFieldType.Boolean:
                        bool tempBool;
                        if (!bool.TryParse(Default, out tempBool))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertBoolean");
                        }
                        break;
                    case (int)CustomFieldType.DateTime:
                        DateTime tempDateTime;
                        if (!DateTime.TryParse(Default, out tempDateTime))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertDateTime");
                        }
                        break;
                    case (int)CustomFieldType.Decimal:
                        decimal tempDecimal;
                        if (!decimal.TryParse(Default, out tempDecimal))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertDecimal");
                        }
                        break;
                    case (int)CustomFieldType.Int:
                        int tempInt;
                        if (!int.TryParse(Default, out tempInt))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertInteger");
                        }
                        break;
                }
            }

            int _DisplayOrder = 0;
            if (string.IsNullOrEmpty(DisplayOrder.Trim()))
            {
                ModelState.AddModelError("DisplayOrder", "DisplayOrder_Required");
            }
            else
            {
                if (!int.TryParse(DisplayOrder, out _DisplayOrder))
                {
                    ModelState.AddModelError("DisplayOrder", "DisplayOrder_ConvertInteger");
                }
            }

            if (!ModelState.IsValid)
            {
                //TempData["ViewData"] = ViewData;
                //return RedirectToAction("CreateField", new { GroupName, returnUrl });
                return CreateField(GroupName, returnUrl);
            }

            //do save
            CustomField field = new CustomField();
            field.DefaultValue = Default.Trim();
            field.Deferred = Deferred;
            field.DisplayOrder = _DisplayOrder;
            field.Group = GroupName.Trim();
            field.Name = Name.Trim();
            field.Required = Required;
            field.Type = (CustomFieldType)Type;
            field.Visibility = (CustomFieldAccess)Visibility;
            field.Mutability = (CustomFieldAccess)Mutability;
            field.IncludeOnInvoice = IncludeOnInvoice;
            field.IncludeInSalesReport = IncludeInSalesReport;
            field.IncludeOnInvoiceAsSeller = IncludeOnInvoiceAsSeller;
            field.IncludeOnInvoiceAsBuyer = IncludeOnInvoiceAsBuyer;
            field.IncludeInSalesReportAsSeller = IncludeInSalesReportAsSeller;
            field.IncludeInSalesReportAsBuyer = IncludeInSalesReportAsBuyer;
            int newFieldID = 0;

            try
            {
                newFieldID = CommonClient.AddCustomField(User.Identity.Name, field);
                SiteClient.Reset();
                UserClient.Reset();

                /* *
                if (GroupName == CustomFieldGroups.Item)
                {
                    PrepareSuccessMessage(Messages.AssignFieldToOneOrMoreCategories);
                    return RedirectToAction(Strings.MVC.CategoryEditorAction);
                }
                * */

                if (AssignToAllCategories == true)
                {
                    CommonClient.AssignFieldToCategoryAndDescendants(User.Identity.Name, newFieldID, 9);
                }

                if (GroupName == CustomFieldGroups.User)
                {
                    CommonClient.ClearCache("CategoriesByID");
                }

                if (SiteClient.EnableEvents)
                {
                    SiteClient.RemoveCacheData("EventActivityReportColumns");
                }

                PrepareSuccessMessage("CreateField", MessageType.Method);

                if ((CustomFieldType)Type == CustomFieldType.Enum)
                {
                    return RedirectToAction(Strings.MVC.EditFieldAction, new { id = newFieldID, returnUrl });
                }
                else
                {
                    return RedirectToAction(Strings.MVC.Fields, new { GroupName });

                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                //PrepareErrorMessage(iofc.Detail.Reason);                
                ModelState.AddModelError("Name", Enum.GetName(typeof(ReasonCode), iofc.Detail.Reason));
                //TempData["ViewData"] = ViewData;
                //return RedirectToAction("CreateField", new { GroupName, returnUrl });
                return CreateField(GroupName, returnUrl);
            }
            catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                //TempData["ViewData"] = ViewData;
                //return RedirectToAction("CreateField", new { GroupName, returnUrl });
                return CreateField(GroupName, returnUrl);
            }
            catch (Exception)
            {
                PrepareErrorMessage("CreateField", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.Fields, new { GroupName });
        }

        /// <summary>
        /// Processes request to delete a custom field
        /// </summary>
        /// <param name="FieldID">ID of the custom field to be deleted</param>
        /// <param name="GroupName">Name of the group to redirect to</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to /Admin/Fields/[GroupName]</returns>
        [Authorize(Roles = Roles.Admin)]
        /*[AcceptVerbs(HttpVerbs.Post)]*/
        public ActionResult DeleteField(int FieldID, string GroupName, string returnUrl)
        {
            if (FieldID == 5 || FieldID == 6)
            {
                PrepareErrorMessage(ReasonCode.CustomFieldDeletingForbidden);
                return RedirectToAction(Strings.MVC.Fields, new { GroupName });
            }

            try
            {
                CommonClient.DeleteField(User.Identity.Name, FieldID);
                PrepareSuccessMessage("DeleteField", MessageType.Method);
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DeleteField", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.Fields, new { GroupName });
        }

        /// <summary>
        /// Displays form to edit the specified custom field
        /// </summary>
        /// <param name="id">ID of the field to be edited</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>View(CustomField)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditField(int id, string returnUrl)
        {
            //if (TempData["ViewData"] != null)
            //{
            //    ViewData = (ViewDataDictionary)TempData["ViewData"];
            //}

            CustomField field = CommonClient.GetCustomFieldByID(id);

            Dictionary<int, string> types = new Dictionary<int, string>(6);
            types.Add((int)CustomFieldType.String, this.AdminResourceString(CustomFieldType.String.ToString()));
            types.Add((int)CustomFieldType.Boolean, this.AdminResourceString(CustomFieldType.Boolean.ToString()));
            types.Add((int)CustomFieldType.DateTime, this.AdminResourceString("Date")); // CustomFieldType.DateTime.ToString()));
            types.Add((int)CustomFieldType.Decimal, this.AdminResourceString(CustomFieldType.Decimal.ToString()));
            types.Add((int)CustomFieldType.Enum, this.AdminResourceString(CustomFieldType.Enum.ToString()));
            types.Add((int)CustomFieldType.Int, this.AdminResourceString(CustomFieldType.Int.ToString()));
            ViewData[Strings.Fields.Type] = new SelectList(types, Strings.Fields.Key, Strings.Fields.Value, (int)field.Type);
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;

            Dictionary<int, string> access = new Dictionary<int, string>(7);

            //EditField()
            if (field.Group != CustomFieldGroups.User)
            {
                access.Add((int)CustomFieldAccess.None, CustomFieldAccess.None.ToString());
                access.Add((int)CustomFieldAccess.System, CustomFieldAccess.System.ToString());
            }
            access.Add((int)CustomFieldAccess.Admin, CustomFieldAccess.Admin.ToString());
            access.Add((int)CustomFieldAccess.Owner, CustomFieldAccess.Owner.ToString());
            access.Add((int)CustomFieldAccess.Purchaser, CustomFieldAccess.Purchaser.ToString());
            access.Add((int)CustomFieldAccess.Authenticated, CustomFieldAccess.Authenticated.ToString());
            access.Add((int)CustomFieldAccess.Anonymous, CustomFieldAccess.Anonymous.ToString());
            ViewData["Visibility"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)field.Visibility);
            ViewData["Mutability"] = new SelectList(access, Strings.Fields.Key, Strings.Fields.Value, (int)field.Mutability);

            return View(Strings.MVC.EditFieldAction, field);
        }

        /// <summary>
        /// Processes request to edit an existing custom field
        /// </summary>
        /// <param name="id">ID of the requested custom field</param>
        /// <param name="Name">Requested name for the custom field</param>
        /// <param name="Type">Integer value of the corresponding CustomFieldType enum (e.g. String = 1)</param>
        /// <param name="Required">Indicates whether this field will be required when available</param>
        /// <param name="Default">Default value to be pre-filled in the appropriate form(s)</param>
        /// <param name="GroupName">Name of the group to which the custom field is assigned</param>
        /// <param name="Deferred">Indicates whether this custom field will be available in applicable "Create" forms, versus "Edit" forms</param>
        /// <param name="DisplayOrder">Integer value which determines the order this custom field will be rendered relative to other custom fields</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <param name="Visibility">an integer value used to specify the level of authority required to view this field value</param>
        /// <param name="Mutability">an integer value used to specify the level of authority required to change this field value</param>
        /// <param name="IncludeOnInvoice">bool value used to specify if this custom field should be displayed on sale invoice line items (Listing, Event fields)</param>
        /// <param name="IncludeInSalesReport">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports (Listing, Event fields)</param>
        /// <param name="IncludeOnInvoiceAsSeller">bool value used to specify if this custom field should be displayed on sale invoice as seller (User fields)</param>
        /// <param name="IncludeOnInvoiceAsBuyer">bool value used to specify if this custom field should be displayed on sale invoice as buyer (User fields)</param>
        /// <param name="IncludeInSalesReportAsSeller">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports as seller (User fields)</param>
        /// <param name="IncludeInSalesReportAsBuyer">bool value used to specify if this custom field should be displayed on sales transaction report CSV exports as buyer (User fields)</param>
        /// <returns>
        /// Redirect to /Admin/Fields/[GroupName] (success);
        /// Redirect to /Admin/CreateField/[GroupName] (failure)
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SaveField(int id, string Name, int Type, bool Required, string Default, string GroupName,
            bool Deferred, string DisplayOrder, string returnUrl, int Visibility, int Mutability, bool IncludeOnInvoice, bool IncludeInSalesReport,
            bool IncludeOnInvoiceAsSeller, bool IncludeOnInvoiceAsBuyer, bool IncludeInSalesReportAsSeller, bool IncludeInSalesReportAsBuyer)
        {
            //if (string.IsNullOrEmpty(Name))
            //{
            //    ModelState.AddModelError("Name", "Name_Required");
            //}

            if (id == 5 || id == 6)
            {
                PrepareErrorMessage(ReasonCode.CustomFieldEditingForbidden);
                return RedirectToAction(Strings.MVC.Fields, new { GroupName });
            }

            if (!string.IsNullOrEmpty(Default))
            {
                switch (Type)
                {
                    case (int)CustomFieldType.Boolean:
                        bool tempBool;
                        if (!bool.TryParse(Default, out tempBool))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertBoolean");
                        }
                        break;
                    case (int)CustomFieldType.DateTime:
                        DateTime tempDateTime;
                        if (!DateTime.TryParse(Default, this.GetCultureInfo(), DateTimeStyles.None, out tempDateTime))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertDateTime");
                        }
                        Default = DateTime.Parse(tempDateTime.ToString()).ToString();
                        break;
                    case (int)CustomFieldType.Decimal:
                        decimal tempDecimal;
                        if (!decimal.TryParse(Default, NumberStyles.Number, this.GetCultureInfo(), out tempDecimal))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertDecimal");
                        }
                        Default = decimal.Parse(tempDecimal.ToString()).ToString();
                        break;
                    case (int)CustomFieldType.Int:
                        int tempInt;
                        if (!int.TryParse(Default, NumberStyles.Number, this.GetCultureInfo(), out tempInt))
                        {
                            ModelState.AddModelError("Default", "Default_ConvertInteger");
                        }
                        Default = int.Parse(tempInt.ToString()).ToString();

                        break;
                }
            }

            int _DisplayOrder = 0;
            if (string.IsNullOrEmpty(DisplayOrder.Trim()))
            {
                ModelState.AddModelError("DisplayOrder", "DisplayOrder_Required");
            }
            else
            {
                if (!int.TryParse(DisplayOrder, out _DisplayOrder))
                {
                    ModelState.AddModelError("DisplayOrder", "DisplayOrder_ConvertInteger");
                }
            }

            if (!ModelState.IsValid)
            {
                //TempData["ViewData"] = ViewData;
                //return RedirectToAction("EditField", new { id, returnUrl });
                return EditField(id, returnUrl);
            }

            CustomField field = CommonClient.GetCustomFieldByID(id);

            //do save                        
            field.DefaultValue = Default.Trim();
            field.Deferred = Deferred;
            field.DisplayOrder = _DisplayOrder;
            field.ID = id;
            field.Name = Name.Trim();
            field.Required = Required;
            field.Type = (CustomFieldType)Type;
            field.Visibility = (CustomFieldAccess)Visibility;
            field.Mutability = (CustomFieldAccess)Mutability;
            field.IncludeOnInvoice = IncludeOnInvoice;
            field.IncludeInSalesReport = IncludeInSalesReport;
            field.IncludeOnInvoiceAsSeller = IncludeOnInvoiceAsSeller;
            field.IncludeOnInvoiceAsBuyer = IncludeOnInvoiceAsBuyer;
            field.IncludeInSalesReportAsSeller = IncludeInSalesReportAsSeller;
            field.IncludeInSalesReportAsBuyer = IncludeInSalesReportAsBuyer;
            try
            {
                CommonClient.UpdateCustomField(User.Identity.Name, field);
                SiteClient.Reset();
                UserClient.Reset();
                if (SiteClient.EnableEvents)
                {
                    SiteClient.RemoveCacheData("EventActivityReportColumns");
                }
                PrepareSuccessMessage("SaveField", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                //TempData["ViewData"] = ViewData;
                //return RedirectToAction(Strings.MVC.EditFieldAction, new { id, returnUrl });
                return EditField(id, returnUrl);
            }
            catch (Exception)
            {
                PrepareErrorMessage("SaveField", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.Fields, new { GroupName });
        }

        /// <summary>
        /// Processes request to add a new custom field enumeration value
        /// </summary>
        /// <param name="id">ID of the custom field to which this enum will be assigned</param>
        /// <param name="Name">Name of the enum to be added</param>
        /// <param name="Title">Display name of the enum to be added</param>
        /// <param name="Value">Value of the enum to be added</param>
        /// <param name="Enabled">Determines whether this enum if currently available for use</param>
        /// <param name="returnUrl">the optional url to retain in the redirect url upon success</param>
        /// <returns>Redirect to /Admin/EditField/[CustomFieldId]</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddEnumeration(int id, string Name, string Title, string Value, bool Enabled, string returnUrl)
        {
            try
            {
                string timmedName = string.IsNullOrEmpty(Name) ? Name : Name.Trim();
                string timmedTitle = string.IsNullOrEmpty(Title) ? Title : Title.Trim();
                string timmedValue = string.IsNullOrEmpty(Value) ? Value : Value.Trim();
                CommonClient.AddEnumeration(User.Identity.Name, id, timmedName, timmedTitle, timmedValue, Enabled);
                SiteClient.Reset();
                UserClient.Reset();
                PrepareSuccessMessage("AddEnumeration", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AddEnumeration", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditFieldAction, new { id, returnUrl });
        }

        /// <summary>
        /// Processes request to delete the specified enum value
        /// </summary>
        /// <param name="id">ID of the custom field from which this enum will be removed</param>
        /// <param name="EnumerationID">ID of the enum value to be deleted</param>
        /// <param name="returnUrl">the optional url to retain in the redirect url upon success</param>
        /// <returns>Redirect to /Admin/EditField/[CustomFieldId]</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteEnumeration(int id, int EnumerationID, string returnUrl)
        {
            try
            {
                CommonClient.DeleteEnum(User.Identity.Name, EnumerationID);
                SiteClient.Reset();
                UserClient.Reset();
                PrepareSuccessMessage("DeleteEnumeration", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteEnumeration", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditFieldAction, new { id, returnUrl });
        }

        /// <summary>
        /// Update custom field display order
        /// </summary>
        /// <param name="index">index of the custom field to be re-ordered</param>
        /// <param name="moveUp">if true, move the order of the custom field up, otherwise move down</param>
        /// <param name="GroupName">Name of the requested group</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <returns>View(Page&lt;CustomField&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateDisplayOrder(int index, Boolean moveUp, string GroupName, int? page)
        {

            Page<CustomField> fields = CommonClient.GetCustomFields(GroupName, (page ?? 0), SiteClient.PageSize, "DisplayOrder", false);
            Page<CustomField> swapFields;

            CustomField field = fields.List[index];
            CustomField swapField;

            int swapOrder;

            // if object is in the first position of the first page, it cannot move up
            // if object is in the last positino of the last page, it cannot move down
            // this is also checked in the /Admin/Fields view
            if ((moveUp && index == 0 && (page ?? 0) == 0) || (!moveUp && index == (fields.List.Count - 1) && fields.TotalPageCount == (page ?? 0) + 1))
            {
                return RedirectToAction(Strings.MVC.Fields, new { GroupName, page });
            }

            if (moveUp)
            {
                // moving up the object will put it on the previous page
                if (index == 0)
                {
                    swapFields = CommonClient.GetCustomFields(GroupName, (page ?? 0) - 1, SiteClient.PageSize, "DisplayOrder", false);
                    swapField = swapFields.List[SiteClient.PageSize - 1];
                }
                else
                {
                    swapField = fields.List[index - 1];
                }
            }
            else
            {
                // moving down the object will put it on the next page
                if (index == (fields.List.Count - 1))
                {
                    swapFields = CommonClient.GetCustomFields(GroupName, (page ?? 0) + 1, SiteClient.PageSize, "DisplayOrder", false);
                    swapField = swapFields.List[0];
                }
                else
                {
                    swapField = fields.List[index + 1];
                }
            }

            if (field.DisplayOrder == swapField.DisplayOrder)
            {
                int duplicateDisplayOrder = field.DisplayOrder;
                bool changeNext = false;

                for (int i = 0; i < fields.TotalPageCount; i++)
                {
                    Page<CustomField> currentFields = CommonClient.GetCustomFields(GroupName, i, SiteClient.PageSize, "DisplayOrder", false);
                    for (int j = 0; j < currentFields.List.Count; j++)
                    {
                        CustomField checkField = currentFields.List[j];
                        if (duplicateDisplayOrder <= checkField.DisplayOrder)
                        {
                            if (changeNext)
                            {
                                checkField.DisplayOrder++;
                                CommonClient.UpdateCustomFieldDisplayOrder(User.Identity.Name, checkField);
                            }
                            else
                            {
                                changeNext = true;
                            }
                        }
                    }
                }
            }

            // switches the display order between the two field objects
            swapOrder = field.DisplayOrder;
            field.DisplayOrder = swapField.DisplayOrder;
            swapField.DisplayOrder = swapOrder;

            try
            {
                CommonClient.UpdateCustomFieldDisplayOrder(User.Identity.Name, field);
                CommonClient.UpdateCustomFieldDisplayOrder(User.Identity.Name, swapField);

                SiteClient.Reset();
                UserClient.Reset();
                PrepareSuccessMessage("UpdateDisplayOrder", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("UpdateDisplayOrder", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.Fields, new { GroupName, page });

        }

        #endregion

        #region Listing Type Management

        /// <summary>
        /// Processes request to associate a listing type with a listing category
        /// </summary>
        /// <param name="CategoryID">ID of the reuqested listing category</param>
        /// <param name="ListingTypeID">ID of the requested listing type</param>
        /// <returns>Redirect to /Admin/CategoryDetail/[CategoryID]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AssignListingTypeFromCategoryDetail(int CategoryID, int ListingTypeID)
        {
            try
            {
                ListingClient.AssignListingTypeToCategory(User.Identity.Name, CategoryID, ListingTypeID);
                ListingClient.Reset();
                PrepareSuccessMessage("AssignListingTypeFromCategoryDetail", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AssignListingTypeFromCategoryDetail", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID });
        }

        /// <summary>
        /// Processes request to disassociate a listing type from a listing category
        /// </summary>
        /// <param name="CategoryID">ID of the reuqested listing category</param>
        /// <param name="ListingTypeID">ID of the requested listing type</param>
        /// <returns>Redirect to /Admin/CategoryDetail/[CategoryID]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UnassignListingTypeFromCategoryDetail(int CategoryID, int ListingTypeID)
        {
            try
            {
                ListingClient.UnassignListingTypeToCategory(User.Identity.Name, CategoryID, ListingTypeID);
                ListingClient.Reset();
                PrepareSuccessMessage("UnassignListingTypeFromCategoryDetail", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("UnassignListingTypeFromCategoryDetail", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CategoryDetail, new { CategoryID });
        }

        /// <summary>
        /// Displays form to enable or disable all available listing types
        /// </summary>
        /// <returns>View(List&lt;ListingType&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ListingTypeToggle()
        {
            return View(ListingClient.ListingTypes);
        }

        /// <summary>
        /// Processes request to enable or disable the specified listing type
        /// </summary>
        /// <param name="listingTypeID">ID of the requested listing type</param>
        /// <returns>Redirect to /Admin/ListingTypeToggle</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ToggleListingType(int listingTypeID)
        {
            try
            {
                ListingClient.ToggleListingType(User.Identity.Name, listingTypeID);
                ListingClient.Reset();
                PrepareSuccessMessage("ToggleListingType", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("ToggleListingType", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ListingTypeToggle);
        }

        #endregion

        #region Extending Listings

        /// <summary>
        /// Displays form to extend listing end dates
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ExtendListings()
        {
            return View();
        }

        /// <summary>
        /// Processes request to a extend a listing end date 10 minutes
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <returns>Redirect to /Admin/ExtendListings</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Extend10Minutes(int id)
        {
            try
            {
                ListingClient.ExtendEndDTTM(User.Identity.Name, id, 10);
                PrepareSuccessMessage("Extend10Minutes", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("Extend10Minutes", MessageType.Method);
            }

            if (Request.UrlReferrer != null)
            {
                return Redirect(Request.UrlReferrer.PathAndQuery);
            }
            else
            {
                return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.AdminController);
            }


        }

        /// <summary>
        /// Processes request to extend a listing end date the specified number of minutes
        /// </summary>
        /// <param name="ListingID">ID of the requested listing</param>
        /// <param name="Minutes">The requested number of minutes to extend</param>
        /// <param name="AllListings">Indicates if all listings should be extended</param>
        /// <returns>Redirects to Admin/Index</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ExtendListings(string ListingID, string Minutes, bool AllListings)
        {
            try
            {
                int minutes = 0;
                if (AllListings) ListingID = null;
                if (!string.IsNullOrEmpty(Minutes))
                {
                    if (!int.TryParse(Minutes, out minutes))
                    {
                        ModelState.AddModelError("Minutes", string.Format(Strings.Formats.IntegerConversionValidationMessage, null, "Minutes"));
                    }

                    if (minutes <= 0)
                    {
                        ModelState.AddModelError("Minutes", string.Format(Strings.Formats.PositiveNumericValidationMessage, null, "Minutes"));
                    }
                }
                else
                {
                    ModelState.AddModelError("Minutes", string.Format(Strings.Formats.RequiredValidationMessage, null, "Minutes"));
                }

                int listingID = 0;
                if (!string.IsNullOrEmpty(ListingID))
                {
                    if (!int.TryParse(ListingID, out listingID))
                    {
                        ModelState.AddModelError("ListingID", string.Format(Strings.Formats.IntegerConversionValidationMessage, null, "ListingID"));
                    }
                }

                if (!ModelState.IsValid) return View();

                if (listingID > 0)
                {
                    ListingClient.ExtendEndDTTM(User.Identity.Name, listingID, minutes);
                }
                else
                {
                    ListingClient.ExtendAllEndDTTM(User.Identity.Name, minutes);
                }
                PrepareSuccessMessage("ExtendListings", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                if (iofc.Detail.Reason == ReasonCode.ListingNotExist)
                {
                    ModelState.AddModelError("ListingID", "ListingNotFound");
                }
                else
                {
                    PrepareErrorMessage(iofc.Detail.Reason);
                }
            }
            catch
            {
                PrepareErrorMessage("ExtendListings", MessageType.Method);
            }
            return View();
        }

        #endregion

        #region Increments

        /// <summary>
        /// Displays form and processes request to add, edit or delete bid increments
        /// </summary>
        /// <param name="id">ID of the requested category under which to adjust bid increments</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult IncrementManagement(int id)
        {
            try
            {
                Category currentCategory = CommonClient.GetCategoryByID(id);
                ViewData[Strings.Fields.Category] = currentCategory;
                Category parentCategory = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);
                ViewData[Strings.Fields.ParentCategory] = parentCategory;


                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {
                    //Update
                    List<Increment> increments = DecodeIncrements();
                    if (increments.Count > 0)
                        ListingClient.UpdateIncrements(HttpContext.User.Identity.Name, increments);

                    //Delete
                    if (!string.IsNullOrEmpty(Request[Strings.Fields.DeleteIncrement]))
                        ListingClient.DeleteIncrements(HttpContext.User.Identity.Name,
                                                       Request[Strings.Fields.DeleteIncrement]);

                    //Add
                    if (!string.IsNullOrEmpty(Request[Strings.Fields.IncrementNewPriceLevel]) &&
                        !string.IsNullOrEmpty(Request[Strings.Fields.IncrementNewIncrement]))
                    {
                        decimal priceLevel;
                        decimal amount;

                        if (decimal.TryParse(Request[Strings.Fields.IncrementNewPriceLevel], out priceLevel))
                        {
                            if (priceLevel < 0)
                            {
                                ModelState.AddModelError(Strings.Fields.IncrementNewPriceLevel, string.Format(
                                    Strings.Formats.PositiveNumericValidationMessage, null, Strings.Fields.IncrementNewPriceLevel));
                            }
                        }
                        else
                        {
                            ModelState.AddModelError(Strings.Fields.IncrementNewPriceLevel, string.Format(
                                Strings.Formats.DecimalConversionValidationMessage, null, Strings.Fields.IncrementNewPriceLevel));
                        }

                        if (decimal.TryParse(Request[Strings.Fields.IncrementNewIncrement], out amount))
                        {
                            if (amount < 0)
                            {
                                ModelState.AddModelError(Strings.Fields.IncrementNewIncrement, string.Format(
                                    Strings.Formats.PositiveNumericValidationMessage, null, Strings.Fields.IncrementNewIncrement));
                            }
                        }
                        else
                        {
                            ModelState.AddModelError(Strings.Fields.IncrementNewIncrement, string.Format(
                                Strings.Formats.DecimalConversionValidationMessage, null, Strings.Fields.IncrementNewIncrement));
                        }

                        if (decimal.TryParse(Request[Strings.Fields.IncrementNewPriceLevel], out priceLevel) &&
                            decimal.TryParse(Request[Strings.Fields.IncrementNewIncrement], out amount))
                        {
                            ListingClient.AddIncrement(HttpContext.User.Identity.Name,
                                                        parentCategory.Name,
                                                       priceLevel, amount);
                        }
                    }

                    if (!ModelState.IsValid) return View();

                    PrepareSuccessMessage("IncrementManagement", MessageType.Method);
                }
                else
                {
                    //Load
                }
            }
            catch
            {
                PrepareErrorMessage("IncrementManagement", MessageType.Method);
            }
            return View();
        }

        /// <summary>
        /// Parses form values into a list of increments
        /// </summary>
        /// <returns>List&lt;Increment&gt;</returns>
        private List<Increment> DecodeIncrements()
        {
            List<Increment> retVal = new List<Increment>();

            foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            {
                //for all keys in the form collection
                if (!key.StartsWith(Strings.MVC.PriceLevelPrefix)) continue;
                //if the key is for an image
                int id = int.Parse(key.Substring(15));
                decimal priceLevel;
                decimal amount;

                if (decimal.TryParse(Request[Strings.MVC.PriceLevelPrefix + id], out priceLevel))
                {
                    if (priceLevel < 0)
                    {
                        ModelState.AddModelError(string.Concat(Strings.MVC.PriceLevelPrefix, id), string.Format(
                            Strings.Formats.PositiveNumericValidationMessage, null, "PriceLevel"));
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Concat(Strings.MVC.PriceLevelPrefix, id), string.Format(
                        Strings.Formats.DecimalConversionValidationMessage, null, "PriceLevel"));
                }

                if (decimal.TryParse(Request[Strings.MVC.IncrementPrefix + id], out amount))
                {
                    if (amount < 0)
                    {
                        ModelState.AddModelError(string.Concat(Strings.MVC.IncrementPrefix, id), string.Format(
                            Strings.Formats.PositiveNumericValidationMessage, null, "BidIncrement"));
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Concat(Strings.MVC.IncrementPrefix, id), string.Format(
                        Strings.Formats.DecimalConversionValidationMessage, null, "BidIncrement"));
                }

                if (decimal.TryParse(Request[Strings.MVC.PriceLevelPrefix + id], out priceLevel) &&
                    decimal.TryParse(Request[Strings.MVC.IncrementPrefix + id], out amount))
                {
                    var newIncrement = new Increment
                    {
                        ID = id,
                        PriceLevel = priceLevel,
                        Amount = amount
                    };
                    retVal.Add(newIncrement);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Parses form values into a list of increments
        /// </summary>
        /// <param name="validationIssues">reference to a list of validation issues - any invalid form values are added to this list</param>
        /// <returns>List&lt;Increment&gt;</returns>
        private List<Increment> DecodeIncrementsWithValidation(ref List<ValidationIssue> validationIssues)
        {
            List<Increment> retVal = new List<Increment>();

            foreach (string formKey in Request.Form.AllKeys.Where(k => k != null))
            {
                //for all keys in the form collection
                if (!formKey.StartsWith(Strings.MVC.PriceLevelPrefix)) continue;
                //if the key is for an image
                int id = int.Parse(formKey.Substring(Strings.MVC.PriceLevelPrefix.Length));
                decimal priceLevel;
                decimal amount;
                int prevErrorCount = validationIssues.Count;

                if (decimal.TryParse(Request[Strings.MVC.PriceLevelPrefix + id], out priceLevel))
                {
                    if (priceLevel < 0)
                    {
                        string key = string.Concat(Strings.MVC.PriceLevelPrefix, id);
                        string message = string.Format(Strings.Formats.PositiveNumericValidationMessage, null, "PriceLevel");
                        validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                    }
                }
                else
                {
                    string key = string.Concat(Strings.MVC.PriceLevelPrefix, id);
                    string message = string.Format(Strings.Formats.DecimalConversionValidationMessage, null, "PriceLevel");
                    validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                }

                if (decimal.TryParse(Request[Strings.MVC.IncrementPrefix + id], out amount))
                {
                    if (amount < 0)
                    {
                        string key = string.Concat(Strings.MVC.IncrementPrefix, id);
                        string message = string.Format(Strings.Formats.PositiveNumericValidationMessage, null, "BidIncrement");
                        validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                    }
                }
                else
                {
                    string key = string.Concat(Strings.MVC.IncrementPrefix, id);
                    string message = string.Format(Strings.Formats.DecimalConversionValidationMessage, null, "BidIncrement");
                    validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                }

                if (validationIssues.Count == prevErrorCount)
                {
                    var newIncrement = new Increment
                    {
                        ID = id,
                        PriceLevel = priceLevel,
                        Amount = amount
                    };
                    retVal.Add(newIncrement);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Processes request to add, edit or delete bid increments
        /// </summary>
        /// <param name="ListingTypeName">Name of the requested listing type (e.g. "Auction") under which to adjust bid increments</param>
        /// <returns>Redirect to the property page of specified listing type (e.g. "/Admin/ListingTypeProperties/Auction")</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateBidIncrements(string ListingTypeName)
        {
            ListingType listingType = ListingClient.ListingTypes
                .Where(lt => lt.Name == ListingTypeName).SingleOrDefault();
            if (listingType != null)
            {
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                this.GetCookie(Strings.MVC.CultureCookie),
                                                this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                List<ValidationIssue> validationIssues = new List<ValidationIssue>();
                try
                {
                    //Update
                    List<Increment> increments = DecodeIncrementsWithValidation(ref validationIssues);//DecodeIncrements(validation);
                    if (validationIssues.Count == 0 && increments.Count > 0)
                    {
                        ListingClient.UpdateIncrements(HttpContext.User.Identity.Name, increments);
                    }

                    //Delete
                    if (validationIssues.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(Request[Strings.Fields.DeleteIncrement]))
                            ListingClient.DeleteIncrements(HttpContext.User.Identity.Name,
                                                           Request[Strings.Fields.DeleteIncrement]);
                    }

                    //Add
                    if (!string.IsNullOrEmpty(Request[Strings.Fields.IncrementNewPriceLevel]) &&
                        !string.IsNullOrEmpty(Request[Strings.Fields.IncrementNewIncrement]))
                    {
                        decimal priceLevel;
                        decimal amount;

                        if (decimal.TryParse(Request[Strings.Fields.IncrementNewPriceLevel], out priceLevel))
                        {
                            if (priceLevel < 0)
                            {
                                string key = Strings.Fields.IncrementNewPriceLevel;
                                string message = string.Format(Strings.Formats.PositiveNumericValidationMessage, null, "PriceLevel");
                                validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                            }
                        }
                        else
                        {
                            string key = Strings.Fields.IncrementNewPriceLevel;
                            string message = string.Format(Strings.Formats.DecimalConversionValidationMessage, null, "PriceLevel");
                            validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                        }

                        if (decimal.TryParse(Request[Strings.Fields.IncrementNewIncrement], out amount))
                        {
                            if (amount < 0)
                            {
                                string key = Strings.Fields.IncrementNewIncrement;
                                string message = string.Format(Strings.Formats.PositiveNumericValidationMessage, null, "BidIncrement");
                                validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                            }
                        }
                        else
                        {
                            string key = Strings.Fields.IncrementNewIncrement;
                            string message = string.Format(Strings.Formats.DecimalConversionValidationMessage, null, "BidIncrement");
                            validationIssues.Add(new ValidationIssue(message, key, null, null, null));
                        }

                        if (validationIssues.Count == 0)
                        {
                            ListingClient.AddIncrement(HttpContext.User.Identity.Name
                                , listingType.Name, priceLevel, amount);
                        }
                    }

                    if (validationIssues.Count == 0)
                    {
                        PrepareSuccessMessage("UpdateBidIncrements", MessageType.Method);
                    }
                    else
                    {
                        //StoreValidationIssues(validationIssues, null);
                        foreach (ValidationIssue issue in validationIssues)
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                        if (!SiteClient.EnableEvents)
                        {
                            return ListingTypeProperties(ListingTypeName);
                        }
                        else
                        {
                            return AuctionLotSettings();
                        }
                    }
                }
                catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors
                    //TODO mostly deprecated by FunctionResult
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                    if (!SiteClient.EnableEvents)
                    {
                        return ListingTypeProperties(ListingTypeName);
                    }
                    else
                    {
                        return AuctionLotSettings();
                    }
                }
                catch (Exception)
                {
                    PrepareErrorMessage("UpdateBidIncrements", MessageType.Method);
                }
                if (!SiteClient.EnableEvents)
                {
                    return RedirectToAction(Strings.MVC.ListingTypePropertiesAction, new { ListingTypeName = listingType.Name });
                }
                else
                {
                    return RedirectToAction(Strings.MVC.AuctionLotSettingsAction);
                }
            }
            //an invalid listing type was specified, redirect to general listing options page
            PrepareErrorMessage("UpdateBidIncrements", MessageType.Method);

            return RedirectToAction(Strings.MVC.PropertyManagementAction, new { @id = 41401 });
        }

        #endregion

        #region Currency

        /// <summary>
        /// Displays form and processes request to change the site default currency and to edit currency exchange rates
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CurrencyManagement()
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {
                    //Save which currencies are enabled
                    string currencies = Request[Strings.Fields.Currencies] ?? Request[Strings.Fields.DefaultCurrency];
                    SiteClient.SetCurrencies(User.Identity.Name, currencies);

                    //Save the currency conversion rates (ignore rate values entered for any that are not selected as enabled)
                    UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
                    List<Currency> allEnabledCurrencies = SiteClient.GetCurrencies();
                    foreach (string key in Request.Form.AllKeys.Where(k => k != null && k.StartsWith(Strings.MVC.CurrencyRatePrefix)))
                    {
                        bool isEnabled = false;
                        foreach (Currency enabledCurrency in allEnabledCurrencies)
                        {
                            if (key == (Strings.MVC.CurrencyRatePrefix + enabledCurrency.Code))
                            {
                                isEnabled = true;
                                break;
                            }
                        }
                        if (isEnabled)
                        {
                            input.Items.Add(key,
                                            Request.Form[key] == Strings.MVC.TrueFormValue
                                                ? Strings.MVC.TrueValue
                                                : Request.Form[key].Trim());
                        }
                    }
                    SiteClient.SaveCurrencyConversions(User.Identity.Name, input);

                    //Save the default currency setting
                    //List<CustomProperty> propsToUpdate =
                    //    SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.SiteCurrency).ToList();
                    //propsToUpdate[0].Value = Request[Strings.Fields.DefaultCurrency];
                    //SiteClient.UpdateSettings(User.Identity.Name, propsToUpdate);
                    string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
                    SiteClient.UpdateSetting(User.Identity.Name, SiteProperties.SiteCurrency, Request[Strings.Fields.DefaultCurrency], cultureCode);

                    //Finally, reload settings now that the requested changes have been saved
                    SiteClient.Reset();

                    PrepareSuccessMessage("CurrencyManagement", MessageType.Method);
                }
                else
                {
                    //Load
                }
            }
            catch
            {
                PrepareErrorMessage("CurrencyManagement", MessageType.Method);
            }
            var allCurrencyRegionsSorted = new List<CustomCurrency>(SiteClient.AllCurrencies.Keys.Count);
            var allCurrencyRegions = new List<CustomCurrency>(SiteClient.AllCurrencies.Values);
            var enabledCurrencyRegions = new List<CustomCurrency>(SiteClient.SupportedCurrencyRegions.Values);
            //first add enabled currencies to the top of the list, ordered by English Currency Name
            allCurrencyRegionsSorted.AddRange(
                allCurrencyRegions.Where(ri => enabledCurrencyRegions.Exists(eri => eri.ISOCurrencySymbol == ri.ISOCurrencySymbol))
                                  .OrderBy(ri => ri.CurrencyEnglishName));
            //then add the remaining currencies, ordered by the English Currency Name
            allCurrencyRegionsSorted.AddRange(
                allCurrencyRegions.Where(ri => !enabledCurrencyRegions.Exists(eri => eri.ISOCurrencySymbol == ri.ISOCurrencySymbol))
                                  .OrderBy(ri => ri.CurrencyEnglishName));
            ViewData["AllCurrencyRegions"] = allCurrencyRegionsSorted;
            ViewData["EnabledCurrencies"] = SiteClient.GetCurrencies();
            return View();
        }

        #endregion

        #region Fees

        /// <summary>
        /// Displays form and processes request to edit basic fee properties in a consolidated form
        /// </summary>
        /// <returns>View(SiteFeeAmounts)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditFees()
        {
            //Standard Flat Fee list (not including Event Fees (post/final),
            //  Locations (Featured), or Decorations (Bold, Hightlight, Italic)
            List<string> standardFlatFeeNames = new List<string>();
            standardFlatFeeNames.Add(Strings.FeeNames.Subtitle);
            standardFlatFeeNames.Add(Strings.FeeNames.FirstImage);
            standardFlatFeeNames.Add(Strings.FeeNames.AdditionalImages);
            standardFlatFeeNames.Add(Strings.FeeNames.YouTubeVideo);

            //if (SiteClient.EnableEvents)
            //{
            //    standardFlatFeeNames.Add(Strings.FeeNames.PublishEventFee);
            //}

            List<FeeProperty> allFeeProperties = SiteClient.GetFeeProperties();
            List<FeeProperty> standardFlatFeeProps = new List<FeeProperty>(); //the first instance, per unique fee name
            foreach (string feeName in standardFlatFeeNames)
            {
                FeeProperty feeToAdd = allFeeProperties.Where(f => f.Name == feeName).FirstOrDefault();
                if (feeToAdd != null)
                {
                    if (!standardFlatFeeProps.Exists(f => f.Name == feeToAdd.Name))
                    {
                        standardFlatFeeProps.Add(feeToAdd);
                    }
                }
                else
                {
                    //missing - add placeholder
                    FeeProperty newFeeProperty = new FeeProperty();
                    switch (feeName)
                    {
                        case Strings.FeeNames.AdditionalImages:
                            newFeeProperty.Name = Strings.FeeNames.AdditionalImages;
                            newFeeProperty.Description = "Each Additional Image Fee";
                            newFeeProperty.Amount = 0.00m;
                            break;
                        case Strings.FeeNames.FirstImage:
                            newFeeProperty.Name = Strings.FeeNames.FirstImage;
                            newFeeProperty.Description = "First Image Fee";
                            newFeeProperty.Amount = 0.00m;
                            break;
                        case Strings.FeeNames.Subtitle:
                            newFeeProperty.Name = Strings.FeeNames.Subtitle;
                            newFeeProperty.Description = "Listing Subtitle";
                            newFeeProperty.Amount = 0.00m;
                            break;
                        case Strings.FeeNames.YouTubeVideo:
                            newFeeProperty.Name = Strings.FeeNames.YouTubeVideo;
                            newFeeProperty.Description = "YouTube Video";
                            newFeeProperty.Amount = 0.00m;
                            break;
                    }
                    standardFlatFeeProps.Add(newFeeProperty);
                }
            }
            List<string> nonStandardFlatFeeNames = new List<string>();
            List<FeeProperty> nonStandardFlatFeeProps = new List<FeeProperty>(); //the first instance, per unique fee name
            foreach (FeeProperty feeToAdd in allFeeProperties)
            {
                if (!standardFlatFeeNames.Exists(n => n == feeToAdd.Name) &&
                    !nonStandardFlatFeeNames.Exists(n => n == feeToAdd.Name))
                {
                    nonStandardFlatFeeProps.Add(feeToAdd);
                    nonStandardFlatFeeNames.Add(feeToAdd.Name);
                }
            }

            if (!string.IsNullOrEmpty(Request.Form["SaveChanges"]))
            {
                try
                {
                    //process the form
                    UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
                    foreach (string key in Request.Form.AllKeys.Where(k => k != null && k.StartsWith("Row_")))
                    {
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                    }

                    //capture the form value for "UseAggregateFinalPercentageFees"
                    if (Request.Form.AllKeys.Contains(SiteProperties.UseAggregateFinalPercentageFees))
                    {
                        string key = SiteProperties.UseAggregateFinalPercentageFees;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                    }
                    //capture the form value for "UseFlatFinalFees"
                    bool useFlatFinalFeesRequested = false;
                    if (Request.Form.AllKeys.Contains(SiteProperties.UseFlatFinalFees))
                    {
                        string key = SiteProperties.UseFlatFinalFees;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                        bool.TryParse(input.Items[key], out useFlatFinalFeesRequested);
                    }
                    //capture the form value for "UseAggregateBuyerFinalPercentageFees"
                    if (Request.Form.AllKeys.Contains(SiteProperties.UseAggregateBuyerFinalPercentageFees))
                    {
                        string key = SiteProperties.UseAggregateBuyerFinalPercentageFees;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                    }
                    //capture the form value for "UseFlatBuyerFinalFees"
                    bool useFlatBuyerFinalFeesRequested = false;
                    if (Request.Form.AllKeys.Contains(SiteProperties.UseFlatBuyerFinalFees))
                    {
                        string key = SiteProperties.UseFlatBuyerFinalFees;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                        bool.TryParse(input.Items[key], out useFlatBuyerFinalFeesRequested);
                    }

                    //capture the form value for "MinSellerFinalFee"
                    if (Request.Form.AllKeys.Contains(SiteProperties.MinSellerFinalFee))
                    {
                        string key = SiteProperties.MinSellerFinalFee;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                    }
                    //capture the form value for "MaxSellerFinalFee"
                    if (Request.Form.AllKeys.Contains(SiteProperties.MaxSellerFinalFee))
                    {
                        string key = SiteProperties.MaxSellerFinalFee;
                        input.Items.Add(key,
                                        Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : Request.Form[key].Trim());
                    }

                    //Pay to Proceed checkbox (applies to all fees)
                    string ptpKey = Strings.Fields.GlobalPayToProceed;//"GlobalPayToProceed";
                    bool globalPayToProceed = (Request.Form[ptpKey] == Strings.MVC.TrueFormValue);

                    //convert user input to validated fee amounts container object
                    SiteFeeAmounts newFees = new SiteFeeAmounts(input);

                    //Save new tiered fee values 

                    //drop all FeeSchedule tiers
                    foreach (FeeSchedule fs in SiteClient.FeeSchedules)
                        foreach (Tier t in fs.Tiers)
                            SiteClient.DeleteFeeTier(input.ActingUserName, t.ID);

                    //post fees (also includes pro-rated post fee on listing update when price is changed)
                    int addListingEventId = SiteClient.Events.Where(
                        ev => ev.Name == Strings.Events.AddListing).SingleOrDefault().ID;
                    int updateListingEventId = SiteClient.Events.Where(
                        ev => ev.Name == Strings.Events.UpdateListing).Single().ID;
                    List<int> postFeeEvents = new List<int>(2) { addListingEventId, updateListingEventId };
                    TieredFlatFee postFee = (TieredFlatFee)newFees.Fees.Where(
                        f => f.Name == Strings.FeeNames.PostListingFee).SingleOrDefault();

                    foreach (int eventId in postFeeEvents)
                    {
                        foreach (ListingType lt in ListingClient.ListingTypes)
                        {
                            FeeSchedule fs;

                            fs = SiteClient.FeeSchedules.Where(
                                f => f.ListingType.Name == lt.Name
                                    && f.Event.ID == eventId).SingleOrDefault();

                            if (fs != null)
                            {
                                //update description
                                SiteClient.UpdateEventFee(input.ActingUserName, fs.ID, lt.ID, fs.Event.ID
                                    , globalPayToProceed, postFee.Description, Strings.Roles.Seller);
                            }
                            else
                            {
                                //create FeeSchedule
                                SiteClient.AddEventFee(input.ActingUserName, lt.ID, eventId
                                    , globalPayToProceed, postFee.Description, Strings.Roles.Seller);
                                //re-get fee schedule
                                fs = SiteClient.FeeSchedules.Where(f => f.ListingType.Name == lt.Name && f.Event.ID == eventId).SingleOrDefault();
                            }
                            //add or re-add tiers as needed
                            decimal lastUpperBoundAmount = 0;
                            foreach (FlatFeeTier fft in postFee.FeeAmountTiers.OrderBy(t => t.UpperBound))
                            {
                                decimal newLowerBoundAmount = lastUpperBoundAmount;
                                decimal newUpperBound;
                                if (fft.UpperBound.HasValue)
                                    newUpperBound = fft.UpperBound.Value;
                                else
                                    newUpperBound = decimal.MaxValue;
                                SiteClient.AddFeeTier(input.ActingUserName, fs.ID
                                    , newLowerBoundAmount, newUpperBound, fft.FeeAmount, Strings.TierTypes.Fixed);
                                lastUpperBoundAmount = newUpperBound;
                            }
                        }
                    }

                    //final sale fees (seller)
                    int endListingSuccessEventId = SiteClient.Events.Where(
                        ev => ev.Name == Strings.Events.EndListingSuccess).SingleOrDefault().ID;

                    if (useFlatFinalFeesRequested)
                    {
                        //final sale fee with FLAT tiers
                        TieredFlatFee finalFee = (TieredFlatFee)newFees.Fees.Where(
                            f => f.Name == Strings.FeeNames.FinalSaleFee).SingleOrDefault();

                        foreach (ListingType lt in ListingClient.ListingTypes)
                        {
                            FeeSchedule fs = SiteClient.FeeSchedules.Where(
                                f => f.ListingType.Name == lt.Name
                                    && f.Event.ID == endListingSuccessEventId
                                    && f.Name == Roles.Seller
                                    ).SingleOrDefault();

                            if (fs != null)
                            {
                                //update description
                                SiteClient.UpdateEventFee(input.ActingUserName, fs.ID, lt.ID, fs.Event.ID
                                    , globalPayToProceed, finalFee.Description, Roles.Seller);
                            }
                            else
                            {
                                //create FeeSchedule
                                SiteClient.AddEventFee(input.ActingUserName, lt.ID, endListingSuccessEventId
                                    , globalPayToProceed, finalFee.Description, Roles.Seller);
                                //re-get fee schedule
                                fs = SiteClient.FeeSchedules.Where(
                                    f => f.ListingType.Name == lt.Name
                                        && f.Event.ID == endListingSuccessEventId
                                        && f.Name == Roles.Seller
                                        ).SingleOrDefault();
                            }
                            //add or re-add tiers as needed
                            decimal lastUpperBoundAmount = 0;
                            foreach (FlatFeeTier fft in finalFee.FeeAmountTiers.OrderBy(t => t.UpperBound == null ? decimal.MaxValue : t.UpperBound.Value))
                            {
                                decimal newLowerBoundAmount = lastUpperBoundAmount;
                                decimal newUpperBound;
                                if (fft.UpperBound.HasValue)
                                    newUpperBound = fft.UpperBound.Value;
                                else
                                    newUpperBound = decimal.MaxValue;
                                SiteClient.AddFeeTier(input.ActingUserName, fs.ID, newLowerBoundAmount
                                    , newUpperBound, fft.FeeAmount, Strings.TierTypes.Fixed);
                                lastUpperBoundAmount = newUpperBound;
                            }
                        }
                    }
                    else
                    {
                        //final sale fee with PERCENT tiers
                        TieredPercentFee finalFee = (TieredPercentFee)newFees.Fees.Where(
                            f => f.Name == Strings.FeeNames.FinalSaleFee).SingleOrDefault();

                        foreach (ListingType lt in ListingClient.ListingTypes)
                        {
                            FeeSchedule fs = SiteClient.FeeSchedules.Where(
                                f => f.ListingType.Name == lt.Name
                                    && f.Event.ID == endListingSuccessEventId
                                    && f.Name == Roles.Seller
                                    ).SingleOrDefault();

                            if (fs != null)
                            {
                                //update description
                                SiteClient.UpdateEventFee(input.ActingUserName, fs.ID, lt.ID, fs.Event.ID
                                    , globalPayToProceed, finalFee.Description, Roles.Seller);
                            }
                            else
                            {
                                //create FeeSchedule
                                SiteClient.AddEventFee(input.ActingUserName, lt.ID, endListingSuccessEventId
                                    , globalPayToProceed, finalFee.Description, Roles.Seller);
                                //re-get fee schedule
                                fs = SiteClient.FeeSchedules.Where(
                                        f => f.ListingType.Name == lt.Name
                                            && f.Event.ID == endListingSuccessEventId
                                            && f.Name == Roles.Seller
                                        ).SingleOrDefault();
                            }
                            //add or re-add tiers as needed
                            decimal lastUpperBoundAmount = 0;
                            foreach (PercentFeeTier pft in finalFee.FeePercentTiers.OrderBy(t => t.UpperBound == null ? decimal.MaxValue : t.UpperBound.Value))
                            {
                                decimal newLowerBoundAmount = lastUpperBoundAmount;
                                decimal newUpperBound;
                                if (pft.UpperBound.HasValue)
                                    newUpperBound = pft.UpperBound.Value;
                                else
                                    newUpperBound = decimal.MaxValue;
                                SiteClient.AddFeeTier(input.ActingUserName, fs.ID, newLowerBoundAmount
                                    , newUpperBound, (pft.FeePercent / 100), Strings.TierTypes.Percent);
                                lastUpperBoundAmount = newUpperBound;
                            }
                        }
                    }

                    //final sale fees (buyer)
                    if (useFlatBuyerFinalFeesRequested)
                    {
                        //final sale fee with FLAT tiers
                        TieredFlatFee finalFee = (TieredFlatFee)newFees.Fees.Where(
                            f => f.Name == Strings.FeeNames.FinalBuyerFee).SingleOrDefault();

                        foreach (ListingType lt in ListingClient.ListingTypes)
                        {
                            FeeSchedule fs = SiteClient.FeeSchedules.Where(
                                f => f.ListingType.Name == lt.Name
                                    && f.Event.ID == endListingSuccessEventId
                                    && f.Name == Roles.Buyer
                                    ).SingleOrDefault();

                            if (fs != null)
                            {
                                //update description
                                SiteClient.UpdateEventFee(input.ActingUserName, fs.ID, lt.ID, fs.Event.ID
                                    , globalPayToProceed, finalFee.Description, Roles.Buyer);
                            }
                            else
                            {
                                //create FeeSchedule
                                SiteClient.AddEventFee(input.ActingUserName, lt.ID, endListingSuccessEventId
                                    , globalPayToProceed, finalFee.Description, Roles.Buyer);
                                //re-get fee schedule
                                fs = SiteClient.FeeSchedules.Where(
                                    f => f.ListingType.Name == lt.Name
                                        && f.Event.ID == endListingSuccessEventId
                                        && f.Name == Roles.Buyer
                                        ).SingleOrDefault();
                            }
                            //add or re-add tiers as needed
                            decimal lastUpperBoundAmount = 0;
                            foreach (FlatFeeTier fft in finalFee.FeeAmountTiers.OrderBy(t => t.UpperBound == null ? decimal.MaxValue : t.UpperBound.Value))
                            {
                                decimal newLowerBoundAmount = lastUpperBoundAmount;
                                decimal newUpperBound;
                                if (fft.UpperBound.HasValue)
                                    newUpperBound = fft.UpperBound.Value;
                                else
                                    newUpperBound = decimal.MaxValue;
                                SiteClient.AddFeeTier(input.ActingUserName, fs.ID, newLowerBoundAmount
                                    , newUpperBound, fft.FeeAmount, Strings.TierTypes.Fixed);
                                lastUpperBoundAmount = newUpperBound;
                            }
                        }
                    }
                    else
                    {
                        //final sale fee with PERCENT tiers
                        TieredPercentFee finalFee = (TieredPercentFee)newFees.Fees.Where(
                            f => f.Name == Strings.FeeNames.FinalBuyerFee).SingleOrDefault();

                        foreach (ListingType lt in ListingClient.ListingTypes)
                        {
                            FeeSchedule fs = SiteClient.FeeSchedules.Where(
                                f => f.ListingType.Name == lt.Name
                                    && f.Event.ID == endListingSuccessEventId
                                    && f.Name == Roles.Buyer
                                    ).SingleOrDefault();

                            if (fs != null)
                            {
                                //update description
                                SiteClient.UpdateEventFee(input.ActingUserName, fs.ID, lt.ID, fs.Event.ID
                                    , globalPayToProceed, finalFee.Description, Roles.Buyer);
                            }
                            else
                            {
                                //create FeeSchedule
                                SiteClient.AddEventFee(input.ActingUserName, lt.ID, endListingSuccessEventId
                                    , globalPayToProceed, finalFee.Description, Roles.Buyer);
                                //re-get fee schedule
                                fs = SiteClient.FeeSchedules.Where(
                                        f => f.ListingType.Name == lt.Name
                                            && f.Event.ID == endListingSuccessEventId
                                            && f.Name == Roles.Buyer
                                        ).SingleOrDefault();
                            }
                            //add or re-add tiers as needed
                            decimal lastUpperBoundAmount = 0;
                            foreach (PercentFeeTier pft in finalFee.FeePercentTiers.OrderBy(t => t.UpperBound == null ? decimal.MaxValue : t.UpperBound.Value))
                            {
                                decimal newLowerBoundAmount = lastUpperBoundAmount;
                                decimal newUpperBound;
                                if (pft.UpperBound.HasValue)
                                    newUpperBound = pft.UpperBound.Value;
                                else
                                    newUpperBound = decimal.MaxValue;
                                SiteClient.AddFeeTier(input.ActingUserName, fs.ID, newLowerBoundAmount
                                    , newUpperBound, (pft.FeePercent / 100), Strings.TierTypes.Percent);
                                lastUpperBoundAmount = newUpperBound;
                            }
                        }
                    }

                    //locations
                    List<Location> currentLocations = SiteClient.Locations;
                    foreach (Location l in currentLocations)
                    {
                        FlatFee ff = (FlatFee)newFees.Fees.Where(f => f.Name == l.Name).SingleOrDefault();
                        SiteClient.UpdateLocation(input.ActingUserName, l.ID
                            , ff.Description, ff.FeeAmount, globalPayToProceed);
                    }

                    //decorations
                    List<Decoration> currentDecorations = SiteClient.Decorations;
                    foreach (Decoration d in currentDecorations)
                    {
                        FlatFee ff = (FlatFee)newFees.Fees.Where(f => f.Name == d.Name).SingleOrDefault();
                        SiteClient.UpdateDecoration(input.ActingUserName, d.ID, d.Name
                            , ff.Description, ff.FeeAmount, d.FormatString, d.ValidFields, globalPayToProceed);
                    }

                    //other flat fees (e.g. "Images", "Subtitle", etc.)
                    List<FeeProperty> feePropertiesToUpdate = new List<FeeProperty>();

                    //make list of applicable events ("add listing" or "update listing")
                    List<ListItem> feePropEvents = SiteClient.Events.Where(ev =>
                           ev.Name == Strings.Events.AddListing
                        || ev.Name == Strings.Events.UpdateListing).ToList();

                    //standard flat fees
                    //  add one Fee Property per listing type, per applicable event, per standrad fee name
                    foreach (ListingType lt in ListingClient.ListingTypes)
                    {
                        foreach (ListItem ev in feePropEvents)
                        {
                            foreach (string stdFeeName in standardFlatFeeNames)
                            {
                                FlatFee ff = (FlatFee)newFees.Fees.Where(f => f.Name == stdFeeName && f.FeeType == "FlatFee").SingleOrDefault();
                                if (ff != null)
                                {
                                    FeeProperty updatedProperty = allFeeProperties.Where(
                                        fp => fp.ListingType.Name == lt.Name
                                           && fp.Event.Name == ev.Name
                                           && fp.Name == stdFeeName).SingleOrDefault();
                                    if (updatedProperty == null)
                                    {
                                        updatedProperty = new FeeProperty();
                                        updatedProperty.Name = stdFeeName;
                                        updatedProperty.ListingType = new ListItem { ID = lt.ID, Name = lt.Name, Enabled = lt.Enabled };
                                        updatedProperty.Event = ev;
                                    }
                                    updatedProperty.Description = ff.Description;
                                    updatedProperty.Amount = ff.FeeAmount;
                                    switch (stdFeeName)
                                    {
                                        case Strings.FeeNames.AdditionalImages:
                                            updatedProperty.Processor = "RainWorx.FrameWorx.Providers.Fee.Standard.ImageCount";
                                            break;
                                        case Strings.FeeNames.FirstImage:
                                            updatedProperty.Processor = "RainWorx.FrameWorx.Providers.Fee.Standard.ImageCount";
                                            break;
                                        case Strings.FeeNames.Subtitle:
                                            updatedProperty.Processor = "RainWorx.FrameWorx.Providers.Fee.Standard.SubtitleExists";
                                            break;
                                        case Strings.FeeNames.YouTubeVideo:
                                            updatedProperty.Processor = "RainWorx.FrameWorx.Providers.Fee.Standard.YouTube";
                                            break;
                                    }
                                    feePropertiesToUpdate.Add(updatedProperty);
                                }
                            }
                        }
                    }

                    //non standard flat fees
                    //  update existing fee properties, but do not propagate per listing type and event, if missing
                    foreach (string nonStdFeeName in nonStandardFlatFeeNames)
                    {
                        FlatFee ff = (FlatFee)newFees.Fees.Where(f => f.Name == nonStdFeeName && f.FeeType == "FlatFee").SingleOrDefault();
                        if (ff != null)
                        {
                            foreach (FeeProperty updatedProperty in allFeeProperties.Where(fp => fp.Name == nonStdFeeName))
                            {
                                updatedProperty.Description = ff.Description;
                                updatedProperty.Amount = ff.FeeAmount;
                                feePropertiesToUpdate.Add(updatedProperty);
                            }
                        }
                    }

                    SiteClient.UpdateAllFeeProperties(input.ActingUserName, feePropertiesToUpdate);

                    if (input.Items.ContainsKey(SiteProperties.UseAggregateFinalPercentageFees))
                    {
                        string newValue = input.Items[SiteProperties.UseAggregateFinalPercentageFees];
                        SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.UseAggregateFinalPercentageFees,
                                                 newValue, input.CultureName);
                    }
                    if (input.Items.ContainsKey(SiteProperties.UseFlatFinalFees))
                    {
                        string newValue = input.Items[SiteProperties.UseFlatFinalFees];
                        SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.UseFlatFinalFees, newValue,
                                                 input.CultureName);
                    }

                    if (input.Items.ContainsKey(SiteProperties.UseAggregateBuyerFinalPercentageFees))
                    {
                        string newValue = input.Items[SiteProperties.UseAggregateBuyerFinalPercentageFees];
                        SiteClient.UpdateSetting(input.ActingUserName,
                                                 SiteProperties.UseAggregateBuyerFinalPercentageFees, newValue,
                                                 input.CultureName);
                    }
                    if (input.Items.ContainsKey(SiteProperties.UseFlatBuyerFinalFees))
                    {
                        string newValue = input.Items[SiteProperties.UseFlatBuyerFinalFees];
                        SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.UseFlatBuyerFinalFees, newValue,
                                                 input.CultureName);
                    }

                    if (SiteClient.EnableEvents && input.Items.Any(kvp => kvp.Value == FeeNames.PublishEventFee))
                    {
                        string feeRowKey = input.Items.Single(kvp => kvp.Value == FeeNames.PublishEventFee).Key;
                        string feeDescKey = feeRowKey.Replace("FeeName", "FeeDesc");
                        string feeAmountKey = feeRowKey.Replace("FeeName", "FeeAmount");

                        if (input.Items.ContainsKey(feeDescKey))
                        {
                            string newValue = input.Items[feeDescKey];
                            SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.EventPublishFee_Description, newValue,
                                                     input.CultureName);
                        }

                        if (input.Items.ContainsKey(feeAmountKey))
                        {
                            string newValue = input.Items[feeAmountKey];
                            SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.EventPublishFee_Amount, newValue,
                                                     input.CultureName);
                        }
                    }

                    //update site property "PayToProceed"
                    SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.PayToProceed, globalPayToProceed.ToString(),
                                             input.CultureName);

                    if (input.Items.ContainsKey(SiteProperties.MinSellerFinalFee))
                    {
                        string newValue = input.Items[SiteProperties.MinSellerFinalFee];
                        SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.MinSellerFinalFee, newValue,
                                                 input.CultureName);
                    }
                    if (input.Items.ContainsKey(SiteProperties.MaxSellerFinalFee))
                    {
                        string newValue = input.Items[SiteProperties.MaxSellerFinalFee];
                        SiteClient.UpdateSetting(input.ActingUserName, SiteProperties.MaxSellerFinalFee, newValue,
                                                 input.CultureName);
                    }

                    //finally, clear the cache so all new fee values are reloaded
                    SiteClient.Reset();

                    //success!
                    PrepareSuccessMessage("EditFees", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors                
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                    PrepareErrorMessage("EditFees", MessageType.Method);
                }
                catch (Exception)
                {
                    //handle other errors
                    PrepareErrorMessage("EditFees", MessageType.Method);
                }
            } // save changes

            //set form values
            SiteFeeAmounts fees = new SiteFeeAmounts();

            if (SiteClient.EnableEvents)
            {
                fees.Fees.Add(new FlatFee()
                {
                    Name = Strings.FeeNames.PublishEventFee,
                    Description = SiteClient.TextSetting(SiteProperties.EventPublishFee_Description),
                    FeeAmount = SiteClient.DecimalSetting(SiteProperties.EventPublishFee_Amount)
                });
            }

            bool? currenGlobalPayToProceed = null;

            List<ListingType> enabledListingTypes =
                ListingClient.ListingTypes.Where(t => t.Enabled == true).ToList();

            //find first enabled listing type
            string firstEnabledType;
            if (enabledListingTypes.Count > 0)
                firstEnabledType = enabledListingTypes[0].Name;
            else
                firstEnabledType = ListingTypes.Auction;

            //flat post fee tiers
            List<FeeSchedule> allFeeSchedules = SiteClient.FeeSchedules;
            FeeSchedule postFeeTiers = allFeeSchedules.Where(fs =>
                fs.ListingType.Name == firstEnabledType &&
                fs.Event.Name == Events.AddListing).SingleOrDefault();
            if (postFeeTiers != null)
            {
                fees.AddTieredFlatFee(postFeeTiers, Strings.FeeNames.PostListingFee);
                if (!currenGlobalPayToProceed.HasValue)
                    currenGlobalPayToProceed = postFeeTiers.PayToProceed;
            }

            //final sale percent fee tiers
            FeeSchedule finalFeeTiers = allFeeSchedules.Where(
                fs => fs.ListingType.Name == firstEnabledType
                    && fs.Event.Name == Events.EndListingSuccess
                    && fs.Name == Roles.Seller
                ).SingleOrDefault();
            if (finalFeeTiers != null)
            {
                if (SiteClient.BoolSetting(SiteProperties.UseFlatFinalFees))
                {
                    fees.AddTieredFlatFee(finalFeeTiers, FeeNames.FinalSaleFee);
                }
                else
                {
                    fees.AddTieredPercentFee(finalFeeTiers, FeeNames.FinalSaleFee);
                }
                if (!currenGlobalPayToProceed.HasValue)
                    currenGlobalPayToProceed = postFeeTiers.PayToProceed;
            }

            //final buyer fee tiers
            FeeSchedule finalBuyerFeeTiers = allFeeSchedules.Where(
                fs => fs.ListingType.Name == firstEnabledType
                    && fs.Event.Name == Events.EndListingSuccess
                    && fs.Name == Roles.Buyer
                ).SingleOrDefault();
            if (finalBuyerFeeTiers != null)
            {
                if (SiteClient.BoolSetting(SiteProperties.UseFlatBuyerFinalFees))
                {
                    fees.AddTieredFlatFee(finalBuyerFeeTiers, FeeNames.FinalBuyerFee);
                }
                else
                {
                    fees.AddTieredPercentFee(finalBuyerFeeTiers, FeeNames.FinalBuyerFee);
                }
                if (!currenGlobalPayToProceed.HasValue)
                    currenGlobalPayToProceed = postFeeTiers.PayToProceed;
            }

            //location flat fees
            fees.AddFlatFees(SiteClient.Locations);

            //decoration flat fees
            fees.AddFlatFees(SiteClient.Decorations);

            //standard flat fees (propagated to all listing types for both "AddListing" and "EditListing" events)
            fees.AddFlatFees(standardFlatFeeProps);

            //nonstandard flat fees (only updated if they exist)
            fees.AddFlatFees(nonStandardFlatFeeProps);

            if (currenGlobalPayToProceed.HasValue)
                fees.PayToProceed = currenGlobalPayToProceed.Value;
            else
                //last resort default global PayToProceed value (if no tiered fees exist yet for some reason)
                fees.PayToProceed = false;

            var currentCultureInfo = this.GetCultureInfo();
            ViewData[SiteProperties.MinSellerFinalFee] = SiteClient.Properties.GetPropertyValue(SiteProperties.MinSellerFinalFee, 0.0M).ToString("N2", currentCultureInfo);
            ViewData[SiteProperties.MaxSellerFinalFee] = SiteClient.Properties.GetPropertyValue(SiteProperties.MaxSellerFinalFee, 0.0M).ToString("N2", currentCultureInfo);

            return View(fees);
        }

        /// <summary>
        /// Displays form to add, edit or delete event fees
        /// </summary>
        /// <returns>List&lt;EventFee&gt;</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EventFeeManagement()
        {
            //if (TempData["ViewData"] != null)
            //{
            //    ViewData = (ViewDataDictionary)TempData["ViewData"];
            //}

            List<FeeSchedule> feeSchedules = SiteClient.FeeSchedules;

            IEnumerable<Models.EventFee> eventFees = from fs in feeSchedules
                                                     select new Models.EventFee()
                                                     {
                                                         Event = fs.Event.Name,
                                                         Description = fs.Description,
                                                         ID = fs.ID,
                                                         ListingType = fs.ListingType.Name,
                                                         PayToProceed = fs.PayToProceed
                                                     };

            ViewData[Strings.Fields.Event] = new SelectList(SiteClient.Events, Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.ListingType] = new SelectList(ListingClient.ListingTypes, Strings.Fields.ID, Strings.Fields.Name);

            return View(Strings.MVC.EventFeeManagementAction, eventFees.ToList());
        }

        /// <summary>
        /// Processes request to add a new EventFee
        /// </summary>
        /// <param name="ListingType">ID of the selected listing type</param>
        /// <param name="Event">ID of the selected Event</param>
        /// <param name="PayToProceed">Indicates whether immediate fee payment is required before the event is processed</param>
        /// <param name="Description">The text to be displayed to end users in the line item of the invoice</param>
        /// <param name="Name">The name of the Fee Schedule </param>
        /// <returns>Redirect to /Admin/EventFeeManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AddEventFee(int ListingType, int Event, bool PayToProceed, string Description, string Name)
        {
            try
            {
                //description required
                if (string.IsNullOrEmpty(Description))
                {
                    ModelState.AddModelError("Description", "Description_Required");
                }

                if (!ModelState.IsValid)
                {
                    //TempData["ViewData"] = ViewData;
                    //return RedirectToAction(Strings.MVC.EventFeeManagementAction);
                    return EventFeeManagement();
                }

                SiteClient.AddEventFee(User.Identity.Name, ListingType, Event, PayToProceed, Description, Name);
                SiteClient.Reset();
                PrepareSuccessMessage("AddEventFee", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AddEventFee", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EventFeeManagementAction);
        }

        /// <summary>
        /// Displays form to edit the specified event fee
        /// </summary>
        /// <param name="id">ID of the event fee to be edited</param>
        /// <returns>View(FeeSchedule)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditEventFees(int id)
        {
            //if (TempData["ViewData"] != null)
            //{
            //    ViewData = (ViewDataDictionary)TempData["ViewData"];
            //}

            FeeSchedule currentFeeSchedule = SiteClient.FeeSchedules.Where(fs => (int)fs.ID == id).SingleOrDefault();

            ViewData[Strings.Fields.Event] = new SelectList(
                SiteClient.Events, Strings.Fields.ID, Strings.Fields.Name, currentFeeSchedule.Event.ID);
            ViewData[Strings.Fields.ListingType] = new SelectList(
                ListingClient.ListingTypes, Strings.Fields.ID, Strings.Fields.Name, currentFeeSchedule.ListingType.ID);

            List<string> valueTypeOptions = new List<string>(2);
            valueTypeOptions.Add(Strings.TierTypes.Fixed);
            valueTypeOptions.Add(Strings.TierTypes.Percent);
            ViewData[Strings.Fields.ValueType] = new SelectList(valueTypeOptions);
            return View(Strings.MVC.EditEventFeesAction, currentFeeSchedule);
        }

        /// <summary>
        /// Processes request to delete an existing EventFee
        /// </summary>
        /// <param name="id">ID of the EventFee to be deleted</param>
        /// <returns>Redirect to /Admin/EventFeeManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteEventFee(int id)
        {
            try
            {
                SiteClient.DeleteEventFee(User.Identity.Name, id);
                SiteClient.Reset();
                PrepareSuccessMessage("DeleteEventFee", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteEventFee", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.EventFeeManagementAction);
        }

        /// <summary>
        /// Processes request to edit an existing EventFee
        /// </summary>
        /// <param name="id">ID of the EventFee to be edited</param>
        /// <param name="ListingType">ID of the selected listing type</param>
        /// <param name="Event">ID of the selected Event</param>
        /// <param name="PayToProceed">Indicates whether immediate fee payment is required before the event is processed</param>
        /// <param name="Description">The text to be displayed to end users in the line item of the invoice</param>
        /// <param name="Name">the name of the fee schedule </param>
        /// <returns>Redirect to /Admin/EditEventFees/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditEventFees(int id, int ListingType, int Event, bool PayToProceed, string Description, string Name)
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {

                    //TODO: Move validation logic to BLL

                    //description required
                    if (string.IsNullOrEmpty(Description))
                    {
                        ModelState.AddModelError("Description", "Description_Required");
                    }

                    if (!ModelState.IsValid)
                    {
                        //TempData["ViewData"] = ViewData;
                        //return RedirectToAction(Strings.MVC.EditEventFeesAction);
                        return EditEventFees(id);
                    }

                    //Save
                    SiteClient.UpdateEventFee(User.Identity.Name, id, ListingType, Event, PayToProceed, Description, Name);
                }

                SiteClient.Reset();
                PrepareSuccessMessage("EditEventFees", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("EditEventFees", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.EditEventFeesAction, new { id });
        }

        /// <summary>
        /// Processes request to add a new fee tier
        /// </summary>
        /// <param name="id">ID of the requested FeeSchedule to which this tier will be assigned</param>
        /// <param name="LowerBoundInclusive">decimal value of the lower bound for this tier</param>
        /// <param name="UpperBoundExclusive">decimal value of the upper bound for this tier</param>
        /// <param name="Value">decimal value of the fee amount for this tier</param>
        /// <param name="ValueType">string value indicating the fee amount type ("Fixed" or "Percent")</param>
        /// <returns>Redirect to /Admin/EditEventFees/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AddFeeTier(int id, string LowerBoundInclusive, string UpperBoundExclusive, string Value, string ValueType)
        {
            try
            {
                decimal _LowerBoundInclusive = 0;
                decimal _UpperBoundExclusive = 0;
                decimal _Value = 0;

                //TODO: Move validation logic to BLL

                //amount required, convertible to decimal and >= 0
                if (string.IsNullOrEmpty(LowerBoundInclusive))
                {
                    ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_Required");
                }
                else
                {
                    if (decimal.TryParse(LowerBoundInclusive, out _LowerBoundInclusive))
                    {
                        if (_LowerBoundInclusive < 0)
                        {
                            ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_GTEZero");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_ConvertDecimal");
                    }
                }
                //amount required, convertible to decimal and >= 0
                if (string.IsNullOrEmpty(UpperBoundExclusive))
                {
                    ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_Required");
                }
                else
                {
                    if (decimal.TryParse(UpperBoundExclusive, out _UpperBoundExclusive))
                    {
                        if (_UpperBoundExclusive < 0)
                        {
                            ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_GTEZero");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_ConvertDecimal");
                    }
                }
                //amount required, convertible to decimal and >= 0
                if (string.IsNullOrEmpty(Value))
                {
                    ModelState.AddModelError("Value", "Value_Required");
                }
                else
                {
                    if (decimal.TryParse(Value, out _Value))
                    {
                        if (_Value < 0)
                        {
                            ModelState.AddModelError("Value", "Value_GTEZero");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("Value", "Value_ConvertDecimal");
                    }
                }

                //TODO: Raise a validation error if ValueType is anything other than "Fixed" or "Percent"

                if (!ModelState.IsValid)
                {
                    //TempData["ViewData"] = ViewData;
                    //return RedirectToAction(Strings.MVC.EditEventFeesAction, new { id });
                    return EditEventFees(id);
                }

                SiteClient.AddFeeTier(User.Identity.Name, id, _LowerBoundInclusive, _UpperBoundExclusive, _Value, ValueType);
                SiteClient.Reset();
                PrepareSuccessMessage("AddFeeTier", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AddFeeTier", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditEventFeesAction, new { id = id });
        }

        /// <summary>
        /// Displays form to edit the specified fee tier
        /// </summary>
        /// <param name="id">ID of the requested fee tier</param>
        /// <returns>View(Tier)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditFeeTier(int id)
        {
            //if (TempData["ViewData"] != null)
            //{
            //    ViewData = (ViewDataDictionary)TempData["ViewData"];
            //}

            List<Tier> tiers = new List<Tier>();
            foreach (FeeSchedule feeSchedule in SiteClient.FeeSchedules)
            {
                tiers.AddRange(feeSchedule.Tiers);
            }
            Tier currentTier = tiers.Where(t => (int)t.ID == id).SingleOrDefault();

            FeeSchedule currentFeeSchedule = SiteClient.FeeSchedules.Where(fs => fs.Tiers.Contains(currentTier)).SingleOrDefault();
            ViewData[Strings.Fields.FeeScheduleID] = currentFeeSchedule.ID;

            List<string> valueTypeOptions = new List<string>(2);
            valueTypeOptions.Add(Strings.TierTypes.Fixed);
            valueTypeOptions.Add(Strings.TierTypes.Percent);
            ViewData[Strings.Fields.ValueType] = new SelectList(valueTypeOptions, currentTier.ValueType);
            return View(currentTier);
        }

        /// <summary>
        /// Processes request to edit a fee tier
        /// </summary>
        /// <param name="id">ID of the requested fee tier</param>
        /// <param name="feeScheduleID">ID of the requested FeeSchedule to which this tier will be assigned</param>
        /// <param name="LowerBoundInclusive">decimal value of the lower bound for this tier</param>
        /// <param name="UpperBoundExclusive">decimal value of the upper bound for this tier</param>
        /// <param name="Value">decimal value of the fee amount for this tier</param>
        /// <param name="ValueType">string value indicating the fee amount type ("Fixed" or "Percent")</param>
        /// <returns>Redirect to /Admin/EditEventFees/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditFeeTier(int id, int feeScheduleID, string LowerBoundInclusive, string UpperBoundExclusive, string Value, string ValueType)
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {

                    //TODO: Move validation logic to BLL

                    decimal _LowerBoundInclusive = 0;
                    decimal _UpperBoundExclusive = 0;
                    decimal _Value = 0;

                    //amount required, convertible to decimal and >= 0
                    if (string.IsNullOrEmpty(LowerBoundInclusive))
                    {
                        ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_Required");
                    }
                    else
                    {
                        if (decimal.TryParse(LowerBoundInclusive, out _LowerBoundInclusive))
                        {
                            if (_LowerBoundInclusive < 0)
                            {
                                ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_GTEZero");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("LowerBoundInclusive", "LowerBoundInclusive_ConvertDecimal");
                        }
                    }
                    //amount required, convertible to decimal and >= 0
                    if (string.IsNullOrEmpty(UpperBoundExclusive))
                    {
                        ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_Required");
                    }
                    else
                    {
                        if (decimal.TryParse(UpperBoundExclusive, out _UpperBoundExclusive))
                        {
                            if (_UpperBoundExclusive < 0)
                            {
                                ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_GTEZero");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("UpperBoundExclusive", "UpperBoundExclusive_ConvertDecimal");
                        }
                    }
                    //amount required, convertible to decimal and >= 0
                    if (string.IsNullOrEmpty(Value))
                    {
                        ModelState.AddModelError("Value", "Value_Required");
                    }
                    else
                    {
                        if (decimal.TryParse(Value, out _Value))
                        {
                            if (_Value < 0)
                            {
                                ModelState.AddModelError("Value", "Value_GTEZero");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("Value", "Value_ConvertDecimal");
                        }
                    }

                    if (!ModelState.IsValid)
                    {
                        //TempData["ViewData"] = ViewData;
                        //return RedirectToAction("EditFeeTier", new { id });
                        return EditFeeTier(id);
                    }

                    //Save
                    SiteClient.UpdateFeeTier(User.Identity.Name, id, _LowerBoundInclusive, _UpperBoundExclusive, _Value,
                                             ValueType);
                }

                if (Request.Form[Strings.MVC.SubmitAction_Delete] != null)
                {
                    //Delete
                    SiteClient.DeleteFeeTier(User.Identity.Name, id);
                }

                SiteClient.Reset();
                PrepareSuccessMessage("EditFeeTier", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("EditFeeTier", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditEventFeesAction, new { id = feeScheduleID });
        }

        /// <summary>
        /// Displays and processes request to add category-specific fee exceptions
        /// </summary>
        /// <param name="FeeName">the name of the fee the rest of the parameters apply to (e.g. &quot;PostListingFee&quot;, &quot;FinalSaleFee&quot;, &quot;FinalBuyerFee&quot;, &quot;FlatFee&quot;)</param>
        /// <param name="FeeCategoryMode">whether to -exclude- or -include- the selected categories from charging this fee (e.g. &quot;ExcludeSelected&quot; or &quot;IncludeSelected&quot;)</param>
        /// <param name="FeeCategories">comma-delimited list of category ID's to include or exclude</param>
        /// <param name="SaveChanges">&quot;true&quot; to save changes, otherwise will only display existing settings</param>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult FeeCategories(string FeeName, string FeeCategoryMode, string FeeCategories, bool? SaveChanges)
        {
            if (SaveChanges ?? false)
            {
                //update applicable site properties
                try
                {
                    SiteClient.UpdateSetting(User.Identity.Name, string.Format("FeeCategoryMode_{0}", FeeName), FeeCategoryMode, SiteClient.SiteCulture);
                    SiteClient.UpdateSetting(User.Identity.Name, string.Format("FeeCategories_{0}", FeeName), FeeCategories, SiteClient.SiteCulture);
                    SiteClient.Reset();
                    PrepareSuccessMessage(Strings.MVC.FeeCategoriesAction, MessageType.Method);
                }
                catch (Exception e)
                {
                    PrepareErrorMessage(Strings.MVC.FeeCategoriesAction, e);
                }
            }

            //set defaults
            string defaultFeeName = SiteClient.TextSetting(SiteProperties.CategoryFeeNames).Split(',').FirstOrDefault();
            FeeName = string.IsNullOrEmpty(FeeName) ? (defaultFeeName ?? "PostListingFee") : FeeName;
            ViewData["FeeName"] = FeeName;
            ViewData["FeeCategoryMode"] = SiteClient.TextSetting(string.Format("FeeCategoryMode_{0}", FeeName));
            string feeCatIds = SiteClient.TextSetting(string.Format("FeeCategories_{0}", FeeName));
            ViewData["FeeCategories"] = feeCatIds;

            List<Category> retVal = new List<Category>();

            if (!string.IsNullOrEmpty(feeCatIds))
            {
                foreach (string possibleCatId in feeCatIds.Split(','))
                {
                    int catId;
                    if (int.TryParse(possibleCatId.Trim(), out catId))
                    {
                        var catToAdd = CommonClient.GetCategoryByID(catId);
                        if (catToAdd != null)
                        {
                            retVal.Add(catToAdd);
                        }
                    }
                }
            }
            return View(retVal);
        }

        #endregion

        #region Decorations & Locations

        /// <summary>
        /// Displays form to edit listing Locations, and add or edit listing decorations
        /// </summary>
        /// <returns>View(List&lt;Decoration&gt;</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DecorationsManagement()
        {
            ViewData["Locations"] = SiteClient.Locations;
            return View(SiteClient.Decorations);
        }

        /// <summary>
        /// Processes request to add a new listing Decoration
        /// </summary>
        /// <param name="Name">Name of the decoration</param>
        /// <param name="Description">Description of the decoration</param>
        /// <param name="Amount">Fee Amount to be charged for the decoration</param>
        /// <param name="FormatString">
        /// HTML format string to determine the effect of the decoration
        /// (e.g. "&lt;b&gt;{0}&lt;/b&gt;" for Bold
        /// </param>
        /// <param name="ValidFields">
        /// Comma separated list of Listing Field Names to which this decoration will be applied
        /// (e.g. "ID,Title" if both the Listing # and the Listing Title will be rendered as Bold)
        /// </param>
        /// <param name="PayToProceed">Indicates whether immediate fee payment is required before the decoration is assigned</param>
        /// <returns>Redirect to /Admin/DecorationsManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult AddDecoration(string Name, string Description, string Amount, string FormatString, string ValidFields, bool PayToProceed)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            try
            {

                //TODO: Move validation logic to BLL

                decimal _amount = 0;

                //name required
                if (string.IsNullOrEmpty(Name))
                {
                    ModelState.AddModelError("Name", "Name_Required");
                }
                //description required
                if (string.IsNullOrEmpty(Description))
                {
                    ModelState.AddModelError("Description", "Description_Required");
                }
                //format string required
                if (string.IsNullOrEmpty(FormatString))
                {
                    ModelState.AddModelError("FormatString", "FormatString_Required");
                }
                //validfields required
                if (string.IsNullOrEmpty(ValidFields))
                {
                    ModelState.AddModelError("ValidFields", "ValidFields_Required");
                }
                //amount required, convertible to decimal and >= 0
                if (string.IsNullOrEmpty(Amount))
                {
                    ModelState.AddModelError("Amount", "Amount_Required");
                }
                else
                {
                    if (decimal.TryParse(Amount, out _amount))
                    {
                        if (_amount < 0)
                        {
                            ModelState.AddModelError("Amount", "Amount_GTEZero");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("Amount", "Amount_ConvertDecimal");
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewData["Locations"] = SiteClient.Locations;
                    return View("DecorationsManagement", SiteClient.Decorations);
                }

                SiteClient.AddDecoration(User.Identity.Name, Name, Description, _amount, FormatString, ValidFields, PayToProceed);
                SiteClient.Reset();
                PrepareSuccessMessage("AddDecoration", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AddDecoration", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.DecorationsManagementAction);
        }

        /// <summary>
        /// Displays form to edit the specified decoration
        /// </summary>
        /// <param name="id">ID of the requested decoration</param>
        /// <returns>View(List&lt;Decoration&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditDecoration(int id)
        {
            return View(SiteClient.Decorations.Where(d => (int)d.ID == id).SingleOrDefault());
        }

        /// <summary>
        /// Processes request to edit an existing decoration
        /// </summary>
        /// <param name="id">ID of the requested decoration</param>
        /// <param name="Name">Name of the decoration</param>
        /// <param name="Description">Description of the decoration</param>
        /// <param name="Amount">Fee Amount to be charged for the decoration</param>
        /// <param name="FormatString">
        /// HTML format string to determine the effect of the decoration
        /// (e.g. "&lt;b&gt;{0}&lt;/b&gt;" for Bold
        /// </param>
        /// <param name="ValidFields">
        /// Comma separated list of Listing Field Names to which this decoration will be applied
        /// (e.g. "ID,Title" if both the Listing # and the Listing Title will be rendered as Bold)
        /// </param>
        /// <param name="PayToProceed">Indicates whether immediate fee payment is required before the decoration is assigned</param>
        /// <returns>Redirect to /Admin/DecorationsManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditDecoration(int id, string Name, string Description, string Amount, string FormatString, string ValidFields, bool PayToProceed)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {

                    //TODO: Move validation logic to BLL

                    decimal _amount = 0;

                    //name required
                    if (string.IsNullOrEmpty(Name))
                    {
                        ModelState.AddModelError("Name", "Name_Required");
                    }
                    //description required
                    if (string.IsNullOrEmpty(Description))
                    {
                        ModelState.AddModelError("Description", "Description_Required");
                    }
                    //format string required
                    if (string.IsNullOrEmpty(FormatString))
                    {
                        ModelState.AddModelError("FormatString", "FormatString_Required");
                    }
                    //validfields required
                    if (string.IsNullOrEmpty(ValidFields))
                    {
                        ModelState.AddModelError("ValidFields", "ValidFields_Required");
                    }
                    //amount required, convertible to decimal and >= 0
                    if (string.IsNullOrEmpty(Amount))
                    {
                        ModelState.AddModelError("Amount", "Amount_Required");
                    }
                    else
                    {
                        if (decimal.TryParse(Amount, out _amount))
                        {
                            if (_amount < 0)
                            {
                                ModelState.AddModelError("Amount", "Amount_GTEZero");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("Amount", "Amount_ConvertDecimal");
                        }
                    }

                    if (!ModelState.IsValid)
                    {
                        return View(SiteClient.Decorations.Where(d => (int)d.ID == id).SingleOrDefault());
                    }

                    //Save         
                    SiteClient.UpdateDecoration(User.Identity.Name, id, Name, Description, _amount, FormatString,
                                                ValidFields, PayToProceed);
                }

                SiteClient.Reset();
                PrepareSuccessMessage("EditDecoration", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("EditDecoration", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.DecorationsManagementAction);
        }

        /// <summary>
        /// Processes request to delete the specified decoration
        /// </summary>
        /// <param name="id">ID of the requested decoration</param>
        /// <returns>Redirect to /Admin/DecorationsManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteDecoration(int id)
        {
            try
            {
                SiteClient.DeleteDecoration(User.Identity.Name, id);
                SiteClient.Reset();
                PrepareSuccessMessage("DeleteDecoration", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteDecoration", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.DecorationsManagementAction);
        }

        /// <summary>
        /// Displays form to edit the specified location
        /// </summary>
        /// <param name="id">ID of the requested location</param>
        /// <returns>View(Location)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditLocation(int id)
        {
            return View(SiteClient.Locations.Where(l => (int)l.ID == id).SingleOrDefault());
        }

        /// <summary>
        /// Processes request to edit the specified listing location
        /// </summary>
        /// <param name="id">ID of the requested listing location</param>
        /// <param name="Description">Short description for the location</param>
        /// <param name="Amount">decimal fee amount for the location</param>
        /// <param name="PayToProceed">Indicates whether immediate fee payment is required before the location is assigned</param>
        /// <returns>Redirect to /Admin/DecorationsManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditLocation(int id, string Description, string Amount, bool PayToProceed)
        {
            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {
                    decimal _amount = 0;

                    //description required
                    if (string.IsNullOrEmpty(Description))
                    {
                        ModelState.AddModelError("Description", "Description_Required");
                    }

                    //amount required, convertible to decimal and >= 0
                    if (string.IsNullOrEmpty(Amount))
                    {
                        ModelState.AddModelError("Amount", "Amount_Required");
                    }
                    else
                    {
                        if (decimal.TryParse(Amount, out _amount))
                        {
                            if (_amount < 0)
                            {
                                ModelState.AddModelError("Amount", "Amount_GTEZero");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("Amount", "Amount_ConvertDecimal");
                        }
                    }

                    if (!ModelState.IsValid)
                    {
                        return View(SiteClient.Locations.Where(l => (int)l.ID == id).SingleOrDefault());
                    }

                    SiteClient.UpdateLocation(User.Identity.Name, id, Description, _amount, PayToProceed);
                }

                SiteClient.Reset();
                PrepareSuccessMessage("EditLocation", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("EditLocation", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.DecorationsManagementAction);
        }

        #endregion

        #region Scheduled Payments

        /// <summary>
        /// Displays form for Scheduled Payment options for Fee Invoices
        /// </summary>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ScheduledPayments()
        {
            var cultureInfo = this.GetCultureInfo();

            int id = 157;
            Category currentCategory = CommonClient.GetCategoryByID(id);
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(id).Trees[id].ToLocalizedLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Site" });

            //ViewData[Strings.Fields.ParentCategory] = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);

            //this list includes demo-disabled properties so they can be shown on the form, even though they are not updatable
            List<CustomProperty> propertiesToDisplay =
                SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

            //setup Batch Provider Enumeration
            CustomProperty batchProviders =
                propertiesToDisplay.Where(p => p.Field.Name == "BatchPaymentProvider").Single();
            List<string> batchPaymentProviders = AccountingClient.GetBatchPaymentProviders(User.Identity.Name);
            batchProviders.Field.Enumeration.Clear();
            foreach (string name in batchPaymentProviders)
            {
                batchProviders.Field.Enumeration.Add(new ListItem(0, name, true, name));
            }

            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"

            CustomProperty nextBatchPayment =
                propertiesToDisplay.Where(p => p.Field.Name == "NextBatchPayment").Single();
            if (DateTime.Parse(nextBatchPayment.Value) < DateTime.Parse("1/1/1950"))
                ViewData["NextAttemptDTTM"] = this.CustomFieldResourceString("Never");
            else
                ViewData["NextAttemptDTTM"] =
                    this.LocalDTTM(DateTime.Parse(nextBatchPayment.Value));

            CustomProperty batchPaymentTime =
                propertiesToDisplay.Where(p => p.Field.Name == "BatchPaymentTime").Single();
            ViewData["BatchTime"] =
                DateTime.Parse(batchPaymentTime.Value).ToString("t", cultureInfo);

            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //This is a save postback
                try
                {
                    if (propertiesToDisplay.Count > 0)
                    {
                        //IN (populate UserInput and prepare ModelState for output)                        
                        UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
                        input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });
                        DateTime batchPaymentDTTM = DateTime.MinValue;
                        try
                        {
                            batchPaymentDTTM =
                                DateTime.Parse(input.Items["BatchTime"], cultureInfo);
                        }
                        catch
                        {
                            //any parse failure:
                            ModelState.AddModelError("BatchTime", "BatchTime_ConvertDateTime");
                            return (View(propertiesToDisplay));
                        }
                        input.Items.Add("BatchPaymentTime", batchPaymentDTTM.ToString(cultureInfo));

                        //set Next Batch Payment Attempt dttm
                        DateTime nextBatchTime = AccountingClient.GetNextDateTime(User.Identity.Name,
                                                                                  input.Items["BatchPaymentPeriod"],
                                                                                  batchPaymentDTTM,
                                                                                  int.Parse(
                                                                                      input.Items["BatchPaymentDay"]),
                                                                                  int.Parse(
                                                                                      input.Items["BatchPaymentDate"]));
                        if (nextBatchTime < DateTime.Parse("1/1/1950"))
                            ViewData["NextAttemptDTTM"] = this.CustomFieldResourceString("Never");
                        else
                            ViewData["NextAttemptDTTM"] = this.LocalDTTM(nextBatchTime);
                        input.Items.Add("NextBatchPayment", nextBatchTime.ToString(cultureInfo));

                        try
                        {
                            SiteClient.UpdateSettings(User.Identity.Name, propertiesToDisplay, input);
                            SiteClient.Reset();
                            PrepareSuccessMessage("ScheduledPayments", MessageType.Method);
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            //display validation errors                            
                            foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                            {
                                ModelState.AddModelError(issue.Key, issue.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage("ScheduledPayments", MessageType.Method);
                }
                return (View(propertiesToDisplay));
            }
            else
            {
                //This is a load
                foreach (CustomProperty customProperty in propertiesToDisplay)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(customProperty.Value, customProperty.Value, null);
                    ModelState.Add(customProperty.Field.Name, ms);
                }

                return (View(propertiesToDisplay));
            }
        }

        /// <summary>
        /// Displays form for Invoice Generation and Payment options for Sale Invoices
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SalesPreferences()
        {
            var cultureInfo = this.GetCultureInfo();

            int id = 1083;
            Category currentCategory = CommonClient.GetCategoryByID(id);
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(id).Trees[id].ToLocalizedLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Site" });

            //ViewData[Strings.Fields.ParentCategory] = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);

            //this list includes demo-disabled properties so they can be shown on the form, even though they are not updatable
            List<CustomProperty> propertiesToDisplay =
                SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

            ////setup Batch Provider Enumeration
            //CustomProperty batchProviders =
            //    propertiesToDisplay.Where(p => p.Field.Name == "BatchPaymentProvider").Single();
            //List<string> batchPaymentProviders = AccountingClient.GetBatchPaymentProviders(User.Identity.Name);
            //batchProviders.Field.Enumeration.Clear();
            //foreach (string name in batchPaymentProviders)
            //{
            //    batchProviders.Field.Enumeration.Add(new ListItem(0, name, true, name));
            //}

            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"

            DateTime nextSalesBatchPaymentDttm = SiteClient.Properties.GetPropertyValue(
                SiteProperties.NextSalesBatchPaymentDttm, DateTime.Parse("1/1/2100"));
            if (nextSalesBatchPaymentDttm >= DateTime.Parse("1/1/2100"))
                ViewData["NextAttemptDTTM"] = this.CustomFieldResourceString("Never");
            else
                ViewData["NextAttemptDTTM"] = this.LocalDTTM(nextSalesBatchPaymentDttm);

            DateTime nextSalesBatchPayment = SiteClient.Properties.GetPropertyValue(
                SiteProperties.AutoChargeCCsTimeOfDay, DateTime.Parse("1/1/2100 12:00 am"));
            ViewData["BatchTime"] = nextSalesBatchPayment.ToString("t", cultureInfo);

            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //This is a save postback
                try
                {
                    if (propertiesToDisplay.Count > 0)
                    {
                        //IN (populate UserInput and prepare ModelState for output)                        
                        UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
                        input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });
                        DateTime batchTimeOfDay = DateTime.MinValue;
                        try
                        {
                            batchTimeOfDay = DateTime.Parse(input.Items["BatchTime"], cultureInfo);
                        }
                        catch
                        {
                            //any parse failure:
                            ModelState.AddModelError("BatchTime", "BatchTime_ConvertDateTime");
                            return (View(propertiesToDisplay));
                        }
                        input.Items.Add(SiteProperties.AutoChargeCCsTimeOfDay, batchTimeOfDay.ToString(cultureInfo));

                        //set Next Batch Payment Attempt dttm
                        DateTime nextBatchDttm = DateTime.Parse("1/1/2100");
                        if (input.Items[SiteProperties.AutoChargeCCs] == "Daily")
                        {
                            nextBatchDttm = AccountingClient.GetNextDateTime(User.Identity.Name, "DAILY", batchTimeOfDay, 1, 1);
                            if (nextBatchDttm >= DateTime.Parse("1/1/2100"))
                                ViewData["NextAttemptDTTM"] = this.CustomFieldResourceString("Never");
                            else
                                ViewData["NextAttemptDTTM"] = this.LocalDTTM(nextBatchDttm);
                        }
                        input.Items.Add(SiteProperties.NextSalesBatchPaymentDttm, nextBatchDttm.ToString(cultureInfo));

                        try
                        {
                            SiteClient.UpdateSettings(User.Identity.Name, propertiesToDisplay, input);
                            SiteClient.Reset();
                            PrepareSuccessMessage("SalesPreferences", MessageType.Method);
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            //display validation errors                            
                            foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                            {
                                ModelState.AddModelError(issue.Key, issue.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage("SalesPreferences", MessageType.Method);
                }
                return (View(propertiesToDisplay));
            }
            else
            {
                //This is a load
                foreach (CustomProperty customProperty in propertiesToDisplay)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(customProperty.Value, customProperty.Value, null);
                    ModelState.Add(customProperty.Field.Name, ms);
                }

                return (View(propertiesToDisplay));
            }
        }

        #endregion

        #region Property Management

        /// <summary>
        /// Displays form and processes request to edit a set of custom properties
        /// </summary>
        /// <param name="id">ID of the requested category of custom properties</param>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult PropertyManagement(int id)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            Category currentCategory = CommonClient.GetCategoryByID(id);
            if (currentCategory == null || currentCategory.Type != "Site")
            {
                throw new HttpException(404, "Category Not Found");
            }
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(id).Trees[id].ToLocalizedLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Site" });

            ViewData[Strings.Fields.ParentCategory] = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);

            //this list includes demo-disabled properties so they can be shown on the form, even though they are not updatable
            List<CustomProperty> propertiesToDisplay =
                SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //This is a save postback
                try
                {
                    //this list will only include the currently updatable properties, so the demo can't be hacked
                    List<CustomProperty> propertiesToUpdate =
                        SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);
                    if (SiteClient.DemoEnabled)
                    {
                        var demoFields = new List<string>();
                        demoFields.Add(SiteProperties.URL); //demoFields.Add(76101); // URL
                        demoFields.Add(SiteProperties.SecureURL); //demoFields.Add(76103); // SecureURL
                        demoFields.Add(SiteProperties.RestrictOutsideSellers); //demoFields.Add(41402); // RestrictOutsideSellers

                        demoFields.Add(SiteProperties.TopBannersToDisplay); //demoFields.Add(201); // TopBannersToDisplay
                        demoFields.Add(SiteProperties.LeftBannersToDisplay); //demoFields.Add(202); // LeftBannersToDisplay
                        demoFields.Add(SiteProperties.BottomBannersToDisplay); //demoFields.Add(203); // BottomBannersToDisplay
                        demoFields.Add(SiteProperties.RequireAuthentication); //demoFields.Add(704); // RequireAuthentication

                        demoFields.Add(SiteProperties.ProcessingEnabled); //demoFields.Add(69101); // ProcessingEnabled

                        demoFields.Add(SiteProperties.CssTheme); //demoFields.Add(740); // CssTheme
                        demoFields.Add(SiteProperties.HeadingColor); //demoFields.Add(904); // HeadingColor
                        demoFields.Add(SiteProperties.CenterLogo); //demoFields.Add(903); // CenterLogo

                        demoFields.Add(SiteProperties.EnableWebAPI); //demoFields.Add(727); // EnableWebAPI

                        demoFields.Add(SiteProperties.RecaptchaPublicKey); //demoFields.Add(817); //RecaptchaPublicKey
                        demoFields.Add(SiteProperties.RecaptchaPrivateKey); // demoFields.Add(818); //RecaptchaPrivateKey
                        demoFields.Add(SiteProperties.EnableRecaptchaForContactUs); //demoFields.Add(819); //EnableRecaptchaForContactUs
                        demoFields.Add(SiteProperties.EnableRecaptchaForRegistration); //demoFields.Add(820); //EnableRecaptchaForRegistration

                        demoFields.Add(SiteProperties.AuthorizeNet_PostUrl); //demoFields.Add(44404); //AuthorizeNet_PostUrl
                        demoFields.Add(SiteProperties.AuthorizeNet_MerchantLoginID); //demoFields.Add(44406); //AuthorizeNet_MerchantLoginID
                        demoFields.Add(SiteProperties.AuthorizeNet_TransactionKey); //demoFields.Add(44408); //AuthorizeNet_TransactionKey
                        demoFields.Add(SiteProperties.AuthorizeNet_TestMode); //demoFields.Add(111013); //AuthorizeNet_TestMode
                        demoFields.Add(SiteProperties.AuthorizeNet_EnableForSellers); //demoFields.Add(1001); //AuthorizeNet_EnableForSellers

                        demoFields.Add(SiteProperties.PayPal_PostURL); //demoFields.Add(44905); //PayPal_PostURL
                        demoFields.Add(SiteProperties.PayPal_IPNURL); //demoFields.Add(54201); //PayPal_IPNURL
                        demoFields.Add(SiteProperties.PayPal_SuccessReturnURL); //demoFields.Add(59305); //PayPal_SuccessReturnURL
                        demoFields.Add(SiteProperties.PayPal_CancelReturnURL); //demoFields.Add(59401); //PayPal_CancelReturnURL
                        demoFields.Add(SiteProperties.PayPal_FeesEmail); //demoFields.Add(63801); //PayPal_FeesEmail

                        demoFields.Add(SiteProperties.MaintenanceMode); //demoFields.Add(827); //MaintenanceMode

                        demoFields.Add(SiteProperties.StripeConnect_ClientId);
                        demoFields.Add(SiteProperties.StripeConnect_SiteFeesPublishableApiKey);
                        demoFields.Add(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
                        demoFields.Add(SiteProperties.StripeConnect_EnabledForSellers);

                        foreach (string demoFieldName in demoFields)
                        {
                            CustomProperty propToRemove =
                                propertiesToUpdate.Where(p => p.Field.Name == demoFieldName).FirstOrDefault();
                            if (propToRemove != null)
                            {
                                propertiesToUpdate.Remove(propToRemove);
                                //add a model state for this field, so it doesn't appear blank after the save
                                string key = propToRemove.Field.Name;
                                if (!ModelState.ContainsKey(key))
                                {
                                    //...add it to the model
                                    ModelState ms = new ModelState();
                                    ms.Value = new ValueProviderResult(propToRemove.Value, propToRemove.Value, null);
                                    ModelState.Add(key, ms);
                                }
                            }
                        }
                    }

                    List<int> securedFields = new List<int>();
                    securedFields.Add(44408); //AuthorizeNet_TransactionKey
                    foreach (var property in propertiesToUpdate)
                    {
                        if (property.Field.Encrypted)
                        {
                            securedFields.Add(property.Field.ID);
                        }
                    }
                    foreach (int securedFieldId in securedFields)
                    {
                        CustomProperty propToRemove =
                            propertiesToUpdate.Where(p => p.Field.ID == securedFieldId).FirstOrDefault();
                        if (propToRemove != null)
                        {
                            if (Request.Form.AllKeys.Contains(propToRemove.Field.Name) && Request[propToRemove.Field.Name] == Strings.Fields.MaskedFieldValue)
                            {
                                propertiesToUpdate.Remove(propToRemove);
                                //add a model state for this field, so it doesn't appear blank after the save
                                string key = propToRemove.Field.Name;
                                if (!ModelState.ContainsKey(key))
                                {
                                    //...add it to the model
                                    ModelState ms = new ModelState();
                                    ms.Value = new ValueProviderResult(propToRemove.Value, propToRemove.Value, null);
                                    ModelState.Add(key, ms);
                                }
                            }
                        }
                    }

                    if (propertiesToUpdate.Count > 0)
                    {
                        //IN (populate UserInput and prepare ModelState for output)
                        UserInput input = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
                        input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });

                        //validate specific admin settings if applicable
                        var validation = new ValidationResults();

                        CheckSitePropertyRules(propertiesToUpdate, input, validation);

                        try
                        {
                            if (!validation.IsValid)
                            {
                                Statix.ThrowValidationFaultContract(validation);
                            }

                            //changing the payment provider requires resetting the AccountingClient cache to fully take effect
                            bool accountingCacheResetNeeded = (id == 45001);

                            //enabling buyer's premium requires resetting the UserClient cache to fully take effect, 
                            //  but disabling it does not require resetting the UserClient cache.
                            bool userCacheResetNeeded = input.Items.ContainsKey(SiteProperties.EnableBuyersPremium)
                                                        && !SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium)
                                                        && bool.Parse(input.Items[SiteProperties.EnableBuyersPremium]);

                            bool homepageRouteUpdateNeeded = input.Items.ContainsKey(SiteProperties.HomepageContent);

                            bool stripeCredentialsNeeded = input.Items.ContainsKey(SiteProperties.StripeConnect_Enabled)
                                                        && !SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled)
                                                        && bool.Parse(input.Items[SiteProperties.StripeConnect_Enabled])
                                                        && (string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_ClientId)) ||
                                                            string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesPublishableApiKey)) ||
                                                            string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey)));

                            bool authNetCredentialsNeeded = input.Items.ContainsKey(SiteProperties.AuthorizeNet_Enabled)
                                                        && !SiteClient.BoolSetting(SiteProperties.AuthorizeNet_Enabled)
                                                        && bool.Parse(input.Items[SiteProperties.AuthorizeNet_Enabled])
                                                        && (string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_PostUrl)) ||
                                                            string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_MerchantLoginID)) ||
                                                            string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_TransactionKey)) || 
                                                            SiteClient.TextSetting(SiteProperties.AuthorizeNet_MerchantLoginID).Equals("login") ||
                                                            SiteClient.TextSetting(SiteProperties.AuthorizeNet_TransactionKey).Equals("key"));

                            SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
                            SiteClient.Reset();

                            if (accountingCacheResetNeeded)
                            {
                                AccountingClient.Reset();
                            }
                            if (userCacheResetNeeded)
                            {
                                UserClient.Reset();
                            }

                            if (homepageRouteUpdateNeeded)
                            {
                                var routes = RouteTable.Routes;
                                if (routes["AlternateHomepage1"] != null)
                                {
                                    routes.Remove(routes["AlternateHomepage1"]);
                                }
                                if (routes["AlternateHomepage2"] != null)
                                {
                                    routes.Remove(routes["AlternateHomepage2"]);
                                }
                                if (routes["AlternateHomepage3"] != null)
                                {
                                    routes.Remove(routes["AlternateHomepage3"]);
                                }
                                routes.Remove(routes["Default"]);
                                if (SiteClient.TextSetting(SiteProperties.HomepageContent) == "browse")
                                {
                                    routes.MapRoute(
                                        name: "AlternateHomepage1",
                                        url: "",
                                        defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                                    );
                                    routes.MapRoute(
                                        name: "AlternateHomepage2",
                                        url: "Home/",
                                        defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                                    );
                                    routes.MapRoute(
                                        name: "AlternateHomepage3",
                                        url: "Home/Index/{id}",
                                        defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                                    );
                                }
                                routes.MapRoute(
                                    name: "Default",
                                    url: "{controller}/{action}/{id}",
                                    defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
                                );
                            }

                            if (stripeCredentialsNeeded)
                            {
                                PrepareErrorMessage("StripeEnabled_CredentialsNeeded", MessageType.Message);
                                var stripePropManagementCat = CommonClient.GetChildCategories(45001).FirstOrDefault(c => c.Name == "StripeConnect");
                                if (stripePropManagementCat != null)
                                {
                                    return RedirectToAction(Strings.MVC.PropertyManagementAction, new { id = stripePropManagementCat.ID });
                                }
                            }
                            else if (authNetCredentialsNeeded)
                            {
                                PrepareErrorMessage("AuthNetEnabled_CredentialsNeeded", MessageType.Message);
                                var authNetPropManagementCat = CommonClient.GetChildCategories(45001).FirstOrDefault(c => c.Name == "AuthorizeDotNet");
                                if (authNetPropManagementCat != null)
                                {
                                    return RedirectToAction(Strings.MVC.PropertyManagementAction, new { id = authNetPropManagementCat.ID });
                                }
                            }
                            else
                            {
                                PrepareSuccessMessage("PropertyManagement", MessageType.Method);
                            }
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            //display validation errors
                            foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                            {
                                ModelState.AddModelError(issue.Key, issue.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage("PropertyManagement", MessageType.Method);
                }
            }
            else
            {
                //This is a load
                ModelState.FillProperties(propertiesToDisplay, cultureInfo);

            }
            return (View(propertiesToDisplay));
        }

        /// <summary>
        /// checks all attempted site property values to ensure they are mutually compatible
        /// </summary>
        /// <param name="propertiesToUpdate">list of properties that will be updated</param>
        /// <param name="input">input container with attempted values</param>
        /// <param name="validation">validation results container</param>
        private void CheckSitePropertyRules(IEnumerable<CustomProperty> propertiesToUpdate, UserInput input, ValidationResults validation)
        {
            //do not allow MaxResultsPerPage to be less than 1
            int tempInt1;
            if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.MaxResultsPerPage) &&
                input.Items.ContainsKey(Strings.SiteProperties.MaxResultsPerPage) &&
                int.TryParse(input.Items[Strings.SiteProperties.MaxResultsPerPage], out tempInt1))
            {
                if (tempInt1 < 1)
                {
                    validation.AddResult(new ValidationResult("MaxResultsPerPage_GTZero", this,
                        Strings.SiteProperties.RecaptchaPublicKey, Strings.SiteProperties.RecaptchaPublicKey, null));
                }
            }

            //require captcha keys if either EnableRecaptchaForContactUs or EnableRecaptchaForRegistration is being enabled
            bool temp1 = false;
            if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.EnableRecaptchaForContactUs) &&
                input.Items.ContainsKey(Strings.SiteProperties.EnableRecaptchaForContactUs) &&
                bool.TryParse(input.Items[Strings.SiteProperties.EnableRecaptchaForContactUs], out temp1) &&
                temp1 == true)
            {
                if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.RecaptchaPublicKey))
                {
                    if (!input.Items.ContainsKey(Strings.SiteProperties.RecaptchaPublicKey) ||
                        string.IsNullOrEmpty(input.Items[Strings.SiteProperties.RecaptchaPublicKey]))
                    {
                        validation.AddResult(new ValidationResult("RecaptchaPublicKey_Required_For_EnableRecaptchaForContactUs", this,
                            Strings.SiteProperties.RecaptchaPublicKey, Strings.SiteProperties.RecaptchaPublicKey, null));
                    }
                }
                else if (string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.RecaptchaPublicKey)))
                {
                    validation.AddResult(new ValidationResult("RecaptchaPublicKey_Required_For_EnableRecaptchaForContactUs", this,
                        Strings.SiteProperties.RecaptchaPublicKey, Strings.SiteProperties.RecaptchaPublicKey, null));
                }
                if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.RecaptchaPrivateKey))
                {
                    if (!input.Items.ContainsKey(Strings.SiteProperties.RecaptchaPrivateKey) ||
                        string.IsNullOrEmpty(input.Items[Strings.SiteProperties.RecaptchaPrivateKey]))
                    {
                        validation.AddResult(new ValidationResult("RecaptchaPrivateKey_Required_For_EnableRecaptchaForContactUs", this,
                            Strings.SiteProperties.RecaptchaPrivateKey, Strings.SiteProperties.RecaptchaPrivateKey, null));
                    }
                }
                else if (string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.RecaptchaPrivateKey)))
                {
                    validation.AddResult(new ValidationResult("RecaptchaPrivateKey_Required_For_EnableRecaptchaForContactUs", this,
                        Strings.SiteProperties.RecaptchaPrivateKey, Strings.SiteProperties.RecaptchaPrivateKey, null));
                }
            }
            bool temp2;
            if (!temp1 && 
                propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.EnableRecaptchaForRegistration) &&
                input.Items.ContainsKey(Strings.SiteProperties.EnableRecaptchaForRegistration) &&
                bool.TryParse(input.Items[Strings.SiteProperties.EnableRecaptchaForRegistration], out temp2) &&
                temp2 == true)
            {
                if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.RecaptchaPublicKey))
                {
                    if (!input.Items.ContainsKey(Strings.SiteProperties.RecaptchaPublicKey) ||
                        string.IsNullOrEmpty(input.Items[Strings.SiteProperties.RecaptchaPublicKey]))
                    {
                        validation.AddResult(new ValidationResult("RecaptchaPublicKey_Required_For_EnableRecaptchaForRegistration", this,
                            Strings.SiteProperties.RecaptchaPublicKey, Strings.SiteProperties.RecaptchaPublicKey, null));
                    }
                }
                else if (string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.RecaptchaPublicKey)))
                {
                    validation.AddResult(new ValidationResult("RecaptchaPublicKey_Required_For_EnableRecaptchaForRegistration", this,
                        Strings.SiteProperties.RecaptchaPublicKey, Strings.SiteProperties.RecaptchaPublicKey, null));
                }
                if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.RecaptchaPrivateKey))
                {
                    if (!input.Items.ContainsKey(Strings.SiteProperties.RecaptchaPrivateKey) ||
                        string.IsNullOrEmpty(input.Items[Strings.SiteProperties.RecaptchaPrivateKey]))
                    {
                        validation.AddResult(new ValidationResult("RecaptchaPrivateKey_Required_For_EnableRecaptchaForRegistration", this,
                            Strings.SiteProperties.RecaptchaPrivateKey, Strings.SiteProperties.RecaptchaPrivateKey, null));
                    }
                }
                else if (string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.RecaptchaPrivateKey)))
                {
                    validation.AddResult(new ValidationResult("RecaptchaPrivateKey_Required_For_EnableRecaptchaForRegistration", this,
                        Strings.SiteProperties.RecaptchaPrivateKey, Strings.SiteProperties.RecaptchaPrivateKey, null));
                }
            }

            //require value for VatRate if VATEnabled is being enabled
            bool temp4 = false;
            if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.VATEnabled) &&
                input.Items.ContainsKey(Strings.SiteProperties.VATEnabled) &&
                bool.TryParse(input.Items[Strings.SiteProperties.VATEnabled], out temp4) &&
                temp4 == true)
            {
                if (propertiesToUpdate.Any(p => p.Field.Name == Strings.SiteProperties.VatRate))
                {
                    if (!input.Items.ContainsKey(Strings.SiteProperties.VatRate) ||
                        string.IsNullOrEmpty(input.Items[Strings.SiteProperties.VatRate]))
                    {
                        validation.AddResult(new ValidationResult("VatRate_Required_For_VATEnabled", this,
                            Strings.SiteProperties.VatRate, Strings.SiteProperties.VatRate, null));
                    }
                }
                else if (string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.VatRate)))
                {
                    validation.AddResult(new ValidationResult("VatRate_Required_For_VATEnabled", this,
                        Strings.SiteProperties.VatRate, Strings.SiteProperties.VatRate, null));
                }
            }

            //Authorize.Net and Stripe can't be enabled at the same time
            if (propertiesToUpdate.Any(p => p.Field.Name == SiteProperties.StripeConnect_Enabled) &&
                propertiesToUpdate.Any(p => p.Field.Name == SiteProperties.AuthorizeNet_Enabled))
            {
                bool stripeEnabled;
                bool authNetEnabled;
                if (bool.TryParse(input.Items[SiteProperties.StripeConnect_Enabled], out stripeEnabled) &&
                    bool.TryParse(input.Items[SiteProperties.AuthorizeNet_Enabled], out authNetEnabled))
                {
                    if (stripeEnabled && authNetEnabled)
                    {
                        validation.AddResult(new ValidationResult("AuthNetAndStripeCantBothBeEnabled", this,
                            SiteProperties.StripeConnect_Enabled, SiteProperties.StripeConnect_Enabled, null));
                    }
                }
            }

            //if stripe is enabled, Stored Credit Cards (f.k.a Credit Cards Enabled) must be disabled
            if (propertiesToUpdate.Any(p => p.Field.Name == SiteProperties.StripeConnect_Enabled) &&
                propertiesToUpdate.Any(p => p.Field.Name == SiteProperties.CreditCardsEnabled))
            {
                bool stripeEnabled;
                bool creditCardsEnabled;
                if (bool.TryParse(input.Items[SiteProperties.StripeConnect_Enabled], out stripeEnabled) &&
                    bool.TryParse(input.Items[SiteProperties.CreditCardsEnabled], out creditCardsEnabled))
                {
                    if (stripeEnabled && creditCardsEnabled)
                    {
                        validation.AddResult(new ValidationResult("StoredCreditCardsAndStripeCantBothBeEnabled", this,
                            SiteProperties.StripeConnect_Enabled, SiteProperties.StripeConnect_Enabled, null));
                    }
                }
            }


        }

        //public ActionResult Maintenance()
        //{
        //    const int id = 722;

        //    Category currentCategory = CommonClient.GetCategoryByID(id);
        //    ViewData[Strings.MVC.LineageString] =
        //        CommonClient.GetCategoryPath(id).Trees[id].ToLocalizedLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Site" });

        //    ViewData[Strings.Fields.ParentCategory] = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);

        //    //this list includes demo-disabled properties so they can be shown on the form, even though they are not updatable
        //    List<CustomProperty> propertiesToDisplay =
        //        SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

        //    if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
        //    {
        //        //This is a save postback
        //        try
        //        {
        //            //this list will only include the currently updatable properties, so the demo can't be hacked
        //            List<CustomProperty> propertiesToUpdate =
        //                SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);
        //            if (SiteClient.DemoEnabled)
        //            {
        //                List<int> demoFields = new List<int>(3);
        //                demoFields.Add(76101); // URL
        //                demoFields.Add(76103); // SecureURL
        //                demoFields.Add(41402); // RestrictOutsideSellers

        //                demoFields.Add(201); // TopBannersToDisplay
        //                demoFields.Add(202); // LeftBannersToDisplay
        //                demoFields.Add(203); // BottomBannersToDisplay
        //                demoFields.Add(704); // RequireAuthentication

        //                demoFields.Add(69101); // ProcessingEnabled

        //                demoFields.Add(740); // CssTheme

        //                foreach (int demoFieldId in demoFields)
        //                {
        //                    CustomProperty propToRemove =
        //                        propertiesToUpdate.Where(p => p.Field.ID == demoFieldId).FirstOrDefault();
        //                    if (propToRemove != null)
        //                    {
        //                        propertiesToUpdate.Remove(propToRemove);
        //                        //add a model state for this field, so it doesn't appear blank after the save
        //                        string key = propToRemove.Field.Name;
        //                        if (!ModelState.ContainsKey(key))
        //                        {
        //                            //...add it to the model
        //                            ModelState ms = new ModelState();
        //                            ms.Value = new ValueProviderResult(propToRemove.Value, propToRemove.Value, null);
        //                            ModelState.Add(key, ms);
        //                        }
        //                    }
        //                }
        //            }
        //            if (propertiesToUpdate.Count > 0)
        //            {
        //                //IN (populate UserInput and prepare ModelState for output)
        //                string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
        //                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
        //                input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });

        //                try
        //                {
        //                    SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
        //                    SiteClient.Reset();
        //                    PrepareSuccessMessage("Maintenance", MessageType.Method);
        //                }
        //                catch (FaultException<ValidationFaultContract> vfc)
        //                {
        //                    //display validation errors
        //                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
        //                    {
        //                        ModelState.AddModelError(issue.Key, issue.Message);
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
        //                }
        //            }
        //        }
        //        catch
        //        {
        //            PrepareErrorMessage("Maintenance", MessageType.Method);
        //        }
        //        return (View(propertiesToDisplay));
        //    }
        //    else
        //    {
        //        //This is a load

        //        List<CustomProperty> existingProperties =
        //            SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

        //        foreach (
        //        CustomProperty customProperty in existingProperties)
        //        {
        //            //Add Model control
        //            ModelState ms = new ModelState();
        //            ms.Value = new ValueProviderResult(customProperty.Value, customProperty.Value, null);
        //            ModelState.Add(customProperty.Field.Name, ms);
        //        }

        //        return (View(existingProperties));
        //    }
        //}

        /// <summary>
        /// Displays form and processes request to edit a set of custom properties
        /// </summary>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult Maintenance()
        {
            const int containerCategoryId = 722; // "Advanced > Maintenance"
            //const int containerCategoryId = 802; // "Data Management > Maintenance"
            Category containerCategory = CommonClient.GetCategoryByID(containerCategoryId);
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            List<CustomProperty> propertiesToDisplay = SiteClient.Properties.WhereContainsFields(containerCategory.CustomFieldIDs);

            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                //This is a postback
                try
                {
                    UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
                    input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });
                    //only attempt to update properties that weren't disabled on the HTML form
                    var propsToUpdate = new List<CustomProperty>(propertiesToDisplay.Where(p => input.Items.ContainsKey(p.Field.Name)));
                    try
                    {
                        if (SiteClient.DemoEnabled)
                        {
                            throw new Exception("DemoDisabledNotSaved");
                        }

                        //ensure "DataCleanup_DeleteListings_DaysOld" site property it not negative
                        int temp1;
                        if (input.Items.ContainsKey(Strings.SiteProperties.DataCleanup_DeleteListings_DaysOld)
                            && int.TryParse(input.Items[Strings.SiteProperties.DataCleanup_DeleteListings_DaysOld], out temp1))
                        {
                            if (temp1 < 0)
                            {
                                throw new Exception("VAL_ERR_001");
                            }
                        }

                        SiteClient.UpdateSettings(User.Identity.Name, propsToUpdate, input);
                        SiteClient.Reset();
                        propertiesToDisplay = SiteClient.Properties.WhereContainsFields(containerCategory.CustomFieldIDs); // reload after update
                        PrepareSuccessMessage(Strings.MVC.MaintenanceAction, MessageType.Method);
                    }
                    catch (FaultException<ValidationFaultContract> vfc)
                    {
                        //display validation errors
                        foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message == "VAL_ERR_001")
                        {
                            ModelState.AddModelError(Strings.SiteProperties.DataCleanup_DeleteListings_DaysOld, "DataCleanup_DeleteListings_DaysOld_GTEZero");
                        }
                        else
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage(Strings.MVC.MaintenanceAction, MessageType.Method);
                }
            }
            else
            {
                //initial page load, populate modelstate
                ModelState.FillProperties(propertiesToDisplay, cultureInfo);
            }

            return View(Strings.MVC.MaintenanceAction, propertiesToDisplay);
        }


        /// <summary>
        /// Displays form and processes request to edit a set of custom properties
        /// </summary>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteListingsNow()
        {
            string cultureCode = SiteClient.SiteCulture;
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode);
            //set "next batch dttm" to "Now"
            SiteClient.UpdateSetting(User.Identity.Name, Strings.SiteProperties.DataCleanup_DeleteListings_NextBatchDTTM,
                DateTime.UtcNow.ToString(cultureInfo), cultureCode);
            //set "immediate batch requested" to true
            SiteClient.UpdateSetting(User.Identity.Name, Strings.SiteProperties.DataCleanup_DeleteListings_ImmediateBatchRequested,
                true.ToString(), cultureCode);
            SiteClient.Reset();

            PrepareSuccessMessage(Strings.MVC.DeleteListingsNowAction, MessageType.Method);

            return Maintenance();
        }

        #endregion

        #region Taxes

        /// <summary>
        /// Displays form and processes request to edit tax-related site properties
        /// </summary>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Taxes()
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            var currentCategory = CommonClient.GetCategoryByID(1011);
            var properties = SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                //This is a postback
                try
                {
                    UserInput input = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
                    input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });

                    //validate specific admin settings if applicable
                    var validation = new ValidationResults();

                    CheckSitePropertyRules(properties, input, validation);

                    if (!validation.IsValid)
                    {
                        Statix.ThrowValidationFaultContract(validation);
                    }

                    //saves changes
                    SiteClient.UpdateSettings(User.Identity.Name, properties, input);
                    SiteClient.Reset();

                    //reload properties
                    properties = SiteClient.Properties.WhereContainsFields(currentCategory.CustomFieldIDs);

                    //display "success" message
                    PrepareSuccessMessage("Taxes", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                }
            }
            else
            {
                //This is a load
                ModelState.FillProperties(properties, cultureInfo);
            }
            return View(properties);
        }

        #endregion Taxes

        #region Users

        /// <summary>
        /// Displays a page of a list of users
        /// </summary>
        /// <param name="userid">integer id of a specific user, limits list to 0 or 1 results when not null or &gt;0</param>
        /// <param name="username">limits results to users with this keyword in their username</param>
        /// <param name="first">limits results to users with this keyword in their first name</param>
        /// <param name="last">limits results to users with this keyword in their last name</param>
        /// <param name="email">limits results to users with this keyword in their email address</param>
        /// <param name="status"></param>
        /// <param name="role"></param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;User&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult UserManagement(int? userid, string username, string first, string last, string email, string status, string role, string sort, int? page, bool? descending)
        {
            int pageSize = 50; // SiteClient.PageSize;

            ViewData["userid"] = userid;
            ViewData["username"] = username;
            ViewData["first"] = first;
            ViewData["last"] = last;
            ViewData["email"] = email;
            ViewData["status"] = status;
            ViewData["role"] = role;
            ViewData["sort"] = sort ?? "id";
            ViewData["page"] = page;
            ViewData["descending"] = descending;

            SelectList userStatuses =
                new SelectList(new[] { 
                      new { value = string.Empty, text = this.AdminResourceString("All") }
                    , new { value = UserStatuses.Active, text = this.AdminResourceString("Active") }
                    , new { value = UserStatuses.Newsletter, text = this.AdminResourceString("Newsletter") }
                    , new { value = UserStatuses.Unapproved, text = this.AdminResourceString("Unapproved") }
                    , new { value = UserStatuses.Unverified, text = this.AdminResourceString("Unverified") }
                    , new { value = UserStatuses.Restricted, text = this.AdminResourceString("Restricted") }
                    , new { value = UserStatuses.Deactivated, text = this.AdminResourceString("Deactivated") }
                }, "value", "text", status);
            ViewData["StatusSelectList"] = userStatuses;

            List<SelectListItem> roleSelectOptions = new List<SelectListItem>(UserClient.GetRoles(User.Identity.Name).Count + 1);
            roleSelectOptions.Add(new SelectListItem() { Value = string.Empty, Text = this.AdminResourceString("All") });
            foreach (Role userRole in UserClient.GetRoles(User.Identity.Name).OrderBy(r => this.AdminResourceString(r.Name)))
            {
                roleSelectOptions.Add(new SelectListItem() { Value = userRole.Name, Text = this.AdminResourceString(userRole.Name) });
            }
            ViewData["RoleSelectList"] = new SelectList(roleSelectOptions, "Value", "Text", role);

            Page<User> retVal = UserClient.SearchUsers(User.Identity.Name,
                userid ?? 0,
                username ?? string.Empty,
                first ?? string.Empty,
                last ?? string.Empty,
                email ?? string.Empty,
                status ?? string.Empty,
                role ?? string.Empty,
                sort ?? "id",
                descending ?? false,
                page ?? 0,
                pageSize);
            return View(retVal);
        }

        /// <summary>
        /// Displays admin export user csv form
        /// </summary>
        /// <param name="userid">id of a specific user (0 to skip user id filter)</param>
        /// <param name="username">partial username string to match</param>
        /// <param name="first">partial first name string to match</param>
        /// <param name="last">partial last name string to match</param>
        /// <param name="email">partial email address string to match</param>
        /// <param name="status">filter results by various status flag combinations - see remarks for more info</param>
        /// <param name="role">the name of the role to include (e.g. "Admin", "Seller", or "Buyer"; or empty string ("") to skip role filter)</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <param name="count">previously calculated count of records that will be exported</param>
        /// <returns>View()</returns>
        /// <remarks>
        /// valid status values:
        /// <list type="table">
        ///     <item>
        ///         <value>empty string ("")</value>
        ///         <meaning>IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"deactivated"</value>
        ///         <meaning>IsActive = 0</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"restricted"</value>
        ///         <meaning>IsLockedOut = 1 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"unverified"</value>
        ///         <meaning>IsVerified = 0 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"unapproved"</value>
        ///         <meaning>IsApproved = 0 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"active"</value>
        ///         <meaning>IsActive = 1 and IsLockedOut = 0 and IsVerified = 1 and IsApproved = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"newsletter"</value>
        ///         <meaning>IsActive = 1 and IsLockedOut = 0 and IsVerified = 1 and IsApproved = 1 and Newsletter = 1</meaning>
        ///     </item>
        /// </list>
        /// </remarks>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ExportUserCSV(int? userid, string username, string first, string last, string email, string status, string role, string sort, bool? descending, int count)
        {
            ViewData["userid"] = userid;
            ViewData["username"] = username;
            ViewData["first"] = first;
            ViewData["last"] = last;
            ViewData["email"] = email;
            ViewData["status"] = status;
            ViewData["role"] = role;
            ViewData["sort"] = sort ?? "id";
            ViewData["descending"] = descending ?? false;
            ViewData["count"] = count;

            return View();
        }

        /// <summary>
        /// Processes admin export user csv request
        /// </summary>
        /// <param name="userid">id of a specific user (0 to skip user id filter)</param>
        /// <param name="username">partial username string to match</param>
        /// <param name="first">partial first name string to match</param>
        /// <param name="last">partial last name string to match</param>
        /// <param name="email">partial email address string to match</param>
        /// <param name="status">filter results by various status flag combinations - see remarks for more info</param>
        /// <param name="role">the name of the role to include (e.g. "Admin", "Seller", or "Buyer"; or empty string ("") to skip role filter)</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <param name="includeHeaders">true to include a header row in resulting CSV data</param>
        /// <param name="columnSpec">a comma-delimited list of columns to include</param>
        /// <returns>Redirect to /Admin/UserManagement</returns>
        /// <remarks>
        /// valid status values:
        /// <list type="table">
        ///     <item>
        ///         <value>empty string ("")</value>
        ///         <meaning>IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"deactivated"</value>
        ///         <meaning>IsActive = 0</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"restricted"</value>
        ///         <meaning>IsLockedOut = 1 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"unverified"</value>
        ///         <meaning>IsVerified = 0 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"unapproved"</value>
        ///         <meaning>IsApproved = 0 and IsActive = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"active"</value>
        ///         <meaning>IsActive = 1 and IsLockedOut = 0 and IsVerified = 1 and IsApproved = 1</meaning>
        ///     </item>
        ///     <item>
        ///         <value>"newsletter"</value>
        ///         <meaning>IsActive = 1 and IsLockedOut = 0 and IsVerified = 1 and IsApproved = 1 and Newsletter = 1</meaning>
        ///     </item>
        /// </list>
        /// </remarks>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ExportUserCSV(int? userid, string username, string first, string last, string email, string status, string role, string sort, bool descending, bool includeHeaders, string columnSpec)
        {
            //ViewData["userid"] = userid;
            //ViewData["username"] = username;
            //ViewData["first"] = first;
            //ViewData["last"] = last;
            //ViewData["email"] = email;
            //ViewData["status"] = status;
            //ViewData["role"] = role;
            //ViewData["sort"] = sort;
            //ViewData["descending"] = descending;            

            UserClient.GenerateUserListCSV(User.Identity.Name, userid ?? 0, username, first, last, email, status, role, sort, descending, includeHeaders, columnSpec);
            PrepareSuccessMessage("ExportUserCSV", MessageType.Method);

            return RedirectToAction(Strings.MVC.UserManagementAction,
                                    new { userid, username, first, last, email, status, role, sort, descending });
        }

        /// <summary>
        /// Displays summary account info for the specified user
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UserSummary(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("UserSummary", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }

            //get certain summary counts
            Dictionary<string, int> allCounts = AccountingClient.GetMySummaryCounts(User.Identity.Name, targetUser.UserName);
            int activeListingCount = allCounts.ContainsKey("ActiveListings") ? allCounts["ActiveListings"] : 0;
            int successfulListingCount = allCounts.ContainsKey("SuccessListings") ? allCounts["SuccessListings"] : 0;
            int unsuccessfulListingCount = allCounts.ContainsKey("UnsuccessListings") ? allCounts["UnsuccessListings"] : 0;
            int totalListingCount = allCounts.ContainsKey("AllListings") ? allCounts["AllListings"] : 0;//activeListingCount + successfulListingCount + unsuccessfulListingCount;
            int totalSaleCount = allCounts.ContainsKey("SaleLineitems") ? allCounts["SaleLineitems"] : 0;
            int totalPurchaseCount = allCounts.ContainsKey("PurchaseLineitems") ? allCounts["PurchaseLineitems"] : 0;
            ViewData["ActiveListingCount"] = activeListingCount;
            ViewData["TotalListingCount"] = totalListingCount;
            ViewData["TotalSaleCount"] = totalSaleCount;
            ViewData["TotalPurchaseCount"] = totalPurchaseCount;

            ViewData["User"] = targetUser;
            ViewData["backUrl"] = Request.QueryString["backUrl"];
            return (View(targetUser));
        }

        /// <summary>
        /// Displays form to edit the specified user
        /// </summary>
        /// <param name="id">ID of the requested user to be edited</param>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditUser(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("EditUser", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["backUrl"] = Request.QueryString["backUrl"];
            string targetUN = targetUser.UserName; // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            ViewData["AcceptedListingActionCount"] = ListingClient.GetAcceptedListingActionsByUser(actingUN, targetUN, 0, 1, Strings.Fields.Id, true).TotalItemCount;

            List<CustomProperty> userProperties = PruneUserCustomFieldsForEditAsAdmin(UserClient.Properties(actingUN, targetUN));

            //populate model state for user roles
            foreach (Role userRole in UserClient.GetRoles(User.Identity.Name))
            {
                string roleName = userRole.Name;
                string fieldName = Strings.Fields.RoleInputPrefix + roleName;
                bool isInrole = (targetUser.Roles.Count(r => r.Name == roleName) > 0);
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(isInrole.ToString(), isInrole.ToString(), null);
                ModelState.Add(fieldName, ms);
                ViewData[fieldName] = isInrole;
            }

            //populate model state for custom user properties
            ModelState.FillProperties(userProperties, cultureInfo);

            ViewData[Strings.Fields.IsApproved] = targetUser.IsApproved;
            ViewData[Strings.Fields.IsLockedOut] = targetUser.IsLockedOut;
            ViewData[Strings.Fields.IsVerified] = targetUser.IsVerified;
            ViewData[Strings.Fields.Newsletter] = targetUser.Newsletter;
            ViewData[Strings.Fields.WebAPIEnabled] = targetUser.WebAPIEnabled;

            ViewData[Strings.Fields.Properties] = userProperties;

            return (View(targetUser));
        }

        /// <summary>
        /// Displays form to edit the specified user
        /// </summary>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> EditUser()
        {
            string actingUN = User.Identity.Name; // username of logged in user
            int targetUserId;
            User targetUser = null;
            if (int.TryParse(Request.Form[RainWorx.FrameWorx.Strings.Fields.Id], out targetUserId))
            {
                targetUser = UserClient.GetUserByID(actingUN, targetUserId);
            }
            if (targetUser == null)
            {
                PrepareErrorMessage("EditUser", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["backUrl"] = Request.QueryString["backUrl"];
            string targetUN = targetUser.UserName; // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            ViewData["AcceptedListingActionCount"] = ListingClient.GetAcceptedListingActionsByUser(actingUN, targetUN, 0, 1, Strings.Fields.Id, true).TotalItemCount;

            List<CustomProperty> userProperties = PruneUserCustomFieldsForEditAsAdmin(UserClient.Properties(actingUN, targetUN));

            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(actingUN, targetUN, cultureCode, cultureCode);
            input.AddAllFormValues(this);

            string fieldName;
            foreach (Role userRole in UserClient.GetRoles(User.Identity.Name))
            {
                string roleName = userRole.Name;
                fieldName = Strings.Fields.RoleInputPrefix + roleName;
                ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            }

            fieldName = Strings.Fields.IsApproved;
            ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            fieldName = Strings.Fields.IsLockedOut;
            ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            fieldName = Strings.Fields.IsVerified;
            ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            fieldName = Strings.Fields.Newsletter;
            ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            fieldName = Strings.Fields.WebAPIEnabled;
            ViewData[fieldName] = (input.Items[fieldName] == Strings.MVC.TrueValue) ? true : false;
            try
            {
                //validates the new password, if supplied
                string newPassword = await ValidateNewPassword(input, required: false);

                ValidateUserPropertyValues(userProperties, input);

                UserClient.UpdateAllUserDetails(actingUN, input);

                //at this point, if a new username was specified and we got past the UpdateAllUserDetails call without any exceptions then use the new username
                if (input.Items.ContainsKey(Strings.Fields.UserName) && !string.IsNullOrEmpty(input.Items[Strings.Fields.UserName]))
                {
                    targetUN = input.Items[Strings.Fields.UserName];
                }

                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (await UserManager.HasPasswordAsync(targetUserId))
                    {
                        await UserManager.RemovePasswordAsync(targetUserId);
                    }
                    var result = await UserManager.AddPasswordAsync(targetUserId, newPassword);
                    if (!result.Succeeded)
                    {
                        AddErrors(result);
                    }
                }

                //re-pull user props after update
                userProperties = PruneUserCustomFieldsForEditAsAdmin(UserClient.Properties(actingUN, targetUN));

                PrepareSuccessMessage("EditUser", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
                targetUser = UserClient.GetUserByID(actingUN, targetUserId);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch
            {
                PrepareErrorMessage("EditUser", MessageType.Method);
                targetUser = UserClient.GetUserByID(actingUN, targetUserId);
            }

            User currentUser = UserClient.GetUserByUserName(actingUN, actingUN);
            if (currentUser == null)
            {
                PrepareSuccessMessage(Strings.Messages.LogoffDueToPasswordChange, MessageType.Message);
                string editUserUrl = Url.Action(Strings.MVC.EditUserAction, Strings.MVC.AdminController, new { id = targetUser.ID });
                string loginUrl = Url.Action(Strings.MVC.LogOnAction, Strings.MVC.AccountController, new { returnUrl = editUserUrl });
                return RedirectToAction(Strings.MVC.LogoffAction, Strings.MVC.AccountController, new { returnUrl = loginUrl });
            }

            ViewData[Strings.Fields.Properties] = userProperties;
            return (View(targetUser));
        }

        private async Task<string> ValidateNewPassword(UserInput input, bool required = false)
        {
            ValidationResults validation = new ValidationResults();
            string newPassword = null;
            bool passwordSupplied = input.Items.ContainsKey(Strings.Fields.Password) && !string.IsNullOrWhiteSpace(input.Items[Strings.Fields.Password]);
            if (passwordSupplied)
            {
                //check that confirmation passwords matches
                newPassword = input.Items[Strings.Fields.Password];
                string confPassword = input.Items.ContainsKey(Strings.Fields.ConfirmPassword) 
                    ? input.Items[Strings.Fields.ConfirmPassword] : string.Empty;
                if (newPassword != confPassword)
                {
                    validation.AddResult(new ValidationResult(Messages.ConfirmationPasswordMismatch, this, Strings.Fields.Password, Strings.Fields.Password, null));
                }
                var passwordResult = await UserManager.PasswordValidator.ValidateAsync(newPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var errorMessage in passwordResult.Errors)
                    {
                        if (errorMessage == "PasswordTooShort")
                        {
                            //pre-localize this one because it requires an argument
                            string localizedErr = this.ValidationResourceString(errorMessage, ConfigurationManager.AppSettings["Password_RequiredLength"] ?? "6");
                            validation.AddResult(new ValidationResult(localizedErr, this, Strings.Fields.Password, Strings.Fields.Password, null));
                        }
                        else
                        {
                            validation.AddResult(new ValidationResult(errorMessage, this, Strings.Fields.Password, Strings.Fields.Password, null));
                        }
                    }
                }
                input.Items.Remove(Strings.Fields.Password);
                if (input.Items.ContainsKey(Strings.Fields.ConfirmPassword))
                    input.Items.Remove(Strings.Fields.ConfirmPassword);
            }
            else if (required)
            {
                validation.AddResult(new ValidationResult(Messages.PasswordMissing, this, Strings.Fields.Password, Strings.Fields.Password, null));
            }

            //if any validation issues exist, throw an exception with the details
            if (!validation.IsValid)
            {
                Statix.ThrowValidationFaultContract(validation);
            }

            return newPassword;
        }

        #region User Addresses

        /// <summary>
        /// Displays form to manage addresses for the specified user
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UserAddresses(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("UserAddresses", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID ?? -1;
            ViewData["backUrl"] = Request.QueryString["backUrl"];
            return (View(UserClient.GetAddresses(User.Identity.Name, targetUser.UserName)));
        }

        /// <summary>
        /// Displays form to enter a new address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="SetBillingAddress">if supplied, the id of the invoice to set the new billing address upon success</param>
        /// <param name="SetShippingAddress">if supplied, the id of the invoice to set the new shipping address upon success</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddAddress(int id, int? SetBillingAddress, int? SetShippingAddress)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("AddAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID ?? -1;
            ViewData["backUrl"] = Request.QueryString["backUrl"];
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.SetBillingAddress] = SetBillingAddress;
            ViewData[Strings.Fields.SetShippingAddress] = SetShippingAddress;
            return (View(targetUser));
        }

        /// <summary>
        /// Processes request to add a new address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="Description">user-defined name for the new address</param>
        /// <param name="SetBillingAddress">if supplied, the id of the invoice to set the new billing address upon success</param>
        /// <param name="SetShippingAddress">if supplied, the id of the invoice to set the new shipping address upon success</param>
        /// <returns>Redirects to address management view (success) or View() (errors)</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AddAddress(int id, string Description, int? SetBillingAddress, int? SetShippingAddress)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("AddAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID ?? -1;
            string backUrl = Request.Form["backUrl"];
            ViewData["backUrl"] = backUrl;

            //new or update
            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(User.Identity.Name, targetUser.UserName,
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);

            //do call to BLL
            try
            {
                //update the address, should return an address int...
                int newAddressId = UserClient.UpdateAddress(User.Identity.Name, input);
                if (SetBillingAddress.HasValue)
                {
                    AccountingClient.SetInvoiceBillingAddress(User.Identity.Name, SetBillingAddress.Value, newAddressId);
                    PrepareSuccessMessage("SetBillingAddress", MessageType.Method);
                    return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = SetBillingAddress.Value });
                }
                else if (SetShippingAddress.HasValue)
                {
                    AccountingClient.SetInvoiceShippingAddress(User.Identity.Name, SetShippingAddress.Value, newAddressId);
                    PrepareSuccessMessage("SetShippingAddress", MessageType.Method);
                    return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = SetShippingAddress.Value });
                }
                else
                {
                    PrepareSuccessMessage("AddAddress", MessageType.Method);
                    return RedirectToAction(Strings.MVC.UserAddressesAction, new { @id = targetUser.ID, backUrl });
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                PrepareErrorMessage("AddAddress", MessageType.Method);
            }

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.SetBillingAddress] = SetBillingAddress;
            ViewData[Strings.Fields.SetShippingAddress] = SetShippingAddress;

            return View(targetUser);
        }

        /// <summary>
        /// Processes request to delete the spcified address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="addressID">ID of the address to be deleted</param>
        /// <returns>Redirect to address management view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteAddress(int id, int addressID)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("DeleteAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID;
            string backUrl = Request.Form["backUrl"];
            ViewData["backUrl"] = backUrl;
            try
            {
                UserClient.DeleteAddress(User.Identity.Name, targetUser.UserName, addressID);
                PrepareSuccessMessage("DeleteAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.UserAddressesAction, new { @id = targetUser.ID, backUrl });
        }

        /// <summary>
        /// Processes request to set the specified address as the primary address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="addressID">ID of the address to be updated</param>
        /// <returns>Redirect to address management view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetPrimaryAddress(int id, int addressID)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("SetPrimaryAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID;
            string backUrl = Request.Form["backUrl"];
            ViewData["backUrl"] = backUrl;
            try
            {
                UserClient.SetPrimaryAddressForUser(User.Identity.Name, targetUser.UserName, addressID);
                PrepareSuccessMessage("SetPrimaryAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetPrimaryAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.UserAddressesAction, new { @id = targetUser.ID, backUrl });
        }

        /// <summary>
        /// Displays form to update the specified address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="addressID">ID of the address to be updated</param>
        /// <returns>View(Address)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EditAddress(int id, int addressID)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("EditAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID;
            string backUrl = Request.Form["backUrl"];
            ViewData["backUrl"] = backUrl;
            Address address = UserClient.GetAddresses(User.Identity.Name, targetUser.UserName).Where(a => a.ID == addressID).SingleOrDefault();
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name, address.Country.ID);
            return View(address);
        }

        /// <summary>
        /// Processes request to update the specified address
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <returns>Redirect to address management view on success or View(Address) if there are errors</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditAddress(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("EditAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["PrimaryAddressID"] = targetUser.PrimaryAddressID;
            string backUrl = Request.Form["backUrl"];
            ViewData["backUrl"] = backUrl;
            //new or update
            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(User.Identity.Name, targetUser.UserName,
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);

            //do call to BLL
            try
            {
                //update the address, should return an address int...
                UserClient.UpdateAddress(User.Identity.Name, input);
                PrepareSuccessMessage("EditAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserAddressesAction, new { @id = targetUser.ID, backUrl });
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                PrepareErrorMessage("EditAddress", MessageType.Method);
            }
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            return View(new Address());
        }

        #endregion User Addresses

        #region User Credit Cards

        /// <summary>
        /// Displays form to manage credit cards for the specified user
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UserCreditCards(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("UserCreditCards", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["backUrl"] = Request.QueryString["backUrl"];

            ViewData[Strings.MVC.ViewData_BillingCreditCardId] = targetUser.BillingCreditCardID;
            List<CreditCard> dtoCards = UserClient.GetCreditCards(User.Identity.Name, targetUser.UserName);
            List<Address> dtoAddresses = UserClient.GetAddresses(User.Identity.Name, targetUser.UserName);
            List<CreditCardWithBillingAddress> cards = new List<CreditCardWithBillingAddress>();
            foreach (CreditCard creditCard in dtoCards)
            {
                Address billingAddress = dtoAddresses.Where(a => a.ID == creditCard.AddressID).SingleOrDefault();
                cards.Add(new CreditCardWithBillingAddress(creditCard, billingAddress));
            }
            return View(cards);
        }

        /// <summary>
        /// Displays form to add a credit card
        /// Processes request to add a credit card
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <returns>Redirect to "Credit Cards" view (success), View() otherwise</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddCreditCard(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("AddCreditCard", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            string backUrl = Request.QueryString["backUrl"];
            ViewData["backUrl"] = backUrl;

            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //capture user input
                UserInput userInput = new UserInput(User.Identity.Name, targetUser.UserName,
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                userInput.AddAllFormValues(this);

                //do call to BLL
                try
                {
                    UserClient.AddCreditCard(User.Identity.Name, targetUser.UserName, userInput);
                    PrepareSuccessMessage("AddCreditCard", MessageType.Method);
                    return RedirectToAction(Strings.MVC.UserCreditCardsAction, new { @id = targetUser.ID, backUrl });
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors                
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        if (issue.Key == null)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, issue.Message);
                        }
                        else
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                    PrepareErrorMessage("AddCreditCard", MessageType.Method);
                }
            }

            ViewData[Strings.MVC.ViewData_AddressList] = UserClient.GetAddresses(User.Identity.Name, targetUser.UserName);
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);

            return (View(targetUser));
        }

        /// <summary>
        /// Processes request to delete credit card
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="creditCardId">ID of credit card record to be deleted</param>
        /// <returns>Redirect to credit cards view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteCreditCard(int id, int creditCardId)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("DeleteCreditCard", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            string backUrl = Request.QueryString["backUrl"];
            ViewData["backUrl"] = backUrl;

            try
            {
                UserClient.DeleteCreditCard(User.Identity.Name, targetUser.UserName, creditCardId);
                PrepareSuccessMessage("DeleteCreditCard", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteCreditCard", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.UserCreditCardsAction, new { @id = targetUser.ID, backUrl });
        }

        /// <summary>
        /// Processes request to set credit card as default
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="creditCardId">ID of credit card record to be updated</param>
        /// <returns>Redirect to credit cards view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetDefaultCreditCard(int id, int creditCardId)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("SetDefaultCreditCard", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            string backUrl = Request.QueryString["backUrl"];
            ViewData["backUrl"] = backUrl;

            try
            {
                UserClient.SetBillingCreditCard(User.Identity.Name, targetUser.UserName, creditCardId);
                PrepareSuccessMessage("SetDefaultCreditCard", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetDefaultCreditCard", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.UserCreditCardsAction, new { @id = targetUser.ID, backUrl });
        }

        #endregion User Credit Cards

        #region User Feedback

        /// <summary>
        /// Displays form to manage feedback for the specified user
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="forOthers">if true returns feedback for others, otherwise returns feedback from others</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(User)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UserFeedback(int id, bool? forOthers, int? months, string sort, int? page, bool? descending)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("UserFeedback", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            ViewData["User"] = targetUser;
            ViewData["backUrl"] = Request.QueryString["backUrl"];

            ViewData["FeedbackForOthers"] = forOthers ?? false;
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? false;
            ViewData["FeedbackRating"] = UserClient.GetFeedbackRating(User.Identity.Name, targetUser.UserName, false);
            //ensure the # of months is exactly null, 1, 6, or 12, otherwise set it to null
            int? safeMonths = (months == null || months == 1 || months == 6 || months == 12) ? months : null;
            ViewData["Months"] = safeMonths ?? 0;
            //ensure sort value is null, "DateStamp" or "Rating", otherwise set to "DateStamp"
            string safeSort = (sort == null || sort == "DateStamp" || sort == "Rating") ? sort : "DateStamp";
            ViewData["sort"] = safeSort;
            //ensure page is 0 if null
            int safePage = (page != null ? (int)page : 0);
            int pageSize = SiteClient.PageSize;
            bool safeDescending = (descending != null ? (bool)descending : false);

            Page<Feedback> feedbackList;
            if (forOthers ?? false)
            {
                feedbackList = UserClient.GetAllFeedbackFromUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, safeSort, safeDescending);
            }
            else
            {
                feedbackList = UserClient.GetAllFeedbackToUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, safeSort, safeDescending);
            }

            return (View(feedbackList));
        }

        /// <summary>
        /// Processes request to delete a feedback record
        /// </summary>
        /// <param name="id">ID of the requested user</param>
        /// <param name="feedbackId">ID of the requested feedback record</param>
        /// <param name="forOthers">if true returns feedback for others, otherwise returns feedback from others</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <param name="backUrl">'back' url to render on resulting redirect target</param>
        /// <returns>Redirect to feedback view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteFeedback(int id, int feedbackId, bool? forOthers, int? months, string sort, int? page, bool? descending, string backUrl)
        {
            string actingUN = User.Identity.Name; // username of logged in user
            User targetUser = UserClient.GetUserByID(actingUN, id);
            if (targetUser == null)
            {
                PrepareErrorMessage("DeleteFeedback", MessageType.Method);
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }

            try
            {
                UserClient.DeleteFeedback(User.Identity.Name, feedbackId);
                PrepareSuccessMessage("DeleteFeedback", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteFeedback", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.UserFeedbackAction, new { id, forOthers, months, sort, page, descending, backUrl });
        }

        #endregion User Feedback

        /// <summary>
        /// Processes request for admin user to simulate being logged in as the specified user
        /// </summary>
        /// <param name="id">ID of the requested user account</param>
        /// <returns>Redirect to /Admin/EditUser/[id]</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult Impersonate(int id)
        {
            try
            {
                User user = UserClient.GetUserByID(User.Identity.Name, id);
                this.ImpersonateUser(user.UserName);
                //PrepareSuccessMessage();
            }
            catch
            {
                PrepareErrorMessage("Impersonate", MessageType.Method);
            }
            string backUrl = Request.QueryString["backUrl"];
            return RedirectToAction(Strings.MVC.EditUserAction, new { id, backUrl });
        }

        /// <summary>
        /// Processes request to delete a user
        /// </summary>
        /// <param name="id">ID of the requested user account</param>
        /// <returns>Redirect to /Admin/UserManagement (or /Admin/EditUser/[id] if an error occurs)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteUser(int id)
        {
            try
            {
                //int x = int.Parse(("errortest"));
                UserClient.DeleteUser(User.Identity.Name, id);
                PrepareSuccessMessage("DeleteUser", MessageType.Method);

                if (HttpContext.Request.Cookies.AllKeys.Contains(Strings.MVC.FBOUserName))
                {
                    HttpCookie cookie = new HttpCookie(Strings.MVC.FBOUserName)
                    {
                        Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
                    };
                    HttpContext.Response.Cookies.Set(cookie);
                }
                return RedirectToAction(Strings.MVC.UserManagementAction);
            }
            catch (FaultException<InvalidArgumentFaultContract> ia)
            {
                PrepareErrorMessage(ia.Detail.Reason);
                return RedirectToAction(Strings.MVC.EditUserAction, new { id });
            }
            catch
            {
                PrepareErrorMessage("DeleteUser", MessageType.Method);
                return RedirectToAction(Strings.MVC.EditUserAction, new { id });
            }
        }

        /// <summary>
        /// Processes request for admin user to simulate being logged in as the specified user
        /// </summary>
        /// <param name="id">ID of the requested user account</param>
        /// <param name="active">true to set user as active, false to set user as deactivated</param>
        /// <returns>Redirect to /Admin/EditUser/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetUserActive(int id, bool active)
        {
            try
            {
                //int x = int.Parse(("errortest"));
                User user = UserClient.GetUserByID(User.Identity.Name, id);
                UserClient.SetUserActive(User.Identity.Name, user.UserName, active);
                user.IsActive = active;
                PrepareSuccessMessage("SetUserActive", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("SetUserActive", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditUserAction, new { id });
        }

        /// <summary>
        /// Send verification email
        /// </summary>
        /// <param name="id">ID of the requested user account</param>
        /// <returns>Redirect to /Admin/EditUser/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SendVerificationEmail(int id)
        {
            try
            {
                //int x = int.Parse(("errortest"));
                User user = UserClient.GetUserByID(User.Identity.Name, id);
                user.IsVerified = false;
                user.EmailConfirmed = false;
                UserClient.UpdateUser(User.Identity.Name, user);

                NotifierClient.QueueNotification(User.Identity.Name, null, user.UserName, Strings.Templates.UserVerification,
                    Strings.DetailTypes.User, id, null, null, null, null, null);

                PrepareSuccessMessage("SendVerificationEmail", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("SendVerificationEmail", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.EditUserAction, new { id });
        }

        /// <summary>
        /// Displays Create User admin form
        /// </summary>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CreateUser()
        {
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID,
                                                              Strings.Fields.Name);
            return View();
        }

        /// <summary>
        /// Processes Create User admin form
        /// </summary>
        /// <param name="UserName">username of the user to be created</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> CreateUser(string UserName)
        {
            UserInput input = new UserInput(Strings.Roles.AnonymousUser, Strings.Roles.AnonymousUser,
                                            this.GetCookie(Strings.MVC.CultureCookie),
                                            this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);
            input.Items.Add(Strings.Fields.LastIP, Request.UserHostAddress);

            //do call to BLL
            try
            {
                string newPassword = await ValidateNewPassword(input, required: true);
                UserClient.RegisterUser(User.Identity.Name, input);
                User newUser = UserClient.GetUserByUserName(User.Identity.Name, UserName);
                await UserManager.AddPasswordAsync(newUser.ID, newPassword);
                PrepareSuccessMessage("CreateUser", MessageType.Method);

                return RedirectToAction(Strings.MVC.EditUserAction, new { id = newUser.ID });
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
            }

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            return View();
        }

        #endregion

        #region CMS

        /// <summary>
        /// Displays form and processes request to edit the content of CMS-enabled
        /// areas of the site (e.g. Homepage Accouncement)
        /// </summary>
        /// <param name="id">ID of the requested content area</param>
        /// <param name="pageTitle">The page title for user defined content items</param>
        /// <param name="Text">New html content to be displayed in the specified content area</param>
        /// <returns>View(Content)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult ContentEditor(int id, string pageTitle, string Text)
        {
            //disable browser XSS detection for this specific page because it can randomly break the HTML editor 
            //  if the content being saved legitimately contains javascript also contained in the editor library.
            Response.AddHeader("X-XSS-Protection", "0");

            Content content = SiteClient.GetContent(id);
            try
            {                
                if (Text != null)
                {
                    content.Text = Text;
                    content.PageTitle = pageTitle;
                    SiteClient.SetContent(User.Identity.Name, content);
                    PrepareSuccessMessage("ContentEditor", MessageType.Method);
                }
            }
            catch
            {
                PrepareErrorMessage("ContentEditor", MessageType.Method);
            }
            return View(content);
        }

        /// <summary>
        /// Displays list of CMS-enabled content areas
        /// </summary>
        /// <returns>View(List&lt;Content&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ContentManagement()
        {
            List<DTO.Content> retVal = SiteClient.GetAllContent();
            return View(Strings.MVC.ContentManagementAction, retVal);
        }

        /// <summary>
        /// Processes request to add custom CMS content
        /// </summary>
        /// <param name="name">the name of the content to be added</param>
        /// <param name="useHTMLEditor">indicates whether management of this content will use an HTML editor</param>
        /// <returns>redirects to /Admin/ContentManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AddUserContent(string name, bool useHTMLEditor)
        {
            try
            {
                SiteClient.CreateUserContent(User.Identity.Name, name, useHTMLEditor);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return ContentManagement();
            }
            return RedirectToAction(Strings.MVC.ContentManagementAction);
        }

        /// <summary>
        /// Processes request to delete custom CMS content
        /// </summary>
        /// <param name="name">the name of the content to be added</param>
        /// <returns>redirects to /Admin/ContentManagement</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Get)]
        public ActionResult DeleteUserContent(string name)
        {
            try
            {
                SiteClient.DeleteUserContent(User.Identity.Name, name);
            }
            catch (FaultException<InvalidArgumentFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            return RedirectToAction(Strings.MVC.ContentManagementAction);
        }

        /// <summary>
        /// Adds a set of Content rows for the specified language
        /// </summary>
        /// <param name="lang">language code for the specified language (e.g. "en" or "en-US")</param>
        /// <returns>redirect to ContentManagement view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddContentLanguage(string lang)
        {
            try
            {
                SiteClient.AddContentForCulture(User.Identity.Name, lang);
            }
            catch
            {
                PrepareErrorMessage("AddContentLanguage", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ContentManagementAction);
        }

        /// <summary>
        /// Removes a set of Content rows for the specified language
        /// </summary>
        /// <param name="lang">language code for the specified language (e.g. "en" or "en-US")</param>
        /// <returns>redirect to ContentManagement view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RemoveContentLanguage(string lang)
        {
            try
            {
                SiteClient.DeleteContentForCulture(User.Identity.Name, lang);
            }
            catch
            {
                PrepareErrorMessage("AddContentLanguage", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ContentManagementAction);
        }

        #endregion

        #region ShippingSettings

        /// <summary>
        /// Displays form and processes request to edit enabled shipping methods
        /// </summary>
        /// <returns>View(List&lt;ShippingMethod&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ShippingMethods()
        {
            List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(2);
            propertiesToUpdate.Add(SiteClient.Properties.First(p => p.Field.Name == SiteProperties.ShowShippingInfoOnItemLists));

            foreach (CustomProperty property in propertiesToUpdate)
            {
                //Add Model control
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(property.Value, property.Value, null);
                ModelState.Add(property.Field.Name, ms);
            }
            ViewData[Strings.MVC.ViewData_ShippingSiteProperties] = propertiesToUpdate;

            List<ShippingMethod> shippingMethodsToReturn;
            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //This is a save postback
                //IN (populate UserInput)
                var input = new UserInput(User.Identity.Name, User.Identity.Name,
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));

                foreach (string key in Request.Form.AllKeys.Where(k => k != null))
                {
                    input.Items.Add(key, Request.Form[key].Trim() /*== Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue :Request.Form[key]*/ );
                }

                try
                {
                    //attempt to commit the new list to the database
                    SiteClient.UpdateShippingMethods(HttpContext.User.Identity.Name, input);

                    shippingMethodsToReturn = SiteClient.ShippingMethods;

                    //prepare ModelState for output
                    foreach (ShippingMethod shipMethod in shippingMethodsToReturn)
                    {
                        //Add Model control (shipping method name)
                        var ms1 = new ModelState();
                        ms1.Value = new ValueProviderResult(shipMethod.Name, shipMethod.Name, null);
                        ModelState.Add("sm_name_" + shipMethod.ID, ms1);

                        //Add Model control (shipping method display order)
                        var ms2 = new ModelState();
                        ms2.Value = new ValueProviderResult(shipMethod.DisplayOrder, shipMethod.DisplayOrder.ToString(), null);
                        ModelState.Add("sm_order_" + shipMethod.ID, ms2);
                    }

                    PrepareSuccessMessage("ShippingMethods", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //changes were not saved, so fill attempted values back into model state
                    shippingMethodsToReturn = SiteClient.ShippingMethods;

                    //Add Model control (shipping method name)
                    var ms4 = new ModelState();
                    ms4.Value = new ValueProviderResult(input.Items[Strings.Fields.NewShippingMethodName], input.Items[Strings.Fields.NewShippingMethodName], null);
                    ModelState.Add("sm_name_new", ms4);

                    //Add Model control (display order)
                    var ms5 = new ModelState();
                    ms5.Value = new ValueProviderResult(
                        input.Items[Strings.Fields.NewShippingMethodDisplayOrder],
                        input.Items[Strings.Fields.NewShippingMethodDisplayOrder], null);
                    ModelState.Add("sm_order_new", ms5);

                    foreach (ShippingMethod shipMethod in shippingMethodsToReturn)
                    {
                        string nameKey = "sm_name_" + shipMethod.ID;
                        string dispOrdKey = "sm_order_" + shipMethod.ID;
                        string delChkbxKey = "sm_delete_" + shipMethod.ID;

                        //Add Model control (shipping method name)
                        var ms1 = new ModelState();
                        ms1.Value = new ValueProviderResult(
                            input.Items[nameKey],
                            input.Items[nameKey], null);
                        ModelState.Add(nameKey, ms1);

                        //Add Model control (display order)
                        var ms2 = new ModelState();
                        ms2.Value = new ValueProviderResult(
                            input.Items[dispOrdKey],
                            input.Items[dispOrdKey], null);
                        ModelState.Add(dispOrdKey, ms2);

                        //Add Model control (delete checkbox)
                        var ms3 = new ModelState();
                        ms3.Value = new ValueProviderResult(
                            input.Items[delChkbxKey] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : input.Items[delChkbxKey],
                            input.Items[delChkbxKey] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : input.Items[delChkbxKey],
                            null);
                        ModelState.Add(delChkbxKey, ms3);
                    }

                    //display validation errors                
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                }
                catch
                {
                    shippingMethodsToReturn = SiteClient.ShippingMethods;
                    PrepareErrorMessage("ShippingMethods", MessageType.Method);
                }

                return View(Strings.MVC.ShippingMethodsAction, shippingMethodsToReturn);
            }
            else
            {
                //This is a load
                shippingMethodsToReturn = SiteClient.ShippingMethods;

                foreach (ShippingMethod shipMethod in shippingMethodsToReturn)
                {
                    //Add Model control (shipping method name)
                    ModelState ms1 = new ModelState();
                    ms1.Value = new ValueProviderResult(shipMethod.Name, shipMethod.Name, null);
                    ModelState.Add("sm_name_" + shipMethod.ID.ToString(), ms1);

                    //Add Model control (display order)
                    ModelState ms2 = new ModelState();
                    ms2.Value = new ValueProviderResult(shipMethod.DisplayOrder, shipMethod.DisplayOrder.ToString(), null);
                    ModelState.Add("sm_order_" + shipMethod.ID.ToString(), ms2);
                }

                ////display validation errors, if applicable
                //CheckValidationIssues();

                return View(Strings.MVC.ShippingMethodsAction, shippingMethodsToReturn);
            }
        }

        /// <summary>
        /// Processes request to enable a standard group of shipping methods
        /// </summary>
        /// <param name="subset">shipping method group code (e.g. "ups-d" for all Domestic UPS Options)</param>
        /// <returns>Redirect to /Admin/ShippingMethods</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddStandardShippingMethods(string subset)
        {
            var shippingMethodsToAdd = new List<string>();
            if (string.IsNullOrEmpty(subset) || subset == "ups-d")
                foreach (string sm in Strings.ShippingMethods.UpsDomesticGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "ups-i")
                foreach (string sm in Strings.ShippingMethods.UpsInternationalGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "fedex-d")
                foreach (string sm in Strings.ShippingMethods.FedexDomesticGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "fedex-i")
                foreach (string sm in Strings.ShippingMethods.FedexInternationalGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "usps-d")
                foreach (string sm in Strings.ShippingMethods.UspsDomesticGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "usps-i")
                foreach (string sm in Strings.ShippingMethods.UspsInternationalGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "dhl-d")
                foreach (string sm in Strings.ShippingMethods.DhlDomesticGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            if (string.IsNullOrEmpty(subset) || subset == "dhl-i")
                foreach (string sm in Strings.ShippingMethods.DhlInternationalGroup.Split(','))
                    AppendShippingMethodToList(sm, shippingMethodsToAdd);
            try
            {
                SiteClient.AddShippingMethods(HttpContext.User.Identity.Name, shippingMethodsToAdd);
                PrepareSuccessMessage("AddStandardShippingMethods", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("AddStandardShippingMethods", MessageType.Method);
            }
            return (RedirectToAction(Strings.MVC.AdminShippingMethodsAction, Strings.MVC.AdminController));
        }

        /// <summary>
        /// Appends the friendly shipping method name which matches
        /// the specified code to the specified list of strings
        /// </summary>
        /// <param name="smCode">shipping method code (e.g. "UPSGND" appends "UPS Ground" to the list)</param>
        /// <param name="smList">the list of strings to be appended to</param>
        private static void AppendShippingMethodToList(string smCode, ICollection<string> smList)
        {
            switch (smCode)
            {
                case "UPSNDA": smList.Add(Strings.ShippingMethods.UPSNDA);
                    break;
                case "UPSNDE": smList.Add(Strings.ShippingMethods.UPSNDE);
                    break;
                case "UPSNDAS": smList.Add(Strings.ShippingMethods.UPSNDAS);
                    break;
                case "UPSNDS": smList.Add(Strings.ShippingMethods.UPSNDS);
                    break;
                case "UPS2DE": smList.Add(Strings.ShippingMethods.UPS2DE);
                    break;
                case "UPS2ND": smList.Add(Strings.ShippingMethods.UPS2ND);
                    break;
                case "UPS3DS": smList.Add(Strings.ShippingMethods.UPS3DS);
                    break;
                case "UPSGND": smList.Add(Strings.ShippingMethods.UPSGND);
                    break;
                case "UPSCAN": smList.Add(Strings.ShippingMethods.UPSCAN);
                    break;
                case "UPSWEX": smList.Add(Strings.ShippingMethods.UPSWEX);
                    break;
                case "UPSWSV": smList.Add(Strings.ShippingMethods.UPSWSV);
                    break;
                case "UPSWEP": smList.Add(Strings.ShippingMethods.UPSWEP);
                    break;
                case "FDX2D": smList.Add(Strings.ShippingMethods.FDX2D);
                    break;
                case "FDXES": smList.Add(Strings.ShippingMethods.FDXES);
                    break;
                case "FDXFO": smList.Add(Strings.ShippingMethods.FDXFO);
                    break;
                case "FDXPO": smList.Add(Strings.ShippingMethods.FDXPO);
                    break;
                case "FDXPOS": smList.Add(Strings.ShippingMethods.FDXPOS);
                    break;
                case "FDXSO": smList.Add(Strings.ShippingMethods.FDXSO);
                    break;
                case "FDXGND": smList.Add(Strings.ShippingMethods.FDXGND);
                    break;
                case "FDXHD": smList.Add(Strings.ShippingMethods.FDXHD);
                    break;
                case "FDXIGND": smList.Add(Strings.ShippingMethods.FDXIGND);
                    break;
                case "FDXIE": smList.Add(Strings.ShippingMethods.FDXIE);
                    break;
                case "FDXIF": smList.Add(Strings.ShippingMethods.FDXIF);
                    break;
                case "FDXIP": smList.Add(Strings.ShippingMethods.FDXIP);
                    break;
                case "USPFC": smList.Add(Strings.ShippingMethods.USPFC);
                    break;
                case "USPEXP": smList.Add(Strings.ShippingMethods.USPEXP);
                    break;
                case "USPBPM": smList.Add(Strings.ShippingMethods.USPBPM);
                    break;
                case "USPLIB": smList.Add(Strings.ShippingMethods.USPLIB);
                    break;
                case "USPMM": smList.Add(Strings.ShippingMethods.USPMM);
                    break;
                case "USPPM": smList.Add(Strings.ShippingMethods.USPPM);
                    break;
                case "USPPP": smList.Add(Strings.ShippingMethods.USPPP);
                    break;
                case "USPFCI": smList.Add(Strings.ShippingMethods.USPFCI);
                    break;
                case "USPPMI": smList.Add(Strings.ShippingMethods.USPPMI);
                    break;
                case "USPEMI": smList.Add(Strings.ShippingMethods.USPEMI);
                    break;
                case "USPGXG": smList.Add(Strings.ShippingMethods.USPGXG);
                    break;
                case "DHL2D": smList.Add(Strings.ShippingMethods.DHL2D);
                    break;
                case "DHLEXA": smList.Add(Strings.ShippingMethods.DHLEXA);
                    break;
                case "DHLEXM": smList.Add(Strings.ShippingMethods.DHLEXM);
                    break;
                case "DHLEXP": smList.Add(Strings.ShippingMethods.DHLEXP);
                    break;
                case "DHLGND": smList.Add(Strings.ShippingMethods.DHLGND);
                    break;
                case "DHLWPE": smList.Add(Strings.ShippingMethods.DHLWPE);
                    break;
            }
        }

        /// <summary>
        /// Processes request to update shipping-specific site proerties
        /// </summary>
        /// <returns>Redirect to shipping admin page</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UpdateShippingProperties()
        {
            List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(2);
            propertiesToUpdate.Add(SiteClient.Properties.First(p => p.Field.Name == SiteProperties.ShowShippingInfoOnItemLists));

            //capture user input
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
            input.AddAllFormValues(this);

            //attempt to update site properties
            try
            {
                SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
                SiteClient.Reset();
                PrepareSuccessMessage("UpdateShippingProperties", MessageType.Method);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //store validation issues to be displayed by redirect target
                //StoreValidationIssues(this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues), input);
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return ShippingMethods();
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("UpdateShippingProperties", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ShippingMethodsAction);
        }

        #endregion

        #region Site Fee Invoices

        /// <summary>
        /// Displays a page of a list of unpaid site fee invoices
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of the requested sort option defined in QuerySortDefinitions.SiteFeesReportOptions</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult NewSiteFeesReport(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            //capture date inputs (parse with local culture)
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
            DateTime? fromDate = null;
            if (!string.IsNullOrEmpty(Request[Strings.Fields.FromDate]))
            {
                string key = Strings.Fields.FromDate;
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Request[key], Request[key], null);
                ModelState.Add(key, ms);
                DateTime temp;
                if (DateTime.TryParse(Request[key], cultureInfo, DateTimeStyles.None, out temp))
                {
                    fromDate = temp;
                }
                else
                {
                    ModelState.AddModelError(key,
                                             this.ResourceString("Validation, CustomField_ConvertDateTime",
                                                                 this.AdminResourceString(key)));
                }
            }
            DateTime? toDate = null;
            if (!string.IsNullOrEmpty(Request[Strings.Fields.ToDate]))
            {
                string key = Strings.Fields.ToDate;
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Request[key], Request[key], null);
                ModelState.Add(key, ms);
                DateTime temp;
                if (DateTime.TryParse(Request[key], cultureInfo, DateTimeStyles.None, out temp))
                {
                    toDate = temp;
                }
                else
                {
                    ModelState.AddModelError(key,
                                             this.ResourceString("Validation, CustomField_ConvertDateTime",
                                                                 this.AdminResourceString(key)));
                }
            }

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.SiteFeesReportOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.AdminResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.SiteFeesReportOptions[SortFilterOptions.Value];

            //refresh site fees
            AccountingClient.RefreshSiteFees(User.Identity.Name);

            // GetNewInvoicesByDate GetPaidInvoicesByDate
            Page<Invoice> retVal = AccountingClient.GetNewInvoicesByDate(User.Identity.Name, fromDate, toDate, page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            return View(retVal);
        }

        /// <summary>
        /// Processes request to set maximum log age to the specified number of days
        /// </summary>
        /// <param name="days">specified number of days</param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetLogAgeDays(int days)
        {
            SiteClient.UpdateSetting(User.Identity.Name, "LogAgeDays", days.ToString(), this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture);
            SiteClient.Reset();
            return RedirectToAction("EventLog");
        }

        /// <summary>
        /// Processes request to enable or disable logging 
        /// </summary>
        /// <param name="enabled">true to enable logging</param>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetLoggingEnabled(bool enabled)
        {
            if (!enabled) LogManager.WriteLog("Event Log Disabled", "Event Log", Strings.FunctionalAreas.Site, TraceEventType.Information, User.Identity.Name, null, new Dictionary<string, object>() { { "enabled", enabled } });

            SiteClient.UpdateSetting(User.Identity.Name, "EnableLogging", enabled.ToString(), this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture);
            SiteClient.Reset();

            if (enabled) LogManager.WriteLog("Event Log Enabled", "Event Log", Strings.FunctionalAreas.Site, TraceEventType.Information, User.Identity.Name, null, new Dictionary<string, object>() { { "enabled", enabled } });

            return RedirectToAction("EventLog");
        }


        /// <summary>
        /// Processes request to set loggin options
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateLoggingSettings()
        {
            Category eventLogCategory = CommonClient.GetCategoryByID(723);
            List<CustomProperty> propertiesToUpdate = SiteClient.Properties.WhereContainsFields(eventLogCategory.CustomFieldIDs);

            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
            input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });

            //validate specific admin settings if applicable
            var validation = new ValidationResults();

            CheckSitePropertyRules(propertiesToUpdate, input, validation);

            try
            {
                if (!validation.IsValid)
                {
                    Statix.ThrowValidationFaultContract(validation);
                }

                SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
                SiteClient.Reset();
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
            }

            return RedirectToAction(Strings.MVC.EventLogAction);
        }

        /// <summary>
        /// Processes request to clear the log
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ClearEventLog()
        {
            CommonClient.TruncateLogEntries(User.Identity.Name, 0);
            return RedirectToAction("EventLog");
        }

        /// <summary>
        /// Displays a page of log results
        /// </summary>
        /// <param name="FunctionalArea">optional string to filter results (exact match)</param>
        /// <param name="Severity">optional string to filter results (exact match)</param>
        /// <param name="FromDate">local date/time to specify lower bound of oldest entries</param>
        /// <param name="ToDate">local date/time to specify upper bound of newest entries</param>
        /// <param name="SearchTerm">optional string to filter results (fulltext search)</param>
        /// <param name="page">the 0-based index of which page of results to retrieve</param>
        /// <returns>Page&lt;LogEntry&gt;</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult EventLog(string[] FunctionalArea, string[] Severity, string FromDate, string ToDate, string SearchTerm, int? page)
        {
            int eventLogPageSize = 50; // SiteClient.PageSize;

            if (FunctionalArea == null)
            {
                FunctionalArea = new string[] { "All" };
            }
            else if (FunctionalArea.Length == 1 && FunctionalArea[0].Contains(","))
            {
                var tempVals = new List<string>();
                foreach (string value in FunctionalArea[0].Split(','))
                {
                    tempVals.Add(value.Trim());
                }
                FunctionalArea = tempVals.ToArray();
                ModelState["FunctionalArea"] = new ModelState()
                {
                    Value = new ValueProviderResult(FunctionalArea, string.Join(",", FunctionalArea), CultureInfo.InvariantCulture)
                };
            }
            if (FunctionalArea.Any(s => s == "All") && FunctionalArea.Length > 1)
            {
                FunctionalArea = new string[] { "All" };
            }

            if (Severity == null)
            {
                Severity = new string[] { "Information", "Error", "Warning" };
            }
            else if (Severity.Length == 1 && Severity[0].Contains(","))
            {
                var tempVals = new List<string>();
                foreach (string value in Severity[0].Split(','))
                {
                    tempVals.Add(value.Trim());
                }
                Severity = tempVals.ToArray();
                ModelState["Severity"] = new ModelState() {
                    Value = new ValueProviderResult(Severity, string.Join(",", Severity), CultureInfo.InvariantCulture)
                };
            }
            if (Severity.Any(s => s == "All") && Severity.Length > 1)
            {
                Severity = new string[] { "All" };
            }

            ViewData["FunctionalArea"] = FunctionalArea;
            ViewData["Severity"] = Severity;
            ViewData["SearchTerm"] = SearchTerm;

            var cultureInfo = this.GetCultureInfo();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

            Category eventLogCategory = CommonClient.GetCategoryByID(723);
            List<CustomProperty> propertiesToDisplay = SiteClient.Properties.WhereContainsFields(eventLogCategory.CustomFieldIDs);
            ViewData["LogOptionSiteProps"] = propertiesToDisplay;
            ModelState.FillProperties(propertiesToDisplay, cultureInfo);

            DateTime? utcFromDate = null;
            DateTime temp1;
            if (!string.IsNullOrWhiteSpace(FromDate) && DateTime.TryParse(FromDate, cultureInfo, DateTimeStyles.None, out temp1))
            {
                utcFromDate = TimeZoneInfo.ConvertTime(temp1, siteTimeZone, TimeZoneInfo.Utc);
            }
            DateTime? utcToDate = null;
            DateTime temp2;
            if (!string.IsNullOrWhiteSpace(ToDate) && DateTime.TryParse(ToDate, cultureInfo, DateTimeStyles.AdjustToUniversal, out temp2))
            {
                utcToDate = TimeZoneInfo.ConvertTime(temp2, siteTimeZone, TimeZoneInfo.Utc);
            }

            if ((page ?? 0) == 0)
            {
                CommonClient.ClearCache("LogEntryData");
            }

            Page<LogEntry> retVal = CommonClient.GetLogEntries(User.Identity.Name,
                FunctionalArea.Any(selVal => selVal == "All") ? null : string.Join(",", FunctionalArea), 
                Severity.Any(selVal => selVal == "All") ? null : string.Join(",", Severity),
                utcFromDate, utcToDate, SearchTerm,
                page == null ? 0 : (int)page, eventLogPageSize, "EntryDateStamp", true);

            return View(retVal);
        }

        /// <summary>
        /// Returns log entry stats for the range and severity specified
        /// </summary>
        /// <param name="DataIncrement">'MINUTE', 'HOUR' or 'DAY'</param>
        /// <param name="FromDate">local date/time to specify lower bound of oldest entries</param>
        /// <param name="ToDate">local date/time to specify upper bound of newest entries</param>
        /// <param name="DisplayWidth">the width, in pixels, of the target container where the chart will be displayed</param>
        /// <param name="Severity">optional string to filter results (exact match)</param>
        /// <returns>Dictionary&lt;string, List&lt;EventLogStat&gt;&gt;</returns>
        public ActionResult EventLogHistogram(string DataIncrement, string FromDate, string ToDate, double? DisplayWidth, string[] Severity)
        {
            //if (Severity == null)
            //{
            //    Severity = new string[] { "Information", "Error", "Warning" };
            //}
            //else if (Severity.Length == 1 && Severity[0].Contains(","))
            //{
            //    var tempVals = new List<string>();
            //    foreach (string value in Severity[0].Split(','))
            //    {
            //        tempVals.Add(value.Trim());
            //    }
            //    Severity = tempVals.ToArray();
            //    ModelState["Severity"] = new ModelState()
            //    {
            //        Value = new ValueProviderResult(Severity, string.Join(",", Severity), CultureInfo.InvariantCulture)
            //    };
            //}
            //if (Severity.Any(s => s == "All") && Severity.Length > 1)
            //{
            //    Severity = new string[] { "All" };
            //}
            //var severitiesToRetrieve = Severity;
            //var severitiesToRetrieve = new string[] { "Information", "Warning", "Error" };
            var severitiesToRetrieve = new string[] { "Warning", "Error" };

            ViewData["DataIncrement"] = DataIncrement ?? "HOUR"; //'MINUTE', 'HOUR' or 'DAY'
            ViewData["DisplayWidth"] = DisplayWidth ?? 700D;

            var cultureInfo = this.GetCultureInfo();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

            DateTime utcFromDate;
            DateTime localDtmTemp1;
            if (!DateTime.TryParse(FromDate, cultureInfo, DateTimeStyles.None, out localDtmTemp1))
            {
                utcFromDate = DateTime.UtcNow.AddHours(-48); //default 48 hours ago if missing
            }
            else
            {
                utcFromDate = TimeZoneInfo.ConvertTime(localDtmTemp1, siteTimeZone, TimeZoneInfo.Utc);
            }
            DateTime utcToDate;
            DateTime localDtmTemp2;
            if (!DateTime.TryParse(ToDate, cultureInfo, DateTimeStyles.None, out localDtmTemp2))
            {
                utcToDate = DateTime.UtcNow; //default "now" if missing
            }
            else
            {
                utcToDate = TimeZoneInfo.ConvertTime(localDtmTemp2, siteTimeZone, TimeZoneInfo.Utc);
            }

            ViewData["utcFromDate"] = utcFromDate;
            ViewData["utcToDate"] = utcToDate;

            var results = new Dictionary<string, List<EventLogStat>>();
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach(string severity in severitiesToRetrieve)
                {
                    results.Add(severity, new List<EventLogStat>());
                    using (SqlCommand command = new SqlCommand("RWX_GetLogStats", connection) { CommandType = CommandType.StoredProcedure })
                    {
                        command.Parameters.AddWithValue("@Severity", severity);
                        command.Parameters.AddWithValue("@DataIncrement", DataIncrement);
                        command.Parameters.AddWithValue("@FromDate", utcFromDate);
                        command.Parameters.AddWithValue("@ToDate", utcToDate);
                        SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                        while (reader.Read())
                        {
                            //RWX_LogStats columns: FromDate, RangeMinutes, Severity, EntryCount, IsArchived
                            results[severity].Add(new EventLogStat()
                            {
                                FromDate = (DateTime)reader[0],
                                RangeMinutes = (int)reader[1],
                                Severity = (string)reader[2],
                                EntryCount = (int)reader[3],
                                IsArchived = (bool)reader[4]
                            });
                        }
                        reader.Close();
                    }
                }
                connection.Close();
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of a list of paid site fee invoices
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of the requested sort option defined in QuerySortDefinitions.SiteFeesReportOptions</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SiteFeesReport(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            //capture date inputs (parse with local culture)
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
            DateTime? fromDate = null;
            if (!string.IsNullOrEmpty(Request[Strings.Fields.FromDate]))
            {
                string key = Strings.Fields.FromDate;
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Request[key], Request[key], null);
                ModelState.Add(key, ms);
                DateTime temp;
                if (DateTime.TryParse(Request[key], cultureInfo, DateTimeStyles.None, out temp))
                {
                    fromDate = temp;
                }
                else
                {
                    ModelState.AddModelError(key,
                                             this.ResourceString("Validation, CustomField_ConvertDateTime",
                                                                 this.AdminResourceString(key)));
                }
            }
            DateTime? toDate = null;
            if (!string.IsNullOrEmpty(Request[Strings.Fields.ToDate]))
            {
                string key = Strings.Fields.ToDate;
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Request[key], Request[key], null);
                ModelState.Add(key, ms);
                DateTime temp;
                if (DateTime.TryParse(Request[key], cultureInfo, DateTimeStyles.None, out temp))
                {
                    toDate = temp;
                }
                else
                {
                    ModelState.AddModelError(key,
                                             this.ResourceString("Validation, CustomField_ConvertDateTime",
                                                                 this.AdminResourceString(key)));
                }
            }

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.SiteFeesReportOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.AdminResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.SiteFeesReportOptions[SortFilterOptions.Value];

            // GetNewInvoicesByDate GetPaidInvoicesByDate
            Page<Invoice> retVal = AccountingClient.GetPaidInvoicesByDate(User.Identity.Name, fromDate, toDate, page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            return View(retVal);
        }
        #endregion

        #region Invoices

        /// <summary>
        /// Helper function which determines selected billing address, if possible 
        /// </summary>
        /// <param name="invoice">invoice to be evaluated</param>
        /// <param name="payerAddresses">list of payer's existing address records</param>
        /// <returns>The ID of the matching billing address, or null if none match</returns>
        private int? GetBillingAddrId(Invoice invoice, List<Address> payerAddresses)
        {
            int? retVal = null;
            if (invoice != null && payerAddresses != null)
            {
                int billingAddressId = 0;
                if (invoice.Payer.BillingAddressID != null)
                    billingAddressId = (int)invoice.Payer.BillingAddressID;
                foreach (Address addr in payerAddresses)
                {
                    if (invoice.BillingCity == addr.City &&
                        invoice.BillingCountry == addr.Country.Code &&
                        invoice.BillingFirstName == addr.FirstName &&
                        invoice.BillingLastName == addr.LastName &&
                        invoice.BillingStateRegion == addr.StateRegion &&
                        invoice.BillingStreet1 == addr.Street1 &&
                        invoice.BillingStreet2 == addr.Street2 &&
                        invoice.BillingZipPostal == addr.ZipPostal)
                    {
                        billingAddressId = addr.ID;
                    }
                }
                if (billingAddressId > 0)
                {
                    retVal = billingAddressId;
                }
            }
            return retVal;
        }

        /// <summary>
        /// Helper function which determines selected shipping address, if possible 
        /// </summary>
        /// <param name="invoice">invoice to be evaluated</param>
        /// <param name="payerAddresses">list of payer's existing address records</param>
        /// <returns>The ID of the matching shipping address, or null if none match</returns>
        private static int? GetShippingAddrId(Invoice invoice, IEnumerable<Address> payerAddresses)
        {
            int? retVal = null;
            if (invoice != null && payerAddresses != null)
            {
                int shippingAddressId = 0;
                if (invoice.Payer.PrimaryAddressID != null)
                    shippingAddressId = (int)invoice.Payer.PrimaryAddressID;
                foreach (Address addr in payerAddresses)
                {
                    if (invoice.ShippingCity == addr.City &&
                        invoice.ShippingCountry == addr.Country.Code &&
                        invoice.ShippingFirstName == addr.FirstName &&
                        invoice.ShippingLastName == addr.LastName &&
                        invoice.ShippingStateRegion == addr.StateRegion &&
                        invoice.ShippingStreet1 == addr.Street1 &&
                        invoice.ShippingStreet2 == addr.Street2 &&
                        invoice.ShippingZipPostal == addr.ZipPostal)
                    {
                        shippingAddressId = addr.ID;
                    }
                }
                if (shippingAddressId > 0)
                {
                    retVal = shippingAddressId;
                }
            }
            return retVal;
        }

        /// <summary>
        /// Displays form to edit applicable invoice details
        /// </summary>
        /// <param name="id">ID of the requested invoice</param>
        /// <param name="approved">indicates result of payment attempt</param>
        /// <param name="message">payment result message key</param>
        /// <param name="creditCardId">ID of the credit card used for payment, if applicable</param>
        /// <param name="page">index of the page of lineitems to be displayed (default 0)</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>View(Invoice)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult InvoiceDetail(int id, bool? approved, string message, int? creditCardId, int? page, string returnUrl)
        {
            Invoice invoice = null;
            try
            {
                invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, id);
            }
            catch
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return View(Strings.MVC.InvoiceDetailAction);
            }
            if (invoice == null)
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return View(Strings.MVC.InvoiceDetailAction);
            }
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            ViewData[Strings.Fields.InvoiceID] = id;
            ViewData[Strings.Fields.Approved] = approved;
            ViewData[Strings.MVC.ViewData_Message] = message;

            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem() { Text = this.GlobalResourceString("Credit"), Value = Strings.Fields.AdjustmentAmountCredit });
            items.Add(new SelectListItem() { Text = this.GlobalResourceString("Debit"), Value = Strings.Fields.AdjustmentAmountDebit });
            ViewData[Strings.Fields.AdjustmentAmountTypes] = items;

            if (invoice.Type != Strings.InvoiceTypes.Fee)
            {
                List<ShippingMethodCounts> shippingMethodCounts = null;
                List<LineItem> similarLineItems = AccountingClient.GetSimilarLineItems(User.Identity.Name, id, ref shippingMethodCounts);
                ViewData[Strings.MVC.ViewData_SimilarLineItems] = similarLineItems;
                if (invoice.Type == Strings.InvoiceTypes.Shipping)
                {
                    if (invoice.Status != InvoiceStatuses.Paid && invoice.Status != InvoiceStatuses.Pending)
                    {
                        List<InvoiceShippingOption> shippingOptions = AccountingClient.GetShippingOptionsForInvoice(User.Identity.Name, invoice.ID);
                        invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, id);
                        List<SelectListItem> shippingItems = new List<SelectListItem>(shippingOptions.Count);
                        foreach (InvoiceShippingOption shippingOption in shippingOptions)
                        {
                            SelectListItem item = new SelectListItem();
                            item.Text = string.Format(Strings.Formats.ShippingMethodName, shippingOption.ShippingOption.Method.Name,
                                                      SiteClient.FormatCurrency(shippingOption.Amount, invoice.Currency,
                                                                                this.GetCookie(Strings.MVC.CultureCookie)));
                            
                            //removed mainly because this information is confusing when included in the shipping dropdown
                            //   the meaning is "if this option is selected, this many additional items will still be eligible to add to invoice
                            //if (shippingMethodCounts != null)
                            //{
                            //    foreach (ShippingMethodCounts smc in shippingMethodCounts)
                            //    {
                            //        if (smc.Method.ID == shippingOption.ShippingOption.Method.ID)
                            //        {
                            //            item.Text = item.Text + " (" + smc.Count + " " + this.GlobalResourceString("SimilarLineItems") + ")";
                            //        }
                            //    }
                            //}

                            item.Value = shippingOption.ShippingOption.ID.ToString();

                            //if (shippingOptions.Count == 1 && invoice.ShippingOption == null)
                            shippingItems.Add(item);
                        }
                        ViewData[Strings.Fields.ShippingOption] = new SelectList(shippingItems, Strings.Fields.Value, Strings.Fields.Text,
                                                                    invoice.ShippingOption == null ? null : invoice.ShippingOption.ID.ToString());
                    }
                    else
                    {
                        ViewData[Strings.Fields.ShippingOption] =
                            string.Format(Strings.Formats.ShippingMethodName, invoice.ShippingOption == null ? null : invoice.ShippingOption.Method.Name
                                , SiteClient.FormatCurrency(invoice.ShippingAmount,
                                    invoice.Currency, this.GetCookie(Strings.MVC.CultureCookie))
                        );
                    }
                }
            }

            //unnecessary if invoice is paid or it's not the payer viewing their invoice...
            if (invoice.Status != Strings.InvoiceStatuses.Paid && this.FBOUserName() == invoice.Payer.UserName)
            {
                User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                //user's credit cards
                List<CreditCard> creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
                //user's addresses
                List<Address> currentUserAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
                ViewData[Strings.MVC.ViewData_AddressList] = currentUserAddresses;
                //payer's billing address id
                ViewData[Strings.MVC.ViewData_PayerBillingAddressId] = GetBillingAddrId(invoice, currentUserAddresses);
                //credit card types
                ViewData[Strings.Fields.CreditCardTypes] = new SelectList(SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
                //selected credit card id
                ViewData[Strings.MVC.ViewData_SelectedCreditCardId] = currentUser.BillingCreditCardID;
                //credit cards
                ViewData[Strings.MVC.ViewData_CreditCards] = creditCards;
                //payment provider views
                ViewData[Strings.MVC.ViewData_PaymentProviderViews] = AccountingClient.GetPaymentProviderViewsForInvoice(User.Identity.Name, invoice);
            }

            //pagination is only implemented for site fee invoices
            if (invoice.Type == Strings.InvoiceTypes.Fee)
            {
                //int totalItemCount = invoice.LineItems.Count;
                //int pageIndex = page ?? 0;
                //int pageSize = SiteClient.InvoicePageSize;
                //List<LineItem> tempPageOfLineItems =
                //    invoice.LineItems.Skip(pageIndex*pageSize).Take(pageSize).ToList();
                //var pageOfLineItems =
                //    new Page<LineItem>(tempPageOfLineItems, pageIndex, pageSize, totalItemCount, "");
                var pageOfLineItems = AccountingClient.GetLineItemsByInvoice(User.Identity.Name, invoice.ID, page ?? 0,
                                                                             SiteClient.InvoicePageSize,
                                                                             Strings.Fields.DateStamp, false);
                ViewData[Strings.MVC.ViewData_PageOfLineitems] = pageOfLineItems;
            }
            else
            {
                var pageOfLineItems = AccountingClient.GetLineItemsByInvoice(User.Identity.Name, invoice.ID, 0,
                                                                             0, // page size=0 returns all line items
                                                                             Strings.Fields.DateStamp, false);
                ViewData[Strings.MVC.ViewData_PageOfLineitems] = pageOfLineItems;
            }

            if (approved.HasValue)
            {
                if (approved.Value == true)
                {
                    PrepareSuccessMessage(Messages.PaymentProcessedSuccessfully, MessageType.Method);
                }
            }

            //CheckValidationIssues();

            return View(Strings.MVC.InvoiceDetailAction, invoice);
        }

        /// <summary>
        /// Processes request to pay invoice
        /// </summary>
        /// <param name="id">ID of the requested invoice</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <param name="formCollection">details of the payment request</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult InvoiceDetail(int id, string returnUrl, FormCollection formCollection)
        {
            try
            {
                string providerName = formCollection[Strings.MVC.Field_Provider];
                PaymentProviderResponse result = null;
                if (!string.IsNullOrEmpty(providerName))
                {
                    PaymentParameters paymentParameters = new PaymentParameters();
                    paymentParameters.PayerIPAddress = Request.UserHostAddress;
                    foreach (string key in formCollection.AllKeys.Where(k => k != null))
                    {
                        paymentParameters.Items.Add(key, formCollection[key]);

                    }
                    //it's probably best to let the payment provider populate the rest of the PaymentParameters fields, 
                    //  since different providers will use different data and we don't want anything provider-specific here
                    result = AccountingClient.ProcessSynchronousPayment(HttpContext.User.Identity.Name, providerName, id,
                                                                        paymentParameters);
                    //TODO: decide what to do here... display error message if payment was unsuccessful? etc.                
                    //save new card if a new card was entered, approved and the "save" box was checked
                    if (formCollection[Strings.Fields.SelectedCreditCardId] == "0" &&
                        formCollection[Strings.Fields.SaveNewCard] != null &&
                        result.Approved) // only save new card if requested AND the charge was approved
                    {
                        bool saveNewCard;
                        if (bool.TryParse(formCollection[Strings.Fields.SaveNewCard].Split(',')[0], out saveNewCard))
                        {
                            if (saveNewCard)
                            {
                                //capture user input
                                UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                                userInput.AddAllFormValues(this);

                                //do call to BLL
                                try
                                {
                                    UserClient.AddCreditCard(User.Identity.Name, this.FBOUserName(), userInput);
                                }
                                catch (FaultException<ValidationFaultContract> vfc)
                                {
                                    //display validation errors                
                                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                                    {
                                        ModelState.AddModelError(issue.Key, issue.Message);
                                    }
                                }
                                catch (Exception e)
                                {
                                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                                }
                            }
                        }
                    }
                }
                else
                {
                    //o no! we don't know what payment provider was used.
                    //TODO: handle this condition
                }
                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl, result.Approved, message = result.ResponseDescription });
            }
            catch
            {
                PrepareErrorMessage("InvoiceDetail", MessageType.Method);
                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl });
            }
        }

        /// <summary>
        /// Processes request to add a line item to the specified invoice
        /// </summary>
        /// <param name="InvoiceID">ID of the requested invoice to be adjusted</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to /Admin/InvoiceDetail/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AddInvoiceAdjustment(int InvoiceID, string returnUrl)
        {
            /*
             * expected input fields:
             * 
                "InvoiceID" (hidden)
                "adjustmentDescription" (text input)
                "adjustmentCreditDebit" (dropdown, either Html.GlobalResource("Credit") or Html.GlobalResource("Debit"))
                "adjustmentAmount" (positive decimal value)
             * 
             */

            //capture user input
            UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            userInput.AddAllFormValues(this);

            //do call to BLL
            try
            {
                //add the adjustment, should return an address int...
                AccountingClient.AddInvoiceAdjustment(User.Identity.Name, InvoiceID, userInput);
                PrepareSuccessMessage("AddInvoiceAdjustment", MessageType.Method);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //StoreValidationIssues(vfc.Detail.ValidationIssues, userInput);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return InvoiceDetail(InvoiceID, null, null, null, null, returnUrl);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                PrepareErrorMessage("AddInvoiceAdjustment", MessageType.Method);
            }
            //return to invoice detail view            
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = InvoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to remove a line item from the specified invoice
        /// </summary>
        /// <param name="lineItemID">ID of the requested line item to be removed</param>
        /// <param name="invoiceID">ID of the requested invoice to be adjusted</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to /Admin/InvoiceDetail/[id]</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult RemoveLineItem(int lineItemID, int invoiceID, string returnUrl)
        {
            string alternateReturnUrl = Url.Action(Strings.MVC.IndexAction);
            if (string.IsNullOrEmpty(returnUrl))
            {
                //a return url was not provided, so pull the invoice to determine the appropriate 
                //  location to return if the invoice is deleted by this action
                try
                {
                    Invoice invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, invoiceID);
                    if (invoice.Type == InvoiceTypes.Fee)
                    {
                        //fee invoice
                        if (invoice.Status == InvoiceStatuses.Paid)
                        {
                            //paid
                            alternateReturnUrl = Url.Action(Strings.MVC.SiteFeesReportAction);
                        }
                        else
                        {
                            //unpaid
                            alternateReturnUrl = Url.Action(Strings.MVC.NewSiteFeesReportAction);
                        }
                    }
                    else
                    {
                        //listing invoice not owned by this user
                        alternateReturnUrl = Url.Action(Strings.MVC.SalesTransactionReportAction);
                    }
                }
                catch (Exception)
                {
                    //ignore this error;
                }
            }
            bool invoiceDeleted;
            try
            {
                //remove line item
                invoiceDeleted = AccountingClient.RemoveLineItemFromInvoice(User.Identity.Name, invoiceID, lineItemID);

                if (invoiceDeleted)
                {
                    //this was the last line item, so the invoice was deleted - redirect to the return Url or admin invoice list
                    PrepareSuccessMessage(Messages.LastLineitemRemovedInvoiceDeleted, MessageType.Message);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return Redirect(alternateReturnUrl);
                }
                else
                {
                    //admin invoice detail page
                    PrepareSuccessMessage("RemoveLineItem", MessageType.Method);
                    return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("RemoveLineItem", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request from admin to manually a site fee invoice as "Paid"
        /// </summary>
        /// <param name="id">ID of the invoice to be updated</param>
        /// <param name="paid"></param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to the invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetInvoicePaid(int id, bool paid, string returnUrl)
        {
            try
            {
                AccountingClient.SetInvoicePaid(User.Identity.Name, id, paid);
                PrepareSuccessMessage("SetInvoicePaid", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("SetInvoicePaid", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl });
        }

        /// <summary>
        /// Processes request from admin to add a non-standard fee to a site fee invoice
        /// </summary>
        /// <param name="userName">payer's username</param>
        /// <param name="feeAmount">fee amount</param>
        /// <param name="feeDesc">fee description</param>
        /// <param name="listingId">(optional) null, or id of associated listing</param>
        /// <param name="additionalInfo">(optional) additional information to be associated with new line item</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to the invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult AddMiscSiteFee(string userName, decimal feeAmount, string feeDesc, int? listingId, string additionalInfo, string returnUrl)
        {
            try
            {
                int invoiceId = AccountingClient.AddMiscSiteFee(User.Identity.Name, userName, feeAmount, feeDesc,
                    listingId, additionalInfo);
                PrepareSuccessMessage("AddMiscSiteFee", MessageType.Method);
                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { @id = invoiceId, returnUrl });
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("AddMiscSiteFee", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.NewSiteFeesReportAction);
        }


        /// <summary>
        /// Processes request to update an invoice comment
        /// </summary>
        /// <param name="InvoiceID">ID of the invoice to be updated</param>
        /// <param name="Comments">New comments value</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <param name="ApplyToAllInvoices">in Events Edition, if true applies this comment to all invoices in the same event</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateInvoiceComments(int InvoiceID, string Comments, string returnUrl, bool? ApplyToAllInvoices)
        {
            try
            {
                AccountingClient.UpdateInvoiceComments(User.Identity.Name, InvoiceID, Comments);
                if (ApplyToAllInvoices.HasValue && ApplyToAllInvoices.Value)
                {
                    var thisInvoice = AccountingClient.GetInvoiceByID(User.Identity.Name, InvoiceID);
                    //if (thisInvoice.AuctionEventId != null)
                    //{
                        var allInvoicesInThisEvent = AccountingClient.GetInvoicesBySeller(User.Identity.Name, thisInvoice.Owner.UserName, "All",
                            string.Empty, string.Empty, thisInvoice.AuctionEventId ?? 0, 0, 0, Strings.Fields.CreatedDTTM, false);
                        foreach (var invoice in allInvoicesInThisEvent.List.Where(i => i.ID != InvoiceID))
                        {
                            AccountingClient.UpdateInvoiceComments(User.Identity.Name, invoice.ID, Comments);
                        }
                    //}
                }
                PrepareSuccessMessage("UpdateInvoiceComments", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("UpdateInvoiceComments", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = InvoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to update an invoice comment
        /// </summary>
        /// <param name="InvoiceID">ID of the invoice to be updated</param>
        /// <param name="BuyersPremiumPercent">New comments value</param>
        /// <param name="returnUrl">Url to redirect to (default invoice detail view if missing)</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateInvoiceBuyersPremium(int InvoiceID, string BuyersPremiumPercent, string returnUrl)
        {
            try
            {
                //decimal x = decimal.Parse("GeneralErrorTest");
                string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
                CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
                decimal newBuyerPremiumPercent;
                if (decimal.TryParse(BuyersPremiumPercent, NumberStyles.Float, cultureInfo, out newBuyerPremiumPercent)
                    && newBuyerPremiumPercent >= 0.00M
                    && newBuyerPremiumPercent <= 100.00M)
                {
                    AccountingClient.UpdateInvoiceBuyersPremium(User.Identity.Name, InvoiceID, newBuyerPremiumPercent);
                    PrepareSuccessMessage("UpdateInvoiceBuyersPremium", MessageType.Method);
                }
                else
                {
                    PrepareErrorMessage("InvalidBuyersPremiumPercent", MessageType.Message);
                }
            }
            catch
            {
                PrepareErrorMessage("UpdateInvoiceBuyersPremium", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = InvoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to change or specify the preferred shipping option on a listing invoice
        /// </summary>
        /// <param name="id">ID of the invoice to be updated</param>
        /// <param name="ShippingOption">Selected shipping option value</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult UpdateInvoiceShipping(int id, int ShippingOption, string returnUrl)
        {
            try
            {
                AccountingClient.UpdateInvoiceShipping(User.Identity.Name, id, ShippingOption);
                PrepareSuccessMessage("UpdateInvoiceShipping", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("UpdateInvoiceShipping", MessageType.Method);
            }

            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl });
        }

        /// <summary>
        /// Processes request to set the "shipped" status of an invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to update</param>
        /// <param name="shipped">true for shipped, false for NOT shipped</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetInvoiceShipped(int invoiceID, bool shipped, string returnUrl)
        {
            try
            {
                AccountingClient.SetInvoiceShipped(User.Identity.Name, invoiceID, shipped);
                PrepareSuccessMessage("SetInvoiceShipped", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("SetInvoiceShipped", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to toggle whether Buyer's Premium is taxable for the specified invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to update</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult ToggleTaxableBp(int invoiceID, string returnUrl)
        {
            var invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, invoiceID);
            Dictionary<string, string> invoiceProperties;
            if (invoice.PropertyBag != null)
            {
                invoiceProperties = invoice.PropertyBag.Properties;
            }
            else
            {
                invoiceProperties = new Dictionary<string, string>(1);
            }
            if (invoiceProperties.ContainsKey(InvoiceProperties.BuyersPremiumIsTaxable))
            {
                var currentBpIsTaxableValue = invoiceProperties[InvoiceProperties.BuyersPremiumIsTaxable];
                var newBpIsTaxableValue = !bool.Parse(currentBpIsTaxableValue);
                invoiceProperties[InvoiceProperties.BuyersPremiumIsTaxable] = newBpIsTaxableValue.ToString().ToLower();
            }
            else
            {
                invoiceProperties.Add(InvoiceProperties.BuyersPremiumIsTaxable, (!invoice.BuyersPremiumIsTaxable()).ToString());
            }
            AccountingClient.UpdateInvoiceProperties(User.Identity.Name, invoiceID, invoiceProperties);
            AccountingClient.UpdateInvoiceBuyersPremium(User.Identity.Name, invoiceID, invoice.BuyersPremiumPercent);
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to toggle whether a line item is taxable
        /// </summary>
        /// <param name="lineItemID">ID of the line item to be updated</param>
        /// <param name="invoiceID">ID of the invoice to return to</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>Redirect to invoice detail</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ToggleTaxableLineItem(int lineItemID, int invoiceID, string returnUrl)
        {
            AccountingClient.ToggleLineItemTaxable(User.Identity.Name, lineItemID);
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Displays invoice details, formatted for printing
        /// </summary>
        /// <param name="id">ID of the requested invoice</param>
        /// <returns>View(Invoice)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult PrintInvoice(int id)
        {
            Invoice invoice = null;
            try
            {
                invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, id);
            }
            catch (FaultException<AuthorizationFaultContract>)
            {
                //the logged in user is not authiorized to view this invoice
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return View();
            }
            if (invoice == null)
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return View();
            }
            //ViewData[Strings.Fields.InvoiceID] = id;

            string shippingOption = string.Empty;
            if (invoice.Type != Strings.InvoiceTypes.Fee)
            {
                if (invoice.Type == Strings.InvoiceTypes.Shipping)
                {
                    if (invoice.ShippingOption != null)
                    {
                        shippingOption =
                            string.Format(Strings.Formats.ShippingMethodName, invoice.ShippingOption.Method.Name
                                , SiteClient.FormatCurrency(invoice.ShippingAmount,
                                    invoice.Currency, this.GetCookie(Strings.MVC.CultureCookie))
                        );
                    }
                    else
                    {
                        shippingOption = string.Empty;
                    }
                }
            }
            var shippingOptionContainer = new Dictionary<int, string>();
            shippingOptionContainer.Add(invoice.ID, shippingOption);
            ViewData[Strings.Fields.ShippingOption] = shippingOptionContainer;

            var pageOfLineItems = AccountingClient.GetLineItemsByInvoice(User.Identity.Name, invoice.ID, 0,
                                                                         0, // page size=0 returns all line items
                                                                         Strings.Fields.DateStamp, false);
            var lineItemsContainer = new Dictionary<int, Page<LineItem>>();
            lineItemsContainer.Add(invoice.ID, pageOfLineItems);
            ViewData[Strings.MVC.ViewData_PageOfLineitems] = lineItemsContainer;

            return View(invoice);
        }

        /// <summary>
        /// Displays form to select a billing address for the specified invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <returns>View(List&lt;Address&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetBillingAddress(int invoiceID)
        {
            Invoice invoice = null;
            try
            {
                invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, invoiceID);
            }
            catch
            {
                //the logged in user is not authiorized to view this invoice?
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
            }
            if (invoice == null)
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return RedirectToAction(Strings.MVC.IndexAction);
            }
            List<Address> addresses = UserClient.GetAddresses(User.Identity.Name, invoice.Payer.UserName);
            if (addresses.Count == 0)
            {
                return RedirectToAction(Strings.MVC.AddAddressAction, new { @id = invoice.Payer.ID, SetBillingAddress = invoiceID });
            }
            ViewData["selectedAddressID"] = GetBillingAddrId(invoice, addresses);
            ViewData[Strings.Fields.InvoiceID] = invoiceID;
            ViewData["TargetUserID"] = invoice.Payer.ID;
            return View(addresses);
        }

        /// <summary>
        /// Processes request to set the specified address as the billing address
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="selectedAddressID">ID of the address to be used</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SetBillingAddress(int invoiceID, int selectedAddressID)
        {
            try
            {
                AccountingClient.SetInvoiceBillingAddress(User.Identity.Name, invoiceID, selectedAddressID);
                PrepareSuccessMessage("SetBillingAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetBillingAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID });
        }

        /// <summary>
        /// Displays form to select a billing address for the specified invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <returns>View(List&lt;Address&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult SetShippingAddress(int invoiceID)
        {
            Invoice invoice = null;
            try
            {
                invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, invoiceID);
            }
            catch
            {
                //the logged in user is not authiorized to view this invoice?
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
            }
            if (invoice == null)
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return RedirectToAction(Strings.MVC.IndexAction);
            }
            List<Address> addresses = UserClient.GetAddresses(User.Identity.Name, invoice.Payer.UserName);
            if (addresses.Count == 0)
            {
                return RedirectToAction(Strings.MVC.AddAddressAction, new { @id = invoice.Payer.ID, SetShippingAddress = invoiceID });
            }
            ViewData["selectedAddressID"] = GetShippingAddrId(invoice, addresses);
            ViewData[Strings.Fields.InvoiceID] = invoiceID;
            ViewData["TargetUserID"] = invoice.Payer.ID;
            return View(addresses);
        }

        /// <summary>
        /// Processes request to set the specified address as the billing address
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="selectedAddressID">ID of the address to be used</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SetShippingAddress(int invoiceID, int selectedAddressID)
        {
            try
            {
                AccountingClient.SetInvoiceShippingAddress(User.Identity.Name, invoiceID, selectedAddressID);
                PrepareSuccessMessage("SetShippingAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetShippingAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID });
        }

        #endregion

        #region Attributes

        /// <summary>
        /// Displays a page of a list of attributes
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of the requested sort option defined in QuerySortDefinitions.AttributeOptions</param>
        /// <returns>View(Page&lt;Attribute&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Attributes(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.AttributeOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.AdminResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.AttributeOptions[SortFilterOptions.Value];

            Page<DTO.Attribute> attributes = CommonClient.GetEditableAttributes(User.Identity.Name,
                                                                                page == null ? 0 : (int)page,
                                                                                SiteClient.PageSize, currentQuery.Sort,
                                                                                currentQuery.Descending);

            try
            {
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null && !SiteClient.DemoEnabled)
                {
                    //Save                           
                    foreach (DTO.Attribute attribute in attributes.List)
                    {
                        attribute.Value = Request.Form[attribute.ID + "_value"];
                    }

                    CommonClient.SetAttributes(User.Identity.Name, attributes.List);

                    //add any missing attributes in case any "Saver", "Loader" or "Generator" settings were changed to a differnt type
                    CommonClient.RegisterMediaAssetProviders();

                    //re-pull attributes data in case any new ones were added
                    attributes = CommonClient.GetEditableAttributes(User.Identity.Name,
                                                                    page == null ? 0 : (int)page,
                                                                    SiteClient.PageSize, currentQuery.Sort,
                                                                    currentQuery.Descending);

                    PrepareSuccessMessage("Attributes", MessageType.Method);
                }
            }
            catch
            {
                PrepareErrorMessage("Attributes", MessageType.Method);
            }

            return View(attributes);
        }

        #endregion

        #region Banners

        /// <summary>
        /// Processes request to view/update banners
        /// </summary>
        /// <param name="bannerLocationFilter">banner type requested (e.g. "TOP", "LEFT", "BOTTOM")</param>
        /// <param name="page">0-based index of the requested page</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="isDescending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Banner&gt;) or redirect to /Admin/Summary on errors</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult Banners(string bannerLocationFilter, int? page, string sort, bool? isDescending)
        {
            List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(3);
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.TopBannersToDisplay).First());
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.LeftBannersToDisplay).First());
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.BottomBannersToDisplay).First());
            foreach (CustomProperty property in propertiesToUpdate)
            {
                string key = property.Field.Name;
                if (!this.ModelState.ContainsKey(key))
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(property.Value, property.Value, null);
                    ModelState.Add(key, ms);
                }
            }
            ViewData[Strings.MVC.ViewData_BannerSiteProperties] = propertiesToUpdate;
            try
            {
                const int pageSize = 10;
                if (!string.IsNullOrEmpty(bannerLocationFilter))
                    if (bannerLocationFilter == this.AdminResourceString("AllBanners"))
                        bannerLocationFilter = string.Empty;

                ViewData[Strings.Fields.BannerLocationFilter] = bannerLocationFilter;

                Page<Banner> results = SiteClient.GetAllBanners(
                    (!string.IsNullOrEmpty(bannerLocationFilter) ? bannerLocationFilter : string.Empty),
                    (page.HasValue ? page.Value : 0),
                    pageSize,
                    (!string.IsNullOrEmpty(sort) ? sort : string.Empty),
                    (isDescending.HasValue ? isDescending.Value : false));

                //display validation errors, if applicable
                //CheckValidationIssues();

                return View(Strings.MVC.BannersAction, results);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch /*(Exception e)*/
            {
                PrepareErrorMessage("Banners", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.SummaryAction);
        }

        /// <summary>
        /// Processes request to create a new banner
        /// </summary>
        /// <returns>redirect to /Admin/Banners</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult CreateBanner()
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            // populate UserInput
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            {
                if (key == Strings.Fields.BannerHtml)
                {
                    Media htmlMedia = CreateHtmlMedia(input.ActingUserName, Request.Form[key]);
                    input.Items.Add("media_guid_" + htmlMedia.GUID.ToString(), htmlMedia.GUID.ToString());
                    input.Items.Add("media_ordr_" + htmlMedia.GUID.ToString(), "1");
                }
                else
                {
                    input.Items.Add(key,
                                    Request.Form[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.Form[key].Trim());
                }
            }
            try
            {
                Banner banner = SiteClient.AddBanner(User.Identity.Name, input);
                PrepareSuccessMessage("CreateBanner", MessageType.Method);
                //do something with the return value?
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //store validation issues to be displayed by redirect target
                //StoreValidationIssues(vfc.Detail.ValidationIssues, null);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return Banners(null, null, null, null);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("CreateBanner", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.BannersAction);
        }

        /// <summary>
        /// Processes request to update a banner
        /// </summary>
        /// <returns>redirect to /Admin/Banners</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult UpdateBanner()
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            // populate UserInput
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            {
                if (key == Strings.Fields.BannerHtml)
                {
                    Media htmlMedia = CreateHtmlMedia(input.ActingUserName, Request.Form[key]);
                    input.Items.Add("media_guid_" + htmlMedia.GUID.ToString(), htmlMedia.GUID.ToString());
                    input.Items.Add("media_ordr_" + htmlMedia.GUID.ToString(), "1");
                }
                else
                {
                    input.Items.Add(key,
                                    Request.Form[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.Form[key].Trim());
                }
            }
            try
            {
                Banner banner = SiteClient.UpdateBanner(User.Identity.Name, input);
                PrepareSuccessMessage("UpdateBanner", MessageType.Method);
                //do something with the return value?
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //store validation issues to be displayed by redirect target
                //StoreValidationIssues(vfc.Detail.ValidationIssues, null);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return Banners(null, null, null, null);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("UpdateBanner", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.BannersAction);
        }

        /// <summary>
        /// Generates a Media object based on the suppied user input
        /// </summary>
        /// <param name="actingUsername">username of the requesting user</param>
        /// <param name="htmlText">html text to be associated with the result</param>
        /// <returns>an HTML Media object</returns>
        private Media CreateHtmlMedia(string actingUsername, string htmlText)
        {
            const string context = MediaUploadContexts.UploadBannerHtml;

            //Get workflow for submitting HTML to be saved as a Media object
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            byte[] byteArray = Encoding.UTF8.GetBytes(htmlText);
            MemoryStream stream = new MemoryStream(byteArray);
            Media newHtml = mediaGenerator.Generate(generatorProviderSettings, stream);
            newHtml.Context = context;

            //Save the media    
            newHtml.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(newHtml.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            mediaSaver.Save(saverProviderSettings, newHtml);

            //Load the media (for display on website)
            newHtml.Loader = workflowParams["Loader"];
            //IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(newHtml.Loader);
            //Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            //string loadResult = mediaLoader.Load(loaderProviderSettings, newHtml, newHtml.DefaultVariationName);

            //Save the media object to the db
            CommonClient.AddMedia(actingUsername, newHtml);

            ////Save the html Media GUID to attributes
            //Dictionary<string, string> attributes = new Dictionary<string, string>(1);
            //attributes.Add("MediaGUID", newHtml.GUID.ToString());
            //CommonClient.SetAttributeData(actingUsername, null, "Site.Content", context, attributes);

            return newHtml;
        }

        /// <summary>
        /// Processes request to update banner-specific site properties
        /// </summary>
        /// <returns>Redirect to /Admin/Bannerse</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UpdateBannerProperties()
        {
            List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(3);
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.TopBannersToDisplay).First());
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.LeftBannersToDisplay).First());
            propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.BottomBannersToDisplay).First());

            //capture user input
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
            input.AddAllFormValues(this);

            //attempt to update site properties
            try
            {
                SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
                SiteClient.Reset();
                PrepareSuccessMessage("UpdateBannerProperties", MessageType.Method);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //store validation issues to be displayed by redirect target
                //StoreValidationIssues(this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues), input);
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return Banners(null, null, null, null);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("UpdateBannerProperties", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.BannersAction);
        }

        /// <summary>
        /// Processes request to delete the specified banner
        /// </summary>
        /// <param name="bannerId">ID of the specified banner</param>
        /// <returns>Redirect to /Admin/Banners</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DeleteBanner(int bannerId)
        {
            try
            {
                SiteClient.DeleteBanner(User.Identity.Name, bannerId);
                PrepareSuccessMessage("DeleteBanner", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DeleteBanner", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.BannersAction);
        }

        #endregion Banners

        #region Credit Cards

        /// <summary>
        /// Prepares view data for Credit Card administration
        /// </summary>
        /// <returns>View(List&lt;ListItem&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult CreditCards()
        {
            //List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(1);
            //propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.CreditCardsEnabled).First());
            //foreach (CustomProperty property in propertiesToUpdate)
            //{
            //    //Add Model control
            //    ModelState ms = new ModelState();
            //    ms.Value = new ValueProviderResult(property.Value, property.Value, null);
            //    ModelState.Add(property.Field.Name, ms);
            //}
            //ViewData[Strings.MVC.ViewData_CreditCardProperties] = propertiesToUpdate;

            return View(SiteClient.CreditCardTypes);
        }

        /// <summary>
        /// Processes request to update Credit Card enable or disable credit card types
        /// </summary>
        /// <param name="formCollection">user input</param>
        /// <returns>View(List&lt;ListItem&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CreditCards(FormCollection formCollection)
        {
            //List<CustomProperty> propertiesToUpdate = new List<CustomProperty>(1);
            //propertiesToUpdate.Add(SiteClient.Properties.Where(p => p.Field.Name == SiteProperties.CreditCardsEnabled).First());

            //ViewData[Strings.MVC.ViewData_CreditCardProperties] = propertiesToUpdate;

            //capture user input
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
            input.AddAllFormValues(this);

            //attempt to update settings
            try
            {
                ////properties
                //SiteClient.UpdateSettings(User.Identity.Name, propertiesToUpdate, input);
                //SiteClient.Reset();

                //credit card types
                SiteClient.SetCreditCardTypes(HttpContext.User.Identity.Name, input);

                PrepareSuccessMessage("CreditCards", MessageType.Method);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("CreditCards", MessageType.Method);
            }
            return View(SiteClient.CreditCardTypes);
        }

        #endregion Credit Cards

        #region Listing Type Properties

        /// <summary>
        /// Processes request to update listing type properties
        /// </summary>
        /// <param name="ListingTypeName">the name of the listing type to be updated</param>
        /// <returns>Redirect to /Admin/PropertyManagement/41401 (general listing-related site properties )</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult ListingTypeProperties(string ListingTypeName)
        {
            ListingType listingType = ListingClient.ListingTypes
                .Where(lt => lt.Name == ListingTypeName).SingleOrDefault();
            if (listingType != null)
            {
                List<CustomProperty> properties = ListingClient.GetListingTypeProperties(listingType.Name, "Site");
                if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
                {   //This is a save postback

                    //capture new values
                    UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
                    input.AddAllFormValues(this);
                    input.Items.Add(Strings.Fields.ListingType, ListingTypeName);

                    //attempt to save updated properties
                    try
                    {
                        ListingClient.UpdateListingTypeProperties(User.Identity.Name, input);
                        PrepareSuccessMessage("ListingTypeProperties", MessageType.Method);
                    }
                    catch (FaultException<ValidationFaultContract> vfc)
                    {
                        //store validation issues in temp data
                        //StoreValidationIssues(this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues), null);
                        foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                    }
                }
                else
                {
                    //initial page load, set up model state values
                    ModelState.FillProperties(properties);
                }

                //Add validation errors stored in temp data to modelstate errors list, if applicable
                //CheckValidationIssues();

                ViewData["ListingTypeProperties"] = properties;
                return View(Strings.MVC.ListingTypePropertiesAction, listingType);
            }
            //an invalid listing type was specified, redirect to general listing options page
            PrepareErrorMessage("ListingTypeProperties", MessageType.Method);

            return RedirectToAction(Strings.MVC.PropertyManagementAction, new { @id = 41401 });
        }

        #endregion Listing Type Properties

        #region Event Settings

        /// <summary>
        /// displays/process updates to event-specific, site-scoped Auction listing type properties only
        /// </summary>
        /// <returns>View(List&lt;CustomPreoprty&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult AuctionLotSettings()
        {
            List<CustomProperty> allAuctionProperties = null;
            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {   //This is a save postback

                //capture new values
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                this.GetCookie(Strings.MVC.CultureCookie),
                                                this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                input.Items.Add(Strings.Fields.ListingType, Strings.ListingTypes.Auction);

                //attempt to save updated properties
                try
                {
                    ListingClient.UpdateListingTypeProperties(User.Identity.Name, input);
                    PrepareSuccessMessage("AuctionLotSettings", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //store validation issues in temp data
                    //StoreValidationIssues(this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues), null);
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                }
                allAuctionProperties = ListingClient.GetListingTypeProperties(Strings.ListingTypes.Auction, "Site");
            }
            else
            {
                //initial page load, set up model state values
                allAuctionProperties = ListingClient.GetListingTypeProperties(Strings.ListingTypes.Auction, "Site");
                ModelState.FillProperties(allAuctionProperties);
            }
            var hiddenAuctionPropties = new List<CustomProperty>();
            var eventAuctionProperties = new List<CustomProperty>();
            foreach(var property in allAuctionProperties)
            {
                switch(property.Field.Name)
                {
                    case SiteProperties.EnableShipping:
                    case SiteProperties.ProxyBiddingBelowReserve:
                    case SiteProperties.DisableBuyNowAfterWinningBids:
                    case SiteProperties.EnableReserve:
                    case SiteProperties.EnableBuyNow:
                    case SiteProperties.EnableMakeOffer:
                    case SiteProperties.QuickBidEnabled:
                    case SiteProperties.QuickBidForListViewsEnabled:
                    case SiteProperties.LargeBidConfirmationEnabled:
                        eventAuctionProperties.Add(property);
                        break;
                    default:
                        hiddenAuctionPropties.Add(property);
                        break;
                }
            }
            ViewData["HiddenAuctionProperties"] = hiddenAuctionPropties;
            return View(Strings.MVC.AuctionLotSettingsAction, eventAuctionProperties);
        }

        #endregion

        #region Reports

        /// <summary>
        /// Displays "Sales Transactions" admin report
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="payee">partial payee username string to match</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="lotNumber">line item lot number string to match</param>
        /// <param name="description">partial line item description string to match</param>
        /// <param name="quantity">specific sale quantity to include (0 or blank to skip)</param>
        /// <param name="priceLow">minimum sale price to include</param>
        /// <param name="priceHigh">maximum sale price to include</param>
        /// <param name="totalPriceLow">minimum invoice total to include</param>
        /// <param name="totalPriceHigh">maximum invoice total to include</param>
        /// <param name="isPaid">0=All, 1=Paid Only, 2=Unpaid Only</param>
        /// <param name="payer">partial payer username string to match</param>
        /// <param name="firstName">partial payer first name string to match</param>
        /// <param name="lastName">partial payer last name string to match</param>
        /// <param name="email">partial payer email address string to match</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>View(Page&lt;SalesTransactionReportResult&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult SalesTransactionReport(string dateStart,
                                    string dateEnd,
                                    string payee,
                                    string invoiceID,
                                    string listingID,
                                    string eventID,
                                    string lotNumber,
                                    string description,
                                    string quantity,
                                    string priceLow,
                                    string priceHigh,
                                    string totalPriceLow,
                                    string totalPriceHigh,
                                    string isPaid,
                                    string payer,
                                    string firstName,
                                    string lastName,
                                    string email,
                                    string sort, string page, string descending)
        {
            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            ViewData["dateStart"] = dateStart;
            ViewData["dateEnd"] = dateEnd;
            ViewData["payee"] = payee;
            ViewData["invoiceID"] = invoiceID;
            ViewData["listingID"] = listingID;
            ViewData["eventID"] = eventID;
            ViewData["lotNumber"] = lotNumber;
            ViewData["description"] = description;
            ViewData["quantity"] = quantity;
            ViewData["priceLow"] = priceLow;
            ViewData["priceHigh"] = priceHigh;
            ViewData["totalPriceLow"] = totalPriceLow;
            ViewData["totalPriceHigh"] = totalPriceHigh;
            ViewData["isPaid"] = isPaid;
            ViewData["payer"] = payer;
            ViewData["firstName"] = firstName;
            ViewData["lastName"] = lastName;
            ViewData["email"] = email;
            ViewData["sort"] = sort ?? "DateTime";
            ViewData["page"] = page;
            ViewData["descending"] = string.IsNullOrEmpty(descending) ? true : bool.Parse(descending);

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            int isPaidInt = 0;
            int.TryParse(isPaid, out isPaidInt);

            SelectList paidStatus =
                new SelectList(new[] { 
                      new { value = 0, text = this.AdminResourceString("All") }
                    , new { value = 1, text = this.AdminResourceString("Paid") }
                    , new { value = 2, text = this.AdminResourceString("Unpaid") }                    
                    , new { value = 3, text = this.AdminResourceString("Voided") }                    
                }, "value", "text", isPaidInt);
            ViewData["PaidStatusSelectList"] = paidStatus;

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else if (key == "dateStart")
                    {
                        input.Items.Add(key, dateStart);
                    }
                    else if (key == "payee")
                    {
                        if (!string.IsNullOrWhiteSpace(payee))
                        {
                            input.Items.Add(key, "%" + payee + "%");
                        }
                        else
                        {
                            input.Items.Add(key, Request.QueryString[key].Trim());
                        }
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "25");

            Page<SalesTransactionReportResult> retVal = null;
            try
            {
                int currencyCount = 0;
                decimal totalAmount = 0;
                retVal = AccountingClient.SalesTransactionReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);
                ViewData["CurrencyCount"] = currencyCount;
                ViewData["TotalAmount"] = totalAmount;
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }

            return View(retVal);
        }

        /// <summary>
        /// Displays "Listing Fees Revenue" admin report
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="lotNumber">line item lot number string to match</param>
        /// <param name="userName">partial payer username string to match</param>
        /// <param name="firstName">partial payer first name string to match</param>
        /// <param name="lastName">partial payer last name string to match</param>
        /// <param name="description">partial line item description string to match</param>
        /// <param name="email">partial payer email address string to match</param>
        /// <param name="amountLow">minimum fee amount to include</param>
        /// <param name="amountHigh">maximum fee amount to include</param>
        /// <param name="isPaid">0=All, 1=Paid Only, 2=Unpaid Only</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>View(Page&lt;ListingFeesRevenueReportResult&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult ListingFeesRevenueReport(string dateStart,
                                    string dateEnd,
                                    string listingID,
                                    string invoiceID,
                                    string eventID,
                                    string lotNumber,
                                    string userName,
                                    string firstName,
                                    string lastName,
                                    string description,
                                    string email,
                                    string amountLow,
                                    string amountHigh,
                                    string isPaid,
                                    string sort, string page, string descending)
        {
            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            ViewData["dateStart"] = dateStart;
            ViewData["dateEnd"] = dateEnd;
            ViewData["listingID"] = listingID;
            ViewData["invoiceID"] = invoiceID;
            ViewData["eventID"] = eventID;
            ViewData["lotNumber"] = lotNumber;
            ViewData["userName"] = userName;
            ViewData["firstName"] = firstName;
            ViewData["lastName"] = lastName;
            ViewData["description"] = description;
            ViewData["email"] = email;
            ViewData["amountLow"] = amountLow;
            ViewData["amountHigh"] = amountHigh;
            ViewData["isPaid"] = isPaid;
            ViewData["sort"] = sort ?? "DateTime";
            ViewData["page"] = page;
            ViewData["descending"] = string.IsNullOrEmpty(descending) ? true : bool.Parse(descending);

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            int isPaidInt = 0;
            int.TryParse(isPaid, out isPaidInt);

            SelectList paidStatus =
                new SelectList(new[] { 
                      new { value = 0, text = this.AdminResourceString("All") }
                    , new { value = 1, text = this.AdminResourceString("Paid") }
                    , new { value = 2, text = this.AdminResourceString("Unpaid") }                    
                }, "value", "text", isPaidInt);
            ViewData["PaidStatusSelectList"] = paidStatus;

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else if (key == "dateStart")
                    {
                        input.Items.Add(key, dateStart);
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "25");

            Page<ListingFeesRevenueReportResult> retVal = null;
            try
            {
                int currencyCount = 0;
                decimal totalAmount = 0;
                retVal = AccountingClient.ListingFeesRevenueReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);
                ViewData["CurrencyCount"] = currencyCount;
                ViewData["TotalAmount"] = totalAmount;
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }

            return View(retVal);
        }

        /// <summary>
        /// Sends "Listing Fees Revenue" admin report data to the user's browser in CSV format
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="lotNumber">line item lot number string to match</param>
        /// <param name="userName">partial payer username string to match</param>
        /// <param name="firstName">partial payer first name string to match</param>
        /// <param name="lastName">partial payer last name string to match</param>
        /// <param name="description">partial line item description string to match</param>
        /// <param name="email">partial payer email address string to match</param>
        /// <param name="amountLow">minimum fee amount to include</param>
        /// <param name="amountHigh">maximum fee amount to include</param>
        /// <param name="isPaid">0=All, 1=Paid Only, 2=Unpaid Only</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>resulting CSV data</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public FileContentResult ListingFeesRevenueCSV(string dateStart,
                                    string dateEnd,
                                    string listingID,
                                    string invoiceID,
                                    string eventID,
                                    string lotNumber,
                                    string userName,
                                    string firstName,
                                    string lastName,
                                    string description,
                                    string email,
                                    string amountLow,
                                    string amountHigh,
                                    string isPaid,
                                    string sort, string descending)
        {
            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            StringBuilder csv = new StringBuilder();

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "0");

            int currencyCount = 0;
            decimal totalAmount = 0;
            Page<ListingFeesRevenueReportResult> retVal = AccountingClient.ListingFeesRevenueReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);

            //Get culture information            
            CultureInfo tempCulture = CultureInfo.GetCultureInfo(this.GetCookie("culture") ?? SiteClient.SiteCulture);
            CultureInfo currentCulture = (CultureInfo)tempCulture.Clone();
            currentCulture.NumberFormat.CurrencySymbol = string.Empty;
            currentCulture.NumberFormat.CurrencyGroupSeparator = string.Empty;
            currentCulture.NumberFormat.CurrencyPositivePattern = 0;

            //add header
            csv.Append(this.AdminResourceString("DateTime"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("UserName"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("FirstName"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("LastName"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Email"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("ListingID"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Description"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Amount"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Currency"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("InvoiceID"));
            csv.Append(",");
            string paid = this.AdminResourceString("Paid");
            csv.Append(paid);
            if (SiteClient.EnableEvents)
            {
                csv.Append(",");
                csv.Append(this.AdminResourceString("EventID"));
                csv.Append(",");
                csv.Append(this.AdminResourceString("LotNumber"));
            }
            csv.AppendLine();

            foreach (ListingFeesRevenueReportResult result in retVal.List)
            {
                csv.Append(QuoteCSVData(result.DateTime.ToLocalDTTM().ToString("G", currentCulture)));
                csv.Append(",");
                csv.Append(result.UserName);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.FirstName));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.LastName));
                csv.Append(",");
                csv.Append(result.Email);
                csv.Append(",");
                csv.Append(result.ListingID);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Description));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Amount.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(result.Currency);
                csv.Append(",");
                csv.Append(result.InvoiceID);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Paid ? paid : string.Empty));
                if (SiteClient.EnableEvents)
                {
                    csv.Append(",");
                    csv.Append(result.AuctionEventId.HasValue ? result.AuctionEventId.ToString() : string.Empty);
                    csv.Append(",");
                    csv.Append(!string.IsNullOrEmpty(result.LotNumber) ? result.LotNumber : string.Empty);
                }
                csv.AppendLine();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.AdminResourceString("ListingFeesRevenue_csv");
            return content;
        }

        /// <summary>
        /// Sends "Sales Transactions" admin report data to the user's browser in CSV format
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="payee">partial payee username string to match</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="lotNumber">line item lot number string to match</param>
        /// <param name="description">partial line item description string to match</param>
        /// <param name="quantity">specific sale quantity to include (0 or blank to skip)</param>
        /// <param name="priceLow">minimum sale price to include</param>
        /// <param name="priceHigh">maximum sale price to include</param>
        /// <param name="totalPriceLow">minimum invoice total to include</param>
        /// <param name="totalPriceHigh">maximum invoice total to include</param>
        /// <param name="isPaid">0=All, 1=Paid Only, 2=Unpaid Only</param>
        /// <param name="payer">partial payer username string to match</param>
        /// <param name="firstName">partial payer first name string to match</param>
        /// <param name="lastName">partial payer last name string to match</param>
        /// <param name="email">partial payer email address string to match</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>resulting CSV data</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public FileContentResult SalesTransactionCSV(string dateStart,
                                    string dateEnd,
                                    string payee,
                                    string invoiceID,
                                    string listingID,
                                    string eventID,
                                    string lotNumber,
                                    string description,
                                    string quantity,
                                    string priceLow,
                                    string priceHigh,
                                    string totalPriceLow,
                                    string totalPriceHigh,
                                    string isPaid,
                                    string payer,
                                    string firstName,
                                    string lastName,
                                    string email,
                                    string sort, string descending)
        {

            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            StringBuilder csv = new StringBuilder();

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                   this.GetCookie(Strings.MVC.CultureCookie),
                                                   this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else if (key == "dateStart")
                    {
                        input.Items.Add(key, dateStart);
                    }
                    else if (key == "payee")
                    {
                        if (!string.IsNullOrWhiteSpace(payee))
                        {
                            input.Items.Add(key, "%" + payee + "%");
                        }
                        else
                        {
                            input.Items.Add(key, Request.QueryString[key].Trim());
                        }
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "0");

            int currencyCount = 0;
            decimal totalAmount = 0;
            Page<SalesTransactionReportResult> retVal = AccountingClient.SalesTransactionReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);

            //Get culture information            
            CultureInfo tempCulture = CultureInfo.GetCultureInfo(this.GetCookie("culture") ?? SiteClient.SiteCulture);
            CultureInfo currentCulture = (CultureInfo)tempCulture.Clone();
            currentCulture.NumberFormat.CurrencySymbol = string.Empty;
            currentCulture.NumberFormat.CurrencyGroupSeparator = string.Empty;
            currentCulture.NumberFormat.CurrencyPositivePattern = 0;

            var includedListingFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.Item, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReport).ToList();

            List<CustomField> includedEventFields;
            if (SiteClient.EnableEvents)
            {
                includedEventFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.Event, 0, 0, "DisplayOrder", false).List
                    .Where(f => f.IncludeInSalesReport).ToList();
            }
            else
            {
                includedEventFields = new List<CustomField>(0);
            }

            var includedSellerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsSeller).ToList();

            var includedBuyerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsBuyer).ToList();

            //add header
            csv.Append(this.AdminResourceString("DateTime"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Seller")); // renamed from "Payee"

            foreach (var field in includedSellerFields)
            {
                csv.Append(",");
                if (field.IncludeInSalesReportAsBuyer)
                {
                    csv.Append(QuoteCSVData(string.Format("Seller {0}", this.CustomFieldResourceString(field.Name))));
                }
                else
                {
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                }
            }

            if (SiteClient.EnableEvents)
            {
                csv.Append(",");
                csv.Append(this.AdminResourceString("EventID"));

                foreach (var field in includedEventFields)
                {
                    csv.Append(",");
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                }

            }

            csv.Append(",");
            csv.Append(this.AdminResourceString("ListingID"));

            if (SiteClient.EnableEvents)
            {
                csv.Append(",");
                csv.Append(this.AdminResourceString("LotNumber"));
            }

            foreach (var field in includedListingFields)
            {
                csv.Append(",");
                csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
            }

            csv.Append(",");
            csv.Append(this.AdminResourceString("Description"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Price"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Quantity"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Total"));
            csv.Append(",");
            string paid = this.AdminResourceString("Paid");
            csv.Append(paid);

            csv.Append(",");
            csv.Append(this.AdminResourceString("InvoiceID"));

            csv.Append(",");
            csv.Append(this.AdminResourceString("BuyerID")); // renamed from "PayerID"
            csv.Append(",");
            csv.Append(this.AdminResourceString("Buyer")); // renamed from "Payer"

            csv.Append(",");
            csv.Append(this.AdminResourceString("FirstName"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("LastName"));

            foreach (var field in includedBuyerFields)
            {
                csv.Append(",");
                if (field.IncludeInSalesReportAsSeller)
                {
                    csv.Append(QuoteCSVData(string.Format("Buyer {0}", this.CustomFieldResourceString(field.Name))));
                }
                else
                {
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                }
            }

            csv.Append(",");
            csv.Append(this.AdminResourceString("Email"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Address"));

            csv.AppendLine();

            foreach (SalesTransactionReportResult result in retVal.List)
            {
                int allIncludedFieldsCount = includedListingFields.Count +
                    includedEventFields.Count +
                    includedSellerFields.Count +
                    includedBuyerFields.Count;
                Dictionary<string, string> packedValues = new Dictionary<string, string>(allIncludedFieldsCount);
                if (allIncludedFieldsCount > 0)
                {
                    var packedValueParts = result.PackedValues.Split('|');
                    foreach (var kvp in packedValueParts)
                    {
                        string key = null;
                        string value = null;
                        if (kvp.Contains(":="))
                        {
                            key = kvp.Left(kvp.IndexOf(":="));
                            value = kvp.Right(kvp.Length - key.Length - 2);
                            packedValues.Add(key, value);
                        }
                    }
                }

                csv.Append(QuoteCSVData(result.DateTime.ToString("G", currentCulture)));
                csv.Append(",");
                csv.Append(result.Payee);

                foreach (var field in includedSellerFields)
                {
                    csv.Append(",");
                    if (packedValues.ContainsKey(string.Format("Seller_{0}", field.Name)))
                    {
                        csv.Append(QuoteCSVData(packedValues[string.Format("Seller_{0}", field.Name)]));
                    }
                }

                if (SiteClient.EnableEvents)
                {
                    csv.Append(",");
                    csv.Append(result.AuctionEventId.HasValue ? result.AuctionEventId.ToString() : string.Empty);

                    foreach (var field in includedEventFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                    }
                }

                csv.Append(",");
                csv.Append(result.ListingID);

                if (SiteClient.EnableEvents)
                {
                    csv.Append(",");
                    csv.Append(!string.IsNullOrEmpty(result.LotNumber) ? result.LotNumber : string.Empty);
                }

                foreach (var field in includedListingFields)
                {
                    csv.Append(",");
                    if (packedValues.ContainsKey(field.Name))
                    {
                        csv.Append(QuoteCSVData(packedValues[field.Name]));
                    }
                }

                csv.Append(",");
                csv.Append(QuoteCSVData(result.Description));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Price.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(result.Quantity);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Total.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Paid ? paid : string.Empty));

                csv.Append(",");
                csv.Append(result.InvoiceID);

                csv.Append(",");
                csv.Append(result.PayerID);
                csv.Append(",");
                csv.Append(result.Payer);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.FirstName));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.LastName));

                foreach (var field in includedBuyerFields)
                {
                    csv.Append(",");
                    if (packedValues.ContainsKey(string.Format("Buyer_{0}", field.Name)))
                    {
                        csv.Append(QuoteCSVData(packedValues[string.Format("Buyer_{0}", field.Name)]));
                    }
                }

                csv.Append(",");
                csv.Append(result.Email);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Address ?? string.Empty));
                
                csv.AppendLine();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.AdminResourceString("SalesTransactions_csv");
            return content;
        }

        /// <summary>
        /// Displays "Sales Invoices" admin report
        /// </summary>
        /// <param name="dateStart">the earliest invoice date to include</param>
        /// <param name="dateEnd">the latest invoice date to include</param>
        /// <param name="invoiceIDLow">the lowest invoice id to search for (-1 to skip this filter)</param>
        /// <param name="invoiceIDHigh">the highest invoice id to search for (-1 to skip this filter)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="payee">the partial payee username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="payer">the partial payer username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="lineItemCountLow">the lowest invoice lineitem count to search for (-1 to skip this filter)</param>
        /// <param name="lineItemCountHigh">the highest invoice lineitem count to search for (-1 to skip this filter)</param>
        /// <param name="totalQtyLow">the lowest total quantity of invoice line items to search for (-1 to skip this filter)</param>
        /// <param name="totalQtyHigh">the highest total quantity of invoice line items to search for (-1 to skip this filter)</param>
        /// <param name="subTotalLow">the lowest total quantity of the line item to search for (-1 to skip this filter)</param>
        /// <param name="subTotalHigh">the highest total quantity of the line item to search for (-1 to skip this filter)</param>
        /// <param name="taxLow">the lowest tax amount to search for (-1 to skip this filter)</param>
        /// <param name="taxHigh">the highest tax amount to search for (-1 to skip this filter)</param>
        /// <param name="shippingLow">the lowest shipping amount to search for (-1 to skip this filter)</param>
        /// <param name="shippingHigh">the highest shipping amount to search for (-1 to skip this filter)</param>
        /// <param name="buyersPremiumLow">the lowest buyers premium amount to search for (-1 to skip this filter)</param>
        /// <param name="buyersPremiumHigh">the highest buyers premium amount to search for (-1 to skip this filter)</param>
        /// <param name="adjustmentsLow">the lowest total adjustment amount to search for (-1 to skip this filter)</param>
        /// <param name="adjustmentsHigh">the highest total adjustment amount to search for (-1 to skip this filter)</param>
        /// <param name="totalLow">the lowest invoice total to search for (-1 to skip this filter)</param>
        /// <param name="totalHigh">the highest invoice total to search for (-1 to skip this filter)</param>
        /// <param name="isPaid">1 for paid only, 2 for unpaid only (0 to skip this filter)</param>
        /// <param name="firstName">the partial payer first name to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="lastName">the partial payer last name to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="address">the partial payer address to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="city">the partial payer city to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="stateRegion">the partial state/region username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="zipPostal">the partial zip/postal username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="country">the partial country username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>View(Page&lt;SalesTransactionReportResult&gt;)</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult SalesInvoicesReport(string dateStart,
                                    string dateEnd,
                                    string invoiceIDLow,
                                    string invoiceIDHigh,
                                    string eventID,
                                    string payee,
                                    string payer,
                                    string lineItemCountLow,
                                    string lineItemCountHigh,
                                    string totalQtyLow,
                                    string totalQtyHigh,
                                    string subTotalLow,
                                    string subTotalHigh,
                                    string taxLow,
                                    string taxHigh,
                                    string shippingLow,
                                    string shippingHigh,
                                    string buyersPremiumLow,
                                    string buyersPremiumHigh,
                                    string adjustmentsLow,
                                    string adjustmentsHigh,
                                    string totalLow,
                                    string totalHigh,
                                    string isPaid,
                                    string firstName,
                                    string lastName,
                                    string address,
                                    string city,
                                    string stateRegion,
                                    string zipPostal,
                                    string country,
                                    string sort, string page, string descending)
        {
            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            ViewData["dateStart"] = dateStart;
            ViewData["dateEnd"] = dateEnd;
            ViewData["invoiceIDLow"] = invoiceIDLow;
            ViewData["invoiceIDHigh"] = invoiceIDHigh;
            ViewData["eventID"] = eventID;
            ViewData["payee"] = payee;
            ViewData["payer"] = payer;
            ViewData["lineItemCountLow"] = lineItemCountLow;
            ViewData["lineItemCountHigh"] = lineItemCountHigh;
            ViewData["totalQtyLow"] = totalQtyLow;
            ViewData["totalQtyHigh"] = totalQtyHigh;
            ViewData["subTotalLow"] = subTotalLow;
            ViewData["subTotalHigh"] = subTotalHigh;
            ViewData["taxLow"] = taxLow;
            ViewData["taxHigh"] = taxHigh;
            ViewData["shippingLow"] = shippingLow;
            ViewData["shippingHigh"] = shippingHigh;
            ViewData["buyersPremiumLow"] = buyersPremiumLow;
            ViewData["buyersPremiumHigh"] = buyersPremiumHigh;
            ViewData["adjustmentsLow"] = adjustmentsLow;
            ViewData["adjustmentsHigh"] = adjustmentsHigh;
            ViewData["totalLow"] = totalLow;
            ViewData["totalHigh"] = totalHigh;
            ViewData["isPaid"] = isPaid;
            ViewData["firstName"] = firstName;
            ViewData["lastName"] = lastName;
            ViewData["address"] = address;
            ViewData["city"] = city;
            ViewData["stateRegion"] = stateRegion;
            ViewData["zipPostal"] = zipPostal;
            ViewData["country"] = country;
            ViewData["sort"] = sort ?? "CreatedDTTM";
            ViewData["page"] = page;
            ViewData["descending"] = string.IsNullOrEmpty(descending) ? true : bool.Parse(descending);

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            int isPaidInt = 0;
            int.TryParse(isPaid, out isPaidInt);

            SelectList paidStatus =
                new SelectList(new[] { 
                      new { value = 0, text = this.AdminResourceString("All") }
                    , new { value = 1, text = this.AdminResourceString("Paid") }
                    , new { value = 2, text = this.AdminResourceString("Unpaid") }                    
                }, "value", "text", isPaidInt);
            ViewData["PaidStatusSelectList"] = paidStatus;

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    this.GetCookie(Strings.MVC.CultureCookie),
                                                    this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else if (key == "dateStart")
                    {
                        input.Items.Add(key, dateStart);
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "25");

            Page<SalesInvoicesReportResult> retVal = null;
            try
            {
                int currencyCount = 0;
                decimal totalAmount = 0;
                retVal = AccountingClient.SalesInvoicesReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);
                ViewData["CurrencyCount"] = currencyCount;
                ViewData["TotalAmount"] = totalAmount;
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }

            return View(retVal);
        }

        /// <summary>
        /// Displays "Sales Invoices" admin report
        /// </summary>
        /// <param name="dateStart">the earliest invoice date to include</param>
        /// <param name="dateEnd">the latest invoice date to include</param>
        /// <param name="invoiceIDLow">the lowest invoice id to search for (-1 to skip this filter)</param>
        /// <param name="invoiceIDHigh">the highest invoice id to search for (-1 to skip this filter)</param>
        /// <param name="eventID">null, blank or -2 for all data, -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="payee">the partial payee username to search for ("" to skip this filter)</param>
        /// <param name="payer">the partial payer username to search for ("" to skip this filter)</param>
        /// <param name="lineItemCountLow">the lowest invoice lineitem count to search for (-1 to skip this filter)</param>
        /// <param name="lineItemCountHigh">the highest invoice lineitem count to search for (-1 to skip this filter)</param>
        /// <param name="totalQtyLow">the lowest total quantity of invoice line items to search for (-1 to skip this filter)</param>
        /// <param name="totalQtyHigh">the highest total quantity of invoice line items to search for (-1 to skip this filter)</param>
        /// <param name="subTotalLow">the lowest total quantity of the line item to search for (-1 to skip this filter)</param>
        /// <param name="subTotalHigh">the highest total quantity of the line item to search for (-1 to skip this filter)</param>
        /// <param name="taxLow">the lowest tax amount to search for (-1 to skip this filter)</param>
        /// <param name="taxHigh">the highest tax amount to search for (-1 to skip this filter)</param>
        /// <param name="shippingLow">the lowest shipping amount to search for (-1 to skip this filter)</param>
        /// <param name="shippingHigh">the highest shipping amount to search for (-1 to skip this filter)</param>
        /// <param name="buyersPremiumLow">the lowest buyers premium amount to search for (-1 to skip this filter)</param>
        /// <param name="buyersPremiumHigh">the highest buyers premium amount to search for (-1 to skip this filter)</param>
        /// <param name="adjustmentsLow">the lowest total adjustment amount to search for (-1 to skip this filter)</param>
        /// <param name="adjustmentsHigh">the highest total adjustment amount to search for (-1 to skip this filter)</param>
        /// <param name="totalLow">the lowest invoice total to search for (-1 to skip this filter)</param>
        /// <param name="totalHigh">the highest invoice total to search for (-1 to skip this filter)</param>
        /// <param name="isPaid">1 for paid only, 2 for unpaid only (0 to skip this filter)</param>
        /// <param name="firstName">the partial payer first name to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="lastName">the partial payer last name to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="address">the partial payer address to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="city">the partial payer city to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="stateRegion">the partial state/region username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="zipPostal">the partial zip/postal username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="country">the partial country username to search for (&quot;&quot; to skip this filter)</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>resulting CSV data</returns>
        [Authorize(Roles = Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult SalesInvoicesCSV(string dateStart,
                                    string dateEnd,
                                    string invoiceIDLow,
                                    string invoiceIDHigh,
                                    string eventID,
                                    string payee,
                                    string payer,
                                    string lineItemCountLow,
                                    string lineItemCountHigh,
                                    string totalQtyLow,
                                    string totalQtyHigh,
                                    string subTotalLow,
                                    string subTotalHigh,
                                    string taxLow,
                                    string taxHigh,
                                    string shippingLow,
                                    string shippingHigh,
                                    string buyersPremiumLow,
                                    string buyersPremiumHigh,
                                    string adjustmentsLow,
                                    string adjustmentsHigh,
                                    string totalLow,
                                    string totalHigh,
                                    string isPaid,
                                    string firstName,
                                    string lastName,
                                    string address,
                                    string city,
                                    string stateRegion,
                                    string zipPostal,
                                    string country,
                                    string sort, string descending)
        {

            //by default only show the last 90 days of transactions
            int defaultMonthsOld = 0;
            int.TryParse(ConfigurationManager.AppSettings["AdminReportDefaultMonthsOldFilter"], out defaultMonthsOld);
            if (defaultMonthsOld > 0 && string.IsNullOrWhiteSpace(dateStart) && string.IsNullOrEmpty(eventID))
            {
                dateStart = DateTime.Now.AddMonths(0 - defaultMonthsOld).ToString("d", this.GetCultureInfo());
            }

            if (!string.IsNullOrEmpty(dateEnd)) dateEnd = DateTime.Parse(dateEnd, this.GetCultureInfo()).AddDays(1).ToString("d", this.GetCultureInfo());

            StringBuilder csv = new StringBuilder();

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                   this.GetCookie(Strings.MVC.CultureCookie),
                                                   this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (!string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    if (key == "dateEnd")
                    {
                        input.Items.Add(key, dateEnd);
                    }
                    else
                    {
                        input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());
                    }
                }
            }
            if (defaultMonthsOld > 0 && !input.Items.ContainsKey("dateStart"))
            {
                input.Items.Add("dateStart", dateStart);
            }
            input.Items.Add("pageSize", "0");

            int currencyCount = 0;
            decimal totalAmount = 0;
            Page<SalesInvoicesReportResult> retVal = AccountingClient.SalesInvoicesReport(User.Identity.Name, input, ref currencyCount, ref totalAmount);

            //Get culture information
            CultureInfo tempCulture = CultureInfo.GetCultureInfo(this.GetCookie("culture") ?? SiteClient.SiteCulture);
            CultureInfo currentCulture = (CultureInfo)tempCulture.Clone();
            currentCulture.NumberFormat.CurrencySymbol = string.Empty;
            currentCulture.NumberFormat.CurrencyGroupSeparator = string.Empty;
            currentCulture.NumberFormat.CurrencyPositivePattern = 0;

            var includedListingFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.Item, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReport).ToList();

            List<CustomField> includedEventFields;
            if (SiteClient.EnableEvents)
            {
                includedEventFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.Event, 0, 0, "DisplayOrder", false).List
                    .Where(f => f.IncludeInSalesReport).ToList();
            }
            else
            {
                includedEventFields = new List<CustomField>(0);
            }

            var includedSellerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsSeller).ToList();

            var includedBuyerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsBuyer).ToList();

            //add header
            if (SiteClient.EnableEvents)
            {
                csv.Append(this.AdminResourceString("EventID"));
                csv.Append(",");

                foreach (var field in includedEventFields)
                {
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                    csv.Append(",");
                }
            }

            csv.Append(this.AdminResourceString("CreatedDTTM"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("InvoiceID"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Seller")); // renamed from "PayeeUserName"

            foreach (var field in includedSellerFields)
            {
                csv.Append(",");
                if (field.IncludeInSalesReportAsBuyer)
                {
                    csv.Append(QuoteCSVData(string.Format("Seller {0}", this.CustomFieldResourceString(field.Name))));
                }
                else
                {
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                }
            }

            csv.Append(",");
            csv.Append(this.AdminResourceString("TotalQty"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("ListingLineItemCount"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Subtotal"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("SalesTax"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("ShippingAmount"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("BuyersPremiumAmount"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("TotalAdjustments"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Total"));
            csv.Append(",");
            string paid = this.AdminResourceString("Paid");
            csv.Append(paid);

            csv.Append(",");
            csv.Append(this.AdminResourceString("Buyer")); // renamed from "PayerUserName"
            csv.Append(",");
            csv.Append(this.AdminResourceString("BuyerEmail"));

            csv.Append(",");
            csv.Append(this.AdminResourceString("FirstName"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("LastName"));

            foreach (var field in includedBuyerFields)
            {
                csv.Append(",");
                if (field.IncludeInSalesReportAsSeller)
                {
                    csv.Append(QuoteCSVData(string.Format("Buyer {0}", this.CustomFieldResourceString(field.Name))));
                }
                else
                {
                    csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
                }
            }

            csv.Append(",");
            csv.Append(this.AdminResourceString("Street1"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Street2"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("City"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("StateRegion"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("ZipPostal"));
            csv.Append(",");
            csv.Append(this.AdminResourceString("Country"));

            csv.AppendLine();

            foreach (SalesInvoicesReportResult result in retVal.List)
            {
                int allIncludedFieldsCount = includedListingFields.Count +
                    includedEventFields.Count +
                    includedSellerFields.Count +
                    includedBuyerFields.Count;
                Dictionary<string, string> packedValues = new Dictionary<string, string>(allIncludedFieldsCount);
                if (allIncludedFieldsCount > 0)
                {
                    var packedValueParts = result.PackedValues.Split('|');
                    foreach (var kvp in packedValueParts)
                    {
                        string key = null;
                        string value = null;
                        if (kvp.Contains(":="))
                        {
                            key = kvp.Left(kvp.IndexOf(":="));
                            value = kvp.Right(kvp.Length - key.Length - 2);
                            packedValues.Add(key, value);
                        }
                    }
                }

                if (SiteClient.EnableEvents)
                {
                    csv.Append(result.AuctionEventId.HasValue ? result.AuctionEventId.ToString() : string.Empty);
                    csv.Append(",");
                    foreach (var field in includedEventFields)
                    {
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                        csv.Append(",");
                    }
                }

                csv.Append(QuoteCSVData(result.CreatedDTTM.ToString("G", currentCulture)));
                csv.Append(",");
                csv.Append(result.InvoiceID);
                csv.Append(",");
                csv.Append(result.PayeeUserName);

                foreach (var field in includedSellerFields)
                {
                    csv.Append(",");
                    if (packedValues.ContainsKey(string.Format("Seller_{0}", field.Name)))
                    {
                        csv.Append(QuoteCSVData(packedValues[string.Format("Seller_{0}", field.Name)]));
                    }
                }

                csv.Append(",");
                csv.Append(result.TotalQty);
                csv.Append(",");
                csv.Append(result.ListingLineItemCount);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Subtotal.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.SalesTax.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.ShippingAmount.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.BuyersPremiumAmount.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.TotalAdjustments.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Total.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.InvoiceStatus == Strings.InvoiceStatuses.Paid ? paid : string.Empty));

                csv.Append(",");
                csv.Append(result.PayerUserName);
                csv.Append(",");
                csv.Append(result.PayerEmail);

                csv.Append(",");
                csv.Append(result.FirstName);
                csv.Append(",");
                csv.Append(result.LastName);

                foreach (var field in includedBuyerFields)
                {
                    csv.Append(",");
                    if (packedValues.ContainsKey(string.Format("Buyer_{0}", field.Name)))
                    {
                        csv.Append(QuoteCSVData(packedValues[string.Format("Buyer_{0}", field.Name)]));
                    }
                }

                csv.Append(",");
                csv.Append(result.Street1);
                csv.Append(",");
                csv.Append(result.Street2);
                csv.Append(",");
                csv.Append(result.City);
                csv.Append(",");
                csv.Append(result.StateRegion);
                csv.Append(",");
                csv.Append(result.ZipPostal);
                csv.Append(",");
                csv.Append(result.Country);

                csv.AppendLine();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.AdminResourceString("SalesInvoices_csv");
            return content;
        }

        /// <summary>
        /// Converts the specified value to be safe for CSV use
        /// </summary>
        /// <param name="data">the specified value</param>
        /// <returns>CSV-safe version of the specified value</returns>
        private string QuoteCSVData(string data)
        {
            if (data == null) return string.Empty;

            if (data.Contains(",") || data.Contains("\"") || data.Contains("\n") || data.Contains("\r"))
            {
                //value must be quoted
                data = data.Replace("\"", "\"\"");
                data = "\"" + data + "\"";
            }

            return data;
        }

        private void AddCustomFieldsToColumnList(List<ReportColumn> columns, List<CustomField> fields, string prefix)
        {
            foreach (var field in fields.OrderBy(f => f.DisplayOrder))
            {
                string inputKey1 = null;
                string inputKey2 = null;
                string columnName = string.Format("{0}_{1}", prefix, field.ID);

                switch (field.Type)
                {
                    case CustomFieldType.Boolean:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.IsEqualTo);
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, inputKey2, ReportArgumentTypes.BoolValue));
                        break;
                    case CustomFieldType.DateTime:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.GTE);
                        inputKey2 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.LTE);
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, inputKey2, ReportArgumentTypes.DateTimeValue));
                        break;
                    case CustomFieldType.Decimal:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.GTE);
                        inputKey2 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.LTE);
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, inputKey2, ReportArgumentTypes.DecimalValue));
                        break;
                    case CustomFieldType.Enum:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.IsEqualTo);
                        string typeValue = ReportArgumentTypes.EnumValue;
                        foreach(var opt in field.Enumeration)
                        {
                            typeValue += ("|" + opt.Value);
                        }
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, typeValue));
                        break;
                    case CustomFieldType.Int:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.IsEqualTo);
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, ReportArgumentTypes.IntegerValue));
                        break;
                    case CustomFieldType.String:
                        inputKey1 = string.Format("{0}_{1}_{2}", prefix, field.ID, ReportArgumentOperators.Like);
                        columns.Add(new ReportColumn(columnName, this.CustomFieldResourceString(field.Name), inputKey1, ReportArgumentTypes.StringValue));
                        break;
                }
            }
        }

        private List<ReportColumn> GetEventActivityReportColumns()
        {
            List<ReportColumn> columnList = (List<ReportColumn>)SiteClient.GetCacheData("EventActivityReportColumns");
            if (columnList == null)
            {
                var eventFields = CommonClient.GetCustomFields(CustomFieldGroups.Event, 0, 0, Strings.Fields.Id, false).List.Where(cf => cf.IncludeInSalesReport).ToList();
                var listingFields = CommonClient.GetCustomFields(CustomFieldGroups.Item, 0, 0, Strings.Fields.Id, false).List.Where(cf => cf.IncludeInSalesReport).ToList();
                var allUserFields = CommonClient.GetCustomFields(CustomFieldGroups.User, 0, 0, Strings.Fields.Id, false).List;
                var sellerFields = allUserFields.Where(cf => cf.IncludeInSalesReportAsSeller).ToList();
                var buyerFields = allUserFields.Where(cf => cf.IncludeInSalesReportAsBuyer).ToList();

                columnList = new List<ReportColumn>();
                columnList.Add(new ReportColumn("DateTime", this.GlobalResourceString("DateTime"), "FromEndDTTM", "ToEndDTTM", ReportArgumentTypes.DateTimeValue));
                columnList.Add(new ReportColumn("SellerUN", this.GlobalResourceString("Seller"), "SellerUN", ReportArgumentTypes.StringValue));
                AddCustomFieldsToColumnList(columnList, sellerFields, ReportArgumentPrefixes.SCF); // seller fields
                columnList.Add(new ReportColumn("EventID", this.GlobalResourceString("EventID"), "EventID", ReportArgumentTypes.IntegerValue));
                AddCustomFieldsToColumnList(columnList, eventFields, ReportArgumentPrefixes.ECF); // event fields
                columnList.Add(new ReportColumn("ListingID", this.GlobalResourceString("ListingID"), "ListingID", ReportArgumentTypes.IntegerValue));
                columnList.Add(new ReportColumn("LotNumber", this.GlobalResourceString("LotNumber"), "LotNumber", ReportArgumentTypes.StringValue));
                columnList.Add(new ReportColumn("Title", this.GlobalResourceString("Title"), "Title", ReportArgumentTypes.StringValue));
                AddCustomFieldsToColumnList(columnList, listingFields, ReportArgumentPrefixes.LCF); // listing fields
                columnList.Add(new ReportColumn("ReservePrice", this.ResourceString("AuctionListing, ReservePrice"), "FromReservePrice", "ToReservePrice", ReportArgumentTypes.DecimalValue));
                columnList.Add(new ReportColumn("ReserveMet", this.GlobalResourceString("ReservePriceMet"), "ReserveMet", ReportArgumentTypes.BoolValue));
                columnList.Add(new ReportColumn("Status", this.GlobalResourceString("Status"), "FinalStatus", ReportArgumentTypes.EnumValue + "|Successful|Unsuccessful|NoBids"));
                columnList.Add(new ReportColumn("Price", this.GlobalResourceString("Price"), "FromFinalPrice", "ToFinalPrice", ReportArgumentTypes.DecimalValue));
                columnList.Add(new ReportColumn("HighestBid", this.GlobalResourceString("HighestBid"), "FromHighestBid", "ToHighestBid", ReportArgumentTypes.DecimalValue));
                columnList.Add(new ReportColumn("ProxyBid", this.GlobalResourceString("ProxyBid"), "FromProxyBid", "ToProxyBid", ReportArgumentTypes.DecimalValue));
                columnList.Add(new ReportColumn("BuyerUN", this.GlobalResourceString("Buyer"), "BuyerUN", ReportArgumentTypes.StringValue));
                columnList.Add(new ReportColumn("BCF_5", this.CustomFieldResourceString("FirstName"), "BCF_5_Like", ReportArgumentTypes.StringValue));
                columnList.Add(new ReportColumn("BCF_6", this.CustomFieldResourceString("LastName"), "BCF_6_Like", ReportArgumentTypes.StringValue));
                AddCustomFieldsToColumnList(columnList, buyerFields, ReportArgumentPrefixes.BCF); //buyer fields
                columnList.Add(new ReportColumn("Email", this.GlobalResourceString("Email"), "BuyerEmail", ReportArgumentTypes.StringValue));
                columnList.Add(new ReportColumn("Address", this.GlobalResourceString("Address"), null, null, null, false, 6)); // no filters and no sort option for this column, by design

                SiteClient.SetCacheData("EventActivityReportColumns", columnList, 5);
            }
            return columnList;
        }

        /// <summary>
        /// Displays "Event Activity" admin report
        /// </summary>
        /// <param name="EventID">The ID of the spcified Event; if missing, the most recent closed event is used</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>View(Page&lt;ReportRow&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult EventActivityReport(int? EventID, string sort, int? page, bool? descending)
        {
            if (!EventID.HasValue)
            {
                //find the most recent closed event as the default, if none was specified
                var closedEvents = EventClient.GetEventsByStatusWithFillLevel(User.Identity.Name, AuctionEventStatuses.Closed, 0, 1, Strings.Fields.EndDTTM, true, EventFillLevels.None);
                if (closedEvents.TotalItemCount > 0)
                {
                    EventID = closedEvents.List.First().ID;
                }
                else
                {
                    //in case the site is brand new, show *any* event rather than nothing
                    string alternateStatuses = AuctionEventStatuses.Active + "," + AuctionEventStatuses.Closing + "," + AuctionEventStatuses.Preview;
                    var alternateEvents = EventClient.GetEventsByStatusWithFillLevel(User.Identity.Name, alternateStatuses, 0, 1, Strings.Fields.EndDTTM, true, EventFillLevels.None);
                    if (alternateEvents.TotalItemCount > 0)
                    {
                        EventID = alternateEvents.List.First().ID;
                    }
                }
            }
            ViewData["sort"] = sort ?? "LotOrder";
            ViewData["descending"] = descending ?? false;

            var columnList = GetEventActivityReportColumns();
            ViewData["ReportColumns"] = columnList;

            Page<ReportRow> results = null;
            const int pageSize = 10;
            UserInput input = new UserInput(User.Identity.IsAuthenticated ? User.Identity.Name : null, this.FBOUserName(),
                                            this.GetCookie(Strings.MVC.CultureCookie),
                                            this.GetCookie(Strings.MVC.CultureCookie));

            //input.AddAllFormValues(this);
            input.AddAllQueryStringValues(this);

            if (EventID.HasValue)
            {
                if (!input.Items.ContainsKey("EventID"))
                {
                    input.Items.Add("EventID", EventID.Value.ToString(CultureInfo.InvariantCulture));
                }
                else if (string.IsNullOrEmpty(input.Items["EventID"]))
                {
                    input.Items["EventID"] = EventID.Value.ToString(CultureInfo.InvariantCulture);
                }
                ViewData["EventID"] = input.Items["EventID"];
            }

            //explicitly set ViewData for any selected dropdown values
            foreach (var rptCol in columnList.Where(rc => rc.InputType == ReportArgumentTypes.EnumValue))
            {
                if (input.Items.ContainsKey(rptCol.InputKey1) && !string.IsNullOrEmpty(input.Items[rptCol.InputKey1]))
                {
                    ViewData[rptCol.InputKey1] = input.Items[rptCol.InputKey1];
                }
            }

            try
            {
                var inputTypes = new Dictionary<string, string>();
                foreach(var rptCol in columnList)
                {
                    if (!string.IsNullOrEmpty(rptCol.InputKey1)) inputTypes.Add(rptCol.InputKey1, rptCol.InputType);
                    if (!string.IsNullOrEmpty(rptCol.InputKey2)) inputTypes.Add(rptCol.InputKey2, rptCol.InputType);
                }
                results = CommonClient.GetEventActivityReportResults(input.ActingUserName, inputTypes, input, page ?? 0, pageSize, sort ?? "LotOrder", descending ?? false);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }

            return View(results);
        }

        /// <summary>
        /// Displays "Event Activity" admin report
        /// </summary>
        /// <param name="EventID">The ID of the spcified Event; if missing, the most recent closed event is used</param>
        /// <param name="sort">the name of the column to sort the results by</param>
        /// <param name="descending">true to order the results from highest to lowest</param>
        /// <returns>resulting CSV data</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult EventActivityReportCSV(int? EventID, string sort, bool? descending)
        {
            if (!EventID.HasValue)
            {
                //find the most recent closed event as the default, if none was specified
                var closedEvents = EventClient.GetEventsByStatusWithFillLevel(User.Identity.Name, AuctionEventStatuses.Closed, 0, 1, Strings.Fields.EndDTTM, true, EventFillLevels.None);
                if (closedEvents.TotalItemCount > 0)
                {
                    EventID = closedEvents.List.First().ID;
                }
                else
                {
                    //in case the site is brand new, show *any* event rather than nothing
                    string alternateStatuses = AuctionEventStatuses.Active + "," + AuctionEventStatuses.Closing + "," + AuctionEventStatuses.Preview;
                    var alternateEvents = EventClient.GetEventsByStatusWithFillLevel(User.Identity.Name, alternateStatuses, 0, 1, Strings.Fields.EndDTTM, true, EventFillLevels.None);
                    if (alternateEvents.TotalItemCount > 0)
                    {
                        EventID = alternateEvents.List.First().ID;
                    }
                }
            }
            //ViewData["sort"] = sort ?? "LotOrder";
            //ViewData["descending"] = descending ?? false;

            var columnList = GetEventActivityReportColumns();
            //ViewData["ReportColumns"] = columnList;

            Page<ReportRow> results = null;
            //const int pageSize = 10;
            UserInput input = new UserInput(User.Identity.IsAuthenticated ? User.Identity.Name : null, this.FBOUserName(),
                                            this.GetCookie(Strings.MVC.CultureCookie),
                                            this.GetCookie(Strings.MVC.CultureCookie));

            //input.AddAllFormValues(this);
            input.AddAllQueryStringValues(this);

            if (EventID.HasValue)
            {
                if (!input.Items.ContainsKey("EventID"))
                {
                    input.Items.Add("EventID", EventID.Value.ToString(CultureInfo.InvariantCulture));
                }
                else if (string.IsNullOrEmpty(input.Items["EventID"]))
                {
                    input.Items["EventID"] = EventID.Value.ToString(CultureInfo.InvariantCulture);
                }
                //ViewData["EventID"] = input.Items["EventID"];
            }

            ////explicitly set ViewData for any selected dropdown values
            //foreach (var rptCol in columnList.Where(rc => rc.InputType == ReportArgumentTypes.EnumValue))
            //{
            //    if (input.Items.ContainsKey(rptCol.InputKey1) && !string.IsNullOrEmpty(input.Items[rptCol.InputKey1]))
            //    {
            //        ViewData[rptCol.InputKey1] = input.Items[rptCol.InputKey1];
            //    }
            //}

            //try
            //{
                var inputTypes = new Dictionary<string, string>();
                foreach (var rptCol in columnList)
                {
                    if (!string.IsNullOrEmpty(rptCol.InputKey1)) inputTypes.Add(rptCol.InputKey1, rptCol.InputType);
                    if (!string.IsNullOrEmpty(rptCol.InputKey2)) inputTypes.Add(rptCol.InputKey2, rptCol.InputType);
                }
                results = CommonClient.GetEventActivityReportResults(input.ActingUserName, inputTypes, input, 0, 0, sort ?? "LotOrder", descending ?? false);
            //}
            //catch (FaultException<ValidationFaultContract> vfc)
            //{
            //    //display validation errors                
            //    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
            //    {
            //        ModelState.AddModelError(issue.Key, issue.Message);
            //    }
            //}

            //return View(results);

            StringBuilder csv = new StringBuilder();

            //Get culture information
            CultureInfo tempCulture = CultureInfo.GetCultureInfo(this.GetCookie("culture") ?? SiteClient.SiteCulture);
            CultureInfo currentCulture = (CultureInfo)tempCulture.Clone();
            currentCulture.NumberFormat.CurrencySymbol = string.Empty;
            currentCulture.NumberFormat.CurrencyGroupSeparator = string.Empty;
            currentCulture.NumberFormat.CurrencyPositivePattern = 0;

            //add header
            string delim = "";
            foreach(var column in columnList)
            {
                csv.Append(delim);
                csv.Append(QuoteCSVData(column.DisplayName));
                delim = ",";
            }
            csv.AppendLine();

            foreach (ReportRow reportRecord in results.List)
            {
                var values = new Dictionary<string, string>();
                var types = new Dictionary<string, string>();
                for (int i = 0; i < reportRecord.Names.Count; i++)
                {
                    values.Add(reportRecord.Names[i], reportRecord.Values[i]);
                    types.Add(reportRecord.Names[i], reportRecord.Types[i]);
                }
                string currenyCode = values["CurrencyCode"];
                delim = "";
                foreach (var thisReportColumn in columnList)
                {
                    string thisColumnType = null;
                    string thisColumnValue = null;
                    try
                    {
                        thisColumnType = types[thisReportColumn.ColumnName]; // reportRecord.Types[thisReportColIndex];
                        thisColumnValue = values[thisReportColumn.ColumnName]; // reportRecord.Values[thisReportColIndex];
                    }
                    catch (Exception)
                    {
                        thisColumnType = ReportValueTypes.UnknownType;
                        thisColumnValue = string.Empty;
                    }
                    csv.Append(delim);

                    if (!string.IsNullOrEmpty(thisColumnValue))
                    {
                        if (thisColumnType == ReportValueTypes.BoolValue)
                        {
                            bool boolValue;
                            if (bool.TryParse(thisColumnValue, out boolValue))
                            {
                                csv.Append(QuoteCSVData(boolValue ? this.GlobalResourceString("Yes") : this.GlobalResourceString("No")));
                            }
                        }
                        else if (thisColumnType == ReportValueTypes.DateTimeValue)
                        {
                            DateTime dttmValue;
                            if (DateTime.TryParse(thisColumnValue, out dttmValue))
                            {
                                csv.Append(QuoteCSVData(this.LocalDTTMString(dttmValue, "G")));
                            }
                        }
                        else if (thisColumnType == ReportValueTypes.DecimalValue)
                        {
                            decimal decimalValue;
                            if (decimal.TryParse(thisColumnValue, out decimalValue))
                            {
                                //csv.Append(QuoteCSVData(this.LocalCurrency(decimalValue, currenyCode)));
                                csv.Append(QuoteCSVData(decimalValue.ToString("c", currentCulture)));
                            }
                        }
                        else if (thisColumnType == ReportValueTypes.IntegerValue)
                        {
                            csv.Append(QuoteCSVData(thisColumnValue));
                            
                        }
                        else if (thisReportColumn.InputKey1 == "FinalStatus")
                        {
                            csv.Append(QuoteCSVData(this.AdminResourceString("ActivityReportFinalStatus_" + thisColumnValue)));
                        }
                        else if (thisColumnType == ReportValueTypes.StringValue)
                        {
                            csv.Append(QuoteCSVData(thisColumnValue));
                        }
                        else if (thisColumnType == ReportValueTypes.UnknownType)
                        {
                            csv.Append(QuoteCSVData(thisColumnValue));
                        }
                    }
                    delim = ",";
                }
                csv.AppendLine();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.AdminResourceString("EventActivity_X_csv", EventID.HasValue ? EventID.ToString() : "All");
            return content;
        }

        /// <summary>
        /// Displays the results of the specified custom stored procedure
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult Reports(string id)
        {
            string customAdminReports = ConfigurationManager.AppSettings["CustomAdminReports"] ?? string.Empty;
            var columnList = new List<string>();
            var retVal = new List<Dictionary<string, object>>();
            try
            {
                if (id == null || !customAdminReports.ToLower().Contains(id.ToLower()))
                {
                    throw new Exception(string.Format("Unknown Procedure \"{0}\"", id));
                }

                string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.CommandText = id;
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                    while (reader.Read())
                    {
                        var resultRow = new Dictionary<string, object>();
                        if (columnList.Count == 0)
                        {
                            columnList.AddRange(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
                        }
                        foreach(string columnName in columnList)
                        {
                            object rawVal = reader[columnName];
                            switch (rawVal.GetType().Name)
                            {
                                default:
                                    //resultRow[columnName] = rawVal.GetType().ToString();
                                    resultRow[columnName] = rawVal;
                                    break;
                            }
                        }
                        retVal.Add(resultRow);
                    }
                    connection.Close();
                }
            }
            catch (Exception e)
            {
                columnList.Add("ERROR");
                retVal = new List<Dictionary<string, object>>();
                retVal.Add(new Dictionary<string, object>() { { "ERROR", e.Message } });
            }
            ViewData["Columns"] = columnList;
            ViewData["ReportName"] = !string.IsNullOrWhiteSpace(id) ? id.Trim() : "No Report Specified";

            return View(retVal);
        }

        #endregion Reports

        #region Import CSV

        /// <summary>
        /// Displays form to upload a CSV file of listings to be imported
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult ImportCSV()
        {
            ViewData["ResultsEmail"] = UserClient.GetUserByUserName(User.Identity.Name, User.Identity.Name).Email; //SiteClient.Settings["AdministratorEmail"];
            return View();
        }

        /// <summary>
        /// Processes request to upload a CSV file of listings to be imported
        /// </summary>
        /// <param name="validate">true if the uploaded file should be validated only, not imported</param>
        /// <param name="drafts">true if listings are to be saved with Status:Draft instead of being activated immediately</param>
        /// <param name="resultsEmail">email address to notify when the import has completed</param>
        /// <param name="formCollection">the collection containing all form fields submitted by the user</param>
        /// <returns></returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ImportCSV(bool validate, bool drafts, string resultsEmail, FormCollection formCollection)
        {
            Dictionary<string, object> importProperties = new Dictionary<string, object>();
            importProperties.Add("resultsEmail", resultsEmail);
            importProperties.Add("actingUserName", User.Identity.Name);
            importProperties.Add("culture", this.GetCookie(Strings.MVC.CultureCookie));
            importProperties.Add("fileName", Request.Files[0].FileName);
            importProperties.Add("validateOnly", validate);

            ViewData["ResultsEmail"] = resultsEmail;

            ImportData csvData = CSV.Parse(Request.Files[0].InputStream);

            if (csvData.Status != ImportListingStatus.Success)
            {
                //parse errors, show on page         
                importProperties.Add("Disposition", csvData.Disposition);
                LogManager.WriteLog("CSV " + (validate ? "Validation" : "Import") + " Parse Error", "Import CSV", Strings.FunctionalAreas.Site,
                                TraceEventType.Error, null, null, importProperties);

                return View(csvData);
            }

            //ImportCSVProc(csvData, resultsEmail, User.Identity.Name, this.GetCookie(Strings.MVC.CultureCookie), Request.Files[0].FileName, validate);            
            KeepAlive.Start();
            Thread t = new Thread(ImportCSVThreader);
            t.IsBackground = false;
            t.Name = "ImportCSV";
            t.Start(new ImportCSVArgs()
            {
                CSVData = csvData,
                ActingUserName = User.Identity.Name,
                Culture = this.GetCookie(Strings.MVC.CultureCookie),
                FileName = Request.Files[0].FileName,
                ResultsEmail = resultsEmail,
                ValidateOnly = validate,
                SaveAsDraft = drafts
            });

            //PrepareSuccessMessage(" CSV listing import has started.  Import and validation results will be sent to the following email address once completed: " + resultsEmail, MessageType.Message);
            PrepareSuccessMessage(this.AdminResourceString("CSV_ListingImportStarted", resultsEmail), MessageType.Message);
            return View();
        }

        /// <summary>
        /// container for Import CSV arguments
        /// </summary>
        public class ImportCSVArgs
        {
            /// <summary>
            /// csv data
            /// </summary>
            public ImportData CSVData;

            /// <summary>
            /// email to send final status message to
            /// </summary>
            public string ResultsEmail;

            /// <summary>
            /// username of the acting user
            /// </summary>
            public string ActingUserName;

            /// <summary>
            /// culture that data is expected to be formatted in
            /// </summary>
            public string Culture;

            /// <summary>
            /// the filename use for resulting output
            /// </summary>
            public string FileName;

            /// <summary>
            /// true to validate the data without actually performing any inserts
            /// </summary>
            public bool ValidateOnly;

            /// <summary>
            /// true to save as drafts
            /// </summary>
            public bool SaveAsDraft;
        }

        private void ImportCSVThreader(object args)
        {
            ImportCSVArgs unpackedArgs = (ImportCSVArgs)args;
            ImportCSVProc(unpackedArgs.CSVData,
                unpackedArgs.ResultsEmail,
                unpackedArgs.ActingUserName,
                unpackedArgs.Culture,
                unpackedArgs.FileName,
                unpackedArgs.ValidateOnly,
                unpackedArgs.SaveAsDraft);
        }

        private void ImportCSVProc(ImportData csvData, string resultsEmail, string actingUserName, string culture, string fileName, bool validateOnly, bool saveAsDraft)
        {
            Dictionary<string, object> importProperties = new Dictionary<string, object>();
            importProperties.Add("resultsEmail", resultsEmail);
            importProperties.Add("actingUserName", actingUserName);
            importProperties.Add("culture", this.GetCookie(Strings.MVC.CultureCookie));
            importProperties.Add("fileName", Request.Files[0].FileName);
            importProperties.Add("validateOnly", validateOnly);
            importProperties.Add("saveAsDraft", saveAsDraft);

            try
            {
                DateTime jobStartTime = DateTime.UtcNow;

                List<IColumnSpec> csvColumns = CSV.GetColumnSpec(this, actingUserName, Server.MapPath("~"), culture, saveAsDraft);
                if (!CSV.PreValidate(csvColumns, csvData))
                {
                    var parseErrorMessage = new MailMessage
                    {
                        From = new MailAddress(
                            SiteClient.Settings[Strings.SiteProperties.SystemEmailAddress],
                            SiteClient.Settings[Strings.SiteProperties.SystemEmailName]),
                        Subject = //"Import results for " + fileName + " job started at " + jobStartTime.ToString()
                            this.AdminResourceString("CSV_ImportResultsForXxxJobStartedAtYyy", fileName, this.LocalDTTM(jobStartTime).ToString())
                    };
                    parseErrorMessage.To.Add(new MailAddress(resultsEmail, resultsEmail));

                    try
                    {
                        StringBuilder parserSB = new StringBuilder();
                        parserSB.AppendLine(this.AdminResourceString("CSV_YourFileFailedPreValidationNoActionHasBeenTaken")); //"Your File failed pre-validation.  No action has been taken."
                        parserSB.AppendLine();

                        foreach (
                            ImportListing listing in
                                csvData.ListingData.Where(l => l.Status == ImportListingStatus.Validation || l.Status == ImportListingStatus.Exception))
                        {
                            parserSB.Append(this.AdminResourceString("CSV_Line") + ": ");
                            parserSB.Append(listing.Line);
                            parserSB.Append(", " + this.AdminResourceString("CSV_Title") + ": \"");
                            parserSB.Append(listing.ColumnData["Title"]);
                            parserSB.AppendLine("\"");
                            parserSB.AppendLine("--- " + this.AdminResourceString("CSV_ValidationErrorsFromCsvPreValidator") + " ---");

                            foreach (string disposition in listing.Disposition)
                            {
                                parserSB.AppendLine(disposition);
                            }
                            parserSB.AppendLine();
                        }

                        parseErrorMessage.Body = parserSB.ToString();
                    }
                    catch (Exception e)
                    {
                        parseErrorMessage.Body = this.AdminResourceString("CSV_EmailSendErrorParserErrorEmail") + ": " + e.Message;
                    }

                    parseErrorMessage.BodyEncoding = Encoding.UTF8;
                    parseErrorMessage.IsBodyHtml = false;

                    try
                    {
                        var parseErrorClient = new SmtpClient();
                        parseErrorClient.Send(parseErrorMessage);
                        LogManager.WriteLog("CSV " + (validateOnly ? "Validation" : "Import") + " Pre-Validation Failed, email sent", "Import CSV", Strings.FunctionalAreas.Site,
                                TraceEventType.Error, null, null, importProperties);
                    }
                    catch (Exception)
                    {
                        //ignore this exception
                    }

                    //pre-validation failed, don't follow up with actual validation :)
                    return;
                }
                else
                {
                    LogManager.WriteLog("Starting CSV " + (validateOnly ? "Validation" : "Import"), "Import CSV",
                        Strings.FunctionalAreas.Site,
                        TraceEventType.Start, null, null, importProperties);
                    var auctionEvents = new Dictionary<int, Event>();
                    foreach (ImportListing listing in csvData.ListingData)
                    {
                        try
                        {
                            int eventId;
                            Event auctionEvent = null;
                            if (SiteClient.EnableEvents)
                            {
                                if (listing.ColumnData.ContainsKey(Strings.Fields.EventID) && int.TryParse(listing.ColumnData[Strings.Fields.EventID], out eventId))
                                {
                                    if (!auctionEvents.ContainsKey(eventId))
                                    {
                                        auctionEvents.Add(eventId, EventClient.GetEventByIDWithFillLevel(actingUserName, eventId, EventFillLevels.None));
                                    }
                                    auctionEvent = auctionEvents[eventId];
                                }
                            }
                            
                            //if "Seller" column is missing or blank, use either the impersonated user or the logged in user
                            string sellerUserName;
                            if (auctionEvent != null)
                            {
                                sellerUserName = auctionEvent.OwnerUserName;
                            }
                            else if (listing.ColumnData.ContainsKey("Seller") && !string.IsNullOrEmpty(listing.ColumnData["Seller"]))
                            {
                                sellerUserName = listing.ColumnData["Seller"];
                            }
                            else
                            {
                                sellerUserName = actingUserName;
                            }

                            UserInput input = new UserInput(actingUserName, sellerUserName, culture, culture);

                            CSV.Translate(csvColumns, listing, input.Items, !validateOnly);

                            if (saveAsDraft)
                            {
                                input.Items.Add(Strings.Fields.SaveAsDraft, "True");
                            }

                            //fill some values from the specified event if applicable
                            if (auctionEvent != null)
                            {
                                input.Items.Add(Strings.Fields.PublishNow, auctionEvent.Published.ToString());
                                if (input.Items.ContainsKey(Strings.Fields.ListingType))
                                {
                                    input.Items[Strings.Fields.ListingType] = ListingTypes.Auction;
                                }
                                else
                                {
                                    input.Items.Add(Strings.Fields.ListingType, ListingTypes.Auction);
                                }
                                if (input.Items.ContainsKey(Strings.Fields.Currency))
                                {
                                    input.Items[Strings.Fields.Currency] = auctionEvent.Currency.Code;
                                }
                                else
                                {
                                    input.Items.Add(Strings.Fields.Currency, auctionEvent.Currency.Code);
                                }
                            }

                            if (!input.Items.ContainsKey(Strings.Fields.IsTaxable))
                            {
                                input.Items.Add(Strings.Fields.IsTaxable, false.ToString());
                            }

                            if (listing.Status == ImportListingStatus.Success)
                            {

                                //CreateListing                
                                try
                                {
                                    if (auctionEvent != null)
                                    {
                                        int lotId;
                                        EventClient.CreateLot(actingUserName, input, false, out lotId, validateOnly);
                                        Dictionary<string, object> properties =
                                            new Dictionary<string, object>(input.Items.ToDictionary(k => k.Key,
                                                k => (object)k.Value));
                                        LogManager.WriteLog((validateOnly ? "Validated" : "Created") + " Lot from CSV",
                                            "Import CSV", FunctionalAreas.Site,
                                            TraceEventType.Information, null, null, properties);
                                    }
                                    else
                                    {
                                        int listingID;
                                        ListingClient.CreateListing(actingUserName, input, false, out listingID, validateOnly);
                                        Dictionary<string, object> properties =
                                            new Dictionary<string, object>(input.Items.ToDictionary(k => k.Key,
                                                k => (object)k.Value));
                                        LogManager.WriteLog((validateOnly ? "Validated" : "Created") + " Listing from CSV",
                                            "Import CSV", FunctionalAreas.Site,
                                            TraceEventType.Information, null, null, properties);
                                    }
                                    listing.Status = ImportListingStatus.Success;
                                }
                                catch (FaultException<ValidationFaultContract> vfc)
                                {
                                    listing.Status = ImportListingStatus.Validation;
                                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                                    {
                                        listing.Disposition.Add(issue.Key + ": " + issue.Message);
                                    }

                                    Dictionary<string, object> properties =
                                        new Dictionary<string, object>(input.Items.ToDictionary(k => k.Key,
                                            k => (object)k.Value));
                                    LogManager.WriteLog(
                                        "Failed " + (validateOnly ? "Validating" : "Creating") + " Listing from CSV",
                                        "Import CSV", FunctionalAreas.Site,
                                        TraceEventType.Warning, null, null, properties);
                                }
                                catch (Exception e)
                                {
                                    listing.Status = ImportListingStatus.Exception;
                                    listing.Disposition.Add("1_" + e.Message);
                                    LogManager.WriteLog("CSV Import Error (1)", "Import CSV Error",
                                        FunctionalAreas.Site,
                                        TraceEventType.Error, actingUserName, e);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            listing.Status = ImportListingStatus.Exception;
                            listing.Disposition.Add("2_" + e.Message);
                            LogManager.WriteLog("CSV Import Error (2)", "Import CSV Error",
                                FunctionalAreas.Site,
                                TraceEventType.Error, actingUserName, e);
                        }
                    }

                    try
                    {
                        CommonClient.DeleteExpiredOriginalMediasNow(actingUserName, Server.MapPath("~"));
                    }
                    catch (Exception)
                    {
                        //ignore this exception
                    }

                    LogManager.WriteLog("Stopping CSV " + (validateOnly ? "Validation" : "Import"), "Import CSV",
                        FunctionalAreas.Site,
                        TraceEventType.Stop, null, null, importProperties);
                }

                DateTime jobEndTime = DateTime.UtcNow;

                //email results
                //send email
                //do somethign with message...
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        SiteClient.Settings[SiteProperties.SystemEmailAddress],
                        SiteClient.Settings[SiteProperties.SystemEmailName]),
                    Subject = this.AdminResourceString("CSV_ImportResultsForXxxJobstartedAtYyy", fileName, this.LocalDTTM(jobStartTime).ToString())
                };
                mailMessage.To.Add(new MailAddress(resultsEmail, resultsEmail));

                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(this.AdminResourceString("CSV_ImportResults"));

                    if (validateOnly)
                    {
                        sb.AppendLine();
                        sb.AppendLine(this.AdminResourceString("CSV_NoteValidateOnlyNoActionTaken"));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine();
                    }

                    sb.Append(this.AdminResourceString("CSV_SuccessCount") + ": ");
                    sb.AppendLine(csvData.ListingData.Count(l => l.Status == ImportListingStatus.Success).ToString());
                    sb.Append(this.AdminResourceString("CSV_ValidationErrorCount") + ": ");
                    sb.AppendLine(csvData.ListingData.Count(l => l.Status == ImportListingStatus.Validation).ToString());
                    sb.Append(this.AdminResourceString("CSV_ExceptionCount") + ": ");
                    sb.AppendLine(csvData.ListingData.Count(l => l.Status == ImportListingStatus.Exception).ToString());
                    sb.AppendLine();
                    sb.Append(this.AdminResourceString("CSV_ProcessingTime") + ": ");
                    sb.Append(jobEndTime.Subtract(jobStartTime).TotalSeconds);
                    sb.AppendLine(" " + this.AdminResourceString("CSV_seconds") + ".");
                    sb.AppendLine();

                    List<ImportListing> exceptions =
                        csvData.ListingData.Where(l => l.Status == ImportListingStatus.Exception).ToList();
                    List<ImportListing> validationErrors =
                        csvData.ListingData.Where(l => l.Status == ImportListingStatus.Validation).ToList();
                    if (exceptions.Count + validationErrors.Count > 0 && !validateOnly)
                    {
                        sb.AppendLine();
                        sb.AppendLine(this.AdminResourceString("CSV_FailedImportLinesAttachedInNewCsvFile"));
                        sb.AppendLine();
                    }

                    if (exceptions.Count > 0)
                    {
                        sb.AppendLine(this.AdminResourceString("CSV_Exceptions") + ": ");
                        foreach (ImportListing listing in exceptions)
                        {
                            sb.Append(this.AdminResourceString("CSV_Line") + ": ");
                            sb.Append(listing.Line);
                            sb.Append(", " + this.AdminResourceString("CSV_Title") + ": \"");
                            sb.Append(listing.ColumnData["Title"]);
                            sb.AppendLine("\"");
                            sb.AppendLine("--- " + this.AdminResourceString("CSV_ExceptionsFromListingProvider") + " ---");

                            foreach (string disposition in listing.Disposition)
                            {
                                sb.AppendLine(disposition);
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                    }

                    if (validationErrors.Count > 0)
                    {
                        sb.AppendLine(this.AdminResourceString("CSV_ValidationErrors") + ": ");
                        foreach (ImportListing listing in validationErrors)
                        {
                            sb.Append(this.AdminResourceString("CSV_Line") + ": ");
                            sb.Append(listing.Line);
                            sb.Append(", " + this.AdminResourceString("CSV_Title") + ": \"");
                            sb.Append(listing.ColumnData["Title"]);
                            sb.AppendLine("\"");
                            sb.AppendLine("--- " + this.AdminResourceString("CSV_ValidationErrorsFromListingProvider") + " ---");

                            foreach (string disposition in listing.Disposition)
                            {
                                sb.AppendLine(disposition);
                            }
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                    }

                    //sb.AppendLine("Success:");

                    //foreach (ImportListing listing in p.NewListings.Where(l => l.Status == StatusEnum.Success))
                    //{
                    //    sb.Append("Line: ");
                    //    sb.Append(listing.Line);
                    //    sb.Append(", Title: \"");
                    //    sb.Append(listing.ColumnData["Title"]);
                    //    sb.AppendLine("\"");
                    //}

                    mailMessage.Body = sb.ToString();

                    //generate CSV of failed items:  
                    List<ImportListing> failedImports =
                        csvData.ListingData.Where(l => l.Status != ImportListingStatus.Success).ToList();
                    if (!validateOnly && failedImports.Count > 0)
                    {
                        StringBuilder errorCSV = new StringBuilder();
                        errorCSV.Append(CSV.GetCSVTemplate(csvColumns));
                        errorCSV.AppendLine();

                        foreach (ImportListing listing in failedImports)
                        {
                            foreach (string csvValue in listing.ColumnData.Values)
                            {
                                string value = csvValue;

                                if (value.Contains(",") || value.Contains("\""))
                                {
                                    //value must be quoted
                                    value = value.Replace("\"", "\"\"");
                                    value = "\"" + value + "\"";
                                }

                                errorCSV.Append(value);
                                errorCSV.Append(",");
                            }
                            errorCSV.Remove(errorCSV.Length - 1, 1);
                            errorCSV.AppendLine();
                        }

                        MemoryStream ms = new MemoryStream();
                        StreamWriter sw = new StreamWriter(ms);
                        sw.Write(errorCSV.ToString());
                        sw.Flush();
                        ms.Seek(0, SeekOrigin.Begin);
                        Attachment errorCSVFile = new Attachment(ms, "Errors_" + fileName, "text/csv");
                        mailMessage.Attachments.Add(errorCSVFile);
                    }

                }
                catch (Exception e)
                {
                    mailMessage.Body = this.AdminResourceString("CSV_EmailSendErrorResultsEmail") + ": " + e.Message;
                }

                mailMessage.BodyEncoding = Encoding.UTF8;
                mailMessage.IsBodyHtml = false;

                try
                {
                    var client = new SmtpClient();
                    client.Send(mailMessage);
                    LogManager.WriteLog("CSV " + (validateOnly ? "Validation" : "Import") + " Disposition email sent", "Import CSV", Strings.FunctionalAreas.Site,
                                TraceEventType.Information, null, null, importProperties);
                }
                catch (Exception)
                {
                    //ignore this exception?
                }
            }
            catch (Exception e)
            {
                LogManager.HandleException(e, FunctionalAreas.Site);
                var taskErrorMessage = new MailMessage
                {
                    From = new MailAddress(
                                    SiteClient.Settings[SiteProperties.SystemEmailAddress],
                                    SiteClient.Settings[SiteProperties.SystemEmailName]),
                    Subject = this.AdminResourceString("CSV_ImportExceptionsForXxx", Request.Files[0].FileName)
                };
                taskErrorMessage.To.Add(new MailAddress(resultsEmail, resultsEmail));
                taskErrorMessage.Body = e.Message + " " +
                                        e.StackTrace;
                taskErrorMessage.BodyEncoding = Encoding.UTF8;
                taskErrorMessage.IsBodyHtml = false;

                var taskErrorMessageClient = new SmtpClient();
                taskErrorMessageClient.Send(taskErrorMessage);
                LogManager.WriteLog("CSV " + (validateOnly ? "Validation" : "Import") + " Error email sent", "Import CSV", FunctionalAreas.Site,
                                TraceEventType.Information, null, null, importProperties);
            }
            finally
            {
                KeepAlive.Stop();
            }
        }

        /// <summary>
        /// Displays CSV Import help view
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult ImportCSVHelp()
        {
            //get help
            var columns = CSV.GetColumnSpec(this, User.Identity.Name, Server.MapPath("~"), this.GetCookie(Strings.MVC.CultureCookie), false);
            return View(columns);
        }

        /// <summary>
        /// Processes request for a blank CSV import template which reflects the relevant site properties, custom fields, etc.
        /// </summary>
        /// <returns></returns>
        public FileContentResult GetCSVImportTemplate(bool? draft)
        {
            List<IColumnSpec> csvColumns = CSV.GetColumnSpec(this, User.Identity.Name, Server.MapPath("~"), this.GetCookie(Strings.MVC.CultureCookie), draft.HasValue ? draft.Value : false);

            byte[] buffer = Encoding.UTF8.GetBytes(CSV.GetCSVTemplate(csvColumns));
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.AdminResourceString("CSVImportTemplate_csv");
            return content;
        }

        #endregion

        #region Email Templates

        /// <summary>
        /// Processes a request to enable or disable the specified email template
        /// </summary>
        /// <param name="id">id of the specified email template</param>
        /// <param name="enabled">true: enable the template, false: disable the template</param>
        /// <returns>View(List&lt;EmailTemplate&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult SetEmailTemplateEnabled(int id, bool enabled)
        {
            NotifierClient.SetEmailTemplateEnabled(User.Identity.Name, id, enabled);
            PrepareSuccessMessage("SetEmailTemplateEnabled", MessageType.Method);
            return View("EmailTemplates", NotifierClient.GetAllEmailTemplates(User.Identity.Name));
        }

        /// <summary>
        /// Displays list of available Email Templates
        /// </summary>
        /// <returns>View(List&lt;EmailTemplate&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult EmailTemplates()
        {
            return View(NotifierClient.GetAllEmailTemplates(User.Identity.Name));
        }

        /// <summary>
        /// Displays the editor for the specified email template
        /// </summary>
        /// <param name="template">the name of the specified template</param>
        /// <param name="culture">the culture code of the specified template</param>
        /// <returns>View(EmailTemplateContent)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult EmailTemplateEditor(string template, string culture)
        {
            var templateContent = NotifierClient.GetEmailTemplateContent(User.Identity.Name, template, culture);
            ViewData[Strings.Fields.template] = templateContent.Name;
            ViewData[Strings.Fields.culture] = templateContent.Culture;
            ViewData[Strings.Fields.Subject] = templateContent.Subject;
            ViewData[Strings.Fields.Body] = templateContent.Body;
            return View(templateContent);
        }

        /// <summary>
        /// Displays the editor for the specified email template
        /// </summary>
        /// <param name="template">the name of the specified template</param>
        /// <param name="culture">the culture code of the specified template</param>
        /// <param name="subject">the new subject content for the specified template</param>
        /// <param name="body">the new body content for the specified template</param>
        /// <returns>View(EmailTemplateContent)</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult EmailTemplateEditor(string template, string culture, string subject, string body)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            try
            {
                NotifierClient.SetEmailTemplateContent(User.Identity.Name, template, culture, subject, body);
                PrepareSuccessMessage(Strings.MVC.EmailTemplateEditorAction, MessageType.Method);
                return View(Strings.MVC.EmailTemplatesAction, NotifierClient.GetAllEmailTemplates(User.Identity.Name));
            }
            catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                //TODO mostly deprecated by FunctionResult
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                PrepareErrorMessage(Strings.MVC.EmailTemplateEditorAction, e);
            }
            ViewData[Strings.Fields.template] = template;
            ViewData[Strings.Fields.culture] = culture;
            ViewData[Strings.Fields.Subject] = subject;
            ViewData[Strings.Fields.Body] = body;
            return View(NotifierClient.GetEmailTemplateContent(User.Identity.Name, template, culture));
        }

        /// <summary>
        /// Adds a set of email templates for the specified culture
        /// </summary>
        /// <param name="lang">the culture code of the specified template</param>
        /// <returns>View("EmailTemplates", List&lt;EmailTemplate&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult AddEmailTemplateLanguage(string lang)
        {
            var allTemplateCultures = NotifierClient.GetEmailTemplateCultures();
            string sourceCulture = null;
            if (lang.Length > 2 && allTemplateCultures.Any(c => c == lang.Left(2)))
            {
                sourceCulture = lang.Left(2);
            }
            try
            {
                NotifierClient.CreateEmailTemplateContent(User.Identity.Name, lang, sourceCulture);
                PrepareSuccessMessage("AddEmailTemplateLanguage", MessageType.Method);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("AddEmailTemplateLanguage", e);
            }
            return View("EmailTemplates", NotifierClient.GetAllEmailTemplates(User.Identity.Name));
        }

        /// <summary>
        /// Adds a set of email templates for the specified culture
        /// </summary>
        /// <param name="lang">the culture code of the specified template</param>
        /// <returns>View("EmailTemplates", List&lt;EmailTemplate&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult RemoveEmailTemplateLanguage(string lang)
        {
            try
            {
                NotifierClient.DeleteEmailTemplateContent(User.Identity.Name, lang);
                PrepareSuccessMessage("RemoveEmailTemplateLanguage", MessageType.Method);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("RemoveEmailTemplateLanguage", e);
            }
            return View("EmailTemplates", NotifierClient.GetAllEmailTemplates(User.Identity.Name));
        }

        /// <summary>
        /// Processes request to preview the results of proposed email templates subject and body changes for the specified template
        /// </summary>
        /// <param name="template">the name of the specified template</param>
        /// <param name="culture">the culture code of the specified template</param>
        /// <param name="subject">the new subject content for the specified template</param>
        /// <param name="body">the new body content for the specified template</param>
        /// <returns>View()</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        [ValidateInput(false)]
        public ActionResult PreviewEmailTemplate(string template, string culture, string subject, string body)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            string previewSubj = null;
            string previewBody = null;
            try
            {
                NotifierClient.GenerateNotificationPreview(User.Identity.Name, template, culture, subject, body, out previewSubj, out previewBody);
                //PrepareSuccessMessage("PreviewEmailTemplate", MessageType.Method);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("PreviewEmailTemplate", e);
            }
            ViewData[Strings.Fields.Subject] = previewSubj == null ? this.AdminResourceString("PreviewNotAvailable") : previewSubj;
            ViewData[Strings.Fields.Body] = previewBody == null ? this.AdminResourceString("PreviewNotAvailable") : previewBody;
            return View();
        }

        /// <summary>
        /// Resets all email templates to the default content for all languages
        /// </summary>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult ResetAllEmailTemplates()
        {
            JsonResult result = new JsonResult();
            string errors = string.Empty;
            string errDelim = string.Empty;
            foreach (var emailTemplate in NotifierClient.GetAllEmailTemplates(User.Identity.Name))
            {
                if (emailTemplate.Name == "EmailHeader" || emailTemplate.Name == "EmailFooter")
                    continue;
                foreach (var culture in NotifierClient.GetEmailTemplateCultures())
                {
                    try
                    {
                        string defaultSubject;
                        string defaultBody;
                        NotifierClient.GetDefaultContent(User.Identity.Name, emailTemplate.Name, out defaultSubject, out defaultBody);
                        NotifierClient.SetEmailTemplateContent(User.Identity.Name, emailTemplate.Name, culture, defaultSubject, defaultBody);
                    }
                    catch (Exception e)
                    {
                        errors += errDelim + e.Message;
                    }
                }
            }
            if (!string.IsNullOrEmpty(errors))
            {
                result.Data = new { status = "Error", errors };
            }
            else
            {
                result.Data = new { status = "OK" };
            }
            return result;
        }

        /// <summary>
        /// outputs a plain text file with all current (culture='en') email template content followed by all default email template content
        /// </summary>
        /// <returns>plain text file download</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public FileContentResult AuditEmailTemplates()
        {
            StringBuilder outputData = new StringBuilder();

            var allEmailTemplates = NotifierClient.GetAllEmailTemplates(User.Identity.Name);
            outputData.AppendLine("============= BEGIN CURRENT CONTENT");
            foreach (var emailTemplate in allEmailTemplates)
            {
                var emailContent = NotifierClient.GetEmailTemplateContent(User.Identity.Name, emailTemplate.Name, "en");
                outputData.AppendLine("----------------------------------------------------------");
                outputData.AppendLine(emailContent.Name);
                outputData.AppendLine(emailContent.Subject);
                outputData.AppendLine(emailContent.Body);
            }
            outputData.AppendLine("----------------------------------------------------------");
            outputData.AppendLine("============= END CURRENT CONTENT");
            outputData.AppendLine("============= BEGIN DEFAULT CONTENT");
            foreach (var emailTemplate in allEmailTemplates)
            {
                string defaultSubject;
                string defaultBody;
                NotifierClient.GetDefaultContent(User.Identity.Name, emailTemplate.Name, out defaultSubject, out defaultBody);
                outputData.AppendLine("----------------------------------------------------------");
                outputData.AppendLine(emailTemplate.Name);
                outputData.AppendLine(defaultSubject);
                outputData.AppendLine(defaultBody);
            }
            outputData.AppendLine("----------------------------------------------------------");
            outputData.AppendLine("============= END DEFAULT CONTENT");
            outputData.Append(string.Empty);
            outputData.AppendLine();

            byte[] buffer = Encoding.UTF8.GetBytes(outputData.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/plain");
            content.FileDownloadName = "email_content_audit.txt";
            return content;
        }

        #endregion

        #region DemoHeader

        /// <summary>
        /// Displays the demo header as a separate view, for use with the admin control panel
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult DemoHeader()
        {
            return View();
        }

        #endregion

        #region Data Importing

        /// <summary>
        /// Imports users from an rwAuction Pro 7.0 database
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ImportUsers()
        {
            string[] nonEditableFields = new[] {
                "FirstName", "LastName", "AllowInstantCheckout", "AcceptPayPal",
                "PaymentInstructions", "PayPal_Email", "AcceptCreditCard",
                "DefaultSalesInvoiceComment", "BuyersPremiumPercent", "ManagerName",
                StdUserProps.AuthorizeNet_SellerMerchantLoginID, StdUserProps.AuthorizeNet_SellerTransactionKey,
                StdUserProps.TaxExempt,
                StdUserProps.StripeConnect_SellerAccountConnected, StdUserProps.StripeConnect_SellerUserId,
                StdUserProps.StripeConnect_SellerPublishableApiKey, StdUserProps.StripeConnect_SellerSecretApiKey,
                StdUserProps.CreditCardRequiredExempt
            };

            //Set Default Values
            ViewData["BatchSize"] = "10";
            ViewData["DupUsernameAction"] = "skip";

            //retrieve all non-standard custom user fields
            var allCustomUserFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "Id", false).List
                .Where(cf => !nonEditableFields.Contains(cf.Name)).OrderBy(cf => cf.DisplayOrder).ToList();
            allCustomUserFields.Insert(0, new CustomField() { Name = "DoNotImport", ID = 0 });
            //localize names
            foreach (var cf in allCustomUserFields)
            {
                cf.DisplayName = this.GlobalResourceString(cf.Name);
            }

            //ViewData["AllUserFields"] = allCustomUserFields;

            //set up dropdown for HomePhone
            string defaultHomePhoneValue = allCustomUserFields.Any(cf => cf.Name == "HomePhone") ? "HomePhone" : null;
            ViewData["HomePhoneCustomField"] = new SelectList(allCustomUserFields, Strings.Fields.Name, Strings.Fields.DisplayName, defaultHomePhoneValue);

            //set up dropdown for WorkPhone
            string defaultWorkPhoneValue = allCustomUserFields.Any(cf => cf.Name == "WorkPhone") ? "WorkPhone" : null;
            ViewData["WorkPhoneCustomField"] = new SelectList(allCustomUserFields, Strings.Fields.Name, Strings.Fields.DisplayName, defaultWorkPhoneValue);

            //set up dropdown for Company
            string defaultCompanyValue = allCustomUserFields.Any(cf => cf.Name == "Company") ? "Company" : null;
            ViewData["CompanyCustomField"] = new SelectList(allCustomUserFields, Strings.Fields.Name, Strings.Fields.DisplayName, defaultCompanyValue);

            return View();
        }

        /// <summary>
        /// Gets a count of user that could be imported
        /// </summary>
        /// <returns>the number of rw7 users records found in the specified database</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public string ImportUsers_GetCount()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = "0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select all_users_count = count(u.UserID) from USERS u inner join ACCOUNT a on u.UserID = a.UserID and u.UserName is not null and a.FirstName != 'new'"
                    /*+" where u.userid<3 "*/;
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = ((int)reader["all_users_count"]).ToString();
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Imports a batch of users
        /// </summary>
        /// <param name="DupUsernameAction">how to handle duplicate user names ("skip" or "overwrite")</param>
        /// <param name="BatchSize">the number of users to attempt to import per batch</param>
        /// <param name="BatchIndex">0-based index of the requested page to process</param>
        /// <param name="Verbose">bool indicating whether non-errors message should be generated</param>
        /// <param name="HomePhoneCustomField">Custom User Field Name to store imported home phone, or "DoNotImport" to skip</param>
        /// <param name="WorkPhoneCustomField">Custom User Field Name to store imported work phone, or "DoNotImport" to skip</param>
        /// <param name="CompanyCustomField">Custom User Field Name to store imported company, or "DoNotImport" to skip</param>
        /// <returns>
        ///     a string in the following format: "x{dat}y{dat}z{dat}error_msg_1{err}error_msg_2{err}error_msg_n"
        ///     where x = # users added in this batch, y = # of users skipped in this batch, z = # of users updated in this batch
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<string> ImportUsers_ImportBatch(string DupUsernameAction, int BatchSize, int BatchIndex, bool? Verbose,
            string HomePhoneCustomField, string WorkPhoneCustomField, string CompanyCustomField)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            bool skipDuplicateUsernames = !DupUsernameAction.Equals("overwrite", StringComparison.OrdinalIgnoreCase);

            bool showInformationMessages = Verbose ?? false;

            string errors = string.Empty;

            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

            int usersAdded = 0;
            int usersSkipped = 0;
            int usersUpdated = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                if (BatchSize < 1) BatchSize = 1; // values less than 1 are invalid
                int firstRow = (BatchSize * BatchIndex) + 1;
                int lastRow = BatchSize * (BatchIndex + 1);
                command.CommandText =
                      "select * from ( "
                    + " select rowNum = row_number() over (order by u.UserId) "
                    + " , u.* "
                    + " ,a.Address1 Billing_Address1 "
                    + " ,a.Address2 Billing_Address2 "
                    + " ,a.City Billing_City "
                    + " ,a.State Billing_State "
                    + " ,a.Zip Billing_Zip "
                    + " ,a.Country Billing_Country "
                    + " from USERS u inner join ACCOUNT a on u.UserID = a.UserID and u.UserName is not null and a.FirstName != 'new') as NumberedResults "
                    + " where (rowNum between " + firstRow + " and " + lastRow + ") "
                    /*+ " and userid<3 "*/;
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                string username;
                UserInput input;
                UserInput input2;
                UserInput input3;
                while (reader.Read())
                {
                    username = (string)reader["UserName"];
                    input = new UserInput(this.User.Identity.Name, username, "en", "en");
                    input2 = new UserInput(this.User.Identity.Name, username, "en", "en");
                    input3 = new UserInput(this.User.Identity.Name, username, "en", "en");
                    try
                    {
                        var rwaUserId = SqlToInt(reader["UserID"]);
                        var city = SqlToString(reader["City"]);
                        var email = SqlToString(reader["EmailAddress"]);
                        var passWord = SqlToString(reader["Pword"]);
                        var countryCode = SqlToString(reader["Country"]);

                        //auto correct invalid country codes
                        if (countryCode == "USA") countryCode = "US";
                        if (countryCode == "CANADA") countryCode = "CA";

                        var countryId = GetCountryIdByCountryCode(countryCode);
                        if (countryId == 0 && SiteClient.BoolSetting(SiteProperties.RequireAddressOnRegistration))
                            throw new Exception(this.ResourceString("Validation,UnknownCountryX", countryCode));
                        var firstName = SqlToString(reader["FirstName"]);
                        var lastIp = SqlToString(reader["LastIP"]);
                        if (string.IsNullOrEmpty(lastIp)) lastIp = "0.0.0.0";
                        var lastName = SqlToString(reader["LastName"]);
                        var newsletter = (SqlToInt(reader["Block_MassMail"]) == 0);
                        var state = SqlToString(reader["State"]);
                        var street1 = SqlToString(reader["Address1"]);
                        var street2 = SqlToString(reader["Address2"]);
                        var zip = SqlToString(reader["Zip"]);
                        var payPalEmail = SqlToString(reader["PayPalEmail"]);
                        bool allowInstantCheckout = false;
                        try
                        {
                            allowInstantCheckout = (SqlToInt(reader["ImmediatePayment"]) != 2) &&
                                                   !string.IsNullOrEmpty(payPalEmail);
                        }
                        catch
                        {
                            //ignore this error -- RWA 5.0?
                        }
                        var isRestricted = (SqlToInt(reader["IsRestricted"]) != 0);
                        var isApproved = !SiteClient.BoolSetting(SiteProperties.UserApprovalRequired); // ((Int16) reader["SellerAccessRequested"] == 1 && (Int16) reader["IsSeller"] == 0);
                        var isVerified = (SqlToInt(reader["IsVerified"]) != 0);
                        var isAdmin = (SqlToInt(reader["IsAdmin"]) != 0);

                        var Billing_Street1 = SqlToString(reader["Billing_Address1"]);
                        var Billing_Street2 = SqlToString(reader["Billing_Address2"]);
                        var Billing_City = SqlToString(reader["Billing_City"]);
                        var Billing_State = SqlToString(reader["Billing_State"]);
                        var Billing_Zip = SqlToString(reader["Billing_Zip"]);
                        var Billing_CountryCode = SqlToString(reader["Billing_Country"]);

                        //auto correct invalid country codes
                        if (Billing_CountryCode == "USA") Billing_CountryCode = "US";
                        if (Billing_CountryCode == "CANADA") Billing_CountryCode = "CA";

                        var BillingCountryId = GetCountryIdByCountryCode(Billing_CountryCode);
                        if (BillingCountryId == 0 && SiteClient.BoolSetting(SiteProperties.RequireAddressOnRegistration))
                            throw new Exception(this.ResourceString("Validation,UnknownBillingCountryX", Billing_CountryCode));

                        var homePhone = SqlToString(reader["HomePhone"]);
                        var workPhone = SqlToString(reader["WorkPhone"]);
                        var companyName = SqlToString(reader["Company"]);

                        var existingUser = UserClient.GetUserByUserName(this.User.Identity.Name, username);

                        if (existingUser == null)
                        { // new user
                            input.Items.Add("agreements", true.ToString());
                            input.Items.Add("City", city);
                            input.Items.Add("confirmEmail", email);
                            //input.Items.Add("confirmPassword", passWord);

                            input.Items.Add("Country", countryId.ToString());
                            input.Items.Add("Email", email);
                            input.Items.Add("FirstName", firstName);
                            input.Items.Add("LastIP", lastIp);
                            input.Items.Add("LastName", lastName);
                            input.Items.Add("Newsletter", newsletter.ToString());
                            //input.Items.Add("Password", passWord);
                            input.Items.Add("StateRegion", state);
                            input.Items.Add("Street1", street1);
                            input.Items.Add("Street2", street2);
                            input.Items.Add("UserName", username);
                            input.Items.Add("ZipPostal", zip);

                            if (!string.IsNullOrEmpty(HomePhoneCustomField) && HomePhoneCustomField != "DoNotImport")
                            {
                                input.Items.Add(HomePhoneCustomField, homePhone);
                            }
                            if (!string.IsNullOrEmpty(WorkPhoneCustomField) && WorkPhoneCustomField != "DoNotImport")
                            {
                                input.Items.Add(WorkPhoneCustomField, workPhone);
                            }
                            if (!string.IsNullOrEmpty(CompanyCustomField) && CompanyCustomField != "DoNotImport")
                            {
                                input.Items.Add(CompanyCustomField, companyName);
                            }

                            input2.Items.Add("AcceptPayPal", (!string.IsNullOrEmpty(payPalEmail)).ToString());
                            input2.Items.Add("AllowInstantCheckout", allowInstantCheckout.ToString());
                            input2.Items.Add("Comment", "Imported " + TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, siteTimeZone));
                            //input2.Items.Add("confirmPassword", passWord);
                            input2.Items.Add("Email", email);
                            input2.Items.Add("FirstName", firstName);

                            input2.Items.Add("IsApproved", isApproved.ToString());
                            input2.Items.Add("IsLockedOut", isRestricted.ToString());
                            input2.Items.Add("IsVerified", isVerified.ToString());
                            input2.Items.Add("LastName", lastName);
                            input2.Items.Add("Newsletter", newsletter.ToString());
                            //input2.Items.Add("Password", passWord);
                            //input2.Items.Add("PaymentInstructions", xxxx);
                            input2.Items.Add("PayPal_Email", payPalEmail ?? string.Empty);
                            input2.Items.Add("UserName", username);
                            if (isAdmin)
                                input2.Items.Add("Role_Admin", true.ToString());

                            UserClient.RegisterUser(this.User.Identity.Name, input);
                            var newUser = UserClient.GetUserByUserName(this.User.Identity.Name, username);

                            await UserManager.AddPasswordAsync(newUser.ID, passWord);

                            input2.Items.Add("Id", newUser.ID.ToString());

                            var userProperties = UserClient.Properties(this.User.Identity.Name, username);
                            ValidateUserPropertyValues(userProperties, input);

                            UserClient.UpdateAllUserDetails(this.User.Identity.Name, input2);

                            //billing address, if applicable

                            if (
                                !string.IsNullOrWhiteSpace(Billing_Street1) &&
                                !string.IsNullOrWhiteSpace(Billing_City) &&
                                !string.IsNullOrWhiteSpace(Billing_State) &&
                                !string.IsNullOrWhiteSpace(Billing_Zip) &&
                                !string.IsNullOrWhiteSpace(Billing_CountryCode) &&
                                (countryCode != Billing_CountryCode ||
                                street1 != Billing_Street1 ||
                                street2 != Billing_Street2 ||
                                city != Billing_City ||
                                state != Billing_State ||
                                zip != Billing_Zip))
                            {
                                //billing address is different from primary address, so add it now
                                input3.Items.Add("City", Billing_City);
                                input3.Items.Add("Country", BillingCountryId.ToString());
                                input3.Items.Add("FirstName", firstName);
                                input3.Items.Add("LastName", lastName);
                                input3.Items.Add("StateRegion", Billing_State);
                                input3.Items.Add("Street1", Billing_Street1);
                                input3.Items.Add("Street2", Billing_Street2);
                                input3.Items.Add("ZipPostal", Billing_Zip);
                                input3.Items.Add("Description", string.Empty);
                                int newAddressId = UserClient.UpdateAddress(User.Identity.Name, input3);
                            }

                            //used to import feedback later
                            ImportUsers_MapImportedUser(newUser.ID, rwaUserId, newUser.UserName);

                            usersAdded++;
                        }
                        else if (!skipDuplicateUsernames)
                        { // update user info
                            UserClient.Properties(Strings.SystemActors.SystemUserName, username); //necessary to force the system to add any missing custom properties created after the user was created
                            input.Items.Add("AcceptPayPal", (!string.IsNullOrEmpty(payPalEmail)).ToString());
                            input.Items.Add("AllowInstantCheckout", allowInstantCheckout.ToString());
                            input.Items.Add("Comment", "Imported " + TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, siteTimeZone));
                            //input.Items.Add("confirmPassword", passWord);

                            input.Items.Add("Email", email);
                            input.Items.Add("FirstName", firstName);
                            input.Items.Add("Id", existingUser.ID.ToString());
                            input.Items.Add("IsApproved", isApproved.ToString());
                            input.Items.Add("IsLockedOut", isRestricted.ToString());
                            input.Items.Add("IsVerified", isVerified.ToString());
                            input.Items.Add("LastName", lastName);
                            input.Items.Add("Newsletter", newsletter.ToString());
                            //input.Items.Add("Password", passWord);
                            //input.Items.Add("PaymentInstructions", xxxx);
                            input.Items.Add("PayPal_Email", payPalEmail ?? string.Empty);
                            input.Items.Add("UserName", username);
                            if (isAdmin)
                                input.Items.Add("Role_Admin", true.ToString());

                            if (!string.IsNullOrEmpty(HomePhoneCustomField) && HomePhoneCustomField != "DoNotImport")
                            {
                                input.Items.Add(HomePhoneCustomField, homePhone);
                            }
                            if (!string.IsNullOrEmpty(WorkPhoneCustomField) && WorkPhoneCustomField != "DoNotImport")
                            {
                                input.Items.Add(WorkPhoneCustomField, workPhone);
                            }
                            if (!string.IsNullOrEmpty(CompanyCustomField) && CompanyCustomField != "DoNotImport")
                            {
                                input.Items.Add(CompanyCustomField, companyName);
                            }

                            if (existingUser.PrimaryAddressID.HasValue)
                            {
                                input2.Items.Add("Address", existingUser.PrimaryAddressID.Value.ToString());
                            }
                            input2.Items.Add("City", city);
                            input2.Items.Add("Country", countryId.ToString());
                            input2.Items.Add("FirstName", firstName);
                            input2.Items.Add("LastName", lastName);
                            input2.Items.Add("StateRegion", state);
                            input2.Items.Add("Street1", street1);
                            input2.Items.Add("Street2", street2);
                            input2.Items.Add("ZipPostal", zip);
                            input2.Items.Add("Description", string.Empty);

                            var userProperties = UserClient.Properties(this.User.Identity.Name, username);
                            ValidateUserPropertyValues(userProperties, input);

                            UserClient.UpdateAllUserDetails(this.User.Identity.Name, input);
                            UserClient.UpdateAddress(this.User.Identity.Name, input2);

                            if (countryCode != Billing_CountryCode ||
                                street1 != Billing_Street1 ||
                                street2 != Billing_Street2 ||
                                city != Billing_City ||
                                state != Billing_State ||
                                zip != Billing_Zip)
                            {
                                //billing address is different from primary address, so add it now
                                input3.Items.Add("City", Billing_City);
                                input3.Items.Add("Country", BillingCountryId.ToString());
                                input3.Items.Add("FirstName", firstName);
                                input3.Items.Add("LastName", lastName);
                                input3.Items.Add("StateRegion", Billing_State);
                                input3.Items.Add("Street1", Billing_Street1);
                                input3.Items.Add("Street2", Billing_Street2);
                                input3.Items.Add("ZipPostal", Billing_Zip);
                                input3.Items.Add("Description", string.Empty);
                                int newAddressId = UserClient.UpdateAddress(User.Identity.Name, input3);
                            }

                            if (showInformationMessages)
                            {
                                errors += (!string.IsNullOrEmpty(errors) ? "{err}" : string.Empty) + "     '" + username.Replace("{err}", string.Empty) + "': Updated Existing User";
                            }

                            ImportUsers_MapImportedUser(existingUser.ID, rwaUserId, existingUser.UserName);

                            usersUpdated++;
                        }
                        else
                        { // skip this user
                            if (showInformationMessages)
                            {
                                errors += (!string.IsNullOrEmpty(errors) ? "{err}" : string.Empty) + "     '" + username.Replace("{err}", string.Empty) + "': Skipped Duplicate User";
                            }

                            ImportUsers_MapImportedUser(existingUser.ID, rwaUserId, existingUser.UserName);

                            usersSkipped++;
                        }
                    }
                    catch (FaultException<ValidationFaultContract> vfc)
                    {
                        foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                        {
                            errors += (!string.IsNullOrEmpty(errors) ? "{err}" : string.Empty);
                            errors += "'" + username.Replace("{err}", string.Empty) + "': ";
                            errors += this.ResourceString("Validation," + issue.Message).Replace("{err}", string.Empty);
                            if (issue.Key != "UserName" && !issue.Key.Contains("Password"))
                            {
                                if (input.Items.ContainsKey(issue.Key))
                                {
                                    errors += " ('" + input.Items[issue.Key] + "')";
                                }
                                else if (input2.Items.ContainsKey((issue.Key)))
                                {
                                    errors += " ('" + input2.Items[issue.Key] + "')";
                                }
                            }
                        }
                        usersSkipped++;
                    }
                    catch (Exception e)
                    {
                        errors += (!string.IsNullOrEmpty(errors)) ? "{err}" : string.Empty;
                        errors += "'" + username.Replace("{err}", string.Empty) + "': ";
                        errors += e.Message.Replace("{err}", string.Empty);
                        usersSkipped++;
                    }
                }
                connection.Close();
            }
            return usersAdded + "{dat}" + usersUpdated + "{dat}" + usersSkipped + "{dat}" + errors.Replace("{dat}", string.Empty);
        }

        /// <summary>
        /// Gets the AWE user ID mapped to the specified RWA user ID
        /// </summary>
        /// <param name="rwaUserId">the specified RWA user ID</param>
        /// <param name="username">returns the associated username</param>
        /// <returns>the AWE user ID</returns>
        private int ImportUsers_GetLocalUserId(int rwaUserId, out string username)
        {
            int retVal = 0;
            //private Dictionary<int, int> _importedUIDs = new Dictionary<int, int>();
            //private Dictionary<int, string> _importedUNs = new Dictionary<int, string>();
            if (_importedUIDs.ContainsKey(rwaUserId))
            {
                retVal = _importedUIDs[rwaUserId];
                username = _importedUNs[rwaUserId];
                return retVal;
            }

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            username = string.Empty;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("select localUserId, importedUsername from RWX_ImportedUserMap where foreignUserId = {0}", rwaUserId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = SqlToInt(reader["localUserId"]);
                    username = SqlToString(reader["importedUsername"]);
                }
                connection.Close();
            }
            _importedUIDs.Add(rwaUserId, retVal);
            _importedUNs.Add(rwaUserId, username);
            return retVal;
        }

        /// <summary>
        /// Inserts a record mapping the specified AWE user ID to the specified RWA user ID
        /// </summary>
        /// <param name="aweUserId">the specified AWE user ID</param>
        /// <param name="rwaUserId">the specified RWA user ID</param>
        /// <param name="username"></param>
        /// <returns>the AWE user ID</returns>
        private void ImportUsers_MapImportedUser(int aweUserId, int rwaUserId, string username)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format(
                    " if not exists (select * from RWX_ImportedUserMap where foreignUserId={1}) " +
                    " insert RWX_ImportedUserMap (localUserId, foreignUserId, importedUsername) values ({0}, {1}, N'{2}') " +
                    " else " +
                    " update RWX_ImportedUserMap set localUserId={0},importedUsername=N'{2}' where foreignUserId={1} ", 
                    aweUserId, rwaUserId, username.Replace("'", "''"));
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
                connection.Close();
            }
        }
        
        /// <summary>
        /// retrieves the integer id for the specified country code, if it exists
        /// </summary>
        /// <param name="code">the specified country code</param>
        /// <returns>0 if not found</returns>
        private int GetCountryIdByCountryCode(string code)
        {
            if (code.Contains("'")) return 0;
            if (string.IsNullOrWhiteSpace(code)) return 0;

            //known common country code aliases
            if (code.Equals("USA", StringComparison.OrdinalIgnoreCase)) code = "US";
            if (code.Equals("UK", StringComparison.OrdinalIgnoreCase)) code = "GB";

            int retVal = 0;
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select Id from RWX_Countries where Code = N'" +
                                      (code.Equals("USA", StringComparison.OrdinalIgnoreCase) ? "US" : code) + "'";
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    retVal = (int)reader["Id"];
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// returns null if not a valid string
        /// </summary>
        private string SqlToString(object value)
        {
            try
            {
                return (string)value;
            }
            catch
            {
            }
            return string.Empty;
        }

        /// <summary>
        /// returns 0 if not a valid int
        /// </summary>
        private int SqlToInt(object value)
        {
            try
            {
                return (int)value;
            }
            catch
            {
                try
                {
                    return int.Parse(((Int16)value).ToString());
                }
                catch
                {
                }
            }
            return 0;
        }

        /// <summary>
        /// returns 0 if not a valid decimal
        /// </summary>
        private decimal SqlToDecimal(object value)
        {
            try
            {
                return (decimal)value;
            }
            catch
            {
                try
                {
                    return decimal.Parse(((Decimal)value).ToString());
                }
                catch
                {
                }
            }
            return 0.0M;
        }

        /// <summary>
        /// returns DateTime.MinValue if not a valid date
        /// </summary>
        private DateTime SqlToDateTime(object value)
        {
            try
            {
                return (DateTime)value;
            }
            catch
            {
                try
                {
                    return DateTime.Parse(((string)value).ToString());
                }
                catch
                {
                }
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Imports categories from an rwAuction Pro 7.0 database
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ImportCategories()
        {
            //Set Default Values
            ViewData["BatchSize"] = "25";

            return View();
        }

        /// <summary>
        /// Gets a count of categories that could be imported
        /// </summary>
        /// <returns>the number of rw7 category records found in the specified database</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public string ImportCategories_GetCount()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = "0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select all_cats_count = count(*) from category";
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = ((int)reader["all_cats_count"]).ToString();
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Gets the AWE category ID mapped to the specified RWA category ID
        /// </summary>
        /// <param name="rwaCatId">the specified RWA category ID</param>
        /// <returns>the AWE category ID</returns>
        private int ImportCategories_GetLocalCatId(int rwaCatId)
        {
            if (rwaCatId == 0) return 9; // always map 0 (RWA root) to 9 (AWE root)

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            int retVal = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("select localCategoryId from RWX_ImportedCategoryMap where foreignCategoryId = {0}", rwaCatId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = SqlToInt(reader["localCategoryId"]);
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Inserts a record mapping the specified AWE category ID to the specified RWA category ID
        /// </summary>
        /// <param name="aweCatId">the specified AWE category ID</param>
        /// <param name="rwaCatId">the specified RWA category ID</param>
        /// <returns>the AWE category ID</returns>
        private void ImportCategories_MapImportedCategory(int aweCatId, int rwaCatId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("insert RWX_ImportedCategoryMap (localCategoryId, foreignCategoryId) values ({0}, {1})", aweCatId, rwaCatId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        /// <summary>
        /// Imports a batch of categories
        /// </summary>
        /// <param name="BatchSize">the number of categories to attempt to import per batch</param>
        /// <param name="BatchIndex">0-based index of the requested page to process</param>
        /// <returns>
        ///     JSON encoded result { { addedCount = x }, { skippedCount = y }, { errors = string[] } }
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult ImportCategories_ImportBatch(int BatchSize, int BatchIndex)
        {
            JsonResult result = new JsonResult();

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            var errorMessages = new List<string>();

            int catsAdded = 0;
            int catsSkipped = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                if (BatchSize < 1) BatchSize = 1; // values less than 1 are invalid
                int firstRow = (BatchSize * BatchIndex) + 1;
                int lastRow = BatchSize * (BatchIndex + 1);
                command.CommandText =
                    " with all_cats (CatID, ParentID, Name, DisplaySequence, DisplayOrder) as ( " +
                    "  SELECT c.CatID,  c.ParentID, c.Name, c.DisplaySequence, DisplayOrder=LEFT( " +
                    "  ( ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), p5.displaysequence + 100), 10), '') " +
                    "  + ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), p4.displaysequence + 100), 10), '') " +
                    "  + ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), p3.displaysequence + 100), 10), '') " +
                    "  + ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), p2.displaysequence + 100), 10), '') " +
                    "  + ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), p1.displaysequence + 100), 10), '') " +
                    "  + ISNULL(RIGHT('0000000000' + CONVERT(varchar(10), c.displaysequence + 100), 10), '') " +
                    "  + '0000000000' + '0000000000' + '0000000000' + '0000000000' + '0000000000' + '0000000000' ) " +
                    " , 60) " +
                    "  FROM category c " +
                    "   LEFT JOIN category p1 ON c.parentid=p1.catid " +
                    "   LEFT JOIN category p2 ON c.parentid2=p2.catid " +
                    "   LEFT JOIN category p3 ON c.parentid3=p3.catid " +
                    "   LEFT JOIN category p4 ON c.parentid4=p4.catid " +
                    "   LEFT JOIN category p5 ON c.parentid5=p5.catid " +
                    " )" +
                    " select * from  ( " +
                    "   select rowNum = row_number() over (order by DisplayOrder), CatID, ParentID, Name, DisplaySequence from all_cats " +
                    string.Format(" ) as resultSet where (rowNum between {0} and {1}) order by rowNum ", firstRow, lastRow);

                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    int rwaCatId = SqlToInt(reader["CatID"]);
                    int existingCatId = ImportCategories_GetLocalCatId(rwaCatId);
                    if (existingCatId == 0)
                    {
                        int rwaParentId = SqlToInt(reader["ParentID"]);
                        string rwaCatName = SqlToString(reader["Name"]);
                        int rwaDisplaySequence = SqlToInt(reader["DisplaySequence"]);

                        int aweParentId = ImportCategories_GetLocalCatId(rwaParentId);

                        Category category = new Category();
                        category.ParentCategoryID = aweParentId;
                        category.MVCAction = string.Empty;
                        category.Type = Strings.CategoryTypes.Item;
                        category.Name = rwaCatName;
                        category.DisplayOrder = rwaDisplaySequence;
                        try
                        {
                            if (category.ParentCategoryID == 0)
                                throw new Exception(string.Format("Skipped -- Parent Category ID {0} was not imported", rwaParentId));
                            CommonClient.AddChildCategory(User.Identity.Name, category);
                            ImportCategories_MapImportedCategory(category.ID, rwaCatId);
                            catsAdded++;
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1})]: {2}", rwaCatName, rwaCatId, this.AdminResourceString(iofc.Detail.Reason.ToString())));
                        }
                        catch(Exception e)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1})]: {2}", rwaCatName, rwaCatId, e.Message));
                        }
                    }
                    else
                    {
                        catsSkipped++;
                    }
                }
                connection.Close();
            }

            result.Data = new
            {
                addedCount = catsAdded,
                skippedCount = catsSkipped,
                errors = errorMessages.ToArray()
            };
            return result;
        }

        /// <summary>
        /// Imports listings from an rwAuction Pro 7.0 database
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ImportListings()
        {
            //Set Default Values
            ViewData["BatchSize"] = "10";

            //retrieve all non-standard custom item fields
            var allCustomItemFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.Item, 0, 0, "Id", false).List
                .OrderBy(cf => cf.DisplayOrder).ToList();
            allCustomItemFields.Insert(0, new CustomField() { Name = "DoNotImport", ID = 0 });
            //localize names
            foreach (var cf in allCustomItemFields)
            {
                cf.DisplayName = this.GlobalResourceString(cf.Name);
            }

            var allRwaItemFields = ImportListings_GetRwaFields();
            ViewData["AllRwaItemFields"] = allRwaItemFields;

            //set up dropdowns for custom field mappings
            foreach (var rwaField in allRwaItemFields)
            {
                string defaultValue = allCustomItemFields.Any(cf => cf.Name == rwaField.FieldSymbol) ? rwaField.FieldSymbol : null;
                ViewData["CFIMPORT_" + rwaField.FieldID] = new SelectList(allCustomItemFields, Strings.Fields.Name, Strings.Fields.DisplayName, defaultValue);
            }

            return View();
        }

        /// <summary>
        /// Gets a list of all RWA custom item fields
        /// </summary>
        private List<RwaCustomField> ImportListings_GetRwaFields()
        {
            List<RwaCustomField> retVal = (List<RwaCustomField>)SiteClient.GetCacheData("ListingImportRwaFields");
            if (retVal == null)
            {
                string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

                retVal = new List<RwaCustomField>();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.CommandText =
                        " with all_rwa_fields as ( " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=1 from CUSTOM_FIELDS_TXT where ClassID=1 and DropDown=0 " +
                        " union all " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=2 from CUSTOM_FIELDS_INT where ClassID=1 " +
                        " union all " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=3 from CUSTOM_FIELDS_BOOL where ClassID=1 " +
                        " union all " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=4 from CUSTOM_FIELDS_CUR where ClassID=1 " +
                        " union all " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=5 from CUSTOM_FIELDS_DTTM where ClassID=1 " +
                        " union all " +
                        " select FieldID, FieldSymbol, FieldTitle, DisplaySequence, AweFieldType=6 from CUSTOM_FIELDS_TXT where ClassID=1 and DropDown=1 " +
                        " ) select * from all_rwa_fields order by DisplaySequence ";
                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                    while (reader.Read())
                    {
                        retVal.Add(new RwaCustomField()
                        {
                            FieldID = SqlToInt(reader["FieldID"]),
                            FieldSymbol = SqlToString(reader["FieldSymbol"]),
                            FieldTitle = SqlToString(reader["FieldTitle"]),
                            DisplaySequence = SqlToInt(reader["DisplaySequence"]),
                            AweFieldType = (CustomFieldType)SqlToInt(reader["AweFieldType"])
                        });
                    }
                    connection.Close();
                }

                SiteClient.SetCacheData("ListingImportRwaFields", retVal, 15); // cache this data for 15 minutes
            }
            return retVal;
        }

        /// <summary>
        /// Gets a list of all custom field values for the specified RWA listing ID
        /// </summary>
        /// <param name="rwaListingId">the specified RWA listing ID</param>
        private Dictionary<string,string> ImportListings_GetRwaCustomFieldValues(int rwaListingId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            var retVal = new Dictionary<string, string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText =
                    string.Format(" select cf.FieldID, cf.FieldSymbol, FieldValue = CONVERT(nvarchar(max), cv.FieldValue) " +
                    " from CUSTOM_VALUES_TXT cv inner join CUSTOM_FIELDS_TXT cf on cf.FieldSymbol = cv.FieldSymbol " +
                    " where cf.ClassID=1 and cv.ObjectID={0} " +
                    " 	union all " +
                    " select cf.FieldID, cf.FieldSymbol, FieldValue = CONVERT(nvarchar(max), cv.FieldValue) " +
                    " from CUSTOM_VALUES_INT cv inner join CUSTOM_FIELDS_INT cf on cf.FieldSymbol = cv.FieldSymbol " +
                    " where cf.ClassID=1 and cv.ObjectID={0} " +
                    " 	union all " +
                    " select cf.FieldID, cf.FieldSymbol, FieldValue = CONVERT(nvarchar(max), case cv.FieldValue when 1 then N'true' else N'false' end) " +
                    " from CUSTOM_VALUES_BOOL cv inner join CUSTOM_FIELDS_BOOL cf on cf.FieldSymbol = cv.FieldSymbol " +
                    " where cf.ClassID=1 and cv.ObjectID={0} " +
                    " 	union all " +
                    " select cf.FieldID, cf.FieldSymbol, FieldValue = CONVERT(nvarchar(max), cv.FieldValue) " +
                    " from CUSTOM_VALUES_CUR cv inner join CUSTOM_FIELDS_CUR cf on cf.FieldSymbol = cv.FieldSymbol " +
                    " where cf.ClassID=1 and cv.ObjectID={0} " +
                    " 	union all " +
                    " select cf.FieldID, cf.FieldSymbol, FieldValue = CONVERT(nvarchar(max), cv.FieldValue) " +
                    " from CUSTOM_VALUES_DTTM cv inner join CUSTOM_FIELDS_DTTM cf on cf.FieldSymbol = cv.FieldSymbol " +
                    " where cf.ClassID=1 and cv.ObjectID={0} ", rwaListingId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal.Add(string.Format("CFIMPORT_{0}", SqlToInt(reader["FieldID"])), SqlToString(reader["FieldValue"]));
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Gets a count of listings that could be imported
        /// </summary>
        /// <returns>the number of rw7 listing records found in the specified database</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public string ImportListings_GetCount()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = "0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select all_listings_count = count(*) from ITEM where IsClosed = 0 and BidCount = 0 ";
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = ((int)reader["all_listings_count"]).ToString();
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Imports a batch of listings
        /// </summary>
        /// <param name="BatchSize">the number of listings to attempt to import per batch</param>
        /// <param name="BatchIndex">0-based index of the requested page to process</param>
        /// <param name="imageImportUriBase">the uri to the image import location</param>
        /// <returns>
        ///     JSON encoded result { { addedCount = x }, { skippedCount = y }, { errors = string[] } }
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult ImportListings_ImportBatch(int BatchSize, int BatchIndex, string imageImportUriBase)
        {
            JsonResult result = new JsonResult();

            //custom field dictionary
            var customFieldMap = new Dictionary<string, string>();
            foreach(string key in Request.Form.AllKeys.Where(k => k != null && k.StartsWith("CFIMPORT_")))
            {
                customFieldMap.Add(key, (string)Request[key]);
            }

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            var errorMessages = new List<string>();

            int addedCount = 0;
            int skippedCount = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                if (BatchSize < 1) BatchSize = 1; // values less than 1 are invalid
                int firstRow = (BatchSize * BatchIndex) + 1;
                int lastRow = BatchSize * (BatchIndex + 1);

                command.CommandText =
                    " select * from  ( " +
                    "   select rowNum = row_number() over (order by ItemID), SellerUN = u.Username, i.* from ITEM i inner join USERS u on i.SellerID = u.UserID " +
                    "   where i.IsClosed = 0 and BidCount = 0 " +
                    string.Format(" ) as resultSet where (rowNum between {0} and {1}) order by rowNum ", firstRow, lastRow);

                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    int rwaListingId = SqlToInt(reader["ItemID"]);
                    int existingListingId = ImportListings_GetLocalListingId(rwaListingId);
                    if (existingListingId == 0)
                    {
                        string sellerUN = SqlToString(reader["SellerUN"]);
                        string title = SqlToString(reader["Name"]);
                        string description = SqlToString(reader["Description"]);
                        decimal price = SqlToDecimal(reader["Price"]);
                        decimal reserve = SqlToDecimal(reader["Reserve"]);
                        DateTime startDTTM = SqlToDateTime(reader["StartDate"]);
                        DateTime endDTTM = SqlToDateTime(reader["EndDate"]);
                        string itemLocation = SqlToString(reader["ItemLocation"]);
                        int quantity = SqlToInt(reader["Quantity"]);
                        bool homepageFeatured = (SqlToInt(reader["HomePageFeatured"]) == 1);
                        bool highlightListing = (SqlToInt(reader["HighlightListing"]) == 1);
                        bool boldListing = (SqlToInt(reader["BoldListing"]) == 1);
                        decimal fixedPrice = SqlToDecimal(reader["BuyItNowPrice"]);
                        int relist = SqlToInt(reader["ReList"]);
                        bool isFixedPrice = (SqlToInt(reader["FixedPrice"]) == 1);
                        bool isClassified = (SqlToInt(reader["Classified"]) == 1);
                        string currencyCode = SqlToString(reader["PreferredCurrency"]);
                        int primaryRwaCategoryID = SqlToInt(reader["CatID"]);
                        int region1 = SqlToInt(reader["Region1"]);
                        int region2 = SqlToInt(reader["Region2"]);
                        int region3 = SqlToInt(reader["Region3"]);
                        int region4 = SqlToInt(reader["Region4"]);
                        bool isListingTemplate = (SqlToInt(reader["isActive"]) == 0);

                        UserInput input = new UserInput(User.Identity.Name, sellerUN, "en", "en");
                        var cultureInfo = CultureInfo.GetCultureInfo("en");

                        input.Items.Add(Strings.Fields.Price, price.ToString(cultureInfo));
                        input.Items.Add(Strings.Fields.Title, title);
                        input.Items.Add(Strings.Fields.Subtitle, string.Empty);
                        input.Items.Add(Strings.Fields.Description, description);
                        input.Items.Add(Strings.Fields.AutoRelist, relist.ToString());
                        input.Items.Add(Strings.Fields.Currency, currencyCode);

                        input.Items.Add(Strings.Fields.Quantity, quantity.ToString());

                        //start/end dttm
                        TimeZoneInfo sourceTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // this must be the timezone that the source database/server uses
                        TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.TextSetting(Strings.SiteProperties.SiteTimeZone));
                        input.Items.Add(Strings.Fields.DurOpsOverride, Strings.DurationOptions.StartEnd); // note: this only works if the acting user is an admin

                        try
                        {
                            input.Items.Add(Strings.Fields.StartDate, TimeZoneInfo.ConvertTime(startDTTM, sourceTimeZone, siteTimeZone).ToShortDateString());
                            input.Items.Add(Strings.Fields.StartTime, TimeZoneInfo.ConvertTime(startDTTM, sourceTimeZone, siteTimeZone).ToShortTimeString());
                            input.Items.Add(Strings.Fields.EndDate, TimeZoneInfo.ConvertTime(endDTTM, sourceTimeZone, siteTimeZone).ToShortDateString());
                            input.Items.Add(Strings.Fields.EndTime, TimeZoneInfo.ConvertTime(endDTTM, sourceTimeZone, siteTimeZone).ToShortTimeString());
                        }
                        catch (Exception e)
                        {
                            LogManager.WriteLog(null, "AdminController.ImportListings_ImportBatch > TimeZoneInfo.ConvertTime()", "Listing Imports", TraceEventType.Warning, input.ActingUserName, e);
                            input.Items[Strings.Fields.StartDate] = startDTTM.ToShortDateString();
                            input.Items[Strings.Fields.StartTime] =startDTTM.ToShortTimeString();
                            input.Items[Strings.Fields.EndDate] = endDTTM.ToShortDateString();
                            input.Items[Strings.Fields.EndTime] = endDTTM.ToShortTimeString();
                        }

                        if ((isFixedPrice || isClassified) && (endDTTM - DateTime.Now).TotalDays > 365)
                        {
                            input.Items.Add(Strings.Fields.GoodTilCanceled, true.ToString());
                        }

                        //categories
                        var lineages = new List<string>();
                        int primaryAweCategoryId = ImportCategories_GetLocalCatId(primaryRwaCategoryID);
                        if (primaryAweCategoryId > 0)
                        {
                            input.Items.Add(Strings.Fields.CategoryID, primaryAweCategoryId.ToString());
                            lineages.Add(CommonClient.GetCategoryPath(primaryAweCategoryId).Trees[primaryAweCategoryId].LineageString);
                        }
                        int? primaryAweRegionId = null;
                        if (region4 > 0)
                        {
                            primaryAweRegionId = ImportRegions_GetLocalRegionId(4, region4);
                        }
                        else if (region3 > 0)
                        {
                            primaryAweRegionId = ImportRegions_GetLocalRegionId(3, region3);
                        }
                        else if (region2 > 0)
                        {
                            primaryAweRegionId = ImportRegions_GetLocalRegionId(2, region2);
                        }
                        else if (region1 > 0)
                        {
                            primaryAweRegionId = ImportRegions_GetLocalRegionId(1, region1);
                        }
                        if (primaryAweRegionId.HasValue && primaryAweRegionId.Value > 0)
                        {
                            lineages.Add(CommonClient.GetCategoryPath(primaryAweRegionId.Value).Trees[primaryAweRegionId.Value].LineageString);
                        }
                        string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
                        input.Items.Add(Strings.Fields.AllCategories, categories);

                        if (isClassified)
                        {
                            input.Items.Add(Strings.Fields.ListingType, Strings.ListingTypes.Classified);
                        }
                        else if (isFixedPrice)
                        {
                            input.Items.Add(Strings.Fields.ListingType, Strings.ListingTypes.FixedPrice);
                        }
                        else
                        {
                            input.Items.Add(Strings.Fields.ListingType, Strings.ListingTypes.Auction);
                        }

                        if (reserve > 0.0M)
                        {
                            input.Items.Add(Strings.Fields.ReservePrice, reserve.ToString(cultureInfo));
                        }

                        if (fixedPrice > 0.0M)
                        {
                            input.Items.Add(Strings.Fields.FixedPrice, fixedPrice.ToString(cultureInfo));
                        }

                        if (homepageFeatured)
                        {
                            input.Items.Add("location_1", true.ToString());
                        }

                        if (boldListing)
                        {
                            input.Items.Add("decoration_1", true.ToString());
                        }

                        if (highlightListing)
                        {
                            input.Items.Add("decoration_3", true.ToString());
                        }

                        //shipping options
                        ImportListings_GetRwaCustomShippingOptions(input, rwaListingId);

                        //custom fields
                        var customFieldValues = ImportListings_GetRwaCustomFieldValues(rwaListingId);
                        foreach (string key in customFieldValues.Keys)
                        {
                            if (!string.IsNullOrEmpty(customFieldMap[key]) && customFieldMap[key] != "DoNotImport")
                            {
                                input.Items.Add(customFieldMap[key], customFieldValues[key]);
                            }
                        }

                        //save as draft?
                        if (isListingTemplate)
                        {
                            input.Items.Add(Strings.Fields.SaveAsDraft, true.ToString());
                        }

                        try
                        {
                            //images
                            try
                            {
                                ImportListings_GetRwaListingImages(input, rwaListingId, imageImportUriBase);
                            }
                            catch
                            {
                                throw new Exception("One or more images could not be processed");
                            }

                            if (!input.Items.ContainsKey(Strings.Fields.CategoryID))
                            {
                                throw new Exception("One or more categories could not be processed");
                            }

                            int newListingId;
                            ListingClient.CreateListing(User.Identity.Name, input, false, out newListingId, false);
                            ImportListings_MapImportedListing(newListingId, rwaListingId);
                            addedCount++;
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            string allValidationErrors = string.Empty;
                            foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                            {
                                allValidationErrors += "[" + issue.Key + "]" + issue.Message + "; ";
                            }
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1})]: {2}", title, rwaListingId, this.AdminResourceString(allValidationErrors)));
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1})]: {2}", title, rwaListingId, this.AdminResourceString(iofc.Detail.Reason.ToString())));
                        }
                        catch (Exception e)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1})]: {2}", title, rwaListingId, e.Message));
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                connection.Close();
            }

            result.Data = new
            {
                addedCount = addedCount,
                skippedCount = skippedCount,
                errors = errorMessages.ToArray()
            };
            return result;
        }

        /// <summary>
        /// Gets the AWE listing ID mapped to the specified RWA listing ID
        /// </summary>
        /// <param name="rwaListingId">the specified RWA listing ID</param>
        /// <returns>the AWE listing ID</returns>
        private int ImportListings_GetLocalListingId(int rwaListingId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            int retVal = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("select localListingId from RWX_ImportedListingsMap where foreignListingId = {0}", rwaListingId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = SqlToInt(reader["localListingId"]);
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Inserts a record mapping the specified AWE listing ID to the specified RWA listing ID
        /// </summary>
        /// <param name="aweListingId">the specified AWE listing ID</param>
        /// <param name="rwaListingId">the specified RWA listing ID</param>
        /// <returns>the AWE category ID</returns>
        private void ImportListings_MapImportedListing(int aweListingId, int rwaListingId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("insert RWX_ImportedListingsMap (localListingId, foreignListingId) values ({0}, {1})", aweListingId, rwaListingId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        /// <summary>
        /// Generates Media objects for all images attached to the specified RWA listing and updates the input container with required references to these objects
        /// </summary>
        /// <param name="input">user input container</param>
        /// <param name="rwaListingId">the ID of the specified listing</param>
        /// <param name="importLocationBase">the base URI of images to be imported</param>
        private void ImportListings_GetRwaListingImages(UserInput input, int rwaListingId, string importLocationBase)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText =
                    string.Format(" select PictureURL from PICURLS where ItemId = {0} order by picIndex ", rwaListingId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                int mediaOrder = 0;
                while (reader.Read())
                {
                    string picURL = SqlToString(reader["PictureURL"]).Replace("\\", "/");
                    Uri uri = new Uri(Path.Combine(importLocationBase, picURL));


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
                        saverProviderSettings.Add("VirtualFolder", Server.MapPath("~"));
                    }
                    mediaSaver.Save(saverProviderSettings, newMedia);
                    //Save the media object to the db                        
                    CommonClient.AddMedia(input.ActingUserName, newMedia);

                    input.Items.Add("media_guid_" + newMedia.GUID.ToString(), newMedia.GUID.ToString());
                    input.Items.Add("media_ordr_" + newMedia.GUID.ToString(), (mediaOrder++).ToString(CultureInfo.InvariantCulture));
                }
                connection.Close();
            }
        }

        /// <summary>
        /// Gets a list of all custom field values for the specified RWA listing ID
        /// </summary>
        /// <returns>a dictionary with keys that map to RWA service codes and values that map to AWE shipping method names</returns>
        private Dictionary<string, string> ImportListings_GetShippingOptionDictionary()
        {
            Dictionary<string, string> retVal = (Dictionary<string, string>)SiteClient.GetCacheData("ListingImportShippingOptsDictionary");

            if (retVal == null)
            {
                string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

                retVal = new Dictionary<string, string>();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.CommandText = " select * from PREFERENCES where Name like 'ShippingServiceName%' ";
                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                    while (reader.Read())
                    {
                        string prefName = SqlToString(reader["Name"]);
                        string prefValue = SqlToString(reader["PrefValue"]);
                        switch (prefName)
                        {
                            case "ShippingServiceName1":
                                retVal.Add("admin_defined1", prefValue);
                                break;
                            case "ShippingServiceName2":
                                retVal.Add("admin_defined2", prefValue);
                                break;
                            case "ShippingServiceName3":
                                retVal.Add("admin_defined3", prefValue);
                                break;
                            case "ShippingServiceName4":
                                retVal.Add("admin_defined4", prefValue);
                                break;
                        }
                    }
                    connection.Close();
                }

                //'UPS
                retVal.Add("UPSNDA", "UPS Next Day Air");
                retVal.Add("UPSNDE", "UPS Next Day Early AM");
                retVal.Add("UPSNDAS", "UPS Next Day Saturday Delivery");
                retVal.Add("UPSNDS", "UPS Next Day Saver");
                retVal.Add("UPS2DE", "UPS 2 Day Air AM (comm. destinations only)");
                retVal.Add("UPS2ND", "UPS 2nd Day Air");
                retVal.Add("UPS3DS", "UPS 3 Day Select");
                retVal.Add("UPSGND", "UPS Ground");

                retVal.Add("UPSCAN", "UPS Canada Standard");
                retVal.Add("UPSWEX", "UPS Worldwide Express");
                retVal.Add("UPSWSV", "UPS Worldwide Saver");
                retVal.Add("UPSWEP", "UPS Worldwide Expedited");

                //'FEDEX
                retVal.Add("FDX2D", "FEDEX 2 Day");
                retVal.Add("FDXES", "FEDEX Express Saver");
                retVal.Add("FDXFO", "FEDEX First Overnight");
                retVal.Add("FDXPO", "FEDEX Priority Overnight");
                retVal.Add("FDXPOS", "FEDEX Priority Overnight Saturday Delivery");
                retVal.Add("FDXSO", "FEDEX Standard Overnight");
                retVal.Add("FDXGND", "FEDEX Ground");
                retVal.Add("FDXHD", "FEDEX Home Delivery");

                retVal.Add("FDXIGND", "FEDEX International Ground");
                retVal.Add("FDXIE", "FEDEX International Economy");
                retVal.Add("FDXIF", "FEDEX International First");
                retVal.Add("FDXIP", "FEDEX International Priority");

                //'USPS
                retVal.Add("USPFC", "USPS First-Class Mail");
                retVal.Add("USPEXP", "USPS Express Mail");
                retVal.Add("USPBPM", "USPS Bound/Printed");
                retVal.Add("USPLIB", "USPS Library");
                retVal.Add("USPMM", "USPS Media Mail");
                retVal.Add("USPPM", "USPS Priority Mail");
                retVal.Add("USPPP", "USPS Parcel Post");

                retVal.Add("USPFCI", "USPS First Class International");
                retVal.Add("USPPMI", "USPS Priority Mail International");
                retVal.Add("USPEMI", "USPS Express Mail International");
                retVal.Add("USPGXG", "USPS Global Express Guaranteed");

                //DHL
                retVal.Add("DHL2D", "DHL Second Day");
                retVal.Add("DHLEXA", "DHL Next Afternoon");
                retVal.Add("DHLEXM", "DHL Express 10:30");
                retVal.Add("DHLEXP", "DHL Express");
                retVal.Add("DHLGND", "DHL Ground");

                retVal.Add("DHLWPE", "DHL Worldwide Priority Express");

                SiteClient.SetCacheData("ListingImportShippingOptsDictionary", retVal, 15); // cache this data for 15 minutes
            }
            return retVal;
        }

        /// <summary>
        /// Gets a list of all flat rate shipping options for the specified RWA listing ID and updates the input container with required references to the AWE equivalents
        /// </summary>
        /// <param name="input">user input container</param>
        /// <param name="rwaListingId">the specified RWA listing ID</param>
        private void ImportListings_GetRwaCustomShippingOptions(UserInput input, int rwaListingId)
        {
            var shippingOptsDictionary = ImportListings_GetShippingOptionDictionary();
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText =
                    string.Format(" select ServiceCode, FlatShippingCost, FlatExtraShippingCost " +
                    "from SHIPPINGOPTS_ITEM where ItemID = {0} order by DisplayOrder ", rwaListingId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    string serviceCode = SqlToString(reader["ServiceCode"]);
                    decimal shippingMethodAmount = SqlToDecimal(reader["FlatShippingCost"]);
                    decimal shippingMethodAdditionalAmount = SqlToDecimal(reader["FlatExtraShippingCost"]);
                    if (shippingOptsDictionary.Keys.Any(k => k == serviceCode))
                    {
                        ShippingMethod aweShippingMethod = SiteClient.ShippingMethods.FirstOrDefault(sm => sm.Name == shippingOptsDictionary[serviceCode]);
                        if (aweShippingMethod != null)
                        {
                            input.Items.Add("ship_method_" + aweShippingMethod.ID, aweShippingMethod.ID.ToString());
                            input.Items.Add("ship_amount_" + aweShippingMethod.ID, shippingMethodAmount.ToString());
                            input.Items.Add("ship_additional_" + aweShippingMethod.ID, shippingMethodAdditionalAmount.ToString());
                        }
                    }
                }
                connection.Close();
            }
        }

        /// <summary>
        /// Imports regions from an rwAuction Pro 7.0 database
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ImportRegions()
        {
            //Set Default Values
            ViewData["BatchSize"] = "25";

            return View();
        }

        /// <summary>
        /// Gets a count of regions that could be imported
        /// </summary>
        /// <returns>the number of rw7 region records found in the specified database</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public string ImportRegions_GetCount()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = "0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select all_regions_count = count(*) from (" +
                    "select ID from region1 union all " +
                    "select ID from region2 union all " +
                    "select ID from region3 union all " +
                    "select ID from region4) as allregions";
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = ((int)reader["all_regions_count"]).ToString();
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Gets the AWE region ID mapped to the specified RWA region tier and region ID
        /// </summary>
        /// <param name="rwaRegionTier">the specified RWA region tier</param>
        /// <param name="rwaRegionId">the specified RWA region ID</param>
        /// <returns>the AWE region ID</returns>
        private int ImportRegions_GetLocalRegionId(int rwaRegionTier, int rwaRegionId)
        {
            if (rwaRegionTier == 0) return 27; // always map "Tier 0" (RWA region root) to 27 (AWE region root)

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            int retVal = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("select localRegionId from RWX_ImportedRegionMap " +
                    " where foreignRegionTier = {0} and foreignRegionId = {1}", rwaRegionTier, rwaRegionId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = SqlToInt(reader["localRegionId"]);
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Inserts a record mapping the specified AWE region ID to the specified RWA region ID
        /// </summary>
        /// <param name="aweRegionId">the specified AWE region ID</param>
        /// <param name="rwaRegionTier">the specified RWA region tier</param>
        /// <param name="rwaRegionId">the specified RWA region ID</param>
        /// <returns>the AWE region ID</returns>
        private void ImportRegions_MapImportedRegion(int aweRegionId, int rwaRegionTier, int rwaRegionId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("insert RWX_ImportedRegionMap (localRegionId, foreignRegionTier, foreignRegionId) " +
                    "values ({0}, {1}, {2})", aweRegionId, rwaRegionTier, rwaRegionId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        /// <summary>
        /// Imports a batch of regions
        /// </summary>
        /// <param name="BatchSize">the number of regions to attempt to import per batch</param>
        /// <param name="BatchIndex">0-based index of the requested page to process</param>
        /// <returns>
        ///     JSON encoded result { { addedCount = x }, { skippedCount = y }, { errors = string[] } }
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult ImportRegions_ImportBatch(int BatchSize, int BatchIndex)
        {
            JsonResult result = new JsonResult();

            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            var errorMessages = new List<string>();

            int catsAdded = 0;
            int catsSkipped = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                if (BatchSize < 1) BatchSize = 1; // values less than 1 are invalid
                int firstRow = (BatchSize * BatchIndex) + 1;
                int lastRow = BatchSize * (BatchIndex + 1);
                command.CommandText =
                    " with all_regions (ID, Tier, ParentID, RegionName, DisplayOrder) as ( " +
                    "  select ID, Tier = 1, ParentID = 0, RegionName, DisplayOrder from region1 union all " +
                    "  select ID, Tier = 2, ParentID, RegionName, DisplayOrder from region2 union all " +
                    "  select ID, Tier = 3, ParentID, RegionName, DisplayOrder from region3 union all " +
                    "  select ID, Tier = 4, ParentID, RegionName, DisplayOrder from region4 " +
                    " ) " +
                    " select * from  ( " +
                    "   select rowNum = row_number() over (order by Tier, DisplayOrder), ID, Tier, ParentID, RegionName, DisplayOrder from all_regions " +
                    string.Format(" ) as resultSet where (rowNum between {0} and {1}) order by rowNum ", firstRow, lastRow);

                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    int rwaRegionId = SqlToInt(reader["ID"]);
                    int rwaRegionTier = SqlToInt(reader["Tier"]);
                    int existingRegionId = ImportRegions_GetLocalRegionId(rwaRegionTier, rwaRegionId);
                    if (existingRegionId == 0)
                    {
                        int rwaParentId = SqlToInt(reader["ParentID"]);
                        string rwaRegionName = SqlToString(reader["RegionName"]);
                        int rwaDisplaySequence = SqlToInt(reader["DisplayOrder"]);

                        int aweParentId = ImportRegions_GetLocalRegionId(rwaRegionTier - 1, rwaParentId);

                        Category region = new Category();
                        region.ParentCategoryID = aweParentId;
                        region.MVCAction = string.Empty;
                        region.Type = Strings.CategoryTypes.Region;
                        region.Name = rwaRegionName;
                        region.DisplayOrder = rwaDisplaySequence;
                        try
                        {
                            if (region.ParentCategoryID == 0)
                                throw new Exception(string.Format("Skipped -- Parent Region ID {0} in tier {1} was not imported", rwaParentId, rwaRegionTier - 1));
                            CommonClient.AddChildCategory(User.Identity.Name, region);
                            ImportRegions_MapImportedRegion(region.ID, rwaRegionTier, rwaRegionId);
                            catsAdded++;
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1},{2})]: {3}", rwaRegionName, rwaRegionTier, rwaRegionId, this.AdminResourceString(iofc.Detail.Reason.ToString())));
                        }
                        catch (Exception e)
                        {
                            errorMessages.Add(string.Format("ERROR [\"{0}\" ({1},{2})]: {3}", rwaRegionName, rwaRegionTier, rwaRegionId, e.Message));
                        }
                    }
                    else
                    {
                        catsSkipped++;
                    }
                }
                connection.Close();
            }

            result.Data = new
            {
                addedCount = catsAdded,
                skippedCount = catsSkipped,
                errors = errorMessages.ToArray()
            };
            return result;
        }
        
        /// <summary>
        /// Imports feedback data from an rwAuction Pro 7.0 database
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult ImportFeedbacks()
        {
            //Set Default Values
            ViewData["BatchSize"] = "25";

            return View();
        }

        /// <summary>
        /// Gets a count of regions that could be imported
        /// </summary>
        /// <returns>the number of rw7 region records found in the specified database</returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public string ImportFeedbacks_GetCount()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = "0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = "select all_feedbacks_count = count(*) from FEEDBACK";
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = ((int)reader["all_feedbacks_count"]).ToString();
                }
                connection.Close();
            }
            return retVal;
        }

        private string LookupRwaUserName(int rwaUserId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;

            string retVal = string.Empty;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand();
                command.CommandText = string.Format("select UserName from USERS where UserID={0}", rwaUserId);
                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    retVal = (string)reader["UserName"];
                }
                connection.Close();
            }
            return retVal;
        }

        /// <summary>
        /// Imports a batch of feedbacks
        /// </summary>
        /// <param name="BatchSize">the number of feedbacks to attempt to import per batch</param>
        /// <param name="BatchIndex">0-based index of the requested page to process</param>
        /// <returns>
        ///     JSON encoded result { { addedCount = x }, { skippedCount = y }, { errors = string[] } }
        /// </returns>
        [Authorize(Roles = Roles.Admin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult ImportFeedbacks_ImportBatch(int BatchSize, int BatchIndex)
        {
            JsonResult result = new JsonResult();

            string rwaConnectionString = ConfigurationManager.ConnectionStrings["db_connection_rw7"].ConnectionString;
            string aweConnectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;

            var errorMessages = new List<string>();

            int feedbacksAdded = 0;
            int feedbacksSkipped = 0;
            using (SqlConnection conn2 = new SqlConnection(aweConnectionString))
            using (SqlConnection connection = new SqlConnection(rwaConnectionString))
            {
                conn2.Open();
                connection.Open();
                SqlCommand command = new SqlCommand();
                if (BatchSize < 1) BatchSize = 1; // values less than 1 are invalid
                int firstRow = (BatchSize * BatchIndex) + 1;
                int lastRow = BatchSize * (BatchIndex + 1);
                command.CommandText =
                    " select * from  ( " +
                    "   select rowNum = row_number() over (order by FeedbackID), * from FEEDBACK " +
                    string.Format(" ) as resultSet where (rowNum between {0} and {1}) order by rowNum ", firstRow, lastRow);

                command.Connection = connection;
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
                while (reader.Read())
                {
                    int rwa_FeedbackID = SqlToInt(reader["FeedbackID"]);
                    int rwa_ToUserID = SqlToInt(reader["ToUserID"]);
                    int rwa_FromUserID = SqlToInt(reader["FromUserID"]);
                    int rwa_ItemID = SqlToInt(reader["ItemID"]);
                    string rwa_Comment = SqlToString(reader["Comment"]);
                    int rwa_FeedbackType = SqlToInt(reader["FeedbackType"]);
                    bool rwa_IsBuyer = SqlToInt(reader["IsBuyer"]) == 1;
                    DateTime rwa_EntryDate = SqlToDateTime(reader["EntryDate"]);
                    string rwa_ItemTitle = SqlToString(reader["ItemTitle"]);

                    string toUserName;
                    int aweToUserId = ImportUsers_GetLocalUserId(rwa_ToUserID, out toUserName);
                    string fromUserName;
                    int aweFromUserId = ImportUsers_GetLocalUserId(rwa_FromUserID, out fromUserName);

                    try
                    {
                        if (aweToUserId == 0)
                        {
                            throw new Exception(string.Format("Imported \"To User\" not found [RWA User: {0}({1})]", LookupRwaUserName(rwa_ToUserID), rwa_ToUserID));
                        }
                        if (aweFromUserId == 0)
                        {
                            throw new Exception(string.Format("Imported \"From User\" not found [RWA User: {0}({1})]", LookupRwaUserName(rwa_FromUserID), rwa_FromUserID));
                        }
                        SqlCommand insCmd = new SqlCommand();
                        insCmd.Connection = conn2;
                        //insCmd.CommandTimeout = timeoutSeconds;
                        insCmd.CommandType = CommandType.StoredProcedure;
                        insCmd.CommandText = "RWX_ImportOldFeedback";
                        insCmd.Parameters.AddWithValue("@ToUserId", aweToUserId);
                        insCmd.Parameters.AddWithValue("@FromUserId", aweFromUserId);
                        insCmd.Parameters.AddWithValue("@IsFromSeller", rwa_IsBuyer);
                        insCmd.Parameters.AddWithValue("@Comment", rwa_Comment);
                        switch (rwa_FeedbackType)
                        {
                            case 1: insCmd.Parameters.AddWithValue("@Rating", 5); break; //positive
                            case 0: insCmd.Parameters.AddWithValue("@Rating", 3); break; //neutral
                            default: insCmd.Parameters.AddWithValue("@Rating", 1); break; //negative
                        }
                        insCmd.Parameters.AddWithValue("@DateStamp", rwa_EntryDate);
                        insCmd.Parameters.AddWithValue("@ListingID", 0 - rwa_ItemID);
                        insCmd.Parameters.AddWithValue("@ListingOwnerUN", rwa_IsBuyer ? fromUserName : toUserName);
                        insCmd.ExecuteNonQuery();
                        feedbacksAdded++;
                    }
                    catch (Exception e)
                    {
                        errorMessages.Add(string.Format("ERROR [{0},{1}]: {2}", aweToUserId, aweFromUserId, e.Message));
                        feedbacksSkipped++;
                    }
                }
                connection.Close();
                conn2.Close();
            }

            result.Data = new
            {
                addedCount = feedbacksAdded,
                skippedCount = feedbacksSkipped,
                errors = errorMessages.ToArray()
            };
            return result;
        }

        #endregion Data Importing

        #region Data Management

        /// <summary>
        /// Provides settings to automatic old data cleanup
        /// </summary>
        [Authorize(Roles = Roles.Admin)]
        public ActionResult DataMaintenance()
        {
            const int containerCategoryId = 802;
            Category containerCategory = CommonClient.GetCategoryByID(containerCategoryId);
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
            
            List<CustomProperty> propertiesToDisplay = SiteClient.Properties.WhereContainsFields(containerCategory.CustomFieldIDs);

            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                //This is a postback
                try
                {
                    UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), cultureCode, cultureCode);
                    input.AddAllFormValues(this, new string[] { Strings.MVC.SubmitAction_Save });
                    //only attempt to update properties that weren't disabled on the HTML form
                    var propsToUpdate = new List<CustomProperty>(propertiesToDisplay.Where(p => input.Items.ContainsKey(p.Field.Name)));
                    try
                    {
                        if (SiteClient.DemoEnabled)
                        {
                            throw new Exception("DemoDisabledNotSaved");
                        }

                        SiteClient.UpdateSettings(User.Identity.Name, propsToUpdate, input);
                        SiteClient.Reset();
                        propertiesToDisplay = SiteClient.Properties.WhereContainsFields(containerCategory.CustomFieldIDs); // reload after update
                        PrepareSuccessMessage("DataMaintenance", MessageType.Method);
                    }
                    catch (FaultException<ValidationFaultContract> vfc)
                    {
                        //display validation errors
                        foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                    }
                }
                catch
                {
                    PrepareErrorMessage("DataMaintenance", MessageType.Method);
                }
            }
            else
            {
                //initial page load, populate modelstate
                ModelState.FillProperties(propertiesToDisplay, cultureInfo);
            }

            return View(propertiesToDisplay);
        }

        #endregion Data Management

    }
}
