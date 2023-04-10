using System;
using System.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.MVC.Helpers;
using MvcSiteMap.Core;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.SiteMap;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.Strings;
using Stripe;
using System.IO;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to all MVC requests not specific to admin, account or listing areas
    /// </summary>
    public class StripeController : AuctionWorxController
    {
        #region Stripe.com

        /// <summary>
        /// Processes request to connect a new Stripe account
        /// </summary>
        /// <param name="code">authorization code returned by Stripe</param>
        /// <param name="scope">&quot;read_only&quot; or &quot;read_write&quot;</param>
        /// <param name="state">optional fraud prevention string</param>
        /// <param name="error"></param>
        /// <param name="error_description"></param>
        /// <returns></returns>
        public ActionResult HandleStripeConnectResponse(string code, string scope, string state, string error, string error_description)
        {
            string actingUN = SystemActors.SystemUserName; // User.Identity.Name;
            string fboUN = this.FBOUserName();
            if (string.IsNullOrEmpty(error))
            {
                //success -- update user account as needed
                var stripeOAuthTokenService = new StripeOAuthTokenService(SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey));
                var _stripeOAuthTokenCreateOptions = new StripeOAuthTokenCreateOptions()
                {
                    ClientSecret = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey),
                    Code = code,
                    GrantType = "authorization_code",
                    Scope = scope
                };
                try
                {
                    StripeOAuthToken stripeOAuthToken = stripeOAuthTokenService.Create(_stripeOAuthTokenCreateOptions);

                    var allUserProps = UserClient.Properties(actingUN, fboUN);
                    var propsToUpdate = allUserProps.Where(p =>
                        p.Field.Name == StdUserProps.StripeConnect_SellerAccountConnected ||
                        p.Field.Name == StdUserProps.StripeConnect_SellerUserId ||
                        p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey ||
                        p.Field.Name == StdUserProps.StripeConnect_SellerPublishableApiKey ||
                        p.Field.Name == StdUserProps.AcceptCreditCard).ToList();

                    var input = new UserInput(actingUN, fboUN, this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                    input.Items.Add(StdUserProps.StripeConnect_SellerAccountConnected, "True");
                    input.Items.Add(StdUserProps.StripeConnect_SellerUserId, stripeOAuthToken.StripeUserId);
                    input.Items.Add(StdUserProps.StripeConnect_SellerSecretApiKey, stripeOAuthToken.AccessToken);
                    input.Items.Add(StdUserProps.StripeConnect_SellerPublishableApiKey, stripeOAuthToken.StripePublishableKey);
                    input.Items.Add(StdUserProps.AcceptCreditCard, "True");

                    //ValidateUserPropertyValues(propsToUpdate, input);

                    UserClient.UpdateProperties(actingUN, fboUN, propsToUpdate, input);

                    LogManager.WriteLog(string.Format("Seller: {0}", fboUN), "Account Connected", "Stripe", TraceEventType.Information, User.Identity.Name, null,
                        new Dictionary<string, object>() {
                            { "user", fboUN }
                        });
                    PrepareSuccessMessage("HandleStripeConnectResponse", MessageType.Method);
                }
                catch (Exception e)
                {
                    //handle error
                    LogManager.WriteLog(null, "Error Connecting Account", "Stripe", TraceEventType.Error, User.Identity.Name, e,
                        new Dictionary<string, object>() {
                            { "User", fboUN }
                        });
                    PrepareErrorMessage(ReasonCode.StripeAccountConnectionFailed);
                }
            }
            else
            {
                //handle error
                LogManager.WriteLog("Remote Error", "Account Connection Failed", "Stripe", TraceEventType.Error, User.Identity.Name, null,
                    new Dictionary<string, object>() {
                        { "User", fboUN },
                        { "error", error ?? string.Empty },
                        { "error_description", error_description ?? string.Empty } });
                PrepareErrorMessage(ReasonCode.StripeAccountConnectionFailed);
            }
            return RedirectToAction(Strings.MVC.PropertyManagementAction, Strings.MVC.AccountController, new { @id = 44902 }); // My Account > Listing Preferences > Payment
        }

        /// <summary>
        /// Processes request to copy site fees Stripe credentials for use with sales payments to this user account
        /// </summary>
        /// <param name="targetUserName">optional, if not specified then the impersonated username will be used, otherwise the logged in username</param>
        /// <param name="returnUrl">optional, url to redirect after action is completed</param>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult CopySiteFeesStripeCredentials(string targetUserName, string returnUrl)
        {
            try
            {
                string actingUN = SystemActors.SystemUserName; // User.Identity.Name;
                string fboUN = string.IsNullOrWhiteSpace(targetUserName) ? this.FBOUserName() : targetUserName;
                var allUserProps = UserClient.Properties(actingUN, fboUN);
                var propsToUpdate = allUserProps.Where(p =>
                    p.Field.Name == StdUserProps.StripeConnect_SellerAccountConnected ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerUserId ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerPublishableApiKey ||
                    p.Field.Name == StdUserProps.AcceptCreditCard).ToList();

                var input = new UserInput(actingUN, fboUN, this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.Items.Add(StdUserProps.StripeConnect_SellerAccountConnected, "True");
                input.Items.Add(StdUserProps.StripeConnect_SellerUserId, string.Empty);
                input.Items.Add(StdUserProps.StripeConnect_SellerSecretApiKey, SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey));
                input.Items.Add(StdUserProps.StripeConnect_SellerPublishableApiKey, SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesPublishableApiKey));
                input.Items.Add(StdUserProps.AcceptCreditCard, "True");

                //ValidateUserPropertyValues(propsToUpdate, input);

                UserClient.UpdateProperties(actingUN, fboUN, propsToUpdate, input);

                PrepareSuccessMessage("CopySiteFeesStripeCredentials", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("CopySiteFeesStripeCredentials", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.PropertyManagementAction, Strings.MVC.AccountController, new { id = 44902 });
        }

        /// <summary>
        /// Processes request to remove site fees Stripe credentials for use with sales payments to this user account
        /// </summary>
        /// <param name="targetUserName">optional, if not specified then the impersonated username will be used, otherwise the logged in username</param>
        /// <param name="returnUrl">optional, url to redirect after action is completed</param>
        [Authorize(Roles = Strings.Roles.Admin)]
        public ActionResult DisconnectSiteFeesStripeCredentials(string targetUserName, string returnUrl)
        {
            try
            {
                string actingUN = SystemActors.SystemUserName; // User.Identity.Name;
                string fboUN = string.IsNullOrWhiteSpace(targetUserName) ? this.FBOUserName() : targetUserName;
                var allUserProps = UserClient.Properties(actingUN, fboUN);
                var propsToUpdate = allUserProps.Where(p =>
                    p.Field.Name == StdUserProps.StripeConnect_SellerAccountConnected ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerUserId ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey ||
                    p.Field.Name == StdUserProps.StripeConnect_SellerPublishableApiKey ||
                    p.Field.Name == StdUserProps.AcceptCreditCard).ToList();

                var input = new UserInput(actingUN, fboUN, this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                input.Items.Add(StdUserProps.StripeConnect_SellerAccountConnected, "False");
                input.Items.Add(StdUserProps.StripeConnect_SellerUserId, string.Empty);
                input.Items.Add(StdUserProps.StripeConnect_SellerSecretApiKey, string.Empty);
                input.Items.Add(StdUserProps.StripeConnect_SellerPublishableApiKey, string.Empty);
                input.Items.Add(StdUserProps.AcceptCreditCard, "False");

                //ValidateUserPropertyValues(propsToUpdate, input);

                UserClient.UpdateProperties(actingUN, fboUN, propsToUpdate, input);

                PrepareSuccessMessage("DisconnectSiteFeesStripeCredentials", MessageType.Method);
            }
            catch (Exception)
            {
                PrepareErrorMessage("DisconnectSiteFeesStripeCredentials", MessageType.Method);
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.PropertyManagementAction, Strings.MVC.AccountController, new { id = 44902 });
        }

        /// <summary>
        /// Handle stripe.com webhook events
        /// </summary>
        public string WebHookHandler()
        {
            var json = new StreamReader(Request.InputStream).ReadToEnd();
            try
            {
                var stripeEvent = StripeEventUtility.ParseEvent(json);
                switch (stripeEvent.Type)
                {
                    //case StripeEvents.ChargeRefunded:  // all of the types available are listed in StripeEvents
                    //    var stripeCharge = Stripe.Mapper<StripeCharge>.MapFromJson(stripeEvent.Data.Object.ToString());
                    //    break;
                    case StripeEvents.AccountApplicationDeauthorized:
                        try
                        {
                            string stripeUserId = stripeEvent.Account;
                            if (!string.IsNullOrWhiteSpace(stripeUserId))
                            {
                                string actingUN = SystemActors.SystemUserName; // User.Identity.Name;
                                var usersToDeAuthorize = UserClient.FindUsersByCustomProperty(actingUN, StdUserProps.StripeConnect_SellerUserId, stripeUserId);
                                foreach(var user in usersToDeAuthorize)
                                {
                                    string fboUN = user.UserName;
                                    var propsToUpdate = user.Properties.Where(p =>
                                        p.Field.Name == StdUserProps.StripeConnect_SellerAccountConnected ||
                                        p.Field.Name == StdUserProps.StripeConnect_SellerUserId ||
                                        p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey ||
                                        p.Field.Name == StdUserProps.StripeConnect_SellerPublishableApiKey ||
                                        p.Field.Name == StdUserProps.AcceptCreditCard).ToList();

                                    var input = new UserInput(actingUN, fboUN, this.GetCookie(Strings.MVC.CultureCookie), this.GetCookie(Strings.MVC.CultureCookie));
                                    input.Items.Add(StdUserProps.StripeConnect_SellerAccountConnected, "False");
                                    input.Items.Add(StdUserProps.StripeConnect_SellerUserId, string.Empty);
                                    input.Items.Add(StdUserProps.StripeConnect_SellerSecretApiKey, string.Empty);
                                    input.Items.Add(StdUserProps.StripeConnect_SellerPublishableApiKey, string.Empty);
                                    input.Items.Add(StdUserProps.AcceptCreditCard, "False");

                                    //ValidateUserPropertyValues(propsToUpdate, input);

                                    UserClient.UpdateProperties(actingUN, fboUN, propsToUpdate, input);

                                    LogManager.WriteLog(string.Format("Seller: {0}", fboUN), "Account Disconnected", "Stripe", TraceEventType.Information, null, null,
                                        new Dictionary<string, object>() {
                                            { "Stripe User ID", stripeUserId },
                                            { "request data", ToEncodedJSON(json) }
                                        });
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            LogManager.WriteLog(null, "Webhook Handler: Deauthorize Error", "Stripe", TraceEventType.Error, null, e,
                                new Dictionary<string, object>() {
                                    { "request data", ToEncodedJSON(json)}
                                });
                        }
                        break;
                    default:
                        //ignore unexpected but valid web hooks silently...
                        //
                        //LogManager.WriteLog("Unhandled Stripe Event", "STRIPE TESTING (2): Unknown Event", "StripeController.Handler", TraceEventType.Information, null, null,
                        //    new Dictionary<string, object>()
                        //    {
                        //        { "raw JSON", ToEncodedJSON(json)},
                        //        { "stripeEvent.Type", stripeEvent.Type }
                        //    });
                        break;
                }
            }
            catch (Exception e)
            {
                //e.g. someone visited this action manually, or any hacking attempt
                LogManager.WriteLog(null, "Webhook Handler: Error Parsing Event", "Stripe", TraceEventType.Error, null, e,
                    new Dictionary<string, object>() {
                        { "request data", ToEncodedJSON(json) }
                    });
            }
            return string.Empty;
        }

        private string ToEncodedJSON(string rawJSON)
        {
            return rawJSON.Replace("{", "@'").Replace("}", "'@").Replace("\"", "''");
        }

        /// <summary>
        /// Displays form and processes request to add new stripe card
        /// </summary>
        /// <param name="sellerID">The integer User ID of the seller, or null to associate this card with the site fee stripe credentials</param>
        /// <param name="stripeToken">the token generated by stripe to reference the card details entered by the user</param>
        /// <param name="cardID">the stripe ID of the specified card</param>
        /// <param name="returnUrl">optional url to redirect after action is completed</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public ActionResult AddCard(int? sellerID, string stripeToken, string cardID, string returnUrl)
        {
            try
            {

                //first get api key based on sellerID
                string sellerUN;
                string apiKey;
                string stripeCustomerId;
                if (sellerID.HasValue && sellerID.Value > 0)
                {
                    var seller = UserClient.GetUserByID(SystemActors.SystemUserName, sellerID.Value);
                    sellerUN = seller.UserName;
                    stripeCustomerId = AccountingClient.GetStripeCustomerId(User.Identity.Name, seller.UserName, this.FBOUserName());
                    apiKey = seller.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        apiKey = Utilities.DecryptString(apiKey);
                    }
                }
                else
                {
                    sellerUN = null;
                    stripeCustomerId = AccountingClient.GetStripeCustomerId(User.Identity.Name, null, this.FBOUserName());
                    apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
                }

                if (string.IsNullOrWhiteSpace(stripeCustomerId))
                {
                    var buyer = UserClient.GetUserByUserName(User.Identity.Name, this.FBOUserName());
                    //saving first card, create custromer record
                    var myCustomer = new StripeCustomerCreateOptions();
                    myCustomer.Email = buyer.Email;
                    myCustomer.Description = string.Format("{0} {1} ({2})", buyer.FirstName(), buyer.LastName(), buyer.Email);

                    myCustomer.SourceToken = stripeToken;

                    //myCustomer.PlanId = *planId *;                          // only if you have a plan
                    //myCustomer.TaxPercent = 20;                            // only if you are passing a plan, this tax percent will be added to the price.
                    //myCustomer.Coupon = *couponId *;                        // only if you have a coupon
                    //myCustomer.TrialEnd = DateTime.UtcNow.AddMonths(1);    // when the customers trial ends (overrides the plan if applicable)
                    //myCustomer.Quantity = 1;                               // optional, defaults to 1

                    var customerService = new StripeCustomerService(apiKey);
                    StripeCustomer stripeCustomer = customerService.Create(myCustomer);
                    stripeCustomerId = stripeCustomer.Id;
                    AccountingClient.SetStripeCustomerId(User.Identity.Name, sellerUN, buyer.UserName, stripeCustomerId);

                    var cardService = new StripeCardService(apiKey);
                    IEnumerable<StripeCard> stripeCards = cardService.List(stripeCustomerId);
                    //selectedStripeCardId = stripeCards.First().Id;
                }
                else
                {
                    //customer records exists, add card to it
                    var myCard = new StripeCardCreateOptions();

                    myCard.SourceToken = stripeToken;

                    var cardService = new StripeCardService(apiKey);
                    StripeCard newStripeCard = cardService.Create(stripeCustomerId, myCard); // optional isRecipient
                    //selectedStripeCardId = stripeCard.Id;

                    //find any old cards and delete them, if applicable
                    foreach(var existingStripeCard in cardService.List(stripeCustomerId))
                    {
                        if (existingStripeCard.Id != newStripeCard.Id)
                        {
                            cardService.Delete(stripeCustomerId, existingStripeCard.Id);
                        }
                    }

                }
                string recipientUserName = sellerUN;
                if (apiKey == SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey))
                {
                    recipientUserName = null;
                }
                string payerUserName = this.FBOUserName();
                string cacheKey = string.Format("UnexpStripeCard_{0}_{1}", recipientUserName, payerUserName);
                SiteClient.SetCacheData(cacheKey, true, 1440); // 1440 minutes = 24 hours

                PrepareSuccessMessage("AddCard", MessageType.Method);
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error Adding Card", "Stripe", TraceEventType.Error, User.Identity.Name, e);
                PrepareErrorMessage("AddCard", e);
                return RedirectToAction(Strings.MVC.AddStripeCardAction, Strings.MVC.AccountController, new { sellerID, returnUrl });
            }
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.StripeCardManagementAction);
        }

        /// <summary>
        /// Processes request to delete the specified card stored on stripe.com associated with the specified invoice or seller
        /// </summary>
        /// <param name="invoiceID">the id of the specified invoice</param>
        /// <param name="SellerID">the id of the specified seller</param>
        /// <param name="cardID">the stripe ID of the specified card</param>
        /// <param name="returnUrl">optional url to redirect after action is completed</param>
        [Authenticate]
        public ActionResult DeleteCard(int? invoiceID, int? SellerID, string cardID, string returnUrl)
        {
            string actingUN = User.Identity.Name;
            string fboUN = this.FBOUserName();
            string apiKey = null;
            string stripeCustomerId = null;

            string recipientUserName;
            string payerUserName = fboUN;

            try
            {
                if (invoiceID.HasValue)
                {
                    var invoice = AccountingClient.GetInvoiceByID(actingUN, invoiceID.Value);
                    if (invoice.Type == InvoiceTypes.Fee)
                    {
                        stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUN, null, invoice.Payer.UserName);
                        apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
                        recipientUserName = null;
                    }
                    else
                    {
                        stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUN, invoice.Owner.UserName, invoice.Payer.UserName);
                        apiKey = invoice.Owner.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                        if (!string.IsNullOrWhiteSpace(apiKey))
                        {
                            apiKey = Utilities.DecryptString(apiKey);
                        }
                        recipientUserName = invoice.Owner.UserName;
                    }
                }
                else if (SellerID.HasValue && SellerID.Value > 0)
                {
                    //sellerid specified
                    var seller = UserClient.GetUserByID(SystemActors.SystemUserName, SellerID.Value);
                    stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUN, seller.UserName, fboUN);
                    apiKey = seller.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        apiKey = Utilities.DecryptString(apiKey);
                    }
                    recipientUserName = seller.UserName;
                }
                else
                {
                    //neither invoice nor seller specified, assume site fee credentials
                    stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUN, null, fboUN);
                    apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
                    recipientUserName = null;
                }
                if (apiKey == SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey))
                {
                    recipientUserName = null;
                }

                var cardService = new StripeCardService(apiKey);
                cardService.Delete(stripeCustomerId, cardID);
                PrepareSuccessMessage("DeleteCard", MessageType.Method);
                LogManager.WriteLog(string.Format("Payer: {0}", fboUN), "Card Deleted", "Stripe", TraceEventType.Information, actingUN, null,
                    new Dictionary<string, object>() {
                        { "invoiceId", invoiceID },
                        { "stripeCustomerId", stripeCustomerId},
                        { "stripeCardId", cardID }
                    });

                string cacheKey = string.Format("UnexpStripeCard_{0}_{1}", recipientUserName, payerUserName);
                SiteClient.RemoveCacheData(cacheKey);
            }
            catch (Exception e)
            {
                PrepareErrorMessage("DeleteCard", MessageType.Method);
                LogManager.WriteLog(null, "Error Deleting Card", "Stripe", TraceEventType.Error, actingUN, e,
                    new Dictionary<string, object>() {
                        { "invoiceId", invoiceID },
                        { "User", fboUN },
                        { "stripeCustomerId", stripeCustomerId},
                        { "stripeCardId", cardID }
                    });
            }

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(Strings.MVC.InvoiceDetailAction, Strings.MVC.AccountController, new { @id = invoiceID });
        }

        #endregion Stripe.com

    }
}
