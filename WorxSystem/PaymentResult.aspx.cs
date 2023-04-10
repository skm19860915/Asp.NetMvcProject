using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace RainWorx.FrameWorx.MVC {

	public partial class PaymentResult : System.Web.UI.Page {
	
		protected void Page_Load(object sender, EventArgs e) {
			//TODO: decide where to go.  paypal (for example) posts the transaction data back to this page, so we know the invoice number, etc.
            int? invoiceId = null;
            int temp;
            if (int.TryParse(Request["invoiceId"], out temp))
            {
                invoiceId = temp;
            }
            bool isSuccessful;
            if (bool.TryParse(Request["success"], out isSuccessful) && isSuccessful)
            {
                if (invoiceId.HasValue)
                {
                    Response.Redirect("~/Account/PaymentReceived?invoice=" + invoiceId.Value);
                }
                else
                {
                    Response.Redirect("~/Account/PaymentReceived");
                }
            }
            if (invoiceId.HasValue)
            {
                Response.Redirect("~/Account/Invoice/" + invoiceId.Value.ToString() + "?approved=false&message=PaymentCancelled");
            }
			Response.Redirect("~/Account");
		}

	}

}