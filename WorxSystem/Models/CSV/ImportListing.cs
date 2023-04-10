using System.Collections.Generic;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public enum ImportListingStatus
    {
        Success,
        Exception,
        Validation,
        ParseError
    }

    public class ImportListing
    {
        public Dictionary<string, string> ColumnData { get; set; }

        public int Line { get; set; }
        public ImportListingStatus Status { get; set; }
        public List<string> Disposition { get; set; }

        public ImportListing()
        {
            ColumnData = new Dictionary<string, string>();
            Disposition = new List<string>();
            Status = ImportListingStatus.Success;
        }
    }
}
