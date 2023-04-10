using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Models;
using System.Web.UI.WebControls;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaSaver;
using RainWorx.FrameWorx.Providers.MediaLoader;
using Attribute = RainWorx.FrameWorx.DTO.Attribute;
using System.Reflection;
using System.ServiceModel;
using Image = System.Drawing.Image;

namespace RainWorx.FrameWorx.MVC.Controllers
{

    /// <summary>
    /// Provides methods that respond to listing-specific MVC requests
    /// </summary>
    [GoUnsecure]
    [Authenticate]
    public class ListingController : AuctionWorxController
    {

        #region Browse / Search

        /// <summary>
        /// Redirects to default view for Listing area
        /// </summary>
        /// <returns>Redirect to /Listing/Browse</returns>
        public ActionResult Index(/*string breadcrumbs, int? page, int? SortFilterOptions*/)
        {
            /*
            var routeValues = new {
                                      controller = Strings.MVC.ListingController, 
                                      action = Strings.MVC.BrowseAction,
                                      breadcrumbs, page, SortFilterOptions
                                  };
            return new MVCTransferResult(routeValues, this.HttpContext);
            */
            return RedirectToAction(Strings.MVC.BrowseAction);
        }

        /// <summary>
        /// Displays a page of a list of listings
        /// </summary>
        /// <param name="breadcrumbs">list of applicable category data, formatted for SEO</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of the requested sort option defined in QuerySortDefinitions.BrowseOptions</param>
        /// <param name="ViewStyle">&quot;list&quot; (default) or &quot;grid&quot;</param>
        /// <param name="StatusFilter">&quot;active_only&quot; (default), &quot;completed_only&quot; or &quot;all&quot;</param>
        /// <returns>
        ///     (success)   Page&lt;Listing&gt;
        ///     (errors)    Redirect to /Listing/Search/[applicable route values]
        /// </returns>
        //[OutputCache(NoStore = true, Duration = 0)]
        public ActionResult Browse(string breadcrumbs, int? page, int? SortFilterOptions, string ViewStyle, string StatusFilter)
        {
            //do not allow browsers to store stale copies of this page, especially for browser back button use
            Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate"); // HTTP 1.1.
            Response.AddHeader("Pragma", "no-cache"); // HTTP 1.0.
            Response.AddHeader("Expires", "0"); // Proxies.

            if (Request.QueryString.AllKeys.Contains("FullTextQuery"))
            {
                //ViewData["FullTextQuery"] = Request.QueryString["FullTextQuery"];
                if (!ModelState.ContainsKey("FullTextQuery"))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(Request.QueryString["FullTextQuery"], Request.QueryString["FullTextQuery"], null);
                    ModelState.Add("FullTextQuery", ms);
                }
            }

            if (Request.QueryString.AllKeys.Contains("CategoryID"))
            {
                int catId;
                if (int.TryParse(Request.QueryString["CategoryID"], out catId))
                {
                    if (catId != 9) ViewData["CategoryID"] = catId;
                }
            }

            if (Request.QueryString.AllKeys.Contains("RegionID"))
            {
                int regId;
                if (int.TryParse(Request.QueryString["RegionID"], out regId))
                {
                    if (regId != 27) ViewData["RegionID"] = regId;
                }
            }

            //capture SortFilterOptions
            var defaultSortOption = QuerySortDefinitions.BrowseOptions.FirstOrDefault(lpq => lpq.Name == SiteClient.TextSetting(Strings.SiteProperties.DefaultBrowseSort));
            SortFilterOptions = SortFilterOptions ?? (defaultSortOption != null ? defaultSortOption.Index : 0);

            //capture/parse ViewStyle
            if (ViewStyle == null || (ViewStyle.ToLower() != "grid" && ViewStyle.ToLower() != "list"))
            {
                ViewData["ViewStyle"] = SiteClient.TextSetting(SiteProperties.DefaultBrowseStyle);
            }
            else
            {
                ViewData["ViewStyle"] = ViewStyle;
            }

            //capture/parse StatusFilter
            if (string.IsNullOrEmpty(StatusFilter))
            {
                ViewData["StatusFilter"] = "active_only";
            }
            else if (StatusFilter.ToLower() == "completed_only")
            {
                ViewData["StatusFilter"] = "completed_only";
            }
            else if (StatusFilter.ToLower() == "all")
            {
                ViewData["StatusFilter"] = "all";
            }
            else
            {
                ViewData["StatusFilter"] = "active_only";
            }

            //set defaults
            breadcrumbs = breadcrumbs ?? string.Empty;
            page = page ?? 0;

            bool countsValid = true;
            string seller = string.Empty;

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie),
                                            this.GetCookie(Strings.MVC.CultureCookie));
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                input.Items.Add(key,
                                    Request.QueryString[key] == Strings.MVC.TrueFormValue
                                        ? Strings.MVC.TrueValue
                                        : Request.QueryString[key].Trim());

                if (countsValid &&
                    !key.StartsWith("SortFilterOptions", StringComparison.OrdinalIgnoreCase) && //added because Browse page SortFilterOptions don't currently Filter at all... so results are same, counts valid...
                    !(key.StartsWith("StatusFilter", StringComparison.OrdinalIgnoreCase) && Request.QueryString[key].Equals("active_only", StringComparison.OrdinalIgnoreCase)) &&
                    !(key.StartsWith("ListingType", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Request.QueryString[key])) && // blank value here doesn't invalidate counts
                    !key.StartsWith("ViewStyle", StringComparison.OrdinalIgnoreCase) &&
                    !key.StartsWith("page", StringComparison.OrdinalIgnoreCase) &&
                    !key.StartsWith("seller", StringComparison.OrdinalIgnoreCase) &&
                    !key.StartsWith("CategoryID", StringComparison.OrdinalIgnoreCase) &&
                    !(key.StartsWith("CompletedListings", StringComparison.OrdinalIgnoreCase) && Request.QueryString[key].Equals("false", StringComparison.OrdinalIgnoreCase)))
                {
                    countsValid = false; //counts are invalid if any query string parameter was found other than "page", "seller", "CategoryID", OR "CompletedListings=false" (CompletedListings=true would be invalid)
                }

                if (key.StartsWith("seller", StringComparison.OrdinalIgnoreCase))
                {
                    seller = Request.QueryString[key];
                }
            }
            input.Items[Strings.Fields.BreadCrumbs] = breadcrumbs;

            string effectiveStatuses;
            switch ((string)ViewData["StatusFilter"])
            {
                case "completed_only":
                    effectiveStatuses = Strings.ListingStatuses.Ended + "," +
                                        Strings.ListingStatuses.Successful + "," +
                                        Strings.ListingStatuses.Unsuccessful;
                    break;
                case "all":
                    effectiveStatuses = Strings.ListingStatuses.Active + "," +
                                        Strings.ListingStatuses.Ended + "," +
                                        Strings.ListingStatuses.Successful + "," +
                                        Strings.ListingStatuses.Unsuccessful + "," +
                                        Strings.ListingStatuses.Preview;
                    break;
                default: // "active_only" or missing/invalid value
                    effectiveStatuses = Strings.ListingStatuses.Active + "," +
                                        Strings.ListingStatuses.Preview;
                    break;
            }
            input.Items[Strings.Fields.Statuses] = effectiveStatuses;

            //decode breadcrumbs for navigational controls
            List<Category> bannerCats = new List<Category>();
            this.DecodeBreadCrumbs(breadcrumbs, bannerCats);
            ViewData["BannerCats"] = bannerCats;

            //get meta data
            int categoryID = breadcrumbs.LastIndexOf(Strings.BreadCrumbPrefixes.Category);
            if (categoryID >= 0)
            {
                string firstIntFound = string.Empty;
                string temp = breadcrumbs.Substring(categoryID + 1);
                for (int i = 0; i < temp.Length; i++)
                {
                    string nextLetter = temp.Substring(i, 1);
                    if ("1234567890".Contains(nextLetter))
                    {
                        firstIntFound += nextLetter;
                    }
                    else
                    {
                        break;
                    }
                }
                int listingCatId = int.Parse(firstIntFound);
                Category selectedCategory = CommonClient.GetCategoryByID(listingCatId);
                if (selectedCategory != null)
                {
                    ViewData["MetaKeywords"] = selectedCategory.MetaKeywords;
                    ViewData["MetaDescription"] = selectedCategory.MetaDescription;
                    ViewData["PageTitle"] = selectedCategory.PageTitle;
                    ViewData["PageContent"] = selectedCategory.PageContent;
                    ViewData["CategoryName"] = selectedCategory.Name;
                }
            }
            else
            {
                Category rootCategory = CommonClient.GetCategoryByID(9);
                ViewData["MetaKeywords"] = rootCategory.MetaKeywords;
                ViewData["MetaDescription"] = rootCategory.MetaDescription;
                ViewData["PageTitle"] = rootCategory.PageTitle;
                ViewData["PageContent"] = rootCategory.PageContent;
                ViewData["CategoryName"] = string.Empty;
            }

            //prepare ListingType drop down box
            List<ListingType> listingTypes = null;
            if (categoryID > 0)
            {
                listingTypes = ListingClient.GetValidListingTypesForCategory(categoryID > 0 ? categoryID : 9);
            }
            if (listingTypes == null || listingTypes.Count == 0)
            {
                listingTypes = ListingClient.ListingTypes.Where(lt => lt.Enabled).ToList();
            }
            var listingTypeOptions = new List<SelectListItem>();
            listingTypeOptions.Add(new SelectListItem { Text = this.GlobalResourceString("All"), Value = string.Empty });
            string selectedListingType = null;
            foreach (ListingType listingType in listingTypes)
            {
                listingTypeOptions.Add(new SelectListItem { Text = this.GlobalResourceString(listingType.Name), Value = listingType.Name });
                if (input.Items.ContainsKey(Strings.Fields.ListingType) && ((string)input.Items[Strings.Fields.ListingType]).Contains(listingType.Name))
                {
                    selectedListingType = listingType.Name;
                }
            }
            ViewData[Strings.Fields.ListingType] = new SelectList(listingTypeOptions, "Value", "Text", selectedListingType);

            //prepare sortfilter drop down box            
            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BrowseOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //merge query options
            bool validCategoryCounts;
            ListingPageQuery currentQuery = QuerySortDefinitions.MergeBrowseOptions(SortFilterOptions.Value,
                                                                                    input,
                                                                                    out validCategoryCounts);

            //Check if counts would be valid...
            bool getRealTimeCatCounts = false;
            ViewData["ValidCategoryCounts"] = countsValid && validCategoryCounts;
            if ((bool)ViewData["ValidCategoryCounts"])
            {
                //if so, calculate counts
                //decode breadcrumb string for category counts
                List<string> fixIDs = new List<string>();
                if (!string.IsNullOrEmpty(breadcrumbs))
                {
                    fixIDs = new List<string>(breadcrumbs.Split('-'));
                    fixIDs = fixIDs.Select(s => s.Substring(1)).ToList();
                }
                //get actual counts
                ViewData[Strings.MVC.ViewData_CategoryCounts] = CommonClient.GetCategoryCounts(fixIDs, effectiveStatuses, seller);
            }
            else if (SiteClient.BoolSetting(SiteProperties.GetCatCountsForAllSearches))
            {
                getRealTimeCatCounts = true;
                ViewData["ValidCategoryCounts"] = true;
            }

            string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Decorations;
            fillLevel += "," + ListingFillLevels.Properties;
            if (SiteClient.BoolSetting(SiteProperties.ShowShippingInfoOnItemLists))
            {
                fillLevel += "," + ListingFillLevels.Shipping;
            }

            bool quickBidForListViewsEnabled = false;
            //determine if inline bidding is enabled, which requires "CurrentAction" and "Actions" to be filled for each listing, in order to give authenticated user their current context (e.g. "Winning" "Not Winning" etc)
            List<CustomProperty> auctionProperties = ListingClient.GetListingTypeProperties(ListingTypes.Auction, "Site");
            var quickBidForListViewsProp = auctionProperties.FirstOrDefault(p => p.Field.Name == SiteProperties.QuickBidForListViewsEnabled);
            if (quickBidForListViewsProp != null)
            {
                bool.TryParse(quickBidForListViewsProp.Value, out quickBidForListViewsEnabled);
            }
            if (quickBidForListViewsEnabled)
            {
                fillLevel += "," + ListingFillLevels.CurrentAction;
                if (User.Identity.IsAuthenticated)
                {
                    fillLevel += "," + ListingFillLevels.Actions;
                }
            }

            //perform search with combined query                          
            try
            {
                Page<Listing> results;
                Dictionary<int, int> rtCatcounts;
                if (User.Identity.IsAuthenticated && quickBidForListViewsEnabled)
                {
                    results = ListingClient.SearchListingsWithFillLevelAndContext(User.Identity.Name, currentQuery, page.Value, SiteClient.PageSize, fillLevel, this.FBOUserName(), getRealTimeCatCounts, out rtCatcounts);
                    if (getRealTimeCatCounts) ViewData[Strings.MVC.ViewData_CategoryCounts] = rtCatcounts;
                }
                else
                {
                    results = ListingClient.SearchListingsWithFillLevel(User.Identity.Name, currentQuery, page.Value, SiteClient.PageSize, fillLevel, getRealTimeCatCounts, out rtCatcounts);
                    if (getRealTimeCatCounts) ViewData[Strings.MVC.ViewData_CategoryCounts] = rtCatcounts;
                }

                /*
                  //example usage of passing additional parameter(s) directly to the RWX_SearchListings stored procedure
                  List<KeyValuePair<string, string>> extraParams = new List<KeyValuePair<string, string>>(1);
                  extraParams.Add(new KeyValuePair<string, string>("excludeKeywords", "exclude results matching this keyword")); //exclude all listings that contain the specified keyword(s)
                  Page<Listing> results = ListingClient.SearchListingsWithAdditionalParameters(User.Identity.Name, currentQuery, extraParams, page.Value, SiteClient.PageSize);
                */

                return View(results);
            }
            catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
            {
                UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                userInput.AddAllQueryStringValues(this);
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return View(Strings.MVC.SearchAction);
            }
        }

        /// <summary>
        /// Displays form and processes request to search for listings with a veriety of filters and options
        /// </summary>
        /// <returns>
        ///     (success)               Redirect to /Listing/Browse/[applicable route values]
        ///     (validation errors)     View()
        /// </returns>
        public ActionResult Search()
        {
            if (Request.QueryString.Count <= 0 || Request.QueryString.AllKeys.Contains("RefineSearch"))
            {
                var input = new UserInput(User.Identity.Name, this.FBOUserName());
                input.AddAllQueryStringValues(this); // adds all querystring items into the current ModelState
                int catId;
                if (input.Items.ContainsKey("CategoryID") && int.TryParse(input.Items["CategoryID"], out catId))
                {
                    if (catId != 9) ViewData["CategoryID"] = catId;
                }
                int regId;
                if (input.Items.ContainsKey("RegionID") && int.TryParse(input.Items["RegionID"], out regId))
                {
                    if (regId != 27) ViewData["RegionID"] = regId;
                }
                return View();
            }

            RouteValueDictionary routes = new RouteValueDictionary();
            string breadcrumbs = string.Empty;
            string extra = string.Empty;
            foreach (string key in Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (key.StartsWith("CategoryID") && !string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    int categoryId;
                    if (int.TryParse(Request.QueryString[key], out categoryId))
                    {
                        foreach (Category cat in CommonClient.GetCategoryPath(categoryId).Descendents.Where(c => c.ID != 9))
                        {
                            breadcrumbs = string.IsNullOrEmpty(breadcrumbs)
                                              ? "C" + cat.ID
                                              : "C" + cat.ID + "-" + breadcrumbs;
                            extra = string.IsNullOrEmpty(extra)
                                         ? cat.Name.SimplifyForURL("-")
                                         : cat.Name.SimplifyForURL("-") + "-" + extra;
                        }
                    }
                }
                else if (key.StartsWith("RegionID") && !string.IsNullOrEmpty(Request.QueryString[key]))
                {
                    int regionId;
                    if (int.TryParse(Request.QueryString[key], out regionId))
                    {
                        foreach (Category cat in CommonClient.GetCategoryPath(regionId).Descendents.Where(c => c.ID != 27))
                        {
                            breadcrumbs = string.IsNullOrEmpty(breadcrumbs)
                                              ? "R" + cat.ID
                                              : "R" + cat.ID + "-" + breadcrumbs;
                            extra = string.IsNullOrEmpty(extra)
                                         ? cat.Name.SimplifyForURL("-")
                                         : cat.Name.SimplifyForURL("-") + "-" + extra;
                        }
                    }
                }
                if (!key.StartsWith("selectFor") && !key.StartsWith("Search") && !key.StartsWith("page") && !key.StartsWith("FullTextQuery"))
                {
                    routes.Add(key,
                               Request.QueryString[key].Equals(Strings.MVC.TrueFormValue)
                                   ? Strings.MVC.TrueValue
                                   : Request.QueryString[key]);
                }
                else if (key.StartsWith("FullTextQuery"))
                {
                    int possibleListingId;
                    if (int.TryParse(Request.QueryString[key], out possibleListingId))
                    {
                        //an int, but is it a listing id?
                        try
                        {
                            ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, possibleListingId, string.Empty);
                            //a listing, show it immediately
                            return RedirectToAction(Strings.MVC.DetailsAction,
                                                    new RouteValueDictionary() { { "id", possibleListingId } });
                        }
                        catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract>)
                        {
                            //not a listing, or error, proceed...
                            routes.Add(key, Request.QueryString[key]);
                        }
                    }
                    else
                    {
                        //not an int, proceed...
                        routes.Add(key, Request.QueryString[key]);
                    }
                }
            }
            if (!string.IsNullOrEmpty(breadcrumbs))
            {
                routes.Add("breadcrumbs", breadcrumbs);
                routes.Add("extra", extra);
            }


            //if (TempData["Validation"] != null)
            //{
            //    //display validation errors
            //    System.ServiceModel.FaultException<ValidationFaultContract> vfc =
            //        (System.ServiceModel.FaultException<ValidationFaultContract>)TempData["Validation"];
            //    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
            //    {
            //        ModelState.AddModelError(issue.Key, issue.Message);
            //    }

            //    foreach (string key in Request.QueryString.AllKeys.Where(k => k!= null))
            //    {
            //        if (!ModelState.ContainsKey(key))
            //        {
            //            //...add it to the model
            //            ModelState ms = new ModelState();
            //            ms.Value = new ValueProviderResult(Request.QueryString[key], Request.QueryString[key], null);
            //            ModelState.Add(key, ms);
            //        }
            //    }

            //    return View();
            //}
            if (!this.ModelState.IsValid)
            {
                return View();
            }

            return RedirectToAction("Browse", routes);
        }

        #endregion

        #region Listing Details

        /// <summary>
        /// displays a listing confirmation view for the specified listing
        /// </summary>
        /// <param name="id">ID of the specified listing</param>
        /// <returns></returns>
        public ActionResult ListingConfirmation(int? id)
        {
            if (id.HasValue)
            {
                try
                {
                    string fillLevel = ListingFillLevels.Default;
                    Listing currentListing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, id.Value, fillLevel);
                    //Listing currentListing = ListingClient.GetListingByID(User.Identity.Name, id.Value);

                    if (currentListing.Status.Equals(Strings.ListingStatuses.Deleted))
                    {
                        return NotFound();
                    }
                    else
                    {
                        //get listing type-specific properties
                        List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(currentListing.Type.Name, "Site");
                        //string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.ListingDurationOptions).First().Value;
                        //ViewData[Strings.Fields.ListingDurationOptions] = durationOpts;
                        bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
                        ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;
                        //bool youtubeInputEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.EnableYoutubeInput).First().Value);
                        //ViewData[Strings.Fields.EnableYoutubeInput] = youtubeInputEnabled;
                        //string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.DurationDaysList).First().Value;

                        //get primary listing and region categories to determine banner selection
                        List<Category> bannerCats = new List<Category>();
                        bannerCats.Add(currentListing.PrimaryCategory);
                        var regions = currentListing.Categories.Where(c => c.Type == Strings.CategoryTypes.Region);
                        int parentRegionId = 27; // root region category
                        Category regionCat = regions.Where(c => c.ParentCategoryID == parentRegionId).FirstOrDefault();
                        while (regionCat != null)
                        {
                            parentRegionId = regionCat.ID;
                            regionCat = regions.Where(c => c.ParentCategoryID == parentRegionId).FirstOrDefault();
                        }
                        regionCat = regions.Where(c => c.ID == parentRegionId).FirstOrDefault();
                        if (regionCat != null) bannerCats.Add(regionCat);
                        ViewData["BannerCats"] = bannerCats;

                        return View(currentListing);
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                }
            }
            //return RedirectToAction(Strings.MVC.NotFoundAction, Strings.MVC.ListingController, new { id  });
            //return new MVCTransferResult(new { controller = Strings.MVC.ListingController, action = Strings.MVC.NotFoundAction }, this.HttpContext);
            return NotFound();
        }

        /// <summary>
        /// Displays detailed listing view
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <returns>View(Listing)</returns>
        //[ImportModelStateFromTempData]
        public ActionResult Details(int? id)
        {
            //do not allow browsers to store stale copies of this page, especially for browser back button use
            Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate"); // HTTP 1.1.
            Response.AddHeader("Pragma", "no-cache"); // HTTP 1.0.
            Response.AddHeader("Expires", "0"); // Proxies.

            if (id.HasValue)
            {
                try
                {
                    Listing currentListing = ListingClient.GetListingByIDAndUserWithFillLevel(User.Identity.Name, id.Value,
                                                                          this.FBOUserName(), Strings.ListingFillLevels.Default);

                    //if this listing is awaiting payment or a draft and it's not the owner or an admin, return a "Not Found" result
                    if (currentListing != null && (currentListing.Status == ListingStatuses.AwaitingPayment || currentListing.Status == ListingStatuses.Draft))
                    {
                        if (currentListing.OwnerUserName != this.FBOUserName())
                        {
                            if (!User.IsInRole(Roles.Admin))
                            {
                                return NotFound();
                            }
                        }
                    }

                    if (SiteClient.EnableEvents && currentListing.Lot != null)
                    {
                        return RedirectToAction(Strings.MVC.LotDetailsAction, Strings.MVC.EventController, new { id = currentListing.Lot.ID });
                    }

                    if (currentListing.OfferCount > 0)
                    {
                        ViewData["AllOffers"] = ListingClient.GetOffersByListingId(User.Identity.Name, currentListing.ID);
                    }
                    else
                    {
                        ViewData["AllOffers"] = new List<Offer>(0);
                    }

                    bool enableListingHitCounts = false;
                    bool.TryParse(ConfigurationManager.AppSettings["EnableListingHitCounts"], out enableListingHitCounts);
                    ViewData["EnableListingHitCounts"] = enableListingHitCounts;

                    if (!currentListing.Properties.Any(p => p.Field.Name.Equals("ReservePrice")))
                    {
                        ViewData["ReserveStatus"] = "NA";
                    }
                    else if (currentListing.Properties.GetPropertyValue("ReservePrice", 0.0M) == 0.0M)
                    {
                        ViewData["ReserveStatus"] = "NoReserve";
                    }
                    else if ((currentListing.CurrentPrice ?? 0.0M) >=
                        currentListing.Properties.GetPropertyValue("ReservePrice", 0.0M))
                    {
                        ViewData["ReserveStatus"] = "ReserveMet";
                    }
                    else
                    {
                        ViewData["ReserveStatus"] = "ReserveNotMet";
                    }

                    PruneListingCustomFieldsVisbility(currentListing);

                    if (currentListing.Status.Equals(Strings.ListingStatuses.Deleted))
                    {
                        return NotFound();
                    }
                    else
                    {
                        //get listing type-specific properties
                        List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(currentListing.Type.Name, "Site");
                        //string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.ListingDurationOptions).First().Value;
                        //ViewData[Strings.Fields.ListingDurationOptions] = durationOpts;
                        bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
                        ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;
                        //bool youtubeInputEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.EnableYoutubeInput).First().Value);
                        //ViewData[Strings.Fields.EnableYoutubeInput] = youtubeInputEnabled;
                        //string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.Fields.DurationDaysList).First().Value;

                        //get primary listing and region categories to determine banner selection
                        List<Category> bannerCats = new List<Category>();
                        bannerCats.Add(currentListing.PrimaryCategory);
                        var regions = currentListing.Categories.Where(c => c.Type == Strings.CategoryTypes.Region);
                        int parentRegionId = 27; // root region category
                        Category regionCat = regions.Where(c => c.ParentCategoryID == parentRegionId).FirstOrDefault();
                        while (regionCat != null)
                        {
                            parentRegionId = regionCat.ID;
                            regionCat = regions.Where(c => c.ParentCategoryID == parentRegionId).FirstOrDefault();
                        }
                        regionCat = regions.Where(c => c.ID == parentRegionId).FirstOrDefault();
                        if (regionCat != null) bannerCats.Add(regionCat);
                        ViewData["BannerCats"] = bannerCats;

                        //get seller location details
                        bool showSellerLocation = false;
                        CustomProperty siteProp = SiteClient.Properties.Where(
                            p => p.Field.Name == Strings.SiteProperties.ShowSellerLocationOnListingDetails).FirstOrDefault();
                        if (siteProp != null)
                        {
                            bool.TryParse(siteProp.Value, out showSellerLocation);
                        }
                        if (showSellerLocation)
                        {
                            Address sellerAddress = UserClient.GetAddresses(currentListing.OwnerUserName, currentListing.Owner.UserName).Where(
                                a => a.ID == currentListing.Owner.PrimaryAddressID).SingleOrDefault();
                            if (sellerAddress != null)
                            {
                                ViewData["SellerLocation"] = sellerAddress.City + ", " + sellerAddress.StateRegion + " " + sellerAddress.Country.Code;
                            }
                            else
                            {
                                ViewData["SellerLocation"] = string.Empty;
                            }

                        }

                        //determine final buyer fee details
                        decimal finalFeeMin;
                        decimal finalFeeMax;
                        List<Tier> finalFeeTiers;
                        string finalFeeDescription;
                        GetFinalBuyerFeeRanges(currentListing.Type.Name, currentListing.Categories, out finalFeeMin, out finalFeeMax, out finalFeeTiers, out finalFeeDescription);
                        ViewData[Strings.MVC.ViewData_MinFinalBuyerFee] = finalFeeMin;
                        ViewData[Strings.MVC.ViewData_MaxFinalBuyerFee] = finalFeeMax;
                        ViewData[Strings.MVC.ViewData_FinalBuyerFeeTiers] = finalFeeTiers;
                        ViewData[Strings.MVC.ViewData_FinalBuyerFeeDescription] = finalFeeDescription;

                        //if this listing ended successfully or has associated purchases then retrieve all existing invoices
                        List<Invoice> invoices = null;
                        List<LineItem> purchases = null;
                        bool checkInvoices = false;
                        if (User.Identity.IsAuthenticated)
                        {
                            if (currentListing.Status == ListingStatuses.Successful)
                            {
                                checkInvoices = true;
                            }
                            else if (currentListing.Type.Name == ListingTypes.FixedPrice)
                            {
                                if (currentListing.AcceptedListingActionCount() > 0 || currentListing.OfferCount > 0)
                                {
                                    checkInvoices = true;
                                }
                            }
                        }
                        if (checkInvoices)
                        {
                            //retrieve all existing invoices for this listing
                            invoices = AccountingClient.GetInvoicesBySeller(currentListing.OwnerUserName, currentListing.Owner.UserName,
                                "All", currentListing.ID.ToString(), "ListingID", null, 0, 0, "CreatedDTTM", true).List;

                            if (currentListing.OwnerAllowsInstantCheckout() || SiteClient.BoolSetting(SiteProperties.AutoGenerateInvoices))
                            {
                                ListingPageQuery currentQuery = QuerySortDefinitions.BidWonOptions[0];
                                purchases = AccountingClient.GetListingLineItemsByPayer(User.Identity.Name, this.FBOUserName(),
                                    "NeedInvoice", currentListing.ID.ToString(), "ListingID",
                                    0, 0, currentQuery.Sort, currentQuery.Descending).List;
                            }
                        }
                        ViewData["AllInvoices"] = invoices ?? new List<Invoice>();
                        ViewData["UninvoicedPurchases"] = purchases ?? new List<LineItem>();

                        //CheckValidationIssues();

                        return View(Strings.MVC.DetailsAction, currentListing);
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                }
            }
            //return RedirectToAction(Strings.MVC.NotFoundAction, Strings.MVC.ListingController, new { id  });
            //return new MVCTransferResult(new { controller = Strings.MVC.ListingController, action = Strings.MVC.NotFoundAction }, this.HttpContext);
            return NotFound();
        }

        /// <summary>
        /// Displays "Listing not found" error message.
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult NotFound()
        {
            return View(Strings.MVC.NotFoundAction);
        }

        #endregion

        #region Create Listing

        /// <summary>
        /// Displays "You do not currently have permission to post listings..." error message.
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult NoPermissionToSell()
        {
            return View();
        }

        /// <summary>
        /// Displays form to enter initial listing details (page 1 of 2)
        /// </summary>
        /// <returns>
        ///     (not logged in)     Redirect to /Account/LogOn/...
        ///     (not a seller)      Redirect to /Listing/NoPermissionToSell
        ///     (success)           View()
        /// </returns>        
        public ActionResult CreateListingPage1(int? SimilarListingID, string ReturnUrl)
        {
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            if (HttpContext.User.Identity.IsAuthenticated)
            {
                //if logged in...
                if (!HttpContext.User.IsInRole(Strings.Roles.Admin) && !HttpContext.User.IsInRole(Strings.Roles.Seller))
                {
                    //if not admin nor seller...
                    return RedirectToAction("NoPermissionToSell");
                }
            }
            else
            {
                //if not logged in... send to login form
                UrlHelper urlHelper = new UrlHelper(Request.RequestContext);
                string loginRetUrl = urlHelper.Action(Strings.MVC.CreateListingPage1Action, new { SimilarListingID, ReturnUrl });
                //return RedirectToAction("LogOn", "Account", new { returnURL = VirtualPathUtility.ToAbsolute("~/") + "Listing/CreateListingPage1" });
                return RedirectToAction("LogOn", "Account", new { returnURL = loginRetUrl });
            }

            if (SiteClient.PayToProceed && !User.IsInRole("Admin"))
            {
                Invoice invoice = AccountingClient.GetPayerFees(User.Identity.Name, this.FBOUserName());
                if (invoice != null && invoice.Total > 0.00M)
                {
                    PrepareErrorMessage("FeePaymentRequiredToCreateListing", MessageType.Message);

                    // unpaid fees exist – do redirect
                    return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController);
                }
            }

            var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            bool isCcReqExempt = (loggedOnUser != null && loggedOnUser.Properties != null)
                ? loggedOnUser.Properties.GetPropertyValue(StdUserProps.CreditCardRequiredExempt, false) : false;
            if (!isCcReqExempt && SiteClient.BoolSetting(SiteProperties.RequireCreditCardForSellers) && !User.IsInRole("Admin") && !this.HasUnexpiredCardOnFile(null, this.FBOUserName()))
            {
                PrepareNeutralMessage(Messages.CreditCardRequiredForSellers);
                return RedirectToAction(Strings.MVC.AddCardAction, Strings.MVC.AccountController, new { returnUrl = Request.Url.PathAndQuery });
            }

            //if creating a similar listing, get the appropriate value and redirect to CreateListingPage2
            if (SimilarListingID.HasValue)
            {
                try
                {
                    Listing sourceListing = ListingClient.GetListingByIDWithFillLevel(this.FBOUserName(), SimilarListingID.Value, ListingFillLevels.Categories + "," + ListingFillLevels.PrimaryCategory);
                    if (sourceListing.OwnerUserName != this.FBOUserName())
                    {
                        //note - an attempt by any user other than the owner to list a similar item
                        //  intentionally just shows a "Listing not found" error.
                        ModelState.AddModelError("SimilarListingID", "ListingNotFound");
                    }
                    else
                    {
                        int? categoryID = sourceListing.PrimaryCategory.ID;
                        //int? storeID = null; // not yet implemented
                        Category leafRegion = sourceListing.LeafRegion();
                        int? regionID = (leafRegion != null ? (int?)leafRegion.ID : null);
                        //int? eventID = null; // not yet implemented
                        string currencyCode = sourceListing.Currency.Code;
                        string listingType = sourceListing.Type.Name;

                        ViewData[Strings.Fields.CategoryID] = categoryID;
                        //ViewData[Strings.Fields.StoreID] = storeID;
                        ViewData[Strings.Fields.RegionID] = regionID;
                        //ViewData[Strings.Fields.EventID] = eventID;
                        ViewData[Strings.Fields.Currency] = currencyCode;
                        ViewData[Strings.Fields.ListingType] = listingType;
                        ViewData[Strings.Fields.SimilarListingID] = SimilarListingID.Value;

                        /**********************************************************************\
                         * Uncomment this block to skip page 1 when creating a similar listing
                         * 
                            TempData["SimilarListingID"] = SimilarListingID.Value;
                            return RedirectToAction("CreateListingPage2", new { 
                                CategoryID = categoryID, 
                                StoreID = storeID, 
                                RegionID = regionID, 
                                EventID = eventID, 
                                currency = currencyCode, 
                                ListingType = listingType });                                 *
                                                                                              *
                        \**********************************************************************/
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason == ReasonCode.ListingNotExist)
                    {
                        ModelState.AddModelError("SimilarListingID", "ListingNotFound");
                    }
                    else
                    {
                        PrepareErrorMessage("CreateListingPage1", iafc);
                    }
                }
                catch (Exception e)
                {
                    PrepareErrorMessage("CreateListingPage1", e);
                }
            }

            //store and event data
            ViewData[Strings.Fields.StoreID] = new SelectList(CommonClient.GetChildCategories(28), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.EventID] = new SelectList(CommonClient.GetChildCategories(29), Strings.Fields.ID, Strings.Fields.Name);
            return View();
        }


        /// <summary>
        /// Processes request to enter initial listing details (page 1 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="SimilarListingID">the optional id of the listing to prefill forms fields with</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success (after CreateLotPage2) when no fees are owed</param>
        /// <returns>
        ///     (success)   Redirect to /Listing/CreateListingPage2
        ///     (errors)    CreateListingPage1()
        /// </returns>        
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CreateListingPage1(int? CategoryID, int? StoreID, int? RegionID, int? EventID, string currency, string ListingType, int? SimilarListingID, string ReturnUrl)
        {
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            ViewData[Strings.Fields.Currency] = currency;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.SimilarListingID] = SimilarListingID;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;

            var bSkipRedirect = !string.IsNullOrEmpty(Request["FromStep2"]);
            if (!CategoryID.HasValue || string.IsNullOrEmpty(ListingType) || string.IsNullOrEmpty(currency) || bSkipRedirect)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Strings.Messages.SelectCategoryAndListingType);
                return CreateListingPage1(null, ReturnUrl);
            }
            if (!string.IsNullOrEmpty(Request["FromPage2"]))
            {
                return CreateListingPage1(null, ReturnUrl);
            }

            //if creating a similar listing, get the appropriate value and redirect to CreateListingPage2
            if (SimilarListingID.HasValue)
            {
                try
                {
                    Listing sourceListing = ListingClient.GetListingByIDWithFillLevel(this.FBOUserName(), SimilarListingID.Value, string.Empty);
                    if (sourceListing.OwnerUserName != this.FBOUserName())
                    {
                        //note - an attempt by any user other than the owner to list a similar item
                        //  intentionally just shows a "Listing not found" error.
                        ModelState.AddModelError("SimilarListingID", "ListingNotFound");
                    }
                    else
                    {
                        //pass along the listing ID to use to pre-fill the CreateListingPage2 form
                        TempData["SimilarListingID"] = SimilarListingID.Value;
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason == ReasonCode.ListingNotExist)
                    {
                        ModelState.AddModelError("SimilarListingID", "ListingNotFound");
                    }
                    else
                    {
                        PrepareErrorMessage("CreateListingPage1", iafc);
                    }
                }
                catch (Exception e)
                {
                    PrepareErrorMessage("CreateListingPage1", e);
                }
            }

            return RedirectToAction("CreateListingPage2", new { CategoryID, StoreID, RegionID, EventID, currency, ListingType, ReturnUrl });
        }

        /// <summary>
        /// Displays form to enter remaining new listing details (page 2 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success (after CreateLotPage2) when no fees are owed</param>
        /// <returns>View()</returns>        
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult CreateListingPage2(int CategoryID, int? StoreID, int? RegionID, int? EventID, string currency, string ListingType, string ReturnUrl)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(ListingType, "Site");
            string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.ListingDurationOptions).First().Value;
            ViewData[Strings.SiteProperties.ListingDurationOptions] = durationOpts;
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;
            string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.DurationDaysList).First().Value;

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Select list for Duration
            bool gtcOptionAvailable =
                listingTypeProperties.Exists(p => p.Field.Name == Strings.SiteProperties.EnableGTC)
                    ? bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableGTC).First().Value)
                    : false;
            ViewData[Strings.MVC.ViewData_GtcAvailable] = gtcOptionAvailable;
            List<object> durationOptionsList = new List<object>();
            foreach (string durOpt in durationDaysList.Split(','))
            {
                durationOptionsList.Add(new { Value = durOpt, Text = durOpt + " " + this.GlobalResource("Days") });
            }
            if (gtcOptionAvailable)
            {
                //add GoodUntilCanceled fee

                decimal goodUntilCanceledCharge = AccountingClient.GetAllFeeProperties().Where(fp => fp.Processor.Equals("RainWorx.FrameWorx.Providers.Fee.Standard.GoodUntilCanceled") &&
                                fp.Event.Name.Equals(Strings.Events.AddListing) &&
                                fp.ListingType.Name.Equals(ListingType) &&
                                fp.Name.Equals(FeeNames.GoodUntilCanceled)).SingleOrDefault().Amount;

                String s = this.GlobalResourceString("GoodTilCanceled") + " (" + this.SiteCurrencyOrFree(goodUntilCanceledCharge) + ")";

                durationOptionsList.Add(new { Value = "GTC", Text = s });
            }
            SelectList durOptSelectList = new SelectList(durationOptionsList, "Value", "Text");
            ViewData[Strings.Fields.Duration] = durOptSelectList;
            ViewData[Strings.Fields.GoodTilCanceled] = false.ToString();

            //Select list for Shipping
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(SiteClient.ShippingMethods, Strings.Fields.ID, Strings.Fields.Name);

            //Auto Relist options
            ViewData[Fields.AutoRelistMax] = null;
            if (listingTypeProperties.Exists(p => p.Field.Name == SiteProperties.MaxAutoRelists))
            {
                ViewData[Fields.AutoRelistMax] =
                    int.Parse(
                        listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value);
            }

            //ViewData for input parameters to bounce back
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.Currency] = currency;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.AddListing;

            //ViewData[Strings.MVC.LineageString] =
            //    CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].ToLineageString(Strings.Fields.Name, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].LocalizedCategoryLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });

            //populate view data with seller details
            ViewData[Strings.Fields.Seller] = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());

            if (TempData["SimilarListingID"] != null)
            {
                int similarListingID = (int)TempData["SimilarListingID"];
                Listing existingListing = ListingClient.GetListingByIDWithFillLevel(this.FBOUserName(), similarListingID, Strings.ListingFillLevels.Default);

                TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

                ViewData[Strings.Fields.Title] = existingListing.Title;
                ViewData[Strings.Fields.Subtitle] = existingListing.Subtitle;
                ViewData[Strings.Fields.Description] = existingListing.Description;

                //SelectList temp = SimpleSelectList(durationDaysList, (existingListing.Duration / 60 / 24));
                //ViewData[Strings.Fields.Duration] = temp;
                if (existingListing.IsGoodTilCanceled())
                {
                    ViewData[Strings.Fields.Duration] = new SelectList(durationOptionsList, "Value", "Text", "GTC");
                }
                else
                {
                    string existingDuration = (existingListing.Duration / 60 / 24).ToString();
                    //durationOptionsList.Add(new { Value = existingDuration, Text = existingDuration });
                    ViewData[Strings.Fields.Duration] = new SelectList(durationOptionsList, "Value", "Text", existingDuration);
                }

                if (existingListing.EndDTTM > DateTime.UtcNow && existingListing.EndDTTM.HasValue)
                {
                    //DateTime temp =
                    //    existingListing.EndDTTM.Value.AddHours((double)SiteClient.DecimalSetting(SiteProperties.TimeZoneOffset));
                    DateTime temp = TimeZoneInfo.ConvertTime(existingListing.EndDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                    ViewData[Fields.EndDate] = temp.ToString("d", cultureInfo);
                    ViewData[Fields.EndTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || temp.Second == 0 ? temp.ToString("t", cultureInfo) : temp.ToString("T", cultureInfo);
                }

                if (existingListing.StartDTTM > DateTime.UtcNow && existingListing.StartDTTM.HasValue)
                {
                    //DateTime temp =
                    //    existingListing.StartDTTM.Value.AddHours((double)SiteClient.DecimalSetting(SiteProperties.TimeZoneOffset));
                    DateTime temp = TimeZoneInfo.ConvertTime(existingListing.StartDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                    ViewData[Fields.StartDate] = temp.ToString("d", cultureInfo);
                    ViewData[Fields.StartTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || temp.Second == 0 ? temp.ToString("t", cultureInfo) : temp.ToString("T", cultureInfo);
                }

                //custom fields
                ModelState.FillProperties(existingListing.Properties, cultureInfo);

                //Create modelstate for each location
                foreach (Location location in existingListing.Locations)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                    ModelState.Add("location_" + location.ID, ms);
                }

                //Create modelstate for each decoration
                foreach (Decoration decoration in existingListing.Decorations)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                    ModelState.Add("decoration_" + decoration.ID, ms);
                }

                //Create model state for each html field with a different name than the corresponding model property
                if (existingListing.OriginalPrice.HasValue)
                {
                    string originalPrice = existingListing.OriginalPrice.Value.ToString("N2", cultureInfo);
                    ModelState.Add(Strings.Fields.Price, new ModelState()
                    {
                        Value =
                            new ValueProviderResult(existingListing.OriginalPrice,
                                                    originalPrice, null)
                    });
                }
                //string quantity = existingListing.CurrentQuantity.ToString(cultureInfo);
                //ModelState.Add(Strings.Fields.Quantity, new ModelState()
                //{
                //    Value = new ValueProviderResult(existingListing.CurrentQuantity, quantity, null)
                //});
                string quantity = existingListing.OriginalQuantity.ToString(cultureInfo);
                ModelState.Add(Strings.Fields.Quantity, new ModelState()
                {
                    Value = new ValueProviderResult(existingListing.OriginalQuantity, quantity, cultureInfo)
                });

                //Shipping Methods and Options
                List<ShippingOption> existingShippingOpts = existingListing.ShippingOptions;
                List<ShippingMethod> allShippingMethods = SiteClient.ShippingMethods;
                List<ShippingMethod> availableShippingMethods = new List<ShippingMethod>();
                foreach (ShippingMethod method in allShippingMethods)
                {
                    if (existingShippingOpts.Where(o => o.Method.ID == method.ID).Count() == 0)
                        availableShippingMethods.Add(method);
                }
                ViewData[Strings.Fields.ShippingMethod] = new SelectList(availableShippingMethods, Strings.Fields.ID, Strings.Fields.Name);
                ViewData[Strings.Fields.ShippingOptions] = existingShippingOpts;

                //Auto Relist options
                //ViewData[Fields.AutoRelist] = null;
                //if (listingTypeProperties.Exists(p => p.Field.Name == SiteProperties.MaxAutoRelists))
                //{
                //    string autoRelistOpts = "0";
                //    for (int i = 1; i <= int.Parse(listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value); i++)
                //    {
                //        autoRelistOpts += "," + i;
                //    }
                //    if (autoRelistOpts != "0")
                //    {
                //        //previous original auto relist value will be selected
                //        ViewData[Fields.AutoRelist] = SimpleSelectList(autoRelistOpts, existingListing.OriginalRelistCount);
                //    }
                //}
                ViewData[Fields.AutoRelist] = existingListing.OriginalRelistCount;

                //List of "Custom Item Field" Properties (anything that is not Listing Format-Specific)
                var allProperties = existingListing.AllCustomItemProperties(
                    CommonClient.GetFieldsByCategoryID(existingListing.PrimaryCategory.ID));
                PruneListingCustomPropertiesEditability(this.FBOUserName(), this.FBOUserName(), allProperties);
                ViewData[Strings.Fields.ItemProperties] = allProperties;

                //Media
                ViewData[Strings.Fields.Media] = this.CloneMediaAssets(existingListing.Media);

            } // if (TempData["SimilarListingID"] != null)
            else
            {
                //not listing similar, populate with default field values
                //Set custom item properties, add them to the view state for refresh      
                List<CustomProperty> itemProperties = new List<CustomProperty>();
                List<CustomField> itemFields = CommonClient.GetFieldsByCategoryID(CategoryID);

                PruneListingCustomFieldsEditability(this.FBOUserName(), this.FBOUserName(), itemFields);

                foreach (CustomField customField in itemFields)
                {
                    CustomProperty newProp = new CustomProperty();
                    newProp.Field = customField;
                    newProp.Value = customField.DefaultValue;
                    itemProperties.Add(newProp);
                }
                ViewData[Strings.Fields.ItemProperties] = itemProperties;
                ModelState.FillProperties(itemProperties, cultureInfo);

                ViewData[Strings.Fields.ItemProperties] = itemProperties;

                ViewData[Fields.IsTaxable] = !SiteClient.BoolSetting(SiteProperties.HideTaxFields);
            }

            return View();
        }

        private static void GetPostAndFinalFeeRanges(string listingType,
            out decimal postFeeMin, out decimal postFeeMax, out List<Tier> postFeeTiers,
            out decimal finalFeeMin, out decimal finalFeeMax, out List<Tier> finalFeeTiers)
        {
            postFeeMin = 0.0M;
            postFeeMax = 0.0M;
            postFeeTiers = new List<Tier>();
            finalFeeMin = 0.0M;
            finalFeeMax = 0.0M;
            finalFeeTiers = new List<Tier>();
            if (listingType != ListingTypes.Classified)
            {
                foreach (FeeSchedule fs in SiteClient.FeeSchedules.Where(f => f.ListingType.Name == listingType
                                                                           && f.Event.Name == Events.AddListing
                                                                           && f.Name == Roles.Seller))
                {
                    bool addTiersToOutput = postFeeTiers.Count == 0;
                    foreach (Tier t in fs.Tiers.OrderBy(t => t.UpperBoundExclusive))
                    {
                        if (postFeeMin == 0.0M)
                        {
                            postFeeMin = t.Value;
                        }
                        else if (postFeeMin > t.Value)
                        {
                            postFeeMin = t.Value;
                        }
                        if (postFeeMax == 0.0M)
                        {
                            postFeeMax = t.Value;
                        }
                        else if (postFeeMax < t.Value)
                        {
                            postFeeMax = t.Value;
                        }
                        if (addTiersToOutput) postFeeTiers.Add(t);
                    }
                }
                foreach (FeeSchedule fs in SiteClient.FeeSchedules.Where(f => f.ListingType.Name == listingType
                                                                           && f.Event.Name == Events.EndListingSuccess
                                                                           && f.Name == Roles.Seller))
                {
                    bool addTiersToOutput = finalFeeTiers.Count == 0;
                    foreach (Tier t in fs.Tiers)
                    {
                        if (finalFeeMin == 0.0M)
                        {
                            finalFeeMin = t.Value;
                        }
                        else if (finalFeeMin > t.Value)
                        {
                            finalFeeMin = t.Value;
                        }
                        if (finalFeeMax == 0.0M)
                        {
                            finalFeeMax = t.Value;
                        }
                        else if (finalFeeMax < t.Value)
                        {
                            finalFeeMax = t.Value;
                        }
                        if (addTiersToOutput) finalFeeTiers.Add(t);
                    }
                }
            }
            else // classified uses a flat fee instead of tiers, and the final fee is always N/A
            {
                FeeProperty classPostFee =
                    SiteClient.GetFeeProperties().Where(f => f.Name == FeeNames.FlatFee).FirstOrDefault();
                if (classPostFee != null)
                {
                    postFeeMin = classPostFee.Amount;
                    postFeeMax = postFeeMin;
                }
            }
        }

        /// <summary>
        /// Processes request to enter remaining new listing details (page 2 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="LineageString">represents all categories to be assigned to the new listing</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success when no fees are owed</param>
        /// <returns>
        ///     (success, pmt req'd)            Redirect to /Account/Fees
        ///     (success, ReturnUrl specified)  Redirect to [ReturnUrl]
        ///     (success)                       Redirect to /Listing/Details/[id of new listing]
        ///     (validation errors)             View()
        /// </returns>        
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult CreateListingPage2(int CategoryID, int? StoreID, int? RegionID, int? EventID, string currency, string ListingType, string LineageString, string ReturnUrl)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            //foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            //{
            //    input.Items.Add(key, Request.Form[key] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : Request.Form[key]);
            //    if (!ModelState.ContainsKey(key))
            //    {
            //        //...add it to the model
            //        ModelState ms = new ModelState();
            //        ms.Value = new ValueProviderResult(input.Items[key], input.Items[key], null);
            //        ModelState.Add(key, ms);
            //    }
            //}
            input.AddAllFormValues(this);

            List<CustomProperty> itemProperties = new List<CustomProperty>();
            List<CustomField> itemFields = CommonClient.GetFieldsByCategoryID(CategoryID);

            PruneListingCustomFieldsEditability(this.FBOUserName(), this.FBOUserName(), itemFields);

            foreach (CustomField customField in itemFields)
            {
                CustomProperty newProp = new CustomProperty();
                newProp.Field = customField;
                itemProperties.Add(newProp);
                if (!ModelState.ContainsKey(customField.Name))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(string.Empty, string.Empty, null);
                    ModelState.Add(customField.Name, ms);
                }
                if (!input.Items.ContainsKey(customField.Name))
                {
                    input.Items.Add(customField.Name, string.Empty);
                }
            }

            //Calculate Store, Event, RegionID, CategoryID are lineage nodes, merge them, send them
            var lineages = new List<string>();
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.StoreID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.StoreID])).Trees[int.Parse(Request.Form[Strings.Fields.StoreID])].LineageString);
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.EventID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.EventID])).Trees[int.Parse(Request.Form[Strings.Fields.EventID])].LineageString);
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.RegionID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.RegionID])).Trees[int.Parse(Request.Form[Strings.Fields.RegionID])].LineageString);
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.CategoryID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.CategoryID])).Trees[int.Parse(Request.Form[Strings.Fields.CategoryID])].LineageString);
            string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
            input.Items.Add(Strings.Fields.AllCategories, categories);

            input.Items.Remove("ThumbnailRendererState");
            input.Items.Remove("YouTubeRendererState");
            input.Items.Remove("ShippingRenderState");
            input.Items.Remove("FilesRendererState");
            input.Items.Remove("files[]");
            input.Items.Remove("ReturnUrl");
            input.Items.Remove("ShippingMethod");
            input.Items.Remove("Amount");
            input.Items.Remove("AdditionalItemAmount");

            //do call to BLL
            try
            {
                int listingID;
                bool payToProceed = ListingClient.CreateListing(User.Identity.Name, input, true, out listingID);

                try
                {
                    CommonClient.DeleteExpiredOriginalMediasNow(User.Identity.Name, Server.MapPath("~"));
                }
                catch (Exception)
                {
                    //ignore this exception
                }

                if (payToProceed)
                {
                    //paytoproceed is true
                    return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController);
                }
                else if (Url.IsLocalUrl(ReturnUrl))
                {
                    PrepareSuccessMessage("CreateListingPage2", MessageType.Method);
                    return Redirect(ReturnUrl);
                }
                else
                {
                    return RedirectToAction(Strings.MVC.ListingConfirmationAction, Strings.MVC.ListingController, new { id = listingID });
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                //TODO mostly deprecated by FunctionResult
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (FaultException<AuthorizationFaultContract>)
            {
                return View("NoPermissionToSell");
            }
            catch (Exception e)
            {
                //TODO mostly deprecated by FunctionResult?
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
            }

            //OUT
            //Set custom item properties, add them to the view state for refresh                 
            //List<CustomProperty> itemproperties = new List<CustomProperty>();
            //List<CustomField> itemFields = CommonClient.GetFieldsByCategoryID(9);
            //itemFields.AddRange(CommonClient.GetFieldsByCategoryID(CategoryID));
            //foreach (CustomField customField in itemFields)
            //{
            //    CustomProperty newProp = new CustomProperty();
            //    newProp.Field = customField;
            //    itemproperties.Add(newProp);
            //}
            ViewData[Strings.Fields.ItemProperties] = itemProperties;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(ListingType, "Site");
            string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.ListingDurationOptions).First().Value;
            ViewData[Strings.SiteProperties.ListingDurationOptions] = durationOpts;
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;
            string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.DurationDaysList).First().Value;

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Select list for Duration
            bool gtcOptionAvailable =
                listingTypeProperties.Exists(p => p.Field.Name == Strings.SiteProperties.EnableGTC)
                    ? bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableGTC).First().Value)
                    : false;
            ViewData[Strings.MVC.ViewData_GtcAvailable] = gtcOptionAvailable;
            List<object> durationOptionsList = new List<object>();
            foreach (string durOpt in durationDaysList.Split(','))
            {
                durationOptionsList.Add(new { Value = durOpt, Text = durOpt + " " + this.GlobalResource("Days") });
            }

            if (gtcOptionAvailable)
            {
                //add GoodUntilCanceled fee

                decimal goodUntilCanceledCharge = AccountingClient.GetAllFeeProperties().Where(fp => fp.Processor.Equals("RainWorx.FrameWorx.Providers.Fee.Standard.GoodUntilCanceled") &&
                                fp.Event.Name.Equals(Strings.Events.AddListing) &&
                                fp.ListingType.Name.Equals(ListingType) &&
                                fp.Name.Equals(FeeNames.GoodUntilCanceled)).SingleOrDefault().Amount;

                String s = this.GlobalResourceString("GoodTilCanceled") + " (" + this.SiteCurrencyOrFree(goodUntilCanceledCharge) + ")";

                durationOptionsList.Add(new { Value = "GTC", Text = s });
            }


            SelectList durOptSelectList = new SelectList(durationOptionsList, "Value", "Text");
            ViewData[Strings.Fields.Duration] = durOptSelectList;

            //Select list for Shipping
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(SiteClient.ShippingMethods, Strings.Fields.ID, Strings.Fields.Name);

            //Auto Relist options
            ViewData[Fields.AutoRelistMax] = null;
            if (listingTypeProperties.Exists(p => p.Field.Name == SiteProperties.MaxAutoRelists))
            {
                ViewData[Fields.AutoRelistMax] =
                    int.Parse(
                        listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value);
            }

            //ViewData for input parameters to bounce back
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.Currency] = currency;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.AddListing;

            ViewData[Strings.MVC.LineageString] = LineageString;

            //populate view data with seller details
            ViewData[Strings.Fields.Seller] = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());

            //View Prep
            ViewData[Strings.Fields.Description] = input.Items[Strings.Fields.Description];

            return View();
        }

        /// <summary>
        /// Displays Post Fee and Final Fee details for the specified listing type and price
        /// </summary>
        /// <param name="t">the specified listing type</param>
        /// <param name="p">the specified price</param>
        /// <param name="c">the specified currency code</param>
        /// <param name="pc">the specified listing category ID</param>
        /// <returns></returns>
        public ActionResult PostAndFinalFees(string t, string p, string c, string pc)
        {
            string actingUN = User.Identity.IsAuthenticated ? User.Identity.Name : string.Empty;

            //get post fee and final % fee ranges and tiers
            decimal postFeeMin;
            decimal postFeeMax;
            List<Tier> postFeeTiers;
            decimal finalFeeMin;
            decimal finalFeeMax;
            List<Tier> finalFeeTiers;
            GetPostAndFinalFeeRanges(t, out postFeeMin, out postFeeMax, out postFeeTiers, out finalFeeMin, out finalFeeMax, out finalFeeTiers);
            ViewData[Strings.MVC.ViewData_ListingType] = ListingClient.ListingTypes.Single(lt => lt.Name.Equals(t, StringComparison.OrdinalIgnoreCase)).Name;

            ViewData[Strings.MVC.ViewData_MinPostFee] = postFeeMin;
            ViewData[Strings.MVC.ViewData_MaxPostFee] = postFeeMax;
            ViewData[Strings.MVC.ViewData_PostFeeTiers] = postFeeTiers;
            ViewData[Strings.MVC.ViewData_MinFinalFee] = finalFeeMin;
            ViewData[Strings.MVC.ViewData_MaxFinalFee] = finalFeeMax;
            ViewData[Strings.MVC.ViewData_FinalFeeTiers] = finalFeeTiers;

            //calculate fee amounts for the specified price
            decimal price = 0.0M;
            if (!string.IsNullOrWhiteSpace(p))
            {
                decimal.TryParse(p, NumberStyles.Currency, this.GetCultureInfo(), out price);
            }
            int catId = 9; // root listing category
            if (!string.IsNullOrWhiteSpace(pc))
            {
                int.TryParse(pc, NumberStyles.Integer, this.GetCultureInfo(), out catId);
            }
            ViewData[Strings.MVC.ViewData_PostFeeAmount] = ListingClient.CalculatePostFee(actingUN, t, price, c, catId);
            ViewData[Strings.MVC.ViewData_FinalFeeAmount] = ListingClient.CalculateFinalFee(actingUN, t, price, c, catId);

            return View();
        }

        /// <summary>
        /// create listing test
        /// </summary>
        public string CreateTestListing()
        {
            string retVal = string.Empty;

            string userName = "admin";
            DateTime openDate = DateTime.Now.AddDays(10);
            UserInput input = new UserInput("admin", userName, "en-US", "en-US");
            input.Items.Add("CategoryID", "160536"); // default ID for "Category C"
            input.Items.Add("RegionID", "159832"); // default ID for "Region C"
            input.Items.Add("Currency", "USD");
            input.Items.Add("ListingType", "Auction");
            input.Items.Add("Title", "test 2 ttl");
            input.Items.Add("Subtitle", "test 2 sbttl");
            input.Items.Add("Description", "test 2 dscr");
            input.Items.Add("Price", "100.00");
            input.Items.Add("Quantity", "1");
            input.Items.Add("Duration", "10");
            input.Items.Add("EndDate", openDate.ToString("MM/dd/yyyy"));
            input.Items.Add("EndTime", openDate.ToString("HH:mm:ss"));
            int newListingID;

            if (!input.Items.ContainsKey("AllCategories") || string.IsNullOrEmpty(input.Items["AllCategories"]))
            {
                var lineages = new List<string>();
                int categoryID = 0;
                if (input.Items.ContainsKey("CategoryID") && int.TryParse(input.Items["CategoryID"], out categoryID) && categoryID > 0)
                {
                    lineages.Add(CommonClient.GetCategoryPath(categoryID).Trees[categoryID].LineageString);
                }
                int regionID = 0;
                if (input.Items.ContainsKey("RegionID") && int.TryParse(input.Items["RegionID"], out regionID) && regionID > 0)
                {
                    lineages.Add(CommonClient.GetCategoryPath(regionID).Trees[regionID].LineageString);
                }
                if (lineages.Count > 0)
                {
                    string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
                    input.Items.Add(Strings.Fields.AllCategories, categories);
                }
            }

            try
            {
                ListingClient.CreateListing("admin", input, false, out newListingID);
                retVal = "Created Listing #" + newListingID.ToString();
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                retVal += "Validation Errors:\n";
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    retVal += issue.Key + ": " + issue.Message + "\n";
                }
            }
            catch (FaultException<AuthorizationFaultContract>)
            {
                retVal += "NoPermissionToSell";
            }
            catch (Exception e)
            {
                retVal += "Error: " + e.Message;
            }
            return retVal;
        }

        #endregion

        #region Edit Listing

        /// <summary>
        /// Displays form to edit listing
        /// </summary>
        /// <param name="id">ID of the requested listing to be edited</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success when no fees are owed</param>
        /// <returns>
        /// (auth. success) View(Listing)
        /// (auth. failure) Redirect to /Listing/Detail/[id]
        /// </returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult Edit(int id, string ReturnUrl)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

            //get the existing Listing object
            Listing existingListing = ListingClient.GetListingByIDWithFillLevel(actingUN, id, Strings.ListingFillLevels.Default);

            if (existingListing == null)
            {
                //handle non-existant listing # on detail page
                return RedirectToAction(Strings.MVC.DetailsAction, new { id = id });
            }

            if (SiteClient.EnableEvents && existingListing.Lot != null)
            {
                return RedirectToAction(Strings.MVC.EditLotAction, Strings.MVC.EventController, new { id = existingListing.Lot.ID, returnUrl = ReturnUrl });
            }

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (existingListing.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, new { id = id });
            }

            //get list of fields/field groups allowed to be edited in the current context
            Dictionary<string, bool> editableFields = ListingClient.GetUpdateableListingFields(actingUN, existingListing);
            if (editableFields.Values.Where(v => v == true).Count() == 0)
            {
                //zero editable fields - non-admin + closed listing?
                return RedirectToAction(Strings.MVC.DetailsAction, new { id = id });
            }
            ViewData[Strings.MVC.ViewData_EditableFieldList] = editableFields;

            //Create modelstate for each property (for initial render)
            ModelState.FillProperties(existingListing.Properties, cultureInfo);

            //ensure initial model state for IsTaxable, in case this is an old listing which doesn't have the "IsTaxable" property yet
            if (!SiteClient.BoolSetting(SiteProperties.HideTaxFields))
            {
                if (!existingListing.Properties.Any(lp => lp.Field.Name == Fields.IsTaxable))
                {
                    ViewData[Fields.IsTaxable] = true;
                }
            }

            //Create modelstate for each location
            foreach (Location location in existingListing.Locations)
            {
                //Add Model control
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                ModelState.Add("location_" + location.ID, ms);
            }

            //Create modelstate for each decoration
            foreach (Decoration decoration in existingListing.Decorations)
            {
                //Add Model control
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                ModelState.Add("decoration_" + decoration.ID, ms);
            }

            //Create model state for each html field with a different name than the corresponding model property
            if (existingListing.OriginalPrice.HasValue)
            {
                string originalPrice = existingListing.OriginalPrice.Value.ToString("N2", cultureInfo);
                ModelState.Add(Strings.Fields.Price, new ModelState()
                {
                    Value =
                        new ValueProviderResult(existingListing.OriginalPrice,
                                                originalPrice, null)
                });
            }
            string quantity = existingListing.CurrentQuantity.ToString(cultureInfo);
            ModelState.Add(Strings.Fields.Quantity, new ModelState()
            {
                Value = new ValueProviderResult(existingListing.CurrentQuantity, quantity, null)
            });
            ModelState.Add(Strings.Fields.CategoryID, new ModelState()
            {
                Value = new ValueProviderResult(existingListing.PrimaryCategory.ID,
                    existingListing.PrimaryCategory.ID.ToString(), null)
            });

            //ReturnUrl, Listing Type, Event view data
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.Fields.ListingType] = existingListing.Type.Name;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.UpdateListing;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(existingListing.Type.Name, "Site");

            //regions
            if (SiteClient.BoolSetting(SiteProperties.EnableRegions))
            {
                Category leafRegion = existingListing.LeafRegion();
                int? regionID = (leafRegion != null ? (int?)leafRegion.ID : null);
                ViewData[Fields.RegionID] = regionID;
            }

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Duration
            string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.ListingDurationOptions).First().Value;
            ViewData[Strings.SiteProperties.ListingDurationOptions] = durationOpts;

            string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.DurationDaysList).First().Value;
            bool gtcOptionAvailable =
                listingTypeProperties.Exists(p => p.Field.Name == Strings.SiteProperties.EnableGTC)
                    ? bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableGTC).First().Value)
                    : false;
            ViewData[Strings.MVC.ViewData_GtcAvailable] = gtcOptionAvailable;
            List<object> durationOptionsList = new List<object>();
            foreach (string durOpt in durationDaysList.Split(','))
            {
                durationOptionsList.Add(new { Value = durOpt, Text = durOpt + " " + this.GlobalResource("Days") });
            }
            if (gtcOptionAvailable)
            {
                //add GoodUntilCanceled fee

                decimal goodUntilCanceledCharge = AccountingClient.GetAllFeeProperties().Where(fp => fp.Processor.Equals("RainWorx.FrameWorx.Providers.Fee.Standard.GoodUntilCanceled") &&
                                fp.Event.Name.Equals(Strings.Events.AddListing) &&
                                fp.ListingType.Name.Equals(existingListing.Type.Name) &&
                                fp.Name.Equals(FeeNames.GoodUntilCanceled)).SingleOrDefault().Amount;

                String s = this.GlobalResourceString("GoodTilCanceled") + " (" + this.SiteCurrencyOrFree(goodUntilCanceledCharge) + ")";

                durationOptionsList.Add(new { Value = "GTC", Text = s });

                //durationOptionsList.Add(new { Value = "GTC", Text = this.GlobalResourceString("GoodTilCanceled") });
            }
            string selectedDurationOption = ((existingListing.Duration ?? 0) / 1440).ToString();
            if (existingListing.IsGoodTilCanceled())
            {
                selectedDurationOption = "GTC";
            }
            SelectList durOptSelectList = new SelectList(durationOptionsList, "Value", "Text", selectedDurationOption);
            ViewData[Strings.Fields.Duration] = durOptSelectList;
            ViewData[Strings.Fields.GoodTilCanceled] = false.ToString();

            //End Date/Time
            if (!existingListing.IsGoodTilCanceled() && existingListing.EndDTTM.HasValue)
            {
                //DateTime localEndDTTM = existingListing.EndDTTM.Value.AddHours(SiteClient.TimeZoneOffset);
                DateTime localEndDTTM = TimeZoneInfo.ConvertTime(existingListing.EndDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                ViewData[Strings.Fields.EndDate] = localEndDTTM.ToString("d", cultureInfo);
                ViewData[Strings.Fields.EndTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || localEndDTTM.Second == 0 ? localEndDTTM.ToString("t", cultureInfo) : localEndDTTM.ToString("T", cultureInfo);
            }

            //Start Date/Time
            if (!existingListing.IsGoodTilCanceled() && existingListing.StartDTTM.HasValue)
            {
                //DateTime localStartDTTM = existingListing.StartDTTM.Value.AddHours(SiteClient.TimeZoneOffset);
                DateTime localStartDTTM = TimeZoneInfo.ConvertTime(existingListing.StartDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                ViewData[Strings.Fields.StartDate] = localStartDTTM.ToString("d", cultureInfo);
                ViewData[Strings.Fields.StartTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || localStartDTTM.Second == 0 ? localStartDTTM.ToString("t", cultureInfo) : localStartDTTM.ToString("T", cultureInfo);
            }

            //Shipping Methods and Options
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

            List<ShippingOption> existingShippingOpts = existingListing.ShippingOptions;
            List<ShippingMethod> allShippingMethods = SiteClient.ShippingMethods;
            List<ShippingMethod> availableShippingMethods = new List<ShippingMethod>();
            foreach (ShippingMethod method in allShippingMethods)
            {
                if (existingShippingOpts.Where(o => o.Method.ID == method.ID).Count() == 0)
                    availableShippingMethods.Add(method);
            }
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(availableShippingMethods, Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.ShippingOptions] = existingShippingOpts;

            //List of "Custom Item Field" Properties (anything that is not Listing Format-Specific)
            var propertiesToEdit = existingListing.AllCustomItemProperties(
                CommonClient.GetFieldsByCategoryID(existingListing.PrimaryCategory.ID));
            PruneListingCustomPropertiesEditability(this.FBOUserName(), existingListing.OwnerUserName, propertiesToEdit);
            ViewData[Strings.Fields.Properties] = propertiesToEdit;

            //Media
            ViewData[Strings.Fields.Media] = existingListing.Media;

            //Currency (needed so CreateListingPage2 can use the same shared "XXXListingFields" user control)
            ViewData[Strings.Fields.Currency] = existingListing.Currency.Code;

            ////Auto Relist options
            //ViewData[Fields.AutoRelist] = null;
            //if (listingTypeProperties.Exists(p => p.Field.Name == SiteProperties.MaxAutoRelists))
            //{
            //    string autoRelistOpts = "0";
            //    bool allowRelistIncrease = (editableFields.ContainsKey("AutoRelist") && editableFields["AutoRelist"]);
            //    for (int i = 1; i <= int.Parse(listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value); i++)
            //    {
            //        if (i <= existingListing.AutoRelistRemaining || allowRelistIncrease) // non-admins can only decrease this value
            //        {
            //            autoRelistOpts += "," + i;
            //        }
            //    }
            //    if (autoRelistOpts != "0")
            //    {
            //        ViewData[Fields.AutoRelist] = SimpleSelectList(autoRelistOpts, existingListing.AutoRelistRemaining);
            //    }
            //}
            //Auto Relist options
            bool allowRelistIncrease = (editableFields.ContainsKey("AutoRelist") && editableFields["AutoRelist"]);
            ViewData[Fields.AutoRelist] = existingListing.AutoRelistRemaining;
            if (allowRelistIncrease)
            {
                ViewData[Fields.AutoRelistMax] = int.Parse(
                        listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value);
            }
            else
            {
                ViewData[Fields.AutoRelistMax] = existingListing.AutoRelistRemaining;
            }

            return View(existingListing);
        }

        /// <summary>
        /// Processes request to edit the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing to be edited</param>
        /// <param name="CategoryID">ID of the requested listing category</param>        
        /// <param name="ListingType">Name of requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="LineageString">represents all categories to be assigned to the new listing</param>
        /// <param name="Currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success when no fees are owed</param>
        /// <returns>
        ///     (success)               Redirect to /Listing/Details/[id]
        ///     (auth. failure)         Redirect to /Listing/Details/[id]
        ///     (validation errors)     View(Listing)
        /// </returns>
        //TODO: all parameters except id should be factored out - these details should be pulled from DAL, not submitted via user input.
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [ValidateInput(false)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Edit(int id, int CategoryID, string ListingType, string LineageString, string Currency, string ReturnUrl)
        {
            ViewData[Fields.CategoryID] = CategoryID;

            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            //username of logged in user
            var actingUserName = User.Identity.Name;

            //get the existing Listing object
            Listing existingListing = ListingClient.GetListingByIDWithFillLevel(actingUserName, id, Strings.ListingFillLevels.Default);
            if (existingListing == null)
            {
                //handle non-existant listing # on detail page
                return RedirectToAction("Details", new { id = id });
            }

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (existingListing.OwnerUserName.Equals(actingUserName, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction("Details", new { id = id });
            }

            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(actingUserName, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            //The Listing to be updated
            input.Items.Add(Strings.Fields.ListingID, id.ToString());
            //foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            //{
            //    input.Items.Add(key, Request.Form[key] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : Request.Form[key]);
            //    if (!ModelState.ContainsKey(key))
            //    {
            //        //...add it to the model
            //        ModelState ms = new ModelState();
            //        ms.Value = new ValueProviderResult(input.Items[key], input.Items[key], null);
            //        ModelState.Add(key, ms);
            //    }
            //}
            input.AddAllFormValues(this);

            //add any missing custom item property model states
            List<CustomProperty> itemproperties = existingListing.AllCustomItemProperties(
                CommonClient.GetFieldsByCategoryID(existingListing.PrimaryCategory.ID));
            PruneListingCustomPropertiesEditability(this.FBOUserName(), existingListing.OwnerUserName, itemproperties);
            foreach (CustomProperty customProp in itemproperties)
            {
                if (!ModelState.ContainsKey(customProp.Field.Name))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(string.Empty, string.Empty, null);
                    ModelState.Add(customProp.Field.Name, ms);
                }
                if (!input.Items.ContainsKey(customProp.Field.Name))
                {
                    input.Items.Add(customProp.Field.Name, string.Empty);
                }
            }

            //format description append note (if applicable - non-admin + bids/purchases)
            if (input.Items.ContainsKey(Strings.Fields.AppendDescription))
            {
                string formattedAppendage = string.Empty;
                if (!string.IsNullOrEmpty(input.Items[Strings.Fields.AppendDescription]))
                {
                    formattedAppendage += this.GlobalResource("AppendedCommentLeadingText");
                    formattedAppendage += HttpUtility.HtmlEncode(input.Items[Strings.Fields.AppendDescription]);
                    formattedAppendage += this.GlobalResource("AppendedCommentTrailingText");
                }
                input.Items.Add(Strings.Fields.AppendDescriptionFormatted, formattedAppendage);
            }

            //do call to BLL
            try
            {
                //return a different view here for success or redirectaction...
                if (ListingClient.UpdateListingWithUserInput(actingUserName, existingListing, input))
                {
                    //paytoproceed is true
                    return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController);
                }
                if (Url.IsLocalUrl(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id });
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                //display validation errors
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
            }

            //OUT

            //A validation or other error occurred so get all data needed to render view for correction

            //get list of fields/field groups allowed to be edited in the current context
            Dictionary<string, bool> editableFields = ListingClient.GetUpdateableListingFields(actingUserName, existingListing);
            ViewData[Strings.MVC.ViewData_EditableFieldList] = editableFields;

            //ReturnUrl, Listing Type, Event view data
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.Fields.ListingType] = existingListing.Type.Name;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.UpdateListing;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(existingListing.Type.Name, "Site");

            if (SiteClient.BoolSetting(SiteProperties.EnableRegions) && 
                input.Items.ContainsKey(Fields.RegionID) && 
                !string.IsNullOrEmpty(input.Items[Fields.RegionID]))
            {
                ViewData[Fields.RegionID] = int.Parse(input.Items[Fields.RegionID]);
            }

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Duration
            string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.ListingDurationOptions).First().Value;
            ViewData[Strings.SiteProperties.ListingDurationOptions] = durationOpts;

            string durationDaysList = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.DurationDaysList).First().Value;
            bool gtcOptionAvailable =
                listingTypeProperties.Exists(p => p.Field.Name == Strings.SiteProperties.EnableGTC)
                    ? bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableGTC).First().Value)
                    : false;
            ViewData[Strings.MVC.ViewData_GtcAvailable] = gtcOptionAvailable;
            List<object> durationOptionsList = new List<object>();
            foreach (string durOpt in durationDaysList.Split(','))
            {
                durationOptionsList.Add(new { Value = durOpt, Text = durOpt + " " + this.GlobalResource("Days") });
            }
            if (gtcOptionAvailable)
            {
                durationOptionsList.Add(new { Value = "GTC", Text = this.GlobalResourceString("GoodTilCanceled") });
            }
            SelectList durOptSelectList = new SelectList(durationOptionsList, "Value", "Text");
            ViewData[Strings.Fields.Duration] = durOptSelectList;
            ViewData[Strings.Fields.GoodTilCanceled] = false.ToString();

            //Shipping Methods and Options
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;
            List<ShippingMethod> allShippingMethods = SiteClient.ShippingMethods;
            List<ShippingOption> newShippingOptions = Utilities.GetShippingOptions(input);
            List<ShippingMethod> availableShippingMethods = new List<ShippingMethod>();
            foreach (ShippingMethod method in allShippingMethods)
            {
                if (newShippingOptions.Where(o => o.Method.ID == method.ID).Count() == 0)
                    availableShippingMethods.Add(method);
            }
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(availableShippingMethods, Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.ShippingOptions] = newShippingOptions;

            //set properties for property renderer
            //List of "Custom Item Field" Properties (anything that is not Listing Format-Specific)
            //ViewData[Strings.Fields.Properties] = existingListing.AllCustomItemProperties(
            //    CommonClient.GetFieldsByCategoryID(existingListing.PrimaryCategory.ID));
            ViewData[Strings.Fields.Properties] = itemproperties;

            //Images - on validation errors images are re-populated via the ImageRenderState input value
            //ViewData[RainWorx.FrameWorx.Strings.Fields.Images] = Utilities.GetImages(input);
            ViewData[Strings.Fields.Media] = new List<RainWorx.FrameWorx.DTO.Media.Media>();

            //Currency (needed so CreateListingPage2 can use the same shared "XXXListingFields" user control)
            ViewData[Strings.Fields.Currency] = existingListing.Currency.Code;

            //Auto Relist options
            //ViewData[Fields.AutoRelist] = null;
            //if (listingTypeProperties.Exists(p => p.Field.Name == SiteProperties.MaxAutoRelists))
            //{
            //    string autoRelistOpts = "0";
            //    bool allowRelistIncrease = (editableFields.ContainsKey("AutoRelist") && editableFields["AutoRelist"]);
            //    for (int i = 1; i <= int.Parse(listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value); i++)
            //    {
            //        if (i <= existingListing.AutoRelistRemaining || allowRelistIncrease) // non-admins can only decrease this value
            //        {
            //            autoRelistOpts += "," + i;
            //        }
            //    }
            //    if (autoRelistOpts != "0")
            //    {
            //        if (input.Items.ContainsKey(Fields.AutoRelist))
            //        {
            //            ViewData[Fields.AutoRelist] = SimpleSelectList(autoRelistOpts, input.Items[Fields.AutoRelist]);
            //        }
            //        else
            //        {
            //            ViewData[Fields.AutoRelist] = SimpleSelectList(autoRelistOpts, existingListing.AutoRelistRemaining);
            //        }
            //    }
            //}            
            bool allowRelistIncrease = (editableFields.ContainsKey("AutoRelist") && editableFields["AutoRelist"]);
            ViewData[Fields.AutoRelist] = existingListing.AutoRelistRemaining;
            if (allowRelistIncrease)
            {
                ViewData[Fields.AutoRelistMax] = int.Parse(
                        listingTypeProperties.Where(p => p.Field.Name == SiteProperties.MaxAutoRelists).First().Value);
            }
            else
            {
                ViewData[Fields.AutoRelistMax] = existingListing.AutoRelistRemaining;
            }

            return View(existingListing);
        }

        #endregion

        #region Add / Remove Watch

        /// <summary>
        /// Processes request to add the specified listing to user's watch list
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Listing/Details/[id]</returns>
        [Authorize]
        public ActionResult AddWatch(int id, string returnUrl)
        {
            try
            {
                UserClient.AddWatch(User.Identity.Name, this.FBOUserName(), id);
                PrepareSuccessMessage("AddWatch", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("AddWatch", MessageType.Method);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.DetailsAction, new { id });
        }

        /// <summary>
        /// Processes request to remove the specified listing from the user's watch list
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Listing/Details/[id]</returns>
        [Authorize]
        public ActionResult RemoveWatch(int id, string returnUrl)
        {
            try
            {
                UserClient.RemoveWatch(User.Identity.Name, this.FBOUserName(), id);
                PrepareSuccessMessage("RemoveWatch", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("RemoveWatch", MessageType.Method);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.DetailsAction, new { id });
        }

        #endregion

        #region Submit Bid / Purchase

        /// <summary>
        /// Displays confirmation page for listing action request (e.g. bid or purchase)
        /// </summary>
        /// <param name="ListingID">ID of the requested listing</param>
        /// <returns>View(Listing)</returns>        
        public ActionResult ConfirmAction(int ListingID)
        {
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);
            input.AddAllQueryStringValues(this);
            foreach (string key in input.Items.Keys)
            {
                ViewData[key] = input.Items[key];
            }
            Listing listing = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                fillLevel += "," + ListingFillLevels.PrimaryCategory + "," + ListingFillLevels.Categories;
                if (SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium))
                {
                    fillLevel += "," + ListingFillLevels.Owner;
                }
                listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);
                ListingClient.ValidateListingActionInput(User.Identity.Name, input, ListingID);

                //determine final buyer fee details
                decimal finalFeeMin;
                decimal finalFeeMax;
                List<Tier> finalFeeTiers;
                string finalFeeDescription;
                GetFinalBuyerFeeRanges(listing.Type.Name, listing.Categories, out finalFeeMin, out finalFeeMax, out finalFeeTiers, out finalFeeDescription);
                ViewData[Strings.MVC.ViewData_MinFinalBuyerFee] = finalFeeMin;
                ViewData[Strings.MVC.ViewData_MaxFinalBuyerFee] = finalFeeMax;
                ViewData[Strings.MVC.ViewData_FinalBuyerFeeTiers] = finalFeeTiers;
                ViewData[Strings.MVC.ViewData_FinalBuyerFeeDescription] = finalFeeDescription;

                return View(listing);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //StoreValidationIssues(vfc.Detail.ValidationIssues, input);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return Details(ListingID);
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                PrepareErrorMessage(iafc.Detail.Reason);
            }
            catch (FaultException<AuthorizationFaultContract> afc)
            {
                string localizedMessage;
                if (listing == null)
                {
                    localizedMessage = this.GlobalResourceString(afc.Detail.Reason.ToString());
                }
                else
                {
                    localizedMessage = this.ResourceString(listing.Type.Name + "Listing, " + afc.Detail.Reason.ToString());
                }
                PrepareErrorMessage(localizedMessage, MessageType.Message);
            }
            catch (Exception)
            {
                PrepareErrorMessage("ConfirmAction", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.DetailsAction, new { id = ListingID });
        }

        /// <summary>
        /// Displays confirmation page for listing action request as a modal window
        /// </summary>
        /// <param name="ListingID">ID of the requested listing</param>
        /// <returns>View(Listing), when there's an error, DOES NOT return Details(ListingID) (which would show the Listing Details in the modal...)</returns> 
        public ActionResult ConfirmActionModal(int ListingID)
        {
            ViewData["CCRequired"] = false;

            if (!User.Identity.IsAuthenticated)
            {
                return View();
            }

            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);
            input.AddAllQueryStringValues(this);
            foreach (string key in input.Items.Keys)
            {
                ViewData[key] = input.Items[key];
            }
            Listing listing = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                fillLevel += "," + ListingFillLevels.PrimaryCategory + "," + ListingFillLevels.Categories;
                if (SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium) || SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers))
                {
                    fillLevel += "," + ListingFillLevels.Owner;
                }
                listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);

                var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                bool isCcReqExempt = (loggedOnUser != null && loggedOnUser.Properties != null)
                    ? loggedOnUser.Properties.GetPropertyValue(StdUserProps.CreditCardRequiredExempt, false) : false;
                if (!isCcReqExempt && 
                    SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers) &&
                    listing.Owner.CreditCardAccepted() && 
                    !User.IsInRole("Admin") && 
                    !this.HasUnexpiredCardOnFile(listing.OwnerUserName, this.FBOUserName()))
                {
                    PrepareNeutralMessage(Messages.CreditCardRequiredForBuyers);
                    //return RedirectToAction(Strings.MVC.AddCardAction, Strings.MVC.AccountController, new { returnUrl = Request.Url.PathAndQuery });
                    ViewData["CCRequired"] = true;
                }

                ListingClient.ValidateListingActionInput(User.Identity.Name, input, ListingID);

                //missing decimal point check -- begin
                if (listing.Type.Name == ListingTypes.Auction && !input.Items.ContainsKey("large-bid-confirmed"))
                {
                    bool largeBidConfirmationEnabled = false;
                    List<CustomProperty> auctionProperties = ListingClient.GetListingTypeProperties(ListingTypes.Auction, "Site");
                    var largeBidConfProp = auctionProperties.FirstOrDefault(p => p.Field.Name == SiteProperties.LargeBidConfirmationEnabled);
                    if (largeBidConfProp != null)
                    {
                        bool.TryParse(largeBidConfProp.Value, out largeBidConfirmationEnabled);
                    }
                    if (largeBidConfirmationEnabled)
                    {
                        //this is an auction and passed validation, assume a parseable "BidAmount" input key exists...
                        decimal attemptedAmount = decimal.Parse(input.Items[Fields.BidAmount], NumberStyles.Number, this.GetCultureInfo());
                        decimal minBid = listing.CurrentPrice ?? 0.0M + listing.Increment ?? 0.0M;
                        if (attemptedAmount >= minBid * 100)
                        {
                            //the bid amount is more than 100x greater than the minimum acceptable bid, confirm a decimal point is not missing...
                            ViewData["LargeBidAmountConfirmationRequired"] = true;
                            ViewData["LowBidAmount"] = attemptedAmount / 100;
                            ViewData["HighBidAmount"] = attemptedAmount;
                        }
                    }
                }
                //missing decimal point check -- end

                //determine final buyer fee details
                decimal finalFeeMin;
                decimal finalFeeMax;
                List<Tier> finalFeeTiers;
                string finalFeeDescription;
                GetFinalBuyerFeeRanges(listing.Type.Name, listing.Categories, out finalFeeMin, out finalFeeMax, out finalFeeTiers, out finalFeeDescription);
                ViewData[Strings.MVC.ViewData_MinFinalBuyerFee] = finalFeeMin;
                ViewData[Strings.MVC.ViewData_MaxFinalBuyerFee] = finalFeeMax;
                ViewData[Strings.MVC.ViewData_FinalBuyerFeeTiers] = finalFeeTiers;
                ViewData[Strings.MVC.ViewData_FinalBuyerFeeDescription] = finalFeeDescription;

                return View(listing);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //StoreValidationIssues(vfc.Detail.ValidationIssues, input);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return View();
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                //PrepareErrorMessage(iafc.Detail.Reason);
                string localizedMessage = this.GlobalResourceString(iafc.Detail.Reason.ToString());
                ModelState.AddModelError("ConfirmActionModal_Error", localizedMessage);
            }
            catch (FaultException<AuthorizationFaultContract> afc)
            {
                if (listing == null)
                {
                    string localizedMessage = this.GlobalResourceString(afc.Detail.Reason.ToString());
                    ModelState.AddModelError("ConfirmActionModal_Error", localizedMessage);
                }
                else
                {
                    string localizedMessage = this.ResourceString(listing.Type.Name + "Listing, " + afc.Detail.Reason.ToString());
                    ModelState.AddModelError("ConfirmActionModal_Error", localizedMessage);
                }
            }
            catch (Exception)
            {
                //PrepareErrorMessage("ConfirmAction", MessageType.Method);
                string localizedMessage = this.GlobalResourceString("ConfirmActionFailure");
                ModelState.AddModelError("ConfirmActionModal_Error", localizedMessage);
            }
            //return RedirectToAction(Strings.MVC.DetailsAction, new { id = ListingID });
            return View();
        }

        /// <summary>
        /// Processes request to submit a listing action (e.g. bid or purchase)
        /// </summary>
        /// <param name="listingID">ID of the requested listing</param>
        /// <returns>
        ///     (success &amp; pmt req'd)   Redirect to /Account/CreateInvoice/[id of new line item]
        ///     (success)               Redirect to /Listing/Details/[listingID]
        ///     (failure)               View("Details", Listing)
        /// </returns>
        //[ExportModelStateToTempData]
        public ActionResult Action(int listingID)
        {
            //IN (populate UserInput)
            string actingUserName = User.Identity.Name;

            UserInput input = new UserInput(actingUserName, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            //foreach (string key in Request.QueryString.AllKeys.Where(k => k!= null))
            //{
            //    if (!input.Items.ContainsKey(key)) input.Items.Add(key, Request.QueryString[key]);
            //}
            input.AddAllQueryStringValues(this);
            try
            {
                //int RaiseGeneralErrorTest = int.Parse("asdf");
                bool accepted;
                ReasonCode reasonCode;
                LineItem newPurchaseLineitem;
                Listing listing = null;
                ListingClient.SubmitListingAction(actingUserName, input, out accepted, out reasonCode, out newPurchaseLineitem, listing);

                if (reasonCode == ReasonCode.AsyncActionWait)
                {
                    //listing action submitted asynchronourly
                    //PrepareSuccessMessage(this.ResourceString(listing.Type.Name + "Listing, ActionQueuedSuccess"), MessageType.Message); --using a spinner image instead

                    if (bool.Parse(ConfigurationManager.AppSettings["ForceAsyncBidWaitForTesting"]))
                    {
                        AutoResetEvent threadSignal = new AutoResetEvent(false);
                        System.Web.HttpContext.Current.Application.Lock();
                        System.Web.HttpContext.Current.Application[User.Identity.Name + listingID.ToString(CultureInfo.InvariantCulture)] = threadSignal;
                        System.Web.HttpContext.Current.Application.UnLock();
                        threadSignal.WaitOne();
                        return new EmptyResult();
                    }
                    else
                    {
                        //return new EmptyResult();
                        return Json("OK", JsonRequestBehavior.AllowGet); // fixes Firefox browser javascript console error "XML Parsing Error: no root element found"
                    }
                }
                else if (accepted && newPurchaseLineitem != null)
                {
                    //if listing owner allows instant purchases AND accepts PayPal, redirect to "checkout" action
                    if (listing == null) listing = ListingClient.GetListingByIDWithFillLevel(actingUserName, listingID, ListingFillLevels.Owner);

                    PrepareSuccessMessage(this.ResourceString(listing.Type.Name + "Listing, ActionSuccess"), MessageType.Message);

                    if (listing.OwnerAllowsInstantCheckout() || SiteClient.BoolSetting(SiteProperties.AutoGenerateInvoices))
                    {
                        return RedirectToAction(Strings.MVC.CheckoutAction, Strings.MVC.AccountController, new { lineitemid = newPurchaseLineitem.ID });
                    }
                    //else
                    //{
                    //    return RedirectToAction("Details", "Listing", new { id = listingID });
                    //}
                }
                else if (accepted)
                {
                    if (listing == null) listing = ListingClient.GetListingByIDWithFillLevel(actingUserName, listingID, string.Empty);
                    //listing action accepted, redirect to listing detail page
                    //PrepareSuccessMessage();
                    PrepareSuccessMessage(this.ResourceString(listing.Type.Name + "Listing, ActionSuccess"), MessageType.Message);
                    //return RedirectToAction("Details", "Listing", new { id = listingID });
                }
                else
                {
                    //display reason listing action was not accepted (e.g. bid amount too low)
                    //prepare ModelState for output
                    //foreach (string key in input.Items.Keys)
                    //{
                    //    if (!ModelState.ContainsKey(key))
                    //    {
                    //        //...add it to the model
                    //        ModelState ms = new ModelState();
                    //        ms.Value = new ValueProviderResult(input.Items[key], input.Items[key], null);
                    //        ModelState.Add(key, ms);
                    //    }
                    //}
                    //ViewData[Strings.MVC.ViewData_Message] = Enum.GetName(typeof(ReasonCode), reasonCode);
                    PrepareErrorMessage(reasonCode);
                    //return RedirectToAction("Details", "Listing", new { id = listingID });
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //TODO: copy over StoreValidationIssues() and CheckValidationIssues() from account controller and apply here

                //display validation errors (any other invalid form value, e.g. non-numeric bid amount)
                //prepare ModelState for output
                foreach (string key in input.Items.Keys)
                {
                    if (!ModelState.ContainsKey(key))
                    {
                        //...add it to the model
                        ModelState ms = new ModelState();
                        ms.Value = new ValueProviderResult(input.Items[key], input.Items[key], null);
                        ModelState.Add(key, ms);
                    }
                }
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                //return RedirectToAction("Details", "Listing", new { id = listingID });
                return Details(listingID);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("Action", e);
                //throw e;
            }
            return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id = listingID });
        }

        #endregion

        #region Make Offer

        /// <summary>
        /// Displays make offer modal content for the specified listing
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        public ActionResult MakeOfferModal(int ListingID)
        {
            string fillLevel = ListingFillLevels.None;
            if (SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers))
            {
                fillLevel += "," + ListingFillLevels.Owner;
            }
            var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);

            ViewData["CCRequired"] = false;
            bool isCcReqExempt = false;
            if (User.Identity.IsAuthenticated)
            {
                var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                isCcReqExempt = (loggedOnUser != null && loggedOnUser.Properties != null)
                    ? loggedOnUser.Properties.GetPropertyValue(StdUserProps.CreditCardRequiredExempt, false) : false;
            }
            if (!isCcReqExempt && 
                User.Identity.IsAuthenticated && 
                SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers) &&
                listing.Owner.CreditCardAccepted() &&
                !User.IsInRole("Admin") && 
                !this.HasUnexpiredCardOnFile(listing.OwnerUserName, this.FBOUserName()))
            {
                PrepareNeutralMessage(Messages.CreditCardRequiredForBuyers);
                //return RedirectToAction(Strings.MVC.AddCardAction, Strings.MVC.AccountController, new { returnUrl = Request.Url.PathAndQuery });
                ViewData["CCRequired"] = true;
            }

            return View(listing);
        }

        /// <summary>
        /// validates an offer for the specified listing and displays confirmation page
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        public ActionResult ConfirmOfferModal(int ListingID)
        {
            string fillLevel = ListingFillLevels.None;
            if (SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers))
            {
                fillLevel += "," + ListingFillLevels.Owner;
            }
            var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);

            ViewData["CCRequired"] = false;

            if (!User.Identity.IsAuthenticated)
            {
                return View(Strings.MVC.MakeOfferModalAction, null);
            }
            else
            {
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                try
                {
                    Offer newOffer;
                    ListingClient.ValidateOffer(input, out newOffer);

                    var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                    bool isCcReqExempt = (loggedOnUser != null && loggedOnUser.Properties != null)
                        ? loggedOnUser.Properties.GetPropertyValue(StdUserProps.CreditCardRequiredExempt, false) : false;
                    if (!isCcReqExempt && 
                        SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers) &&
                        listing.Owner.CreditCardAccepted() &&
                        !User.IsInRole("Admin") && 
                        !this.HasUnexpiredCardOnFile(newOffer.ListingOwnerUsername, this.FBOUserName()))
                    {
                        PrepareNeutralMessage(Messages.CreditCardRequiredForBuyers);
                        //return RedirectToAction(Strings.MVC.AddCardAction, Strings.MVC.AccountController, new { returnUrl = Request.Url.PathAndQuery });
                        ViewData["CCRequired"] = true;
                    }

                    ViewData["Quantity"] = newOffer.Quantity;
                    ViewData["OfferAmount"] = newOffer.Amount;

                    //determine final buyer fee details
                    decimal finalFeeMin;
                    decimal finalFeeMax;
                    List<Tier> finalFeeTiers;
                    string finalFeeDescription;
                    GetFinalBuyerFeeRanges(newOffer.Listing.Type.Name, newOffer.Listing.Categories, out finalFeeMin, out finalFeeMax, out finalFeeTiers, out finalFeeDescription);
                    ViewData[Strings.MVC.ViewData_MinFinalBuyerFee] = finalFeeMin;
                    ViewData[Strings.MVC.ViewData_MaxFinalBuyerFee] = finalFeeMax;
                    ViewData[Strings.MVC.ViewData_FinalBuyerFeeTiers] = finalFeeTiers;
                    ViewData[Strings.MVC.ViewData_FinalBuyerFeeDescription] = finalFeeDescription;
                    return View(newOffer);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                }
                catch (Exception e)
                {
                    PrepareErrorMessage(Strings.MVC.MakeOfferModalAction, e);
                }
            }
            return View(Strings.MVC.MakeOfferModalAction, listing);
        }

        /// <summary>
        /// processes offer for the specified listing
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        /// <returns>JSON result</returns>
        public JsonResult SubmitOffer(int ListingID)
        {
            JsonResult result = new JsonResult();
            if (!User.Identity.IsAuthenticated)
            {
                result.Data = new { status = "LOGON_NEEDED" };
            }
            else
            {
                var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                bool isCcReqExempt = (loggedOnUser != null && loggedOnUser.Properties != null)
                    ? loggedOnUser.Properties.GetPropertyValue(StdUserProps.CreditCardRequiredExempt, false) : false;
                if (!isCcReqExempt && 
                    SiteClient.BoolSetting(SiteProperties.RequireCreditCardForBuyers) && 
                    !User.IsInRole("Admin"))
                {
                    var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, ListingFillLevels.Owner);
                    if (listing != null && 
                        listing.Owner.CreditCardAccepted() && 
                        !this.HasUnexpiredCardOnFile(listing.OwnerUserName, this.FBOUserName()))
                    {
                        result.Data = new { status = "CC_NEEDED" };
                        return result;
                    }
                }

                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                try
                {
                    Offer newOffer;
                    ListingClient.SendOffer(input, out newOffer);
                    result.Data = new { status = "OK", offerDetails = newOffer };
                }
                catch (Exception e)
                {
                    result.Data = new { status = "ERROR", errorDetails = e.Message };
                }
            }
            return result;
        }

        /// <summary>
        /// displays all relevant offers for the specified listing where the authenticated user is involved
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        /// <param name="SelectedOfferID">if specified, this offer will be highlighted for a response</param>
        /// <param name="CounterOffer">when true, shows counter offer form</param>
        /// <param name="DeclineOffer">when true, shows decline offer form</param>
        /// <param name="returnUrl">if specified, the back button will go to this url instead of the listing detail page</param>
        [Authorize]
        public ActionResult ManageOffers(int ListingID, int? SelectedOfferID, bool? CounterOffer, bool? DeclineOffer, string returnUrl)
        {
            string fillLevel = ListingFillLevels.Actions + "," + ListingFillLevels.CurrentAction + "," + ListingFillLevels.Properties;
            if (SiteClient.EnableEvents)
            {
                fillLevel += "," + ListingFillLevels.LotEvent;
            }
            Listing listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);
            ViewData["CurrentListing"] = listing;
            var offers = ListingClient.SearchOffersByUser(User.Identity.Name, this.FBOUserName(), 
                "All", "All", ListingID.ToString(), "ListingID", 0, 0, "CreatedOn", true).List;
            if (SelectedOfferID.HasValue)
            {
                ViewData["SelectedOffer"] = offers.FirstOrDefault(o => o.ID == SelectedOfferID.Value);
            }
            ViewData["CounterOffer"] = CounterOffer ?? false;
            ViewData["DeclineOffer"] = DeclineOffer ?? false;
            ViewData["returnUrl"] = returnUrl;
            return View(offers);
        }

        /// <summary>
        /// processes request to decline an offer for the specified listing
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        /// <param name="returnUrl">if specified, redirects to this url for either error or success results</param>
        [Authorize]
        public ActionResult DeclineOffer(int ListingID, string returnUrl)
        {
            try
            {
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                ListingClient.DeclineOffer(input);
                PrepareSuccessMessage("DeclineOffer", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DeclineOffer", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ManageOffersAction, new { ListingID });
        }

        /// <summary>
        /// processes request to accept the specified offer for the specified listing
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        /// <param name="OfferID">the id of the specified offer</param>
        /// <param name="returnUrl">if specified, redirects to this url for success result</param>
        /// <returns>if successful, redirects to the appropriate my account > sales report view; if a failure occcurs, redirects back to the manage offers view</returns>
        [Authorize]
        public ActionResult AcceptOffer(int ListingID, int OfferID, string returnUrl)
        {
            try
            {
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllQueryStringValues(this);
                input.AddAllFormValues(this);
                ListingClient.AcceptOffer(input);
                PrepareSuccessMessage("AcceptOffer", MessageType.Method);

                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    var offer = ListingClient.GetOfferById(User.Identity.Name, OfferID);
                    if (this.FBOUserName() == offer.ListingOwnerUsername)
                    {
                        if (offer.Listing.Lot != null)
                        {
                            return RedirectToAction(Strings.MVC.EventSalesTransactionReportAction, Strings.MVC.AccountController, new { payer = offer.BuyingUser });
                        }
                        else
                        {
                            return RedirectToAction(Strings.MVC.SalesTransactionReportAction, Strings.MVC.AccountController, new { payer = offer.BuyingUser });
                        }
                    }
                    else
                    {
                        return RedirectToAction(Strings.MVC.BiddingWonAction, Strings.MVC.AccountController, new { ViewFilterOption = "Unpaid", SortFilterOptions = 0, SearchType = "User", SearchTerm = offer.ListingOwnerUsername });
                    }
                }
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                if (iafc.Detail.Reason == ReasonCode.ListingNotExist)
                {
                    ModelState.AddModelError("ListingID", "ListingNotFound");
                }
                else if (iafc.Detail.Reason == ReasonCode.OfferNotExist)
                {
                    ModelState.AddModelError("OfferID", "OfferNotFound");
                }
                else
                {
                    PrepareErrorMessage("AcceptOffer", iafc);
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("AcceptOffer", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ManageOffersAction, new { ListingID, SelectedOfferID = OfferID });
        }

        /// <summary>
        /// processes request to submit a counteroffer to the specified offer for the specified listing
        /// </summary>
        /// <param name="ListingID">the id of the specified listing</param>
        /// <param name="OfferID">the id of the specified offer</param>
        /// <param name="returnUrl">if specified, redirects to this url for success result</param>
        /// <returns>for validation results, renders the manage offers view with validation results displayed, otherwise (success or unexpected error) redirects to the manage offers view</returns>
        [Authorize]
        public ActionResult SubmitCounteroffer(int ListingID, int OfferID, string returnUrl)
        {
            try
            {
                UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.AddAllFormValues(this);
                Offer newOffer;
                ListingClient.SendCounterOffer(input, out newOffer);
                PrepareSuccessMessage("SubmitCounteroffer", MessageType.Method);

                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction(Strings.MVC.ManageOffersAction, new { ListingID, SelectedOfferID = newOffer.ID });
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                Listing listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, string.Empty);
                ViewData["CurrentListing"] = listing;
                var offers = ListingClient.SearchOffersByUser(User.Identity.Name, this.FBOUserName(),
                    "All", "All", ListingID.ToString(), "ListingID", 0, 0, "CreatedOn", true).List;
                ViewData["SelectedOffer"] = offers.FirstOrDefault(o => o.ID == OfferID);
                ViewData["CounterOffer"] = true;
                ViewData["DeclineOffer"] = false;
                return View(Strings.MVC.ManageOffersAction, offers);
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                if (iafc.Detail.Reason == ReasonCode.ListingNotExist)
                {
                    ModelState.AddModelError("ListingID", "ListingNotFound");
                }
                else if (iafc.Detail.Reason == ReasonCode.OfferNotExist)
                {
                    ModelState.AddModelError("OfferID", "OfferNotFound");
                }
                else
                {
                    PrepareErrorMessage("SubmitCounteroffer", iafc);
                }
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("SubmitCounteroffer", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ManageOffersAction, new { ListingID, SelectedOfferID = OfferID });
        }

        #endregion Make Offer

        #region Bid / Purchase History

        /// <summary>
        /// Displays a page of the list of -accepted- listing actions (i.e. bids or purchases) for the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <param name="currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;ListingAction&gt;)</returns>
        [Authorize]
        public ActionResult History(int id, string currency, string sort, int? page, bool? descending)
        {
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
            ViewData[Strings.Fields.Currency] = currency ?? "USD";
            string fillLevel = ListingFillLevels.Actions + "," + ListingFillLevels.CurrentAction + "," + ListingFillLevels.Properties;
            if (SiteClient.EnableEvents)
            {
                fillLevel += "," + ListingFillLevels.LotEvent;
            }
            Listing listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, id, fillLevel);
            ViewData["CurrentListing"] = listing;

            List<Offer> offers = null;
            if (listing.OfferCount > 0)
            {
                offers = ListingClient.GetOffersByListingId(User.Identity.Name, listing.ID);
            }
            ViewData["Offers"] = offers;

            ViewData[Fields.SellerUserName] = listing.OwnerUserName;
            /* i.e. return all bids, accepted or not, for an auction listing
            return
            View(ListingClient.GetAllListingActions(User.Identity.Name
                   , id, page == null ? 0 : (int)page, SiteClient.PageSize
                   , string.IsNullOrEmpty(sort) ? Strings.Fields.ActionDTTM
                        : sort, descending ?? true));
            */

            /* i.e. return only accepted bids for an auction listing */
            return
            View(ListingClient.GetAcceptedListingActions(User.Identity.Name
                    , id, page == null ? 0 : (int)page, SiteClient.PageSize
                    , string.IsNullOrEmpty(sort) ? Strings.Fields.ActionDTTM
                        : sort, descending ?? true));
        }

        /// <summary>
        /// Displays a page of the list of -all- listing actions (i.e. bids or purchases) for the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <param name="currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;ListingAction&gt;)</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult Audit(int id, string currency, string sort, int? page, bool? descending)
        {
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
            ViewData[Strings.Fields.Currency] = currency ?? "USD";
            string fillLevel = ListingFillLevels.Actions + "," + ListingFillLevels.CurrentAction + "," + ListingFillLevels.Properties;
            if (SiteClient.EnableEvents)
            {
                fillLevel += "," + ListingFillLevels.LotEvent;
            }
            Listing listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, id, fillLevel);
            ViewData["CurrentListing"] = listing;
            ViewData[Fields.SellerUserName] = listing.OwnerUserName;

            /* i.e. return all bids, accepted or not, for an auction listing */
            return
            View("History", ListingClient.GetAllListingActions(User.Identity.Name
                   , id, page == null ? 0 : (int)page, SiteClient.PageSize
                   , string.IsNullOrEmpty(sort) ? Strings.Fields.ActionDTTM
                        : sort, descending ?? true));

            /* i.e. return only accepted bids for an auction listing 
            return
            View(ListingClient.GetAcceptedListingActions(User.Identity.Name
                    , id, page == null ? 0 : (int)page, SiteClient.PageSize
                    , string.IsNullOrEmpty(sort) ? Strings.Fields.ActionDTTM
                        : sort, descending ?? true));
            */
        }

        /// <summary>
        /// Processes request to undo a listing action (e.g. retract the last bid)
        /// </summary>
        /// <param name="listingID">ID of the requested listing</param>
        /// <param name="listingActionID">ID of the requested listing action to be undone</param>
        /// <returns>Redirect to calling page url</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult UndoListingAction(int listingID, int listingActionID)
        {
            ListingClient.UndoListingAction(User.Identity.Name, listingID, listingActionID);
            return Redirect(Request.UrlReferrer.PathAndQuery);
        }

        /// <summary>
        /// Processes request to roll back listing actions to to the specified listing action (e.g. retract a middle bid and all newer bids)
        /// </summary>
        /// <param name="listingID">ID of the requested listing</param>
        /// <param name="listingActionID">ID of the requested listing action to be rolled back to</param>
        /// <returns>Redirect to calling page url</returns>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult RollbackListingActionsByID(int listingID, int listingActionID)
        {
            ListingClient.RollbackListingActionsByID(User.Identity.Name, listingID, listingActionID);
            return Redirect(Request.UrlReferrer.PathAndQuery);
        }

        #endregion

        #region End Early & Delete

        /// <summary>
        /// Processes request to end the specified listing early
        /// </summary>
        /// <param name="id">ID of the requested listing to be ended early</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Listing/Details/[id]</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult EndEarly(int id, string returnUrl)
        {
            try
            {
                ListingClient.EndListingEarly(User.Identity.Name, id);
                PrepareSuccessMessage("EndEarly", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("EndEarly", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.DetailsAction, new { id });
        }

        /// <summary>
        /// Processes request to start the specified scheduled listing immediately
        /// </summary>
        /// <param name="id">ID of the requested listing to be ended early</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Listing/Details/[id]</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult StartNow(int id, string returnUrl)
        {
            try
            {
                ListingClient.StartListingNow(User.Identity.Name, id);
                PrepareSuccessMessage("StartNow", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("StartNow", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.DetailsAction, new { id });
        }

        /// <summary>
        /// Processes request to delete the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing to be deleted</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to site homepage</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult Delete(int id, string returnUrl)
        {
            ListingClient.DeleteListing(User.Identity.Name, id);
            PrepareSuccessMessage("Delete", MessageType.Method);

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
        }

        #endregion

        #region Site Map Placeholder Redirects

        /// <summary>
        /// Redirect helper
        /// </summary>
        /// <returns>Redirects to "Browse" action</returns>
        public ActionResult BrowseCategories() { return RedirectToAction(Strings.MVC.BrowseAction); }
        /// <summary>
        /// Redirect helper
        /// </summary>
        /// <returns>Redirects to "Browse" action</returns>
        public ActionResult BrowseRegions() { return RedirectToAction(Strings.MVC.BrowseAction); }

        #endregion Site Map Placeholder Redirects

        #region Public Q & A

        /// <summary>
        /// processes an AJAX request to post a public listing question
        /// </summary>
        /// <param name="ListingID">the Id of the subject listing</param>
        /// <param name="Question">the question text to post</param>
        public JsonResult PostListingQuestion(int ListingID, string Question)
        {
            JsonResult result = new JsonResult();
            if (User.Identity.IsAuthenticated)
            {
                try
                {
                    //un-comment this block to prevent sellers from posting public questions on their own listings
                    //var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, string.Empty);
                    //if (listing.OwnerUserName.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase))
                    //    throw new Exception(this.GlobalResourceString(ReasonCode.CantAskQuestionOnOwnListing.ToString()));

                    int newQuestionId;
                    ListingClient.AskListingQuestion(User.Identity.Name, this.FBOUserName(), ListingID, Question, out newQuestionId);
                    result.Data = new { result = "OK" };
                    PrepareSuccessMessage("PostListingQuestion", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    string errorMessage = string.Empty;
                    string delim = string.Empty;
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        string localizedMessage = this.ValidationResourceString(issue.Message);
                        errorMessage += delim + localizedMessage;
                        delim = "<br>";
                    }
                    result.Data = new { result = "INVALID", error = errorMessage };
                }
                catch (FaultException<InvalidOperationFaultContract> iofc)
                {
                    string localizedMessage = this.GlobalResourceString(iofc.Detail.Reason.ToString());
                    result.Data = new { result = "INVALID_ARGUMENT", error = localizedMessage };
                }
                catch (FaultException<InvalidArgumentFaultContract> iafc)
                {
                    string localizedMessage = this.GlobalResourceString(iafc.Detail.Reason.ToString());
                    result.Data = new { result = "INVALID_ARGUMENT", error = localizedMessage };
                }
                catch (FaultException<AuthorizationFaultContract> afc)
                {
                    string localizedMessage = this.GlobalResourceString(afc.Detail.Reason.ToString());
                    result.Data = new { result = "NOT_AUTHORIZED", error = localizedMessage };
                }
                catch (Exception e)
                {
                    result.Data = new { result = "ERROR", error = e.Message };
                }
            }
            else
            {
                PrepareErrorMessage(this.GlobalResourceString("LoginRequiredToPostQuestion"), MessageType.Message);
                result.Data = new { result = "LOGON_NEEDED" };
            }
            return result;
        }

        /// <summary>
        /// processes an AJAX request to answer a public listing question
        /// </summary>
        /// <param name="QuestionID">the ID of the question being answered</param>
        /// <param name="Answer">the answer text to post</param>
        public JsonResult AnswerQuestion(int QuestionID, string Answer)
        {
            JsonResult result = new JsonResult();
            if (User.Identity.IsAuthenticated)
            {
                try
                {
                    ListingClient.AnswerListingQuestion(User.Identity.Name, QuestionID, Answer);
                    result.Data = new { result = "OK" };
                    PrepareSuccessMessage("AnswerQuestion", MessageType.Method);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    string errorMessage = string.Empty;
                    string delim = string.Empty;
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        string localizedMessage = this.ValidationResourceString(issue.Message);
                        errorMessage += delim + localizedMessage;
                        delim = "<br>";
                    }
                    result.Data = new { result = "INVALID", error = errorMessage };
                }
                catch (FaultException<InvalidArgumentFaultContract> iafc)
                {
                    string localizedMessage = this.GlobalResourceString(iafc.Detail.Reason.ToString());
                    result.Data = new { result = "INVALID_ARGUMENT", error = localizedMessage };
                }
                catch (FaultException<AuthorizationFaultContract> afc)
                {
                    string localizedMessage = this.GlobalResourceString(afc.Detail.Reason.ToString());
                    result.Data = new { result = "NOT_AUTHORIZED", error = localizedMessage };
                }
                catch (Exception e)
                {
                    result.Data = new { result = "ERROR", error = e.Message };
                }
            }
            else
            {
                PrepareErrorMessage(this.GlobalResourceString("LoginRequiredToAnswerQuestion"), MessageType.Message);
                result.Data = new { result = "LOGON_NEEDED" };
            }
            return result;
        }

        /// <summary>
        /// Processes request to delete a listing question
        /// </summary>
        /// <param name="QuestionID">the ID of the question to delete</param>
        /// <param name="returnUrl">the return URL to redirect to after success or failure (default: /Home/Index if missing)</param>
        public ActionResult DeleteQuestion(int QuestionID, string returnUrl)
        {
            //redirect to logon page if not authenticated
            if (!User.Identity.IsAuthenticated)
            {
                string thisUrl = this.GetActionUrl(Strings.MVC.DeleteQuestionAction, new { QuestionID, returnUrl });
                return RedirectToAction(Strings.MVC.LogOnAction, Strings.MVC.AccountController, new { returnUrl = thisUrl });
            }
            try
            {
                ListingClient.DeleteListingQuestion(User.Identity.Name, QuestionID);
                PrepareSuccessMessage("DeleteQuestion", MessageType.Method);
            }
            catch(Exception)
            {
                PrepareErrorMessage("DeleteQuestion", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            //last resort, redirect to homepage if returnUrl is missing or invalid
            return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
        }

        #endregion Public Q & A

    }
}
