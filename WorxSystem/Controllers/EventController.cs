using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Queueing;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Unity;
using RainWorx.FrameWorx.Utility;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to event-specific MVC requests
    /// </summary>
    [GoUnsecure]
    [Authenticate]
    public class EventController : AuctionWorxController
    {

        #region Create Lot

        /// <summary>
        /// Displays "You do not currently have permission to post listings..." error message.
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult NoPermissionToSell()
        {
            return View();
        }

        /// <summary>
        /// Displays form to enter initial lot details (page 1 of 2)
        /// </summary>
        /// <param name="eventID">the optional id of the event to pre-select</param>
        /// <param name="SimilarLotID">the optional id of the lot to pre-fill form fields with</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success (after CreateLotPage2) when no fees are owed</param>
        /// <returns>
        ///     (not logged in)     Redirect to /Account/LogOn/...
        ///     (not a seller)      Redirect to /Listing/NoPermissionToSell
        ///     (success)           View()
        /// </returns>        
        public ActionResult CreateLotPage1(int? eventID, int? SimilarLotID, string ReturnUrl)
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
                string loginRetUrl = urlHelper.Action(Strings.MVC.CreateLotPage1Action, new { SimilarLotID, ReturnUrl });
                //return RedirectToAction("LogOn", "Account", new { returnURL = VirtualPathUtility.ToAbsolute("~/") + "Listing/CreateListingPage1" });
                return RedirectToAction("LogOn", "Account", new { returnURL = loginRetUrl });
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
            if (SimilarLotID.HasValue)
            {
                try
                {
                    Lot sourceLot = EventClient.GetLotByIDWithFillLevel(this.FBOUserName(), SimilarLotID.Value, Strings.ListingFillLevels.Default);
                    if (sourceLot.Listing.OwnerUserName != this.FBOUserName())
                    {
                        //note - an attempt by any user other than the owner to list a similar item
                        //  intentionally just shows a "Listing not found" error.
                        ModelState.AddModelError("SimilarLotID", "LotNotFound");
                    }
                    else
                    {
                        int? categoryID = sourceLot.Listing.PrimaryCategory.ID;
                        //int? storeID = null; // not yet implemented
                        Category leafRegion = sourceLot.Listing.LeafRegion();
                        int? regionID = (leafRegion != null ? (int?)leafRegion.ID : null);
                        eventID = sourceLot.Event.ID;
                        string listingType = sourceLot.Listing.Type.Name;

                        ViewData[Strings.Fields.CategoryID] = categoryID;
                        //ViewData[Strings.Fields.StoreID] = storeID;
                        ViewData[Strings.Fields.RegionID] = regionID;
                        //ViewData[Strings.Fields.EventID] = eventID;                        
                        ViewData[Strings.Fields.ListingType] = listingType;
                        ViewData[Strings.Fields.SimilarLotID] = SimilarLotID.Value;

                        /**********************************************************************\
                         * Uncomment this block to skip page 1 when creating a similar listing
                         * 
                            TempData["SimilarLotID"] = SimilarLotID.Value;
                            return RedirectToAction("CreateLotPage2", new { 
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
                        ModelState.AddModelError("SimilarLotID", "LotNotFound");
                    }
                    else
                    {
                        PrepareErrorMessage(Strings.MVC.CreateLotPage1Action, iafc);
                    }
                }
                catch (Exception e)
                {
                    PrepareErrorMessage(Strings.MVC.CreateLotPage1Action, e);
                }
            }

            //event data
            string statuses = Strings.AuctionEventStatuses.Draft +
                        "," + Strings.AuctionEventStatuses.Preview +
                        "," + Strings.AuctionEventStatuses.Scheduled +
                        "," + Strings.AuctionEventStatuses.Active +
                        "," + Strings.AuctionEventStatuses.Closing /*+
                        "," + Strings.AuctionEventStatuses.Closed +
                        "," + Strings.AuctionEventStatuses.Archived*/;
            var eligibleEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                0, 0, Strings.Fields.Title, false, EventFillLevels.None).List;

            if (eligibleEvents.Count == 0)
            {
                return RedirectToAction(Strings.MVC.EventNeededForNewLotAction, new
                {
                    returnUrl = this.GetActionUrl(Strings.MVC.CreateLotPage1Action, Strings.MVC.EventController, new { returnUrl = ReturnUrl }),
                    cancelUrl = ReturnUrl
                });
            }

            ViewData[Strings.Fields.EventID] = new SelectList(eligibleEvents, Strings.Fields.ID, Strings.Fields.Title, eventID);

            //store data
            //ViewData[Strings.Fields.StoreID] = new SelectList(CommonClient.GetChildCategories(28), Strings.Fields.ID, Strings.Fields.Name);
            return View();
        }

        /// <summary>
        /// Displays page informing seller that they must create an event before copying/moving lots
        /// </summary>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult EventNeeded(string returnUrl)
        {
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            return View();
        }

        /// <summary>
        /// Displays page informing seller that they must create an event before creating a lot
        /// </summary>
        public ActionResult EventNeededForNewLot(string returnUrl, string cancelUrl)
        {
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            ViewData[Strings.Fields.CancelUrl] = cancelUrl;
            return View();
        }

        /// <summary>
        /// Processes request to enter initial listing details (page 1 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="SimilarLotID">the optional id of the lot to prefill forms fields with</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success (after CreateLotPage2) when no fees are owed</param>
        /// <returns>
        ///     (success)   Redirect to /Listing/CreateListingPage2
        ///     (errors)    CreateListingPage1()
        /// </returns>        
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CreateLotPage1(int? CategoryID, int? StoreID, int? RegionID, int? EventID, string ListingType, int? SimilarLotID, string ReturnUrl)
        {
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.SimilarLotID] = SimilarLotID;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;

            var bSkipRedirect = !string.IsNullOrEmpty(Request["FromStep2"]);
            if (!CategoryID.HasValue || string.IsNullOrEmpty(ListingType) || bSkipRedirect)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Strings.Messages.SelectCategoryAndListingType);
                return CreateLotPage1(null, null, ReturnUrl);
            }
            if (!string.IsNullOrEmpty(Request["FromPage2"]))
            {
                return CreateLotPage1(null, null, ReturnUrl);
            }

            //if creating a similar listing, get the appropriate value and redirect to CreateListingPage2
            if (SimilarLotID.HasValue)
            {
                try
                {
                    Lot sourceLot = EventClient.GetLotByIDWithFillLevel(this.FBOUserName(), SimilarLotID.Value, string.Empty);
                    if (sourceLot.Listing.OwnerUserName != this.FBOUserName())
                    {
                        //note - an attempt by any user other than the owner to list a similar item
                        //  intentionally just shows a "Lot not found" error.
                        ModelState.AddModelError("SimilarLotID", "LotNotFound");
                    }
                    else
                    {
                        //pass along the listing ID to use to pre-fill the CreateListingPage2 form
                        TempData["SimilarLotID"] = SimilarLotID.Value;
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason == ReasonCode.LotNotExist)
                    {
                        ModelState.AddModelError("SimilarLotID", "LotNotFound");
                    }
                    else
                    {
                        PrepareErrorMessage(Strings.MVC.CreateLotPage1Action, iafc);
                    }
                }
                catch (Exception e)
                {
                    PrepareErrorMessage(Strings.MVC.CreateLotPage1Action, e);
                }
            }

            return RedirectToAction(Strings.MVC.CreateLotPage2Action, new { CategoryID, StoreID, RegionID, EventID, ListingType, ReturnUrl });
        }

        /// <summary>
        /// Displays form to enter remaining new listing details (page 2 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success when no fees are owed</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult CreateLotPage2(int CategoryID, int? StoreID, int? RegionID, int EventID, string ListingType, string ReturnUrl)
        {
            //get the user's culture info
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(ListingType, "Site");
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Select list for Shipping
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(SiteClient.ShippingMethods, Strings.Fields.ID, Strings.Fields.Name);

            //ViewData for input parameters to bounce back
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, EventID, Strings.EventFillLevels.None);
            //ViewData[Strings.MVC.EventDetails] = auctionEvent;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.Fields.Currency] = auctionEvent.Currency.Code;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.AddListing;

            //ViewData[Strings.MVC.LineageString] =
            //    CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].ToLineageString(Strings.Fields.Name, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].LocalizedCategoryLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });

            //populate view data with seller details
            ViewData[Strings.Fields.Seller] = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());

            if (TempData["SimilarLotID"] != null)
            {
                int similarLotID = (int)TempData["SimilarLotID"];
                Lot existingLot = EventClient.GetLotByIDWithFillLevel(this.FBOUserName(), similarLotID, ListingFillLevels.Default);

                ViewData[Strings.Fields.Title] = existingLot.Listing.Title;
                ViewData[Strings.Fields.Subtitle] = existingLot.Listing.Subtitle;
                ViewData[Strings.Fields.Description] = existingLot.Listing.Description;

                //custom fields
                ModelState.FillProperties(existingLot.Listing.Properties, cultureInfo);

                //Create modelstate for each location
                foreach (Location location in existingLot.Listing.Locations)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                    ModelState.Add("location_" + location.ID, ms);
                }

                //Create modelstate for each decoration
                foreach (Decoration decoration in existingLot.Listing.Decorations)
                {
                    //Add Model control
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                    ModelState.Add("decoration_" + decoration.ID, ms);
                }

                //Create model state for each html field with a different name than the corresponding model property
                if (existingLot.Listing.OriginalPrice.HasValue)
                {
                    string originalPrice = existingLot.Listing.OriginalPrice.Value.ToString("N2", cultureInfo);
                    ModelState.Add(Strings.Fields.Price, new ModelState()
                    {
                        Value =
                            new ValueProviderResult(existingLot.Listing.OriginalPrice,
                                                    originalPrice, null)
                    });
                }

                string quantity = existingLot.Listing.OriginalQuantity.ToString(cultureInfo);
                ModelState.Add(Strings.Fields.Quantity, new ModelState()
                {
                    Value = new ValueProviderResult(existingLot.Listing.OriginalQuantity, quantity, cultureInfo)
                });

                //Shipping Methods and Options
                List<ShippingOption> existingShippingOpts = existingLot.Listing.ShippingOptions;
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
                var allProperties = existingLot.Listing.AllCustomItemProperties(
                    CommonClient.GetFieldsByCategoryID(existingLot.Listing.PrimaryCategory.ID));
                PruneListingCustomPropertiesEditability(this.FBOUserName(), this.FBOUserName(), allProperties);
                ViewData[Strings.Fields.ItemProperties] = allProperties;

                //Media
                ViewData[Strings.Fields.Media] = this.CloneMediaAssets(existingLot.Listing.Media);

            } // if (TempData["SimilarLotID"] != null)
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
                ModelState.FillProperties(itemProperties, cultureInfo);

                ViewData[Strings.Fields.ItemProperties] = itemProperties;

                ViewData[Fields.IsTaxable] = auctionEvent.LotsTaxable;
            }

            return View();
        }

        /// <summary>
        /// Processes request to enter remaining new listing details (page 2 of 2)
        /// </summary>
        /// <param name="CategoryID">ID of the requested listing category</param>
        /// <param name="StoreID">ID of the requested store</param>
        /// <param name="RegionID">ID of the requested region</param>
        /// <param name="EventID">ID of the requested event</param>
        /// <param name="ListingType">name of the requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="LineageString">represents all categories to be assigned to the new listing</param>
        /// <param name="ReturnUrl">the optional url to redirect to upon success when no fees are owed</param>
        /// <returns>
        ///     (success, pmt req'd)            Redirect to /Account/Fees
        ///     (success, ReturnUrl specified)  Redirect to [ReturnUrl]
        ///     (success)                       Redirect to /Listing/ListingConfirmation/[id of new listing]
        ///     (validation errors)             View()
        /// </returns>        
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult CreateLotPage2(int CategoryID, int? StoreID, int? RegionID, int EventID, string ListingType, string LineageString, string ReturnUrl)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            //IN (populate UserInput and prepare ModelState for output)            
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
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
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.RegionID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.RegionID])).Trees[int.Parse(Request.Form[Strings.Fields.RegionID])].LineageString);
            if (!string.IsNullOrEmpty(Request.Form[Strings.Fields.CategoryID])) lineages.Add(CommonClient.GetCategoryPath(int.Parse(Request.Form[Strings.Fields.CategoryID])).Trees[int.Parse(Request.Form[Strings.Fields.CategoryID])].LineageString);
            string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
            input.Items.Add(Strings.Fields.AllCategories, categories);

            input.Items.Remove("ThumbnailRendererState");
            input.Items.Remove("YouTubeRendererState");
            input.Items.Remove("ShippingRenderState");
            input.Items.Remove("files[]");
            input.Items.Remove("ReturnUrl");

            //do call to BLL
            try
            {
                int listingID;
                bool payToProceed = EventClient.CreateLot(User.Identity.Name, input, true, out listingID);

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
                    PrepareSuccessMessage(Strings.MVC.CreateLotPage2Action, MessageType.Method);
                    return Redirect(ReturnUrl);
                }
                else
                {
                    return RedirectToAction(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { id = EventID });
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
            ViewData[Strings.Fields.ItemProperties] = itemProperties;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(ListingType, "Site");
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

            //MakeOfferEnabled
            bool makeOfferEnabled = false;
            CustomProperty makeOfferEnabledProp = listingTypeProperties.FirstOrDefault(ltp => ltp.Field.Name == SiteProperties.EnableMakeOffer);
            if (makeOfferEnabledProp != null)
            {
                bool.TryParse(makeOfferEnabledProp.Value, out makeOfferEnabled);
            }
            ViewData[SiteProperties.EnableMakeOffer] = makeOfferEnabled;

            //Select list for Shipping
            ViewData[Strings.Fields.ShippingMethod] = new SelectList(SiteClient.ShippingMethods, Strings.Fields.ID, Strings.Fields.Name);

            //ViewData for input parameters to bounce back
            ViewData[Strings.Fields.CategoryID] = CategoryID;
            ViewData[Strings.Fields.StoreID] = StoreID;
            ViewData[Strings.Fields.RegionID] = RegionID;
            ViewData[Strings.Fields.EventID] = EventID;
            ViewData[Strings.Fields.ListingType] = ListingType;
            ViewData[Strings.Fields.ReturnUrl] = ReturnUrl;
            ViewData[Strings.Fields.Currency] = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, EventID,
                EventFillLevels.None).Currency.Code;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.AddListing;

            ViewData[Strings.MVC.LineageString] = LineageString;

            //populate view data with seller details
            ViewData[Strings.Fields.Seller] = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());

            //View Prep
            ViewData[Strings.Fields.Description] = input.Items[Strings.Fields.Description];

            return View();
        }

        #endregion

        #region Edit Lot

        /// <summary>
        /// Displays form to edit lot
        /// </summary>
        /// <param name="id">ID of the requested lot to be edited</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>
        /// (auth. success) View(Lot)
        /// (auth. failure) Redirect to /Listing/Detail/[id]
        /// </returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult EditLot(int id, string returnUrl)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);

            //get the existing Lot object
            Lot existingLot = EventClient.GetLotByIDWithFillLevel(actingUN, id, ListingFillLevels.Default);

            if (existingLot == null)
            {
                //handle non-existant listing # on detail page
                return RedirectToAction(Strings.MVC.LotDetailsAction, new { id = id });
            }

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (existingLot.Listing.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.LotDetailsAction, new { id = id });
            }

            //get list of fields/field groups allowed to be edited in the current context
            Dictionary<string, bool> editableFields = ListingClient.GetUpdateableListingFields(actingUN, existingLot.Listing);
            if (editableFields.Values.Where(v => v == true).Count() == 0)
            {
                //zero editable fields - non-admin + closed listing?
                return RedirectToAction(Strings.MVC.LotDetailsAction, new { id = id });
            }
            ViewData[Strings.MVC.ViewData_EditableFieldList] = editableFields;

            //Create modelstate for each property (for initial render)
            ModelState.FillProperties(existingLot.Listing.Properties, cultureInfo);

            //ensure initial model state for IsTaxable, in case this is an old listing which doesn't have the "IsTaxable" property yet
            if (!SiteClient.BoolSetting(SiteProperties.HideTaxFields))
            {
                if (!existingLot.Listing.Properties.Any(lp => lp.Field.Name == Fields.IsTaxable))
                {
                    ViewData[Fields.IsTaxable] = existingLot.Event.LotsTaxable;
                }
            }

            //Create modelstate for each location
            foreach (Location location in existingLot.Listing.Locations)
            {
                //Add Model control
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                ModelState.Add("location_" + location.ID, ms);
            }

            //Create modelstate for each decoration
            foreach (Decoration decoration in existingLot.Listing.Decorations)
            {
                //Add Model control
                ModelState ms = new ModelState();
                ms.Value = new ValueProviderResult(Strings.MVC.TrueValue, Strings.MVC.TrueValue, null);
                ModelState.Add("decoration_" + decoration.ID, ms);
            }

            //Create model state for each html field with a different name than the corresponding model property
            if (existingLot.Listing.OriginalPrice.HasValue)
            {
                string originalPrice = existingLot.Listing.OriginalPrice.Value.ToString("N2", cultureInfo);
                ModelState.Add(Strings.Fields.Price, new ModelState()
                {
                    Value =
                        new ValueProviderResult(existingLot.Listing.OriginalPrice,
                                                originalPrice, null)
                });
            }
            string quantity = existingLot.Listing.CurrentQuantity.ToString(cultureInfo);
            ModelState.Add(Strings.Fields.Quantity, new ModelState()
            {
                Value = new ValueProviderResult(existingLot.Listing.CurrentQuantity, quantity, null)
            });
            ModelState.Add(Strings.Fields.CategoryID, new ModelState()
            {
                Value = new ValueProviderResult(existingLot.Listing.PrimaryCategory.ID,
                    existingLot.Listing.PrimaryCategory.ID.ToString(), null)
            });

            //ReturnUrl, Listing Type, Event view data
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            ViewData[Strings.Fields.ListingType] = existingLot.Listing.Type.Name;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.UpdateListing;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(existingLot.Listing.Type.Name, "Site");

            //regions
            if (SiteClient.BoolSetting(SiteProperties.EnableRegions))
            {
                Category leafRegion = existingLot.Listing.LeafRegion();
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

            //Shipping Methods and Options
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

            List<ShippingOption> existingShippingOpts = existingLot.Listing.ShippingOptions;
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
            var propertiesToEdit = existingLot.Listing.AllCustomItemProperties(
                CommonClient.GetFieldsByCategoryID(existingLot.Listing.PrimaryCategory.ID));
            PruneListingCustomPropertiesEditability(this.FBOUserName(), existingLot.Listing.OwnerUserName, propertiesToEdit);
            ViewData[Strings.Fields.Properties] = propertiesToEdit;

            //Media
            ViewData[Strings.Fields.Media] = existingLot.Listing.Media;

            //Currency (needed so CreateListingPage2 can use the same shared "XXXListingFields" user control)
            ViewData[Strings.Fields.Currency] = existingLot.Listing.Currency.Code;

            return View(Strings.MVC.EditLotAction, existingLot);
        }

        /// <summary>
        /// Processes request to edit the specified lot
        /// </summary>
        /// <param name="id">ID of the requested lot to be edited</param>
        /// <param name="CategoryID">ID of the requested listing category</param>        
        /// <param name="ListingType">Name of requested listing type (e.g. "Auction", "FixedPrice")</param>
        /// <param name="LineageString">represents all categories to be assigned to the new listing</param>
        /// <param name="Currency">3-character code of the requested currency (e.g. "USD", "AUD", "JPY")</param>
        /// <param name="returnUrl">the optional url to redirect to upon success</param>
        /// <returns>
        ///     (success)               Redirect to /Listing/Details/[id]
        ///     (auth. failure)         Redirect to /Listing/Details/[id]
        ///     (validation errors)     View(Listing)
        /// </returns>
        //TODO: all parameters except id should be factored out - these details should be pulled from DAL, not submitted via user input.
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [ValidateInput(false)]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditLot(int id, int CategoryID, string ListingType, string LineageString, string Currency, string returnUrl)
        {
            ViewData[Fields.CategoryID] = CategoryID;

            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            //username of logged in user
            var actingUserName = User.Identity.Name;

            //get the existing Listing object
            Lot existingLot = EventClient.GetLotByIDWithFillLevel(actingUserName, id, ListingFillLevels.Default);
            if (existingLot == null)
            {
                //handle non-existant listing # on detail page
                return RedirectToAction(Strings.MVC.LotDetailsAction, new { id = id });
            }

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (existingLot.Listing.OwnerUserName.Equals(actingUserName, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.LotDetailsAction, new { id = id });
            }

            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(actingUserName, this.FBOUserName(), this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            //The Listing to be updated
            input.Items.Add(Strings.Fields.ListingID, existingLot.Listing.ID.ToString(CultureInfo.GetCultureInfo(this.GetCookie(Strings.MVC.CultureCookie))));
            input.AddAllFormValues(this);

            //add any missing custom item property model states
            List<CustomProperty> itemproperties = existingLot.Listing.AllCustomItemProperties(
                CommonClient.GetFieldsByCategoryID(existingLot.Listing.PrimaryCategory.ID));
            PruneListingCustomPropertiesEditability(this.FBOUserName(), existingLot.Listing.OwnerUserName, itemproperties);
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
                SiteClient.RemoveCacheData("samplelots_" + existingLot.Event.ID.ToString());

                //return a different view here for success or redirectaction...
                if (EventClient.UpdateLotWithUserInput(actingUserName, existingLot, input))
                {
                    //paytoproceed is true
                    return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController);
                }
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(Strings.MVC.LotDetailsAction, Strings.MVC.EventController, new { id });
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
            Dictionary<string, bool> editableFields = ListingClient.GetUpdateableListingFields(actingUserName, existingLot.Listing);
            ViewData[Strings.MVC.ViewData_EditableFieldList] = editableFields;

            //ReturnUrl, Listing Type, Event view data
            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            ViewData[Strings.Fields.ListingType] = existingLot.Listing.Type.Name;
            ViewData[Strings.MVC.ViewData_Event] = Strings.Events.UpdateListing;

            //max images allowed per listing (this site setting applies to all listing types)
            int maxImages = int.Parse(SiteClient.Settings[Strings.SiteProperties.MaxImagesPerItem]);
            ViewData[Strings.SiteProperties.MaxImagesPerItem] = maxImages;

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(existingLot.Listing.Type.Name, "Site");

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
            ViewData[Strings.Fields.Properties] = itemproperties;

            //Images - on validation errors images are re-populated via the ImageRenderState input value
            ViewData[Strings.Fields.Media] = new List<RainWorx.FrameWorx.DTO.Media.Media>();

            //Currency (needed so CreateListingPage2 can use the same shared "XXXListingFields" user control)
            ViewData[Strings.Fields.Currency] = existingLot.Listing.Currency.Code;

            return View(existingLot);
        }

        #endregion

        #region Lot Details

        /// <summary>
        /// Displays detailed Lot view
        /// </summary>
        /// <param name="id">ID of the requested listing</param>
        /// <param name="FollowLive">optional value to enable automatic redirect to next lot if this one closes while user is viewing it</param>
        /// <returns>View(Listing)</returns>
        //[ImportModelStateFromTempData]
        public ActionResult LotDetails(int? id, bool? FollowLive)
        {
            //do not allow browsers to store stale copies of this page, especially for browser back button use
            Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate"); // HTTP 1.1.
            Response.AddHeader("Pragma", "no-cache"); // HTTP 1.0.
            Response.AddHeader("Expires", "0"); // Proxies.

            ViewData["FollowLive"] = false;
            if (id.HasValue)
            {
                try
                {
                    Lot currentLot = EventClient.GetLotByID(User.Identity.Name, id.Value, this.FBOUserName());

                    //if this listing is a draft and it's not the owner or an admin, return a "Not Found" result
                    if (currentLot != null)
                    {
                        if (currentLot.Listing.Status == ListingStatuses.Draft)
                        {
                            if (currentLot.Listing.OwnerUserName != this.FBOUserName())
                            {
                                if (!User.IsInRole(Roles.Admin))
                                {
                                    return LotNotFound();
                                }
                            }
                        }
                        else if (currentLot.Listing.Status == ListingStatuses.Deleted)
                        {
                            return LotNotFound();
                        }
                    }
                    else
                    {
                        return LotNotFound();
                    }

                    if (currentLot.Event.FollowLiveEnabled && FollowLive.HasValue)
                    {
                        ViewData["FollowLive"] = FollowLive;
                    }

                    if (currentLot.Listing.OfferCount > 0)
                    {
                        ViewData["AllOffers"] = ListingClient.GetOffersByListingId(User.Identity.Name, currentLot.Listing.ID);
                    }
                    else
                    {
                        ViewData["AllOffers"] = new List<Offer>(0);
                    }

                    Event currentEvent = currentLot.Event;
                    ViewData[Strings.MVC.EventDetails] = currentEvent;

                    bool enableListingHitCounts = false;
                    bool.TryParse(ConfigurationManager.AppSettings["EnableListingHitCounts"], out enableListingHitCounts);
                    ViewData["EnableListingHitCounts"] = enableListingHitCounts;

                    if (!currentLot.Listing.Properties.Any(p => p.Field.Name.Equals("ReservePrice")))
                    {
                        ViewData["ReserveStatus"] = "NA";
                    }
                    else if (currentLot.Listing.Properties.GetPropertyValue("ReservePrice", 0.0M) == 0.0M)
                    {
                        ViewData["ReserveStatus"] = "NoReserve";
                    }
                    else if ((currentLot.Listing.CurrentPrice ?? 0.0M) >= 
                        currentLot.Listing.Properties.GetPropertyValue("ReservePrice", 0.0M))
                    {
                        ViewData["ReserveStatus"] = "ReserveMet";
                    }
                    else
                    {
                        ViewData["ReserveStatus"] = "ReserveNotMet";
                    }

                    PruneListingCustomFieldsVisbility(currentLot.Listing);

                    if (currentLot.Listing.Status.Equals(Strings.ListingStatuses.Deleted))
                    {
                        return NotFound();
                    }
                    else
                    {
                        //get listing type-specific properties
                        List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(currentLot.Listing.Type.Name, "Site");
                        bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
                        ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

                        //get primary listing and region categories to determine banner selection
                        List<Category> bannerCats = new List<Category>();
                        bannerCats.Add(currentLot.Listing.PrimaryCategory);
                        var regions = currentLot.Listing.Categories.Where(c => c.Type == Strings.CategoryTypes.Region);
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
                            Address sellerAddress = UserClient.GetAddresses(currentLot.Listing.OwnerUserName, currentLot.Listing.Owner.UserName).Where(
                                a => a.ID == currentLot.Listing.Owner.PrimaryAddressID).SingleOrDefault();
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
                        GetFinalBuyerFeeRanges(currentLot.Listing.Type.Name, currentLot.Listing.Categories, out finalFeeMin, out finalFeeMax, out finalFeeTiers, out finalFeeDescription);
                        ViewData[Strings.MVC.ViewData_MinFinalBuyerFee] = finalFeeMin;
                        ViewData[Strings.MVC.ViewData_MaxFinalBuyerFee] = finalFeeMax;
                        ViewData[Strings.MVC.ViewData_FinalBuyerFeeTiers] = finalFeeTiers;
                        ViewData[Strings.MVC.ViewData_FinalBuyerFeeDescription] = finalFeeDescription;

                        //if this lot ended successfully then retrieve all existing invoices
                        List<Invoice> invoices = null;
                        List<LineItem> purchases = null;
                        if (User.Identity.IsAuthenticated && currentLot.Listing.Status == ListingStatuses.Successful)
                        {
                            //retrieve all existing invoices for this lot
                            invoices = AccountingClient.GetInvoicesBySeller(currentLot.Listing.OwnerUserName, currentLot.Listing.Owner.UserName,
                                "All", currentLot.Listing.ID.ToString(), "ListingID", currentLot.Event.ID, 0, 0, "CreatedDTTM", true).List;

                            if (currentLot.Listing.OwnerAllowsInstantCheckout() || SiteClient.BoolSetting(SiteProperties.AutoGenerateInvoices))
                            {
                                ListingPageQuery currentQuery = QuerySortDefinitions.BidWonOptions[0];
                                purchases = AccountingClient.GetListingLineItemsByPayer(User.Identity.Name, this.FBOUserName(),
                                    "NeedInvoice", currentLot.Listing.ID.ToString(), "ListingID",
                                    0, 0, currentQuery.Sort, currentQuery.Descending).List;
                            }
                        }
                        ViewData["AllInvoices"] = invoices ?? new List<Invoice>();
                        ViewData["UninvoicedPurchases"] = purchases ?? new List<LineItem>();

                        ViewData["CurrentLot"] = currentLot;

                        return View(Strings.MVC.LotDetailsAction, currentLot);
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.LotNotExist) throw iafc;
                }
            }
            return LotNotFound();
        }

        /// <summary>
        /// Displays "Listing not found" error message.
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult LotNotFound()
        {
            return View(Strings.MVC.LotNotFoundAction);
        }

        /// <summary>
        /// Displays "Listing not found" error message.
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult NotFound()
        {
            return View(Strings.MVC.NotFoundAction);
        }

        /// <summary>
        /// displays a lot confirmation view for the specified lot
        /// </summary>
        /// <param name="id">ID of the specified lot</param>
        /// <returns></returns>
        public ActionResult LotConfirmation(int? id)
        {
            if (id.HasValue)
            {
                try
                {
                    string fillLevel = ListingFillLevels.Default;
                    Lot lot = EventClient.GetLotByIDWithFillLevel(User.Identity.Name, id.Value, fillLevel);
                    //Listing currentListing = ListingClient.GetListingByID(User.Identity.Name, id.Value);

                    if (lot.Listing.Status.Equals(Strings.ListingStatuses.Deleted))
                    {
                        return NotFound();
                    }
                    else
                    {
                        //get listing type-specific properties
                        List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(lot.Listing.Type.Name, "Site");
                        bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
                        ViewData[Strings.SiteProperties.EnableShipping] = shippingEnabled;

                        //get primary listing and region categories to determine banner selection
                        List<Category> bannerCats = new List<Category>();
                        bannerCats.Add(lot.Listing.PrimaryCategory);
                        var regions = lot.Listing.Categories.Where(c => c.Type == Strings.CategoryTypes.Region);
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

                        return View(lot);
                    }
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                }
            }
            return NotFound();
        }

        #endregion

        #region Create Event

        /// <summary>
        /// displays and processes form to create new event
        /// </summary>
        /// <param name="ReturnUrl">the optional url to redirect to upon success</param>
        /// <returns>View()</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [ValidateInput(false)]
        public ActionResult CreateEvent(string ReturnUrl)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            //populate view data with seller details
            User user = UserClient.GetUserByUserName(actingUN, fboUN);
            ViewData[Strings.Fields.Seller] = user;
            CustomProperty managerName = user.Properties.SingleOrDefault(p => p.Field.Name == Strings.Fields.ManagerName);
            ViewData[Strings.Fields.ManagedByName] = managerName == null ? "" : managerName.Value;

            List<CustomProperty> eventProperties = new List<CustomProperty>();
            List<CustomField> eventFields = CommonClient.GetCustomFields(CustomFieldGroups.Event, 0, 0, null, false).List;

            PruneListingCustomFieldsEditability(this.FBOUserName(), this.FBOUserName(), eventFields);

            foreach (CustomField customField in eventFields)
            {
                CustomProperty newProp = new CustomProperty();
                newProp.Field = customField;
                newProp.Value = customField.DefaultValue;
                eventProperties.Add(newProp);
            }

            ViewData[Strings.Fields.EventProperties] = eventProperties;

            ViewData[Fields.Media] = null;

            Dictionary<int, string> INC_PriceLevel;
            Dictionary<int, string> INC_Increment;

            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                UserInput input = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
                input.AddAllFormValues(this);

                //Add increment in seconds for BLL (UI currently uses Minutes)
                int closingGroupMinutes = 0;
                if (input.Items.ContainsKey(Fields.ClosingGroupIncrementMinutes))
                {
                    if (int.TryParse(input.Items[Fields.ClosingGroupIncrementMinutes], NumberStyles.Integer, cultureInfo, out closingGroupMinutes))
                    {
                        input.Items.Add(Fields.ClosingGroupIncrementSeconds, (closingGroupMinutes * 60).ToString(cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Fields.ClosingGroupIncrementSeconds, input.Items[Fields.ClosingGroupIncrementMinutes]);
                    }
                }

                int softClosingMinutes = 0;
                if (input.Items.ContainsKey(Fields.SoftClosingGroupIncrementMinutes))
                {
                    if (int.TryParse(input.Items[Fields.SoftClosingGroupIncrementMinutes], NumberStyles.Integer, cultureInfo, out softClosingMinutes))
                    {
                        input.Items.Add(Fields.SoftClosingGroupIncrementSeconds, (softClosingMinutes * 60).ToString(cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Fields.SoftClosingGroupIncrementSeconds, input.Items[Fields.SoftClosingGroupIncrementMinutes]);
                    }
                }

                try
                {
                    int eventID;
                    eventID = EventClient.CreateEvent(actingUN, input);
                    if (Url.IsLocalUrl(ReturnUrl))
                    {
                        PrepareSuccessMessage(Strings.MVC.CreateEventAction, MessageType.Method);
                        return Redirect(ReturnUrl);
                    }
                    else
                    {
                        return RedirectToAction(Strings.MVC.EventConfirmationAction, Strings.MVC.EventController, new { id = eventID });
                    }
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        if (issue.Key == Strings.Fields.ClosingGroupIncrementSeconds && input.Items.ContainsKey(Strings.Fields.ClosingGroupIncrementMinutes))
                        {
                            ModelState.AddModelError(Strings.Fields.ClosingGroupIncrementMinutes, issue.Message);
                        }
                        else if (issue.Key == Strings.Fields.SoftClosingGroupIncrementSeconds && input.Items.ContainsKey(Strings.Fields.SoftClosingGroupIncrementMinutes))
                        {
                            ModelState.AddModelError(Strings.Fields.SoftClosingGroupIncrementMinutes, issue.Message);
                        }
                        else if (!ModelState.ContainsKey(issue.Key) || (ModelState.ContainsKey(issue.Key) && ModelState[issue.Key].Errors.Count == 0))
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                }

                ////populate user-inputted increments
                IncrementDictionariesFromUserInput(input, cultureInfo, out INC_PriceLevel, out INC_Increment);
            }
            else
            {//set initial values

                ////populate default increments
                List<Increment> defaultIncrements = Cache.Increments(Strings.ListingTypes.Auction).OrderBy(i => i.PriceLevel).ToList();
                IncrementDictionariesFromIncrementList(defaultIncrements, cultureInfo, out INC_PriceLevel, out INC_Increment);

                //set initial modelstate for custom fields
                ModelState.FillProperties(eventProperties, cultureInfo);

                var allUserProperties = UserClient.Properties(actingUN, fboUN);

                //BuyersPremiumPercent, if applicable
                if (SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium) && !ModelState.ContainsKey(Fields.BuyersPremiumPercent))
                {
                    var sellerBpPref = allUserProperties.FirstOrDefault(p => p.Field.Name == StdUserProps.BuyersPremiumPercent);
                    decimal tempDec1;
                    if (sellerBpPref != null && sellerBpPref.Value != null && decimal.TryParse(sellerBpPref.Value.TrimEnd('0'), out tempDec1))
                    {
                        int count = BitConverter.GetBytes(decimal.GetBits(tempDec1)[3])[2];
                        string formattedValue = tempDec1.ToString("N" + count, cultureInfo);
                        //...add it to the model
                        ModelState ms = new ModelState();
                        ms.Value = new ValueProviderResult(formattedValue, formattedValue, null);
                        ModelState.Add(Fields.BuyersPremiumPercent, ms);
                    }
                }

                //ClosingGroupIncrementMinutes
                const string defaultClosingGroupIncrementMinutes = "5";
                if (!ModelState.ContainsKey(Fields.ClosingGroupIncrementMinutes))
                {
                    //var sellerPref = allUserProperties.FirstOrDefault(p => p.Field.Name == StdUserProps.ClosingGroupIncrementMinutes);
                    //if (sellerPref != null)
                    //{
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(defaultClosingGroupIncrementMinutes,
                        defaultClosingGroupIncrementMinutes, null);
                    ModelState.Add(Fields.ClosingGroupIncrementMinutes, ms);
                    ms = new ModelState();
                    ms.Value = new ValueProviderResult((int.Parse(defaultClosingGroupIncrementMinutes) * 60).ToString(),
                        (int.Parse(defaultClosingGroupIncrementMinutes) * 60).ToString(), null);
                    ModelState.Add(Fields.ClosingGroupIncrementSeconds, ms);
                    //}
                }

                //SoftClosingGroup
                const string defaultSoftClosingGroupIncrementMinutes = "5";
                if (!ModelState.ContainsKey(Fields.SoftClosingGroupIncrementMinutes))
                {
                    //var sellerPref = allUserProperties.FirstOrDefault(p => p.Field.Name == StdUserProps.ClosingGroupIncrementMinutes);
                    //if (sellerPref != null)
                    //{
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(defaultSoftClosingGroupIncrementMinutes, defaultSoftClosingGroupIncrementMinutes, null);
                    ModelState.Add(Fields.SoftClosingGroupIncrementMinutes, ms);
                    ms = new ModelState();
                    ms.Value = new ValueProviderResult((int.Parse(defaultSoftClosingGroupIncrementMinutes) * 60).ToString(),
                        (int.Parse(defaultClosingGroupIncrementMinutes) * 60).ToString(), null);
                    ModelState.Add(Fields.SoftClosingGroupIncrementSeconds, ms);
                    //}
                }

                //ProxyBidding
                const string defaultProxyBidding = "true";
                if (!ModelState.ContainsKey(Fields.ProxyBidding))
                {
                    //var sellerPref = allUserProperties.FirstOrDefault(p => p.Field.Name == StdUserProps.ProxyBidding);
                    //if (sellerPref != null)
                    //{
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(defaultProxyBidding, defaultProxyBidding, null);
                    ModelState.Add(Fields.ProxyBidding, ms);
                    //}
                }

                //Currency
                if (!ModelState.ContainsKey(Fields.Currency))
                {
                    string defaultCurrency = this.GetCookie("currency");
                    if (string.IsNullOrEmpty(defaultCurrency)) defaultCurrency = SiteClient.SiteCurrency;
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(defaultCurrency, defaultCurrency, null);
                    ModelState.Add(Fields.Currency, ms);
                }

                //FollowLiveEnabled
                string defaultFollowLiveEnabled = SiteClient.BoolSetting(SiteProperties.FollowAuctionLiveEnabled).ToString().ToLower();
                if (!ModelState.ContainsKey(Fields.FollowLiveEnabled))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(defaultFollowLiveEnabled, defaultFollowLiveEnabled, null);
                    ModelState.Add(Fields.FollowLiveEnabled, ms);
                }

            }

            ViewData[Strings.Fields.PriceLevels] = INC_PriceLevel;
            ViewData[Strings.Fields.Increments] = INC_Increment;

            return View();
        }

        /// <summary>
        /// displays summary of the specified event
        /// </summary>
        /// <param name="id">id of the specified event</param>
        /// <returns>View(Event)</returns>
        public ActionResult EventConfirmation(int id)
        {
            Event newEvent = null;
            try
            {
                newEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, id, EventFillLevels.All);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("EventConfirmation", e);
            }
            return View(newEvent);
        }

        #endregion

        #region Edit Event

        /// <summary>
        /// Displays and processes form to edit the specified auction event
        /// </summary>
        /// <param name="id">id of the specified event</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        [ValidateInput(false)]
        public ActionResult Edit(int id, string returnUrl)
        {
            //disable browser XSS detection for this specific page because it can randomly break the javascript when
            //  the content being saved contains anything found within one of the scripts in cases of validation errors.
            Response.AddHeader("X-XSS-Protection", "0");

            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, id, EventFillLevels.All);

            //ensures any missing properties are added automatically
            auctionEvent.Properties = EventClient.EventProperties(User.Identity.Name, auctionEvent.ID);
            PruneCustomPropertiesEditability(auctionEvent.OwnerUserName, auctionEvent.Properties);

            if (auctionEvent == null)
            {
                return RedirectToAction(Strings.MVC.DetailsAction, new { id });
            }

            //determine if this user has permission to edit this event (admin or event owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isEventOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isEventOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, new { id = id });
            }

            string defaultRedirectAction;
            switch (auctionEvent.Status)
            {
                case Strings.AuctionEventStatuses.Archived:
                    defaultRedirectAction = Strings.MVC.EventsArchivedAction;
                    break;
                case Strings.AuctionEventStatuses.Closed:
                    defaultRedirectAction = Strings.MVC.EventsClosedAction;
                    break;
                case Strings.AuctionEventStatuses.Draft:
                    defaultRedirectAction = Strings.MVC.EventsDraftsAction;
                    break;
                default:
                    defaultRedirectAction = Strings.MVC.EventsPublishedAction;
                    break;
            }

            Dictionary<int, string> INC_PriceLevel;
            Dictionary<int, string> INC_Increment;

            if (Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                UserInput input = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
                input.AddAllFormValues(this);

                //Add increment in seconds for BLL (UI currently uses Minutes)
                int closingGroupMinutes;
                if (input.Items.ContainsKey(Fields.ClosingGroupIncrementMinutes))
                {
                    if (int.TryParse(input.Items[Fields.ClosingGroupIncrementMinutes], NumberStyles.Integer, cultureInfo, out closingGroupMinutes))
                    {
                        input.Items.Add(Fields.ClosingGroupIncrementSeconds, (closingGroupMinutes * 60).ToString(cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Fields.ClosingGroupIncrementSeconds, input.Items[Fields.ClosingGroupIncrementMinutes]);
                    }
                }

                int softClosingMinutes;
                if (input.Items.ContainsKey(Fields.SoftClosingGroupIncrementMinutes))
                {
                    if (int.TryParse(input.Items[Fields.SoftClosingGroupIncrementMinutes], NumberStyles.Integer, cultureInfo, out softClosingMinutes))
                    {
                        input.Items.Add(Fields.SoftClosingGroupIncrementSeconds, (softClosingMinutes * 60).ToString(cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Fields.SoftClosingGroupIncrementSeconds, input.Items[Fields.SoftClosingGroupIncrementMinutes]);
                    }
                }

                try
                {
                    EventClient.UpdateEvent(actingUN, input, false);
                    Cache.ClearEventIncrements(id);
                    PrepareSuccessMessage("EditEvent", MessageType.Method);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction(defaultRedirectAction, Strings.MVC.AccountController, null);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        if (issue.Key == Strings.Fields.ClosingGroupIncrementSeconds && input.Items.ContainsKey(Strings.Fields.ClosingGroupIncrementMinutes))
                        {
                            ModelState.AddModelError(Strings.Fields.ClosingGroupIncrementMinutes, issue.Message);
                        }
                        else if (issue.Key == Strings.Fields.SoftClosingGroupIncrementSeconds && input.Items.ContainsKey(Strings.Fields.SoftClosingGroupIncrementMinutes))
                        {
                            ModelState.AddModelError(Strings.Fields.SoftClosingGroupIncrementMinutes, issue.Message);
                        }
                        else if (!ModelState.ContainsKey(issue.Key) || (ModelState.ContainsKey(issue.Key) && ModelState[issue.Key].Errors.Count == 0))
                        {
                            ModelState.AddModelError(issue.Key, issue.Message);
                        }
                    }
                }
                catch (Exception)
                {
                    PrepareErrorMessage("EditEvent", MessageType.Method);
                }

                //get attempted new increments from user input
                IncrementDictionariesFromUserInput(input, cultureInfo, out INC_PriceLevel, out INC_Increment);
            }
            else
            {//set initial values

                //set initial modelstate for custom fields
                ModelState.FillProperties(auctionEvent.Properties, cultureInfo);

                //populate existing increments
                List<Increment> existingIncrements = Cache.EventIncrements(id).OrderBy(i => i.PriceLevel).ToList();
                IncrementDictionariesFromIncrementList(existingIncrements, cultureInfo, out INC_PriceLevel, out INC_Increment);
            }

            var eventTimeZone = TimeZoneInfo.FindSystemTimeZoneById(auctionEvent.TimeZone);
            DateTime localEndDTTM = TimeZoneInfo.ConvertTime(auctionEvent.EndDTTM, TimeZoneInfo.Utc, eventTimeZone);
            ViewData[Strings.Fields.EndDate] = localEndDTTM.ToString("d", cultureInfo);
            ViewData[Strings.Fields.EndTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || localEndDTTM.Second == 0 ? localEndDTTM.ToString("t", cultureInfo) : localEndDTTM.ToString("T", cultureInfo);

            if (auctionEvent.StartDTTM.HasValue)
            {
                DateTime localStartDTTM = TimeZoneInfo.ConvertTime(auctionEvent.StartDTTM.Value, TimeZoneInfo.Utc, eventTimeZone);
                ViewData[Strings.Fields.StartDate] = localStartDTTM.ToString("d", cultureInfo);
                ViewData[Strings.Fields.StartTime] = bool.Parse(SiteClient.Settings["UsejQueryTimePicker"]) || localStartDTTM.Second == 0 ? localStartDTTM.ToString("t", cultureInfo) : localStartDTTM.ToString("T", cultureInfo);
            }

            ViewData[Fields.ReturnUrl] = returnUrl ?? this.GetActionUrl(defaultRedirectAction, Strings.MVC.AccountController, null);

            ViewData[Strings.Fields.PriceLevels] = INC_PriceLevel;
            ViewData[Strings.Fields.Increments] = INC_Increment;

            ViewData["EventImage"] = auctionEvent.Media.Where(m => m.Context == Strings.MediaUploadContexts.UploadEventImage).ToList();
            ViewData["EventBanner"] = auctionEvent.Media.Where(m => m.Context == Strings.MediaUploadContexts.UploadEventBanner).ToList();

            return View(auctionEvent);
        }

        /// <summary>
        /// Returns two Dictionary&lt;int,string&gt; lists, one for the price levels and one for the amounts entered
        /// </summary>
        /// <param name="input">the user input container</param>
        /// <param name="cultureInfo">the current user's CultureInfo details</param>
        /// <param name="priceLevels">the resulting dictionary of price levels</param>
        /// <param name="amounts">the resulting dictionary of amounts</param>
        private void IncrementDictionariesFromUserInput(UserInput input, CultureInfo cultureInfo, out Dictionary<int, string> priceLevels, out Dictionary<int, string> amounts)
        {
            priceLevels = new Dictionary<int, string>();
            amounts = new Dictionary<int, string>();
            foreach (string priceLevelkey in input.Items.Keys.Where(k => k.StartsWith("INC_PriceLevel_")))
            {
                int i = int.Parse(priceLevelkey.Replace("INC_PriceLevel_", ""));
                string amountKey = string.Format("INC_Increment_{0}", i);
                priceLevels.Add(i, input.Items[priceLevelkey]);
                amounts.Add(i, input.Items[amountKey]);
            }
        }

        /// <summary>
        /// Returns two Dictionary&lt;int,string&gt; lists, one for the price levels and one for the amounts entered
        /// </summary>
        /// <param name="increments">a List of Increments</param>
        /// <param name="cultureInfo"></param>
        /// <param name="priceLevels">the resulting dictionary of price levels</param>
        /// <param name="amounts">the resulting dictionary of amounts</param>
        private void IncrementDictionariesFromIncrementList(List<Increment> increments, CultureInfo cultureInfo, out Dictionary<int, string> priceLevels, out Dictionary<int, string> amounts)
        {
            priceLevels = new Dictionary<int, string>();
            amounts = new Dictionary<int, string>();
            int i = 0;
            foreach (var increment in increments)
            {
                priceLevels.Add(i, increment.PriceLevel.ToString("N2", cultureInfo));
                amounts.Add(i, increment.Amount.ToString("N2", cultureInfo));
                i++;
            }
        }

        #endregion

        #region End Lot Early & Delete Lot

        /// <summary>
        /// Processes request to end the specified lot early
        /// </summary>
        /// <param name="id">ID of the requested lot to be ended early</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Event/LotDetails/[id] if no return url supplied</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult EndLotEarly(int id, string returnUrl)
        {
            try
            {
                EventClient.EndLotEarly(User.Identity.Name, id);
                PrepareSuccessMessage("EndLotEarly", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("EndLotEarly", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.LotDetailsAction, new { id });
        }

        /// <summary>
        /// Processes request to delete the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing to be deleted</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to site homepage if no return url supplied</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult DeleteLot(int id, string returnUrl)
        {
            int? auctionEventId = null;
            try
            {
                if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
                {
                    var lot = EventClient.GetLotByIDWithFillLevel(User.Identity.Name, id, ListingFillLevels.LotEvent);
                    auctionEventId = lot.Event.ID;
                }
                EventClient.DeleteLot(User.Identity.Name, id);
                PrepareSuccessMessage("DeleteLot", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DeleteLot", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            if (auctionEventId.HasValue)
            {
                return RedirectToAction(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { id = auctionEventId.Value });
            }
            return LotNotFound();
        }

        #endregion

        #region Event Details

        /// <summary>
        /// Displays detailed event view
        /// </summary>
        /// <param name="id">the id of the event tlo display</param>
        /// <param name="breadcrumbs">list of applicable category data, formatted for SEO</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of the requested sort option defined in QuerySortDefinitions.BrowseOptions</param>
        /// <param name="ViewStyle">&quot;list&quot; (default) or &quot;grid&quot;</param>
        /// <param name="StatusFilter">&quot;active_only&quot; (default), &quot;completed_only&quot; or &quot;all&quot;</param>
        /// <returns>View(Event) or NotFound() is event doesn't exist or is inelleigible to be viewed by current user</returns>
        //[OutputCache(NoStore = true, Duration = 0)]
        public ActionResult Details(int? id, string breadcrumbs, int? page, int? SortFilterOptions, string ViewStyle, string StatusFilter)
        {
            //do not allow browsers to store stale copies of this page, especially for browser back button use
            Response.AddHeader("Cache-Control", "no-store, no-cache, must-revalidate"); // HTTP 1.1.
            Response.AddHeader("Pragma", "no-cache"); // HTTP 1.0.
            Response.AddHeader("Expires", "0"); // Proxies.

            if (id.HasValue)
            {
                Event currentEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, id.Value, Strings.EventFillLevels.All);
                if (currentEvent != null)
                {
                    //only allow admin or owner to view Draft, Scheduled (not Preview) or Archived event
                    bool viewingAllowed = true;
                    bool isOwnerOrAdmin = User.Identity.IsAuthenticated && (User.Identity.Name.Equals(currentEvent.OwnerUserName, StringComparison.OrdinalIgnoreCase) ||
                                                              User.IsInRole(Strings.Roles.Admin));
                    if (currentEvent.Status == Strings.AuctionEventStatuses.Draft ||
                        currentEvent.Status == Strings.AuctionEventStatuses.Publishing ||
                        currentEvent.Status == Strings.AuctionEventStatuses.Archived ||
                        currentEvent.Status == Strings.AuctionEventStatuses.Scheduled ||
                        currentEvent.Status == Strings.AuctionEventStatuses.Deleted)
                    {
                        viewingAllowed = false;
                        if (isOwnerOrAdmin)
                        {
                            viewingAllowed = true;
                        }
                    }
                    if (viewingAllowed)
                    {
                        //string statuses = Strings.ListingStatuses.Active + "," + Strings.ListingStatuses.Preview;
                        //ViewData[Strings.MVC.ViewData_CategoryCounts] = CommonClient.GetCategoryCounts(new List<string>(1) { currentEvent.CategoryID.ToString() }, statuses, string.Empty);

                        //ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(9);
                        //ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(27);

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

                        //capture SortFilterOptions              
                        SortFilterOptions = SortFilterOptions ?? 0;

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
                            //default value is based on Event Status
                            if (currentEvent.Status == AuctionEventStatuses.Closed || currentEvent.Status == AuctionEventStatuses.Archived)
                            {
                                StatusFilter = "completed_only";
                            }
                            else
                            {
                                StatusFilter = "active_only";
                            }
                        }
                        else if (StatusFilter.ToLower() == "completed_only")
                        {
                            StatusFilter = "completed_only";
                        }
                        else if (StatusFilter.ToLower() == "all")
                        {
                            StatusFilter = "all";
                        }
                        else
                        {
                            StatusFilter = "active_only";
                        }
                        ViewData["StatusFilter"] = StatusFilter;

                        //determine if Rolling Lot Updates are currently in effect for this user and this event
                        bool rollingUpdatesAvailable = SiteClient.BoolSetting(SiteProperties.RollingLotUpdatesEnabled) && // site property enabled, and...
                            (currentEvent.Status == AuctionEventStatuses.Preview || // Event Status is currntly either "Preview", "Active" or "Closing"
                             currentEvent.Status == AuctionEventStatuses.Active ||
                             currentEvent.Status == AuctionEventStatuses.Closing);
                        bool rollingUpdatesInEffect = rollingUpdatesAvailable &&
                            StatusFilter == "active_only" && //status filter option is "active_only", and...
                            SortFilterOptions == 0 && // sort option is "Lot Order"
                            currentEvent.Status == AuctionEventStatuses.Closing; // only closing events will have this option enabled by default
                        if (rollingUpdatesAvailable)
                        {
                            string rollingLotUpdatesCookieVal = this.GetCookie("RollingLotUpdatesEnabledByUser");
                            bool rollingUpdatesUserPref;
                            if (!string.IsNullOrWhiteSpace(rollingLotUpdatesCookieVal) && bool.TryParse(rollingLotUpdatesCookieVal, out rollingUpdatesUserPref))
                            {
                                //user has manually specified ON or OFF
                                rollingUpdatesInEffect = rollingUpdatesUserPref;
                            }
                        }
                        ViewData["RollingUpdatesAvailable"] = rollingUpdatesAvailable;
                        ViewData["RollingUpdatesInEffect"] = rollingUpdatesInEffect;

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
                                !key.StartsWith("StatusFilter", StringComparison.OrdinalIgnoreCase) &&
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

                        //string effectiveStatuses = Strings.ListingStatuses.Active + "," +
                        //                            Strings.ListingStatuses.Ended + "," +
                        //                            Strings.ListingStatuses.Successful + "," +
                        //                            Strings.ListingStatuses.Unsuccessful + "," +
                        //                            Strings.ListingStatuses.Preview;
                        //if (isOwnerOrAdmin)
                        //{
                        //    effectiveStatuses += "," + Strings.ListingStatuses.Pending
                        //                       + "," + Strings.ListingStatuses.AwaitingPayment
                        //                       + "," + Strings.ListingStatuses.Draft
                        //                       + "," + Strings.ListingStatuses.Validated;
                        //    //never show these?  Closed Closing Deleted Ending Error_Closing New Updated 
                        //}
                        string effectiveStatuses;
                        switch (StatusFilter)
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
                                if (isOwnerOrAdmin)
                                {
                                    effectiveStatuses += "," + Strings.ListingStatuses.Pending
                                                       + "," + Strings.ListingStatuses.AwaitingPayment
                                                       + "," + Strings.ListingStatuses.Draft
                                                       + "," + Strings.ListingStatuses.Validated;
                                    //never show these?  Closed Closing Deleted Ending Error_Closing New Updated 
                                }
                                break;
                            default: // "active_only" or missing/invalid value
                                effectiveStatuses = Strings.ListingStatuses.Active + "," +
                                                    Strings.ListingStatuses.Preview;
                                if (isOwnerOrAdmin)
                                {
                                    effectiveStatuses += "," + Strings.ListingStatuses.Pending
                                                       + "," + Strings.ListingStatuses.AwaitingPayment
                                                       + "," + Strings.ListingStatuses.Draft
                                                       + "," + Strings.ListingStatuses.Validated;
                                    //never show these?  Closed Closing Deleted Ending Error_Closing New Updated 
                                }
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
                                //ViewData["MetaKeywords"] = selectedCategory.MetaKeywords;
                                //ViewData["MetaDescription"] = selectedCategory.MetaDescription;
                                //ViewData["PageTitle"] = selectedCategory.PageTitle;
                                //ViewData["PageContent"] = selectedCategory.PageContent;
                                ViewData["CategoryName"] = selectedCategory.Name;
                            }
                        }
                        else
                        {
                            //Category rootCategory = CommonClient.GetCategoryByID(9);
                            //ViewData["MetaKeywords"] = rootCategory.MetaKeywords;
                            //ViewData["MetaDescription"] = rootCategory.MetaDescription;
                            //ViewData["PageTitle"] = rootCategory.PageTitle;
                            //ViewData["PageContent"] = rootCategory.PageContent;
                            //ViewData["CategoryName"] = string.Empty;
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
                        foreach (ListingPageQuery query in QuerySortDefinitions.EventDetailOptions)
                        {
                            sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
                        }
                        ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

                        //add event category id
                        input.Items.Add(Strings.Fields.EventCategoryID, currentEvent.CategoryID.ToString());

                        //merge query options
                        bool validCategoryCounts;
                        ListingPageQuery currentQuery = QuerySortDefinitions.MergeEventDetailOptions(SortFilterOptions.Value,
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

                            //add the event category id to ensure results are limited to this event
                            fixIDs.Add(currentEvent.CategoryID.ToString());

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
                            int effectivePageSize = SiteClient.PageSize;
                            if (rollingUpdatesInEffect)
                            {
                                //effectivePageSize *= 2;
                                int rollingLotsBufferSize = SiteClient.PageSize;
                                int tempInt = 0;
                                if (int.TryParse(ConfigurationManager.AppSettings["RollingLotsBufferSize"], out tempInt) && tempInt > 3)
                                {
                                    rollingLotsBufferSize = tempInt;
                                }
                                effectivePageSize += rollingLotsBufferSize;
                            }
                            Page<Listing> results;
                            Dictionary<int, int> rtCatcounts;
                            if (User.Identity.IsAuthenticated && quickBidForListViewsEnabled)
                            {
                                results = ListingClient.SearchListingsWithFillLevelAndContext(User.Identity.Name, currentQuery, page.Value, effectivePageSize, fillLevel, this.FBOUserName(), getRealTimeCatCounts, out rtCatcounts);
                                if (getRealTimeCatCounts) ViewData[Strings.MVC.ViewData_CategoryCounts] = rtCatcounts;
                            }
                            else
                            {
                                results = ListingClient.SearchListingsWithFillLevel(User.Identity.Name, currentQuery, page.Value, effectivePageSize, fillLevel, getRealTimeCatCounts, out rtCatcounts);
                                if (getRealTimeCatCounts) ViewData[Strings.MVC.ViewData_CategoryCounts] = rtCatcounts;
                            }

                            /*
                              //example usage of passing additional parameter(s) directly to the RWX_SearchListings stored procedure
                              List<KeyValuePair<string, string>> extraParams = new List<KeyValuePair<string, string>>(1);
                              extraParams.Add(new KeyValuePair<string, string>("excludeKeywords", "exclude results matching this keyword")); //exclude all listings that contain the specified keyword(s)
                              Page<Listing> results = ListingClient.SearchListingsWithAdditionalParameters(User.Identity.Name, currentQuery, extraParams, page.Value, SiteClient.PageSize);
                            */

                            ViewData["PageOfLots"] = results;
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

                        PruneCustomPropertiesVisbility(currentEvent.OwnerUserName, currentEvent.Properties);

                        ViewData[Strings.MVC.EventDetails] = currentEvent;
                        return View(currentEvent);
                    }
                }
            }
            return NotFound();
        }

        /// <summary>
        /// Retrieves the specified number of lot for the specified event
        /// </summary>
        /// <param name="EventID">the id of the event tlo display</param>
        /// <param name="ViewStyle">&quot;list&quot; (default) or &quot;grid&quot;</param>
        /// <param name="gtLotOrder">results will only include lots with a LotOrder greater than this value</param>
        public ActionResult MoreLots_Inline(int EventID, string ViewStyle, int gtLotOrder)
        {
            int rollingLotsBufferSize = SiteClient.PageSize;
            int tempInt = 0;
            if (int.TryParse(ConfigurationManager.AppSettings["RollingLotsBufferSize"], out tempInt) && tempInt > 3)
            {
                rollingLotsBufferSize = tempInt;
            }

            //capture/parse ViewStyle
            if (ViewStyle == null || (ViewStyle.ToLower() != "grid" && ViewStyle.ToLower() != "list"))
            {
                ViewStyle = SiteClient.TextSetting(SiteProperties.DefaultBrowseStyle);
            }
            ViewData["ViewStyle"] = ViewStyle;

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

            try
            {
                Page<Listing> results = EventClient.SearchLotsByEvent(SystemActors.SystemUserName, EventID, 
                    "Active", gtLotOrder.ToString(), "GreaterThanLotOrder", 
                    0, rollingLotsBufferSize, "LotOrder", false, fillLevel);
                return View(results);
            }
            catch (Exception)
            {
                return View();
            }
        }

        #endregion Event Details

        #region End Time History

        /// <summary>
        /// displays a page of End Date/Time changes
        /// </summary>
        /// <param name="eventId">optional ineteger ID to limit results to a specific event</param>
        /// <param name="listingId">optional ineteger ID to limit results to a specific listing</param>
        /// <param name="page">0-based page index</param>
        /// <param name="sort">the column to sort by, one of: &quot;ChangeDTTM&quot;, &quot;EventID&quot;, &quot;ListingID&quot;, &quot;SourceListingID&quot;, &quot;Origin&quot;, &quot;NewEndDTTM&quot;</param>
        /// <param name="descending">true to order the results from high to low</param>
        /// <returns>model: Page&lt;ReportRow&gt;</returns>
        public ActionResult EndTimeHistory(int? eventId, int? listingId, int? page, string sort, bool? descending)
        {
            return View(ListingClient.GetListingExtensionDetails(User.Identity.Name, eventId, listingId, page ?? 0, SiteClient.PageSize, sort ?? "", descending ?? false));
        }

        #endregion

        #region Validate Drafts

        /// <summary>
        /// Initiates validation of all draft lots for the specified event
        /// </summary>
        /// <param name="eventID">the ID of the specified event</param>
        [Authorize]
        public ActionResult ValidateAllDrafts(int eventID)
        {
            try
            {
                var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, eventID, Strings.EventFillLevels.Owner);
                if (this.FBOUserName() != auctionEvent.Owner.UserName)
                {
                    throw new Exception("EventNotFound");
                }

                //int errortest = int.Parse("NaN");

                KeepAlive.Start();
                Thread t = new Thread(ValidateAllDraftsThreader);
                t.IsBackground = false;
                t.Name = string.Format("ValidateAllDrafts_{0}", eventID);
                t.Start(new ValidateAllDraftsArgs()
                {
                    auctionEvent = auctionEvent,
                    ActingUserName = User.Identity.Name,
                    controller = this,
                    culture = this.GetCultureInfo()
                });

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (Exception e)
            {
                //PrepareErrorMessage("ValidateAllDrafts", e);
                LogManager.WriteLog("ERROR", "EventController.ValidateAllDrafts", "MVC", TraceEventType.Error, User.Identity.Name, e,
                    new Dictionary<string, object>() { { "eventID", eventID } });
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "ERROR: " + e.Message);
            }

            //PrepareSuccessMessage("ValidateAllDrafts", MessageType.Method);
            //return RedirectToAction(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { id = eventID, ProcessStarted = "VALIDATION" });
        }

        private class ValidateAllDraftsArgs
        {
            /// <summary>
            /// auction event
            /// </summary>
            public Event auctionEvent;

            /// <summary>
            /// username of the acting user
            /// </summary>
            public string ActingUserName;

            /// <summary>
            /// reference to calling controller, mainly for access to RESX
            /// </summary>
            public Controller controller;

            /// <summary>
            /// user's culture
            /// </summary>
            public CultureInfo culture;
        }

        private void ValidateAllDraftsThreader(object args)
        {
            ValidateAllDraftsArgs unpackedArgs = (ValidateAllDraftsArgs)args;
            ValidateAllDraftsProc(unpackedArgs.auctionEvent, unpackedArgs.ActingUserName, unpackedArgs.controller, unpackedArgs.culture);
        }

        private void ValidateAllDraftsProc(Event auctionEvent, string actingUserName, Controller controller, CultureInfo cultureInfo)
        {
            var uniqueGuid = Guid.NewGuid();
            LogManager.WriteLog("Lot Validation Started...", "ValidateAllDraftsProc", "EventController", TraceEventType.Start, actingUserName, null,
                new Dictionary<string, object>() {
                    { "unique proc id", uniqueGuid }
                });
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int totalLotsProcessed = 0;
            int validatedCount = 0;
            Page<Listing> draftsToValidate = null;
            try
            {
                TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
                DateTime jobStartTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, siteTimeZone);
                var allValidationIssues = new Dictionary<string, List<ValidationIssue>>();
                var allValidationErrors = new Dictionary<string, string>();
                string fillLevel = Strings.ListingFillLevels.Default;
                draftsToValidate = EventClient.SearchLotsByEvent(actingUserName, auctionEvent.ID, "Draft", null, null, 0, 0, "Id", false, fillLevel);
                foreach (var listing in draftsToValidate.List)
                {
                    totalLotsProcessed++;
                    try
                    {
                        //var input = new UserInput(actingUserName, auctionEvent.OwnerUserName, cultureInfo.Name, cultureInfo.Name);
                        //FillInputFromLot(input, listing);
                        //EventClient.UpdateLotWithUserInput(actingUserName, listing.Lot, input);
                        PruneListingCustomPropertiesEditability(actingUserName, listing.OwnerUserName, listing.Properties);
                        EventClient.ValidateLot(actingUserName, listing);
                        validatedCount++;
                    }
                    catch (System.ServiceModel.FaultException<ValidationFaultContract> vfc)
                    {
                        allValidationIssues.Add(listing.Lot.LotNumber, vfc.Detail.ValidationIssues);
                    }
                    catch (Exception e)
                    {
                        allValidationErrors.Add(listing.Lot.LotNumber, e.Message);
                    }
                }

                //queue an EventStatusChange signalR message
                EventClient.FinalizeDraftValidation(actingUserName, auctionEvent.ID);

                DateTime jobEndTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, siteTimeZone);

                //send results email
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        SiteClient.Settings[Strings.SiteProperties.SystemEmailAddress],
                        SiteClient.Settings[Strings.SiteProperties.SystemEmailName]),
                    Subject = this.GlobalResourceString("ValidateAllDrafts_Results_Subject", auctionEvent.ID) // Lot Validation Results for Event #{0}
                };
                mailMessage.To.Add(new MailAddress(auctionEvent.Owner.Email, auctionEvent.Owner.UserName));

                // -- BODY:
                var sb = new StringBuilder();
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_BodyHeader")); //Draft Lot Validation has been completed.<br><br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_EventTitle", auctionEvent.ID, Utilities.HtmlEncode(auctionEvent.Title))); //Event Title: {1} ({0})<br><br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_CompletedDTTM", jobEndTime.ToString("g", cultureInfo))); //Validation Completed: {0}<br><br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_DraftLotCount", draftsToValidate.TotalItemCount)); //Total Lots Processed: {0}<br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_ValidatedCount", validatedCount)); //Lots Validated Successfully: {0}<br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_InvalidCount", (draftsToValidate.TotalItemCount - validatedCount))); //Lots Failing Validation: {0}<br>
                sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_BodyFooter")); //<br><br><small>If applicable, a list of all validation errors is attached</small>
                mailMessage.Body = sb.ToString();
                mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
                mailMessage.IsBodyHtml = true;

                if (allValidationIssues.Count > 0 || allValidationErrors.Count > 0)
                {
                    sb = new StringBuilder();
                    foreach (var issueKey in allValidationIssues.Keys.OrderBy(k => k))
                    {
                        sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_Row_LotNumber", issueKey));
                        foreach (var issue in allValidationIssues[issueKey])
                        {
                            sb.AppendLine(controller.ResourceString("Validation," + issue.Message));
                        }
                        sb.AppendLine();
                    }
                    foreach (var errorKey in allValidationErrors.Keys.OrderBy(k => k))
                    {
                        sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_Row_LotNumber", errorKey));
                        sb.AppendLine(controller.GlobalResourceString("ValidateAllDrafts_Results_Row_Error", allValidationErrors[errorKey]));
                        sb.AppendLine();
                    }
                    MemoryStream ms = new MemoryStream();
                    StreamWriter sw = new StreamWriter(ms);
                    sw.Write(sb.ToString());
                    sw.Flush();
                    ms.Seek(0, SeekOrigin.Begin);
                    Attachment errorDetails = new Attachment(ms, "LotValidationIssues.txt", "text/csv");
                    mailMessage.Attachments.Add(errorDetails);
                }

                try
                {
                    var client = new SmtpClient();
                    client.Send(mailMessage);
                }
                catch (Exception e)
                {
                    LogManager.WriteLog("Validate All Drafts Email Failed", "Validate All Drafts", Strings.FunctionalAreas.Site, TraceEventType.Error, null, e,
                        new Dictionary<string, object>() {
                            { "EventID", auctionEvent.ID },
                            { "totalLotsFound", (draftsToValidate != null ? draftsToValidate.TotalItemCount : 0) },
                            { "totalLotsProcessed", totalLotsProcessed },
                            { "validatedCount", validatedCount },
                            { "Email", auctionEvent.Owner.Email} });
                }

                //log success
                LogManager.WriteLog("Validate All Drafts Success", "Validate All Drafts", Strings.FunctionalAreas.Site, TraceEventType.Information, null, null,
                    new Dictionary<string, object>() {
                        { "EventID", auctionEvent.ID },
                        { "totalLotsFound", draftsToValidate != null? draftsToValidate.TotalItemCount : 0 },
                        { "totalLotsProcessed", totalLotsProcessed },
                        { "validatedCount", validatedCount } });
            }
            catch (Exception e)
            {
                LogManager.HandleException(e, FunctionalAreas.Site);
                var taskErrorMessage = new MailMessage
                {
                    From = new MailAddress(SiteClient.Settings[Strings.SiteProperties.SystemEmailAddress],
                                           SiteClient.Settings[Strings.SiteProperties.SystemEmailName]),
                    Subject = controller.GlobalResourceString("ValidateAllDrafts_ErrorNotification_Subject", auctionEvent.ID)
                };
                taskErrorMessage.To.Add(new MailAddress(auctionEvent.Owner.Email, auctionEvent.Owner.UserName));
                taskErrorMessage.Body = e.Message + " " + e.StackTrace;
                taskErrorMessage.BodyEncoding = System.Text.Encoding.UTF8;
                taskErrorMessage.IsBodyHtml = false;
                var taskErrorMessageClient = new SmtpClient();
                taskErrorMessageClient.Send(taskErrorMessage);
                LogManager.WriteLog("Validate All Drafts Error", "Validate All Drafts", Strings.FunctionalAreas.Site, TraceEventType.Error, null, e,
                    new Dictionary<string, object>() {
                        { "EventID", auctionEvent.ID },
                        { "totalLotsFound", draftsToValidate != null? draftsToValidate.TotalItemCount : 0 },
                        { "totalLotsProcessed", totalLotsProcessed },
                        { "validatedCount", validatedCount } });
            }
            finally
            {
                KeepAlive.Stop();
            }

            stopwatch.Stop();
            LogManager.WriteLog("Lot Validation Finished.", "ValidateAllDraftsProc", "EventController", TraceEventType.Stop, actingUserName, null,
                new Dictionary<string, object>() {
                    { "Elapsed Time (MS)", stopwatch.ElapsedMilliseconds },
                    { "unique proc id", uniqueGuid }
                });
        }

        /// <summary>
        /// Validates the specified draft lot
        /// </summary>
        /// <param name="id">the id of the specified draft lot</param>
        /// <param name="returnUrl">the url to return to upon success or failure</param>
        /// <returns>redirects to returnUrl on success, or returns "EditLot" view with appropriate model errors set, if not</returns>
        [Authorize]
        public ActionResult ValidateDraftLot(int id, string returnUrl)
        {
            try
            {
                string actingUN = User.Identity.Name;
                string fillLevel = Strings.ListingFillLevels.Default;
                Lot existingLot = EventClient.GetLotByIDWithFillLevel(actingUN, id, fillLevel);
                PruneListingCustomPropertiesEditability(actingUN, existingLot.Listing.OwnerUserName, existingLot.Listing.Properties);
                EventClient.ValidateLot(actingUN, existingLot.Listing);
                PrepareSuccessMessage(Strings.MVC.ValidateDraftLotAction, MessageType.Method);
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { id = existingLot.Event.ID });
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
            catch (Exception)
            {
                PrepareErrorMessage(Strings.MVC.ValidateDraftLotAction, MessageType.Method);
            }
            return EditLot(id, returnUrl);
        }

        #endregion Validate Drafts

        #region Publish Event

        /// <summary>
        /// Processes request to publish the specified auction event
        /// </summary>
        /// <param name="id">the id of the specified auction event</param>
        /// <param name="returnUrl">optional url to redirect to upon failure</param>
        /// <returns>Redirect to 'My Account > Lots By Event' if successful, or if unsuccessful redirect to returnUrl if provided or 'Events > Drafts' if missing)</returns>
        [Authorize]
        public ActionResult PublishEvent(int id, string returnUrl)
        {
            bool successful = false;
            try
            {
                EventClient.PublishEvent(User.Identity.Name, id);
                //PrepareSuccessMessage("PublishEvent", MessageType.Method);

                KeepAlive.Start();
                Thread t = new Thread(PublishEventThreader);
                t.IsBackground = false;
                t.Name = "PublishEvent";
                t.Start(new PublishEventArgs()
                {
                    EventID = id,
                    ActingUserName = User.Identity.Name
                });

                successful = true;
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("PublishEvent", MessageType.Method);
            }
            if (successful)
            {
                return RedirectToAction(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { id });
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.EventsDraftsAction, Strings.MVC.AccountController, null);
        }

        /// <summary>
        /// container for Import CSV arguments
        /// </summary>
        public class PublishEventArgs
        {
            /// <summary>
            /// The event to be published
            /// </summary>
            public int EventID;
            /// <summary>
            /// The username of the user who requested this action
            /// </summary>
            public string ActingUserName;
        }

        private void PublishEventThreader(object args)
        {
            PublishEventArgs unpackedArgs = (PublishEventArgs)args;
            PublishEventProc(unpackedArgs.ActingUserName, unpackedArgs.EventID);
        }

        private void PublishEventProc(string actingUserName, int eventId)
        {
            //3-second delay so that the publishing user has a chance to have the results page load and SignalR connect before publish results start coming in
            Thread.Sleep(3000);
            int currentListingId = 0;
            try
            {
                //find all lots with Status: Validated
                var listingsToProcess = EventClient.SearchLotsByEvent(actingUserName, eventId, "Validated", null, null,
                    0, 0, "Id", false, Strings.ListingFillLevels.Default);
                foreach (var listing in listingsToProcess.List)
                {
                    currentListingId = listing.ID;
                    //update this lot without making any changes, to trigger applicable fees and status change
                    var input = new UserInput(actingUserName, actingUserName, SiteClient.SiteCulture, SiteClient.SiteCulture);
                    input.Items.Add(Fields.ListingID, listing.ID.ToString());
                    input.Items.Add(Fields.PublishNow, true.ToString());
                    input.Items.Add(Fields.PublishOnly, true.ToString());
                    EventClient.UpdateLotWithUserInput(SystemActors.SystemUserName, listing.Lot, input);

                }
                EventClient.FinalizeEventPublication(SystemActors.SystemUserName, eventId);
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Event Publication Failed", "EventController.PublishEventProc", TraceEventType.Error, actingUserName, e,
                    new Dictionary<string, object>() { { "Event ID", eventId }, { "Listing ID", currentListingId } });
                try
                {
                    var auctionEvent = EventClient.GetEventByIDWithFillLevel(SystemActors.SystemUserName, eventId, EventFillLevels.All);
                    auctionEvent.Status = AuctionEventStatuses.Draft;
                    EventClient.UpdateEvent(SystemActors.SystemUserName, auctionEvent);
                    LogManager.WriteLog("Event Status Reverted to Draft", "Event Publication Recovery", "EventController.PublishEventProc", TraceEventType.Error, actingUserName, null,
                        new Dictionary<string, object>() { { "Event ID", eventId } });
                    var queueManager = UnityResolver.Get<IQueueManager>();
                    if (queueManager.GetType() != typeof(QueueingDisabled))
                    {
                        queueManager.FireEventStatusChange(new DTO.EventArgs.EventStatusChange() {
                            EventID = eventId,
                            Source = "EVENT_PUBLICATION_FAILED_ORIGIN",
                            Status = AuctionEventStatuses.Draft
                        });
                    }
                    queueManager = null;
                }
                catch (Exception otherExcep)
                {
                    LogManager.WriteLog(null, "Event Publication Recovery Failed", "EventController.PublishEventProc", TraceEventType.Error, actingUserName, otherExcep,
                        new Dictionary<string, object>() { { "Event ID", eventId } });
                }
            }
            finally
            {
                KeepAlive.Stop();
            }
        }

        /// <summary>
        /// Processes request to send notification to admin request event puiblication
        /// </summary>
        /// <param name="id">the ID of the event to be published</param>
        /// <param name="returnUrl">optional url to redirect to upon failure</param>
        /// <returns>Redirect to 'My Account > Events > Drafts' by default, or redirect to returnUrl if provided</returns>
        [Authorize]
        public ActionResult RequestPublishEvent(int id, string returnUrl)
        {
            try
            {
                var allFields = new Dictionary<string, string>();
                var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, id, EventFillLevels.All);
                allFields.Add("Event_ID", auctionEvent.ID.ToString());
                allFields.Add("Event_Title", auctionEvent.Title);
                allFields.Add("Event_Subtitle", auctionEvent.Subtitle);
                string primaryImageURI = string.Empty;
                if (!string.IsNullOrEmpty(auctionEvent.PrimaryImageURI))
                {
                    primaryImageURI = SiteClient.TextSetting(SiteProperties.URL);
                    if (primaryImageURI.Right(1) != "/") primaryImageURI += "/";
                    primaryImageURI += string.Format(auctionEvent.PrimaryImageURI, "ThumbFit".ToLower());
                }
                allFields.Add("Event_PrimaryImageURI", primaryImageURI);
                allFields.Add("Event_OwnerUserName", auctionEvent.OwnerUserName);
                allFields.Add("Event_Owner_ID", auctionEvent.Owner.ID.ToString());

                var propertyBag = CommonClient.CreatePropertyBag(allFields);

                NotifierClient.QueueSystemNotification(SystemActors.SystemUserName, null, Templates.RequestPublishEvent, DetailTypes.PropertyBag, propertyBag.ID,
                    null, null, null, null, null);
                PrepareSuccessMessage("RequestPublishEvent", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("RequestPublishEvent", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.EventsDraftsAction, Strings.MVC.AccountController, null);
        }

        #endregion Publish Event

        #region Start Event Early, End Event Early & Delete Event

        /// <summary>
        /// Processes request to start the specified event early
        /// </summary>
        /// <param name="id">ID of the requested event to be ended early</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Account/EventsPublished if no return url supplied</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult StartEventEarly(int id, string returnUrl)
        {
            try
            {
                EventClient.StartEventEarly(User.Identity.Name, id);
                PrepareSuccessMessage("StartEventEarly", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("StartEventEarly", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.EventsPublishedAction, Strings.MVC.AccountController, null);
        }

        /// <summary>
        /// Processes request to end the specified event early
        /// </summary>
        /// <param name="id">ID of the requested event to be ended early</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Account/EventsPublished if no return url supplied</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult EndEventEarly(int id, string returnUrl)
        {
            try
            {
                EventClient.EndEventEarly(User.Identity.Name, id);
                PrepareSuccessMessage("EndEventEarly", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("EndEventEarly", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.EventsPublishedAction, Strings.MVC.AccountController, null);
        }

        /// <summary>
        /// Processes request to delete the specified listing
        /// </summary>
        /// <param name="id">ID of the requested listing to be deleted</param>
        /// <param name="returnUrl">the url to redirect to when the action is completed</param>
        /// <returns>Redirect to /Account/EventsPublished if no return url supplied</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult DeleteEvent(int id, string returnUrl)
        {
            try
            {
                EventClient.DeleteEvent(User.Identity.Name, id);
                PrepareSuccessMessage("DeleteEvent", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DeleteEvent", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.EventsPublishedAction, Strings.MVC.AccountController, null);
        }

        #endregion End Event Early & Delete Event

        #region Browse Events

        /// <summary>
        /// Displays a list of all events
        /// </summary>
        /// <param name="viewFilter">the filter code for the specified search criteria (e.g. &quot;All&quot;, &quot;Current&quot;, &quot;Preview&quot;, &quot;Closed&quot;)</param>
        /// <param name="page">0-based page index</param>
        /// <returns>View()</returns>        
        [Authenticate]
        public ActionResult Index(string viewFilter, int? page)
        {
            ViewData["ViewFilter"] = viewFilter;

            ViewData["ValidCategoryCounts"] = true;

            ViewData["ShowDecorations"] = SiteClient.BoolSetting(Strings.SiteProperties.ShowHomepageDecorations);

            string statuses = Strings.ListingStatuses.Active + "," + Strings.ListingStatuses.Preview;
            //if (SiteClient.BoolSetting(Strings.SiteProperties.ShowPendingListings)) statuses += "," + Strings.ListingStatuses.Pending;
            ViewData[Strings.MVC.ViewData_CategoryCounts] = CommonClient.GetCategoryCounts(new List<string>(0), statuses, string.Empty);

            ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(CategoryRoots.ListingCats, includeRelatedCustomFields: false);
            //ViewData[Strings.MVC.ViewData_StoreNavigator] = CommonClient.GetChildCategories(CategoryRoots.Stores, includeRelatedCustomFields: false);
            //ViewData[Strings.MVC.ViewData_EventNagivator] = CommonClient.GetChildCategories(CategoryRoots.Events, includeRelatedCustomFields: false);
            if (SiteClient.Properties.GetPropertyValue(SiteProperties.EnableRegions, defaultValue: false))
                ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(CategoryRoots.Regions, includeRelatedCustomFields: false);

            ViewData[Strings.MVC.PageIndex] = page ?? 0;

            return View();
        }

        #endregion Browse Events

    }
}