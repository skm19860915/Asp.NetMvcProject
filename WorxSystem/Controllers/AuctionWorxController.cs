using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.MVC.Helpers;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Utility;
using Microsoft.AspNet.Identity;
using System.Configuration;
using RainWorx.FrameWorx.MVC.Models;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides some basic functionality needed by most other AuctionWorx controllers
    /// </summary>
    //[HandleError(ExceptionType = typeof(HttpRequestValidationException), View = Strings.MVC.RequestValidationErrorView)]
    [HandleError]
    [ActiveUserChecking]
    public abstract class AuctionWorxController : Controller
    {
        /// <summary>
        /// Stores the specified "success" response message key in temp data after the calling action method succeeds
        /// </summary>
        /// <param name="value">the message to be displayed</param>
        /// <param name="type">specifies whether a generic success message key is needed, or if a custom message is needed</param>
        protected void PrepareSuccessMessage(string value, MessageType type)
        {
            //TempData[Strings.MVC.ErrorMessage] = null;
            //TempData[Strings.MVC.NeutralMessage] = null;

            switch (type)
            {
                case MessageType.Message:
                    {
                        break;
                    }
                case MessageType.Method:
                    {
                        if (!value.EndsWith("Success"))
                        {
                            value += "Success";
                        }

                        break;
                    }

            }

            TempData[Strings.MVC.SuccessMessage] = value;
        }

        /// <summary>
        /// Stores the specified "error" response message key in temp data after the calling action method fails
        /// </summary>
        /// <param name="reasonCode">Code for the error that was encountered by the calling method</param>
        protected void PrepareErrorMessage(ReasonCode reasonCode)
        {
            //TempData[Strings.MVC.SuccessMessage] = null;
            //TempData[Strings.MVC.NeutralMessage] = null;

            TempData[Strings.MVC.ErrorMessage] = Enum.GetName(typeof(ReasonCode), reasonCode);
        }

        /// <summary>
        /// Stores a generic "error" response message key in temp data, generated from the calling method's name
        /// </summary>
        ///
        protected void PrepareErrorMessage(string value, MessageType type)
        {
            //TempData[Strings.MVC.SuccessMessage] = null;
            //TempData[Strings.MVC.NeutralMessage] = null;

            switch (type)
            {
                case MessageType.Message:
                    {
                        break;
                    }
                case MessageType.Method:
                    {
                        if (!value.EndsWith("Failure"))
                        {
                            value += "Failure";
                        }

                        break;
                    }

            }

            TempData[Strings.MVC.ErrorMessage] = value;
        }

        /// <summary>
        /// Stores an "error" response message key in temp data, generated from the specified exception details
        /// </summary>
        /// <param name="functionName">the name of the calling method</param>
        /// <param name="e">the specified exception details</param>
        protected void PrepareErrorMessage(String functionName, Exception e)
        {
            //TempData[Strings.MVC.SuccessMessage] = null;
            //TempData[Strings.MVC.NeutralMessage] = null;

            TempData[Strings.MVC.ErrorMessage] = functionName + "Failure:" + e.Message;
        }

        /// <summary>
        /// Stores the specified "neutral" response message key in temp data when a response that doesn't indicate success or failure is needed
        /// </summary>
        /// <param name="message">A key for the message string defined in the applicable resource file</param>
        protected void PrepareNeutralMessage(string message)
        {
            //TempData[Strings.MVC.ErrorMessage] = null;
            //TempData[Strings.MVC.SuccessMessage] = null;

            TempData[Strings.MVC.NeutralMessage] = message;
        }

        #region Utilities

        /// <summary>
        /// Converts a string of comma separated options to a SelectList object
        /// </summary>
        /// <param name="list">comma separated select option values</param>
        /// <returns>SelectList</returns>
        protected static SelectList SimpleSelectList(string list)
        {
            return SimpleSelectList(list, null);
        }

        /// <summary>
        /// Converts a string of comma separated options to a SelectList object
        /// </summary>
        /// <param name="list">comma separated select option values</param>
        /// <param name="selectedValue">the value to be pre-selected</param>
        /// <returns>SelectList</returns>
        protected static SelectList SimpleSelectList(string list, object selectedValue)
        {
            var newList = new Dictionary<string, string>();
            foreach (string item in list.Split(','))
            {
                newList.Add(item, item);
            }
            return new SelectList(newList, Strings.Fields.Value, Strings.Fields.Key, selectedValue);
        }

        /// <summary>
        /// Removes any custom fields from the specified list where the specified user does not have sufficient permission to edit, per each fields' "Mutability" property
        /// </summary>
        /// <param name="userName">the username of the specified user</param>
        /// <param name="ownerUserName">the username who "owns" the object containing the specified list of fields</param>
        /// <param name="fields">the specified list of fields</param>
        protected void PruneListingCustomFieldsEditability(string userName, string ownerUserName, List<CustomField> fields)
        {
            var access = CustomFieldAccess.Authenticated;
            if (userName == Strings.SystemActors.SystemUserName)
            {
                access = CustomFieldAccess.System;
            }
            else
            {
                User user = UserClient.GetUserByUserName(User.Identity.Name, userName);
                if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin))
                {
                    access = CustomFieldAccess.Admin;
                }
                else if (userName == ownerUserName)
                {
                    access = CustomFieldAccess.Owner;
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    access = CustomFieldAccess.System;
                }
            }
            fields.RemoveAll(cf => cf.Mutability < access);
        }

        /// <summary>
        /// Calculates the applicable access level of the specified listing with respect to the currently authenticated user
        /// </summary>
        /// <param name="listing">the specified listing</param>
        /// <returns>enum of type CustomFieldAccess</returns>
        protected CustomFieldAccess GetCustomFieldVisbilityForListing(Listing listing)
        {
            string userName = this.FBOUserName();

            if (string.IsNullOrEmpty(userName)) return CustomFieldAccess.Anonymous;

            if (userName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            User user = UserClient.GetUserByUserName(User.Identity.Name, userName);
            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;

            if (listing.OwnerUserName == userName) return CustomFieldAccess.Owner;

            List<LineItem> lineItems = AccountingClient.GetLineItemsForListingByPayer(User.Identity.Name, userName, listing.ID, 0, 0, null, false).List;

            if (lineItems.Any(li => li.Status == Strings.LineItemStatuses.Complete)) return CustomFieldAccess.Purchaser;

            if (User.Identity.IsAuthenticated) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        /// <summary>
        /// Removes any custom properties from the specified listing where the currently authenticated user does not have sufficient permission to view, 
        /// per each associated fields' "Visibility" property
        /// </summary>
        /// <param name="listing">the specified listing</param>
        protected void PruneListingCustomFieldsVisbility(Listing listing)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForListing(listing);
            listing.Properties = listing.Properties.Where(
                p => p.Field.Group != Strings.CustomFieldGroups.Item || (int)p.Field.Visibility >= (int)access).ToList();
        }

        /// <summary>
        /// Removes any custom fields from the specified list where the specified user does not have sufficient permission to edit, 
        /// per each fields' "Mutability" property
        /// </summary>
        /// <param name="userName">the username of the specified user</param>
        /// <param name="ownerUserName">the username who "owns" the object containing the specified list of fields</param>
        /// <param name="properties">the specified list of properties</param>
        protected void PruneListingCustomPropertiesEditability(string userName, string ownerUserName, List<CustomProperty> properties)
        {
            var access = CustomFieldAccess.Authenticated;
            if (userName == Strings.SystemActors.SystemUserName)
            {
                access = CustomFieldAccess.System;
            }
            else
            {
                User user = UserClient.GetUserByUserName(userName, userName);
                if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin))
                {
                    access = CustomFieldAccess.Admin;
                }
                else if (userName == ownerUserName)
                {
                    access = CustomFieldAccess.Owner;
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    access = CustomFieldAccess.System;
                }
            }
            properties.RemoveAll(cp => cp.Field.Mutability < access);
        }

        /// <summary>
        /// Removes any custom fields in the specified list which should not be visible for the specified access level
        /// </summary>
        /// <param name="accessLevel">the access level to prune to</param>
        /// <param name="fields">the list of custom fields to prune</param>
        protected void PruneCustomFieldsVisbility(CustomFieldAccess accessLevel, List<CustomField> fields)
        {
            fields.RemoveAll(f => f.Visibility < accessLevel);
        }

        /// <summary>
        /// Removes any custom properties in the specified list which should not be visible for the specified access level
        /// </summary>
        /// <param name="accessLevel">the access level to prune to</param>
        /// <param name="properties">the list of custom properties to prune</param>
        protected void PruneCustomPropertiesVisbility(CustomFieldAccess accessLevel, List<CustomProperty> properties)
        {
            properties.RemoveAll(p => p.Field.Visibility < accessLevel);
        }

        /// <summary>
        /// Removes any custom properties in the specified list which should not be visible to the current user
        /// </summary>
        /// <param name="ownerUN">the username of the owner of this list of properties</param>
        /// <param name="properties">the list of custom properties to prune</param>
        protected void PruneCustomPropertiesVisbility(string ownerUN, List<CustomProperty> properties)
        {
            var access = CustomFieldAccess.Anonymous;
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(Strings.Roles.Admin))
                {
                    access = CustomFieldAccess.Admin;
                }
                else if (User.Identity.Name.Equals(ownerUN, StringComparison.OrdinalIgnoreCase))
                {
                    access = CustomFieldAccess.Owner;
                }
                else
                {
                    access = CustomFieldAccess.Authenticated;
                }
            }
            PruneCustomPropertiesVisbility(access, properties);
        }

        /// <summary>
        /// Removes any custom properties in the specified list which should not be editable for the specified access level
        /// </summary>
        /// <param name="accessLevel">the access level to prune to</param>
        /// <param name="properties">the list of custom properties to prune</param>
        protected void PruneCustomPropertiesEditability(CustomFieldAccess accessLevel, List<CustomProperty> properties)
        {
            properties.RemoveAll(p => p.Field.Mutability < accessLevel);
        }

        /// <summary>
        /// Removes any custom properties in the specified list which should not be editable by the current user
        /// </summary>
        /// <param name="ownerUN">the username of the owner of this list of properties</param>
        /// <param name="properties">the list of custom properties to prune</param>
        protected void PruneCustomPropertiesEditability(string ownerUN, List<CustomProperty> properties)
        {
            var access = CustomFieldAccess.Anonymous;
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole(Strings.Roles.Admin))
                {
                    access = CustomFieldAccess.Admin;
                }
                else if (User.Identity.Name.Equals(ownerUN, StringComparison.OrdinalIgnoreCase))
                {
                    access = CustomFieldAccess.Owner;
                }
                else
                {
                    access = CustomFieldAccess.Authenticated;
                }
            }
            PruneCustomPropertiesVisbility(access, properties);
        }

        #endregion

        #region User Property Logic

        /// <summary>
        /// Validates certain user properties whose values must not conflict with each other
        /// </summary>
        /// <param name="properties">list of user properties</param>
        /// <param name="input">user input container</param>
        protected void ValidateUserPropertyValues(IEnumerable<CustomProperty> properties, UserInput input)
        {
            ValidationResults validation = new ValidationResults();

            bool? allowInstantCheckout = null;
            string payPayEmail = null;
            bool? acceptPayPal = null;
            bool? acceptCreditCard = null;
            string authNetLogin = null;
            string authNetTxnKey = null;
            bool? stripeConnected = null;

            //capture values for properies we care about, if available
            foreach (CustomProperty userProp in properties)
            {
                if (input.Items.ContainsKey(userProp.Field.Name))
                {
                    switch (userProp.Field.Name)
                    {
                        case StdUserProps.AllowInstantCheckout:
                            //allowInstantCheckout = userProp.Value == null ? false : bool.Parse(userProp.Value);
                            bool temp1;
                            if (bool.TryParse(input.Items[userProp.Field.Name], out temp1))
                            {
                                allowInstantCheckout = temp1;
                            }
                            break;
                        case StdUserProps.PayPal_Email:
                            //payPayEmail = userProp.Value == null ? string.Empty : userProp.Value;
                            payPayEmail = input.Items[userProp.Field.Name];
                            break;
                        case StdUserProps.AcceptPayPal:
                            //acceptPayPal = userProp.Value == null ? false : bool.Parse(userProp.Value);
                            bool temp2;
                            if (bool.TryParse(input.Items[userProp.Field.Name], out temp2))
                            {
                                acceptPayPal = temp2;
                            }
                            break;
                        case StdUserProps.AcceptCreditCard:
                            //acceptCreditCard = userProp.Value == null ? false : bool.Parse(userProp.Value);
                            bool temp3;
                            if (bool.TryParse(input.Items[userProp.Field.Name], out temp3))
                            {
                                acceptCreditCard = temp3;
                            }
                            break;
                        case StdUserProps.AuthorizeNet_SellerMerchantLoginID:
                            //authNetLogin = userProp.Value == null ? string.Empty : userProp.Value;
                            authNetLogin = input.Items[userProp.Field.Name];
                            break;
                        case StdUserProps.AuthorizeNet_SellerTransactionKey:
                            //authNetTxnKey = userProp.Value == null ? string.Empty : userProp.Value;
                            authNetTxnKey = input.Items[userProp.Field.Name];
                            break;
                        case StdUserProps.StripeConnect_SellerAccountConnected:
                            //stripeConnected = userProp.Value == null ? false : bool.Parse(userProp.Value);
                            bool temp4;
                            if (bool.TryParse(input.Items[userProp.Field.Name], out temp4))
                            {
                                stripeConnected = temp4;
                            }
                            break;
                    }
                }
            }

            //if paypal email is blank, accept paypal payments must be false
            if (payPayEmail != null && acceptPayPal.HasValue)
            {
                if (payPayEmail == string.Empty && acceptPayPal.Value == true)
                {
                    validation.AddResult(
                        new ValidationResult(
                            //"You must provide a PayPal Email in order to set Accept PayPal Payments"
                            Strings.Messages.PayPalEmailRequiredForAcceptPayPal, this
                            , Strings.StdUserProps.PayPal_Email
                            , Strings.StdUserProps.PayPal_Email, null));
                }
            }

            //if auth.net credentials are blank, accept credit cards must be false
            if (acceptCreditCard.HasValue && acceptCreditCard.Value == true)
            {
                ////login and key properties may be missing due to masked field encrtyption logic, check userInput value in this case
                //if (authNetLogin == null &&
                //    input.Items.ContainsKey(StdUserProps.AuthorizeNet_SellerMerchantLoginID) &&
                //    input.Items[StdUserProps.AuthorizeNet_SellerMerchantLoginID] == Fields.MaskedFieldValue)
                //{
                //    authNetLogin = "dummy non-blank value";
                //}
                //if (authNetLogin == null &&
                //    input.Items.ContainsKey(StdUserProps.AuthorizeNet_SellerTransactionKey) &&
                //    input.Items[StdUserProps.AuthorizeNet_SellerTransactionKey] == Fields.MaskedFieldValue)
                //{
                //    authNetTxnKey = "dummy non-blank value";
                //}

                if (SiteClient.BoolSetting(SiteProperties.AuthorizeNet_Enabled))
                {
                    if (authNetLogin == string.Empty)
                    {
                        validation.AddResult(
                            new ValidationResult(
                                //"You must provide Authorize.Net credentials in order to set Accept Credit Card Payments"
                                Strings.Messages.AuthNetCredentialsRequiredForAcceptCreditCards, this
                                , Strings.StdUserProps.AuthorizeNet_SellerMerchantLoginID
                                , Strings.StdUserProps.AuthorizeNet_SellerMerchantLoginID, null));
                    }

                    if (authNetTxnKey == string.Empty)
                    {
                        validation.AddResult(
                            new ValidationResult(
                                //"You must provide Authorize.Net credentials in order to set Accept Credit Card Payments"
                                Strings.Messages.AuthNetCredentialsRequiredForAcceptCreditCards, this
                                , Strings.StdUserProps.AuthorizeNet_SellerTransactionKey
                                , Strings.StdUserProps.AuthorizeNet_SellerTransactionKey, null));
                    }
                }

                if (SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled))
                {
                    if (stripeConnected.HasValue && !stripeConnected.Value)
                    {
                        validation.AddResult(
                            new ValidationResult(
                                //"You must connect your Stripe.com account in order to Accept Credit Card Payments"
                                Strings.Messages.StripeConnectCredentialsRequiredForAcceptCreditCards, this
                                , Strings.StdUserProps.StripeConnect_SellerAccountConnected
                                , Strings.StdUserProps.StripeConnect_SellerAccountConnected, null));
                    }
                }

            }

            if (allowInstantCheckout.HasValue)
            {
                if (acceptPayPal.HasValue && acceptCreditCard.HasValue)
                {
                    //if accept paypal payments is false and accept credit card is false, allow instant checkout must be false
                    if (acceptPayPal.HasValue && allowInstantCheckout.HasValue)
                    {
                        if (acceptPayPal.Value == false && acceptCreditCard.Value == false && allowInstantCheckout.Value == true)
                        {
                            validation.AddResult(
                                new ValidationResult(
                                    //"You must accept PayPal or Credit Cards in order to offer Instant Checkout"
                                    Strings.Messages.AcceptPayPalOrCreditCardRequiredForInstantCheckout, this
                                    , Strings.StdUserProps.AllowInstantCheckout
                                    , Strings.StdUserProps.AllowInstantCheckout, null));
                        }
                    }
                }
                else if (acceptPayPal.HasValue && stripeConnected.HasValue)
                {
                    if (acceptPayPal.Value == false && stripeConnected.Value == false && allowInstantCheckout.Value == true)
                    {
                        validation.AddResult(
                            new ValidationResult(
                                //"You must accept PayPal or configure Stripe in order to offer Instant Checkout"
                                Strings.Messages.AcceptPayPalOrStripeRequiredForInstantCheckout, this
                                , Strings.StdUserProps.AllowInstantCheckout
                                , Strings.StdUserProps.AllowInstantCheckout, null));
                    }
                }
                else if (acceptCreditCard.HasValue)
                {
                    //if accept paypal payments is false and accept credit card is false, allow instant checkout must be false
                    if (allowInstantCheckout.HasValue)
                    {
                        if (acceptCreditCard.Value == false && allowInstantCheckout.Value == true)
                        {
                            validation.AddResult(
                                new ValidationResult(
                                    //"You must accept Credit Cards in order to offer Instant Checkout"
                                    Strings.Messages.AcceptCreditCardRequiredForInstantCheckout, this
                                    , Strings.StdUserProps.AllowInstantCheckout
                                    , Strings.StdUserProps.AllowInstantCheckout, null));
                        }
                    }
                }
                else if (stripeConnected.HasValue)
                {
                    if (stripeConnected.Value == false && allowInstantCheckout.Value == true)
                    {
                        validation.AddResult(
                            new ValidationResult(
                                //"You must configure Stripe in order to offer Instant Checkout"
                                Strings.Messages.StripeRequiredForInstantCheckout, this
                                , Strings.StdUserProps.AllowInstantCheckout
                                , Strings.StdUserProps.AllowInstantCheckout, null));
                    }
                }
                else
                {
                    //if accept paypal payments is false, allow instant checkout must be false
                    if (acceptPayPal.HasValue && allowInstantCheckout.HasValue)
                    {
                        if (acceptPayPal.Value == false && allowInstantCheckout.Value == true)
                        {
                            validation.AddResult(
                                new ValidationResult(
                                    //"You must accept PayPal in order to offer Instant Checkout"
                                    Strings.Messages.AcceptPayPalRequiredForInstantCheckout, this
                                    , Strings.StdUserProps.AllowInstantCheckout
                                    , Strings.StdUserProps.AllowInstantCheckout, null));
                        }
                    }
                }
            }

            //payment instructions - no requirements here

            //if any validation issues exist, throw an exception with the details
            if (!validation.IsValid)
            {
                Statix.ThrowValidationFaultContract(validation);
            }

        }

        private CustomFieldAccess GetCustomFieldAccessForUser(string userName, out bool isAdmin)
        {
            isAdmin = false;
            string _userName = this.FBOUserName();

            if (string.IsNullOrEmpty(_userName)) return CustomFieldAccess.Anonymous;

            if (_userName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            User user = UserClient.GetUserByUserName(User.Identity.Name, _userName);
            isAdmin = user.Roles.Any(r => r.Name == Strings.Roles.Admin);
            if (user != null && isAdmin) return CustomFieldAccess.Admin;

            if (userName == _userName) return CustomFieldAccess.Owner;

            if (User.Identity.IsAuthenticated) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        private void PruneUserCustomFieldsForRoleAndSitePops(List<CustomProperty> userProperties, bool isAdmin)
        {
            //hide PayPal properties if PayPal is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.PayPal_Enabled))
            {
                userProperties.RemoveAll(p => p.Field.Name == StdUserProps.PayPal_Email || p.Field.Name == StdUserProps.AcceptPayPal);
            }

            //hide the "Accept Credit Cards" field if credit cards are disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.CreditCardsEnabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                }
            }

            //hide "Accept Credit Cards" and auth.net credential fields if either credit cards are disabled globally or authorize.net is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.CreditCardsEnabled) || !SiteClient.BoolSetting(SiteProperties.AuthorizeNet_Enabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                }
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)));
                }
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)));
                }
            }

            //hide stripe fields if stripe is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)));
                }
            }

            //hide "AcceptCreditCard" and auth.net credential fields for non-admin users when AuthorizeNet_EnableForSellers is false
            if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
            {
                if (!isAdmin)
                {
                    if (!SiteClient.BoolSetting(SiteProperties.AuthorizeNet_EnableForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)));
                        }
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)));
                        }
                    }
                    if (!SiteClient.BoolSetting(SiteProperties.StripeConnect_EnabledForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)));
                        }
                    }
                    if (!SiteClient.BoolSetting(SiteProperties.AuthorizeNet_EnableForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                        }
                    }
                }
            }

            //hide "BuyersPremiumPercent" property when it is disabled site-wide
            if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.BuyersPremiumPercent)))
            {
                if (!SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.BuyersPremiumPercent)));
                }
            }
        }

        /// <summary>
        /// returns a list of properties minus any that should not be editable by impersonated user due to role and/or applicable site properties
        /// </summary>
        /// <param name="userProperties">list of user properties</param>
        /// <param name="userName">the subject username</param>
        protected List<CustomProperty> PruneUserCustomFieldsForEdit(List<CustomProperty> userProperties, string userName)
        {
            bool isAdmin;
            CustomFieldAccess access = GetCustomFieldAccessForUser(userName, out isAdmin);
            var retVal = userProperties.Where(
                p => /*(int)p.Field.Visibility >= (int)access &&*/ (int)p.Field.Mutability >= (int)access).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, isAdmin);

            return retVal; 
        }

        /// <summary>
        /// returns a list of properties minus any that should not be visible by impersonated user due to role and/or applicable site properties
        /// </summary>
        /// <param name="userProperties">list of user properties</param>
        /// <param name="userName">the subject username</param>
        protected List<CustomProperty> PruneUserCustomFieldsForVisbilityOnly(List<CustomProperty> userProperties, string userName)
        {
            bool isAdmin;
            CustomFieldAccess access = GetCustomFieldAccessForUser(userName, out isAdmin);
            var retVal = userProperties.Where(
                p => (int)p.Field.Visibility >= (int)access /*&& (int)p.Field.Mutability < (int)access*/).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, isAdmin);

            return retVal;
        }

        /// <summary>
        /// returns a list of properties minus any that should not be editable as admin due to applicable site properties
        /// </summary>
        /// <param name="userProperties">list of user properties</param>
        protected List<CustomProperty> PruneUserCustomFieldsForEditAsAdmin(List<CustomProperty> userProperties)
        {
            CustomFieldAccess access = CustomFieldAccess.Admin;
            var retVal = userProperties.Where(
                p => /*(int)p.Field.Visibility >= (int)access &&*/ (int)p.Field.Mutability >= (int)access).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, true);

            return retVal;
        }

        /// <summary>
        /// returns a list of properties minus any that should not be visible as admin due to applicable site properties
        /// </summary>
        /// <param name="userProperties">list of user properties</param>
        protected List<CustomProperty> PruneUserCustomFieldsForVisbilityAsAdmin(List<CustomProperty> userProperties)
        {
            CustomFieldAccess access = CustomFieldAccess.Admin;
            var retVal = userProperties.Where(
                p => (int)p.Field.Visibility >= (int)access /*&& (int)p.Field.Mutability < (int)access*/).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, true);

            return retVal;
        }

        #endregion User Property Logic

        /// <summary>
        /// Adds IdentityResult errors to ModelState
        /// </summary>
        /// <param name="result">the result status of an AspNet.Identity method</param>
        protected void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                if (error == "PasswordTooShort")
                {
                    //pre-localize this one because it requires an argument
                    ModelState.AddModelError("_FORM", this.ValidationResourceString(error, ConfigurationManager.AppSettings["Password_RequiredLength"] ?? "6"));
                }
                else
                {
                    ModelState.AddModelError("_FORM", error);
                }
            }
        }

        /// <summary>
        /// Retrieves final buyer fee data for the specified listing type
        /// </summary>
        /// <param name="listingType">the name of the specified listing type</param>
        /// <param name="categories">the categories of the listing in question</param>
        /// <param name="finalFeeMin">returns the minimum fee amount</param>
        /// <param name="finalFeeMax">returns the maximum fee amount</param>
        /// <param name="finalFeeTiers">returns a list of applicable fee tiers</param>
        /// <param name="finalFeeDescription">returns the configured final buyer fee description</param>
        protected static void GetFinalBuyerFeeRanges(string listingType, List<Category> categories,
            out decimal finalFeeMin, out decimal finalFeeMax, out List<Tier> finalFeeTiers, out string finalFeeDescription)
        {
            if (categories == null)
                throw new ArgumentNullException("categories", "Check to ensure the proper fill level was used when retrieving the lot/listing data.");

            finalFeeMin = 0.0M;
            finalFeeMax = 0.0M;
            finalFeeTiers = new List<Tier>();
            finalFeeDescription = null;

            bool skipBuyerFinalFee = false;
            string buyerFinalFeeCatList = Cache.SiteProperties[Strings.SiteProperties.FeeCategories_FinalBuyerFee];
            string buyerFinalFeeCategoryMode = Cache.SiteProperties[Strings.SiteProperties.FeeCategoryMode_FinalBuyerFee];
            if (!string.IsNullOrEmpty(buyerFinalFeeCatList))
            {
                var selectedCategories = new List<int>();
                foreach (string possibleCatId in buyerFinalFeeCatList.Split(','))
                {
                    int catId;
                    if (int.TryParse(possibleCatId.Trim(), out catId))
                    {
                        selectedCategories.Add(catId);
                    }
                }
                if (buyerFinalFeeCategoryMode == "ExcludeSelected" && categories.Any(lc => selectedCategories.Any(sc => sc == lc.ID)))
                {
                    skipBuyerFinalFee = true;
                }
                else if (buyerFinalFeeCategoryMode == "IncludeSelected" && !categories.Any(lc => selectedCategories.Any(sc => sc == lc.ID)))
                {
                    skipBuyerFinalFee = true;
                }
                if (skipBuyerFinalFee) return;
            }

            if (!listingType.Equals(ListingTypes.Classified))
            { // listing is not a classified ad, proceed
                foreach (FeeSchedule fs in SiteClient.FeeSchedules.Where(f => f.ListingType.Name == listingType
                                                                              &&
                                                                              f.Event.Name == Events.EndListingSuccess
                                                                              && f.Name == Roles.Buyer))
                {
                    if (string.IsNullOrEmpty(finalFeeDescription))
                        finalFeeDescription = fs.Description;

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
        }

    }

    /// <summary>
    /// The possible message types for use with AuctionWorxController utility functions
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// used then a custom error or success message is needed
        /// </summary>
        Message,
        /// <summary>
        /// used then a generic error or success message is needed
        /// </summary>
        Method
    }

    /// <summary>
    /// action filter attribute to apply an HTTP header attribute
    /// </summary>
    public class HttpHeaderAttribute : ActionFilterAttribute
    {
        /// 
        /// Gets or sets the name of the HTTP Header.
        /// 
        /// The name.
        public string Name { get; set; }

        /// 
        /// Gets or sets the value of the HTTP Header.
        /// 
        /// The value.
        public string Value { get; set; }

        /// 
        /// Initializes a new instance of the  class.
        /// 
        /// The name.
        /// The value.
        public HttpHeaderAttribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// applies the spefied headder attribute
        /// </summary>
        /// <param name="filterContext">ResultExecutedContext</param>
        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            filterContext.HttpContext.Response.AppendHeader(Name, Value);
            base.OnResultExecuted(filterContext);
        }

    }
}
