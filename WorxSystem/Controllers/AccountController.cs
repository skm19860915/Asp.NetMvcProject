using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Utility;
using System.Configuration;
using Microsoft.Owin.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to account-specific MVC requests
    /// </summary>
    [GoSecure]
    public class AccountController : AuctionWorxController
    {
        private static bool _passwordsImported = false;
        private AuctionWorxSignInManager _signInManager;
        private AuctionWorxUserManager _userManager;

        #region Constructors

        /// <summary>
        /// This constructor is used by the MVC framework to instantiate the controller using
        /// the default Asp.Net Identity user management components.
        /// </summary>
        public AccountController()
        {
        }

        /// <summary>
        /// This constructor is not used by the MVC framework but is instead provided for ease
        /// of unit testing this type.
        /// </summary>
        /// <param name="userManager">alternate implementation of the AuctionWorxUserManager type, which inherits Microsoft.AspNet.Identity.UserManager&lt;TUser, TKey&gt;</param>
        /// <param name="signInManager">alternate implementation of the AuctionWorxSignInManager type, which inherits Microsoft.AspNet.Identity.Owin.SignInManager&lt;TUser, TKey&gt;</param>
        public AccountController(AuctionWorxUserManager userManager, AuctionWorxSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        /// <summary>
        /// Get/Set and instance of the sign-in manager
        /// </summary>
        public AuctionWorxSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<AuctionWorxSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
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

        private void ImportUserPasswords()
        {
            try
            {
                var um = UserManager;
                Dictionary<int, string> unHashedPasswords = UserClient.GetUnHashedPasswords();
                foreach (int userId in unHashedPasswords.Keys)
                {
                    string decryptedPassword = Utilities.DecryptString(unHashedPasswords[userId], "passwordEncryptor");
                    var result = um.AddPassword(userId, decryptedPassword);
                    if (result == IdentityResult.Success)
                    {
                        var identityUser = um.FindById(userId);

                        UserInput input = new UserInput(SystemActors.SystemUserName, identityUser.UserName, SiteClient.SiteCulture, SiteClient.SiteCulture);
                        input.Items.Add(Strings.Fields.Id, userId.ToString());
                        input.Items.Add(Strings.Fields.Password, string.Empty);
                        input.Items.Add(Strings.Fields.ConfirmPassword, string.Empty);
                        UserClient.UpdateAllUserDetails(SystemActors.SystemUserName, input);
                    }
                    else
                    {
                        LogManager.WriteLog("Asp.Net Identity Import", "Password import failed",
                            FunctionalAreas.User, TraceEventType.Warning, null, null,
                            new Dictionary<string, object>() { { "UserId", userId }, { "Error", string.Join(",", result.Errors) } });
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Password import failed", FunctionalAreas.User, TraceEventType.Warning, null, e);
            }
        }

        private void ImportUserPasswords_Batch(int? previousLastFailedUserId, int previousSucceededCount, int previousFailedCount, out int? newLastFailedUserId, out int newSucceededCount, out int newFailedCount, out int remainingCount)
        {
            const int maxMSperBatch = 3000;
            newLastFailedUserId = null;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                var um = UserManager;
                Dictionary<int, string> unHashedPasswords = UserClient.GetUnHashedPasswords();
                bool skipUsers = previousLastFailedUserId.HasValue && unHashedPasswords.Any(kvp => kvp.Key == previousLastFailedUserId.Value);
                foreach (int userId in unHashedPasswords.Keys.OrderBy(key => key))
                {
                    if (stopwatch.ElapsedMilliseconds > maxMSperBatch)
                        break;

                    if (skipUsers)
                    {
                        if (userId == (previousLastFailedUserId ?? 0)) skipUsers = false;
                        continue;
                    }

                    try
                    {
                        string decryptedPassword = Utilities.DecryptString(unHashedPasswords[userId], "passwordEncryptor");
                        var result = um.AddPassword(userId, decryptedPassword);
                        if (result == IdentityResult.Success)
                        {
                            var identityUser = um.FindById(userId);

                            UserInput input = new UserInput(SystemActors.SystemUserName, identityUser.UserName, SiteClient.SiteCulture, SiteClient.SiteCulture);
                            input.Items.Add(Strings.Fields.Id, userId.ToString());
                            input.Items.Add(Strings.Fields.Password, string.Empty);
                            input.Items.Add(Strings.Fields.ConfirmPassword, string.Empty);
                            UserClient.UpdateAllUserDetails(SystemActors.SystemUserName, input);
                            previousSucceededCount++;
                        }
                        else
                        {
                            newLastFailedUserId = userId;
                            previousFailedCount++;
                            LogManager.WriteLog("Asp.Net Identity Import", "Password import failed",
                                FunctionalAreas.User, TraceEventType.Warning, null, null,
                                new Dictionary<string, object>() { { "UserId", userId }, { "Error", string.Join(",", result.Errors) } });
                        }
                    }
                    catch (Exception e)
                    {
                        newLastFailedUserId = userId;
                        previousFailedCount++;
                        LogManager.WriteLog(null, "Password import failed for user",
                            FunctionalAreas.User, TraceEventType.Error, null, e,
                            new Dictionary<string, object> { { "User ID", userId } });
                    }
                }
                remainingCount = unHashedPasswords.Count - (previousFailedCount);
            }
            catch (Exception e)
            {
                remainingCount = 0;
                LogManager.WriteLog(null, "Password import failed", FunctionalAreas.User, TraceEventType.Warning, null, e);
            }

            if (!newLastFailedUserId.HasValue && previousLastFailedUserId.HasValue)
                newLastFailedUserId = previousLastFailedUserId;
            newSucceededCount = previousSucceededCount;
            newFailedCount = previousFailedCount;
        }

        #endregion

        #region MVC Controller Overrides

        /// <summary>
        /// Called before all action methods in this controller class are invoked
        /// </summary>
        /// <param name="filterContext">Information about the current request and action</param>
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity is WindowsIdentity)
            {
                throw new InvalidOperationException(Strings.Messages.NoWindowsAuth);
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Processes request by admin to stop impersonating aonther user
        /// </summary>
        /// <returns>Redirect to site homepage</returns>        
        public ActionResult StopImpersonating()
        {
            //remember who we are currently impersonating
            string fboUN = this.FBOUserName();

            //expire the cookie (regardless if it is for a valid user)
            HttpCookie cookie = new HttpCookie(Strings.MVC.FBOUserName)
            {
                Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
            };
            HttpContext.Response.Cookies.Set(cookie);

            //attempt to retrieve the user record of who we were previously impersonating
            User targetUser;
            try
            {
                targetUser = UserClient.GetUserByUserName(User.Identity.Name, fboUN);
            }
            catch (Exception)
            {
                return RedirectToAction(Strings.MVC.UserManagementAction, Strings.MVC.AdminController);
            }

            //trying to get a non-existant user does not raise an exception, so check for null
            if (targetUser == null)
            {
                return RedirectToAction(Strings.MVC.UserManagementAction, Strings.MVC.AdminController);
            }

            //redirect to the "edit user" page for the user we stopped impersonating
            return RedirectToAction(Strings.MVC.EditUserAction, Strings.MVC.AdminController, new { @id = targetUser.ID });
        }

        #endregion

        #region Summary

        /// <summary>
        /// Displays default "My Account" view (normally "My Account > Summary")
        /// </summary>
        /// <returns>Transfer to Summary View</returns>
        [Authorize]
        public ActionResult Index()
        {
            return this.RedirectToAction(Strings.MVC.SummaryAction);
            //return new MVCTransferResult(new { controller = "Account", action = "Summary" }, this.HttpContext);
        }

        /// <summary>
        /// Displays various listing / invoice counts depending on the user's role (e.g. # items won, # invoices due, etc.)
        /// </summary>
        /// <returns>View()</returns>
        [Authorize]
        public ActionResult Summary()
        {
            string effectiveUserName;
            if (HttpContext.Request.Cookies[Strings.MVC.FBOUserName] != null)
                effectiveUserName = HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value;
            else
                effectiveUserName = User.Identity.Name;
            Dictionary<string, int> myGeneralCounts = new Dictionary<string, int>();
            Dictionary<string, int> myActionNeededCounts = new Dictionary<string, int>();
            try
            {
                //int failuretest = int.Parse("X");
                Dictionary<string, int> allCounts = AccountingClient.GetMySummaryCounts(User.Identity.Name, effectiveUserName);
                foreach (string key in allCounts.Keys)
                {
                    switch (key)
                    {
                        //buyer values
                        case "WatchedListings":
                        case "ActiveBidListings":
                        case "WonListings":
                        case "NotWonListings":
                        case "PurchaseLineitems":

                        //seller values (non-events)
                        case "ActiveListings":
                        case "SuccessListings":
                        case "UnsuccessListings":
                        case "SaleLineitems":
                        case "ScheduledListings":

                        //seller values (events)
                        case "DraftEvents":
                        case "PublishedEvents":
                        case "ClosingEvents":
                        case "ClosedEvents":

                            myGeneralCounts.Add(key, allCounts[key]);
                            break;

                        //buyer values
                        case "UnpaidPurchasedListings":
                        case "UnpaidPurchaseInvoices":
                        case "PurchasedItemFeedbackNeeded":

                        //seller values (non-events)
                        case "UnpaidSoldItems":
                        case "UnpaidSalesInvoices":
                        case "SoldItemFeedbackNeeded": //not currently implemented

                        //seller values (events)
                        case "UnpaidSoldLots":

                            myActionNeededCounts.Add(key, allCounts[key]);
                            break;
                    }
                }
            }
            catch
            {
                PrepareErrorMessage("Summary", MessageType.Method);
            }

            ViewData[RainWorx.FrameWorx.Strings.Fields.MyGeneralCounts] = myGeneralCounts;
            ViewData[RainWorx.FrameWorx.Strings.Fields.MyActionNeededCounts] = myActionNeededCounts;

            return View();
        }

        #endregion

        #region Fees

        /// <summary>
        /// Displays a page of "Current Site Fees" lineitems
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <returns>View(Invoice)</returns>
        [Authorize]
        public ActionResult Fees(int? page)
        {
            //invoice
            Invoice invoice = AccountingClient.GetPayerFees(User.Identity.Name, this.FBOUserName());
            if (invoice != null) // will be null if no fee line items exist
            {
                //payment provider views
                ViewData[Strings.MVC.ViewData_PaymentProviderViews] = AccountingClient.GetPaymentProviderViewsForInvoice(User.Identity.Name, invoice);

                /* anything that might be used by a payment provider view is going to need to be available in the view data */

                //user's credit cards
                List<CreditCard> creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
                //user's addresses
                List<Address> currentUserAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
                ViewData[Strings.MVC.ViewData_AddressList] = currentUserAddresses;
                //payer's billing address id
                ViewData[Strings.MVC.ViewData_PayerBillingAddressId] = GetBillingAddrId(invoice, currentUserAddresses);
                //credit card types
                ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                    SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
                //selected credit card id
                User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                ViewData[Strings.MVC.ViewData_SelectedCreditCardId] = currentUser.BillingCreditCardID;
                //credit cards
                ViewData[Strings.MVC.ViewData_CreditCards] = creditCards;

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

            return View(invoice);
        }

        /// <summary>
        /// Processes a request to pay "Current Site Fees"
        /// </summary>
        /// <param name="formCollection">user submitted data (e.g. new credit card details, payment method, etc)</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Fees(FormCollection formCollection)
        {
            try
            {
                Invoice invoice = AccountingClient.GetPayerFees(User.Identity.Name, this.FBOUserName());
                string providerName = formCollection[Strings.MVC.Field_Provider];
                PaymentProviderResponse result = null;
                if (!string.IsNullOrEmpty(providerName))
                {
                    //save new card if a new card was entered, approved and the "save" box was checked
                    if (formCollection[Strings.Fields.SelectedCreditCardId] == "0" &&
                        formCollection[Strings.Fields.SaveNewCard] != null)
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
                                    return Fees(0);
                                }
                                catch (Exception e)
                                {
                                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                                    return Fees(0);
                                }
                            }
                        }
                    }

                    PaymentParameters paymentParameters = new PaymentParameters();
                    paymentParameters.PayerIPAddress = Request.UserHostAddress;
                    foreach (string key in formCollection.AllKeys.Where(k => k != null))
                    {
                        paymentParameters.Items.Add(key,
                            formCollection[key] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : formCollection[key].Trim());
                    }
                    //it's probably best to let the payment provider populate the rest of the PaymentParameters fields, 
                    //  since different providers will use different data and we don't want anything provider-specific here
                    result = AccountingClient.ProcessSynchronousPayment(HttpContext.User.Identity.Name, providerName,
                                                                        invoice.ID, paymentParameters);
                }
                else
                {
                    PrepareErrorMessage(ReasonCode.UnknownPaymentMethod);
                }
                return RedirectToAction(Strings.MVC.InvoiceDetailAction,
                                        new { invoice.ID, result.Approved, message = result.ResponseDescription });
            }
            catch
            {
                PrepareErrorMessage("Fees", MessageType.Method);
                return RedirectToAction("Fees");
            }
        }

        /// <summary>
        /// Displays a page of site fee invoices
        /// </summary>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize]
        public ActionResult HistoricalFees(string sort, int? page, bool? descending)
        {
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? false;
            return View(AccountingClient.GetHistoricalPayerFees(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, sort, descending ?? true));
        }

        #endregion

        #region Bidding Lookups

        /// <summary>
        /// Displays a page of listing line items payable by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;LineItem&gt;)</returns>
        [Authorize]
        public ActionResult BiddingWon(int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BidWonOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived" })
            {
                if (SiteClient.FeedbackEnabled || !viewOpt.StartsWith("Feedback"))
                {
                    viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
                }
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ListingPageQuery currentQuery = QuerySortDefinitions.BidWonOptions[SortFilterOptions.Value];

            Page<LineItem> results = null;
            try
            {
                results = AccountingClient.GetListingLineItemsByPayer(User.Identity.Name, this.FBOUserName(), ViewFilterOption, SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<LineItem>()
                {
                    List = new List<LineItem>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of closed listings where this user has submitted an action (i.e. bid) but was not successful (i.e. did not win)
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult BiddingNotWon(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BidNotWonOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.BidNotWonOptions[SortFilterOptions.Value];

            string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Actions + "," +
                               ListingFillLevels.Properties + "," + ListingFillLevels.CurrentAction;
            return View(ListingClient.GetListingsNotWonWithFillLevel(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel));
        }

        /// <summary>
        /// Displays a page of active listings where this user has submitted an action (i.e. bid)
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult BiddingActive(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BidActiveOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.BidActiveOptions[SortFilterOptions.Value];

            string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Shipping + "," + ListingFillLevels.Actions + "," +
                               ListingFillLevels.Properties + "," + ListingFillLevels.CurrentAction;
            return View(ListingClient.GetListingsWithActiveBidsWithFillLevel(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel));
        }

        /// <summary>
        /// Displays a page of listings this user is watching
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult BiddingWatching(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BidWatchOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.BidWatchOptions[SortFilterOptions.Value];

            string fillLevel = ListingFillLevels.Decorations;
            fillLevel += "," + ListingFillLevels.Properties;
            if (SiteClient.BoolSetting(SiteProperties.ShowShippingInfoOnItemLists))
            {
                fillLevel += "," + ListingFillLevels.Shipping;
            }
            if (SiteClient.EnableEvents)
            {
                fillLevel += "," + ListingFillLevels.LotEvent;
            }

            bool quickBidForListViewsEnabled = false;
            if (User.Identity.IsAuthenticated)
            {
                //determine if inline bidding is enabled, which requires "CurrentAction" and "Actions" to be filled for each listing, in order to give authenticated user their current context (e.g. "Winning" "Not Winning" etc)
                List<CustomProperty> auctionProperties = ListingClient.GetListingTypeProperties(ListingTypes.Auction, "Site");
                var quickBidForListViewsProp = auctionProperties.FirstOrDefault(p => p.Field.Name == SiteProperties.QuickBidForListViewsEnabled);
                if (quickBidForListViewsProp != null)
                {
                    bool.TryParse(quickBidForListViewsProp.Value, out quickBidForListViewsEnabled);
                }
                if (User.Identity.IsAuthenticated && quickBidForListViewsEnabled)
                {
                    fillLevel += "," + ListingFillLevels.Actions + "," + ListingFillLevels.CurrentAction;
                }
            }

            return View(ListingClient.GetWatchedListingsWithFillLevel(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel));
        }

        /// <summary>
        /// Removes a listing from Bidding: Watching section of My Account
        /// </summary>
        /// <param name="id">the id of the specified listing to remove</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>Redirects to the Bidding: Watching page</returns>
        [Authorize]
        public ActionResult RemoveWatch(int id, int? page, int? SortFilterOptions)
        {
            UserClient.RemoveWatch(User.Identity.Name, this.FBOUserName(), id);
            PrepareSuccessMessage("RemoveWatch", MessageType.Method);
            //return View(ListingClient.GetWatchedListings(User.Identity.Name, this.FBOUserName(), page == null ? 0 : (int)page, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending));
            return RedirectToAction(Strings.MVC.BiddingWatchAction, new { page = page, SortFilterOptions = SortFilterOptions });
        }

        /// <summary>
        /// Removes multiple listings from Bidding: Watching section of My Account
        /// </summary>
        /// <param name="selectedObjects">ID or ids of listings to be removed</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>Redirects to the Bidding: Watching page</returns>
        [Authorize]
        public ActionResult RemoveMultipleWatches(string[] selectedObjects, int? page, int? SortFilterOptions)
        {
            if (selectedObjects != null)
            {
                string allIds = string.Join(",", selectedObjects);
                if (!string.IsNullOrEmpty(allIds))
                {
                    foreach (int id in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        try
                        {
                            UserClient.RemoveWatch(User.Identity.Name, this.FBOUserName(), id);
                            PrepareSuccessMessage("RemoveMultipleWatches", MessageType.Method);
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            PrepareErrorMessage(iofc.Detail.Reason);
                        }
                        catch
                        {
                            PrepareErrorMessage("RemoveMultipleWatches", MessageType.Method);
                        }
                    }
                }
            }
            return RedirectToAction(Strings.MVC.BiddingWatchAction, new { page = page, SortFilterOptions = SortFilterOptions });

        }

        #endregion

        #region Listing Lookups

        /// <summary>
        /// Displays a page of active listings owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult ListingsActive(int? page, int? SortFilterOptions, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingActiveOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingActiveOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = ListingClient.GetListingsBySeller(User.Identity.Name, this.FBOUserName(), "Active", SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
                bool offerDataNeeded = results.List.Sum(lst => lst.OfferCount) > 0;
                Dictionary<int, string> listingActiveOfferStatuses = new Dictionary<int, string>(results.List.Count);
                if (offerDataNeeded)
                {
                    string fboUserName = this.FBOUserName();
                    var allActiveOffers = ListingClient.SearchOffersByUser(User.Identity.Name, fboUserName,
                        "All", "Active", null, null, 0, 0, "CreatedOn", true).List;
                    foreach (var listing in results.List)
                    {
                        string offerStatus;
                        if (allActiveOffers.Any(o => o.ListingID == listing.ID && o.ReceivingUserName.Equals(fboUserName, StringComparison.OrdinalIgnoreCase)))
                        {
                            offerStatus = "ResponseNeeded";
                        }
                        else if (allActiveOffers.Any(o => o.ListingID == listing.ID))
                        {
                            offerStatus = "AwaitingResponse";
                        }
                        else
                        {
                            offerStatus = "NoActiveOffers";
                        }
                        listingActiveOfferStatuses.Add(listing.ID, offerStatus);
                    }
                }
                else
                {
                    foreach (var listing in results.List)
                    {
                        listingActiveOfferStatuses.Add(listing.ID, "NoActiveOffers");
                    }
                }
                ViewData["ActiveOfferListings"] = listingActiveOfferStatuses;
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of pending listings owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult ListingsPending(int? page, int? SortFilterOptions, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingPendingOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingPendingOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = ListingClient.GetListingsBySeller(User.Identity.Name, this.FBOUserName(), "Scheduled", SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of closed listings owned by this user that did not end with a buyer/winner
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult ListingsUnsuccessful(int? page, int? SortFilterOptions, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingUnsuccessfulOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingUnsuccessfulOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = ListingClient.GetListingsBySeller(User.Identity.Name, this.FBOUserName(), "Unsuccessful", SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of ended listings owned by this user, which may or may not have associated purchases
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult ListingsEnded(int? page, int? SortFilterOptions, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingEndedOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingEndedOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = ListingClient.GetListingsBySeller(User.Identity.Name, this.FBOUserName(), "Ended", SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of listing line items for listings owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;LineItem&gt;)</returns>
        [Authorize]
        public ActionResult ListingsSuccessful(int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 2; // default to: Newest Sales (index: 2)

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingSuccessOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Voided", "Archived" })
            {
                if (SiteClient.FeedbackEnabled || !viewOpt.StartsWith("Feedback"))
                {
                    viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
                }
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingSuccessOptions[SortFilterOptions.Value];

            Page<LineItem> results = null;
            try
            {
                results = AccountingClient.GetListingLineItemsBySeller(User.Identity.Name, this.FBOUserName(), ViewFilterOption, SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<LineItem>()
                {
                    List = new List<LineItem>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of listing line items for listings owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;LineItem&gt;)</returns>
        [Authorize]
        public ActionResult BiddingOffers(int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            Page<Offer> results = null;

            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;
            ListingPageQuery currentQuery = QuerySortDefinitions.BiddingOffersOptions[SortFilterOptions.Value];

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.BiddingOffersOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            //ViewFilterOption = ViewFilterOption ?? "All"; // "Active";
            try
            {
                if (string.IsNullOrEmpty(ViewFilterOption))
                {
                    //try to get active offers only first, since this is a default view with no filters assumed
                    ViewFilterOption = "Active";
                    string fillLevel = ListingFillLevels.None;
                    if (SiteClient.EnableEvents)
                    {
                        fillLevel = ListingFillLevels.LotEvent;
                    }
                    results = ListingClient.SearchOffersByUserWithListingFillLevel(User.Identity.Name, this.FBOUserName(), "All", ViewFilterOption, SearchTerm, SearchType,
                        page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
                    if (results.TotalItemCount == 0)
                    {
                        results = null;
                        ViewFilterOption = "All";
                    }
                }

                List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
                foreach (string viewOpt in
                    new string[] { "Active", "Accepted", "Declined", "Expired", "Countered", "All" })
                {
                    viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
                }
                ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

                if (results == null)
                {
                    string fillLevel = ListingFillLevels.None;
                    if (SiteClient.EnableEvents)
                    {
                        fillLevel = ListingFillLevels.LotEvent;
                    }
                    results = ListingClient.SearchOffersByUserWithListingFillLevel(User.Identity.Name, this.FBOUserName(), "All", ViewFilterOption, SearchTerm, SearchType,
                        page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Offer>()
                {
                    List = new List<Offer>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of draft listings owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult ListingsDrafts(int? page, int? SortFilterOptions, string SearchTerm, string SearchType)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.ListingDraftOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptionList] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ListingPageQuery currentQuery = QuerySortDefinitions.ListingDraftOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = ListingClient.GetListingsBySeller(User.Identity.Name, this.FBOUserName(), "Draft", SearchTerm, SearchType,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            ViewData[Strings.MVC.PageIndex] = page;
            ViewData[Strings.MVC.SortFilterOptions] = SortFilterOptions;
            ViewData[Strings.Fields.SearchTerm] = SearchTerm;
            ViewData[Strings.Fields.SearchType] = SearchType;

            return View(Strings.MVC.ListingsDraftsAction, results);
        }

        #endregion

        #region Events

        /// <summary>
        /// Displays view showing Events owned by the current user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "All", "Active", "Scheduled", "Preview", "Closing"</param>
        /// <returns>View(Page&lt;Event&gt;)</returns>
        [Authorize]
        public ActionResult EventsPublished(int? page, int? SortFilterOptions, string ViewFilterOption)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.MyEventsOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "All", Strings.AuctionEventStatuses.Active, Strings.AuctionEventStatuses.Scheduled, Strings.AuctionEventStatuses.Preview, Strings.AuctionEventStatuses.Closing })
            {
                viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ViewData[Strings.MVC.PageIndex] = page;

            ListingPageQuery currentQuery = QuerySortDefinitions.MyEventsOptions[SortFilterOptions.Value];

            string statuses;
            if (string.IsNullOrEmpty(ViewFilterOption) || ViewFilterOption.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                statuses = Strings.AuctionEventStatuses.Active +
                    "," + Strings.AuctionEventStatuses.Scheduled +
                    "," + Strings.AuctionEventStatuses.Preview +
                    "," + Strings.AuctionEventStatuses.Closing +
                    "," + Strings.AuctionEventStatuses.AwaitingPayment;
            }
            else
            {
                statuses = ViewFilterOption;
            }

            Page<Event> pageOfEvents = null;
            try
            {
                string fillLevel = Strings.EventFillLevels.Properties;
                pageOfEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("EventsPublished", e);
            }

            return View(pageOfEvents);
        }

        /// <summary>
        /// Displays view showing Events owned by the current user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Event&gt;)</returns>
        [Authorize]
        public ActionResult EventsClosed(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.MyClosedEventsOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ViewData[Strings.MVC.PageIndex] = page;

            ListingPageQuery currentQuery = QuerySortDefinitions.MyClosedEventsOptions[SortFilterOptions.Value];

            Page<Event> pageOfEvents = null;
            try
            {
                string fillLevel = Strings.EventFillLevels.Properties;
                pageOfEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), Strings.AuctionEventStatuses.Closed,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("EventsClosed", e);
            }

            return View(pageOfEvents);
        }

        /// <summary>
        /// Displays view showing Events owned by the current user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Event&gt;)</returns>
        [Authorize]
        public ActionResult EventsDrafts(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.MyEventsOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ViewData[Strings.MVC.PageIndex] = page;

            ListingPageQuery currentQuery = QuerySortDefinitions.MyEventsOptions[SortFilterOptions.Value];

            Page<Event> pageOfEvents = null;
            try
            {
                string fillLevel = Strings.EventFillLevels.Properties;
                string statuses = Strings.AuctionEventStatuses.Draft +
                    "," + Strings.AuctionEventStatuses.Publishing;
                pageOfEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
                Dictionary<int, int> draftLotCounts = new Dictionary<int, int>(pageOfEvents.List.Count);
                foreach (var auctionEvent in pageOfEvents.List)
                {
                    draftLotCounts.Add(auctionEvent.ID, EventClient.GetLotCountByListingStatus(User.Identity.Name, auctionEvent.ID, Strings.ListingStatuses.Draft));
                }
                ViewData["DraftLotCounts"] = draftLotCounts;
            }
            catch (Exception e)
            {
                PrepareErrorMessage("MyEvents", e);
            }

            return View(pageOfEvents);
        }

        /// <summary>
        /// Displays view showing Events owned by the current user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <returns>View(Page&lt;Event&gt;)</returns>
        [Authorize]
        public ActionResult EventsArchived(int? page, int? SortFilterOptions)
        {
            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 0;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.MyClosedEventsOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            ViewData[Strings.MVC.PageIndex] = page;

            ListingPageQuery currentQuery = QuerySortDefinitions.MyClosedEventsOptions[SortFilterOptions.Value];

            Page<Event> pageOfEvents = null;
            try
            {
                string fillLevel = Strings.EventFillLevels.Properties;
                pageOfEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), Strings.AuctionEventStatuses.Archived,
                    page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("MyEvents", e);
            }

            return View(pageOfEvents);
        }

        /// <summary>
        /// Processes request to set the status of one or more events to "Archived"
        /// </summary>
        /// <param name="EventIDs">array of event IDs</param>
        /// <param name="archived">true for archived, false for NOT archived</param>
        /// <param name="page">the page index to redirect to</param>
        /// <param name="SortFilterOptions">the sort option to redirect to</param>
        /// <param name="ViewFilterOption">the filter option to redirect to</param>
        /// <param name="returnUrl">the url to return to, optional</param>
        /// <returns>if returnUrl is specified, redirect to returnUrl, else if archived is true, redirect to /Account/EventsClosed, else redirect to /Account/EventsArchived</returns>
        [Authorize]
        public ActionResult SetEventsArchived(string[] EventIDs, bool archived, int? page, int? SortFilterOptions, string ViewFilterOption, string returnUrl)
        {
            string allIds = string.Join(",", EventIDs);
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int lineItemId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        EventClient.SetEventArchived(User.Identity.Name, lineItemId, archived);
                        PrepareSuccessMessage("SetEventsArchived", MessageType.Method);
                    }
                    catch (FaultException<InvalidOperationFaultContract> iofc)
                    {
                        PrepareErrorMessage(iofc.Detail.Reason);
                    }
                    catch (Exception)
                    {
                        PrepareErrorMessage("SetEventsArchived", MessageType.Method);
                    }
                }
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else if (archived)
            {
                return RedirectToAction(Strings.MVC.EventsClosedAction, new { page, SortFilterOptions, ViewFilterOption });
            }
            else
            {
                return RedirectToAction(Strings.MVC.EventsArchivedAction, new { page, SortFilterOptions, ViewFilterOption });
            }
        }

        /// <summary>
        /// Displays a list of Lots associated with the specified event
        /// </summary>
        /// <param name="id">the id of the specified event</param>
        /// <param name="page">the page index to redirect to</param>
        /// <param name="SortFilterOptions">the sort option to redirect to</param>
        /// <param name="ViewFilterOption">the filter option to redirect to</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public ActionResult LotsByEvent(int id, int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 8; // default to "lot order, low to high"

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.LotsByEventOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "Active", "Successful", "Unsuccessful", "Scheduled", "Validated", "Draft", "All" })
            {
                viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ViewData[Strings.MVC.PageIndex] = page;

            ListingPageQuery currentQuery = QuerySortDefinitions.LotsByEventOptions[SortFilterOptions.Value];

            Page<Listing> results = null;
            try
            {
                string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                results = EventClient.SearchLotsByEvent(User.Identity.Name, id, ViewFilterOption,
                    SearchTerm, SearchType, page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending, fillLevel);
                bool offerDataNeeded = results.List.Sum(lst => lst.OfferCount) > 0;
                Dictionary<int, bool> listingActiveOfferStatuses = new Dictionary<int, bool>(results.List.Count);
                if (offerDataNeeded)
                {
                    var allActiveOffers = ListingClient.SearchOffersByUser(User.Identity.Name, this.FBOUserName(),
                        "ToUser", "Active", null, null, 0, 0, "CreatedOn", true).List;
                    foreach (var listing in results.List)
                    {
                        listingActiveOfferStatuses.Add(listing.ID, allActiveOffers.Any(o => o.ListingID == listing.ID));
                    }
                }
                else
                {
                    foreach (var listing in results.List)
                    {
                        listingActiveOfferStatuses.Add(listing.ID, false);
                    }
                }
                ViewData["ActiveOfferListings"] = listingActiveOfferStatuses;
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Listing>()
                {
                    List = new List<Listing>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(Strings.MVC.LotsByEventAction, results);
        }

        /// <summary>
        /// Displays a list of Lots associated with the specified event
        /// </summary>
        /// <param name="id">the id of the specified event</param>
        /// <param name="SortFilterOptions">the sort option to redirect to</param>
        /// <param name="ViewFilterOption">the filter option to redirect to</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <returns>View(Page&lt;Listing&gt;)</returns>
        [Authorize]
        public FileContentResult LotsByEventCSV(int id, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);

            bool forcePlainTextValues = false;
            bool.TryParse(ConfigurationManager.AppSettings["LotCsvExport_ForcePlainTextValues"], out forcePlainTextValues);
            bool includeEventColumns = false;
            bool.TryParse(ConfigurationManager.AppSettings["LotCsvExport_IncludeEventColumns"], out includeEventColumns);

            //capture SortFilterOptions
            SortFilterOptions = SortFilterOptions ?? 8; // default to "lot order, low to high"

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            ListingPageQuery currentQuery = QuerySortDefinitions.LotsByEventOptions[SortFilterOptions.Value];

            StringBuilder csv = new StringBuilder();

            //determine if this user has permission (admin or owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (isAdmin || isListingOwner)
            {
                Page<Listing> results = null;
                try
                {
                    string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
                    results = EventClient.SearchLotsByEvent(User.Identity.Name, id, ViewFilterOption,
                        SearchTerm, SearchType, 0, 0, currentQuery.Sort, currentQuery.Descending, fillLevel);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    csv.AppendLine(this.GlobalResourceString("GenericErrorTitle"));

                    //display validation errors
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        //ModelState.AddModelError(issue.Key, issue.Message);
                        csv.AppendLine(this.ValidationResourceString(issue.Message));
                    }
                }

                if (results != null)
                {
                    //add header
                    csv.Append(this.GlobalResourceString("LotID"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    if (includeEventColumns)
                    {
                        csv.Append(this.GlobalResourceString("EventID"));
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append(this.GlobalResourceString("EventTitle"));
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append(this.GlobalResourceString("EventManagedBy"));
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append(this.GlobalResourceString("EventStartDate"));
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append(this.GlobalResourceString("EventEndDate"));
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                    }
                    csv.Append(this.GlobalResourceString("LotNumber"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("Title"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("Subtitle"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("Description"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("Reserve"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("ReservePrice"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("StartingPrice"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.Append(",");
                    csv.Append(this.GlobalResourceString("BuyNowPrice"));
                    if (forcePlainTextValues) csv.Append("\t");
                    csv.AppendLine();

                    foreach (Listing listing in results.List)
                    {
                        csv.Append(QuoteCSVData(listing.Lot.ID.ToString(CultureInfo.InvariantCulture) + (forcePlainTextValues ? "\t" : string.Empty)));
                        csv.Append(",");
                        if (includeEventColumns)
                        {
                            csv.Append(QuoteCSVData(auctionEvent.ID.ToString(CultureInfo.InvariantCulture) + (forcePlainTextValues ? "\t" : string.Empty)));
                            csv.Append(",");
                            csv.Append(QuoteCSVData(auctionEvent.Title + (forcePlainTextValues ? "\t" : string.Empty)));
                            csv.Append(",");
                            csv.Append(QuoteCSVData(auctionEvent.ManagedByName + (forcePlainTextValues ? "\t" : string.Empty)));
                            csv.Append(",");
                            if (auctionEvent.StartDTTM.HasValue)
                            {
                                csv.Append(QuoteCSVData(auctionEvent.StartDTTM.Value.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture) + (forcePlainTextValues ? "\t" : string.Empty)));
                            }
                            csv.Append(",");
                            csv.Append(QuoteCSVData(auctionEvent.EndDTTM.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture) + (forcePlainTextValues ? "\t" : string.Empty)));
                            csv.Append(",");
                        }
                        csv.Append(QuoteCSVData(listing.Lot.LotNumber + (forcePlainTextValues ? "\t" : string.Empty)));
                        csv.Append(",");
                        csv.Append(QuoteCSVData(listing.Title + (forcePlainTextValues ? "\t" : string.Empty)));
                        csv.Append(",");
                        csv.Append(QuoteCSVData(listing.Subtitle + (forcePlainTextValues ? "\t" : string.Empty)));
                        csv.Append(",");
                        csv.Append(QuoteCSVData(listing.Description + (forcePlainTextValues ? "\t" : string.Empty)));
                        csv.Append(",");

                        decimal? reservePriceAmount = null;
                        CustomProperty reservePrice =
                            listing.Properties.SingleOrDefault(p => p.Field.Name == Strings.Fields.ReservePrice);
                        if (reservePrice != null && !string.IsNullOrEmpty(reservePrice.Value))
                        {
                            //if reserve price is defined, see if it's met
                            reservePriceAmount = decimal.Parse(reservePrice.Value);
                        }
                        csv.Append(reservePriceAmount.HasValue && reservePriceAmount.Value > 0.0M
                            ? this.GlobalResourceString("Yes").ToUpper() : this.GlobalResourceString("No").ToUpper());
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append(reservePriceAmount.HasValue && reservePriceAmount.Value > 0.0M
                            ? reservePriceAmount.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty);
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        csv.Append((listing.OriginalPrice.HasValue && listing.OriginalPrice.Value > 0.0M)
                            ? listing.OriginalPrice.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty);
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.Append(",");
                        CustomProperty fixedPrice =
                            listing.Properties.SingleOrDefault(p => p.Field.Name == Strings.Fields.FixedPrice);
                        decimal? buyItNowPrice = null;
                        if (fixedPrice != null && !string.IsNullOrEmpty(fixedPrice.Value))
                        {
                            buyItNowPrice = decimal.Parse(fixedPrice.Value);
                        }
                        csv.Append(buyItNowPrice.HasValue && buyItNowPrice.Value > 0.0M
                            ? buyItNowPrice.Value.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty);
                        if (forcePlainTextValues) csv.Append("\t");
                        csv.AppendLine();
                    }
                }
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.GlobalResourceString("Lots_By_Event_X_csv", id);
            return content;
        }

        /// <summary>
        /// Sets ViewData to override default nav cat selection based on action name based on the specified event status
        /// </summary>
        /// <param name="auctionEventStatus">the specified event status</param>
        private void SetHighlightedNavCatByEventStatus(string auctionEventStatus)
        {
            switch (auctionEventStatus)
            {
                case Strings.AuctionEventStatuses.Archived:
                    ViewData["SelectedNavAction"] = Strings.MVC.EventsArchivedAction;
                    ViewData["EventsSubsectionTitle"] = this.GlobalResourceString("Archived");
                    break;
                case Strings.AuctionEventStatuses.Draft:
                    ViewData["SelectedNavAction"] = Strings.MVC.EventsDraftsAction;
                    ViewData["EventsSubsectionTitle"] = this.GlobalResourceString("Drafts");
                    break;
                case Strings.AuctionEventStatuses.Closed:
                    ViewData["SelectedNavAction"] = Strings.MVC.EventsClosedAction;
                    ViewData["EventsSubsectionTitle"] = this.GlobalResourceString("Closed");
                    break;
                default:
                    ViewData["SelectedNavAction"] = Strings.MVC.EventsPublishedAction;
                    ViewData["EventsSubsectionTitle"] = this.GlobalResourceString("Published");
                    break;
            }
        }

        /// <summary>
        /// Displays summary details for the specified event
        /// </summary>
        /// <param name="id">the id of the specified event</param>
        /// <returns>View(Dictionary&lt;string, decimal&gt;)</returns>
        [Authorize]
        public ActionResult EventSummary(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to view this summary (admin or event owner)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            Dictionary<string, decimal> results = null;
            try
            {
                results = EventClient.GetEventSummaryCounts(User.Identity.Name, id);
            }
            catch (Exception)
            {
                PrepareErrorMessage("EventSummary", MessageType.Method);
            }
            return View(results);
        }

        /// <summary>
        /// Queues an email notification for each invoice in the specified Event
        /// </summary>
        /// <param name="id">the ID of the specified Event</param>
        /// <param name="returnUrl">the url to return to, optional</param>
        /// <returns>Redirect to /Account/Listings/Successful</returns>
        [Authorize]
        public ActionResult EmailAllEventInvoices(int id, string returnUrl)
        {
            try
            {
                var allInvoicesInEvent = AccountingClient.GetInvoicesBySeller(User.Identity.Name, this.FBOUserName(), "All",
                    string.Empty, string.Empty, id, 0, 0, Strings.Fields.CreatedDTTM, false);
                string template = "Seller_SendInvoice";
                foreach (var invoice in allInvoicesInEvent.List)
                {
                    NotifierClient.QueueNotification(User.Identity.Name, HtmlHelpers.FBOUserName(), invoice.Payer.UserName, template,
                        "Invoice", invoice.ID, string.Empty, null, null, null, null);
                }
                PrepareSuccessMessage("EmailAllEventInvoices", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("EmailAllEventInvoices", MessageType.Method);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { EventID = id });
        }

        #endregion Events

        #region Invoice Lookups

        /// <summary>
        /// Displays a page of listing invoices payable by this user
        /// </summary>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize]
        public ActionResult InvoicePurchases(string sort, int? page, bool? descending)
        {
            ViewData["sort"] = sort ?? "CreatedDTTM";
            ViewData["page"] = page;
            ViewData["descending"] = descending ?? true;

            return View(AccountingClient.GetPayerInvoices(User.Identity.Name, this.FBOUserName(), page == null ? 0 : (int)page, SiteClient.PageSize, sort ?? "CreatedDTTM", descending ?? true));
        }

        /// <summary>
        /// Displays a page of listing invoices owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">optional filter option, e.g. &quot;All&quot;, &quot;Unpaid&quot;, &quot;Paid&quot;, &quot;NotShipped&quot;, &quot;Shipped&quot;, &quot;Archived&quot;</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by, null or 0 for all</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to, e.g. &quot;User&quot;, &quot;ListingTitle&quot;, &quot;ListingID&quot;, &quot;InvoiceID&quot;, &quot;LotNumber&quot;</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize]
        public ActionResult InvoiceSales(int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType)
        {
            //ViewData["sort"] = sort ?? "CreatedDTTM
            ViewData["page"] = page;
            //ViewData["descending"] = descending ?? true;

            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.InvoiceSalesOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "All", "Unpaid", "Paid", "NotShipped", "Shipped", "Archived" })
            {
                viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ListingPageQuery currentQuery = QuerySortDefinitions.InvoiceSalesOptions[SortFilterOptions.Value];

            Page<Invoice> results = null;
            try
            {
                results = AccountingClient.GetInvoicesBySeller(User.Identity.Name, this.FBOUserName(), ViewFilterOption,
                                                                SearchTerm, SearchType, 0,
                                                                page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Invoice>()
                {
                    List = new List<Invoice>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        /// <summary>
        /// Displays a page of listing invoices owned by this user
        /// </summary>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">optional filter option, e.g. &quot;All&quot;, &quot;Unpaid&quot;, &quot;Paid&quot;, &quot;NotShipped&quot;, &quot;Shipped&quot;, &quot;Archived&quot;</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by, null or 0 for all</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to, e.g. &quot;User&quot;, &quot;ListingTitle&quot;, &quot;ListingID&quot;, &quot;InvoiceID&quot;, &quot;LotNumber&quot;</param>
        /// <param name="EventID">null for all event data, or &gt; 0 for specific event data</param>
        /// <returns>View(Page&lt;Invoice&gt;)</returns>
        [Authorize]
        public ActionResult InvoiceEventSales(int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType, int? EventID)
        {
            //events dropdown
            int? selectedEventId = EventID ?? -1;

            var allEventsOrdered = GetAllOwnerEventsOrderedByRelevance(this.FBOUserName());
            if (!selectedEventId.HasValue && allEventsOrdered.Count > 0)
            {
                selectedEventId = allEventsOrdered.First().ID;
            }

            List<SelectListItem> formattedOptionList = new List<SelectListItem>(allEventsOrdered.Count);
            foreach (var ev in allEventsOrdered)
            {
                if (ev.ID == -1)
                {
                    formattedOptionList.Add(new SelectListItem() { Text = ev.Title, Value = ev.ID.ToString() });
                }
                else
                {
                    formattedOptionList.Add(new SelectListItem() { Text = string.Format("{0} ({1})", ev.Title, ev.ID), Value = ev.ID.ToString() });
                }
            }
            ViewData["eventSelectList"] = new SelectList(formattedOptionList, "Value", "Text", selectedEventId ?? 0);

            //ViewData["sort"] = sort ?? "CreatedDTTM
            ViewData["page"] = page;
            //ViewData["descending"] = descending ?? true;

            //capture SortFilterOptions   
            SortFilterOptions = SortFilterOptions ?? 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            List<SelectListItem> sortFilterOptions = new List<SelectListItem>();
            foreach (ListingPageQuery query in QuerySortDefinitions.InvoiceSalesOptions)
            {
                sortFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(query.Name), Value = query.Index.ToString() });
            }
            ViewData[Strings.MVC.SortFilterOptions] = new SelectList(sortFilterOptions, "Value", "Text", SortFilterOptions);

            //capture ViewFilterOption
            ViewFilterOption = ViewFilterOption ?? "All";

            List<SelectListItem> viewFilterOptions = new List<SelectListItem>(8);
            foreach (string viewOpt in
                new string[] { "All", "Unpaid", "Paid", "NotShipped", "Shipped", "Archived" })
            {
                viewFilterOptions.Add(new SelectListItem { Text = this.GlobalResourceString(viewOpt), Value = viewOpt });
            }
            ViewData[Strings.MVC.ViewFilterOption] = new SelectList(viewFilterOptions, "Value", "Text", ViewFilterOption);

            ListingPageQuery currentQuery = QuerySortDefinitions.InvoiceSalesOptions[SortFilterOptions.Value];

            Page<Invoice> results = null;
            try
            {
                results = AccountingClient.GetInvoicesBySeller(User.Identity.Name, this.FBOUserName(), ViewFilterOption,
                                                                SearchTerm, SearchType, selectedEventId,
                                                                page ?? 0, SiteClient.PageSize, currentQuery.Sort, currentQuery.Descending);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                results = new Page<Invoice>()
                {
                    List = new List<Invoice>(),
                    PageIndex = 0,
                    PageSize = SiteClient.PageSize,
                    SortExpression = currentQuery.Sort,
                    TotalItemCount = 0,
                    TotalPageCount = 0
                };
            }
            return View(results);
        }

        #endregion

        #region Generic Property Management

        /// <summary>
        /// Displays form to view/edit specified user properties
        /// Processes request to update specified user properties
        /// </summary>
        /// <param name="id">Category ID of the required property group</param>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize]
        public ActionResult PropertyManagement(int id)
        {
            return View(PropertyManagement_Implementation(id));
        }

        /// <summary>
        /// Displays form to view/edit specified user properties
        /// Processes request to update specified user properties
        /// </summary>
        /// <param name="id">Category ID of the required property group</param>
        /// <returns>View(List&lt;CustomProperty&gt;)</returns>
        [Authorize]
        public ActionResult PropertyManagement_Inline(int id)
        {
            return View(PropertyManagement_Implementation(id));
        }

        private List<CustomProperty> PropertyManagement_Implementation(int id)
        {
            Category currentCategory = CommonClient.GetCategoryByID(id);
            if (currentCategory == null || currentCategory.Type != "User")
            {
                throw new HttpException(404, "Category Not Found");
            }
            ViewData[Strings.MVC.ViewData_Category] = currentCategory;
            ViewData[Strings.MVC.ViewData_ParentCategory] = CommonClient.GetCategoryByID((int)currentCategory.ParentCategoryID);
            //ViewData["PartialPage"] = currentCategory.PropertyPage;
            ViewData[Strings.MVC.LineageString] =
                CommonClient.GetCategoryPath(id).Trees[id].ToLocalizedLineageString(this, Strings.MVC.LineageSeperator, new string[] { "Root", "User" });

            string actingUN = User.Identity.Name; // username of logged in user 
            string fboUN = this.FBOUserName(); // username of account being updated
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info

            List<CustomProperty> userProperties = PruneUserCustomFieldsForEdit(UserClient.Properties(actingUN, fboUN).WhereContainsFields(currentCategory.CustomFieldIDs), fboUN);

            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //This is a save postback

                // Populate UserInput container
                UserInput userInput = new UserInput(actingUN, fboUN, cultureCode, cultureCode);
                userInput.AddAllFormValues(this);
                try
                {
                    var propertiesToUpdate = new List<CustomProperty>(userProperties.Where(p => !p.Field.Encrypted ||
                        (p.Field.Encrypted && userInput.Items.Any(kvp => kvp.Key == p.Field.Name && kvp.Value != Fields.MaskedFieldValue))));

                    ValidateUserPropertyValues(propertiesToUpdate, userInput);

                    //attempt to update properties 
                    UserClient.UpdateProperties(actingUN, fboUN, propertiesToUpdate, userInput);
                    PrepareSuccessMessage("PropertyManagement_Implementation", MessageType.Method);

                    //re-pull properties after successful update
                    userProperties = PruneUserCustomFieldsForEdit(UserClient.Properties(actingUN, fboUN).WhereContainsFields(currentCategory.CustomFieldIDs), fboUN);
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors                
                    foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                    return (userProperties);
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                    return (userProperties);
                }
                return (userProperties);
            }
            else
            {
                //This is a load - populate model state for each user property, and format the value stored in each Property.Value field
                ModelState.FillProperties(userProperties, cultureInfo);

                return (userProperties);
            }
        }

        #endregion

        #region Login / Logout

        /// <summary>
        /// Displays the login view
        /// </summary>
        /// <param name="username">value to pre-fill the "Username" field</param>
        /// <param name="returnUrl">Url to redirect to (default site homepage if missing)</param>
        /// <returns>View()</returns>
        public ActionResult LogOn(string username, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                if (Url.IsLocalUrl(returnUrl))
                {
                    //clear the ReturnUrl cookie
                    HttpCookie cookie = new HttpCookie(Fields.ReturnUrl)
                    {
                        Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
                    };
                    HttpContext.Response.Cookies.Set(cookie);
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
                }
            }

            //check for any old passwords to be imported, only run once per application initialization -- TODO: move somewhere else?
            if (!_passwordsImported)
            {
                bool autoConvertPasswords = true;
                bool.TryParse(ConfigurationManager.AppSettings["AutoConvertPasswordsOnUpgrade"] ?? "true", out autoConvertPasswords);
                if (autoConvertPasswords)
                {
                    ImportUserPasswords();
                }
                _passwordsImported = true;
            }

            //store the return url as a cookie in case registration is needed first
            Response.SetCookie(new HttpCookie(Fields.ReturnUrl, returnUrl));
            ViewData[Fields.ReturnUrl] = returnUrl;
            return View();
        }

        /// <summary>
        /// Initiates password conversion from v3.0 to v3.1 (ASP.Net Identity)
        /// </summary>
        public ActionResult UpgradeUsers()
        {
            bool autoConvertPasswords = true;
            bool.TryParse(ConfigurationManager.AppSettings["AutoConvertPasswordsOnUpgrade"] ?? "true", out autoConvertPasswords);
            if (autoConvertPasswords)
            {
                return RedirectToAction("LogOn");

            }
            return View();
        }

        /// <summary>
        /// Processes up password conversions for up to 3000 seconds before returning results
        /// </summary>
        public JsonResult UpgradeUsers_Batch(int? totalSucceeded, int? totalFailed, int? lastFailedId)
        {
            JsonResult result = new JsonResult();
            int newSuccessCount;
            int newFailedCount;
            int remaining;
            ImportUserPasswords_Batch(lastFailedId, totalSucceeded ?? 0, totalFailed ?? 0, out lastFailedId, out newSuccessCount, out newFailedCount, out remaining);
            bool keepGoing = (newSuccessCount > (totalSucceeded ?? 0) || newFailedCount > (totalFailed ?? 0));
            result.Data = new { totalSucceeded = newSuccessCount, totalFailed = newFailedCount, lastFailedId, keepGoing, remaining };
            return result;
        }

        /// <summary>
        /// Processes login request
        /// </summary>
        /// <param name="userName">value to pre-fill with "Username" field</param>
        /// <param name="password">value to pre-fill the "Password" field</param>
        /// <param name="rememberMe">value to pre-select the "Remember Me" checkbox</param>
        /// <param name="returnUrl">Url to redirect to (default site homepage if missing)</param>
        /// <returns>Redirect to login form (login failure), specified url or site home page</returns>
        /// <remarks>on successful login the user is redirected to "returnUrl"</remarks>
        [AcceptVerbs(HttpVerbs.Post)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings",
            Justification = "Needs to take same parameter type as Controller.Redirect()")]
        public async Task<ActionResult> LogOn(string userName, string password, bool rememberMe, string returnUrl)
        {
            string actorIP = Request.UserHostAddress;
            userName = userName.Trim(); //prevents strange authentication errors if the user enters their username with leading or trailing whitespace
                                        //var result = await ValidateLogOnAsync(userName, password, rememberMe);

            if (String.IsNullOrEmpty(userName))
            {
                ModelState.AddModelError(Fields.UserName, Messages.UserNameMissing);
                LogManager.WriteLog("User Login Failed: User Name Missing", "Authentication", "User",
                    TraceEventType.Warning, actorIP, null,
                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserNameMissing } }, 0, 0, Environment.MachineName);
            }
            if (String.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(Fields.Password, Messages.PasswordMissing);
                LogManager.WriteLog("User Login Failed: Password Missing", "Authentication", "User",
                    TraceEventType.Warning, actorIP, null,
                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.PasswordMissing } }, 0, 0, Environment.MachineName);
            }

            //Check if using AD for Authentication
            SignInStatus result = SignInStatus.Failure;
            if (SiteClient.BoolSetting(SiteProperties.ActiveDirectoryEnabled))
            {
                result = AuthenticateAD(userName, password);
            }

            User user = null;
            if (!string.IsNullOrWhiteSpace(userName))
            {
                user = UserClient.GetUserByUserNameOrEmail(SystemActors.SystemUserName, userName);
            }
            if (user == null)
            {
                if (SiteClient.BoolSetting(SiteProperties.ActiveDirectoryEnabled) && result == SignInStatus.Success)
                {
                    //AD sign in was successful, but this user has never signed in before, create local user record now...
                    var input = GetAdUserDetails(userName);
                    input.Items.Add(Fields.Agreements, true.ToString());
                    input.Items.Add(Fields.Newsletter, true.ToString());
                    input.Items.Add(Fields.LastIP, actorIP);

                    string startingRoles = Roles.Buyer;
                    if (!SiteClient.RestrictOutsideSellers)
                    {
                        startingRoles += ("," + Roles.Seller);
                    }
                    if (userName.Equals(SiteClient.TextSetting(SiteProperties.ActiveDirectoryAdminUserName), StringComparison.OrdinalIgnoreCase))
                    {
                        startingRoles += ("," + Roles.Admin);
                    }

                    try
                    {
                        UserClient.RegisterUser(SystemActors.SystemUserName, input, false, new Dictionary<string, object> {
                            { UserRegistrationOptions.AddressRequired, false },
                            { UserRegistrationOptions.CreditCardRequired, false },
                            { UserRegistrationOptions.IsApproved, true },
                            { UserRegistrationOptions.IsEmailVerified, true },
                            { UserRegistrationOptions.StartingRoles, startingRoles }
                        });

                        user = UserClient.GetUserByUserNameOrEmail(SystemActors.SystemUserName, userName);
                        var loginInfo = new UserLoginInfo("ActiveDirectory", userName);
                        var addLoginResult = await UserManager.AddLoginAsync(user.ID, loginInfo);
                    }
                    catch (Exception e)
                    {
                        ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.LoginFailed);
                        LogManager.WriteLog(null, "Importing user from Active Directory failed", "User",
                                            TraceEventType.Warning, actorIP, e,
                                            new Dictionary<string, object>() { { "UserName", userName } });
                    }
                }
                else
                {
                    //don't tell browser user doesn't exist... it's information that h4x0rz don't need
                    //ModelState.AddModelError(Strings.Fields.UserName, Strings.Messages.UserNotExist);
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.LoginFailed);
                    if (!string.IsNullOrWhiteSpace(userName))
                    {
                        //only log "User doesn't exist" is username is not blank...
                        LogManager.WriteLog("User Login Failed: User Doesn't Exist", "Authentication", "User",
                        TraceEventType.Warning, actorIP, null,
                        new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserNotExist } }, 0, 0, Environment.MachineName);
                    }
                }
            }
            else
            {
                if (!user.IsActive)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.LoginFailed);
                    LogManager.WriteLog("User Login Failed: User Is Not Active", "Authentication", "User",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsNotActive } }, 0, 0,
                                        Environment.MachineName);
                }
                else if (user.IsLockedOut)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.UserIsLockedOut);
                    LogManager.WriteLog("User Login Failed: User Is Restricted", "Authentication", "User",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsLockedOut } }, 0, 0,
                                        Environment.MachineName);
                }
                else if (!user.IsApproved)
                {
                    //SiteClient.BoolSetting(Strings.SiteProperties.UserApprovalRequired) && 
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.UserIsNotApproved);
                    LogManager.WriteLog("User Login Failed: User Is Not Approved", "Authentication", "User",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsLockedOut } },
                                        0, 0, Environment.MachineName);
                }
            }

            if (!ModelState.IsValid)
            {
                ViewData[Fields.RememberMe] = rememberMe;
                ViewData[Fields.UserName] = userName;
                ViewData[Fields.ReturnUrl] = returnUrl;
                return View();
            }

            if (!user.IsVerified)
            {
                PrepareNeutralMessage(Messages.UserIsNotVerified);
                ViewData[Fields.UserName] = userName;
                return View(Strings.MVC.UserVerificationAction);
            }

            if (!SiteClient.BoolSetting(SiteProperties.ActiveDirectoryEnabled))
            {
                result = await SignInManager.PasswordSignInAsync(user.UserName, password, rememberMe, shouldLockout: true);
            }

            if (result == SignInStatus.LockedOut)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.UserTemporarilyLockedOut);
                LogManager.WriteLog("User Login Failed: User Is Locked Out", "Authentication", "User",
                                    TraceEventType.Warning, actorIP, null,
                                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserTemporarilyLockedOut } }, 0, 0,
                                    Environment.MachineName);
                return View();
            }
            else if (result != SignInStatus.Success)
            {
                if (result == SignInStatus.Failure)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Strings.Messages.LoginFailed);
                    LogManager.WriteLog("User Login Failed: Invalid Password", "Authentication", "User",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", userName } }, 0, 0,
                                        Environment.MachineName);
                }
                else
                {
                    LogManager.WriteLog("User Login Failed: Requires Verification", "Authentication", "User",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", userName } }, 0, 0,
                                        Environment.MachineName);
                }
                ViewData[Fields.RememberMe] = rememberMe;
                ViewData[Fields.UserName] = userName;
                ViewData[Fields.ReturnUrl] = returnUrl;
                return View();
            }
            else
            {
                if (SiteClient.BoolSetting(SiteProperties.ActiveDirectoryEnabled))
                {
                    //set authentication cookie
                    var loginInfo = new UserLoginInfo("ActiveDirectory", userName);
                    var identity = new ClaimsIdentity("ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
                    identity.AddClaim(new Claim("http://schemas.microsoft.com/accesscontrolservice/2010/07/claims/identityprovider", "Active Directory"));
                    identity.AddClaim(new Claim(ClaimTypes.Name, string.Format("{0} {1}", user.FirstName(), user.LastName())));
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userName));
                    foreach (var role in user.Roles)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, role.Name));
                    }
                    var externalLoginInfo = new ExternalLoginInfo()
                    {
                        Login = loginInfo,
                        ExternalIdentity = identity
                    };
                    var externalSignInResult = await SignInManager.ExternalSignInAsync(externalLoginInfo, isPersistent: false);
                }

                LogManager.WriteLog("User Login Succeeded", "Authentication", "User",
                                    TraceEventType.Information, actorIP, null,
                                    new Dictionary<string, object>() { { "UserName", userName }, { "Return URL", returnUrl } }, 0, 0,
                                    Environment.MachineName);
            }

            //record IP;
            UserClient.SetUsersIPAddress(SystemActors.SystemUserName, user.UserName, actorIP);

            if (Url.IsLocalUrl(returnUrl))
            {
                //clear the ReturnUrl cookie
                HttpCookie cookie = new HttpCookie(Fields.ReturnUrl)
                {
                    Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
                };
                HttpContext.Response.Cookies.Set(cookie);
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
            }
        }

        private SignInStatus AuthenticateAD(string username, string password)
        {
            using (var context = new PrincipalContext(ContextType.Domain, SiteClient.TextSetting(SiteProperties.ActiveDirectoryDomain)))
            {
                if (context.ValidateCredentials(username, password))
                {
                    return SignInStatus.Success;
                }
                return SignInStatus.Failure;
            }
        }

        private UserInput GetAdUserDetails(string username)
        {
            UserInput retVal = new UserInput(SystemActors.SystemUserName, username,
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));

            using (var pc = new PrincipalContext(ContextType.Domain, SiteClient.TextSetting(SiteProperties.ActiveDirectoryDomain)))
            {
                //var user = UserPrincipal.FindByIdentity(pc, IdentityType.SamAccountName, "MyDomainName\\" + username);
                var user = UserPrincipal.FindByIdentity(pc, IdentityType.SamAccountName, username);

                //retVal.Items.Add(Fields.UserName, user.UserPrincipalName);
                retVal.Items.Add(Fields.UserName, user.SamAccountName);
                retVal.Items.Add(Fields.Email, user.EmailAddress.ToLower());
                retVal.Items.Add(Fields.ConfirmEmail, user.EmailAddress.ToLower());

                string firstName = user.GivenName;
                string lastName = user.Surname;
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    firstName = string.Empty;
                    lastName = string.Empty;
                    string fullname = user.Name;
                    if (string.IsNullOrWhiteSpace(fullname))
                    {
                        fullname = user.DisplayName;
                    }
                    foreach (string namePart in fullname.Split(' '))
                    {
                        if (string.IsNullOrWhiteSpace(firstName))
                        {
                            firstName = namePart.Trim();
                        }
                        else if (string.IsNullOrWhiteSpace(lastName))
                        {
                            lastName = namePart.Trim();
                        }
                        else
                        {
                            lastName += " " + namePart.Trim();
                        }
                    }
                }
                retVal.Items.Add(Fields.FirstName, firstName);
                retVal.Items.Add(Fields.LastName, lastName);
            }
            return retVal;
        }

        /// <summary>
        /// Processes request to log off
        /// </summary>
        /// <param name="returnUrl">Url to redirect to (default site homepage if missing)</param>
        /// <returns>Redirect to site homepage</returns>
        public ActionResult LogOff(string returnUrl)
        {
            if (HttpContext.Request.Cookies.Get(Strings.MVC.FBOUserName) != null)
            {
                HttpCookie cookie = new HttpCookie(Strings.MVC.FBOUserName)
                {
                    Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
                };
                HttpContext.Response.Cookies.Set(cookie);
            }
            //FormsAuth.SignOut();
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
        }

        /// <summary>
        /// Redirects to applicable provider for authentication
        /// </summary>
        /// <param name="provider">e.g. "Facebook", "Google"</param>
        /// <param name="returnUrl">the url to redirect upon request completion</param>
        //[HttpPost]
        [AllowAnonymous]
        //[ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action(Strings.MVC.ExternalLoginCallbackAction, Strings.MVC.AccountController,
                new { ReturnUrl = returnUrl }));
        }

        /// <summary>
        /// processes user authentication attempt from third party provider
        /// </summary>
        /// <param name="returnUrl">the url to redirect upon request if successful</param>
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                PrepareErrorMessage("ExternalLoginFailed", MessageType.Message);
                return RedirectToAction("Logon");
            }

            string actorIP = Request.UserHostAddress;
            var identityUser = await UserManager.FindAsync(loginInfo.Login);
            if (identityUser == null)
            {
                //the user does not have an account, prompt the user to create an account
                ViewData[Fields.Email] = loginInfo.Email;
                ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
                ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                    SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
                ViewData[Strings.Fields.UserCustomFields] = UserClient.UserCustomFields.Where(
                    ucf => !ucf.Deferred /* && ucf.Visibility >= CustomFieldAccess.Owner */ && ucf.Mutability >= CustomFieldAccess.Owner).ToList();
                ViewData[Strings.Fields.Newsletter] = true; // opt in by default

                PrepareSuccessMessage("AuthenticationSucceeded_RegitrationNeeded", MessageType.Message);
                ViewData[Fields.ExternalUserID] = loginInfo.Login.ProviderKey;
                ViewData[Fields.ExternalProvider] = loginInfo.Login.LoginProvider;

                return View(Strings.MVC.RegisterAction);
            }

            string userName = identityUser.UserName;
            User user = UserClient.GetUserByUserName(SystemActors.SystemUserName, userName);
            if (!user.IsActive)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.LoginFailed);
                LogManager.WriteLog("User Login Failed: User Is Not Active", "Authentication", "User",
                                    TraceEventType.Warning, actorIP, null,
                                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsNotActive } }, 0, 0,
                                    Environment.MachineName);
            }
            else if (user.IsLockedOut)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.UserIsLockedOut);
                LogManager.WriteLog("User Login Failed: User Is Restricted", "Authentication", "User",
                                    TraceEventType.Warning, actorIP, null,
                                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsLockedOut } }, 0, 0,
                                    Environment.MachineName);
            }
            else if (!user.IsApproved)
            {
                //SiteClient.BoolSetting(Strings.SiteProperties.UserApprovalRequired) && 
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, Messages.UserIsNotApproved);
                LogManager.WriteLog("User Login Failed: User Is Not Approved", "Authentication", "User",
                                    TraceEventType.Warning, actorIP, null,
                                    new Dictionary<string, object>() { { "UserName", userName }, { "Reason", Messages.UserIsLockedOut } },
                                    0, 0, Environment.MachineName);
            }

            if (!ModelState.IsValid)
            {
                return View(Strings.MVC.LogOnAction);
            }

            if (!user.IsVerified)
            {
                PrepareNeutralMessage(Messages.UserIsNotVerified);
                ViewData[Fields.UserName] = userName;
                return View(Strings.MVC.UserVerificationAction);
            }

            //set authentication cookie
            //await SignInManager.SignInAsync(identityUser, false, false);
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);

            //record IP;
            UserClient.SetUsersIPAddress(SystemActors.SystemUserName, userName, actorIP);

            if (Url.IsLocalUrl(returnUrl))
            {
                //clear the ReturnUrl cookie
                HttpCookie cookie = new HttpCookie(Fields.ReturnUrl)
                {
                    Expires = DateTime.UtcNow.AddDays(-1) // or any other time in the past
                };
                HttpContext.Response.Cookies.Set(cookie);
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
            }
        }

        /// <summary>
        /// Redirects to applicable provider for authentication
        /// </summary>
        /// <param name="provider">e.g. "Facebook", "Google"</param>
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult LinkLogin(string provider)
        {
            int userId = FBOUserID();
            // Request a redirect to the external login provider to link a login for the current user
            return new ChallengeResult(provider, Url.Action(Strings.MVC.LinkLoginCallbackAction, Strings.MVC.AccountController), userId.ToString());
        }

        /// <summary>
        /// processes user authentication attempt from third party provider, linking it to currently authenticated user account
        /// </summary>
        [Authorize]
        public async Task<ActionResult> LinkLoginCallback()
        {
            int userId = FBOUserID();
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, userId.ToString());
            if (loginInfo == null)
            {
                PrepareErrorMessage("LinkLoginCallback", MessageType.Method);
                return RedirectToAction(Strings.MVC.ChangePasswordAction);
            }
            var result = await UserManager.AddLoginAsync(userId, loginInfo.Login);
            if (result.Succeeded)
            {
                PrepareSuccessMessage("LinkLoginCallback", MessageType.Method);
            }
            else
            {
                PrepareErrorMessage("LinkLoginCallback", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ChangePasswordAction);
        }

        /// <summary>
        /// removes the specified provider authentication method from the currently authenticated user account
        /// </summary>
        /// <param name="loginProvider">e.g. "Facebook", "Google"</param>
        /// <param name="providerKey">the provider-specific unique user identifier associated with this user's accoutn</param>
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        [Authorize]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            int userId = FBOUserID();
            var result = await UserManager.RemoveLoginAsync(userId, new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(userId);
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                PrepareSuccessMessage("RemoveLogin", MessageType.Method);
            }
            else
            {
                PrepareErrorMessage("RemoveLogin", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ChangePasswordAction);
        }

        /// <summary>
        /// Helper function for demo mode to allow easy access to the admin control panel
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> DemoACP()
        {
            bool adminSignInNeeded = true;
            if (SiteClient.DemoEnabled)
            {
                if (User.Identity.IsAuthenticated)
                {
                    if (!User.IsInRole("Admin"))
                    {
                        AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                    }
                    else
                    {
                        adminSignInNeeded = false;
                    }
                }
                if (adminSignInNeeded)
                {
                    string demoAdminUN = ConfigurationManager.AppSettings["DemoAdminUserName"] ?? "admin";
                    string demoAdminPW = ConfigurationManager.AppSettings["DemoAdminPassword"] ?? "demo";
                    SignInStatus result = await SignInManager.PasswordSignInAsync(demoAdminUN, demoAdminPW, true, shouldLockout: false);
                }
            }
            return RedirectToAction(Strings.MVC.SummaryAction, Strings.MVC.AdminController);
        }

        #endregion

        #region Sales Tax

        /// <summary>
        /// Parses requested sales tax rates
        /// </summary>
        /// <returns>View(List&lt;SalesTaxRate&gt;)</returns>
        private List<SalesTaxRate> DecodeSalesTaxRates()
        {
            List<SalesTaxRate> retVal = new List<SalesTaxRate>();

            foreach (string key in Request.Form.AllKeys.Where(k => k != null))
            {
                //for all keys in the form collection
                if (!key.StartsWith(Strings.MVC.RatePrefix)) continue;
                //if the key is for an image
                int id = int.Parse(key.Substring(9));
                decimal rate;
                if (decimal.TryParse(Request[Strings.MVC.RatePrefix + id], out rate))
                {
                    var newSalesTaxRate = new SalesTaxRate
                    {
                        ID = id,
                        TaxRate = rate,
                        Shipping = Request["str_TaxShipping_" + id]
                    };
                    retVal.Add(newSalesTaxRate);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Displays form to view/edit/delete sales tax rates (for use with header and footer)
        /// </summary>
        /// <returns>View(List&lt;TaxRate&gt;)</returns>
        [Authorize]
        public ActionResult SalesTaxManagement()
        {
            List<SalesTaxRate> salesTaxRates = UserClient.GetSalesTaxRatesByUser(User.Identity.Name, this.FBOUserName());
            IEnumerable<State> states = SiteClient.States.Where(s => s.Enabled);
            List<Country> countries = this.Countries();
            List<KeyValuePair<string, string>> taxableShippingOptions = new List<KeyValuePair<string, string>>(3);
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("NotTaxable"), "NotTaxable"));
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("PartiallyTaxable"), "PartiallyTaxable"));
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("FullyTaxable"), "FullyTaxable"));

            IEnumerable<TaxRate> rates = from s in states
                                         join c in countries on s.CountryID equals c.ID
                                         join r in salesTaxRates on s.ID equals r.StateID
                                         select new TaxRate() { Country = c.Name, State = s.Name, ID = r.ID, Rate = r.TaxRate, TaxableShipping = r.Shipping };

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData["TaxableShippingOptions"] = taxableShippingOptions;

            return View(rates.ToList());
        }

        /// <summary>
        /// Displays form to view/edit/delete sales tax rates (for use without header or footer)
        /// </summary>
        /// <returns>View(List&lt;TaxRate&gt;)</returns>
        [Authorize]
        public ActionResult SalesTaxManagement_Inline()
        {
            List<SalesTaxRate> salesTaxRates = UserClient.GetSalesTaxRatesByUser(User.Identity.Name, this.FBOUserName());
            IEnumerable<State> states = SiteClient.States.Where(s => s.Enabled);
            List<Country> countries = this.Countries();
            List<KeyValuePair<string, string>> taxableShippingOptions = new List<KeyValuePair<string, string>>(3);
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("NotTaxable"), "NotTaxable"));
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("PartiallyTaxable"), "PartiallyTaxable"));
            taxableShippingOptions.Add(new KeyValuePair<string, string>(this.GlobalResourceString("FullyTaxable"), "FullyTaxable"));

            IEnumerable<TaxRate> rates = from s in states
                                         join c in countries on s.CountryID equals c.ID
                                         join r in salesTaxRates on s.ID equals r.StateID
                                         select new TaxRate() { Country = c.Name, State = s.Name, ID = r.ID, Rate = r.TaxRate, TaxableShipping = r.Shipping };

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData["TaxableShippingOptions"] = taxableShippingOptions;

            return View(rates.ToList());
        }

        /// <summary>
        /// Processes request to update/delete sales tax rates
        /// </summary>
        /// <returns>View(List&lt;TaxRate&gt;)</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult UpdateSalesTaxRates(string returnTo)
        {
            try
            {
                //Update
                List<SalesTaxRate> salesTaxRatesToUpdate = DecodeSalesTaxRates();
                if (salesTaxRatesToUpdate.Count > 0)
                    UserClient.UpdateSalesTaxRates(User.Identity.Name, salesTaxRatesToUpdate);

                //Delete
                if (!string.IsNullOrEmpty(Request[Strings.Fields.DeleteRate]))
                    UserClient.DeleteSalesTaxRates(User.Identity.Name, Request[Strings.Fields.DeleteRate]);

                //Add
                if (!string.IsNullOrEmpty(Request[Strings.Fields.Rate]) &&
                    !string.IsNullOrEmpty(Request[Strings.Fields.StateRegion]) &&
                    !string.IsNullOrEmpty(Request[Strings.Fields.Country]))
                {
                    decimal rate;

                    if (decimal.TryParse(Request[Strings.Fields.Rate], out rate))
                    {
                        UserClient.AddSalesTaxRate(User.Identity.Name, this.FBOUserName(),
                                                    int.Parse(Request[Strings.Fields.StateRegion]), rate, Request["taxShipping"]);
                    }
                }
                PrepareSuccessMessage("UpdateSalesTaxRates", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("UpdateSalesTaxRates", MessageType.Method);
            }
            if (returnTo == "inline")
            {
                return RedirectToAction(Strings.MVC.SalesTaxManagement_InlineAction);
            }
            return RedirectToAction(Strings.MVC.SalesTaxManagementAction);
        }

        /// <summary>
        /// Displays form to update tax rates
        /// </summary>
        /// <returns>View(List&lt;User&gt;)</returns>
        [GoUnsecure]
        [Authorize]
        public ActionResult MyTaxRates_Inline()
        {
            return View(UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName()));
        }

        /// <summary>
        /// Displays form to update seller's payment settings (e.g. paypal email, allow instant checkout)
        /// </summary>
        /// <returns>View(List&lt;User&gt;)</returns>
        [GoUnsecure]
        [Authorize]
        public ActionResult MyPaymentSettings_Inline()
        {
            return View(UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName()));
        }

        #endregion

        #region Invoices

        /// <summary>
        /// Processes request to update an invoice comment
        /// </summary>
        /// <param name="InvoiceID">ID of the invoice to be updated</param>
        /// <param name="Comments">New comments value</param>
        /// <param name="returnUrl">Url to redirect to (default invoice detail view if missing)</param>
        /// <param name="ApplyToAllInvoices">in Events Edition, if true applies this comment to all invoices in the same event</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
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
        [Authorize]
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
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
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
        /// Processes request to add an invoice adjustment line item
        /// </summary>
        /// <param name="InvoiceID">ID of the invoice to be updated</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
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
                ////display validation errors
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                //PrepareErrorMessage("AddInvoiceAdjustment", MessageType.Method);
                //StoreValidationIssues(vfc.Detail.ValidationIssues, userInput);

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
        /// Processes request to remove a line item from an invoice
        /// </summary>
        /// <param name="lineItemID">ID of the line item to be removed</param>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view, or "returnUrl" if invoice was deleted.</returns>
        /// <remarks>
        /// If the invoice was deleted because the last line item was removed, the user is redirected to "returnUrl".
        /// If "returnUrl" is null or empty, the user is redirected based on the context.  For fee invoices, redirect to
        /// /Account/Fees.  Otherwise, if the user is the invoice owner, redirect to /Account/Invoice/Sales.  Otherwise,
        /// redirect to /Account/Invoice/Purchases.
        /// </remarks>
        [Authorize]
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
                        alternateReturnUrl = Url.Action(Strings.MVC.FeesAction);
                    }
                    else if (invoice.Owner.UserName == this.FBOUserName() || invoice.Payer.UserName == this.FBOUserName())
                    {
                        //listing invoice owned by this user
                        alternateReturnUrl = invoice.AuctionEventId.HasValue
                            ? Url.Action(Strings.MVC.InvoiceEventSalesAction)
                            : Url.Action(Strings.MVC.InvoiceSalesAction);
                    }
                    else
                    {
                        //listing invoice not owned by this user
                        alternateReturnUrl = Url.Action(Strings.MVC.InvoicePurchasesAction);
                    }
                }
                catch
                {
                    //ignore this error;
                }
            }
            bool invoiceDeleted;
            try
            {
                invoiceDeleted = AccountingClient.RemoveLineItemFromInvoice(User.Identity.Name, invoiceID, lineItemID);

                //remove line item
                if (invoiceDeleted)
                {
                    //this was the last line item, so the invoice was deleted - redirect to the return Url or the accounty summary page
                    PrepareSuccessMessage(Messages.LastLineitemRemovedInvoiceDeleted, MessageType.Message);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return Redirect(alternateReturnUrl);
                }
                else
                {
                    PrepareSuccessMessage("RemoveLineItem", MessageType.Method);
                    if (Url.IsLocalUrl(returnUrl) && returnUrl.Contains("void"))
                    {
                        //special case -- if this was part of a request to void an invoiced lineitem then proceed directly with that action
                        return Redirect(returnUrl);
                    }
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
        /// Processes request to add a line item to an invoice
        /// </summary>
        /// <param name="lineItemID">ID of the line item to be added</param>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
        public ActionResult AddLineItem(int lineItemID, int invoiceID, string returnUrl)
        {
            //add line item
            try
            {
                AccountingClient.AddLineItemToInvoice(User.Identity.Name, invoiceID, lineItemID);
                PrepareSuccessMessage("AddLineItem", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("AddLineItem", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to add a line item to an invoice
        /// </summary>
        /// <param name="lineItemIds">Comma separated list of ID's of the line item(s) to be added</param>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
        public ActionResult AddAllLineItems(string[] lineItemIds, int invoiceID, string returnUrl)
        {
            List<int> itemIds;
            if (lineItemIds.Length == 1)
                itemIds = lineItemIds[0].Split(',').Select(s => int.Parse(s)).ToList();
            else
                itemIds = lineItemIds.Select(s => int.Parse(s)).ToList();

            //add line item
            foreach (int lineItemId in itemIds)
            {
                AddLineItem(lineItemId, invoiceID, returnUrl);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Helper function which determines selected billing address, if possible 
        /// </summary>
        /// <param name="invoice">invoice to be evaluated</param>
        /// <param name="payerAddresses">list of payer's existing address records</param>
        /// <returns>The ID of the matching billing address, or null if none match</returns>
        private static int? GetBillingAddrId(Invoice invoice, IEnumerable<Address> payerAddresses)
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
        /// <param name="returnUrl">Url assigned to the "back" button displayed on the invoice detail view</param>
        /// <returns>View(Invoice)</returns>
        [Authorize]
        public ActionResult InvoiceDetail(int id, bool? approved, string message, int? creditCardId, int? page, string returnUrl)
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
                return View(Strings.MVC.InvoiceDetailAction);
            }
            if (invoice == null)
            {
                PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                return View(Strings.MVC.InvoiceDetailAction);
            }

            ////testing -- set/update invoice property(ies)
            //Dictionary<string, string> invoiceProps = new Dictionary<string, string>();
            ////invoiceProps.Add("MyInvProp1", "My test value 1");
            ////invoiceProps.Add("MyInvProp2", "My test value B");
            //AccountingClient.UpdateInvoiceProperties(Strings.SystemActors.SystemUserName, id, invoiceProps);

            ViewData[Strings.Fields.ReturnUrl] = returnUrl;
            ViewData[Strings.Fields.InvoiceID] = id;
            ViewData[Strings.Fields.Approved] = approved;
            if (!string.IsNullOrEmpty(message))
            {
                message = this.GlobalResourceString(message);
            }
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
                    PrepareSuccessMessage(Messages.PaymentProcessedSuccessfully, MessageType.Message);
                }
            }

            //CheckValidationIssues();

            if (invoice.Payer.UserName.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase))
            {
                ViewData["SelectedNavAction"] = Strings.MVC.InvoicePurchasesAction;
            }
            else if (invoice.AuctionEventId.HasValue)
            {
                ViewData["SelectedNavAction"] = Strings.MVC.InvoiceEventSalesAction;
            }
            else
            {
                ViewData["SelectedNavAction"] = Strings.MVC.InvoiceSalesAction;
            }

            return View(Strings.MVC.InvoiceDetailAction, invoice);
        }

        /// <summary>
        /// Processes request to pay invoice
        /// </summary>
        /// <param name="id">ID of the requested invoice</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <param name="formCollection">details of the payment request</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
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
                        paymentParameters.Items.Add(key,
                            formCollection[key] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : formCollection[key].Trim());
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
        /// FOR TESTING USE ONLY - DISABLE IN PRODUCTION
        /// </summary>
        [Authorize]
        public ActionResult TestCardAuthOnly()
        {
            //////////////////////////////////////////////////////////////////////////////////
            //                                                                              //
            //  !! IMPORTANT !! Uncomment this line to properly disable in production !!    //
            //                                                                              //
            return new HttpNotFoundResult();
            //                                                                              //
            //////////////////////////////////////////////////////////////////////////////////

            User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            var minimalProps = currentUser.Properties.Where(p => p.Field.Name == StdUserProps.FirstName || p.Field.Name == StdUserProps.LastName).ToList();
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
            ModelState.FillProperties(minimalProps, cultureInfo);
            ViewData[Fields.Properties] = minimalProps;

            //user's credit cards
            List<CreditCard> creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
            //user's addresses
            List<Address> currentUserAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            ViewData[Strings.MVC.ViewData_AddressList] = currentUserAddresses;
            //payer's billing address id
            ViewData[Strings.MVC.ViewData_PayerBillingAddressId] = currentUser.BillingAddressID; //GetBillingAddrId(invoice, currentUserAddresses);
            //credit card types
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
            //selected credit card id
            ViewData[Strings.MVC.ViewData_SelectedCreditCardId] = currentUser.BillingCreditCardID;
            //credit cards
            ViewData[Strings.MVC.ViewData_CreditCards] = creditCards;
            //payment provider views
            string ccProvider = AccountingClient.GetSaleBatchPaymentProviderName();
            ViewData[Strings.MVC.ViewData_PaymentProviderViews] = //AccountingClient.GetPaymentProviderViewsForInvoice(User.Identity.Name, invoice);
                new Dictionary<string, string>() { { ccProvider, string.Format("{0}Invoice_Buyer", ccProvider) } };

            var billingAddress = currentUserAddresses.FirstOrDefault(adr => adr.ID == currentUser.BillingAddressID);
            if (billingAddress == null)
            {
                billingAddress = currentUserAddresses.FirstOrDefault();
            }
            if (billingAddress != null)
            {
                ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name, billingAddress.Country.ID);
            }
            else
            {
                ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            }

            return View();
        }

        /// <summary>
        /// FOR TESTING USE ONLY - DISABLE IN PRODUCTION
        /// </summary>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult TestCardAuthOnly(FormCollection formCollection)
        {
            //////////////////////////////////////////////////////////////////////////////////
            //                                                                              //
            //  !! IMPORTANT !! Uncomment this line to properly disable in production !!    //
            //                                                                              //
            return new HttpNotFoundResult();
            //                                                                              //
            //////////////////////////////////////////////////////////////////////////////////

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
                        paymentParameters.Items.Add(key,
                            formCollection[key] == Strings.MVC.TrueFormValue ? Strings.MVC.TrueValue : formCollection[key].Trim());
                    }

                    ////it's probably best to let the payment provider populate the rest of the PaymentParameters fields, 
                    ////  since different providers will use different data and we don't want anything provider-specific here
                    //result = AccountingClient.ProcessSynchronousPayment(HttpContext.User.Identity.Name, providerName, id,
                    //                                                    paymentParameters);


                    result = AccountingClient.AuthorizePayment(User.Identity.Name, null, this.FBOUserName(), 1.01M, SiteClient.SiteCurrency, providerName, paymentParameters);


                    if (result.Approved)
                    {
                        PrepareSuccessMessage("APPROVED", MessageType.Message);
                    }
                    else
                    {
                        PrepareNeutralMessage(result.ResponseDescription);
                    }

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
                //return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl, result.Approved, message = result.ResponseDescription });
            }
            catch (Exception e)
            {
                PrepareErrorMessage(e.Message, MessageType.Message);
                //return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl });
            }

            User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            var minimalProps = currentUser.Properties.Where(p => p.Field.Name == StdUserProps.FirstName || p.Field.Name == StdUserProps.LastName).ToList();
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.SiteCulture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureCode); // number & date formatting info
            ModelState.FillProperties(minimalProps, cultureInfo);
            ViewData[Fields.Properties] = minimalProps;

            //user's credit cards
            List<CreditCard> creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
            //user's addresses
            List<Address> currentUserAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            ViewData[Strings.MVC.ViewData_AddressList] = currentUserAddresses;
            //payer's billing address id
            ViewData[Strings.MVC.ViewData_PayerBillingAddressId] = currentUser.BillingAddressID; //GetBillingAddrId(invoice, currentUserAddresses);
            //credit card types
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
            //selected credit card id
            ViewData[Strings.MVC.ViewData_SelectedCreditCardId] = currentUser.BillingCreditCardID;
            //credit cards
            ViewData[Strings.MVC.ViewData_CreditCards] = creditCards;
            //payment provider views
            string ccProvider = AccountingClient.GetSaleBatchPaymentProviderName();
            ViewData[Strings.MVC.ViewData_PaymentProviderViews] = //AccountingClient.GetPaymentProviderViewsForInvoice(User.Identity.Name, invoice);
                new Dictionary<string, string>() { { ccProvider, string.Format("{0}Invoice_Buyer", ccProvider) } };

            var billingAddress = currentUserAddresses.FirstOrDefault(adr => adr.ID == currentUser.BillingAddressID);
            if (billingAddress == null)
            {
                billingAddress = currentUserAddresses.FirstOrDefault();
            }
            if (billingAddress != null)
            {
                ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name, billingAddress.Country.ID);
            }
            else
            {
                ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            }

            return View();
        }

        /// <summary>
        /// Processes request to create an invoice based on the specified line item
        /// </summary>
        /// <param name="id">ID of the line item</param>
        /// <param name="returnUrl">Url to redirect to (default account summary if missing)</param>
        /// <returns>Redirect to invoice detail view (or redirect to "returnUrl" for errors)</returns>
        [Authorize]
        public ActionResult CreateInvoice(int id, string returnUrl)
        {
            try
            {
                //int.Parse("x"); // error test

                //make invoice
                Invoice invoice = AccountingClient.CreateInvoiceFromLineItem(User.Identity.Name, id);

                if (invoice.Payer.UserName == User.Identity.Name)
                {
                    PrepareSuccessMessage(Strings.Messages.PaymentRequired, MessageType.Message);
                }
                else
                {
                    PrepareSuccessMessage("CreateInvoice", MessageType.Method);
                }

                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoice.ID, returnUrl });
            }
            catch
            {
                PrepareErrorMessage("CreateInvoice", MessageType.Method);

                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Processes request from payee to confirm payment record entered by payer via free form payment provider
        /// </summary>
        /// <param name="id">ID of the invoice to be updated</param>
        /// <param name="formCollection">user input collection</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
        [HttpPost]
        public ActionResult ConfirmInvoicePayment(int id, FormCollection formCollection)
        {
            try
            {
                string providerName = formCollection[Strings.MVC.Field_Provider];
                UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                foreach (string key in formCollection.AllKeys.Where(k => k != null))
                {
                    userInput.Items.Add(key, formCollection[key].Trim());
                }
                PaymentProviderResponse response = AccountingClient.ProcessAsynchronousPayment(User.Identity.Name, userInput);
                PrepareSuccessMessage("ConfirmInvoicePayment", MessageType.Method);
                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, response.Approved, response.ResponseDescription });
            }
            catch
            {
                PrepareErrorMessage("ConfirmInvoicePayment", MessageType.Method);
                return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id });
            }
        }

        /// <summary>
        /// Processes request from seller to manually mark their own listing invoice as "Paid" or "Unpaid"
        /// </summary>
        /// <param name="id">ID of the invoice to be updated</param>
        /// <param name="paid">true means "Set invoice as paid", false means "Set invoice as unpaid"</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to the invoice detail view</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
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
            catch (FaultException<AuthorizationFaultContract> afc)
            {
                PrepareErrorMessage(afc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("SetInvoicePaid", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id, returnUrl });
        }

        /// <summary>
        /// Processes request from seller to manually mark their own listing invoice as "Paid"
        /// </summary>
        /// <param name="selectedObjects">ID or ids of invoices to be updated</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <param name="returnToEventsInvoices">if true, return to /Account/InvoiceEventSales</param>
        /// <returns>Redirect to the previous invoice list view</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult MarkMultipleInvoicesPaid(string[] selectedObjects,
            int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType, bool? returnToEventsInvoices)
        {
            if (selectedObjects != null)
            {
                string allIds = string.Join(",", selectedObjects);
                if (!string.IsNullOrEmpty(allIds))
                {
                    foreach (int i in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        try
                        {
                            AccountingClient.SetInvoicePaid(User.Identity.Name, i, true);
                            PrepareSuccessMessage("MarkMultipleInvoicesPaid", MessageType.Method);
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            PrepareErrorMessage(iofc.Detail.Reason);
                        }
                        catch
                        {
                            PrepareErrorMessage("MarkMultipleInvoicesPaid", MessageType.Method);
                        }
                    }
                }
            }

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            if (returnToEventsInvoices ?? false)
            {
                return RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
            else
            {
                return RedirectToAction(Strings.MVC.InvoiceSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
        }

        /// <summary>
        /// Processes request from seller to manually mark their own listing invoice as "Shipped"
        /// </summary>
        /// <param name="selectedObjects">ID or ids of invoices to be updated</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <param name="returnToEventsInvoices">if true, return to /Account/InvoiceEventSales</param>
        /// <returns>Redirect to the previous invoice list view</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult MarkMultipleInvoicesShipped(string[] selectedObjects,
            int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType, bool? returnToEventsInvoices)
        {
            if (selectedObjects != null)
            {
                string allIds = string.Join(",", selectedObjects);
                if (!String.IsNullOrEmpty(allIds))
                {
                    bool errorsExist = false;
                    foreach (int invoiceId in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        try
                        {
                            AccountingClient.SetInvoiceShipped(User.Identity.Name, invoiceId, true);
                            if (!errorsExist) PrepareSuccessMessage("MarkMultipleInvoicesShipped", MessageType.Method);
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            PrepareErrorMessage(iofc.Detail.Reason);
                            errorsExist = true;
                        }
                        catch
                        {
                            PrepareErrorMessage("MarkMultipleInvoicesShipped", MessageType.Method);
                            errorsExist = true;
                        }
                    }
                }
            }

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            if (returnToEventsInvoices ?? false)
            {
                return RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
            else
            {
                return RedirectToAction(Strings.MVC.InvoiceSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
        }

        /// <summary>
        /// Processes request from seller to archive the selected invoices
        /// </summary>
        /// <param name="selectedObjects">ID or ids of invoices to be updated</param>
        /// <param name="archived">true to archive selected, false to un-archive selected</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="SortFilterOptions">index of requested sort option (default 0)</param>
        /// <param name="ViewFilterOption">e.g. "NeedInvoice", "Invoiced", "FeedbackRequired", "FeedbackNotReceived", "Unpaid", "Paid", "All", "Archived"</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <param name="returnToEventsInvoices">if true, return to /Account/InvoiceEventSales</param>
        /// <returns>Redirect to the previous invoice list view</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult MarkMultipleInvoicesArchived(string[] selectedObjects, bool archived,
            int? page, int? SortFilterOptions, string ViewFilterOption, string SearchTerm, string SearchType, bool? returnToEventsInvoices)
        {
            if (selectedObjects != null)
            {
                string allIds = string.Join(",", selectedObjects);
                if (!String.IsNullOrEmpty(allIds))
                {
                    bool errorsExist = false;
                    foreach (int invoiceId in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        try
                        {
                            AccountingClient.SetInvoiceArchived(User.Identity.Name, invoiceId, archived);
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            PrepareErrorMessage(iofc.Detail.Reason);
                            errorsExist = true;
                        }
                        catch
                        {
                            PrepareErrorMessage("MarkMultipleInvoicesArchived", MessageType.Method);
                            errorsExist = true;
                        }
                    }
                    if (!errorsExist) PrepareSuccessMessage("MarkMultipleInvoicesArchived", MessageType.Method);
                }
            }

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            if (returnToEventsInvoices ?? false)
            {
                return RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
            else
            {
                return RedirectToAction(Strings.MVC.InvoiceSalesAction, new { page, SortFilterOptions, ViewFilterOption, SearchTerm, SearchType });
            }
        }

        /// <summary>
        /// Processes request to toggle whether a line item is taxable
        /// </summary>
        /// <param name="lineItemID">ID of the line item to be updated</param>
        /// <param name="invoiceID">ID of the invoice to return to</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail</returns>
        [Authorize]
        public ActionResult ToggleTaxableLineItem(int lineItemID, int invoiceID, string returnUrl)
        {
            AccountingClient.ToggleLineItemTaxable(this.FBOUserName(), lineItemID);
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to toggle whether buyer's premium applies to a line item
        /// </summary>
        /// <param name="lineItemID">ID of the line item to be updated</param>
        /// <param name="invoiceID">ID of the invoice to return to</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail</returns>
        public ActionResult ToggleBpAppliesLineItem(int lineItemID, int invoiceID, string returnUrl)
        {
            AccountingClient.ToggleLineItemBuyersPremiumApplies(this.FBOUserName(), lineItemID);
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID, returnUrl });
        }

        /// <summary>
        /// Processes request to set the "shipped" status of an invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to update</param>
        /// <param name="shipped">true for shipped, false for NOT shipped</param>
        /// <param name="returnUrl">Return Url passed to invoice detail view</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult SetInvoiceShipped(int invoiceID, bool shipped, string returnUrl)
        {
            try
            {
                AccountingClient.SetInvoiceShipped(this.FBOUserName(), invoiceID, shipped);
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
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
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
        /// Processes request to set the "Archived" status of one or more line items
        /// </summary>
        /// <param name="LineItemIDs">array of line item IDs</param>
        /// <param name="archived">true for archived, false for NOT archived</param>
        /// <param name="page">the page index to return to</param>
        /// <returns>Redirect to /Account/Listings/Successful</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult SetLineItemsArchived(string[] LineItemIDs, bool archived, int? page)
        {
            string allIds = string.Join(",", LineItemIDs);
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int lineItemId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        AccountingClient.SetLineItemArchived(User.Identity.Name, lineItemId, archived);
                        PrepareSuccessMessage("SetLineItemsArchived", MessageType.Method);
                    }
                    catch
                    {
                        PrepareErrorMessage("SetLineItemsArchived", MessageType.Method);
                    }
                }
            }
            return RedirectToAction(Strings.MVC.ListingsSuccessfulAction, new { page });
        }

        /// <summary>
        /// Processes request to set the "Archived" status of one or more line items
        /// </summary>
        /// <param name="LineItemIDs">array of line item IDs</param>
        /// <param name="archived">true for archived, false for NOT archived</param>
        /// <param name="page">the page index to return to</param>
        /// <returns>Redirect to /Account/Listings/Successful</returns>
        [Authorize(Roles = Strings.Roles.BuyerAndAdmin)]
        public ActionResult SetLineItemsArchivedByPayer(string[] LineItemIDs, bool archived, int? page)
        {
            string allIds = string.Join(",", LineItemIDs);
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int lineItemId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        AccountingClient.SetLineItemArchivedByPayer(this.FBOUserName(), lineItemId, archived);
                        PrepareSuccessMessage("SetLineItemsArchived", MessageType.Method);
                    }
                    catch
                    {
                        PrepareErrorMessage("SetLineItemsArchived", MessageType.Method);
                    }
                }
            }
            return RedirectToAction(Strings.MVC.BiddingWonAction, new { page });
        }

        /// <summary>
        /// Processes requests to void or un-void the specified sales line items
        /// </summary>
        /// <param name="LineItemIDs">array of line item IDs</param>
        /// <param name="voided">true to void the specified line items, false to un-void</param>
        /// <param name="page">the page index to redirect to</param>
        /// <param name="returnUrl">optional return URL</param>
        /// <returns>Redirect to /Account/Listings/Successful if no return url is provided</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult VoidLineItems(string[] LineItemIDs, bool voided, int? page, string returnUrl)
        {
            string allIds = string.Join(",", LineItemIDs);
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int lineItemId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        AccountingClient.SetLineItemVoided(this.FBOUserName(), lineItemId, voided);
                        PrepareSuccessMessage("VoidLineItems", MessageType.Method);
                        if (voided)
                        {
                            LogManager.WriteLog("Void Sale Success", "Void Sale", "Accounting",
                                        TraceEventType.Information, this.FBOUserName(), null,
                                        new Dictionary<string, object>() { { "UserName", this.FBOUserName() }, { "LineItemID", lineItemId } },
                                        0, 0, Environment.MachineName);
                        }
                        else
                        {
                            LogManager.WriteLog("Un-Void Sale Success", "Void Sale", "Accounting",
                                                                TraceEventType.Information, this.FBOUserName(), null,
                                                                new Dictionary<string, object>() { { "UserName", this.FBOUserName() }, { "LineItemID", lineItemId } },
                                                                0, 0, Environment.MachineName);
                        }
                    }
                    catch (FaultException<InvalidOperationFaultContract> iofc)
                    {
                        PrepareErrorMessage(iofc.Detail.Reason);
                    }
                    catch
                    {
                        PrepareErrorMessage("VoidLineItems", MessageType.Method);
                    }
                }
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsSuccessfulAction, new { page });
        }

        /// <summary>
        /// Creates an invoice for each successful line item, adding similar line items where possible.
        /// </summary>
        /// <param name="eventId">optional event ID to limit results to</param>
        /// <param name="returnUrl">optional return URL</param>
        /// <returns>Redirect to /Account/Listings/Successful if no return url is provided</returns>
        [Authorize(Roles = Strings.Roles.SellerAndAdmin)]
        public ActionResult CreateAllInvoices(int? eventId, string returnUrl)
        {
            int numInvoicesCreated = AccountingClient.CreateAllInvoices(User.Identity.Name, this.FBOUserName(), eventId);
            if (numInvoicesCreated == 1)
            {
                PrepareSuccessMessage("oneInvoiceSuccessfullyCreated", MessageType.Message);
            }
            else if (numInvoicesCreated > 1)
            {
                //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                PrepareSuccessMessage(this.GlobalResourceString("xInvoicesSuccessfullyCreated", numInvoicesCreated), MessageType.Message);
            }
            else
            {
                PrepareNeutralMessage("NoInvoicesToCreate");
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsSuccessfulAction);
        }

        /// <summary>
        /// Attempts to charge the card on file for all applicable unpaid sale invoices
        /// </summary>
        /// <param name="eventId">if specified, only invoices in this event will be processed, otherwise all invoices owned by the currently authenticated (or impersonated) seller will be processed</param>
        /// <param name="returnUrl">if specified, AND no payment failures occurred, the user will be redirected here after payments are finished processing.</param>
        /// <returns>If any payments are skipped or fail, the user will be redirected to the list of invoices</returns>
        public ActionResult PayAllInvoices(int? eventId, string returnUrl)
        {
            bool? ManualPaymentProcessingInitiated = SiteClient.GetCacheData("ManualPaymentProcessingInitiated") as bool?;
            if (ManualPaymentProcessingInitiated ?? false)
            {
                PrepareErrorMessage(this.GlobalResourceString("PaymentsAreAlreadyBeingProcessed"), MessageType.Message);
                return RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { EventID = eventId.Value });
            }

            ActionResult retVal = null;
            try
            {
                SiteClient.SetCacheData("ManualPaymentProcessingInitiated", true, 1440);

                int successCount = 0;
                int failedCount = 0;
                if (eventId.HasValue && eventId.Value > 0)
                {
                    AccountingClient.ChargeCreditCardsForAllUnpaidSalesByEvent(User.Identity.Name, eventId.Value, null, out successCount, out failedCount);
                }
                else
                {
                    AccountingClient.ChargeCreditCardsForAllUnpaidSalesBySeller(User.Identity.Name, this.FBOUserName(), null, out successCount, out failedCount);
                }
                if (successCount == 1)
                {
                    PrepareSuccessMessage("oneInvoiceSuccessfullyPaid", MessageType.Message);
                }
                else if (successCount > 1)
                {
                    //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                    PrepareSuccessMessage(this.GlobalResourceString("xInvoicesSuccessfullyPaid", successCount), MessageType.Message);
                }
                if (failedCount == 1)
                {
                    PrepareErrorMessage("oneInvoiceNotPaid", MessageType.Message);
                }
                else if (failedCount > 1)
                {
                    //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                    PrepareErrorMessage(this.GlobalResourceString("xInvoicesNotPaid", failedCount), MessageType.Message);
                }
                if (successCount == 0 && failedCount == 0)
                {
                    PrepareNeutralMessage("NoInvoicesPaid");
                }
                if (failedCount == 0 && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    retVal = Redirect(returnUrl);
                }
                if (eventId.HasValue)
                {
                    if (failedCount > 0)
                    {
                        retVal = RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { EventID = eventId.Value, ViewFilterOption = "Unpaid" });
                    }
                    else
                    {
                        retVal = RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { EventID = eventId.Value });
                    }
                }
                else
                {
                    if (failedCount > 0)
                    {
                        retVal = RedirectToAction(Strings.MVC.InvoiceSalesAction, new { ViewFilterOption = "Unpaid" });
                    }
                    else
                    {
                        retVal = RedirectToAction(Strings.MVC.InvoiceSalesAction);
                    }
                }
            }
            catch (Exception e)
            {
                PrepareErrorMessage("PayAllInvoices", e);
            }
            finally
            {
                SiteClient.SetCacheData("ManualPaymentProcessingInitiated", false, 1440);
            }
            if (retVal == null)
            {
                retVal = RedirectToAction(Strings.MVC.InvoiceEventSalesAction, new { EventID = eventId });
            }
            return retVal;
        }

        /// <summary>
        /// Displays invoice details, formatted for printing
        /// </summary>
        /// <param name="id">ID of the requested invoice</param>
        /// <returns>View(Invoice)</returns>
        [Authorize]
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
        /// Displays multiple invoice details, formatted for printing
        /// </summary>
        /// <param name="selectedObjects">optional list of Invoice IDs to print</param>
        /// <param name="EventID">optional ID of the Event to print all invoices for</param>
        /// <remarks>either selectedObjects or EventID is needed to retrieve invoices</remarks>
        /// <returns>View(List&lt;Invoice&gt;)</returns>
        [Authorize]
        public ActionResult PrintMultipleInvoices(string[] selectedObjects, int? EventID)
        {
            var results = new List<Invoice>();
            var shippingOptionContainer = new Dictionary<int, string>();
            var lineItemsContainer = new Dictionary<int, Page<LineItem>>();
            if (EventID.HasValue)
            {
                //get all invoices in this event
                var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, EventID.Value, string.Empty);
                var allInvoicesInThisEvent = AccountingClient.GetInvoicesBySeller(User.Identity.Name, auctionEvent.OwnerUserName, "All",
                    string.Empty, string.Empty, EventID.Value, 0, 0, Strings.Fields.CreatedDTTM, false);
                results.AddRange(allInvoicesInThisEvent.List);
            }
            else
            {
                //get each invoice specified
                if (selectedObjects != null)
                {
                    string allIds = string.Join(",", selectedObjects);
                    if (!string.IsNullOrEmpty(allIds))
                    {
                        foreach (int id in allIds.Split(',').Select(s => int.Parse(s)))
                        {
                            Invoice invoice = null;
                            try
                            {
                                invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, id);
                            }
                            catch (FaultException<AuthorizationFaultContract>)
                            {
                                ////the logged in user is not authiorized to view this invoice
                                //PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                                //return View();
                            }
                            //if (invoice == null)
                            //{
                            //    PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                            //    return View();
                            //}
                            if (invoice != null)
                            {
                                results.Add(invoice);
                            }
                        }
                    }
                }
            }
            //ViewData[Strings.Fields.InvoiceID] = id;
            foreach (var invoice in results)
            {
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
                shippingOptionContainer.Add(invoice.ID, shippingOption);

                var pageOfLineItems = AccountingClient.GetLineItemsByInvoice(User.Identity.Name, invoice.ID, 0,
                                                                             0, // page size=0 returns all line items
                                                                             Strings.Fields.DateStamp, false);
                lineItemsContainer.Add(invoice.ID, pageOfLineItems);
            }
            ViewData[Strings.Fields.ShippingOption] = shippingOptionContainer;
            ViewData[Strings.MVC.ViewData_PageOfLineitems] = lineItemsContainer;
            return View(results);
        }


        /// <summary>
        /// Queues multiple invoice emails to be sent
        /// </summary>
        /// <param name="selectedObjects">optional list of Invoice IDs to print</param>
        /// <param name="returnUrl">optional return URL</param>
        /// <returns>Redirect to /Account/Invoice/Sales if no return url is provided</returns>
        [Authorize]
        public ActionResult EmailMultipleInvoices(string[] selectedObjects, string returnUrl)
        {
            int successCount = 0;
            int requestedCount = 0;
            string failList = string.Empty;
            string failDelim = string.Empty;
            //get each invoice specified
            if (selectedObjects != null)
            {
                string allIds = string.Join(",", selectedObjects);
                if (!string.IsNullOrEmpty(allIds))
                {
                    foreach (int id in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        requestedCount++;
                        Invoice invoice = null;
                        try
                        {
                            invoice = AccountingClient.GetInvoiceByID(User.Identity.Name, id);
                        }
                        catch (FaultException<AuthorizationFaultContract>)
                        {
                            ////the logged in user is not authiorized to view this invoice
                            //PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                            //return View();
                        }
                        //if (invoice == null)
                        //{
                        //    PrepareErrorMessage(ReasonCode.InvoiceNotFound);
                        //    return View();
                        //}
                        if (invoice != null)
                        {
                            NotifierClient.QueueNotification(User.Identity.Name, invoice.Owner.UserName, invoice.Payer.UserName,
                                Templates.SellerSendInvoice, Strings.DetailTypes.Invoice, invoice.ID, string.Empty, null, null, null, null);
                            successCount++;
                        }
                        else
                        {
                            failList += (failDelim + id.ToString());
                            failDelim = ", ";
                        }
                    }
                }
            }
            if (requestedCount == 0 || successCount == 0)
            {
                PrepareErrorMessage("EmailMultipleInvoices", MessageType.Method);
            }
            else if (requestedCount > successCount)
            {
                PrepareNeutralMessage(this.GlobalResourceString("EmailMultipleInvoicesPartialSuccess", requestedCount, successCount, failList));
            }
            else
            {
                PrepareSuccessMessage("EmailMultipleInvoices", MessageType.Method);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.InvoiceSalesAction, Strings.MVC.AccountController);
        }

        /// <summary>
        /// displays the success result of a paypal payment
        /// </summary>
        /// <param name="invoice"></param>
        /// <returns></returns>
        [Authorize]
        public ActionResult PaymentReceived(int? invoice)
        {
            ViewData["invoice"] = invoice;
            return View();
        }

        /// <summary>
        /// Either adds the specified line item to an eligible invoice if it exists, otherwise creates a new invoice, and redirects to the invoice detail page
        /// </summary>
        /// <param name="lineitemid">the id of the specified lineitem</param>
        /// <param name="returnUrl">Url to redirect to</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult Checkout(int? lineitemid, string returnUrl)
        {
            try
            {
                if (!lineitemid.HasValue)
                {
                    LogManager.WriteLog("Sale Not Found", "Invalid checkout URL", "MVC", TraceEventType.Warning, this.FBOUserName(), null,
                        new Dictionary<string, object>() { { "Controller", "Account" }, { "Action", "Checkout" } });
                    PrepareErrorMessage("SaleNotFound_PleaseCheckTheUrlAndTryAgain", MessageType.Message);
                }
                else
                {
                    var newLineItem = AccountingClient.GetLineItemByID(User.Identity.Name, lineitemid.Value);
                    if (newLineItem.InvoiceID.HasValue)
                    {
                        //already invoiced...
                        return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { @id = newLineItem.InvoiceID.Value, returnUrl });
                    }
                    //find any uninvoiced lineitems to this buyer and generate an invoice, if needed
                    if (newLineItem.Listing.OwnerAllowsInstantCheckout() || SiteClient.BoolSetting(SiteProperties.AutoGenerateInvoices))
                    {
                        //get all unpaid invoices from this seller to this buyer to see if any of them are appropriate to add this line item to
                        var unpaidInvoicesTothisBuyer = AccountingClient.GetInvoicesBySeller(newLineItem.Listing.OwnerUserName, newLineItem.Listing.OwnerUserName,
                            "Unpaid", this.FBOUserName(), "User", newLineItem.AuctionEventId, 0, 0, Fields.CreatedDTTM, true).List
                            .Where(inv => inv.Status != InvoiceStatuses.Pending && inv.Payer.UserName.Equals(this.FBOUserName()));
                        Invoice invoice = null;
                        foreach (var potentialInvoice in unpaidInvoicesTothisBuyer)
                        {
                            List<ShippingMethodCounts> temp = new List<ShippingMethodCounts>();
                            var similarLineItems = AccountingClient.GetSimilarLineItems(User.Identity.Name, potentialInvoice.ID, ref temp);
                            if (similarLineItems.Any(li => li.ID == newLineItem.ID))
                            {
                                //eligible invoice found, add it now and stop checking remaining invoices...
                                invoice = potentialInvoice;
                                AccountingClient.AddLineItemToInvoice(User.Identity.Name, invoice.ID, newLineItem.ID);
                                break;
                            }
                        }
                        if (invoice == null)
                        {
                            //no eligible invoice was found, create a new one
                            invoice = AccountingClient.CreateInvoiceFromLineItem(User.Identity.Name, newLineItem.ID);
                        }
                        if (invoice != null)
                        {
                            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { @id = invoice.ID, returnUrl });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PrepareErrorMessage("Checkout", e);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(Strings.MVC.BiddingWonAction);
            }
        }

        #endregion

        #region Addresses

        /// <summary>
        /// Displays a list of all addresses owned by this user
        /// </summary>
        /// <returns>View(List&lt;Address&gt;)</returns>
        [Authorize]
        public ActionResult AddressManagement()
        {
            User temp = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            ViewData["PrimaryAddressID"] = temp.PrimaryAddressID;
            var creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
            var addresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            var creditCardCounts = new Dictionary<int, int>();
            foreach (var address in addresses)
            {
                creditCardCounts[address.ID] = creditCards.Count(cc => cc.AddressID == address.ID);
            }
            ViewData["CreditCardCounts"] = creditCardCounts;
            return View(addresses);
        }

        /// <summary>
        /// Processes request to delete the spcified address
        /// </summary>
        /// <param name="addressID">ID of the address to be deleted</param>
        /// <returns>Redirect to address management view</returns>
        [Authorize]
        public ActionResult DeleteAddress(int addressID)
        {
            try
            {
                if (SiteClient.BoolSetting(Strings.SiteProperties.RequireCreditCardOnRegistration))
                {
                    var creditCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
                    if (!creditCards.Any(cc => cc.AddressID != addressID))
                    {
                        PrepareErrorMessage("DeleteAddressDeniedMessage", MessageType.Message);
                        return RedirectToAction(Strings.MVC.AddCreditCardAction);
                    }
                }
                UserClient.DeleteAddress(User.Identity.Name, this.FBOUserName(), addressID);
                PrepareSuccessMessage("DeleteAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.AddressManagementAction);
        }

        /// <summary>
        /// Processes request to set the specified address as the primary address
        /// </summary>
        /// <param name="addressID">ID of the address to be updated</param>
        /// <returns>Redirect to address management view</returns>
        [Authorize]
        public ActionResult SetPrimaryAddress(int addressID)
        {
            try
            {
                UserClient.SetPrimaryAddressForUser(User.Identity.Name, this.FBOUserName(), addressID);
                PrepareSuccessMessage("SetPrimaryAddress", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetPrimaryAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.AddressManagementAction);
        }

        /// <summary>
        /// Displays form to update the specified address
        /// </summary>
        /// <param name="addressID">ID of the address to be updated</param>
        /// <returns>View(Address)</returns>
        [Authorize]
        public ActionResult EditAddress(int addressID)
        {
            Address address = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName()).Where(a => a.ID == addressID).SingleOrDefault();
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name, address.Country.ID);
            return View(address);
        }

        /// <summary>
        /// Processes request to update the specified address
        /// </summary>
        /// <returns>Redirect to address management view on success or View(Address) if there are errors</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult EditAddress()
        {
            //new or update
            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);

            //do call to BLL
            try
            {
                //update the address, should return an address int...
                UserClient.UpdateAddress(User.Identity.Name, input);
                PrepareSuccessMessage("EditAddress", MessageType.Method);
                return RedirectToAction(Strings.MVC.AddressManagementAction);
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

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            PrepareErrorMessage("EditAddress", MessageType.Method);
            return View(new Address());
        }

        /// <summary>
        /// Displays form to enter a new address
        /// </summary>
        /// <param name="SetBillingAddress">if supplied, the id of the invoice to set the new billing address upon success</param>
        /// <param name="SetShippingAddress">if supplied, the id of the invoice to set the new shipping address upon success</param>
        /// <param name="returnUrl">Url to redirect to upon success</param>
        /// <returns>View()</returns>
        [Authorize]
        public ActionResult CreateAddress(int? SetBillingAddress, int? SetShippingAddress, string returnUrl)
        {
            ViewData[Fields.SetBillingAddress] = SetBillingAddress;
            ViewData[Fields.SetShippingAddress] = SetShippingAddress;
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Fields.ReturnUrl] = returnUrl;
            return View();
        }

        /// <summary>
        /// Processes request to add a new address
        /// </summary>
        /// <param name="Description">user-defined name for the new address</param>
        /// <param name="SetBillingAddress">if supplied, the id of the invoice to set the new billing address upon success</param>
        /// <param name="SetShippingAddress">if supplied, the id of the invoice to set the new shipping address upon success</param>
        /// <param name="returnUrl">Url to redirect to upon success (invoice detail or address management list, if missing)</param>
        /// <returns>Redirects to invoice detail, or address management view, or returnUrl (success) or View() (errors)</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CreateAddress(string Description, int? SetBillingAddress, int? SetShippingAddress, string returnUrl)
        {
            //new or update
            //IN (populate UserInput and prepare ModelState for output)
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
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
                }
                else if (SetShippingAddress.HasValue)
                {
                    AccountingClient.SetInvoiceShippingAddress(User.Identity.Name, SetShippingAddress.Value, newAddressId);
                    PrepareSuccessMessage("SetShippingAddress", MessageType.Method);
                }
                else
                {
                    PrepareSuccessMessage("CreateAddress", MessageType.Method);
                }
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else if (SetBillingAddress.HasValue)
                {
                    return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = SetBillingAddress.Value });
                }
                else if (SetShippingAddress.HasValue)
                {
                    return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = SetShippingAddress.Value });
                }
                else
                {
                    PrepareSuccessMessage("CreateAddress", MessageType.Method);
                    return RedirectToAction(Strings.MVC.AddressManagementAction);
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
            }

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            PrepareErrorMessage("CreateAddress", MessageType.Method);
            return View();
        }

        /// <summary>
        /// Displays form to select a shipping address for the specified invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <returns>View(List&lt;Address&gt;)</returns>
        [Authorize]
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
            List<Address> addresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            if (addresses.Count == 0)
            {
                return RedirectToAction(Strings.MVC.CreateAddressAction, new { SetShippingAddress = invoiceID });
            }
            ViewData["selectedAddressID"] = GetShippingAddrId(invoice, addresses);
            ViewData[Strings.Fields.InvoiceID] = invoiceID;
            return View(addresses);
        }

        /// <summary>
        /// Processes request to set the specified address as the shipping address
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="selectedAddressID">ID of the address to be used</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SetShippingAddress(int invoiceID, int selectedAddressID)
        {
            try
            {
                AccountingClient.SetInvoiceShippingAddress(User.Identity.Name, invoiceID, selectedAddressID);
                PrepareSuccessMessage("SetShippingAddress", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("SetShippingAddress", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, new { id = invoiceID });
        }

        /// <summary>
        /// Displays form to select a billing address for the specified invoice
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <returns>View(List&lt;Address&gt;)</returns>
        [Authorize]
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
            List<Address> addresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            if (addresses.Count == 0)
            {
                return RedirectToAction(Strings.MVC.CreateAddressAction, new { SetBillingAddress = invoiceID });
            }
            ViewData["selectedAddressID"] = GetBillingAddrId(invoice, addresses);
            ViewData[Strings.Fields.InvoiceID] = invoiceID;
            return View(addresses);
        }

        /// <summary>
        /// Processes request to set the specified address as the billing address
        /// </summary>
        /// <param name="invoiceID">ID of the invoice to be updated</param>
        /// <param name="selectedAddressID">ID of the address to be used</param>
        /// <returns>Redirect to invoice detail view</returns>
        [Authorize]
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

        #endregion

        #region Relisting / Activating

        /// <summary>
        /// Processes request to relist the specified listing
        /// </summary>
        /// <param name="id">ID of the listing to be updated</param>
        /// <param name="ReturnUrl">Url to redirect to (default listing detail view if missing)</param>
        /// <returns>Redirect to current site fees view (if immediate payment is required) or "ReturnUrl" (if no fee is due)</returns>
        [Authorize]
        public ActionResult Relist(int id, string ReturnUrl)
        {
            bool payToProceedRequired = false;
            try
            {
                //ListingClient.DeleteListing(User.Identity.Name, id);
                //payToProceedRequired = AccountingClient.Relist(User.Identity.Name, ref id);
                payToProceedRequired = ListingClient.Relist(User.Identity.Name, id);

                PrepareSuccessMessage("Relist", MessageType.Method);
            }
            catch (FaultException<InvalidOperationFaultContract> iofc)
            {
                //possible reason codes: ReasonCode.ListingNotExist, ReasonCode.CantRelistSuccessfulListing, ReasonCode.CantRelistActiveListing
                PrepareErrorMessage(iofc.Detail.Reason);
            }
            catch (Exception)
            {
                PrepareErrorMessage("Relist", MessageType.Method);
            }
            if (payToProceedRequired)
            {
                return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController);
            }
            else if (Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            else
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id });
            }
        }

        /// <summary>
        /// Processes request to relist multiple listings at once
        /// </summary>
        /// <param name="ListingIDs">ID or IDs of listings to be relisted</param>
        /// <param name="page">Current page of listings</param>
        /// <param name="SortFilterOptions">Current sort option of listings</param>
        /// <param name="returnUrl">Url to redirect to (default unsuccessul listings view if missing)</param>
        /// <returns>Redirect to current fees view (if immediate payment is required) or "returnUrl", otherwise</returns>
        [Authorize]
        public ActionResult RelistBulk(string[] ListingIDs, int? page, int? SortFilterOptions, string returnUrl)
        {
            bool showInvoice = false;
            string allIds = string.Join(",", ListingIDs);
            int relistCount = 0;
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int listingId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        bool payToProceed = ListingClient.Relist(User.Identity.Name, listingId);
                        relistCount++;
                        if (payToProceed)
                        {
                            showInvoice = true;
                        }
                    }
                    catch
                    {
                        PrepareErrorMessage("RelistBulk", MessageType.Method);
                    }
                }
            }
            //normally success messages are not pre-localized, but this one needs to be because it requires an argument
            PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyRelisted", relistCount), MessageType.Message);
            if (showInvoice)
            {
                return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController, new { id = 0, returnUrl });
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsUnsuccessfulAction, new { page, SortFilterOptions });
        }

        /// <summary>
        /// Processes request to activate multiple draft listings at once
        /// </summary>
        /// <param name="ListingIDs">ID or IDs of listings to be activated</param>
        /// <param name="page">Current page of listings</param>
        /// <param name="SortFilterOptions">Current sort option of listings</param>
        /// <param name="SearchTerm">optional search keyword(s) to filter the results by</param>
        /// <param name="SearchType">optional search type indicating what field(s) the keyword should be applied to</param>
        /// <param name="returnUrl">Url to redirect to (default draft listings view if missing)</param>
        /// <returns>Redirect to current fees view (if immediate payment is required) or "returnUrl", otherwise</returns>
        [Authorize]
        public ActionResult ActivateListings(string[] ListingIDs, int? page, int? SortFilterOptions, string SearchTerm, string SearchType, string returnUrl)
        {
            string actingUserName = User.Identity.Name;
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            bool showInvoice = false;
            string allIds = string.Join(",", ListingIDs);
            int activatedCount = 0;
            int attemptedCount = 0;

            //remove leading/trailing whitespace from SearchTerm
            SearchTerm = !string.IsNullOrEmpty(SearchTerm) ? SearchTerm.Trim() : SearchTerm;

            if (!string.IsNullOrEmpty(allIds))
            {
                try
                {
                    foreach (int listingId in allIds.Split(',').Select(s => int.Parse(s)))
                    {
                        attemptedCount++;
                        try
                        {
                            var listing = ListingClient.GetListingByIDWithFillLevel(actingUserName, listingId, Strings.ListingFillLevels.All);
                            var input = new UserInput(actingUserName, listing.OwnerUserName, cultureCode, cultureCode);
                            input.FillInputFromListing(listing);
                            bool payToProceed = ListingClient.UpdateListingWithUserInput(actingUserName, listing, input);
                            activatedCount++;
                            if (payToProceed)
                            {
                                showInvoice = true;
                            }
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                            {
                                //display each validation error
                                ModelState.AddModelError(string.Format("{0}_ERR_VLDTN_{1}", listingId, issue.Key),
                                    string.Format("({0}{1}) {2}", this.GlobalResourceString("ListingNum"), listingId, this.ValidationResourceString(issue.Message)));
                            }
                        }
                        catch (FaultException<InvalidOperationFaultContract> iofc)
                        {
                            ModelState.AddModelError(string.Format("{0}_{1}", listingId, "ERR_INVLD_OP"),
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("ListingNum"), listingId, this.GlobalResourceString(iofc.Detail.Reason.ToString())));
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(string.Format("{0}_{1}", listingId, "ERR_OTHR_EXCPTN"),
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("ListingNum"), listingId, this.GlobalResourceString(e.Message)));
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage(Strings.MVC.ActivateListingsAction, MessageType.Method);
                }
            }

            if (activatedCount > 0)
            {
                //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyActivated", activatedCount), MessageType.Message);
            }

            if (activatedCount < attemptedCount)
            {
                PrepareErrorMessage(this.GlobalResourceString("xNotActivated", (attemptedCount - activatedCount)), MessageType.Message);
            }

            if (!ModelState.IsValid)
            {
                //one or more errors occurred
                ViewData["SelectedNavAction"] = Strings.MVC.ListingsDraftsAction;
                ViewData[Strings.Fields.ReturnUrl] = returnUrl;
                return ListingsDrafts(page, SortFilterOptions, SearchTerm, SearchType);
            }

            if (showInvoice)
            {
                return RedirectToAction(Strings.MVC.FeesAction, Strings.MVC.AccountController, new { id = 0, returnUrl });
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsDraftsAction, new { page, SortFilterOptions });
        }

        #endregion

        #region User Registration

        /// <summary>
        /// Displays new user registration form
        /// </summary>
        /// <returns>View() if user is not logged in, or Redirect to site homepage, otherwise</returns>
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);

            //check for missing payment provider settings and redirect if necessary
            if (!SiteClient.DemoEnabled && SiteClient.BoolSetting(SiteProperties.RequireCreditCardOnRegistration))
            {
                bool stripeCredentialsNeeded = SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled)
                                            && (string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_ClientId)) ||
                                                string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesPublishableApiKey)) ||
                                                string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey)));

                bool authNetCredentialsNeeded = SiteClient.BoolSetting(SiteProperties.AuthorizeNet_Enabled)
                                            && (string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_PostUrl)) ||
                                                string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_MerchantLoginID)) ||
                                                string.IsNullOrEmpty(SiteClient.TextSetting(SiteProperties.AuthorizeNet_TransactionKey)) ||
                                                SiteClient.TextSetting(SiteProperties.AuthorizeNet_MerchantLoginID).Equals("login") ||
                                                SiteClient.TextSetting(SiteProperties.AuthorizeNet_TransactionKey).Equals("key"));
                if (stripeCredentialsNeeded)
                {
                    PrepareErrorMessage("StripeEnabled_CredentialsNeeded", MessageType.Message);
                    //var stripePropManagementCat = CommonClient.GetChildCategories(45001).FirstOrDefault(c => c.Name == "StripeConnect");
                    //if (stripePropManagementCat != null)
                    //{
                    //    return RedirectToAction(Strings.MVC.PropertyManagementAction, Strings.MVC.AdminController, new { id = stripePropManagementCat.ID });
                    //}
                }
                else if (authNetCredentialsNeeded)
                {
                    PrepareErrorMessage("AuthNetEnabled_CredentialsNeeded", MessageType.Message);
                    //var authNetPropManagementCat = CommonClient.GetChildCategories(45001).FirstOrDefault(c => c.Name == "AuthorizeDotNet");
                    //if (authNetPropManagementCat != null)
                    //{
                    //    return RedirectToAction(Strings.MVC.PropertyManagementAction, Strings.MVC.AdminController, new { id = authNetPropManagementCat.ID });
                    //}
                }
            }

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.UserCustomFields] = UserClient.UserCustomFields.Where(
                ucf => !ucf.Deferred /* && ucf.Visibility >= CustomFieldAccess.Owner */ && ucf.Mutability >= CustomFieldAccess.Owner).ToList();

            return View(Strings.MVC.RegisterAction);
        }

        /// <summary>
        /// Processes new user registration request
        /// </summary>
        /// <param name="UserName">the requested new username</param>
        /// <returns>Redirect to new registration landing page (success) or View()</returns>
        [HttpPost]
        public async Task<ActionResult> Register(string UserName)
        {
            if (!string.IsNullOrWhiteSpace(UserName) && UserName != UserName.Trim())
            {
                UserName = UserName.Trim();
            }

            //validate required fields
            var validation = new ValidationResults();

            string demoReCaptchaPublicKey = ConfigurationManager.AppSettings["DemoReCaptchaPublicKey"];
            if (SiteClient.BoolSetting(SiteProperties.EnableRecaptchaForRegistration) || !string.IsNullOrWhiteSpace(demoReCaptchaPublicKey))
            {
                await this.ValidateCaptcha(validation, this);
            }

            //IN (populate UserInput and prepare ModelState for output)            
            UserInput input = new UserInput(Strings.Roles.AnonymousUser, Strings.Roles.AnonymousUser,
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            input.AddAllFormValues(this);
            input.Items.Add(Fields.LastIP, Request.UserHostAddress);

            var regOptions = new Dictionary<string, object>();

            if (SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled))
            {
                regOptions.Add(UserRegistrationOptions.CreditCardRequired, false);

                if (SiteClient.BoolSetting(SiteProperties.RequireCreditCardOnRegistration) &&
                    (!input.Items.ContainsKey(Fields.StripeToken) || string.IsNullOrWhiteSpace(input.Items[Fields.StripeToken])))
                {
                    //missing credit card token from stripe
                    validation.AddResult(new ValidationResult(Messages.CreditCardRequired, this, string.Empty, string.Empty, null));
                }
            }

            bool passwordSupplied = input.Items.ContainsKey(Fields.Password) && !string.IsNullOrWhiteSpace(input.Items[Fields.Password]);
            if (passwordSupplied)
            {
                //check that confirmation passwords matches
                string newPassword = input.Items[Fields.Password];
                string confPassword = input.Items[Fields.ConfirmPassword];
                if (newPassword != confPassword)
                {
                    validation.AddResult(new ValidationResult(Messages.ConfirmationPasswordMismatch, this, Fields.Password, Fields.Password, null));
                }
                var passwordResult = await UserManager.PasswordValidator.ValidateAsync(newPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var errorMessage in passwordResult.Errors)
                    {
                        if (errorMessage == "PasswordTooShort")
                        {
                            string localizedErr = this.ValidationResourceString("PasswordTooShort", ConfigurationManager.AppSettings["Password_RequiredLength"] ?? "6");
                            validation.AddResult(new ValidationResult(localizedErr, this, Fields.Password, Fields.Password, null));
                        }
                        else
                        {
                            validation.AddResult(new ValidationResult(errorMessage, this, Fields.Password, Fields.Password, null));
                        }
                    }
                }
            }

            bool externalLoginSupplied = input.Items.ContainsKey(Fields.ExternalUserID) && !string.IsNullOrWhiteSpace(input.Items[Fields.ExternalUserID]);
            string externalUserID = null;
            string externalProvider = null;
            if (externalLoginSupplied)
            {
                externalUserID = input.Items[Fields.ExternalUserID];
                externalProvider = input.Items[Fields.ExternalProvider];
            }

            if (passwordSupplied == false && externalLoginSupplied == false)
            {
                validation.AddResult(new ValidationResult(Messages.PasswordMissing, this, Fields.Password, Fields.Password, null));
            }

            //do call to BLL
            try
            {
                if (!validation.IsValid)
                {
                    Statix.ThrowValidationFaultContract(validation);
                }

                UserClient.RegisterUser(Strings.Roles.AnonymousUser, input, false, regOptions);

                var newUser = await UserManager.FindByNameAsync(UserName);

                bool newUserDeleted = false;
                //if password was supplied
                if (passwordSupplied)
                {
                    var result = await UserManager.AddPasswordAsync(newUser.Id, input.Items[Fields.Password].Trim());
                    if (!result.Succeeded)
                    {
                        AddErrors(result);
                        UserClient.DeleteUser(SystemActors.SystemUserName, newUser.Id);
                        //return View();
                        newUserDeleted = true;
                    }
                }

                if (externalLoginSupplied)
                {
                    //link user account with external login info
                    var result = await UserManager.AddLoginAsync(newUser.Id, new UserLoginInfo(externalProvider, externalUserID));
                    if (!result.Succeeded)
                    {
                        AddErrors(result);
                        UserClient.DeleteUser(SystemActors.SystemUserName, newUser.Id);
                        //return View();
                        newUserDeleted = true;
                    }
                }

                if (SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled))
                {
                    if (input.Items.ContainsKey(Fields.StripeToken) && !string.IsNullOrWhiteSpace(input.Items[Fields.StripeToken]))
                    {
                        try
                        {
                            this.SaveStripeCard(UserName, input.Items[Fields.StripeToken]);
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                            UserClient.DeleteUser(SystemActors.SystemUserName, newUser.Id);
                            //return View();
                            newUserDeleted = true;
                        }
                    }
                }

                /*
                //update any non-deferred, admin-only properties here, if needed
                var adminOnlyProps = UserClient.Properties(Strings.SystemActors.SystemUserName, UserName)
                    .Where(up => !up.Field.Deferred && up.Field.Mutability < CustomFieldAccess.Owner).ToList();
                UserInput input2 = new UserInput(Strings.SystemActors.SystemUserName, UserName,
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                foreach(var up in adminOnlyProps)
                {
                    switch(up.Field.Name)
                    {
                        case "MyAdminField1":
                            input2.Items.Add(up.Field.Name, "MyNewValue1");
                            break;
                        case "MyAdminField2":
                            input2.Items.Add(up.Field.Name, "MyNewValue2");
                            break;
                        default:
                            input2.Items.Add(up.Field.Name, up.Value);
                            break;
                    }
                }
                ValidateUserPropertyValues(adminOnlyProps, input2);
                UserClient.UpdateProperties(Strings.SystemActors.SystemUserName, UserName, adminOnlyProps, input2);
                */
                if (!newUserDeleted)
                {
                    if (SiteClient.BoolSetting(SiteProperties.UserApprovalRequired))
                    {
                        UserClient.SendNeedsAdminApprovalEmail(SystemActors.SystemUserName, UserName);
                    }
                    if (SiteClient.VerifyUserEmail)
                    {
                        PrepareSuccessMessage("RegisterSuccess_EmailVerificationNeeded", MessageType.Message);
                        var user = await UserManager.FindByNameAsync(UserName);
                        if (user != null)
                        {
                            //string verificationCode = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                            UserClient.SendUserVerificationEmail(SystemActors.SystemUserName, UserName);
                        }

                        return RedirectToAction(Strings.MVC.UserVerificationAction);
                    }
                    else
                    {
                        PrepareSuccessMessage("Register", MessageType.Method);
                        string returnUrl = Request.Cookies.AllKeys.Contains(Fields.ReturnUrl) ? Request.Cookies[Fields.ReturnUrl].Value : null;
                        return RedirectToAction(Strings.MVC.LogOnAction, new { returnUrl });
                    }
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in this.LocalizeCustomFieldValidationMessages(vfc.Detail.ValidationIssues))
                {
                    if (string.IsNullOrEmpty(issue.Key))
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
            }

            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.UserCustomFields] = UserClient.UserCustomFields.Where(
                ucf => !ucf.Deferred /* && ucf.Visibility >= CustomFieldAccess.Owner */ && ucf.Mutability >= CustomFieldAccess.Owner).ToList();

            ViewData[Fields.ExternalUserID] = input.Items.ContainsKey(Fields.ExternalUserID) ? input.Items[Fields.ExternalUserID] : null;
            ViewData[Fields.ExternalProvider] = input.Items.ContainsKey(Fields.ExternalProvider) ? input.Items[Fields.ExternalProvider] : null;

            return View();
        }

        #endregion

        #region Password Activities

        /// <summary>
        /// Displays forgot password form
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult ForgotPassword()
        {
            //if header sign in link is clicked on this view, return to "My Account" Summary page
            //TODO: the below doesn't work (HtmlHelper methods not available from here) so come up with a better generic url method...
            //string altLoginReturnUrl = htmlHelper.GetActionUrl(Strings.MVC.IndexAction, Strings.MVC.AccountController, null);
            //string altLoginReturnUrl = "/Account";
            //ViewData[RainWorx.FrameWorx.Strings.Fields.AltLogonReturnUrl] = altLoginReturnUrl;

            return View();
        }

        /// <summary>
        /// Processes forgot password request
        /// </summary>
        /// <param name="email">email address of the requesting user</param>
        /// <returns>View()</returns>
        [AcceptVerbs(HttpVerbs.Post)]
        public async Task<ActionResult> ForgotPassword(string email)
        {
            //if header sign in link is clicked on this view, return to "My Account" Summary page
            //TODO: the below doesn't work (HtmlHelper methods not available from here) so come up with a better generic url method...
            //string altLoginReturnUrl = htmlHelper.GetActionUrl(Strings.MVC.IndexAction, Strings.MVC.AccountController, null);
            //string altLoginReturnUrl = "/Account";
            //ViewData[RainWorx.FrameWorx.Strings.Fields.AltLogonReturnUrl] = altLoginReturnUrl;

            try
            {
                var user = await UserManager.FindByEmailAsync(email);
                if (user == null || (SiteClient.BoolSetting(SiteProperties.VerifyUserEmail) && !(await UserManager.IsEmailConfirmedAsync(user.Id))))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View();
                }
                //string passwordResetToken = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                UserClient.SendPasswordResetEmail(User.Identity.Name, email);//, passwordResetToken);
                PrepareSuccessMessage("ForgotPassword", MessageType.Method);
                return RedirectToAction("ResetPassword", new { email });
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                PrepareErrorMessage(iafc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("ForgotPassword", MessageType.Method);
            }

            return View();
        }

        /// <summary>
        /// Displays the reset password form
        /// Processes a reset password request
        /// </summary>
        /// <param name="email">email address of the requesting user</param>
        /// <param name="resetToken">password reset token</param>
        /// <param name="Password">new password requested by user</param>
        /// <param name="confirmPassword">confirmation of new password, must match "Password"</param>
        /// <returns>Redirect to login view (success) or View(), otherwise</returns>
        //[AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public async Task<ActionResult> ResetPassword(string email, string resetToken, string Password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(resetToken) || string.IsNullOrEmpty(Password))
            {
                return View();
            }
            else if (!String.Equals(Password, confirmPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError("_FORM", "ConfirmationNewPasswordMismatch");
                return View();
            }
            else
            {
                try
                {
                    //UserClient.ResetPassword(User.Identity.Name, email, resetToken, Password);
                    var user = await UserManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        // Don't reveal that the user does not exist
                        return View();
                    }
                    UserClient.Reset();
                    var rwUser = UserClient.GetUserByID(SystemActors.SystemUserName, user.Id);
                    if (rwUser.PasswordResetToken != resetToken.Trim())
                    {
                        ModelState.AddModelError("resetToken", this.GlobalResourceString("ResetTokenIncorrect"));
                        return View();
                    }
                    //var result = await UserManager.ResetPasswordAsync(user.Id, resetToken, Password);
                    var result = await UserManager.RemovePasswordAsync(user.Id);
                    var result2 = await UserManager.AddPasswordAsync(user.Id, Password);
                    if (!result2.Succeeded)
                    {
                        AddErrors(result2);
                    }
                    else
                    {
                        PrepareSuccessMessage("ResetPassword", MessageType.Method);
                        return RedirectToAction("LogOn");
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
                catch (FaultException<InvalidArgumentFaultContract> iafc)
                {
                    PrepareErrorMessage(iafc.Detail.Reason);
                }
                catch (FaultException<InvalidOperationFaultContract> iofc)
                {
                    PrepareErrorMessage(iofc.Detail.Reason);
                }
                return View();
            }
        }

        /// <summary>
        /// Displays form for user email verification
        /// Processes request for user email verification
        /// </summary>
        /// <param name="username">requesting user's username</param>
        /// <param name="verificationCode">email verification code sent at registration</param>
        /// <returns></returns>
        //[AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult UserVerification(string username, string verificationCode/*, FormCollection values*/)
        {
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(verificationCode))
            {
                return View();
            }
            else
            {
                try
                {
                    ReasonCode reason;
                    if (UserClient.VerifyUser(username.Trim(), verificationCode.Trim(), out reason))
                    {
                        //valid
                        PrepareSuccessMessage("UserVerification", MessageType.Method);
                        string returnUrl = Request.Cookies.AllKeys.Contains(Fields.ReturnUrl) ? Request.Cookies[Fields.ReturnUrl].Value : null;
                        return RedirectToAction(Strings.MVC.LogOnAction, Strings.MVC.AccountController, new { returnUrl });
                    }
                    else
                    {
                        PrepareErrorMessage(reason);
                        return View();
                    }
                }
                catch
                {
                    PrepareErrorMessage("UserVerification", MessageType.Method);
                    return View();
                }
            }
        }

        /// <summary>
        /// Retrieves the ID of the impersonated user if applicable, otherwise the ID of the currently authenticated user
        /// </summary>
        private int FBOUserID()
        {
            if (User.Identity.IsAuthenticated)
            {
                if (User.Identity.Name.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase))
                {
                    return User.Identity.GetUserId<int>();
                }
                else
                {
                    //impersonating -- use impersonated User's ID, not admin's ID
                    var impersonatedUser = UserClient.GetUserByUserName(SystemActors.SystemUserName, this.FBOUserName());
                    return impersonatedUser.ID;
                }
            }
            return 0;
        }

        /// <summary>
        /// Displays change password form
        /// </summary>
        /// <returns>View()</returns>
        [Authorize]
        public async Task<ActionResult> ChangePassword()
        {
            //get list of external providers associated with this account
            int userId = FBOUserID();
            var userLogins = await UserManager.GetLoginsAsync(userId);
            ViewData["UserLogins"] = userLogins;
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewData["OtherLogins"] = otherLogins;

            return View();
        }

        /// <summary>
        /// Processes request to change password
        /// </summary>
        /// <param name="currentPassword">user's current password</param>
        /// <param name="Password">requested new password</param>
        /// <param name="confirmPassword">requested password confimation</param>
        /// <returns>Redirect to Account homepage (success) or View(), otherwise</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Exceptions result in password not being changed.")]
        public async Task<ActionResult> ChangePassword(string currentPassword, string Password, string confirmPassword)
        {
            try
            {
                int userId = FBOUserID();
                //get list of external providers associated with this account
                var userLogins = await UserManager.GetLoginsAsync(userId);
                ViewData["UserLogins"] = userLogins;
                var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
                ViewData["OtherLogins"] = otherLogins;

                //when in demo mode, don't allow password changes for built-in users
                string[] demoUsers = { "admin", "JohnBuyer", "JoeSeller", "Admin", "AuctionBob", "BidderBill", "SusanSales", "WallyAPI",
                                       "SpringfieldEstates", "DeLongWarehouse", "OnlyMusic", "SurplusMachines", "JCassisAntiques",
                                       "CollectArt", "Gallery9", "TheKarolFamily", "CollectCards", "XLMachines", "Seller1" };
                bool isDemoRestrictedUser = SiteClient.DemoEnabled && demoUsers.Contains(this.FBOUserName());
                if (isDemoRestrictedUser)
                {
                    PrepareErrorMessage("ChangePassword", MessageType.Method);
                    return View();
                }

                IdentityResult result;
                if (await UserManager.HasPasswordAsync(userId))
                {
                    result = await UserManager.ChangePasswordAsync(userId, currentPassword, Password);
                }
                else
                {
                    result = await UserManager.AddPasswordAsync(userId, Password);
                }
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }
                    //return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
                    PrepareSuccessMessage("ChangePassword", MessageType.Method);
                    return RedirectToAction(Strings.MVC.IndexAction);
                }
                AddErrors(result);
                return View();

            }
            catch
            {
                PrepareErrorMessage("ChangePassword", MessageType.Method);
                return View();
            }
        }

        #endregion

        #region Feedback

        /// <summary>
        /// Displays form to submit feedback about a completed listing action
        /// </summary>
        /// <param name="ListingID">ID of the related listing</param>
        /// <param name="Sender">username of the user submitting feedback</param>
        /// <param name="Receiver">username of the related user</param>
        /// <param name="returnUrl">Return Url passed to feedbacks view</param>
        /// <returns>View()</returns>
        [Authorize]
        public ActionResult SubmitFeedback(int ListingID, string Sender, string Receiver, string returnUrl)
        {
            ViewData[Strings.Fields.ListingID] = ListingID;
            ViewData[Strings.Fields.Sender] = Sender;
            ViewData[Strings.Fields.Receiver] = Receiver;
            ViewData[Strings.Fields.ReturnUrl] = returnUrl ??
                Url.Action(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id = ListingID });

            Listing listing;
            try
            {
                string fillLevel = Strings.ListingFillLevels.LotEvent;
                listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, ListingID, fillLevel);
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                //handle the "Listing doesn't exist" error by returning a 404, otherwise re-throw the exception to be handled with generic error handler
                if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                return new HttpNotFoundResult(this.GlobalResourceString("ListingNotFound"));
            }

            if (SiteClient.EnableEvents && listing.Lot != null)
            {
                ViewData[Strings.MVC.ListingName] = string.Format(Strings.Formats.ListingName, listing.Title, listing.Lot.LotNumber);
            }
            else
            {
                ViewData[Strings.MVC.ListingName] = string.Format(Strings.Formats.ListingName, listing.Title, listing.ID);
            }

            //CheckValidationIssues();

            return View();
        }

        /// <summary>
        /// Processes request to submit feedback about a completed listing action
        /// </summary>
        /// <param name="ListingID">ID of the related listing</param>
        /// <param name="Sender">username of the user submitting feedback</param>
        /// <param name="Receiver">username of the related user</param>
        /// <param name="Rating">feedback rating (integer 1-5)</param>
        /// <param name="Comment">feedback comment</param>
        /// <param name="returnUrl">Url to redirect to (default listing detail view if missing)</param>
        /// <returns>Redirect to "returnUrl" (success) or Redirect to submit feedback form (errors)</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SubmitFeedback(int ListingID, string Sender, string Receiver, string Rating, string Comment, string returnUrl)
        {
            //capture user input
            UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
            userInput.AddAllFormValues(this);

            //do call to BLL
            try
            {
                UserClient.AddFeedback(User.Identity.Name, this.FBOUserName(), userInput);
                PrepareSuccessMessage("SubmitFeedback", MessageType.Method);
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return Redirect(Url.Action(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id = ListingID }));
                }
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                //StoreValidationIssues(vfc.Detail.ValidationIssues, userInput);
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
                return SubmitFeedback(ListingID, Sender, Receiver, returnUrl);
            }
            catch (FaultException<InvalidArgumentFaultContract> iafc)
            {
                PrepareErrorMessage(iafc.Detail.Reason);
            }
            catch
            {
                PrepareErrorMessage("SubmitFeedback", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.SubmitFeedbackAction, new { ListingID, Sender, Receiver, returnUrl });
        }

        /// <summary>
        /// Displays this user's feedback summary
        /// </summary>
        /// <returns>View(FeedbackRating)</returns>
        [Authorize]
        public ActionResult Feedback()
        {
            FeedbackRating retVal = UserClient.GetFeedbackRating(User.Identity.Name, this.FBOUserName(), false);
            return View(retVal);
        }

        /// <summary>
        /// Displays this user's feedback as a seller
        /// </summary>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [Authorize]
        public ActionResult SellerFeedback(string sort, int? page, bool? descending)
        {
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
            return View(UserClient.GetSellerFeedback(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, sort ?? "DateStamp", descending ?? true));
        }

        /// <summary>
        /// Displays this user's feedback as a buyer
        /// </summary>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [Authorize]
        public ActionResult BuyerFeedback(string sort, int? page, bool? descending)
        {
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
            return View(UserClient.GetBuyerFeedback(User.Identity.Name, this.FBOUserName(),
                page == null ? 0 : (int)page, SiteClient.PageSize, sort ?? "DateStamp", descending ?? true));
        }

        /// <summary>
        /// Displays the specified user's feedback received from others
        /// </summary>
        /// <param name="userid">ID of the requested user</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [GoUnsecure]
        public ActionResult ViewFeedback(int userid, int? months, string sort, int? page, bool? descending)
        {
            User targetUser = null;
            try
            {
                targetUser = UserClient.GetUserByID(User.Identity.Name, userid);
            }
            catch (Exception)
            {
                //user not found - allow view to handle this gracefully
            }
            if (targetUser != null)
            {
                ViewData["TargetUser"] = targetUser;
                ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
                ViewData["FeedbackRating"] = UserClient.GetFeedbackRating(User.Identity.Name, targetUser.UserName, false);
                //ensure the # of months is exactly null, 1, 6, or 12, otherwise set it to null
                int? safeMonths = (months == null || months == 1 || months == 6 || months == 12) ? months : null;
                ViewData["Months"] = safeMonths ?? 0;
                //ensure sort value is null, "DateStamp" or "Rating", otherwise set to "DateStamp"
                string safeSort = (sort == "DateStamp" || sort == "Rating") ? sort : "DateStamp";
                ViewData["Sort"] = safeSort;
                //ensure page is 0 if null
                int safePage = (page != null ? (int)page : 0);
                int pageSize = SiteClient.PageSize;
                bool safeDescending = (descending != null ? (bool)descending : true);
                ViewData["FeedbackList"] = UserClient.GetAllFeedbackToUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, safeSort, safeDescending);
            }
            return View(targetUser);
        }

        /// <summary>
        /// Displays the specified user's feedback received as a seller
        /// </summary>
        /// <param name="userid">ID of the requested user</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [GoUnsecure]
        public ActionResult ViewSellerFeedback(int userid, int? months, string sort, int? page, bool? descending)
        {
            User targetUser = null;
            try
            {
                targetUser = UserClient.GetUserByID(User.Identity.Name, userid);
            }
            catch (Exception)
            {
                //user not found - allow view to handle this gracefully
            }
            if (targetUser != null)
            {
                ViewData["TargetUser"] = targetUser;
                ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
                ViewData["FeedbackRating"] = UserClient.GetFeedbackRating(User.Identity.Name, targetUser.UserName, false);
                //ensure the # of months is exactly null, 1, 6, or 12, otherwise set it to null
                int? safeMonths = (months == null || months == 1 || months == 6 || months == 12) ? months : null;
                ViewData["Months"] = safeMonths ?? 0;
                //ensure page is 0 if null
                int safePage = (page != null ? (int)page : 0);
                int pageSize = SiteClient.PageSize;
                bool safeDescending = (descending != null ? (bool)descending : true);
                ViewData["FeedbackList"] = UserClient.GetSellerFeedbackToUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, sort ?? "DateStamp", safeDescending);
            }
            return View(targetUser);
        }

        /// <summary>
        /// Displays the specified user's feedback received as a buyer
        /// </summary>
        /// <param name="userid">ID of the requested user</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [GoUnsecure]
        public ActionResult ViewBuyerFeedback(int userid, int? months, string sort, int? page, bool? descending)
        {
            User targetUser = null;
            try
            {
                targetUser = UserClient.GetUserByID(User.Identity.Name, userid);
            }
            catch (Exception)
            {
                //user not found - allow view to handle this gracefully
            }
            if (targetUser != null)
            {
                ViewData["TargetUser"] = targetUser;
                ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
                ViewData["FeedbackRating"] = UserClient.GetFeedbackRating(User.Identity.Name, targetUser.UserName, false);
                //ensure the # of months is exactly null, 1, 6, or 12, otherwise set it to null
                int? safeMonths = (months == null || months == 1 || months == 6 || months == 12) ? months : null;
                ViewData["Months"] = safeMonths ?? 0;
                //ensure page is 0 if null
                int safePage = (page != null ? (int)page : 0);
                int pageSize = SiteClient.PageSize;
                bool safeDescending = (descending != null ? (bool)descending : true);
                ViewData["FeedbackList"] = UserClient.GetBuyerFeedbackToUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, sort ?? "DateStamp", safeDescending);
            }
            return View(targetUser);
        }

        /// <summary>
        /// Displays the specified user's feedback left for other users
        /// </summary>
        /// <param name="userid">ID of the requested user</param>
        /// <param name="months">the requested number of months (1, 6, or 12) of feedback data to retrieve</param>
        /// <param name="sort">field name to order results by</param>
        /// <param name="page">index of the page to be displayed (default 0)</param>
        /// <param name="descending">order results in ascending or descending order (default false / ascending)</param>
        /// <returns>View(Page&lt;Feedback%gt;)</returns>
        [GoUnsecure]
        public ActionResult ViewFeedbackForOthers(int userid, int? months, string sort, int? page, bool? descending)
        {
            User targetUser = null;
            try
            {
                targetUser = UserClient.GetUserByID(User.Identity.Name, userid);
            }
            catch (Exception)
            {
                //user not found - allow view to handle this gracefully
            }
            if (targetUser != null)
            {
                ViewData["TargetUser"] = targetUser;
                ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? true;
                ViewData["FeedbackRating"] = UserClient.GetFeedbackRating(User.Identity.Name, targetUser.UserName, false);
                //ensure the # of months is exactly null, 1, 6, or 12, otherwise set it to null
                int? safeMonths = (months == null || months == 1 || months == 6 || months == 12) ? months : null;
                ViewData["Months"] = safeMonths ?? 0;
                //ensure sort value is null, "DateStamp" or "Rating", otherwise set to "DateStamp"
                string safeSort = (sort == "DateStamp" || sort == "Rating") ? sort : "DateStamp";
                ViewData["Sort"] = safeSort;
                //ensure page is 0 if null
                int safePage = (page != null ? (int)page : 0);
                int pageSize = SiteClient.PageSize;
                bool safeDescending = (descending != null ? (bool)descending : true);
                ViewData["FeedbackList"] = UserClient.GetAllFeedbackFromUser(User.Identity.Name, targetUser.UserName,
                    safeMonths, safePage, pageSize, safeSort, safeDescending);
            }
            return View(targetUser);
        }

        #endregion

        #region Account Settings

        /// <summary>
        /// Displays a form to edit this user's Newsletter property
        /// Also displays some other read-only user properties
        /// </summary>
        /// <returns>View(User)</returns>
        [Authorize]
        public ActionResult AccountSettings()
        {
            string actingUN = User.Identity.Name; // username of logged in user
            string targetUN = this.FBOUserName();
            User targetUser = UserClient.GetUserByUserName(actingUN, targetUN);
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? FieldDefaults.Culture; // culture, e.g. "en-US"
            CultureInfo cultureInfo = new CultureInfo(cultureCode);

            //retrieve the list of user properties
            Category userFieldsCategory = CommonClient.GetCategoryByID(38);
            List<CustomProperty> properties =
                UserClient.Properties(User.Identity.Name, this.FBOUserName()).WhereContainsFields(
                    userFieldsCategory.CustomFieldIDs).OrderBy(p => p.Field.DisplayOrder).ToList();

            List<CustomProperty> editProperties = PruneUserCustomFieldsForEdit(properties, targetUN);
            List<CustomProperty> viewProperties = PruneUserCustomFieldsForVisbilityOnly(properties, targetUN);

            ViewData["UserAccountProperties"] = editProperties;
            ViewData["UserAccountViewProperties"] = viewProperties;

            //add a modelstate for each property in this collection
            foreach (CustomProperty property in editProperties)
            {
                string key = property.Field.Name;
                string val = string.Empty;
                switch (property.Field.Type)
                {
                    case CustomFieldType.Boolean:
                        bool tempBool;
                        if (bool.TryParse(property.Value, out tempBool))
                        {
                            val = tempBool.ToString(cultureInfo);
                        }
                        break;
                    case CustomFieldType.DateTime:
                        DateTime tempDate;
                        if (DateTime.TryParse(property.Value, out tempDate))
                        {
                            val = tempDate.ToString(cultureInfo);
                        }
                        break;
                    case CustomFieldType.Decimal:
                        decimal tempDecimal;
                        if (decimal.TryParse(property.Value, out tempDecimal))
                        {
                            val = tempDecimal.ToString(Strings.Formats.Decimal, cultureInfo);
                        }
                        break;
                    case CustomFieldType.Int:
                        int tempInt;
                        if (int.TryParse(property.Value, out tempInt))
                        {
                            val = tempInt.ToString(cultureInfo);
                        }
                        break;
                    default:
                        val = property.Value;
                        break;
                }

                if (!ModelState.ContainsKey(key))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(val, val, null);
                    ModelState.Add(key, ms);
                }
            }

            return View(targetUser);
        }

        /// <summary>
        /// Processes request to change Newsletter preference
        /// </summary>
        /// <param name="newsletter">prequested Newsletter preference (true/false)</param>
        /// <param name="email">new email address</param>
        /// <returns>View(User)</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult AccountSettings(bool newsletter, string email)
        {
            try
            {
                string actingUN = User.Identity.Name; // username of logged in user
                string targetUN = this.FBOUserName();
                User targetUser = UserClient.GetUserByUserName(actingUN, targetUN);
                string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? FieldDefaults.Culture; // culture, e.g. "en-US"
                UserInput input = new UserInput(actingUN, targetUN, cultureCode, cultureCode);
                input.Items.Add(Fields.Id, targetUser.ID.ToString());

                input.AddAllFormValues(this);

                //retrieve the list of user properties
                Category userFieldsCategory = CommonClient.GetCategoryByID(38);
                List<CustomProperty> properties =
                    UserClient.Properties(User.Identity.Name, this.FBOUserName()).WhereContainsFields(
                        userFieldsCategory.CustomFieldIDs);

                var propertiesForEdit = PruneUserCustomFieldsForEdit(properties, targetUN);
                ViewData["UserAccountProperties"] = propertiesForEdit;
                ViewData["UserAccountViewProperties"] = PruneUserCustomFieldsForVisbilityOnly(properties, targetUN);

                //do call to BLL
                try
                {
                    ValidateUserPropertyValues(propertiesForEdit, input);

                    UserClient.UpdateAllUserDetails(User.Identity.Name, input);

                    // this checks if the user is attempting to change their email address
                    if (targetUser.Email != email && targetUser.Roles.Count(r => r.Name == "Admin") <= 0)
                    {
                        // checks if VerifyUserEmail is set to true
                        if (SiteClient.VerifyUserEmail)
                        {

                            // logs the user out
                            targetUser.IsVerified = false;
                            //FormsAuth.SignOut();
                            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

                            // redirects the user to the verification page
                            return RedirectToAction(Strings.MVC.UserVerificationAction);

                        }
                    }

                    //reload updated properties
                    targetUser.Properties = UserClient.Properties(SystemActors.SystemUserName, targetUN);
                    properties = targetUser.Properties.WhereContainsFields(userFieldsCategory.CustomFieldIDs);
                    ViewData["UserAccountViewProperties"] = PruneUserCustomFieldsForVisbilityOnly(properties, targetUN);
                    ViewData["UserAccountProperties"] = PruneUserCustomFieldsForEdit(properties, targetUN);

                    PrepareSuccessMessage("AccountSettings", MessageType.Method);
                    return (View(targetUser));
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
                    PrepareErrorMessage(iofc.Detail.Reason);
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                    PrepareErrorMessage("AccountSettings", MessageType.Method);
                }

                return View(UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName()));
            }
            catch
            {
                PrepareErrorMessage("AccountSettings", MessageType.Method);
                return View(UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName()));
            }
        }

        #endregion

        #region Credit Cards

        /// <summary>
        /// Displays a list of this user's credit cards on file
        /// </summary>
        /// <returns>View(List&lt;CreditCardWithBillingAddress&gt;</returns>
        [Authorize]
        public ActionResult CreditCards()
        {
            User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            ViewData[Strings.MVC.ViewData_BillingCreditCardId] = currentUser.BillingCreditCardID;
            List<CreditCard> dtoCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
            List<Address> dtoAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            List<CreditCardWithBillingAddress> cards = new List<CreditCardWithBillingAddress>();
            foreach (CreditCard creditCard in dtoCards)
            {
                Address billingAddress = dtoAddresses.Where(a => a.ID == creditCard.AddressID).SingleOrDefault();
                cards.Add(new CreditCardWithBillingAddress(creditCard, billingAddress));
            }
            return View(cards);
        }

        /// <summary>
        /// Displays a list of this user's credit cards on file
        /// </summary>
        /// <returns>View(List&lt;CreditCardWithBillingAddress&gt;</returns>
        [Authorize]
        public ActionResult StripeCardManagement()
        {
            //User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            //ViewData[Strings.MVC.ViewData_BillingCreditCardId] = currentUser.BillingCreditCardID;
            //List<CreditCard> dtoCards = UserClient.GetCreditCards(User.Identity.Name, this.FBOUserName());
            //List<Address> dtoAddresses = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            //List<CreditCardWithBillingAddress> cards = new List<CreditCardWithBillingAddress>();
            //foreach (CreditCard creditCard in dtoCards)
            //{
            //    Address billingAddress = dtoAddresses.Where(a => a.ID == creditCard.AddressID).SingleOrDefault();
            //    cards.Add(new CreditCardWithBillingAddress(creditCard, billingAddress));
            //}
            var cards = this.GetAllStripeCards();
            return View(cards);
        }

        /// <summary>
        /// Redirects to the appropriate payment-gateway-specific form for adding a new credit card
        /// </summary>
        /// <param name="SellerID">the payment recipient user's ID, or null for site fees</param>
        /// <param name="ListingID">the ID of the listing that would be paid, or null for site fees</param>
        /// <param name="returnUrl">the url to redirect to upon success</param>
        [Authorize]
        public ActionResult AddCard(int? SellerID, int? ListingID, string returnUrl)
        {
            if (SellerID == null && ListingID.HasValue)
            {
                var listing = ListingClient.GetListingByIDWithFillLevel(SystemActors.SystemUserName, ListingID.Value, ListingFillLevels.Owner);
                SellerID = listing.Owner.ID;
            }
            switch (AccountingClient.GetSaleBatchPaymentProviderName())
            {
                case "StripeConnect":
                    if (SiteClient.BoolSetting(SiteProperties.StripeConnect_EnabledForSellers))
                        return RedirectToAction(Strings.MVC.AddStripeCardAction, new { SellerID, returnUrl });
                    else
                        return RedirectToAction(Strings.MVC.AddStripeCardAction, new { returnUrl });
                default:
                    return RedirectToAction(Strings.MVC.AddCreditCardAction, new { returnUrl });
            }
        }

        /// <summary>
        /// Displays form to add new stripe card
        /// </summary>
        /// <param name="SellerID">The integer User ID of the seller, or null to associate this card with the site fee stripe credentials</param>
        /// <param name="returnUrl">the url to redirect to upon success</param>
        /// <returns></returns>
        [Authorize]
        public ActionResult AddStripeCard(int? SellerID, string returnUrl)
        {
            User currentUser = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            ViewData[Strings.MVC.ViewData_BillingCreditCardId] = currentUser.BillingCreditCardID;
            string publicApiKey;
            if (SellerID == null || SellerID.Value == 0)
            {
                publicApiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesPublishableApiKey);
            }
            else
            {
                var seller = UserClient.GetUserByID(SystemActors.SystemUserName, SellerID.Value);
                publicApiKey = seller.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerPublishableApiKey).Value;
            }
            ViewData["SellerID"] = SellerID ?? 0;
            ViewData["PublishableApiKey"] = publicApiKey;
            ViewData["returnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : this.GetActionUrl(Strings.MVC.StripeCardManagementAction);
            return View();
        }

        /// <summary>
        /// Displays form to add a credit card
        /// Processes request to add a credit card
        /// </summary>
        /// <returns>Redirect to "Credit Cards" view (success), View() otherwise</returns>
        [Authorize]
        public ActionResult AddCreditCard(string returnUrl)
        {
            if (Request.Form[Strings.MVC.SubmitAction_Save] != null)
            {
                //capture user input
                UserInput userInput = new UserInput(User.Identity.Name, this.FBOUserName(),
                    this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                userInput.AddAllFormValues(this);

                //do call to BLL
                try
                {
                    UserClient.AddCreditCard(User.Identity.Name, this.FBOUserName(), userInput);
                    PrepareSuccessMessage("AddCreditCard", MessageType.Method);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction(Strings.MVC.CreditCardsAction);
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
                }
            }

            User temp = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
            ViewData["PrimaryAddressID"] = temp.PrimaryAddressID;
            ViewData[Strings.MVC.ViewData_AddressList] = UserClient.GetAddresses(User.Identity.Name, this.FBOUserName());
            ViewData[Strings.Fields.CreditCardTypes] = new SelectList(
                SiteClient.CreditCardTypes.Where(cct => cct.Enabled), Strings.Fields.ID, Strings.Fields.Name);
            ViewData[Strings.Fields.Country] = new SelectList(this.Countries(), Strings.Fields.ID, Strings.Fields.Name);
            return View();
        }

        /// <summary>
        /// Processes request to delete credit card
        /// </summary>
        /// <param name="id">ID of credit card record to be deleted</param>
        /// <returns>Redirect to credit cards view</returns>
        [Authorize]
        public ActionResult DeleteCreditCard(int id)
        {
            try
            {
                UserClient.DeleteCreditCard(User.Identity.Name, this.FBOUserName(), id);
                PrepareSuccessMessage("DeleteCreditCard", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteCreditCard", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CreditCardsAction);
        }

        /// <summary>
        /// Processes request to set credit card as default
        /// </summary>
        /// <param name="id">ID of credit card record to be updated</param>
        /// <returns>Redirect to credit cards view</returns>
        [Authorize]
        public ActionResult SetDefaultCreditCard(int id)
        {
            try
            {
                UserClient.SetBillingCreditCard(User.Identity.Name, this.FBOUserName(), id);
                PrepareSuccessMessage("SetDefaultCreditCard", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("SetDefaultCreditCard", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.CreditCardsAction);
        }

        #endregion

        #region User Messaging

        /// <summary>
        /// Displays a list of messages eith to or from this user (not both)
        /// </summary>
        /// <param name="incoming">true or null displays incoming messages, otherwise outgoing messages are displayed</param>
        /// <param name="pageIndex">index of the page to be displayed (default 0)</param>
        /// <param name="sort">field name to order results by (default "Sent")</param>
        /// <param name="descending">order results in ascending or descending order (default true / descending)</param>
        /// <returns>View(Page&lt;UserMessage&gt;)</returns>
        [Authorize]
        public ActionResult ViewMessages(bool? incoming, int? pageIndex, string sort, bool? descending)
        {
            const int PageSize = 0;
            Page<UserMessage> messages;
            //default sort: sent, descending
            if (string.IsNullOrEmpty(sort))
            {
                sort = Strings.Fields.Sent;
                if (!descending.HasValue)
                {
                    descending = true;
                }
            }
            if (incoming ?? true)
            {
                ViewData[Strings.MVC.ViewData_UserMessages_Incoming] = true;
                messages = UserClient.GetIncomingMessages(User.Identity.Name, this.FBOUserName(), pageIndex ?? 0, PageSize, sort, descending ?? false);
            }
            else
            {
                ViewData[Strings.MVC.ViewData_UserMessages_Incoming] = false;
                messages = UserClient.GetOutgoingMessages(User.Identity.Name, this.FBOUserName(), pageIndex ?? 0, PageSize, sort, descending ?? false);
            }
            ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? false;
            ViewData[Strings.MVC.ViewData_SortField] = sort;
            return View(messages);
        }

        /// <summary>
        /// Displays the specified message
        /// </summary>
        /// <param name="id">ID of the requested message</param>
        /// <param name="returnUrl">Url assigned to the "back" button displayed on the read message view</param>
        /// <returns>View(UserMessage)</returns>
        [Authorize]
        public ActionResult ReadMessage(int id, string returnUrl)
        {
            //ViewData[Strings.MVC.ViewData_SortDescending] = descending ?? false;
            //ViewData[Strings.MVC.ViewData_SortField] = sort;
            ViewData[Fields.ReturnUrl] = returnUrl;
            //ViewData[Strings.MVC.ViewData_UserMessages_Incoming] = incoming ?? true;

            //using the admin username who is doing the impoersonating fails to set the 'message read' status, so use FBOUserName as the 'acting user' to set it as read
            UserMessage message = UserClient.ReadMessage(this.FBOUserName(), id); // (User.Identity.Name, id);

            ViewData["Subject"] = this.GlobalResourceString("Message_Subject_Reply_Prefix"/*"RE: "*/) + message.Subject;
            string replyBodyPrefix = this.GlobalResourceString("Message_Body_Reply_Prefix"/*----------------Original Message----------------"*/).Replace(@"\n", "\n");
            string body = replyBodyPrefix + message.Body;
            body = body.Replace("<br/>", "\r\n");
            ViewData["Body"] = body;
            return View(Strings.MVC.ReadMessageAction, message);
        }

        /// <summary>
        /// Processes a request to delete the specified message
        /// </summary>
        /// <param name="id">ID of the requested message to be deleted</param>
        /// <param name="returnUrl">return url (default view all messages view if missing)</param>
        /// <returns>Redirect to "returnUrl"</returns>
        [Authorize]
        public ActionResult DeleteMessage(int id, string returnUrl)
        {
            try
            {
                UserClient.DeleteMessage(User.Identity.Name, id, this.FBOUserName());
                PrepareSuccessMessage("DeleteMessage", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("DeleteMessage", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ViewMessagesAction);
        }

        /// <summary>
        /// Displays form to send a message to another user about a specific listing
        /// </summary>
        /// <param name="receiver">username of recipient</param>
        /// <param name="template">name of the template to be used for this message</param>
        /// <param name="listingID">ID of the listing specified as the subject of this message</param>
        /// <param name="returnUrl">Url assigned to the "back" button displayed on the read message view</param>
        /// <returns>View(Listing)</returns>
        [Authorize]
        public ActionResult SendListingMessage(string receiver, string template, int listingID, string returnUrl)
        {
            ViewData[Fields.Sender] = this.FBOUserName();
            ViewData[Fields.Receiver] = receiver;
            ViewData[Fields.Template] = template;
            ViewData[Fields.ReturnUrl] = returnUrl;

            string fillLevel = Strings.ListingFillLevels.LotEvent;
            var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, listingID, fillLevel);
            return View(Strings.MVC.SendListingMessageAction, listing);
        }

        /// <summary>
        /// Displays form to send a message to another user
        /// </summary>
        /// <param name="receiver">username of recipient</param>
        /// <param name="template">name of the template to be used for this message</param>
        /// <param name="returnUrl">Url assigned to the "back" button displayed on the read message view</param>
        /// <returns>View(Listing)</returns>
        [Authorize]
        public ActionResult SendUserMessage(string receiver, string template, string returnUrl)
        {
            ViewData[Fields.Sender] = this.FBOUserName();
            ViewData[Fields.Receiver] = receiver;
            ViewData[Fields.Template] = template;
            ViewData[Fields.ReturnUrl] = returnUrl;

            return View(Strings.MVC.SendUserMessageAction);
        }

        /// <summary>
        /// Porcesses request to send a message to another user about a specific listing
        /// </summary>
        /// <param name="receiver">username of recipient</param>
        /// <param name="template">name of the template to be used for this message</param>
        /// <param name="listingID">ID of the listing specified as the subject of this message</param>
        /// <param name="Subject">message subject text entered by user</param>
        /// <param name="Body">message body text entered by user</param>
        /// <param name="returnUrl">return url (default listing detail view if missing)</param>
        /// <param name="masterMessageID">the id of the primary related message</param>
        /// <returns>Redirect to "returnUrl" (success) or View&lt;Listing&gt; (error)</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SendListingMessage(string receiver, string template, int listingID, string Subject, string Body, string returnUrl, int? masterMessageID)
        {
            try
            {
                //do call to BLL
                try
                {
                    Body = Body.Replace("\r\n", "<br/>");

                    //send message
                    UserClient.SendUserMessage(User.Identity.Name, this.FBOUserName(), receiver, Subject, Body,
                                               listingID, masterMessageID);

                    //send email notification
                    //TODO: make sure this works to specifications - might need to change sender to be more "like facebook messaging"
                    NotifierClient.QueueNotification(User.Identity.Name, this.FBOUserName(), receiver, template,
                                                     DetailTypes.Listing, listingID, Body, null, null, null, null);

                    PrepareSuccessMessage("SendListingMessage", MessageType.Method);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.ListingController, new { id = listingID });
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
            catch (Exception)
            {
                PrepareErrorMessage("SendListingMessage", MessageType.Method);
            }

            ViewData[Fields.Sender] = User.Identity.Name;
            ViewData[Fields.Receiver] = receiver;
            ViewData[Fields.Template] = template;
            ViewData[Fields.ReturnUrl] = returnUrl;

            Listing listing = null;
            try
            {
                string fillLevel = Strings.ListingFillLevels.LotEvent;
                listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, listingID, fillLevel);
            }
            catch
            {
                //ignore this error -- listing may have been deleted
            }

            if (listing != null)
            {
                return View(Strings.MVC.SendListingMessageAction, listing);
            }
            else
            {
                return View(Strings.MVC.SendUserMessageAction, listing);
            }

        }

        /// <summary>
        /// Processes a request to send a non-listing message
        /// </summary>
        /// <param name="receiver">username of recipient</param>
        /// <param name="template">name of the template to be used for this message</param>
        /// <param name="listingID">ID of the listing specified as the subject of this message</param>
        /// <param name="Subject">message subject text entered by user</param>
        /// <param name="Body">message body text entered by user</param>
        /// <param name="returnUrl">return url (default all messages view if missing)</param>
        /// <param name="masterMessageID">the id of the primary related message</param>
        /// <returns>Redirect to "returnUrl" (success) or all messages view if missing or on error</returns>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SendMessage(string receiver, string template, int? listingID, string Subject, string Body, string returnUrl, int? masterMessageID)
        {
            try
            {
                //do call to BLL
                try
                {
                    //send message
                    int newUserMessageId = UserClient.SendUserMessage(User.Identity.Name, User.Identity.Name, receiver, Subject, Body,
                                               listingID, masterMessageID);

                    //send email notification
                    //TODO: make sure this works to specifications - might need to change sender to be more "like facebook messaging"
                    NotifierClient.QueueNotification(User.Identity.Name, User.Identity.Name, receiver, template,
                                                     DetailTypes.UserMessage, newUserMessageId, Body, null, null, null, null);

                    PrepareSuccessMessage("SendMessage", MessageType.Method);
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                }
                catch (FaultException<ValidationFaultContract> vfc)
                {
                    //display validation errors                
                    foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                    {
                        ModelState.AddModelError(issue.Key, issue.Message);
                    }
                    if (listingID.HasValue)
                    {
                        return SendListingMessage(receiver, template, listingID.Value, returnUrl);
                    }
                    else
                    {
                        return SendUserMessage(receiver, template, returnUrl);
                    }
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                }
            }
            catch
            {
                PrepareErrorMessage("SendMessage", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ViewMessagesAction);
        }

        /// <summary>
        /// Send a "report abuse" email to the website owner regarding a specific message
        /// </summary>
        /// <param name="id">The message id being reported</param>
        /// <returns>Redirect to all messages view</returns>
        [Authorize]
        public ActionResult ReportAbuse(int id)
        {
            try
            {
                NotifierClient.QueueNotification(User.Identity.Name, User.Identity.Name,
                                                 null,
                                                 "report_messaging_abuse", DetailTypes.UserMessage, id, string.Empty, null, null, null, null);
                PrepareSuccessMessage("ReportAbuse", MessageType.Method);
            }
            catch
            {
                PrepareErrorMessage("ReportAbuse", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ViewMessagesAction, new { incoming = true });
        }

        #endregion

        #region Reports

        /// <summary>
        /// Gets a list of all events owned by the specified user, ordered as follows:
        /// FIRST: a dummy ID=-1 event with Title &quot;All&quot;
        /// NEXT: Status:Closed events, ended most recently at the top;
        /// NEXT: non-draft, non-archived events, starting soonest at the top;
        /// NEXT: draft events, starting soonest at the top;
        /// FINALLY: all archived events, ended most recently at the top
        /// </summary>
        /// <param name="ownerUserName">the specified owner username</param>
        private List<Event> GetAllOwnerEventsOrderedByRelevance(string ownerUserName)
        {
            var allEventsUnOrdered = EventClient.GetEventsByOwnerAndStatusWithFillLevel(ownerUserName, ownerUserName,
                    "All", 0, 0, "Id", false, Strings.EventFillLevels.None).List;
            var allEventsOrdered = new List<Event>(allEventsUnOrdered.Count + 1);
            //add "All" option (ID=-1)
            allEventsOrdered.Add(new Event() { Title = this.GlobalResourceString("All"), ID = -1 });
            //first add Status:Closed events, ended most recently at the top
            allEventsOrdered.AddRange(allEventsUnOrdered.Where(e => e.Status == Strings.AuctionEventStatuses.Closed)
                .OrderByDescending(e => e.EndDTTM));
            //next add all non-draft, non-archived events, starting soonest at the top
            allEventsOrdered.AddRange(allEventsUnOrdered.Where(e => e.Status != Strings.AuctionEventStatuses.Closed &&
                e.Status != Strings.AuctionEventStatuses.Draft &&
                e.Status != Strings.AuctionEventStatuses.Archived).OrderBy(e => e.StartDTTM));
            //next add all draft events, starting soonest at the top
            allEventsOrdered.AddRange(allEventsUnOrdered.Where(e => e.Status == Strings.AuctionEventStatuses.Draft)
                .OrderBy(e => e.StartDTTM));

            ////finally, add all archived events, ended most recently at the top
            //allEventsOrdered.AddRange(EventClient.GetEventsByOwnerAndStatusWithFillLevel(ownerUserName, ownerUserName,
            //        "Archived", 0, 0, "EndDTTM", true, Strings.EventFillLevels.None).List);

            return allEventsOrdered;
        }

        /// <summary>
        /// Displays "Sales Transactions" admin report
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
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
        [Authorize]
        public ActionResult SalesTransactionReport(string dateStart,
                                    string dateEnd,
                                    //string payee,
                                    string invoiceID,
                                    string listingID,
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
            string payee = this.FBOUserName();

            ViewData["dateStart"] = dateStart;
            ViewData["dateEnd"] = dateEnd;
            //ViewData["payee"] = payee;
            ViewData["invoiceID"] = invoiceID;
            ViewData["listingID"] = listingID;
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
                      new { value = 0, text = this.GlobalResourceString("All") }
                    , new { value = 1, text = this.GlobalResourceString("Paid") }
                    , new { value = 2, text = this.GlobalResourceString("Unpaid") }
                    , new { value = 3, text = this.GlobalResourceString("Voided") }
                }, "value", "text", isPaidInt);
            ViewData["PaidStatusSelectList"] = paidStatus;

            var currentCultureCode = this.GetCookie(Strings.MVC.CultureCookie);
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    currentCultureCode,
                                                    currentCultureCode);
            var currentCulture = CultureInfo.GetCultureInfo(currentCultureCode);
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
            input.Items.Add("pageSize", SiteClient.IntSetting(Strings.SiteProperties.MaxResultsPerPage).ToString(currentCulture));
            if (!input.Items.ContainsKey("payee"))
            {
                input.Items.Add("payee", payee);
            }
            else
            {
                input.Items["payee"] = payee;
            }

            //forces non-event data only
            if (input.Items.ContainsKey("eventID"))
            {
                input.Items["eventID"] = "0";
            }
            else
            {
                input.Items.Add("eventID", "0");
            }

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
        /// Displays "Sales Transactions" admin report
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="eventID">-2 for all data, null or -1 for all event data, 0 for all non-event data, &gt; 0 for specific event data</param>
        /// <param name="lotNumber">exact match of lot number to include</param>
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
        [Authorize]
        public ActionResult EventSalesTransactionReport(string dateStart,
                                    string dateEnd,
                                    //string payee,
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
            string payee = this.FBOUserName();

            ViewData["dateStart"] = dateStart;
            ViewData["dateEnd"] = dateEnd;
            //ViewData["payee"] = payee;
            ViewData["invoiceID"] = invoiceID;
            ViewData["listingID"] = listingID;
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

            int? selectedEventId = null;
            //events dropdown
            var allEventsOrdered = GetAllOwnerEventsOrderedByRelevance(this.FBOUserName());
            int temp1;
            if (int.TryParse(eventID, out temp1))
            {
                selectedEventId = temp1;
            }
            if (!selectedEventId.HasValue && allEventsOrdered.Count > 0)
            {
                selectedEventId = allEventsOrdered.First().ID;
            }

            List<SelectListItem> formattedOptionList = new List<SelectListItem>(allEventsOrdered.Count);
            foreach (var ev in allEventsOrdered)
            {
                if (ev.ID == -1)
                {
                    formattedOptionList.Add(new SelectListItem() { Text = ev.Title, Value = ev.ID.ToString() });
                }
                else
                {
                    formattedOptionList.Add(new SelectListItem() { Text = string.Format("{0} ({1})", ev.Title, ev.ID), Value = ev.ID.ToString() });
                }
            }
            ViewData["eventSelectList"] = new SelectList(formattedOptionList, "Value", "Text", selectedEventId ?? -1);

            SelectList paidStatus =
                new SelectList(new[] {
                      new { value = 0, text = this.GlobalResourceString("All") }
                    , new { value = 1, text = this.GlobalResourceString("Paid") }
                    , new { value = 2, text = this.GlobalResourceString("Unpaid") }
                    , new { value = 3, text = this.GlobalResourceString("Voided") }
                }, "value", "text", isPaidInt);
            ViewData["PaidStatusSelectList"] = paidStatus;

            var currentCultureCode = this.GetCookie(Strings.MVC.CultureCookie);
            UserInput input = new UserInput(User.Identity.Name, this.FBOUserName(),
                                                    currentCultureCode,
                                                    currentCultureCode);
            var currentCulture = CultureInfo.GetCultureInfo(currentCultureCode);
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
            input.Items.Add("pageSize", SiteClient.IntSetting(Strings.SiteProperties.MaxResultsPerPage).ToString(currentCulture));
            if (!input.Items.ContainsKey("payee"))
            {
                input.Items.Add("payee", payee);
            }
            if (input.Items.ContainsKey("eventID"))
            {
                input.Items["eventID"] = (selectedEventId ?? -1).ToString();
            }
            else
            {
                input.Items.Add("eventID", (selectedEventId ?? -1).ToString());
            }

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
        /// Sends "Sales Transactions" admin report data to the user's browser in CSV format
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
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
        [Authorize]
        public FileContentResult SalesTransactionCSV(string dateStart,
                                    string dateEnd,
                                    //string payee,
                                    string invoiceID,
                                    string listingID,
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
            string payee = this.FBOUserName();

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
            input.Items.Add("pageSize", "0");
            if (!input.Items.ContainsKey("payee"))
            {
                input.Items.Add("payee", payee);
            }

            //forces non-event data only
            if (input.Items.ContainsKey("eventID"))
            {
                input.Items["eventID"] = "0";
            }
            else
            {
                input.Items.Add("eventID", "0");
            }

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
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

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
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

            /* presumably seller fields do not need to be included in the seller's version of the sales transaction report
             *    because they would  be the same in every row for this seller, who already has access to this data anyway */
            //var includedSellerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
            //    .Where(f => f.IncludeInSalesReportAsSeller).ToList();
            //PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields
            var includedSellerFields = new List<CustomField>(0);

            var includedBuyerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsBuyer).ToList();
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

            //add header
            csv.Append(this.GlobalResourceString("DateTime"));
            csv.Append(",");

            csv.Append(this.GlobalResourceString("InvoiceID"));
            csv.Append(",");

            if (SiteClient.EnableEvents)
            {
                csv.Append(this.GlobalResourceString("LotNumber"));
                csv.Append(",");
            }
            else
            {
                csv.Append(this.GlobalResourceString("ListingID"));
                csv.Append(",");
            }

            csv.Append(this.GlobalResourceString("Description"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Price"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Quantity"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Total"));
            csv.Append(",");
            string paid = this.GlobalResourceString("Paid");
            csv.Append(paid);
            //csv.Append(",");
            //csv.Append(this.GlobalResourceString("Email"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("BuyerID")); // renamed from "PayerID"
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Buyer")); // renamed from "Payer"
            csv.Append(",");

            csv.Append(this.GlobalResourceString("Address"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("FirstName"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("LastName"));

            foreach (var field in includedListingFields)
            {
                csv.Append(",");
                csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
            }

            foreach (var field in includedEventFields)
            {
                csv.Append(",");
                csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
            }

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

            csv.AppendLine();

            foreach (SalesTransactionReportResult result in retVal.List)
            {
                csv.Append(QuoteCSVData(result.DateTime.ToLocalDTTM().ToString("G", currentCulture)));
                csv.Append(",");

                csv.Append(result.InvoiceID);
                csv.Append(",");

                if (SiteClient.EnableEvents)
                {
                    csv.Append(result.LotNumber);
                    csv.Append(",");
                }
                else
                {
                    csv.Append(result.ListingID);
                    csv.Append(",");
                }

                csv.Append(QuoteCSVData(result.Description));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Price.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(result.Quantity);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Total.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Paid ? paid : string.Empty));
                //csv.Append(",");
                //csv.Append(result.Email);
                csv.Append(",");
                csv.Append(result.PayerID);
                csv.Append(",");
                csv.Append(result.Payer);
                csv.Append(",");

                csv.Append(QuoteCSVData(result.Address ?? string.Empty));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.FirstName));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.LastName));

                int allIncludedFieldsCount = includedListingFields.Count +
                    includedEventFields.Count +
                    includedSellerFields.Count +
                    includedBuyerFields.Count;
                if (allIncludedFieldsCount > 0)
                {
                    var packedValueParts = result.PackedValues.Split('|');
                    Dictionary<string, string> packedValues = new Dictionary<string, string>(allIncludedFieldsCount);
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
                    foreach (var field in includedListingFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                    }
                    foreach (var field in includedEventFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                    }
                    foreach (var field in includedSellerFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(string.Format("Seller_{0}", field.Name)))
                        {
                            csv.Append(QuoteCSVData(packedValues[string.Format("Seller_{0}", field.Name)]));
                        }
                    }
                    foreach (var field in includedBuyerFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(string.Format("Buyer_{0}", field.Name)))
                        {
                            csv.Append(QuoteCSVData(packedValues[string.Format("Buyer_{0}", field.Name)]));
                        }
                    }
                }

                csv.AppendLine();
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            content.FileDownloadName = this.GlobalResourceString("SalesTransactions_csv");
            return content;
        }

        /// <summary>
        /// Sends "Sales Transactions" admin report data to the user's browser in CSV format
        /// </summary>
        /// <param name="dateStart">minimum sale date to include</param>
        /// <param name="dateEnd">maximum sale date to include</param>
        /// <param name="invoiceID">id of a specific invoice to include (0 or blank to skip)</param>
        /// <param name="listingID">id of a specific listing to include (0 or blank to skip)</param>
        /// <param name="eventID">id of a specific event to filter results by, -1 for all event invoices, or null, blank or 0 for all non-event invoices</param>
        /// <param name="lotNumber">exact match of lot number to include</param>
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
        [Authorize]
        public FileContentResult EventSalesTransactionCSV(string dateStart,
                                    string dateEnd,
                                    //string payee,
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
            string payee = this.FBOUserName();

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
            input.Items.Add("pageSize", "0");
            if (!input.Items.ContainsKey("payee"))
            {
                input.Items.Add("payee", payee);
            }
            int? selectedEventId = null;
            if (!string.IsNullOrWhiteSpace(eventID))
            {
                int temp1;
                if (int.TryParse(eventID, out temp1))
                {
                    selectedEventId = temp1;
                }
            }
            if (input.Items.ContainsKey("eventID"))
            {
                input.Items["eventID"] = (selectedEventId ?? -1).ToString();
            }
            else
            {
                input.Items.Add("eventID", (selectedEventId ?? -1).ToString());
            }

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
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

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
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

            /* presumably seller fields do not need to be included in the seller's version of the sales transaction report
             *    because they would  be the same in every row for this seller, who already has access to this data anyway */
            //var includedSellerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
            //    .Where(f => f.IncludeInSalesReportAsSeller).ToList();
            //PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields
            var includedSellerFields = new List<CustomField>(0);

            var includedBuyerFields = CommonClient.GetCustomFields(Strings.CustomFieldGroups.User, 0, 0, "DisplayOrder", false).List
                .Where(f => f.IncludeInSalesReportAsBuyer).ToList();
            PruneCustomFieldsVisbility(CustomFieldAccess.Owner, includedListingFields); //remove admin-only fields

            //add header
            if (SiteClient.EnableEvents && (string.IsNullOrEmpty(eventID) || eventID == "0"))
            {
                csv.Append(this.GlobalResourceString("EventNumber"));
                csv.Append(",");
            }

            csv.Append(this.GlobalResourceString("DateTime"));
            csv.Append(",");

            csv.Append(this.GlobalResourceString("InvoiceID"));
            csv.Append(",");

            if (SiteClient.EnableEvents)
            {
                csv.Append(this.GlobalResourceString("LotNumber"));
                csv.Append(",");
            }
            else
            {
                csv.Append(this.GlobalResourceString("ListingID"));
                csv.Append(",");
            }

            csv.Append(this.GlobalResourceString("Description"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Price"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Quantity"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Total"));
            csv.Append(",");
            string paid = this.GlobalResourceString("Paid");
            csv.Append(paid);
            //csv.Append(",");
            //csv.Append(this.GlobalResourceString("Email"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("BuyerID")); // renamed from "PayerID"
            csv.Append(",");
            csv.Append(this.GlobalResourceString("Buyer")); // renamed from "Payer"
            csv.Append(",");

            csv.Append(this.GlobalResourceString("Address"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("FirstName"));
            csv.Append(",");
            csv.Append(this.GlobalResourceString("LastName"));

            foreach (var field in includedListingFields)
            {
                csv.Append(",");
                csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
            }

            foreach (var field in includedEventFields)
            {
                csv.Append(",");
                csv.Append(QuoteCSVData(this.CustomFieldResourceString(field.Name)));
            }

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

            csv.AppendLine();

            foreach (SalesTransactionReportResult result in retVal.List)
            {
                if (SiteClient.EnableEvents && (string.IsNullOrEmpty(eventID) || eventID == "0"))
                {
                    csv.Append(result.AuctionEventId);
                    csv.Append(",");
                }

                csv.Append(QuoteCSVData(result.DateTime.ToLocalDTTM().ToString("G", currentCulture)));
                csv.Append(",");

                csv.Append(result.InvoiceID);
                csv.Append(",");

                if (SiteClient.EnableEvents)
                {
                    csv.Append(result.LotNumber);
                    csv.Append(",");
                }
                else
                {
                    csv.Append(result.ListingID);
                    csv.Append(",");
                }

                csv.Append(QuoteCSVData(result.Description));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Price.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(result.Quantity);
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Total.ToString("c", currentCulture)));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.Paid ? paid : string.Empty));
                //csv.Append(",");
                //csv.Append(result.Email);
                csv.Append(",");
                csv.Append(result.PayerID);
                csv.Append(",");
                csv.Append(result.Payer);
                csv.Append(",");

                csv.Append(QuoteCSVData(result.Address ?? string.Empty));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.FirstName));
                csv.Append(",");
                csv.Append(QuoteCSVData(result.LastName));

                int allIncludedFieldsCount = includedListingFields.Count +
                    includedEventFields.Count +
                    includedSellerFields.Count +
                    includedBuyerFields.Count;
                if (allIncludedFieldsCount > 0)
                {
                    var packedValueParts = result.PackedValues.Split('|');
                    Dictionary<string, string> packedValues = new Dictionary<string, string>(allIncludedFieldsCount);
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
                    foreach (var field in includedListingFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                    }
                    foreach (var field in includedEventFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(field.Name))
                        {
                            csv.Append(QuoteCSVData(packedValues[field.Name]));
                        }
                    }
                    foreach (var field in includedSellerFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(string.Format("Seller_{0}", field.Name)))
                        {
                            csv.Append(QuoteCSVData(packedValues[string.Format("Seller_{0}", field.Name)]));
                        }
                    }
                    foreach (var field in includedBuyerFields)
                    {
                        csv.Append(",");
                        if (packedValues.ContainsKey(string.Format("Buyer_{0}", field.Name)))
                        {
                            csv.Append(QuoteCSVData(packedValues[string.Format("Buyer_{0}", field.Name)]));
                        }
                    }
                }

                csv.AppendLine();
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            FileContentResult content = new FileContentResult(buffer, "text/csv");
            if (SiteClient.EnableEvents && !string.IsNullOrEmpty(eventID) && eventID != "0")
            {
                content.FileDownloadName = this.GlobalResourceString("SalesTransactions_Event_X_csv", eventID);
            }
            else
            {
                content.FileDownloadName = this.GlobalResourceString("SalesTransactions_csv");
            }
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

        #endregion Reports

        #region CSV Report

        /// <summary>
        /// Displays "Download Report" view
        /// </summary>
        /// <param name="id">id of the report to download</param>
        /// <param name="compress"></param>
        /// <returns></returns>
        [Authorize]
        public ActionResult GetCSVReport(int id, bool? compress)
        {
            ViewData["compress"] = compress;
            ViewData["id"] = id;
            return View();
        }

        /// <summary>
        /// Streams contents of report to browser
        /// </summary>
        /// <param name="id">id of the report to download</param>
        /// <param name="compress">true if result is to be returned as a zip file</param>
        /// <returns>FileContentResult value</returns>
        [Authorize]
        public FileContentResult DownloadCSVReport(int id, bool? compress)
        {

            Report r = CommonClient.GetReportByID(User.Identity.Name, id);

            if (r != null)
            {
                if (compress.HasValue && compress.Value)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream strm = new GZipStream(ms, CompressionMode.Compress, true))
                        {
                            byte[] enc = System.Text.Encoding.UTF8.GetBytes(r.Data);
                            strm.Write(enc, 0, enc.Length);
                        }
                        FileContentResult content = new FileContentResult(ms.ToArray(), "application/gzip");
                        content.FileDownloadName = r.ReportName + ".csv.gz";
                        return content;
                    }
                }
                else
                {
                    FileContentResult content = new FileContentResult(System.Text.Encoding.UTF8.GetBytes(r.Data), "text/csv");
                    content.FileDownloadName = r.ReportName + ".csv";
                    return content;
                }
            }
            else
            {
                return null;
            }

        }

        #endregion CSV Report

        #region Site Map Placeholder Redirects

        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Default() { return RedirectToAction(Strings.MVC.SummaryAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Bidding() { return RedirectToAction(Strings.MVC.BiddingActiveAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Listing() { return RedirectToAction(Strings.MVC.ListingsActiveAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Invoices() { return RedirectToAction(Strings.MVC.InvoicePurchasesAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult SiteFees() { return RedirectToAction(Strings.MVC.FeesAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Settings() { return RedirectToAction(Strings.MVC.AccountSettingsAction); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Messaging() { return RedirectToAction(Strings.MVC.ViewMessagesAction, new { incoming = true }); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult ListingPreferences() { return RedirectToAction(Strings.MVC.PropertyManagementAction, new { id = 44902 }); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult PaymentPreferences() { return RedirectToAction(Strings.MVC.PropertyManagementAction, new { id = 44902 }); }
        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult FeedbackIndex() { return RedirectToAction(Strings.MVC.FeedbackAction); }

        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult EventSaleInvoices() { return RedirectToAction(Strings.MVC.InvoiceEventSalesAction); }

        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult EventPurchaseInvoices() { return RedirectToAction(Strings.MVC.InvoicePurchasesAction); }

        #endregion Site Map Placeholder Redirects

        #region Event Closing Group Management

        /// <summary>
        /// Displays Closing Group management view for the specified Event
        /// </summary>
        /// <param name="id">ID of the specified Event</param>
        /// <returns>View(EventOrganization)</returns>
        [Authorize]
        public ActionResult ClosingGroups(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }
            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("LotOrderManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            var result = EventClient.GetEventClosingGroups(actingUN, this.FBOUserName(), id);
            return View(result);
        }

        /// <summary>
        /// Processes request to update the Closing Group details for the specified Event
        /// </summary>
        /// <param name="id">ID of the specified Event</param>
        /// <param name="jsonModel">the JSON-encoded data to apply</param>
        /// <returns>View(EventOrganization)</returns>
        [Authorize]
        [HttpPost]
        public ActionResult ClosingGroups(int id, string jsonModel)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }
            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("LotOrderManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            try
            {
                int[][] newOrganization = JSON.Deserialize<int[][]>(jsonModel);
                EventClient.SetEventClosingGroups(actingUN, this.FBOUserName(), id, newOrganization);
                EventClient.CalculateLotEndDTTMs(actingUN, id);
                PrepareSuccessMessage("ClosingGroups", MessageType.Method);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id });
            }
            catch (Exception)
            {
                PrepareErrorMessage("ClosingGroups", MessageType.Method);
                var result = EventClient.GetEventClosingGroups(actingUN, this.FBOUserName(), id);
                return View(result);
            }
            //return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
        }

        /// <summary>
        /// Processes request to re-calculate all lot groups for the specified event
        /// </summary>
        /// <param name="EventID">the ID of the specified event</param>
        /// <param name="GroupingOption">one of &quot;MergeAll&quot;, &quot;SplitAll&quot; or &quot;NLotsPerGroup&quot;</param>
        /// <param name="LotsPerGroup">required integer value of 1 or greater when GroupingOption=&quot;NLotsPerGroup&quot;</param>
        /// <param name="ReOrderByLotNumber">if true, lots will also be re-ordered by lot number.  If any lot numbers in this event 
        ///  contain non-number characters then it will be re-ordered alphabetically, otherwise numerically.</param>
        /// <returns>redirect to /Account/ClosingGroups/{Event ID}</returns>
        [Authorize]
        public ActionResult RecalculateClosingGroups(int EventID, string GroupingOption, int? LotsPerGroup, bool? ReOrderByLotNumber)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, EventID, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this event (admin or event owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = EventID });
            }
            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("LotOrderManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            if (GroupingOption != "MergeAll" && GroupingOption != "SplitAll" && GroupingOption != "NLotsPerGroup")
            {
                PrepareErrorMessage("InvalidGroupingOption", MessageType.Message);
                return RedirectToAction(Strings.MVC.ClosingGroupsAction, new { @id = EventID });
            }
            else if (GroupingOption == "NLotsPerGroup" && (!LotsPerGroup.HasValue || LotsPerGroup.Value < 1))
            {
                PrepareErrorMessage("InvalidLotsPerGroupValue", MessageType.Message);
                return RedirectToAction(Strings.MVC.ClosingGroupsAction, new { @id = EventID });
            }

            //SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            var existingGroups = EventClient.GetEventClosingGroups(actingUN, this.FBOUserName(), EventID);
            int lotCount = (existingGroups.Lots ?? new List<Lot>(0)).Count;

            int[][] newOrganization;
            if (lotCount == 0)
            {
                newOrganization = new int[1][];
                newOrganization[0] = new int[0];

            }
            else if (GroupingOption == "MergeAll")
            {
                //one group for all lots
                newOrganization = new int[2][];

                IEnumerable<Lot> orderedLots = ReOrderByLotNumber.HasValue && ReOrderByLotNumber.Value
                    ? ReOrderLots(existingGroups.Lots) : existingGroups.Lots;

                newOrganization[0] = orderedLots.Select(l => l.ID).ToArray();
                newOrganization[1] = new int[0];
            }
            else if (GroupingOption == "SplitAll")
            {
                //one group for each lot
                newOrganization = new int[lotCount + 1][];
                int groupIndex = 0;

                IEnumerable<Lot> orderedLots = ReOrderByLotNumber.HasValue && ReOrderByLotNumber.Value
                    ? ReOrderLots(existingGroups.Lots) : existingGroups.Lots;

                foreach (var lot in orderedLots)
                {
                    newOrganization[groupIndex] = new int[1] { lot.ID };
                    groupIndex++;
                }
                newOrganization[groupIndex] = new int[0];
            }
            else // if (GroupingOption == "NLotsPerGroup") 
            {
                //one group for each N lots
                int groupsNeeded = (lotCount / LotsPerGroup.Value);
                if (lotCount % LotsPerGroup.Value > 0) groupsNeeded++;
                newOrganization = new int[groupsNeeded + 1][];

                int groupIndex = -1;
                int lotIndex = LotsPerGroup.Value;
                int lotsAdded = 0;

                IEnumerable<Lot> orderedLots = ReOrderByLotNumber.HasValue && ReOrderByLotNumber.Value
                    ? ReOrderLots(existingGroups.Lots) : existingGroups.Lots;

                foreach (var lot in orderedLots)
                {
                    if (lotIndex == LotsPerGroup.Value)
                    {
                        lotIndex = 0;
                        groupIndex++;
                        if ((lotCount - lotsAdded) < LotsPerGroup.Value)
                        {
                            newOrganization[groupIndex] = new int[lotCount - lotsAdded];
                        }
                        else
                        {
                            newOrganization[groupIndex] = new int[LotsPerGroup.Value];
                        }
                    }
                    newOrganization[groupIndex][lotIndex] = lot.ID;
                    lotsAdded++;
                    lotIndex++;
                }
                newOrganization[groupsNeeded] = new int[0];
            }

            try
            {
                EventClient.SetEventClosingGroups(actingUN, this.FBOUserName(), EventID, newOrganization);
                EventClient.CalculateLotEndDTTMs(actingUN, EventID);
                PrepareSuccessMessage("RecalculateClosingGroups", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("RecalculateClosingGroups", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.ClosingGroupsAction, new { @id = EventID });
        }

        private IEnumerable<Lot> ReOrderLots(IEnumerable<Lot> currentLots)
        {
            IEnumerable<Lot> results;
            Regex rgx = new Regex("[^0-9]", RegexOptions.IgnoreCase);
            if (currentLots.Any(lot => rgx.IsMatch(lot.LotNumber)))
            {
                //non-numeric characters detected, use alphabetic sort
                results = currentLots.OrderBy(lot => lot.LotNumber);
            }
            else
            {
                //numeric sort
                results = currentLots.OrderBy(lot => int.Parse(lot.LotNumber));
            }
            return results;
        }

        /// <summary>
        /// Processes request to re-assign all lot numbers in the specified event based on the current order
        /// </summary>
        /// <param name="eventId">The ID of the specified event</param>
        /// <param name="returnUrl">URL to return to whether successful or unsuccessful</param>
        [Authorize]
        public ActionResult ResetLotNumbers(int eventId, string returnUrl)
        {
            try
            {
                var lots = EventClient.GetLotsByEventWithFillLevel(User.Identity.Name, eventId, 0, 0, "LotOrder", false, ListingFillLevels.None);
                int CurrentLotOrder = 1;
                foreach (Lot lot in lots.List)
                {
                    string lotNumberFormatString;
                    int digits = SiteClient.IntSetting(SiteProperties.DefaultLotNumberDigits);
                    if (digits > 0 && digits < 10)
                    {
                        lotNumberFormatString = "0000000000".Left(digits);
                    }
                    else
                    {
                        lotNumberFormatString = "0000000000";
                    }
                    string newLotNumber = (CurrentLotOrder++).ToString(lotNumberFormatString, CultureInfo.InvariantCulture);
                    EventClient.SetLotNumber(User.Identity.Name, lot.ID, newLotNumber);
                }
                PrepareSuccessMessage("ResetLotNumbers", MessageType.Method);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("ResetLotNumbers", e);
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.LotsByEventAction, new { @id = eventId });
        }

        /// <summary>
        /// Displays Soft Closing Group management view for the specified Event
        /// </summary>
        /// <param name="id">ID of the specified Event</param>
        /// <returns>View(EventOrganization)</returns>
        [Authorize]
        public ActionResult SoftClosingGroups(int id)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }
            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("AutoExtendGroupManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            var result = EventClient.GetEventSoftClosingGroups(actingUN, this.FBOUserName(), id);
            return View(result);
        }

        /// <summary>
        /// Processes request to update the Soft Closing Group details for the specified Event
        /// </summary>
        /// <param name="id">ID of the specified Event</param>
        /// <param name="jsonModel">the JSON-encoded data to apply</param>
        /// <returns>View(EventOrganization)</returns>
        [Authorize]
        [HttpPost]
        public ActionResult SoftClosingGroups(int id, string jsonModel)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, id, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this listing (admin or listing owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
            }

            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("AutoExtendGroupManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            try
            {
                int[][] newOrganization = JSON.Deserialize<int[][]>(jsonModel);
                EventClient.SetEventSoftClosingGroups(actingUN, this.FBOUserName(), id, newOrganization);
                PrepareSuccessMessage("SoftClosingGroups", MessageType.Method);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id });
            }
            catch (Exception)
            {
                PrepareErrorMessage("SoftClosingGroups", MessageType.Method);
                var result = EventClient.GetEventSoftClosingGroups(actingUN, this.FBOUserName(), id);
                return View(result);
            }
            //return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = id });
        }

        /// <summary>
        /// Processes request to re-calculate all soft-closing (i.e. auto extend) lot groups for the specified event
        /// </summary>
        /// <param name="EventID">the ID of the specified event</param>
        /// <param name="GroupingOption">one of &quot;MergeAll&quot;, &quot;SplitAll&quot;, &quot;NLotsPerGroup&quot;, or &quot;CopyClosingGroups&quot;</param>
        /// <param name="LotsPerGroup">required integer value of 1 or greater when GroupingOption=&quot;NLotsPerGroup&quot;</param>
        /// <returns>redirect to /Account/ClosingGroups/{Event ID}</returns>
        [Authorize]
        public ActionResult RecalculateSoftClosingGroups(int EventID, string GroupingOption, int? LotsPerGroup)
        {
            string actingUN = User.Identity.Name; // username of logged in user 
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(actingUN, EventID, Strings.EventFillLevels.All);
            ViewData["Event"] = auctionEvent;

            //determine if this user has permission to edit this event (admin or event owner only)
            bool isAdmin = User.IsInRole(Strings.Roles.Admin);
            bool isListingOwner = (auctionEvent.OwnerUserName.Equals(actingUN, StringComparison.OrdinalIgnoreCase));
            if (!(isAdmin || isListingOwner))
            {
                return RedirectToAction(Strings.MVC.DetailsAction, Strings.MVC.EventController, new { id = EventID });
            }
            bool isElligibleStatus = (auctionEvent.Status == AuctionEventStatuses.Draft ||
                                        auctionEvent.Status == AuctionEventStatuses.Preview ||
                                        auctionEvent.Status == AuctionEventStatuses.Scheduled ||
                                        auctionEvent.Status == AuctionEventStatuses.Active);
            if (!isElligibleStatus)
            {
                PrepareErrorMessage("AutoExtendGroupManagementNotAvailableOnceEventIsClosing", MessageType.Message);
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { id = auctionEvent.ID });
            }

            if (GroupingOption != "MergeAll" && GroupingOption != "SplitAll" && GroupingOption != "NLotsPerGroup" && GroupingOption != "CopyClosingGroups")
            {
                PrepareErrorMessage("InvalidGroupingOption", MessageType.Message);
                return RedirectToAction(Strings.MVC.SoftClosingGroupsAction, new { @id = EventID });
            }
            else if (GroupingOption == "NLotsPerGroup" && (!LotsPerGroup.HasValue || LotsPerGroup.Value < 1))
            {
                PrepareErrorMessage("InvalidLotsPerGroupValue", MessageType.Message);
                return RedirectToAction(Strings.MVC.SoftClosingGroupsAction, new { @id = EventID });
            }

            //SetHighlightedNavCatByEventStatus(auctionEvent.Status);

            var existingGroups = EventClient.GetEventSoftClosingGroups(actingUN, this.FBOUserName(), EventID);
            int lotCount = (existingGroups.Lots ?? new List<Lot>(0)).Count;

            int[][] newOrganization;
            if (lotCount == 0)
            {
                newOrganization = new int[1][];
                newOrganization[0] = new int[0];

            }
            else if (GroupingOption == "CopyClosingGroups")
            {
                var existingClosingGroups = EventClient.GetEventClosingGroups(actingUN, this.FBOUserName(), EventID);
                newOrganization = existingClosingGroups.LotStateArray;
            }
            else if (GroupingOption == "MergeAll")
            {
                //one group for all lots
                newOrganization = new int[2][];
                newOrganization[0] = existingGroups.Lots.Select(l => l.ID).ToArray();
                newOrganization[1] = new int[0];
            }
            else if (GroupingOption == "SplitAll")
            {
                //one group for each lot
                newOrganization = new int[lotCount + 1][];
                int groupIndex = 0;
                foreach (var lot in existingGroups.Lots)
                {
                    newOrganization[groupIndex] = new int[1] { lot.ID };
                    groupIndex++;
                }
                newOrganization[groupIndex] = new int[0];
            }
            else // if (GroupingOption == "NLotsPerGroup") 
            {
                //one group for each N lots
                int groupsNeeded = (lotCount / LotsPerGroup.Value);
                if (lotCount % LotsPerGroup.Value > 0) groupsNeeded++;
                newOrganization = new int[groupsNeeded + 1][];
                int groupIndex = -1;
                int lotIndex = LotsPerGroup.Value;
                int lotsAdded = 0;
                foreach (var lot in existingGroups.Lots)
                {
                    if (lotIndex == LotsPerGroup.Value)
                    {
                        lotIndex = 0;
                        groupIndex++;
                        if ((lotCount - lotsAdded) < LotsPerGroup.Value)
                        {
                            newOrganization[groupIndex] = new int[lotCount - lotsAdded];
                        }
                        else
                        {
                            newOrganization[groupIndex] = new int[LotsPerGroup.Value];
                        }
                    }
                    newOrganization[groupIndex][lotIndex] = lot.ID;
                    lotsAdded++;
                    lotIndex++;
                }
                newOrganization[groupsNeeded] = new int[0];
            }

            try
            {
                EventClient.SetEventSoftClosingGroups(actingUN, this.FBOUserName(), EventID, newOrganization);
                PrepareSuccessMessage("RecalculateSoftClosingGroups", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("RecalculateSoftClosingGroups", MessageType.Method);
            }
            return RedirectToAction(Strings.MVC.SoftClosingGroupsAction, new { @id = EventID });
        }

        #endregion Event Closing Group Management

        #region Bulk Listing/Lot Copying/Moving/Deleting

        /// <summary>
        /// Displays a form to copy multiple lots from the specified event to a new event
        /// </summary>
        /// <param name="FromEventID">ID of the specified source event</param>
        /// <param name="ToEventID">ID of the target event</param>
        /// <param name="lotAction">&quot;Copy&quot;, &quot;Move&quot; or &quot;Delete&quot;</param>
        /// <param name="returnUrl">URL to return user upon successful copy operation</param>
        [Authorize]
        public ActionResult CopyLots(int? FromEventID, int? ToEventID, string lotAction, string returnUrl)
        {
            ViewData["LotAction"] = lotAction;
            ViewData["returnUrl"] = returnUrl;
            if (!FromEventID.HasValue)
            {
                PrepareErrorMessage("MissingSourceEventID", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }

            //retrieve source event
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, FromEventID.Value, EventFillLevels.None);
            if (auctionEvent == null)
            {
                //event does not exist
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else if (!auctionEvent.OwnerUserName.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase) && !User.IsInRole("Admin"))
            {
                //logged on user doesn't own this event and is also not an admin
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else
            {
                SetHighlightedNavCatByEventStatus(auctionEvent.Status);
                ViewData["Event"] = auctionEvent;
            }

            //retrieve list of eligible events
            string statuses = Strings.AuctionEventStatuses.Draft;
            var eligibleEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                0, 0, Strings.Fields.Title, false, EventFillLevels.None).List;//.Where(ev => ev.ID != FromEventID.Value).ToList();
            if (lotAction == "Move")
            {
                eligibleEvents = eligibleEvents.Where(ev => ev.ID != FromEventID.Value).ToList();
            }
            if (eligibleEvents.Count == 0 && lotAction != "Delete")
            {
                //string retUrl = this.GetActionUrl(Strings.MVC.CopyLotsAction, Strings.MVC.AccountController, new { FromEventID, ToEventID, lotAction, returnUrl });
                string retUrl = this.GetActionUrl(Strings.MVC.LotsByEventAction, Strings.MVC.AccountController, new { @id = FromEventID });
                return RedirectToAction(Strings.MVC.EventNeededAction, Strings.MVC.EventController, new { @returnUrl = retUrl });
            }
            List<SelectListItem> formattedOptionList = new List<SelectListItem>(eligibleEvents.Count);
            foreach (var ev in eligibleEvents)
            {
                if (ev.ID == 0)
                {
                    formattedOptionList.Add(new SelectListItem() { Text = ev.Title, Value = ev.ID.ToString() });
                }
                else
                {
                    formattedOptionList.Add(new SelectListItem() { Text = string.Format("{0} ({1})", ev.Title, ev.ID), Value = ev.ID.ToString() });
                }
            }
            ViewData[Fields.ToEventID] = new SelectList(formattedOptionList, "Value", "Text", ToEventID ?? 0);

            //retrieve all lots, by lot order
            ListingPageQuery currentQuery = QuerySortDefinitions.LotsByEventOptions[8]; // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
            string fillLevel = ListingFillLevels.LotEvent + "," + ListingFillLevels.Properties;
            var listings = EventClient.SearchLotsByEvent(User.Identity.Name, FromEventID.Value, "All",
                string.Empty, string.Empty, 0, 0, currentQuery.Sort, currentQuery.Descending, fillLevel);

            return View(listings);
        }

        /// <summary>
        /// Processes request to copy multiple lots to another event
        /// </summary>
        /// <param name="FromEventID">ID of the source event</param>
        /// <param name="ToEventID">ID of the target event</param>
        /// <param name="ListingIDs">ID or IDs of listings to be activated</param>
        /// <param name="returnUrl">URL to return user upon successful copy operation</param>
        /// <param name="GenerateNewLotNumbers">true to indicate that lot numbers will not be copied, causing new lot numbers to be generated automatically</param>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CopyLots(int? FromEventID, int? ToEventID, string[] ListingIDs, string returnUrl, bool? GenerateNewLotNumbers)
        {
            ViewData["GenerateNewLotNumbers"] = GenerateNewLotNumbers ?? false;
            ViewData["returnUrl"] = returnUrl;
            if (!FromEventID.HasValue)
            {
                PrepareErrorMessage("MissingSourceEventID", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }

            if (!ToEventID.HasValue)
            {
                PrepareErrorMessage("MissingTargetEventID", MessageType.Message);
                if (FromEventID.HasValue)
                {
                    return RedirectToAction(Strings.MVC.CopyLotsAction, new { @FromEventID = FromEventID.Value });
                }
                else
                {
                    return RedirectToAction(Strings.MVC.SummaryAction);
                }
            }

            //retrieve all lots, by lot order
            ListingPageQuery currentQuery = QuerySortDefinitions.LotsByEventOptions[8]; // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
            string fillLevel = ListingFillLevels.All;
            var listings = EventClient.SearchLotsByEvent(User.Identity.Name, FromEventID.Value, "All",
                string.Empty, string.Empty, 0, 0, currentQuery.Sort, currentQuery.Descending, fillLevel);

            string actingUserName = User.Identity.Name;
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            string allIds = ListingIDs != null ? string.Join(",", ListingIDs) : null;
            int createdCount = 0;
            int attemptedCount = 0;

            if (!string.IsNullOrEmpty(allIds))
            {
                try
                {
                    //retrieve target event to determine default IsTaxable value
                    var targetevent = EventClient.GetEventByIDWithFillLevel(actingUserName, ToEventID.Value, EventFillLevels.None);
                    bool isTaxable = !SiteClient.BoolSetting(SiteProperties.HideTaxFields) && targetevent.LotsTaxable;

                    var selectedIds = allIds.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => int.Parse(s));
                    foreach (var listing in listings.List.Where(l => selectedIds.Any(a => l.ID == a)))
                    {
                        attemptedCount++;
                        try
                        {
                            var input = new UserInput(actingUserName, listing.OwnerUserName, cultureCode, cultureCode);
                            input.FillInputFromListing(listing);

                            input.Items.Add(Fields.SaveAsDraft, "True");

                            input.Items.Add(Fields.CategoryID, listing.PrimaryCategory.ID.ToString());
                            string allCatIds = string.Join(",", listing.Categories.Where(c => c.Type != CategoryTypes.Event).Select(c => c.ID.ToString()));
                            input.Items.Add(Fields.AllCategories, allCatIds);

                            input.Items.Add(Fields.EventID, ToEventID.Value.ToString());

                            if (GenerateNewLotNumbers ?? false)
                            {
                                input.Items.Add(Fields.LotNumber, string.Empty);
                            }
                            else
                            {
                                input.Items.Add(Fields.LotNumber, listing.Lot.LotNumber);
                            }

                            input.Items.Add(Fields.ListingType, listing.Type.Name);

                            if (input.Items.ContainsKey(Fields.BuyItNow))
                                input.Items.Remove(Fields.BuyItNow);
                            if (input.Items.ContainsKey(Fields.BuyItNowUsed))
                                input.Items.Remove(Fields.BuyItNowUsed);

                            if (!input.Items.ContainsKey(Fields.IsTaxable) || string.IsNullOrWhiteSpace(input.Items[Fields.IsTaxable]))
                            {
                                input.Items[Fields.IsTaxable] = isTaxable.ToString();
                            }

                            int newListingId;
                            EventClient.CreateLot(actingUserName, input, false, out newListingId);
                            createdCount++;
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            string allValidationIssues = string.Empty;
                            foreach (var issue in vfc.Detail.ValidationIssues)
                            {
                                allValidationIssues += (!string.IsNullOrEmpty(allValidationIssues) ? "; " : string.Empty);
                                allValidationIssues += this.ValidationResourceString(issue.Message);
                            }
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, allValidationIssues));
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, this.GlobalResourceString(e.Message)));
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage(Strings.MVC.CopyLotsAction, MessageType.Method);
                }
            }

            if (attemptedCount == 0)
            {
                PrepareErrorMessage(this.GlobalResourceString("YouMustSelectAtLeastOneLotToCopy"), MessageType.Message);
            }
            else if (createdCount < attemptedCount)
            {
                PrepareErrorMessage(this.GlobalResourceString("xNotCreated", (attemptedCount - createdCount)), MessageType.Message);
            }

            if (createdCount > 0)
            {
                //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyCreated", createdCount), MessageType.Message);
                ViewData["SelectedNavAction"] = Strings.MVC.ListingsDraftsAction;
                //return ListingsDrafts(0, //page = 0
                //                        8, // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
                //                        null, null);
                //return LotsByEvent(ToEventID.Value, 
                //                   0, // first page
                //                   8, // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
                //                   "All", null, null);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { @id = ToEventID, @page = 0, @SortFilterOptions = 8 });
            }

            //retrieve source event
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, FromEventID.Value, EventFillLevels.None);
            if (auctionEvent == null)
            {
                //event does not exist
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else if (!auctionEvent.OwnerUserName.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase) && !User.IsInRole("Admin"))
            {
                //logged on user doesn't own this event and is also not an admin
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else
            {
                ViewData["Event"] = auctionEvent;
                SetHighlightedNavCatByEventStatus(auctionEvent.Status);
            }

            //retrieve list of eligible events
            string statuses = Strings.AuctionEventStatuses.Draft;
            var eligibleEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                0, 0, Strings.Fields.Title, false, EventFillLevels.None).List; //.Where(ev => ev.ID != FromEventID.Value).ToList();
            if (eligibleEvents.Count == 0)
            {
                string retUrl = this.GetActionUrl(Strings.MVC.CopyLotsAction, Strings.MVC.AccountController, new { FromEventID, ToEventID, returnUrl });
                return RedirectToAction(Strings.MVC.EventNeededAction, Strings.MVC.EventController, new { @returnUrl = retUrl });
            }
            List<SelectListItem> formattedOptionList = new List<SelectListItem>(eligibleEvents.Count);
            foreach (var ev in eligibleEvents)
            {
                if (ev.ID == 0)
                {
                    formattedOptionList.Add(new SelectListItem() { Text = ev.Title, Value = ev.ID.ToString() });
                }
                else
                {
                    formattedOptionList.Add(new SelectListItem() { Text = string.Format("{0} ({1})", ev.Title, ev.ID), Value = ev.ID.ToString() });
                }
            }
            ViewData[Fields.ToEventID] = new SelectList(formattedOptionList, "Value", "Text", ToEventID ?? 0);

            return View(Strings.MVC.CopyLotsAction, listings);

        }

        /// <summary>
        /// Processes request to copy multiple lots to another event
        /// </summary>
        /// <param name="FromEventID">ID of the source event</param>
        /// <param name="ToEventID">ID of the target event</param>
        /// <param name="ListingIDs">ID or IDs of listings to be activated</param>
        /// <param name="returnUrl">URL to return user upon successful copy operation</param>
        /// <param name="GenerateNewLotNumbers">true to indicate that lot numbers will not be copied, causing new lot numbers to be generated automatically</param>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult MoveLots(int? FromEventID, int? ToEventID, string[] ListingIDs, string returnUrl, bool? GenerateNewLotNumbers)
        {
            ViewData["GenerateNewLotNumbers"] = GenerateNewLotNumbers ?? false;
            ViewData["returnUrl"] = returnUrl;
            if (!FromEventID.HasValue)
            {
                PrepareErrorMessage("MissingSourceEventID", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }

            if (!ToEventID.HasValue)
            {
                PrepareErrorMessage("MissingTargetEventID", MessageType.Message);
                if (FromEventID.HasValue)
                {
                    return RedirectToAction(Strings.MVC.CopyLotsAction, new { @FromEventID = FromEventID.Value });
                }
                else
                {
                    return RedirectToAction(Strings.MVC.SummaryAction);
                }
            }

            //retrieve all lots, by lot order
            ListingPageQuery currentQuery = QuerySortDefinitions.LotsByEventOptions[8]; // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
            string fillLevel = ListingFillLevels.All;
            var listings = EventClient.SearchLotsByEvent(User.Identity.Name, FromEventID.Value, "All",
                string.Empty, string.Empty, 0, 0, currentQuery.Sort, currentQuery.Descending, fillLevel);

            string actingUserName = User.Identity.Name;
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            string allIds = ListingIDs != null ? string.Join(",", ListingIDs) : null;
            int createdCount = 0;
            int attemptedCount = 0;
            int deletedCount = 0;

            if (!String.IsNullOrEmpty(allIds))
            {
                try
                {
                    var selectedIds = allIds.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => int.Parse(s));
                    foreach (var listing in listings.List.Where(l => selectedIds.Any(a => l.ID == a)))
                    {
                        attemptedCount++;
                        try
                        {
                            var input = new UserInput(actingUserName, listing.OwnerUserName, cultureCode, cultureCode);
                            input.FillInputFromListing(listing);

                            input.Items.Add(Fields.SaveAsDraft, "True");

                            input.Items.Add(Fields.CategoryID, listing.PrimaryCategory.ID.ToString());
                            string allCatIds = string.Join(",", listing.Categories.Where(c => c.Type != CategoryTypes.Event).Select(c => c.ID.ToString()));
                            input.Items.Add(Fields.AllCategories, allCatIds);

                            input.Items.Add(Fields.EventID, ToEventID.Value.ToString());

                            if (GenerateNewLotNumbers ?? false)
                            {
                                input.Items.Add(Fields.LotNumber, string.Empty);
                            }
                            else
                            {
                                input.Items.Add(Fields.LotNumber, listing.Lot.LotNumber);
                            }

                            input.Items.Add(Fields.ListingType, listing.Type.Name);

                            if (input.Items.ContainsKey(Fields.BuyItNow))
                                input.Items.Remove(Fields.BuyItNow);
                            if (input.Items.ContainsKey(Fields.BuyItNowUsed))
                                input.Items.Remove(Fields.BuyItNowUsed);

                            int newListingId;
                            EventClient.CreateLot(actingUserName, input, false, out newListingId);
                            createdCount++;

                            EventClient.DeleteLot(User.Identity.Name, listing.Lot.ID);
                            deletedCount++;
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            string allValidationIssues = string.Empty;
                            foreach (var issue in vfc.Detail.ValidationIssues)
                            {
                                allValidationIssues += (!string.IsNullOrEmpty(allValidationIssues) ? "; " : string.Empty);
                                allValidationIssues += this.ValidationResourceString(issue.Message);
                            }
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, allValidationIssues));
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, this.GlobalResourceString(e.Message)));
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage(Strings.MVC.MoveLotsAction, MessageType.Method);
                }
            }

            if (attemptedCount == 0)
            {
                PrepareErrorMessage(this.GlobalResourceString("YouMustSelectAtLeastOneLotToCopy"), MessageType.Message);
            }
            else if (createdCount < attemptedCount)
            {
                PrepareErrorMessage(this.GlobalResourceString("xNotCreated", (attemptedCount - createdCount)), MessageType.Message);
            }

            if (createdCount > 0)
            {
                //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyCreated", createdCount), MessageType.Message);
                ViewData["SelectedNavAction"] = Strings.MVC.ListingsDraftsAction;
                //return ListingsDrafts(0, //page = 0
                //                        8, // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
                //                        null, null);
                //return LotsByEvent(ToEventID.Value, 
                //                   0, // first page
                //                   8, // Sort = "LotOrder", Name = "LotOrderLowHigh", Descending = false
                //                   "All", null, null);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(Strings.MVC.LotsByEventAction, new { @id = ToEventID, @page = 0, @SortFilterOptions = 8 });
            }

            //retrieve source event
            var auctionEvent = EventClient.GetEventByIDWithFillLevel(User.Identity.Name, FromEventID.Value, EventFillLevels.None);
            if (auctionEvent == null)
            {
                //event does not exist
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else if (!auctionEvent.OwnerUserName.Equals(this.FBOUserName(), StringComparison.OrdinalIgnoreCase) && !User.IsInRole("Admin"))
            {
                //logged on user doesn't own this event and is also not an admin
                PrepareErrorMessage("EventNotFound", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }
            else
            {
                ViewData["Event"] = auctionEvent;
                SetHighlightedNavCatByEventStatus(auctionEvent.Status);
            }

            //retrieve list of eligible events
            string statuses = Strings.AuctionEventStatuses.Draft;
            var eligibleEvents = EventClient.GetEventsByOwnerAndStatusWithFillLevel(User.Identity.Name, this.FBOUserName(), statuses,
                0, 0, Strings.Fields.Title, false, EventFillLevels.None).List.Where(ev => ev.ID != FromEventID.Value).ToList();
            if (eligibleEvents.Count == 0)
            {
                string retUrl = this.GetActionUrl(Strings.MVC.CopyLotsAction, Strings.MVC.AccountController, new { FromEventID, ToEventID, returnUrl });
                return RedirectToAction(Strings.MVC.EventNeededAction, Strings.MVC.EventController, new { @returnUrl = retUrl });
            }
            List<SelectListItem> formattedOptionList = new List<SelectListItem>(eligibleEvents.Count);
            foreach (var ev in eligibleEvents)
            {
                if (ev.ID == 0)
                {
                    formattedOptionList.Add(new SelectListItem() { Text = ev.Title, Value = ev.ID.ToString() });
                }
                else
                {
                    formattedOptionList.Add(new SelectListItem() { Text = string.Format("{0} ({1})", ev.Title, ev.ID), Value = ev.ID.ToString() });
                }
            }
            ViewData[Fields.ToEventID] = new SelectList(formattedOptionList, "Value", "Text", ToEventID ?? 0);

            ViewData["LotAction"] = "Move";
            return View(Strings.MVC.CopyLotsAction, listings);

        }

        /// <summary>
        /// Processes request to delete multiple unsuccessful listings at once
        /// </summary>
        /// <param name="FromEventID">ID of the source event</param>
        /// <param name="ListingIDs">ID or IDs of unsuccessful listings to be deleted</param>
        /// <param name="returnUrl">Url to redirect to (default unsuccessul listings view if missing)</param>
        /// <returns>Redirect to current fees view (if immediate payment is required) or "returnUrl", otherwise</returns>
        [Authorize]
        public ActionResult DeleteLots(int? FromEventID, string[] ListingIDs, string returnUrl)
        {
            if (!FromEventID.HasValue)
            {
                PrepareErrorMessage("MissingSourceEventID", MessageType.Message);
                return RedirectToAction(Strings.MVC.SummaryAction);
            }

            string allIds = string.Join(",", ListingIDs);
            int deletedCount = 0;
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int listingId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        var listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, listingId, ListingFillLevels.LotEvent);
                        EventClient.DeleteLot(User.Identity.Name, listing.Lot.ID);
                        deletedCount++;
                    }
                    catch (Exception)
                    {
                        PrepareErrorMessage("DeleteListings", MessageType.Method);
                    }
                }
            }
            //normally success messages are not pre-localized, but this one needs to be because it requires an argument
            PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyDeleted", deletedCount), MessageType.Message);
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.LotsByEventAction, new { @id = FromEventID });
        }

        /// <summary>
        /// Processes request to copy multiple listings as drafts
        /// </summary>
        /// <param name="ListingIDs">ID or IDs of listings to be activated</param>
        /// <param name="returnUrl">URL to return user upon successful copy operation</param>
        [Authorize]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CopyListings(string[] ListingIDs, string returnUrl)
        {
            ViewData["returnUrl"] = returnUrl;

            string actingUserName = User.Identity.Name;
            string cultureCode = this.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture; // culture, e.g. "en-US"
            string allIds = ListingIDs != null ? string.Join(",", ListingIDs) : null;
            int createdCount = 0;
            int attemptedCount = 0;

            //default values
            bool isTaxable = !SiteClient.BoolSetting(SiteProperties.HideTaxFields);

            if (!string.IsNullOrEmpty(allIds))
            {
                try
                {
                    var selectedIds = allIds.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => int.Parse(s));
                    foreach (int listingId in selectedIds)
                    {
                        Listing listing = ListingClient.GetListingByIDWithFillLevel(User.Identity.Name, listingId, ListingFillLevels.All);
                        attemptedCount++;
                        try
                        {
                            var input = new UserInput(actingUserName, listing.OwnerUserName, cultureCode, cultureCode);
                            input.FillInputFromListing(listing);

                            input.Items.Add(Fields.SaveAsDraft, "True");

                            input.Items.Add(Fields.CategoryID, listing.PrimaryCategory.ID.ToString());
                            string allCatIds = string.Join(",", listing.Categories.Where(c => c.Type != CategoryTypes.Event).Select(c => c.ID.ToString()));
                            input.Items.Add(Fields.AllCategories, allCatIds);

                            input.Items.Add(Fields.ListingType, listing.Type.Name);

                            if (input.Items.ContainsKey(Fields.BuyItNow))
                                input.Items.Remove(Fields.BuyItNow);
                            if (input.Items.ContainsKey(Fields.BuyItNowUsed))
                                input.Items.Remove(Fields.BuyItNowUsed);

                            if (!input.Items.ContainsKey(Fields.IsTaxable) || string.IsNullOrWhiteSpace(input.Items[Fields.IsTaxable]))
                            {
                                input.Items[Fields.IsTaxable] = isTaxable.ToString();
                            }

                            int newListingId;
                            ListingClient.CreateListing(actingUserName, input, false, out newListingId);
                            createdCount++;
                        }
                        catch (FaultException<ValidationFaultContract> vfc)
                        {
                            string allValidationIssues = string.Empty;
                            foreach (var issue in vfc.Detail.ValidationIssues)
                            {
                                allValidationIssues += (!string.IsNullOrEmpty(allValidationIssues) ? "; " : string.Empty);
                                allValidationIssues += this.ValidationResourceString(issue.Message);
                            }
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, allValidationIssues));
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError(Strings.MVC.FormModelErrorKey,
                                string.Format("({0}{1}) {2}", this.GlobalResourceString("LotNumber"), listing.Lot.LotNumber, this.GlobalResourceString(e.Message)));
                        }
                    }
                }
                catch
                {
                    PrepareErrorMessage(Strings.MVC.CopyLotsAction, MessageType.Method);
                }
            }

            if (attemptedCount == 0)
            {
                PrepareErrorMessage(this.GlobalResourceString("YouMustSelectAtLeastOneListingToCopy"), MessageType.Message);
            }
            else if (createdCount < attemptedCount)
            {
                PrepareErrorMessage(this.GlobalResourceString("xNotCreated", (attemptedCount - createdCount)), MessageType.Message);
            }

            if (createdCount > 0)
            {
                //normally success messages are not pre-localized, but this one needs to be because it requires an argument
                if (createdCount == 1)
                {
                    PrepareSuccessMessage(this.GlobalResourceString("OneListingCopiedAsADraft", createdCount), MessageType.Message);
                }
                else
                {
                    PrepareSuccessMessage(this.GlobalResourceString("xListingsCopiedAsDrafts", createdCount), MessageType.Message);
                }
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsDraftsAction);
        }

        #endregion Bulk Listing/Lot Copying/Moving/Deleting

        #region Delete Listings

        /// <summary>
        /// Processes request to delete multiple unsuccessful listings at once
        /// </summary>
        /// <param name="ListingIDs">ID or IDs of unsuccessful listings to be deleted</param>
        /// <param name="page">Current page of listings</param>
        /// <param name="SortFilterOptions">Current sort option of listings</param>
        /// <param name="returnUrl">Url to redirect to (default unsuccessul listings view if missing)</param>
        /// <returns>Redirect to current fees view (if immediate payment is required) or "returnUrl", otherwise</returns>
        [Authorize]
        public ActionResult DeleteListings(string[] ListingIDs, int? page, int? SortFilterOptions, string returnUrl)
        {
            string allIds = string.Join(",", ListingIDs);
            int deletedCount = 0;
            if (!String.IsNullOrEmpty(allIds))
            {
                foreach (int listingId in allIds.Split(',').Select(s => int.Parse(s)))
                {
                    try
                    {
                        ListingClient.DeleteListing(User.Identity.Name, listingId);
                        deletedCount++;
                    }
                    catch (Exception)
                    {
                        PrepareErrorMessage("DeleteListings", MessageType.Method);
                    }
                }
            }
            //normally success messages are not pre-localized, but this one needs to be because it requires an argument
            PrepareSuccessMessage(this.GlobalResourceString("xSuccessfullyDeleted", deletedCount), MessageType.Message);
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.ListingsUnsuccessfulAction, new { page, SortFilterOptions });
        }

        #endregion Delete Listings

        #region Helpers

        /// <summary>
        /// disposes all instances of applicable private members
        /// </summary>
        /// <param name="disposing">true indicates that it is safe to access disposable members</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        #endregion Helpers

    }
}
