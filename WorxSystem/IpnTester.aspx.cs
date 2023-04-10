using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace RainWorx.FrameWorx.MVC {

	public partial class IpnTester : System.Web.UI.Page {

		protected void Page_Load(object sender, EventArgs e) {
			if (!IsPostBack) {
				postUrl.Text = string.Format("http://{0}:{1}/IPN.aspx", Request.Url.Host, Request.Url.Port);
			}
		}

		protected void sendIpnButton_Click(object sender, EventArgs e) {
			errorLabel.Text = string.Empty;
			ipnPageResponse.Text = string.Empty;
			postData.Text = postData.Text.Trim();
			if (AsyncDummySuccess.Checked) {
				SetProvider("AsyncDummySuccess");
			}
			else if (AsyncDummyFailure.Checked) {
				SetProvider("AsyncDummyFailure");
			}
			try {
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(postUrl.Text);
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				using (StreamWriter writer = new StreamWriter(request.GetRequestStream())) {
					writer.Write(postData.Text);
				}
				HttpWebResponse response = (HttpWebResponse)request.GetResponse();
				using (StreamReader reader = new StreamReader(response.GetResponseStream())) {
					ipnPageResponse.Text = reader.ReadToEnd();
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.ToString());
				errorLabel.Text = ex.Message;
			}
		}

		private void SetProvider(string providerName) {
			string data = postData.Text;
			int providerIndex = data.IndexOf("provider=");
			if (providerIndex > -1) {
				int ampersandIndex = data.IndexOf('&', providerIndex);
				if (ampersandIndex > -1) {
					data = data.Remove(providerIndex, ampersandIndex - providerIndex + 1);
				}
				else {
					data = data.Remove(providerIndex);
				}
			}
			if (data.Length > 0) {
				data += "&";
			}
			data += "provider=" + providerName;
			postData.Text = data;
		}

	}

}