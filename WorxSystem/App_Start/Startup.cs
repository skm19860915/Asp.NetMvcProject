using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;
using RainWorx.FrameWorx.Unity;
using RainWorx.FrameWorx.Queueing;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Utility;

[assembly: OwinStartup(typeof(RainWorx.FrameWorx.MVC.Startup))]
namespace RainWorx.FrameWorx.MVC
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {

            //note: this must be called prior to app.MapSignalR() in order for SignalR methods to have access to usernames
            ConfigureAuth(app);

            var queueManager = UnityResolver.Get<IQueueManager>();
            if (queueManager.GetType() != typeof(QueueingDisabled))
            {
                bool logSignalrStats = false;
                bool.TryParse(ConfigurationManager.AppSettings["LogSignalrStats"] ?? "false", out logSignalrStats);
                if (logSignalrStats)
                {
                    try
                    {
                        //note: this must be called prior to any GlobalHost.DependencyResolver calls
                        GlobalHost.HubPipeline.AddModule(new SignalRLoggingModule());
                    }
                    catch (Exception e)
                    {
                        LogManager.WriteLog(null, "SignalRLoggingModule was already added", "MVC.Startup", System.Diagnostics.TraceEventType.Warning, null, e);
                    }
                }

                bool scaleoutEnabled = false;
                bool.TryParse(ConfigurationManager.AppSettings["SignalR_ScaleoutEnabled"], out scaleoutEnabled);
                if (scaleoutEnabled)
                {
                    if (queueManager.GetType() == typeof(AzureServiceBus))
                    {
                        int signalR_TopicCount = 5; //5 is the default value for Service Bus
                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["SignalR_ServiceBusTopicCount"]))
                        {
                            int.TryParse(ConfigurationManager.AppSettings["SignalR_ServiceBusTopicCount"], out signalR_TopicCount);
                        }
                        var config = new ServiceBusScaleoutConfiguration(ConfigurationManager.ConnectionStrings["azure_service_bus"].ConnectionString, "AweSignalR")
                        {
                            TopicCount = signalR_TopicCount
                        };
                        int signalR_MaxQueueLength = 0; //0 (disabled) is the default value
                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["SignalR_MaxQueueLength"]))
                        {
                            int.TryParse(ConfigurationManager.AppSettings["SignalR_MaxQueueLength"], out signalR_MaxQueueLength);
                        }
                        if (signalR_MaxQueueLength > 0)
                        {
                            config.MaxQueueLength = signalR_MaxQueueLength;
                        }
                        GlobalHost.DependencyResolver.UseServiceBus(config);
                    }
                    else if (queueManager.GetType() == typeof(SQLServiceBroker) || queueManager.GetType() == typeof(SimpleSSB))
                    {
                        string signalrConnStr;
                        if (ConfigurationManager.ConnectionStrings["db_connection_signalr"] != null)
                        {
                            signalrConnStr = ConfigurationManager.ConnectionStrings["db_connection_signalr"].ConnectionString;
                        }
                        else
                        {
                            signalrConnStr = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;
                        }
                        int signalR_TableCount = 1; //1 is the default value for SQL
                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["SignalR_SqlTableCount"]))
                        {
                            int.TryParse(ConfigurationManager.AppSettings["SignalR_SqlTableCount"], out signalR_TableCount);
                        }
                        var config = new SqlScaleoutConfiguration(signalrConnStr)
                        {
                            TableCount = signalR_TableCount
                        };
                        int signalR_MaxQueueLength = 0; //0 (disabled) is the default value
                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["SignalR_MaxQueueLength"]))
                        {
                            int.TryParse(ConfigurationManager.AppSettings["SignalR_MaxQueueLength"], out signalR_MaxQueueLength);
                        }
                        if (signalR_MaxQueueLength > 0)
                        {
                            config.MaxQueueLength = signalR_MaxQueueLength;
                        }
                        GlobalHost.DependencyResolver.UseSqlServer(config);
                    }
                }
                app.MapSignalR();
            }
            queueManager = null;
        }
    }
}
