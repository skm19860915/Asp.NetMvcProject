using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Mvc;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaLoader;
using RainWorx.FrameWorx.Providers.MediaSaver;

using System.Threading;
using System.Configuration;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.MVC.Models;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to AJAX requests
    /// </summary>
    [HttpHeader("Cache-Control", "no-cache")]
    public class RealTimeController : AuctionWorxController
    {
        /// <summary>
        /// Returns the current price of a listing
        /// </summary>
        /// <param name="listingID">ID of the requested listing</param>
        /// <returns>JSON encoded DTO.CurrentPrice object</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetCurrentPrice(int listingID)
        {
            JsonResult result = new JsonResult();

            //var r = new Random();                   
            //return ((decimal)r.NextDouble());                    
            //return (LocalizeCurrency(ListingClient.GetCurrentPrice(listingID)));                        
            CurrentPrice currentPrice = ListingClient.GetCurrentPrice(HttpContext.User.Identity.IsAuthenticated ? HttpContext.User.Identity.Name : Strings.Roles.AnonymousUser, listingID, Currency());
            if (currentPrice == null) return null;

            currentPrice.DisplayListingPrice = SiteClient.FormatCurrency(currentPrice.ListingPrice,
                                                                             currentPrice.ListingCurrencyCode, Culture());
            if (currentPrice.ListingIncrement.HasValue)
                currentPrice.DisplayListingIncrement = SiteClient.FormatCurrency(currentPrice.ListingIncrement.Value,
                                                                                         currentPrice.ListingCurrencyCode, Culture());
            if (currentPrice.NextListingPrice.HasValue)
                currentPrice.DisplayNextListingPrice = SiteClient.FormatCurrency(currentPrice.NextListingPrice.Value,
                                                                                         currentPrice.ListingCurrencyCode, Culture());
            else currentPrice.DisplayNextListingPrice = SiteClient.FormatCurrency(currentPrice.ListingPrice,
                                                                                         currentPrice.ListingCurrencyCode, Culture());

            if (currentPrice.LocalIncrement.HasValue)
                currentPrice.DisplayLocalIncrement = SiteClient.FormatCurrency(currentPrice.LocalIncrement.Value,
                                                                                         currentPrice.LocalCurrencyCode, Culture());
            currentPrice.DisplayLocalPrice = SiteClient.FormatCurrency(currentPrice.LocalPrice,
                                                                                         currentPrice.LocalCurrencyCode, Culture());
            if (currentPrice.NextLocalPrice.HasValue)
                currentPrice.DisplayNextLocalPrice = SiteClient.FormatCurrency(currentPrice.NextLocalPrice.Value,
                                                                                         currentPrice.LocalCurrencyCode, Culture());
            result.Data = currentPrice;
            return result;
        }

        /// <summary>
        /// Returns the value of the 'culture' cookie
        /// </summary>
        /// <returns>string</returns>
        private string Culture()
        {
            if (HttpContext.Request.Cookies["culture"] == null)
            {
                return SiteClient.SiteCulture;
            }
            else if (string.IsNullOrEmpty(HttpContext.Request.Cookies["culture"].Value))
            {
                return SiteClient.SiteCulture;
            }
            else
            {
                return HttpContext.Request.Cookies["culture"].Value;
            }
        }

        /// <summary>
        /// Returns the value of the 'currency' cookie
        /// </summary>
        /// <returns>string</returns>
        private string Currency()
        {
            if (HttpContext.Request.Cookies["currency"] == null)
            {
                return SiteClient.SiteCurrency;
            }
            else if (string.IsNullOrEmpty(HttpContext.Request.Cookies["currency"].Value))
            {
                return SiteClient.SiteCurrency;
            }
            else
            {
                return HttpContext.Request.Cookies["currency"].Value;
            }
        }

        /// <summary>
        /// Returns all immediate subcategories of the requested parent category
        /// </summary>
        /// <param name="parentCategoryID">ID of the requested parent category</param>
        /// <returns>JSON encoded array of DTO.Category objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetChildCategories(int parentCategoryID)
        {
            JsonResult result = new JsonResult();
            //result.Data = CommonClient.GetChildCategories(parentCategoryID).ToArray();
            var resultCategories = CommonClient.GetChildCategories(parentCategoryID);
            List<Category> retVal = new List<Category>(resultCategories.Count);
            foreach (var cat in resultCategories)
            {
                var localizedCat = new Category()
                {
                    CustomFieldIDs = cat.CustomFieldIDs,
                    DisplayOrder = cat.DisplayOrder,
                    EnabledCustomProperty = cat.EnabledCustomProperty,
                    ID = cat.ID,
                    MetaDescription = cat.MetaDescription,
                    MetaKeywords = cat.MetaKeywords,
                    MVCAction = cat.MVCAction,
                    Name = cat.Name,
                    PageContent = cat.PageContent,
                    PageTitle = cat.PageTitle,
                    ParentCategoryID = cat.ParentCategoryID,
                    RolesAllowed = cat.RolesAllowed,
                    Type = cat.Type
                };
                localizedCat.Name = this.LocalizedCategoryName(localizedCat.Name);
                retVal.Add(localizedCat);
            }
            result.Data = retVal.ToArray();
            return result;
        }

        /// <summary>
        /// Returns all immediate subcategories of the requested parent category, excluding any categories which are not allowed for the specified listing type
        /// </summary>
        /// <param name="parentCategoryID">ID of the requested parent category</param>
        /// <param name="listingType">Name of the requested listing type</param>
        /// <returns>JSON encoded array of DTO.Category objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetChildCategoriesForListingType(int parentCategoryID, string listingType)
        {
            JsonResult result = new JsonResult();
            var resultCategories = ListingClient.GetValidChildCategoriesForListingType(parentCategoryID, listingType);
            List<Category> retVal = new List<Category>(resultCategories.Count);
            foreach (var cat in resultCategories)
            {
                var localizedCat = new Category()
                {
                    CustomFieldIDs = cat.CustomFieldIDs,
                    DisplayOrder = cat.DisplayOrder,
                    EnabledCustomProperty = cat.EnabledCustomProperty,
                    ID = cat.ID,
                    MetaDescription = cat.MetaDescription,
                    MetaKeywords = cat.MetaKeywords,
                    MVCAction = cat.MVCAction,
                    Name = cat.Name,
                    PageContent = cat.PageContent,
                    PageTitle = cat.PageTitle,
                    ParentCategoryID = cat.ParentCategoryID,
                    RolesAllowed = cat.RolesAllowed,
                    Type = cat.Type
                };
                localizedCat.Name = this.LocalizedCategoryName(localizedCat.Name);
                retVal.Add(localizedCat);
            }
            result.Data = retVal.ToArray();
            return result;
        }

        /// <summary>
        /// Returns all immediate subregions of the requested parent region
        /// </summary>
        /// <param name="parentRegionID">ID of the requested parent region</param>
        /// <returns>JSON encoded array of DTO.Category objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetChildRegions(int parentRegionID)
        {
            JsonResult result = new JsonResult();
            //result.Data = CommonClient.GetChildCategories(parentRegionID).ToArray();
            var resultCategories = CommonClient.GetChildCategories(parentRegionID);
            List<Category> retVal = new List<Category>(resultCategories.Count);
            foreach (var cat in resultCategories)
            {
                var localizedCat = new Category()
                {
                    CustomFieldIDs = cat.CustomFieldIDs,
                    DisplayOrder = cat.DisplayOrder,
                    EnabledCustomProperty = cat.EnabledCustomProperty,
                    ID = cat.ID,
                    MetaDescription = cat.MetaDescription,
                    MetaKeywords = cat.MetaKeywords,
                    MVCAction = cat.MVCAction,
                    Name = cat.Name,
                    PageContent = cat.PageContent,
                    PageTitle = cat.PageTitle,
                    ParentCategoryID = cat.ParentCategoryID,
                    RolesAllowed = cat.RolesAllowed,
                    Type = cat.Type
                };
                localizedCat.Name = this.LocalizedCategoryName(localizedCat.Name);
                retVal.Add(localizedCat);
            }
            result.Data = retVal.ToArray();
            return result;
        }

        /// <summary>
        /// Returns the localized string representation of the current date/time
        /// </summary>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetCurrentTime()
        {
            JsonResult result = new JsonResult();
            //return DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");            
            result.Data = LocalizeDTTM(CommonClient.GetDalDttm()/*DateTime.UtcNow*/);
            return result;
        }

        /// <summary>
        /// Returns the localized string representation of the current date/time in culture-invariant format
        /// </summary>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetSiteTime()
        {
            JsonResult result = new JsonResult();
            result.Data = CultureInvariantLocalDTTM(DateTime.UtcNow);
            return result;
        }

        /// <summary>
        /// Returns the listing types allowed for the requested category
        /// </summary>
        /// <param name="categoryID">ID of the requested category</param>
        /// <returns>JSON encoded array of DTO.ListingType objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetListingTypes(int categoryID)
        {
            bool allowAuctionListingsAndLots = false;
            if (SiteClient.EnableEvents)
            {
                bool tempBool1;
                if (bool.TryParse(ConfigurationManager.AppSettings["AllowAuctionListingsAndLots"], out tempBool1))
                {
                    allowAuctionListingsAndLots = tempBool1;
                }
            }

            JsonResult result = new JsonResult();
            List<ListingType> listingTypes = ListingClient.GetValidListingTypesForCategory(categoryID);
            foreach (ListingType listingType in listingTypes)
            {
                if (string.IsNullOrEmpty(listingType.DisplayName))
                    listingType.DisplayName = this.GlobalResourceString(listingType.Name);
            }

            if (SiteClient.EnableEvents && !allowAuctionListingsAndLots && listingTypes.Any(lt => lt.Name == Strings.ListingTypes.Auction))
            {
                listingTypes.RemoveAll(lt => lt.Name == Strings.ListingTypes.Auction);
            }

            result.Data = listingTypes.ToArray();
            return (result);
        }

        /// <summary>
        /// Returns all states in the requested country
        /// </summary>
        /// <param name="countryID">ID of the requested country</param>
        /// <returns>JSON encoded array of DTO.State objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetStates(int countryID)
        {
            JsonResult result = new JsonResult();

            if (!SiteClient.Countries.Single(c => c.ID == countryID).StateRequired)
            {
                result.Data = "NOSTATES";
                return result;
            }

            State[] states = SiteClient.States.Where(s => (int)s.CountryID == countryID && s.Enabled).ToArray();
            List<State> localizedStates = new List<State>(states.Length);
            foreach (State state in states)
            {
                State clone = state.Clone();
                clone.Name = this.LocalizeState(clone.Name);
                localizedStates.Add(clone);
            }
            result.Data = localizedStates.ToArray();
            return (result);
        }

        /// <summary>
        /// Returns all fields associated with the requested category
        /// </summary>
        /// <param name="categoryID">ID of the requested category</param>
        /// <returns>JSON encoded array of DTO.CustomField objects</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetFields(int categoryID)
        {
            JsonResult result = new JsonResult();
            CustomField[] retVal = CommonClient.GetFieldsByCategoryID(categoryID)
                .Where(f => f.Visibility == CustomFieldAccess.Anonymous)
                .OrderBy(f => f.DisplayOrder)
                .ToArray();

            //localize all fields            
            foreach (CustomField field in retVal)
            {
                if (string.IsNullOrEmpty(field.DisplayName))
                {
                    field.DisplayName = Localize(field.Name);
                    field.DisplayHelp = LocalizeHelp(field.Name);

                    if (field.Type != CustomFieldType.Enum) continue;
                    foreach (ListItem item in field.Enumeration)
                    {
                        item.DisplayName = Localize(item.Name);
                        item.DisplayHelp = LocalizeHelp(item.Name);
                    }
                }
            }

            result.Data = retVal;
            return (result);
        }

        /// <summary>
        /// Updates user record with specified culture value
        /// </summary>
        /// <param name="culture">culture string</param>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult UpdateUserCulture(string culture)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                UserClient.UpdateUserCulture(HttpContext.User.Identity.Name,
                                             HtmlHelpers.FBOUserName(),
                                             culture);
            }
            JsonResult result = new JsonResult();
            result.Data = string.Empty;
            return result;
        }

        /// <summary>
        /// Updates specified invoice record that the specified asynchronous payment provider has been called
        /// </summary>
        /// <param name="paymentProviderName">name of the specified payment provider</param>
        /// <param name="invoiceID">ID of the specified invoice</param>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult NotifyPayment(string paymentProviderName, int invoiceID)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                AccountingClient.PreNotifyAsynchronousPayment(User.Identity.Name,
                                             paymentProviderName,
                                             invoiceID);
            }
            JsonResult result = new JsonResult();
            result.Data = string.Empty;
            return result;
        }

        /// <summary>
        /// Sends an invoice email to the specified recipient using the specified email template
        /// </summary>
        /// <param name="template">email template name</param>
        /// <param name="invoiceID">ID of the specified invoice</param>
        /// <param name="recipient">username of the specified recipient</param>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult EmailInvoice(string template, int invoiceID, string recipient)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                NotifierClient.QueueNotification(User.Identity.Name, HtmlHelpers.FBOUserName(), recipient, template, "Invoice", invoiceID, string.Empty, null, null, null, null);
            }
            JsonResult result = new JsonResult();
            result.Data = string.Empty;
            return result;
        }

        /// <summary>
        /// Returns the localized string representation of the specified date/time
        /// </summary>
        /// <param name="dateTime">The specified date/time</param>
        /// <returns>string</returns>
        private string LocalizeDTTM(DateTime dateTime)
        {
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString("f", CultureInfo.GetCultureInfo(Culture()));
            ////return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(format, CultureInfo.GetCultureInfo(htmlHelper.GetCookie("culture") ?? "en-US"));
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            return TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Utc, siteTimeZone).ToString("f", CultureInfo.GetCultureInfo(Culture()));
        }

        /// <summary>
        /// returns a string representation of a date/time value, adjusted for the site timezone and formatted as a culture-invariant value
        /// </summary>
        /// <param name="utcDateTime">a UTC date value</param>
        private string CultureInvariantLocalDTTM(DateTime utcDateTime)
        {
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            return TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns the localized version of the specified resource key
        /// </summary>
        /// <param name="value">global resource string key</param>
        /// <returns>string</returns>
        private string Localize(string value)
        {
            try
            {
                //return HttpContext.GetGlobalResourceObject("CustomFields", value, CultureInfo.GetCultureInfo(Culture())).ToString();
                return this.CustomFieldResourceString(value);
            }
            catch (Exception)
            {
                return value;
            }
        }

        /// <summary>
        /// Returns the localized version of the specified resource key
        /// </summary>
        /// <param name="value">global resource string key</param>
        /// <returns>string</returns>
        private string LocalizeHelp(string value)
        {
            try
            {
                //return HttpContext.GetGlobalResourceObject("CustomFields", value + "_Help", CultureInfo.GetCultureInfo(Culture())).ToString();
                return this.CustomFieldResourceOrDefaultString(value + "_Help") ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns the localized string representation of the specified listing's end date/time
        /// </summary>
        /// <param name="listingID">ID of the spcified listing</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetCurrentEndDTTM(int listingID)
        {
            JsonResult result = new JsonResult();
            Listing tempListing = ListingClient.GetListingByIDWithFillLevel("xxx", listingID, string.Empty);
            result.Data = LocalizeDTTM(tempListing.EndDTTM.Value);
            return result;
        }

        /// <summary>
        /// Returns the number of seconds remaining before this listing closes
        /// </summary>
        /// <param name="listingID">ID of the spcified listing</param>
        /// <remarks>will not return a numeric value if the listing does not exist or if it is more than 100 years in the future or the past</remarks>
        /// <returns>JSON encoded int</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetTimeRemaining(int listingID)
        {
            JsonResult result = new JsonResult();// { JsonRequestBehavior = JsonRequestBehavior.AllowGet };
            double secondsRemaining = 0;
            string error = string.Empty;
            DateTime? utcEndDTTM = null;
            DateTime? utcNow = null;
            try
            {
                ListingClient.GetListingEndDTTM(listingID, out utcEndDTTM, out utcNow);
            }
            catch (Exception e)
            {
                error = "ERROR: " + e.Message;
            }
            if (utcEndDTTM.HasValue && utcNow.HasValue)
            {
                var difference = utcEndDTTM.Value - utcNow.Value;
                if (difference.TotalDays < 36500d && difference.TotalDays > -36500d) // limit results to values within 100 years of "now"
                {
                    secondsRemaining = difference.TotalSeconds;
                }
                else
                {
                    error = "ERROR_OUT_OF_RANGE";
                }
            }
            else
            {
                error = "ERROR_NOT_FOUND";
            }
            result.Data = new { secondsRemaining, error };
            return result;
        }

        /// <summary>
        /// Returns the key values for the specified listing
        /// </summary>
        /// <param name="listingID">ID of the spcified listing</param>
        /// <remarks>values returned: Price, Currency, AcceptedActionCount, Quantity, Status, EndDTTM</remarks>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetListingVitals(int listingID)
        {
            JsonResult result = new JsonResult();
            Listing tempListing = ListingClient.GetListingByIDWithFillLevel("xxx", listingID, string.Empty);
            result.Data = new
            {
                Price = tempListing.CurrentPrice,
                Currency = tempListing.Currency.Code,
                AcceptedActionCount = tempListing.AcceptedActionCount,
                Quantity = tempListing.CurrentQuantity,
                Status = tempListing.Status,
                EndDTTM = CultureInvariantLocalDTTM(tempListing.EndDTTM.Value)
            };
            return result;
        }

        /// <summary>
        /// Returns the contextual status of the specified listing, based on the currently authenticated user
        /// </summary>
        /// <param name="listingID">ID of the spcified listing</param>
        /// <remarks>values returned: Status, Disposition, Parameters, Disregard, Watched, MaxBidAmount</remarks>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetListingContextStatus(int listingID)
        {
            JsonResult result = new JsonResult();
            if (User.Identity.IsAuthenticated)
            {
                try
                {
                    string fillLevel = ListingFillLevels.Actions + "," + ListingFillLevels.Properties + "," + ListingFillLevels.CurrentAction;
                    Listing tempListing = ListingClient.GetListingByIDAndUserWithFillLevel(User.Identity.Name, listingID, this.FBOUserName(), fillLevel);
                    if (tempListing.Context != null)
                    {
                        result.Data = new
                        {
                            Status = tempListing.Context.Status,
                            Disposition = tempListing.Context.Disposition,
                            Parameters = tempListing.Context.Parameters,
                            Disregard = tempListing.Context.Disregard,
                            Watched = tempListing.Context.Watched,
                            MaxBidAmount = tempListing.Context.UserListingAction != null ? tempListing.Context.UserListingAction.ProxyAmount ?? 0.0M : 0.0M
                        };
                    }
                }
                catch (Exception e)
                {
                    //unexpected error
                    result.Data = new
                    {
                        Status = "UNKNOWN",
                        Error = e.Message
                    };
                    LogManager.WriteLog(null, "GetListingContextStatus error", "RealTimeController", System.Diagnostics.TraceEventType.Error, User.Identity.Name, e,
                        new Dictionary<string, object> { { "ListingID", listingID }, { "FBOUserName", this.FBOUserName() } });
                }
            }
            else
            {
                //not authenticated
                result.Data = new
                {
                    Status = "UNKNOWN",
                    Error = "Not Authenticated"
                };
            }
            return result;
        }

        /// <summary>
        /// Creates one or more subcategories under the specified parent category
        /// </summary>
        /// <param name="parentCategoryID">ID of the specified parent category</param>
        /// <param name="names">comma- or line break-delimited list of category names</param>
        /// <returns>JSON encoded empty string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult AddMultipleCategories(int parentCategoryID, string names)
        {
            JsonResult result = new JsonResult();

            names = names.Replace("\n", ",");
            names = names.Replace("\r", ",");
            names = names.Replace(",,", ",");
            if (names.EndsWith(",")) names = names.Remove(names.Length - 1);

            Category category = new Category();
            category.ParentCategoryID = parentCategoryID;
            category.MVCAction = string.Empty;
            category.Type = CommonClient.GetCategoryByID(parentCategoryID).Type;

            int displayOrder = 0;
            List<Category> children = CommonClient.GetChildCategories(parentCategoryID);
            if (children.Count == 0)
            {
                displayOrder = 0;
            }
            else
            {
                displayOrder = children.OrderBy(c => c.DisplayOrder).Last().DisplayOrder + 1;
            }

            try
            {
                foreach (string categoryName in names.Split(','))
                {
                    category.Name = categoryName.Trim();
                    category.DisplayOrder = displayOrder++;
                    CommonClient.AddChildCategory(User.Identity.Name, category);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                ListingClient.Reset();
            }
            result.Data = string.Empty;
            return result;
        }

        /// <summary>
        /// Deletes the specified category
        /// </summary>
        /// <param name="categoryID">ID of the category to be deleted</param>
        /// <returns>JSON encoded string ("success" if successful)</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult DeleteCategory(int categoryID)
        {
            JsonResult result = new JsonResult();
            try
            {
                CommonClient.DeleteCategory(User.Identity.Name, categoryID);
                ListingClient.Reset();
            }
            catch (System.ServiceModel.FaultException<InvalidOperationFaultContract> io)
            {
                result.Data = io.Detail.Reason.ToString();
                return result;
            }
            result.Data = "success";
            return result;
        }

        /// <summary>
        /// Deletes the specified region
        /// </summary>
        /// <param name="regionID">ID of the region to be deleted</param>
        /// <returns>JSON encoded string ("success" if successful)</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult DeleteRegion(int regionID)
        {
            JsonResult result = new JsonResult();
            try
            {
                CommonClient.DeleteRegion(User.Identity.Name, regionID);
                ListingClient.Reset();
            }
            catch (System.ServiceModel.FaultException<InvalidOperationFaultContract> io)
            {
                result.Data = io.Detail.Reason.ToString();
                return result;
            }
            result.Data = "success";
            return result;
        }

        /// <summary>
        /// Gets usage information for the given Category or Region
        /// </summary>
        /// <param name="categoryID">ID of the Category or Region to get usages for</param>
        /// <returns>JSON encoded 2 element array of ints.</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GetCategoryUsages(int categoryID)
        {
            JsonResult result = new JsonResult();
            result.Data = CommonClient.GetCategoryUsages(User.Identity.Name, categoryID);
            return result;
        }

        /// <summary>
        /// Saves the requested youtube video id as a media asset.
        /// Returns the the new Media GUID prepended to the result of the YouTube Loader Provider.
        /// </summary>
        /// <param name="videoId">ID of the requested YouTube video</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult AsyncUploadYouTubeVideoID(string videoId)
        {
            JsonResult result = new JsonResult();
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            sw.Write(videoId.Trim());
            sw.Flush();

            string context = Strings.MediaUploadContexts.UploadYouTubeVideoID;

            //Get workflow for uploading an image
            Dictionary<string, string> workflowParams = CommonClient.GetAttributeData("MediaAsset.Workflow", context);

            //Generate the media object
            IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(workflowParams["Generator"]);
            Dictionary<string, string> generatorProviderSettings = CommonClient.GetAttributeData(mediaGenerator.TypeName, context);
            Media youtubeVideo = mediaGenerator.Generate(generatorProviderSettings, ms);
            sw.Close();
            ms.Close();
            youtubeVideo.Context = context;

            //Save the media    
            youtubeVideo.Saver = workflowParams["Saver"];
            IMediaSaver mediaSaver = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(youtubeVideo.Saver);
            Dictionary<string, string> saverProviderSettings = CommonClient.GetAttributeData(mediaSaver.TypeName, context);
            mediaSaver.Save(saverProviderSettings, youtubeVideo);

            //Load the media (for thumbnail preview)
            youtubeVideo.Loader = workflowParams["Loader"];
            IMediaLoader mediaLoader = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaLoader>(youtubeVideo.Loader);
            Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, context);
            string loadResult = mediaLoader.Load(loaderProviderSettings, youtubeVideo, "Main");

            //Save the media object to the db                        
            CommonClient.AddMedia("AsyncUploadYouTubeVideoID", youtubeVideo);

            //return the media's GUID and the load result URI for thumbnail preview
            result.Data = youtubeVideo.GUID + loadResult;
            return result;
        }

        /// <summary>
        /// swaps display orders between the specified category and its immediately preceeding simbling
        /// </summary>
        /// <param name="categoryId">id of the specified category</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult MoveCategoryUp(int categoryId)
        {
            JsonResult retVal = new JsonResult();
            CommonClient.MoveCategoryUp(User.Identity.Name, categoryId);
            retVal.Data = "success";
            return retVal;
        }

        /// <summary>
        /// swaps display orders between the specified category and its immediately following simbling
        /// </summary>
        /// <param name="categoryId">id of the specified category</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult MoveCategoryDown(int categoryId)
        {
            JsonResult retVal = new JsonResult();
            CommonClient.MoveCategoryDown(User.Identity.Name, categoryId);
            retVal.Data = "success";
            return retVal;
        }

        /// <summary>
        /// re-calculates display orders of all immediate child categories of the specified parent category
        /// </summary>
        /// <param name="parentCategoryId">id of the specified parent category</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult AlphaSortCategories(int parentCategoryId)
        {
            JsonResult retVal = new JsonResult();
            CommonClient.AlphaSortCategories(User.Identity.Name, parentCategoryId);
            retVal.Data = "success";
            return retVal;
        }

        /// <summary>
        /// Returns the converted price, or an error message
        /// </summary>
        /// <param name="amount">decimal amount to be converted</param>
        /// <param name="fromCurrency">the 3-letter currency code of the amount to be converted</param>
        /// <param name="toCurrency">the 3-letter currency code of the result</param>
        /// <returns>JSON encoded string representation of the converted amount</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult ConvertAmount(string amount, string fromCurrency, string toCurrency)
        {
            JsonResult result = new JsonResult();

            string resultText = string.Empty;
            string errorMessage = string.Empty;
            string errorKey = string.Empty;

            CultureInfo numberCulture = SiteClient.SupportedCultures[Culture()];
            decimal amountToConvert;
            if (decimal.TryParse(amount, NumberStyles.Currency, numberCulture, out amountToConvert))
            {
                List<Currency> allEnabledCurrencies = SiteClient.GetCurrencies();
                Currency from_Currency = allEnabledCurrencies.Where(c => c.Code == fromCurrency).FirstOrDefault();
                Currency to_Currency = allEnabledCurrencies.Where(c => c.Code == toCurrency).FirstOrDefault();

                if (from_Currency != null && to_Currency != null)
                {
                    decimal convertedAmount = (amountToConvert / from_Currency.ConversionToUSD * to_Currency.ConversionToUSD);
                    resultText = SiteClient.FormatCurrency(convertedAmount, toCurrency, Culture());
                }
                else if (from_Currency == null)
                {
                    errorMessage = this.GlobalResourceString("ErrorCurrencyNotEnabled", fromCurrency);
                    errorKey = "ConvertFromCurrency";
                }
                else // if (to_Currency == null)
                {
                    errorMessage = this.GlobalResourceString("ErrorCurrencyNotEnabled", toCurrency);
                    errorKey = "ConvertToCurrency";
                }
            }
            else
            {
                errorMessage = this.GlobalResourceString("InvalidCurrencyAmount");
                errorKey = "ConvertAmount";
            }
            if (!string.IsNullOrEmpty(errorMessage))
            {
                resultText = "<span class=\"validation-summary-errors\" errorKey=\"" + errorKey + "\">" + errorMessage + "</span>";
            }

            //CurrentPrice currentPrice = ListingClient.GetCurrentPrice(HttpContext.User.Identity.IsAuthenticated ? HttpContext.User.Identity.Name : Strings.Roles.AnonymousUser, listingID, Currency());

            //if (currentPrice == null) return null;

            //currentPrice.DisplayListingPrice = SiteClient.FormatCurrency(currentPrice.ListingPrice,
            //                                                                 currentPrice.ListingCurrency.Code, Culture());
            //if (currentPrice.ListingIncrement.HasValue)
            //    currentPrice.DisplayListingIncrement = SiteClient.FormatCurrency(currentPrice.ListingIncrement.Value,
            //                                                                             currentPrice.ListingCurrency.Code, Culture());
            //if (currentPrice.NextListingPrice.HasValue)
            //    currentPrice.DisplayNextListingPrice = SiteClient.FormatCurrency(currentPrice.NextListingPrice.Value,
            //                                                                             currentPrice.ListingCurrency.Code, Culture());

            //if (currentPrice.LocalIncrement.HasValue)
            //    currentPrice.DisplayLocalIncrement = SiteClient.FormatCurrency(currentPrice.LocalIncrement.Value,
            //                                                                             currentPrice.LocalCurrency.Code, Culture());
            //currentPrice.DisplayLocalPrice = SiteClient.FormatCurrency(currentPrice.LocalPrice,
            //                                                                             currentPrice.LocalCurrency.Code, Culture());
            //if (currentPrice.NextLocalPrice.HasValue)
            //    currentPrice.DisplayNextLocalPrice = SiteClient.FormatCurrency(currentPrice.NextLocalPrice.Value,
            //                                                                             currentPrice.LocalCurrency.Code, Culture());
            result.Data = resultText;
            return result;
        }

        /// <summary>
        /// attempts to charge the primary credit card of the payer associated with the specified invoice
        /// </summary>
        /// <param name="invoiceID">if of the specified invoice</param>
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult AttemptBatchPayment(int invoiceID)
        {
            Invoice invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, invoiceID);
            string details;
            PaymentProviderResponse response = AccountingClient.AttemptBatchPayment(User.Identity.Name, invoice, out details);
            JsonResult result = new JsonResult();
            result.Data = response.Approved;
            return result;
        }

        /// <summary>
        /// Attempt to batch process all invoices on demand
        /// </summary>       
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult DemandBatchProcessing()
        {
            AccountingClient.DemandBatchProcessing(User.Identity.Name);
            JsonResult result = new JsonResult();
            result.Data = string.Empty;
            return result;
        }

        /// <summary>
        /// Attempt to batch process all invoices on demand
        /// </summary>       
        /// <returns>JSON encoded string</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        [Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult DemandSalesBatchProcessing()
        {
            AccountingClient.DemandSalesBatchProcessing(User.Identity.Name);
            JsonResult result = new JsonResult();
            result.Data = string.Empty;
            return result;
        }
        
        /// <summary>
        /// Generates a Service Authorization Token
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult GenerateServiceAuthorizationToken()
        {
            byte[] random = new Byte[32];
            //RNGCryptoServiceProvider is an implementation of a random number generator.
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random); // The array is now filled with cryptographically strong random bytes, and none are zero.            
            JsonResult result = new JsonResult();
            result.Data = Convert.ToBase64String(random);
            return result;
        }

        /// <summary>
        /// Retrieves the lot ID for the specified event ID and Lot Number
        /// </summary>
        /// <param name="eventId">the id of the event to get increments for</param>
        /// <param name="lotNumber">alpha-numeric lot number</param>
        /// <returns>
        ///     JSON encoded result { { LotId = x } }
        /// </returns>
        public JsonResult GetLotIdByLotNumber(int eventId, string lotNumber)
        {
            JsonResult result = new JsonResult();
            int lotId = EventClient.GetLotIdByLotNumber(User.Identity.Name, eventId, lotNumber);
            if (lotId > 0)
            {
                result.Data = new
                {
                    LotId = lotId
                };
            }
            else
            {
                result.Data = string.Empty;
            }
            return result;
        }

        /// <summary>
        /// Retrieves the lot ID for the specified event ID and Lot Order
        /// </summary>
        /// <param name="eventId">the id of the event to get increments for</param>
        /// <param name="lotOrder">0-based lot sequence number</param>
        /// <returns>
        ///     JSON encoded result { { LotId = x } }
        /// </returns>
        public JsonResult GetLotIdByLotOrder(int eventId, int lotOrder)
        {
            JsonResult result = new JsonResult();
            int lotId = EventClient.GetLotIdByLotOrder(User.Identity.Name, eventId, lotOrder);
            if (lotId > 0)
            {
                result.Data = new
                {
                    LotId = lotId
                };
            }
            else
            {
                result.Data = string.Empty;
            }
            return result;
        }

        /// <summary>
        /// Retrieves the lot ID for the specified event ID which is closing next
        /// </summary>
        /// <param name="eventId">the id of the event to get increments for</param>
        /// <returns>
        ///     JSON encoded result { { LotId = x } }
        /// </returns>
        public JsonResult GetNextLotClosing(int eventId)
        {
            JsonResult result = new JsonResult();
            int? lotId = EventClient.GetNextLotClosing(User.Identity.Name, eventId);
            if (lotId.HasValue)
            {
                result.Data = new
                {
                    LotId = lotId.Value
                };
            }
            else
            {
                result.Data = string.Empty;
            }
            return result;
        }

        /// <summary>
        /// Returns the current Status of the specified event
        /// </summary>
        /// <returns>JSON encoded string</returns>
        public JsonResult GetEventStatus(int eventId)
        {
            string resultStatus = string.Empty;
            try
            {
                resultStatus = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, eventId, string.Empty).Status;
            }
            catch(Exception)
            {
                //ignore any errors - just return Status: <blank>
            }
            JsonResult result = new JsonResult();
            result.Data = new { Status = resultStatus };
            return result;
        }

        /// <summary>
        /// Retrieves the default content for the specified email template
        /// </summary>
        /// <param name="template">the name of the email template to retrieve</param>
        /// <returns>
        ///     JSON encoded result { { subject = x, body = y } }
        /// </returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult GetDefaultTemplateContent(string template)
        {
            JsonResult result = new JsonResult();
            try
            {
                string subject;
                string body;
                NotifierClient.GetDefaultContent(User.Identity.Name, template, out subject, out body);
                result.Data = new { subject, body };
            }
            catch (Exception e)
            {
                result.Data = new { error = e.Message };
            }
            return result;
        }

        /// <summary>
        /// places a random bid on the specified listing; requires demo mode to be enabled, otherwise does nothing
        /// </summary>
        /// <param name="listingId">a valid, active auction listing id</param>
        public JsonResult PlaceDemoBid(int listingId)
        {
            if (SiteClient.DemoEnabled)
            {
                string clientIP = Request.UserHostAddress;
                var args = new Dictionary<string, object>();
                args.Add("ListingID", listingId);
                args.Add("ExcludeUN", User.Identity.IsAuthenticated ? User.Identity.Name : string.Empty);
                args.Add("ClientIP", clientIP);
                Thread t = new Thread(DemoBidProc);
                t.IsBackground = false;
                t.Name = "PlaceDemoBid";
                t.Start(args);
            }
            return new JsonResult()
            {
                Data = new { status = "bid request sent" },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private void DemoBidProc(object args)
        {
            try
            {
                var argsDict = (Dictionary<string, object>)args;
                int listingId = (int)argsDict["ListingID"];
                string excludeUN = (string)argsDict["ExcludeUN"];
                string clientIP = (string)argsDict["ClientIP"];
                string demoUsersList = ConfigurationManager.AppSettings["DemoUsers"];  //  e.g. AuctionBob|BidderBill
                decimal demoBidPercentRange = 5M;
                decimal.TryParse(ConfigurationManager.AppSettings["DemoBidPercentRange"], out demoBidPercentRange);
                if (!string.IsNullOrWhiteSpace(demoUsersList) && !string.IsNullOrWhiteSpace(clientIP))
                {
                    var demoUserNames = demoUsersList.Split('|');
                    var listing = ListingClient.GetListingByIDWithFillLevel(SystemActors.SystemUserName, listingId, ListingFillLevels.None);
                    if (listing != null)
                    {
                        if (listing.Type.Name == "Auction" && listing.Status == ListingStatuses.Active)
                        {
                            Random rnd = new Random();
                            string demoBidderUN = demoUserNames[rnd.Next(0, demoUserNames.Length)];
                            //skip demo bid if this username is the user who is currently signed-in
                            if (!excludeUN.Equals(demoBidderUN, StringComparison.OrdinalIgnoreCase))
                            {
                                decimal minBid = Math.Round((listing.CurrentPrice ?? 0M) + (listing.Increment ?? 0M), 2);
                                decimal maxBid = Math.Round(minBid + (minBid * demoBidPercentRange / 100M), 2);
                                decimal bidAmount = Math.Round(((decimal)rnd.Next((int)(minBid * 100), (int)(maxBid * 100)) / 100) * 4, MidpointRounding.ToEven) / 4;
                                var input = new UserInput(SystemActors.SystemUserName, demoBidderUN, SiteClient.SiteCulture, SiteClient.SiteCulture);
                                CultureInfo numberCulture = SiteClient.SupportedCultures[SiteClient.SiteCulture];
                                input.Items.Add("ListingID", listingId.ToString(numberCulture));
                                input.Items.Add("ListingType", "Auction");
                                input.Items.Add("BidAmount", bidAmount.ToString(numberCulture));
                                input.Items.Add("BuyItNow", "false");

                                bool accepted;
                                ReasonCode reasonCode;
                                LineItem newPurchaseLineitem;
                                ListingClient.SubmitListingAction(SystemActors.SystemUserName, input, out accepted, out reasonCode, out newPurchaseLineitem, null);
                                LogManager.WriteLog("Demo Bid Submitted", "Demo Bid Submitted", "RealTimeController", System.Diagnostics.TraceEventType.Information, null, null,
                                    new Dictionary<string, object>() {
                                        { "accepted", accepted },
                                        { "reasonCode", reasonCode },
                                        { "ClientIP", clientIP }
                                    });
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Demo Bid Error", "RealTimeController", System.Diagnostics.TraceEventType.Warning, null, e);
            }
        }

        /// <summary>
        /// Retrieves a page of Events, excluding Deleted and Archived
        /// </summary>
        /// <param name="page">0-based page index</param>
        /// <param name="pageSize">the maximum results to return</param>
        /// <returns>
        ///     JSON encoded result:
        ///     {
        ///       PageCount = n,
        ///       Items = [
        ///         { EventID = n1, EventTitle = &quot;x1&quot;, EndDTTM = &quot;dttm1&quot;, Status = &quot;y1&quot; },
        ///         { EventID = n2, EventTitle = &quot;x2&quot;, EndDTTM = &quot;dttm2&quot;, Status = &quot;y2&quot; },
        ///         { ... }
        ///       ]
        ///     }
        /// </returns>
        //[Authorize(Roles = Strings.Roles.Admin)]
        public JsonResult GetAllEvents(int? page, int? pageSize)
        {
            JsonResult result = new JsonResult();
            try
            {
                string statuses = AuctionEventStatuses.Scheduled
                    + "," + AuctionEventStatuses.Preview
                    + "," + AuctionEventStatuses.Active 
                    + "," + AuctionEventStatuses.Closing 
                    + "," + AuctionEventStatuses.Closed;
                var pageOfEvents = EventClient.GetEventsByStatusWithFillLevel(User.Identity.Name, statuses, page ?? 0, pageSize ?? 10, Fields.EndDTTM, true, EventFillLevels.None);
                result.Data = new
                {
                    PageCount = pageOfEvents.TotalPageCount,
                    Items =  pageOfEvents.List
                                .Select(e => new { EventID = e.ID, EventTitle = e.Title, e.EndDTTM, e.Status })
                                .OrderByDescending(ev => ev.EndDTTM)
                                .ToArray()
                };
            }
            catch (Exception e)
            {
                result.Data = new { error = e.Message };
            }
            return result;
        }

        /// <summary>
        /// Inserts a log entry with the specified details
        /// </summary>
        /// <param name="message">the message to log</param>
        /// <param name="title">the title to log</param>
        /// <param name="type">optional log entry type; default: Warning; valid values: Error, Critical, Information, Resume, Start, Stop, Suspend, Transfer, Verbose</param>
        public JsonResult LogError(string message, string title, string type)
        {
            JsonResult result = new JsonResult();
            try
            {
                var propertiestoLog = new Dictionary<string, object>();
                foreach (string formKey in Request.Form.AllKeys.Where(
                    k => k != null &&
                         !k.Equals("title", StringComparison.OrdinalIgnoreCase) &&
                         !k.Equals("message", StringComparison.OrdinalIgnoreCase) &&
                         !k.Equals("verbose", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrWhiteSpace(Request[k])))
                {
                    propertiestoLog.Add(formKey, Request[formKey]);
                }
                System.Diagnostics.TraceEventType logEntryType;
                switch (type)
                {
                    case "Critical": logEntryType = System.Diagnostics.TraceEventType.Critical; break;
                    case "Error": logEntryType = System.Diagnostics.TraceEventType.Error; break;
                    case "Information": logEntryType = System.Diagnostics.TraceEventType.Information; break;
                    case "Resume": logEntryType = System.Diagnostics.TraceEventType.Resume; break;
                    case "Start": logEntryType = System.Diagnostics.TraceEventType.Start; break;
                    case "Stop": logEntryType = System.Diagnostics.TraceEventType.Stop; break;
                    case "Suspend": logEntryType = System.Diagnostics.TraceEventType.Suspend; break;
                    case "Transfer": logEntryType = System.Diagnostics.TraceEventType.Transfer; break;
                    case "Verbose": logEntryType = System.Diagnostics.TraceEventType.Verbose; break;
                    default: logEntryType = System.Diagnostics.TraceEventType.Warning; break;
                }
                LogManager.WriteLog(message ?? "An unexpected error occurred", title ?? "Realtime Error", "SignalR",
                    logEntryType, User.Identity.IsAuthenticated ? User.Identity.Name : string.Empty, null, propertiestoLog);
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error logging failure", "SignalR", System.Diagnostics.TraceEventType.Error,
                    User.Identity.IsAuthenticated ? User.Identity.Name : string.Empty, e);
            }
            return result;
        }

        /// <summary>
        /// Retrieves the lot ID for the specified event ID which is closing next
        /// </summary>
        /// <param name="eventId">the id of the event to get increments for</param>
        /// <returns>
        ///     JSON encoded result { { LotId = x } }
        /// </returns>
        public JsonResult GetDraftLotCount(int eventId)
        {
            JsonResult result = new JsonResult();
            int draftLotCount = 0;
            try
            {
                draftLotCount = EventClient.GetLotCountByListingStatus(string.Empty, eventId, ListingStatuses.Draft);
            }
            catch (Exception)
            {
                //no need to handle this exception
            }
            result.Data = new
            {
                DraftLotCount = draftLotCount
            };
            return result;
        }

        /// <summary>
        /// Returns true if the specified invoice is payable
        /// </summary>
        /// <param name="invoiceId">the ID of the speicified invoice</param>
        public JsonResult IsInvoicePayable(int invoiceId)
        {
            JsonResult result = new JsonResult();
            bool isPayable = false;
            try
            {
                var invoice = AccountingClient.GetInvoiceByID(SystemActors.SystemUserName, invoiceId);
                //if the invoice status is "Paid" or "Pending" then it is not payable, and further checks are not needed
                if (invoice.Status != InvoiceStatuses.Paid && invoice.Status != InvoiceStatuses.Pending)
                {
                    isPayable = AccountingClient.GetPaymentProviderViewsForInvoice(User.Identity.Name, invoice).Count > 0;
                }
            }
            catch (Exception)
            {
                //no need to handle this exception
            }
            result.Data = new
            {
                isPayable
            };
            return result;
        }

        /// <summary>
        /// Retrieves Server Time data used to sync browser time to server time
        /// </summary>
        /// <returns>the JSON-encoded data required for proper NTP-style time sync</returns>
        [SkipActiveUserCheck]
        public JsonResult ServerTimeSync(long clientTime)
        {
            //inspired by this article: https://stackoverflow.com/questions/1638337/the-best-way-to-synchronize-client-side-javascript-clock-with-server-date

            DateTime utcNow = DateTime.UtcNow;
            //long clientTime = long.Parse(Request.Form["ct"]);
            long serverTimestamp = (utcNow.Ticks - (new DateTime(1970, 1, 1) - DateTime.MinValue).Ticks) / 10000;
            long serverClientRequestDiffTime = serverTimestamp - clientTime;
            //Response.Write("{\"diff\":" + serverClientRequestDiffTime + ",\"serverTimestamp\":" + serverTimestamp + "}");

            return new JsonResult()
            {
                Data = new
                {
                    diff = serverClientRequestDiffTime,
                    serverTimestamp,
                    siteNowTimeString = utcNow.ToLocalDTTM().ToString("yyyy-MM-ddTHH:mm:ss.fff")
                }
            };
        }

    }
}
