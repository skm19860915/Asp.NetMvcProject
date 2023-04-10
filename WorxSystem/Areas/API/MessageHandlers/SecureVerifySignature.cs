using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Runtime.Caching;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class SecureVerifySignature : DelegatingHandler
    {        
        public SecureVerifySignature()
        {            
        }

        public SecureVerifySignature(HttpMessageHandler inner)
            : base(inner)
        {            
        }

        private const string Scheme = "RWX_SECURE";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            //skip authentication for requests without an Authorization header
            if (request.Headers.Authorization == null)
            {
                return base.SendAsync(request, cancellationToken);
            }

            //Make sure Content-MD5 Header is present and valid if there's body content (for some reason, requests without bodies still have empty StreamContent bodies...)
            if (request.Content != null && request.Content.Headers.ContentType != null)
            {
                if (request.Content.Headers.ContentMD5 == null)
                {
                    var tsc = new TaskCompletionSource<HttpResponseMessage>();
                    tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest,
                                                              "Content-MD5 Header MISSING (A Base64-encoded binary MD5 sum of the content of the request body)"));
                    return tsc.Task;
                } else
                {
                    //verify MD5 header
                    byte[] bodyBytes = request.Content.ReadAsByteArrayAsync().Result;

                    byte[] md5Hash = null;
                    using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
                    {
                        md5Hash = md5.ComputeHash(bodyBytes);
                    }

                    if (!md5Hash.SequenceEqual(request.Content.Headers.ContentMD5))
                    {
                        //bad ND5 hash, tampering
                        var tsc = new TaskCompletionSource<HttpResponseMessage>();
                        tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest,
                                                                  "Content-MD5 Header INVALID (Content-MD5 header and MD5 hash of request body do not match, possibly indicating request tampering)"));
                        return tsc.Task;
                    }
                }
            }            

            //Make sure Date Header is present
            if (!request.Headers.Date.HasValue)
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Date Header MISSING (Date header must be present with an RFC1123 formatted UTC date/time)"));
                return tsc.Task;
            }

            //Make sure correct Authorization Scheme is present
            if (!request.Headers.Authorization.Scheme.Equals(Scheme, StringComparison.Ordinal))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Authentication Scheme INVALID (must be \"" + Scheme + "\")"));
                return tsc.Task;
            }

            //Make sure Authorization Parameter is present
            if (string.IsNullOrEmpty(request.Headers.Authorization.Parameter))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Authentication Parameter MISSING (must be {username" + Utilities.SignatureSeparatorCharacter + "signature})"));
                return tsc.Task;
            }

            //check date window
            if (!IsDateValid(request))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Date Header Out Of Bounds (Date header must be close to server time)"));
                return tsc.Task;
            }

            //get actual signature and calculate expected signature
            char[] signatureSplitter = new char[] { Utilities.SignatureSeparatorCharacter };
            string[] splitAuthParameter = request.Headers.Authorization.Parameter.Split(signatureSplitter, 2);
            string username = splitAuthParameter[0];
            string actualSignature = splitAuthParameter[1];

            //disallow duplicate messages being sent within validity window (Utilities.ValidityPeriodInMinutes minutes)
            if (MemoryCache.Default.Contains(actualSignature))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Duplicate identical requests are not allowed (we assume identical, consecutive messages coming from a user will always have different timestamps)"));
                return tsc.Task;
            }

            //this is where we get the authtoken from the db            
            User actingUser = UserClient.GetUserByUserName(username, username);            

            if (actingUser == null)
            {                
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }

            if (actingUser.IsLockedOut || !actingUser.IsApproved || !actingUser.IsActive || !actingUser.IsVerified || !actingUser.WebAPIEnabled)
            {                
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }

            string expectedSignature = Utilities.GenerateSignature(request, username, actingUser.ServiceAuthorizationToken);

            //compare signatures, return if unauthorized
            if (!actualSignature.Equals(expectedSignature, StringComparison.Ordinal))
            {                
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }

            MemoryCache.Default.Add(actualSignature, username, DateTimeOffset.UtcNow.AddMinutes(Utilities.ValidityPeriodInMinutes));

            return base.SendAsync(request, cancellationToken);
        }

        private bool IsDateValid(HttpRequestMessage requestMessage)
        {
            var utcNow = DateTime.UtcNow;
            var date = requestMessage.Headers.Date.Value.UtcDateTime;
            if (date >= utcNow.AddMinutes(Utilities.ValidityPeriodInMinutes)
                || date <= utcNow.AddMinutes(-Utilities.ValidityPeriodInMinutes))
            {
                return false;
            }
            return true;
        }
    }
}