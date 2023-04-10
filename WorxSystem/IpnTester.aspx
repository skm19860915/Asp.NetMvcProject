<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="IpnTester.aspx.cs" Inherits="RainWorx.FrameWorx.MVC.IpnTester" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    <h3 class="h3row">ipn tester</h3>
    <asp:Label runat="server" ID="errorLabel" ForeColor="Red" /><br />
    IPN page response: <asp:Label runat="server" ID="ipnPageResponse" ForeColor="Green" /><br />
    <table border="0">
		<tr>
			<td>IPN Post URL:</td>
			<td><asp:TextBox runat="server" ID="postUrl" Width="350px" /></td>
		</tr>
		<tr>
			<td>Provider:</td>
			<td>
				<asp:RadioButton runat="server" ID="AsyncDummySuccess" Text="AsyncDummySuccess" 
					Checked="true" GroupName="provider" /><br />
				<asp:RadioButton runat="server" ID="AsyncDummyFailure" Text="AsyncDummyFailure" 
					GroupName="provider" /><br />
				<asp:RadioButton runat="server" ID="OtherProvider" Text="Other" GroupName="provider" />
			</td>
		</tr>
		<tr>
			<td>Post data (optional):</td>
			<td><asp:TextBox runat="server" ID="postData" Rows="5" Height="101px" 
					Width="350px" /></td>
		</tr>
		<tr>
			<td></td>
			<td><asp:Button runat="server" ID="sendIpnButton" Text="Send IPN" 
					onclick="sendIpnButton_Click" /></td>
		</tr>
    </table>
    </div>
    </form>
</body>
</html>
