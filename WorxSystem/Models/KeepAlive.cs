using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.Utility;

namespace RainWorx.FrameWorx.MVC.Models
{
    public static class KeepAlive
    {
        private static Thread _keepAliveThread;
        private static readonly ManualResetEvent _keepAliveThreadSignal = new ManualResetEvent(false);
        private static int callCount = 0;

        public static void Start()
        {
            callCount++;
            if (callCount <= 1)
            {
                if (_keepAliveThread == null)
                {
                    _keepAliveThreadSignal.Reset();
                    _keepAliveThread = new Thread(KeepAliveProc) {Priority = ThreadPriority.Lowest};
                    _keepAliveThread.Start();
                    LogManager.WriteLog("Started Keep Alive Thread", "Keep Alive", Strings.FunctionalAreas.Site,
                                        TraceEventType.Start);
                }
            }
        }

        public static void Stop()
        {
            callCount--;
            if (callCount <= 0)
            {
                if (_keepAliveThread != null)
                {
                    _keepAliveThreadSignal.Set();
                    _keepAliveThread.Join();
                    _keepAliveThread = null;
                    LogManager.WriteLog("Stopped Keep Alive Thread", "Keep Alive", Strings.FunctionalAreas.Site,
                                        TraceEventType.Stop);
                }
            }
        }

        private static void KeepAliveProc()
        {            
            do
            {
                try
                {
                    WebRequest req = WebRequest.Create(SiteClient.Settings["URL"]);
                    req.GetResponse();
                    LogManager.WriteLog("Keeping Alive", "Keep Alive", Strings.FunctionalAreas.Site, TraceEventType.Information);
                } catch (Exception e)
                {
                    LogManager.WriteLog("Keeping Alive", "Keep Alive", Strings.FunctionalAreas.Site, TraceEventType.Error, null, e);
                }                
            } while (!_keepAliveThreadSignal.WaitOne(300000));
        }

        //private static KeepAlive instance;
        //private static object sync = new object();
        //private string _applicationUrl;
        //private string _cacheKey;

        //private KeepAlive(string applicationUrl)
        //{
        //    _applicationUrl = applicationUrl;
        //    _cacheKey = Guid.NewGuid().ToString();
        //    instance = this;
        //}

        //public static bool IsKeepingAlive
        //{
        //    get
        //    {
        //        lock (sync)
        //        {
        //            return instance != null;
        //        }
        //    }
        //}

        //public static void Start(string applicationUrl)
        //{
        //    if (IsKeepingAlive)
        //    {
        //        return;
        //    }
        //    lock (sync)
        //    {
        //        instance = new KeepAlive(applicationUrl);
        //        instance.Insert();
        //    }
        //}

        //public static void Stop()
        //{
        //    lock (sync)
        //    {
        //        HttpRuntime.Cache.Remove(instance._cacheKey);
        //        instance = null;
        //    }
        //}

        //private void Callback(string key, object value, CacheItemRemovedReason reason)
        //{
        //    if (reason == CacheItemRemovedReason.Expired)
        //    {
        //        FetchApplicationUrl();
        //        Insert();
        //    }
        //}

        //private void Insert()
        //{
        //    HttpRuntime.Cache.Add(_cacheKey,
        //        this,
        //        null,
        //        Cache.NoAbsoluteExpiration,
        //        new TimeSpan(0, 10, 0),
        //        CacheItemPriority.Normal,
        //        this.Callback);
        //}

        //private void FetchApplicationUrl()
        //{
        //    try
        //    {
        //        HttpWebRequest request = HttpWebRequest.Create(this._applicationUrl) as HttpWebRequest;
        //        using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
        //        {
        //            HttpStatusCode status = response.StatusCode;
        //            //log status
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        //log exception
        //    }
        //}
    }
}