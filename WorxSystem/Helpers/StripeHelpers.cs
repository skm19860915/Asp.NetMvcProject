using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Compilation;
using System.Web.Mvc;
using System;
using System.Web.WebPages;
using RainWorx.FrameWorx.Clients;
using System.Text;
using System.Linq;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Strings;
using Stripe;
using System.Diagnostics;
using RainWorx.FrameWorx.Utility;

namespace RainWorx.FrameWorx.MVC.Helpers
{
    /// <summary>
    /// Provides methods for accessing stripe data
    /// </summary>
    public static class StripeHelpers
    {

        #region stripe helpers

        /// <summary>
        /// Calls out to stripe.com API to retrieve saved cards available for the specified invoice
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="invoice">the specified DTO.invoice object</param>
        /// <returns></returns>
        public static IEnumerable<StripeCard> GetStripeCards(this HtmlHelper htmlHelper, Invoice invoice)
        {
            string apiKey;
            string stripeCustomerId = null;
            if (invoice.Type == InvoiceTypes.Fee)
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(htmlHelper.ViewContext.HttpContext.User.Identity.Name, null, invoice.Payer.UserName);
                apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
            }
            else
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(htmlHelper.ViewContext.HttpContext.User.Identity.Name, invoice.Owner.UserName, invoice.Payer.UserName);
                apiKey = invoice.Owner.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = Utilities.DecryptString(apiKey);
                }
            }
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                return new List<StripeCard>();
            }
            try
            {
                var cardService = new StripeCardService(apiKey);
                return cardService.List(stripeCustomerId).ToList();
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error Retrieving Cards (1)", "Stripe", TraceEventType.Warning, htmlHelper.ViewContext.HttpContext.User.Identity.Name, e,
                    new Dictionary<string, object>() {
                        { "invoiceId", invoice == null ? -1 : invoice.ID },
                        { "User", invoice == null ? null : invoice.Payer.UserName },
                        { "stripeCustomerId", stripeCustomerId}
                    });
                if (e.Message.Contains("No such customer"))
                {
                    //this buyer has a card saved in test mode, but the site has transitioned to live mode
                    try
                    {
                        AccountingClient.SetStripeCustomerId(htmlHelper.ViewContext.HttpContext.User.Identity.Name, invoice.Owner.UserName, invoice.Payer.UserName, null);
                        LogManager.WriteLog("Successfully removed test Customer ID (1)", "Test Data Cleanup", "Stripe", TraceEventType.Information, htmlHelper.ViewContext.HttpContext.User.Identity.Name, null,
                            new Dictionary<string, object>() {
                                { "invoiceId", invoice == null ? -1 : invoice.ID },
                                { "User", invoice == null ? null : invoice.Payer.UserName },
                                { "stripeCustomerId", stripeCustomerId}
                            });
                    }
                    catch (Exception removeError)
                    {
                        LogManager.WriteLog(null, "Error Removing Test Data (1)", "Stripe", TraceEventType.Warning, htmlHelper.ViewContext.HttpContext.User.Identity.Name, removeError,
                            new Dictionary<string, object>() {
                                { "invoiceId", invoice == null ? -1 : invoice.ID },
                                { "User", invoice == null ? null : invoice.Payer.UserName },
                                { "stripeCustomerId", stripeCustomerId}
                            });
                    }
                }
                return new List<StripeCard>();
            }
        }

        /// <summary>
        /// Calls out to stripe.com API to retrieve saved cards available for the specified invoice
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="invoice">the specified DTO.invoice object</param>
        /// <returns></returns>
        public static IEnumerable<StripeCard> GetStripeCards(this Controller controller, Invoice invoice)
        {
            string apiKey;
            string stripeCustomerId;
            if (invoice.Type == InvoiceTypes.Fee)
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(controller.User.Identity.Name, null, invoice.Payer.UserName);
                apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
            }
            else
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(controller.User.Identity.Name, invoice.Owner.UserName, invoice.Payer.UserName);
                apiKey = invoice.Owner.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = Utilities.DecryptString(apiKey);
                }
            }
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                return new List<StripeCard>();
            }
            try
            {
                var cardService = new StripeCardService(apiKey);
                return cardService.List(stripeCustomerId).ToList();
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error Retrieving Cards (2)", "Stripe", TraceEventType.Warning, controller.HttpContext.User.Identity.Name, e,
                    new Dictionary<string, object>() {
                        { "invoiceId", invoice == null ? -1 : invoice.ID },
                        { "User", invoice == null ? null : invoice.Payer.UserName },
                        { "stripeCustomerId", stripeCustomerId}
                    });
                if (e.Message.Contains("No such customer"))
                {
                    //this buyer has a card saved in test mode, but the site has transitioned to live mode
                    try
                    {
                        AccountingClient.SetStripeCustomerId(controller.User.Identity.Name, invoice.Owner.UserName, invoice.Payer.UserName, null);
                        LogManager.WriteLog("Successfully removed test Customer ID (2)", "Test Data Cleanup", "Stripe", TraceEventType.Information, controller.User.Identity.Name, null,
                            new Dictionary<string, object>() {
                                { "invoiceId", invoice == null ? -1 : invoice.ID },
                                { "User", invoice == null ? null : invoice.Payer.UserName },
                                { "stripeCustomerId", stripeCustomerId}
                            });
                    }
                    catch (Exception removeError)
                    {
                        LogManager.WriteLog(null, "Error Removing Test Data (2)", "Stripe", TraceEventType.Warning, controller.User.Identity.Name, removeError,
                            new Dictionary<string, object>() {
                                { "invoiceId", invoice == null ? -1 : invoice.ID },
                                { "User", invoice == null ? null : invoice.Payer.UserName },
                                { "stripeCustomerId", stripeCustomerId}
                            });
                    }
                }
                return new List<StripeCard>();
            }
        }

        /// <summary>
        /// Calls out to stripe.com API to retrieve saved cards available for the specified invoice
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <returns>a list of stripe cards</returns>
        public static Dictionary<int, List<StripeCard>> GetAllStripeCards(this Controller controller)
        {
            var results = new Dictionary<int, List<StripeCard>>();
            var allCustomerIds = AccountingClient.GetAllStripeCustomerIds(controller.User.Identity.Name, controller.FBOUserName());
            foreach (int sellerId in allCustomerIds.Keys)
            {
                string stripeCustomerId = allCustomerIds[sellerId];
                string apiKey;
                string recipientUN = null;
                string payerUN = controller.FBOUserName();
                if (sellerId == 0)
                {
                    apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
                }
                else
                {
                    var seller = UserClient.GetUserByID(SystemActors.SystemUserName, sellerId);
                    apiKey = seller.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        apiKey = Utilities.DecryptString(apiKey);
                    }
                    if (seller != null)
                    {
                        recipientUN = seller.UserName;
                    }
                }
                try
                {
                    var cardService = new StripeCardService(apiKey);
                    results.Add(sellerId, cardService.List(stripeCustomerId).ToList());
                }
                catch (Exception e)
                {
                    LogManager.WriteLog(null, "Error Retrieving Cards (3)", "Stripe", TraceEventType.Warning, controller.HttpContext.User.Identity.Name, e,
                        new Dictionary<string, object>() {
                            { "User", controller.FBOUserName() },
                            { "stripeCustomerId", stripeCustomerId}
                        });
                    if (e.Message.Contains("No such customer"))
                    {
                        //this buyer has a card saved in test mode, but the site has transitioned to live mode
                        try
                        {
                            AccountingClient.SetStripeCustomerId(controller.User.Identity.Name, recipientUN, payerUN, null);
                            LogManager.WriteLog("Successfully removed test Customer ID (3)", "Test Data Cleanup", "Stripe", TraceEventType.Information, controller.User.Identity.Name, null,
                                new Dictionary<string, object>() {
                                    { "User", payerUN },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                        catch (Exception removeError)
                        {
                            LogManager.WriteLog(null, "Error Removing Test Data (3)", "Stripe", TraceEventType.Warning, controller.User.Identity.Name, removeError,
                                new Dictionary<string, object>() {
                                    { "User", payerUN },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                    }
                }
            }
            return results;
        }

        //public static DTO.Address ToDTOAddress(this StripeCard stripeCard)
        //{
        //    var result = new DTO.Address()
        //    {
        //        Street1 = stripeCard.AddressLine1,
        //        Street2 = stripeCard.AddressLine2,
        //        City = stripeCard.AddressCity,
        //        StateRegion = stripeCard.AddressState,
        //        ZipPostal = stripeCard.AddressZip,
        //        Country = SiteClient.Countries.FirstOrDefault(ctry => ctry.Code == stripeCard.Country),
        //        FirstName = stripeCard.Name,
        //        LastName = string.Empty
        //    };
        //    return result;
        //}

        /// <summary>
        /// Generates a new stripe customer id using the site fees api key from the specified stripe token and associates it with the specified username
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="userName">the specified username</param>
        /// <param name="stripeToken">the specified stripe token</param>
        public static void SaveStripeCard(this Controller controller, string userName, string stripeToken)
        {
            var user = UserClient.GetUserByUserName(userName, userName);

            string apiKey;
            apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);

            //saving first card, create custromer record
            var myCustomer = new StripeCustomerCreateOptions();
            myCustomer.Email = user.Email;
            myCustomer.Description = string.Format("{0} {1} ({2})", user.FirstName(), user.LastName(), user.Email);

            myCustomer.SourceToken = stripeToken;

            //myCustomer.PlanId = *planId *;                          // only if you have a plan
            //myCustomer.TaxPercent = 20;                            // only if you are passing a plan, this tax percent will be added to the price.
            //myCustomer.Coupon = *couponId *;                        // only if you have a coupon
            //myCustomer.TrialEnd = DateTime.UtcNow.AddMonths(1);    // when the customers trial ends (overrides the plan if applicable)
            //myCustomer.Quantity = 1;                               // optional, defaults to 1

            var customerService = new StripeCustomerService(apiKey);
            StripeCustomer stripeCustomer = customerService.Create(myCustomer);
            string stripeCustomerId = stripeCustomer.Id;
            AccountingClient.SetStripeCustomerId(userName, null, userName, stripeCustomerId);

            string cacheKey = string.Format("UnexpStripeCard_{0}_{1}", null, userName);
            SiteClient.RemoveCacheData(cacheKey);
        }

        /// <summary>
        /// Calls out to stripe.com API to retrieve saved cards and returns true if an unexpired card is found
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="recipientUserName">the username of the recipient, or null for site fees</param>
        /// <param name="payerUserName">the username of the paying user</param>
        public static bool HasUnexpiredStripeCardOnFile(this Controller controller, string recipientUserName, string payerUserName)
        {
            string apiKey;
            string stripeCustomerId;
            if (string.IsNullOrWhiteSpace(recipientUserName) || !SiteClient.BoolSetting(SiteProperties.StripeConnect_EnabledForSellers))
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(controller.User.Identity.Name, null, payerUserName);
                apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
            }
            else
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(controller.User.Identity.Name, recipientUserName, payerUserName);
                var recipient = UserClient.GetUserByUserName(SystemActors.SystemUserName, recipientUserName);
                apiKey = recipient.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = Utilities.DecryptString(apiKey);
                }
            }
            if (apiKey == SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey))
            {
                recipientUserName = null;
            }
            //first check to see if we have a cached value stored...
            string cacheKey = string.Format("UnexpStripeCard_{0}_{1}", recipientUserName, payerUserName);
            bool? retVal = (bool?)SiteClient.GetCacheData(cacheKey);
            if (retVal.HasValue) return retVal.Value;

            //no cached value exists, attempt to retrieve the data from stripe...
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                retVal = false;
            }
            else
            {
                var cardService = new StripeCardService(apiKey);
                try
                {
                    var cards = cardService.List(stripeCustomerId).ToList();
                    foreach (var stripeCard in cards)
                    {
                        int expYear = stripeCard.ExpirationYear;
                        if (expYear < 2000) expYear += 2000;
                        var expirationDate = new DateTime(expYear, stripeCard.ExpirationMonth, 1).AddMonths(1).AddSeconds(-1); // e.g. 12/21 => '2022-01-01'
                        if (expirationDate > DateTime.UtcNow)
                        {
                            retVal = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.WriteLog(null, "Error Retrieving Cards (4)", "Stripe", TraceEventType.Warning, controller.HttpContext.User.Identity.Name, e,
                        new Dictionary<string, object>() {
                            { "User", controller.FBOUserName() },
                            { "stripeCustomerId", stripeCustomerId}
                        });
                    if (e.Message.Contains("No such customer"))
                    {
                        //this buyer has a card saved in test mode, but the site has transitioned to live mode
                        try
                        {
                            AccountingClient.SetStripeCustomerId(controller.User.Identity.Name, recipientUserName, payerUserName, null);
                            LogManager.WriteLog("Successfully removed test Customer ID (4)", "Test Data Cleanup", "Stripe", TraceEventType.Information, controller.User.Identity.Name, null,
                                new Dictionary<string, object>() {
                                    { "User", payerUserName },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                        catch (Exception removeError)
                        {
                            LogManager.WriteLog(null, "Error Removing Test Data (4)", "Stripe", TraceEventType.Warning, controller.User.Identity.Name, removeError,
                                new Dictionary<string, object>() {
                                    { "User", payerUserName },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                    }
                }
            }

            //cache this value for up to 24 hours before returning it
            SiteClient.SetCacheData(cacheKey, retVal ?? false, 1440); // 1440 minutes = 24 hours
            return retVal ?? false;
        }

        /// <summary>
        /// Calls out to stripe.com API to retrieve saved cards and returns true if an unexpired card is found
        /// </summary>
        /// <param name="invoice">the invoice to evaluate</param>
        public static bool HasUnexpiredStripeCardOnFile(this Invoice invoice)
        {
            string actingUserName = SystemActors.SystemUserName;
            string recipientUserName = invoice.Owner.UserName;
            string payerUserName = invoice.Payer.UserName;
            string apiKey;
            string stripeCustomerId;
            if (string.IsNullOrWhiteSpace(recipientUserName))
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUserName, null, payerUserName);
                apiKey = SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey);
            }
            else
            {
                stripeCustomerId = AccountingClient.GetStripeCustomerId(actingUserName, recipientUserName, payerUserName);
                var recipient = UserClient.GetUserByUserName(SystemActors.SystemUserName, recipientUserName);
                apiKey = recipient.Properties.Single(p => p.Field.Name == StdUserProps.StripeConnect_SellerSecretApiKey).Value;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    apiKey = Utilities.DecryptString(apiKey);
                }
            }
            if (apiKey == SiteClient.TextSetting(SiteProperties.StripeConnect_SiteFeesSecretApiKey))
            {
                recipientUserName = null;
            }
            //first check to see if we have a cached value stored...
            string cacheKey = string.Format("UnexpStripeCard_{0}_{1}", recipientUserName, payerUserName);
            bool? retVal = (bool?)SiteClient.GetCacheData(cacheKey);
            if (retVal.HasValue) return retVal.Value;

            //no cached value exists, attempt to retrieve the data from stripe...
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                retVal = false;
            }
            else
            {
                var cardService = new StripeCardService(apiKey);
                try
                {
                    var cards = cardService.List(stripeCustomerId).ToList();
                    foreach (var stripeCard in cards)
                    {
                        int expYear = stripeCard.ExpirationYear;
                        if (expYear < 2000) expYear += 2000;
                        var expirationDate = new DateTime(expYear, stripeCard.ExpirationMonth, 1).AddMonths(1).AddSeconds(-1); // e.g. 12/21 => '2022-01-01'
                        if (expirationDate > DateTime.UtcNow)
                        {
                            retVal = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogManager.WriteLog(null, "Error Retrieving Cards (4)", "Stripe", TraceEventType.Warning, actingUserName, e,
                        new Dictionary<string, object>() {
                            { "User", payerUserName },
                            { "stripeCustomerId", stripeCustomerId}
                        });
                    if (e.Message.Contains("No such customer"))
                    {
                        //this buyer has a card saved in test mode, but the site has transitioned to live mode
                        try
                        {
                            AccountingClient.SetStripeCustomerId(actingUserName, recipientUserName, payerUserName, null);
                            LogManager.WriteLog("Successfully removed test Customer ID (4)", "Test Data Cleanup", "Stripe", TraceEventType.Information, actingUserName, null,
                                new Dictionary<string, object>() {
                                    { "User", payerUserName },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                        catch (Exception removeError)
                        {
                            LogManager.WriteLog(null, "Error Removing Test Data (4)", "Stripe", TraceEventType.Warning, actingUserName, removeError,
                                new Dictionary<string, object>() {
                                    { "User", payerUserName },
                                    { "stripeCustomerId", stripeCustomerId}
                                });
                        }
                    }
                }
            }

            //cache this value for up to 24 hours before returning it
            SiteClient.SetCacheData(cacheKey, retVal ?? false, 1440); // 1440 minutes = 24 hours
            return retVal ?? false;
        }

        #endregion stripe helpers

    }
}
