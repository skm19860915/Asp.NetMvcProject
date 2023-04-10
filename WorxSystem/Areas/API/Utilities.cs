using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Models;
using User = RainWorx.FrameWorx.DTO.User;

namespace RainWorx.FrameWorx.MVC.Areas.API
{
    public static class Utilities
    {        
        private const char RequestSeparatorCharacter = '\n';
        public const char SignatureSeparatorCharacter = ':';        
        public const int ValidityPeriodInMinutes = 10;
        public static string VirtualRoot = string.Empty;

        /// <summary>
        /// Builds message representation as follows:
        /// HTTP METHOD\n +
        /// Content-MD5\n +  
        /// Content-Type\n +  
        /// Date\n +
        /// Username\n +
        /// Request URI
        /// </summary>
        /// <returns></returns>
        private static string GenerateRequestRepresentation(HttpRequestMessage request, string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            if (!request.Headers.Date.HasValue) return null;

            StringBuilder builder = new StringBuilder();
            builder.Append(request.Method.Method);
            builder.Append(RequestSeparatorCharacter);
            if (request.Content != null && request.Content.Headers.ContentMD5 != null && request.Content.Headers.ContentType != null)
            {
                builder.Append(Convert.ToBase64String(request.Content.Headers.ContentMD5));
                builder.Append(RequestSeparatorCharacter);
                builder.Append(request.Content.Headers.ContentType.MediaType);
                builder.Append(RequestSeparatorCharacter);
            }
            //the date must be in RFC1123 format
            builder.Append(request.Headers.Date.Value.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(RequestSeparatorCharacter);
            builder.Append(username);
            builder.Append(RequestSeparatorCharacter);
            builder.Append(request.RequestUri.AbsolutePath.ToLower());
            return builder.ToString();
        }

        public static string GenerateSignature(HttpRequestMessage request, string userName, string authenticationToken)
        {
            //build the string to be signed
            string stringToSign = GenerateRequestRepresentation(request, userName);

            if (string.IsNullOrEmpty(stringToSign)) return null;

            //convert the string to be signed into an array of bytes
            byte[] bytesToSign = Encoding.UTF8.GetBytes(stringToSign);

            //decode the authentication token into an array of bytes
            byte[] authenticationTokenBytes = Convert.FromBase64String(authenticationToken);

            //compute the hash signature using the authentication token
            byte[] signatureBytes = null;
            using (HMACSHA256 algorithm = new HMACSHA256(authenticationTokenBytes))
            {
                signatureBytes = algorithm.ComputeHash(bytesToSign);
            }

            //convert the hash signature to a base64 string
            return Convert.ToBase64String(signatureBytes);
        }

        private static CustomFieldAccess GetCustomFieldVisbilityForListing(APIListing listing, string userName)
        {            
            if (string.IsNullOrEmpty(userName)) return CustomFieldAccess.Anonymous;

            if (userName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            User user = UserClient.GetUserByUserName(userName, userName);
            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;

            if (listing.OwnerUserName == userName) return CustomFieldAccess.Owner;

            List<LineItem> lineItems = AccountingClient.GetLineItemsForListingByPayer(userName, userName, listing.ID, 0, 0, null, false).List;

            if (lineItems.Any(li => li.Status == Strings.LineItemStatuses.Complete)) return CustomFieldAccess.Purchaser;

            if (!string.IsNullOrEmpty(userName)) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        public static void PruneListingCustomFieldsVisbility(ref APIListing listing, string userName)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForListing(listing, userName);
            listing.Properties = listing.Properties.Where(
                p => (int)p.Field.Visibility >= (int)access).ToList();
        }

        private static CustomFieldAccess GetCustomFieldVisbilityForListing(APIListing listing, User user)
        {
            if (string.IsNullOrEmpty(user.UserName)) return CustomFieldAccess.Anonymous;

            if (user.UserName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;
            
            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;

            if (listing.OwnerUserName == user.UserName) return CustomFieldAccess.Owner;

            if (!string.IsNullOrEmpty(user.UserName)) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        public static void PruneListingCustomFieldsVisbility(ref APIListing listing, User user)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForListing(listing, user);
            listing.Properties = listing.Properties.Where(
                p => (int)p.Field.Visibility >= (int)access).ToList();
        }

        private static CustomFieldAccess GetCustomFieldVisbilityForEvent(APIEvent auctionEvent, string userName)
        {
            if (string.IsNullOrEmpty(userName)) return CustomFieldAccess.Anonymous;

            if (userName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            User user = UserClient.GetUserByUserName(userName, userName);
            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;

            if (auctionEvent.OwnerUserName == userName) return CustomFieldAccess.Owner;

            //List<LineItem> lineItems = AccountingClient.GetLineItemsForListingByPayer(userName, userName, listing.ID, 0, 0, null, false).List;

            //if (lineItems.Any(li => li.Status == Strings.LineItemStatuses.Complete)) return CustomFieldAccess.Purchaser;

            if (!string.IsNullOrEmpty(userName)) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        public static void PruneEventCustomFieldsVisbility(ref APIEvent auctionEvent, string userName)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForEvent(auctionEvent, userName);
            auctionEvent.Properties = auctionEvent.Properties.Where(
                p => (int)p.Field.Visibility >= (int)access).ToList();
        }

        private static CustomFieldAccess GetCustomFieldVisbilityForEvent(APIEvent auctionEvent, User user)
        {
            if (string.IsNullOrEmpty(user.UserName)) return CustomFieldAccess.Anonymous;

            if (user.UserName == Strings.SystemActors.SystemUserName) return CustomFieldAccess.System;

            if (user != null && user.Roles.Any(r => r.Name == Strings.Roles.Admin)) return CustomFieldAccess.Admin;

            if (auctionEvent.OwnerUserName == user.UserName) return CustomFieldAccess.Owner;

            if (!string.IsNullOrEmpty(user.UserName)) return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        public static void PruneEventCustomFieldsVisbility(ref APIEvent auctionEvent, User user)
        {
            CustomFieldAccess access = GetCustomFieldVisbilityForEvent(auctionEvent, user);
            auctionEvent.Properties = auctionEvent.Properties.Where(
                p => (int)p.Field.Visibility >= (int)access).ToList();
        }

    }
}