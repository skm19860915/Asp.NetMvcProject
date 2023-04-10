using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class SecureSignRequest : DelegatingHandler
    {        
        private readonly string _userName;
        private readonly string _authenticationToken;
        private const string Scheme = "RWX_SECURE";

        public SecureSignRequest(string username, string base64AuthenticationToken)
        {
            _userName = username;
            _authenticationToken = base64AuthenticationToken;
        }

        public SecureSignRequest(string username, string base64AuthenticationToken, HttpMessageHandler inner)
            : base(inner)
        {
            _userName = username;
            _authenticationToken = base64AuthenticationToken;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                               System.Threading.CancellationToken cancellationToken)
        {
            //see also http://www.piotrwalat.net/hmac-authentication-in-asp-net-web-api/      
     
            //set Content-MD5 Header if body is not empty
            if (request.Content != null)
            {
                byte[] content = request.Content.ReadAsByteArrayAsync().Result;
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(content);                    
                    request.Content.Headers.ContentMD5 = hash;
                }
            }

            if (!request.Headers.Date.HasValue)
            {
                //set request date, might have been set by an overriding handler
                request.Headers.Date = DateTime.UtcNow;   
            }
            
            string signature = Utilities.GenerateSignature(request, _userName, _authenticationToken);
            if (string.IsNullOrEmpty(signature))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Authentication Signature Invalid (See Specifications for \"" + Scheme + "\" Authentication Scheme)"));
                return tsc.Task;
            }

            //the complete header is a concatenation of the username and hash signature, separated by a SignatureSeparatorCharacter
            string authParameter = string.Format("{0}{2}{1}", _userName, signature, Utilities.SignatureSeparatorCharacter);

            request.Headers.Authorization = new AuthenticationHeaderValue(Scheme, authParameter);            

            return base.SendAsync(request, cancellationToken);
        }
    }
}