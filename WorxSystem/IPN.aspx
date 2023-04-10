<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="IPN.aspx.cs" Inherits="RainWorx.FrameWorx.MVC.IPN" %>
<%@ Import Namespace="System.Diagnostics" %>

<%--
    Uncomment this to debug IPN's
<%    
    Dictionary<string, object> properties = new Dictionary<string, object>();
    foreach (string key in Request.QueryString.AllKeys.Where(k => k!= null))
    {
        properties.Add(key, Request.QueryString[key]);        
    }
    foreach (string key in Request.Form.AllKeys.Where(k => k != null))
    {
        properties.Add(key, Request.Form[key]);
    }   
    if (properties.Count > 0) LogManager.WriteLog("IPN Response", "IPN", "MVC", TraceEventType.Verbose, "IPN", null, properties, 0, 0, Environment.MachineName);
%>--%>
