using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR.Hubs;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.Strings;
using Newtonsoft.Json;
using System.Configuration;

namespace RainWorx.FrameWorx.MVC.Models
{
    public class SignalRLoggingModule : HubPipelineModule
    {

        private static bool? _logSignalrStats = null;

        private bool LogSignalrStats()
        {
            if (!_logSignalrStats.HasValue)
            {
                _logSignalrStats = false;
                bool temp;
                if (bool.TryParse(ConfigurationManager.AppSettings["LogSignalrStats"] ?? "false", out temp))
                {
                    _logSignalrStats = temp;
                }
            }
            return _logSignalrStats.Value;
        }

        protected override bool OnBeforeIncoming(IHubIncomingInvokerContext context)
        {
            //Debug.WriteLine("=> Invoking " + context.MethodDescriptor.Name + " on hub " + context.MethodDescriptor.Hub.Name);
            return base.OnBeforeIncoming(context);
        }
        protected override bool OnBeforeOutgoing(IHubOutgoingInvokerContext context)
        {
            try
            {
                if (LogSignalrStats())
                {
                    var logProps = new Dictionary<string, object>();
                    int i = 0;
                    foreach (object arg in context.Invocation.Args)
                    {
                        string key = string.Format("Arg {0}", i++);
                        object value = JSON.Serialize(arg).Replace("\",", "\" ,").Replace("\"", "`");
                        logProps[key] = value;
                    }
                    int numClients = 1;
                    if (context.Signals != null)
                    {
                        int j = 0;
                        foreach (string signal in context.Signals)
                        {
                            string key = string.Format("context.Signals[{0}]", j++);
                            object value = signal;
                            logProps[key] = value;
                        }
                        numClients = j;
                    }
                    logProps.Add("context.Signal", context.Signal);
                    LogManager.WriteLog(string.Format("Sent to {0} groups", numClients), string.Format("{0}", context.Invocation.Method), 
                        "SignalR Stats", TraceEventType.Verbose, null, null, logProps);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Logging Error (OnBeforeOutgoing)", "SignalR Stats", TraceEventType.Error, null, e);
            }

            //Debug.WriteLine("<= Invoking " + context.Invocation.Method + " on client hub " + context.Invocation.Hub);
            return base.OnBeforeOutgoing(context);
        }
    }
}
