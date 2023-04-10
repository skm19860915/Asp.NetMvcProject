using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Http;
using System.Web.Script.Serialization;
using System.Xml;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

using Newtonsoft.Json;

namespace RainWorx.FrameWorx.MVC.Areas.HelpPage
{
    /// <summary>
    /// Use this class to customize the Help Page.
    /// For example you can set a custom <see cref="System.Web.Http.Description.IDocumentationProvider"/> to supply the documentation
    /// or you can provide the samples for the requests/responses.
    /// </summary>
    public static class HelpPageConfig
    {
        public static void Register(HttpConfiguration config)
        {
            //// Uncomment the following to use the documentation from XML documentation file.
            config.SetDocumentationProvider(new XmlDocumentationProvider(HttpContext.Current.Server.MapPath("~/App_Data/XmlDocument.xml")));

            //// Uncomment the following to use "sample string" as the sample for all actions that have string as the body parameter or return type.
            //// Also, the string arrays will be used for IEnumerable<string>. The sample objects will be serialized into different media type 
            //// formats by the available formatters.
            //config.SetSampleObjects(new Dictionary<Type, object>
            //{
            //    {typeof(string), "sample string"},
            //    {typeof(IEnumerable<string>), new string[]{"sample 1", "sample 2"}}
            //});

            //// Uncomment the following to use "[0]=foo&[1]=bar" directly as the sample for all actions that support form URL encoded format
            //// and have IEnumerable<string> as the body parameter or return type.
            //config.SetSampleForType("[0]=foo&[1]=bar", new MediaTypeHeaderValue("application/x-www-form-urlencoded"), typeof(IEnumerable<string>));

            //// Uncomment the following to use "1234" directly as the request sample for media type "text/plain" on the controller named "Values"
            //// and action named "Put".
            //config.SetSampleRequest("1234", new MediaTypeHeaderValue("text/plain"), "Values", "Put");

            //// Uncomment the following to use the image on "../images/aspNetHome.png" directly as the response sample for media type "image/png"
            //// on the controller named "Values" and action named "Get" with parameter "id".
            //config.SetSampleResponse(new ImageSample("../images/aspNetHome.png"), new MediaTypeHeaderValue("image/png"), "Values", "Get", "id");

            //// Uncomment the following to correct the sample request when the action expects an HttpRequestMessage with ObjectContent<string>.
            //// The sample will be generated as if the controller named "Values" and action named "Get" were having string as the body parameter.
            //config.SetActualRequestType(typeof(string), "Values", "Get");

            //// Uncomment the following to correct the sample response when the action returns an HttpResponseMessage with ObjectContent<string>.
            //// The sample will be generated as if the controller named "Values" and action named "Post" were returning a string.
            //config.SetActualResponseType(typeof(string), "Values", "Post");

            string sample = string.Empty;
            sample += "------WebKitFormBoundary8A2SseNHkA0cadMw" + Environment.NewLine;
            sample += "Content-Disposition: form-data; name=\"anything\"; filename=\"file1.jpg\"" + Environment.NewLine;
            sample += "Content-Type: image/jpeg" + Environment.NewLine;
            sample += Environment.NewLine;
            sample += "[...binary...]" + Environment.NewLine;
            sample += "------WebKitFormBoundary8A2SseNHkA0cadMw" + Environment.NewLine;
            sample += "Content-Disposition: form-data; name=\"anything\"; filename=\"file2.jpg\"" + Environment.NewLine;
            sample += "Content-Type: image/jpeg" + Environment.NewLine;
            sample += Environment.NewLine;
            sample += "[...binary...]" + Environment.NewLine;
            sample += "------WebKitFormBoundary8A2SseNHkA0cadMw" + Environment.NewLine;
            sample += "Content-Disposition: form-data; name=\"context\"" + Environment.NewLine;
            sample += Environment.NewLine;
            sample += "UploadListingImage" + Environment.NewLine;
            sample += "------WebKitFormBoundary8A2SseNHkA0cadMw--" + Environment.NewLine;
            config.SetSampleRequest(sample, new MediaTypeHeaderValue("multipart/form-data"), "Media", "Post");

            //register user request samples
            UserInput regUserSampleInput = new UserInput("MyNewUserName", "MyNewUserName", "en", "en");
            regUserSampleInput.Items.Add("agreements", "True");
            regUserSampleInput.Items.Add("City", "S Burlington");
            regUserSampleInput.Items.Add("confirmEmail", "support@rainworx.com");
            regUserSampleInput.Items.Add("confirmPassword", "MyPaSs1");
            regUserSampleInput.Items.Add("Country", "233");
            regUserSampleInput.Items.Add("Email", "support@rainworx.com");
            regUserSampleInput.Items.Add("FirstName", "John");
            regUserSampleInput.Items.Add("LastIP", "192.168.0.1");
            regUserSampleInput.Items.Add("LastName", "Smith");
            regUserSampleInput.Items.Add("Newsletter", "True");
            regUserSampleInput.Items.Add("Password", "MyPaSs1");
            regUserSampleInput.Items.Add("StateRegion", "VT");
            regUserSampleInput.Items.Add("Street1", "4049 Williston Rd Suite 11");
            regUserSampleInput.Items.Add("Street2", "");
            regUserSampleInput.Items.Add("UserName", "MyNewUserName");
            regUserSampleInput.Items.Add("ZipPostal", "05403");
            string regUserJson = GetIndentedJSON(regUserSampleInput);
            config.SetSampleRequest(regUserJson, new MediaTypeHeaderValue("text/json"), "User", "RegisterUserAsync");
            config.SetSampleRequest(regUserJson, new MediaTypeHeaderValue("application/json"), "User", "RegisterUserAsync");
            string regUserXml = GetIndentedXML(regUserSampleInput);
            config.SetSampleRequest(regUserXml, new MediaTypeHeaderValue("text/xml"), "User", "RegisterUserAsync");
            config.SetSampleRequest(regUserXml, new MediaTypeHeaderValue("application/xml"), "User", "RegisterUserAsync");

        }

        private static string GetIndentedJSON(object myObject)
        {
            string jsonResult = JsonConvert.SerializeObject(myObject, Newtonsoft.Json.Formatting.Indented);
            return jsonResult;
        }
        private static string GetIndentedXML(object myObject)
        {
            string xmlResult = string.Empty;
            var serializer = new DataContractSerializer(myObject.GetType());
            using (var sw = new StringWriter())
            {
                using (var writer = new XmlTextWriter(sw))
                {
                    writer.Formatting = System.Xml.Formatting.Indented;
                    serializer.WriteObject(writer, myObject);
                    writer.Flush();
                    xmlResult = sw.ToString();
                }
            }
            return xmlResult;
        }

    }
}