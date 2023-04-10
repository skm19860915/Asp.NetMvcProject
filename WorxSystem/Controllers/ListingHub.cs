using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Utility;
using System.Diagnostics;
using System.Configuration;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// allows SignalR to fire message pertaining to changes to listings in real time
    /// </summary>
    public class ListingHub : Hub
    {

        private readonly bool logSignalrConnectionIssues;

        /// <summary>
        /// Instantiates an instance of the ListingHub class
        /// </summary>
        public ListingHub()
        {
            logSignalrConnectionIssues = false;
            bool.TryParse(ConfigurationManager.AppSettings["LogSignalrConnectionIssues"] ?? "false", out logSignalrConnectionIssues);
        }

        /// <summary>
        /// registers interest in the specified listing to allow SignalR to fire for a specific listing
        /// </summary>
        /// <param name="listingID">ID of the specified listing</param>
        public void RegisterListingInterest(int? listingID)
        {
            Groups.Add(Context.ConnectionId, listingID.ToString());
        }

        /// <summary>
        /// registers interest in the specified event to allow SignalR to fire for a specific listing
        /// </summary>
        /// <param name="eventID">ID of the specified event</param>
        public void RegisterEventInterest(int eventID)
        {
            Groups.Add(Context.ConnectionId, eventID.ToString());
        }

        /// <summary>
        /// registers the username of the user viewing the page to allow SignalR needs to fire to a specific user
        /// </summary>
        /// <param name="userName">username of the specified user</param>
        public void RegisterUserName(string userName)
        {
            Groups.Add(Context.ConnectionId, userName);
        }

        /// <summary>
        /// Registers interest in all specified groups
        /// </summary>
        /// <param name="ids">a list of listing and/or event ID's</param>
        /// <param name="username">the username of the authenticated user</param>
        public void RegisterMultipleInterest(int[] ids, string username)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                Groups.Add(Context.ConnectionId, username);
            }
            if (ids == null || ids.Count() == 0)
            {
                Groups.Add(Context.ConnectionId, "AllListings");
            }
            else
            {
                foreach (int id in ids)
                {
                    Groups.Add(Context.ConnectionId, id.ToString());
                }
            }
        }

        #region SignalR Logging support

        private readonly static ConnectionMapping<string> _connections = new ConnectionMapping<string>();

        public override Task OnConnected()
        {
            try
            {
                if (logSignalrConnectionIssues)
                {
                    string username = !(Context.User == null) 
                        ? (!string.IsNullOrWhiteSpace(Context.User.Identity.Name) 
                            ? Context.User.Identity.Name 
                            : "anonymous") 
                        : "unknown";
                    _connections.Add(username, Context.ConnectionId);

                    var logProps = new Dictionary<string, object>();
                    logProps.Add("ConnectionId", Context.ConnectionId);
                    foreach (var nvp in Context.Headers.Where(nvp =>
                        nvp.Key.Equals("Referer", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }
                    foreach (var nvp in Context.Request.QueryString.Where(nvp =>
                        nvp.Key.Equals("transport", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("clientProtocol", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }

                    string ipaddy = GetRemoteIpAddress(Context.Request);
                    logProps.Add("IP Address", ipaddy);

                    logProps.Add("Total Connections", _connections.Count);
                    LogManager.WriteLog(string.Format("{0} now has {1} connections", username, _connections.GetConnections(username).Count()), 
                        "Connected", "SignalR Connections", TraceEventType.Verbose, username, null, logProps);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Logging Error (Connected)", "SignalR Connections", TraceEventType.Error, null, e);
            }

            return base.OnConnected();
        }

        private static string GetRemoteIpAddress(IRequest request)
        {
            try
            {
                object ipAddress;
                if (request.Environment.TryGetValue("server.RemoteIpAddress", out ipAddress))
                {
                    return ipAddress as string;
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Logging Error (GetRemoteIpAddress)", "SignalR Connections", TraceEventType.Error, null, e);
            }
            return null;
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            try
            {
                if (logSignalrConnectionIssues)
                {
                    string username = !(Context.User == null)
                        ? (!string.IsNullOrWhiteSpace(Context.User.Identity.Name)
                            ? Context.User.Identity.Name
                            : "anonymous")
                        : "unknown";
                    _connections.Remove(username, Context.ConnectionId);

                    var logProps = new Dictionary<string, object>();
                    logProps.Add("ConnectionId", Context.ConnectionId);
                    logProps.Add("Graceful Disconnect", stopCalled);
                    foreach (var nvp in Context.Headers.Where(nvp =>
                        nvp.Key.Equals("Referer", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }
                    foreach (var nvp in Context.Request.QueryString.Where(nvp =>
                        nvp.Key.Equals("transport", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("clientProtocol", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }
                    logProps.Add("Total Connections", _connections.Count);
                    var applicableSeverity = TraceEventType.Verbose;
                    //if (!stopCalled) applicableSeverity = TraceEventType.Warning;
                    LogManager.WriteLog(string.Format("{0} now has {1} connections", username, _connections.GetConnections(username).Count()), 
                        "Disconnected", "SignalR Connections", applicableSeverity, username, null, logProps);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Logging Error (Disconnected)", "SignalR Connections", TraceEventType.Error, null, e);
            }

            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            try
            {
                if (logSignalrConnectionIssues)
                {
                    string username = !(Context.User == null)
                        ? (!string.IsNullOrWhiteSpace(Context.User.Identity.Name)
                            ? Context.User.Identity.Name
                            : "anonymous")
                        : "unknown";
                    if (!_connections.GetConnections(username).Contains(Context.ConnectionId))
                    {
                        _connections.Add(username, Context.ConnectionId);
                    }

                    var logProps = new Dictionary<string, object>();
                    logProps.Add("ConnectionId", Context.ConnectionId);
                    foreach (var nvp in Context.Headers.Where(nvp =>
                        nvp.Key.Equals("Referer", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }
                    foreach (var nvp in Context.Request.QueryString.Where(nvp =>
                        nvp.Key.Equals("transport", StringComparison.OrdinalIgnoreCase) ||
                        nvp.Key.Equals("clientProtocol", StringComparison.OrdinalIgnoreCase)))
                    {
                        logProps[nvp.Key] = nvp.Value;
                    }
                    logProps.Add("Total Connections", _connections.Count);
                    LogManager.WriteLog(string.Format("{0} now has {1} connections", username, _connections.GetConnections(username).Count()), 
                        "Reconnected", "SignalR Connections", TraceEventType.Verbose, username, null, logProps);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Logging Error (Reconnected)", "SignalR Connections", TraceEventType.Error, null, e);
            }

            return base.OnReconnected();
        }

        public class ConnectionMapping<T>
        {
            private readonly Dictionary<T, HashSet<string>> _connections =
                new Dictionary<T, HashSet<string>>();

            public int Count
            {
                get
                {
                    return _connections.Count;
                }
            }

            public void Add(T key, string connectionId)
            {
                lock (_connections)
                {
                    HashSet<string> connections;
                    if (!_connections.TryGetValue(key, out connections))
                    {
                        connections = new HashSet<string>();
                        _connections.Add(key, connections);
                    }

                    lock (connections)
                    {
                        connections.Add(connectionId);
                    }
                }
            }

            public IEnumerable<string> GetConnections(T key)
            {
                HashSet<string> connections;
                if (_connections.TryGetValue(key, out connections))
                {
                    return connections;
                }

                return Enumerable.Empty<string>();
            }

            public void Remove(T key, string connectionId)
            {
                lock (_connections)
                {
                    HashSet<string> connections;
                    if (!_connections.TryGetValue(key, out connections))
                    {
                        return;
                    }

                    lock (connections)
                    {
                        connections.Remove(connectionId);

                        if (connections.Count == 0)
                        {
                            _connections.Remove(key);
                        }
                    }
                }
            }
        }
        #endregion SignalR Logging support


    }
}
