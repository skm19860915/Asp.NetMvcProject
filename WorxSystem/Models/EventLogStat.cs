using System;
namespace RainWorx.FrameWorx.MVC.Models
{
    public class EventLogStat
    {
        public DateTime FromDate { get; set; }
        public int RangeMinutes { get; set; }
        public string Severity { get; set; }
        public int EntryCount { get; set; }
        public bool IsArchived { get; set; }
    }
}
