using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using System.IO;
using RainWorx.FrameWorx.Unity;
using System.Diagnostics;

namespace RainWorx.FrameWorx.MVC
{
    public partial class IPN : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            UserInput input = new UserInput("IPN_Response_Gateway", "IPN_Response_Gateway");
			using (StreamReader reader = new StreamReader(HttpContext.Current.Request.InputStream)) {
				input.Raw = reader.ReadToEnd();
			}
			
            foreach (string key in HttpContext.Current.Request.Params.AllKeys.Where(k => k != null)) {
                input.Items[key] = HttpContext.Current.Request.Params[key];
            }

            PaymentProviderResponse result = AccountingClient.ProcessAsynchronousPayment("IPN_Response_Gateway", input);
            if (result.Approved)
            {                
                Response.Write("SUCCESS");
            } else
            {
                Response.Write("FAILURE");
            }
        }
    }
}
