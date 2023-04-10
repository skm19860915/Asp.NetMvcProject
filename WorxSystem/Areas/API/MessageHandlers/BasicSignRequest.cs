using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class BasicSignRequest : DelegatingHandler
    {
        private readonly string _userName;
        private readonly string _password;
        private const string Scheme = "RWX_BASIC";

        public BasicSignRequest(string username, string password)
        {
            _userName = username;
            _password = password;
        }

        public BasicSignRequest(string username, string password, HttpMessageHandler inner)
            : base(inner)
        {
            _userName = username;
            _password = password;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                               System.Threading.CancellationToken cancellationToken)
        {            
            //the complete header is a concatenation of the username and password, separated by a SignatureSeparatorCharacter
            string authParameter = string.Format("{0}{2}{1}", _userName, _password, Utilities.SignatureSeparatorCharacter);

            request.Headers.Authorization = new AuthenticationHeaderValue(Scheme, authParameter);

            return base.SendAsync(request, cancellationToken);
        }
    }
}