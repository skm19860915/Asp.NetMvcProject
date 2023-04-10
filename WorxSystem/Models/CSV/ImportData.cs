using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class ImportData
    {
        public List<ImportListing> ListingData { get; set; }
        public ImportListingStatus Status { get; set; }
        public string Disposition { get; set; }

        public ImportData()
        {
            ListingData = new List<ImportListing>();
            Status = ImportListingStatus.Success;
            Disposition = string.Empty;
        }
    }
}