using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models
{
    /// <summary>
    /// Sort definitions for various &quot;Sort&quot; dropdown menus
    /// </summary>
    public static class QuerySortDefinitions
    {
        public static ListingPageQuery[] BrowseOptions;
        public static ListingPageQuery[] ListingSuccessOptions;
        public static ListingPageQuery[] ListingActiveOptions;
        public static ListingPageQuery[] ListingPendingOptions;
        public static ListingPageQuery[] ListingUnsuccessfulOptions;
        public static ListingPageQuery[] ListingEndedOptions;
        public static ListingPageQuery[] ListingDraftOptions;

        public static ListingPageQuery[] BidWatchOptions;
        public static ListingPageQuery[] BidActiveOptions;
        public static ListingPageQuery[] BidWonOptions;
        public static ListingPageQuery[] BidNotWonOptions;

        public static ListingPageQuery[] AttributeOptions;

        public static ListingPageQuery[] SiteFeesReportOptions;

        public static ListingPageQuery[] UserListOptions;

        public static ListingPageQuery[] InvoiceSalesOptions;

        public static ListingPageQuery[] MyEventsOptions;

        public static ListingPageQuery[] MyClosedEventsOptions;

        public static ListingPageQuery[] LotsByEventOptions;

        public static ListingPageQuery[] EventDetailOptions;

        public static ListingPageQuery[] BiddingOffersOptions;

        public static ListingPageQuery MergeBrowseOptions(int index, UserInput input, out bool validCategoryCounts)
        {
            ListingPageQuery retVal = new ListingPageQuery();

            retVal.Descending = BrowseOptions[index].Descending;
            retVal.Index = BrowseOptions[index].Index;
            retVal.Input = input;
            retVal.Name = BrowseOptions[index].Name;
            retVal.Sort = BrowseOptions[index].Sort;

            validCategoryCounts = true;
            return retVal;
        }

        public static ListingPageQuery MergeEventDetailOptions(int index, UserInput input, out bool validCategoryCounts)
        {
            ListingPageQuery retVal = new ListingPageQuery();

            retVal.Descending = EventDetailOptions[index].Descending;
            retVal.Index = EventDetailOptions[index].Index;
            retVal.Input = input;
            retVal.Name = EventDetailOptions[index].Name;
            retVal.Sort = EventDetailOptions[index].Sort;

            validCategoryCounts = true;
            return retVal;
        }

        static QuerySortDefinitions()
        {
            ListingPageQuery listingQuery;

            //Set for Browse
            BrowseOptions = new ListingPageQuery[10];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 0,
                Name = "EndingSoon"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "StartDTTM",
                Index = 1,
                Name = "Newest"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceLowHigh"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceHighLow"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 4,
                Name = "TitleAtoZ"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 5,
                Name = "TitleZtoA"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 6,
                Name = "ListingID0To9"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 7,
                Name = "ListingID9to0"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "AcceptedActionCount",
                Index = 8,
                Name = "ActivityHighToLow"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "AcceptedActionCount",
                Index = 9,
                Name = "ActivityLowToHigh"
            };
            BrowseOptions[listingQuery.Index] = listingQuery;


            //Set for EventDetailOptions
            EventDetailOptions = new ListingPageQuery[8];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "LotOrder",
                Index = 0,
                Name = "LotOrder"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 1,
                Name = "EndingSoon"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceLowHigh"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceHighLow"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "AcceptedActionCount",
                Index = 4,
                Name = "BidCountHighToLow"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "AcceptedActionCount",
                Index = 5,
                Name = "BidCountLowToHigh"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 6,
                Name = "TitleAtoZ"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 7,
                Name = "TitleZtoA"
            };
            EventDetailOptions[listingQuery.Index] = listingQuery;


            //Set for ListingSuccess
            ListingSuccessOptions = new ListingPageQuery[10];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Status",
                Index = 0,
                Name = "Paid"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Status",
                Index = 1,
                Name = "Unpaid"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "DateStamp",
                Index = 2,
                Name = "NewestSales"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "DateStamp",
                Index = 3,
                Name = "OldestSales"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "TotalAmount",
                Index = 4,
                Name = "PriceHighToLow"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "TotalAmount",
                Index = 5,
                Name = "PriceLowToHigh"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Description",
                Index = 6,
                Name = "TitleAtoZ"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Description",
                Index = 7,
                Name = "TitleZtoA"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Payer",
                Index = 8,
                Name = "BuyerAtoZ"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Payer",
                Index = 9,
                Name = "BuyerZtoA"
            };
            ListingSuccessOptions[listingQuery.Index] = listingQuery;

            //Set for ListingActive
            ListingActiveOptions = new ListingPageQuery[10];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 0,
                Name = "EndingFirst"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 1,
                Name = "EndingLast"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 4,
                Name = "ListingID0To9"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 5,
                Name = "ListingID9to0"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 6,
                Name = "TitleAtoZ"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 7,
                Name = "TitleZtoA"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "AcceptedActionCount",
                Index = 8,
                Name = "MostBids"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "AcceptedActionCount",
                Index = 9,
                Name = "FewestBids"
            };
            ListingActiveOptions[listingQuery.Index] = listingQuery;

            //Set for ListingPending
            ListingPendingOptions = new ListingPageQuery[10];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "StartDTTM",
                Index = 0,
                Name = "StartingFirst"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "StartDTTM",
                Index = 1,
                Name = "StartingLast"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 2,
                Name = "EndingFirst"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 3,
                Name = "EndingLast"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 4,
                Name = "PriceHighToLow"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 5,
                Name = "PriceLowToHigh"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 6,
                Name = "ListingID0To9"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 7,
                Name = "ListingID9to0"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 8,
                Name = "TitleAtoZ"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 9,
                Name = "TitleZtoA"
            };
            ListingPendingOptions[listingQuery.Index] = listingQuery;

            //Set for ListingUnsuccessful
            ListingUnsuccessfulOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 0,
                Name = "CloseDateNewToOld"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 1,
                Name = "CloseDateOldToNew"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 4,
                Name = "TitleAtoZ"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 5,
                Name = "TitleZtoA"
            };
            ListingUnsuccessfulOptions[listingQuery.Index] = listingQuery;

            //Set for ListingUnsuccessful
            ListingEndedOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 0,
                Name = "CloseDateNewToOld"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 1,
                Name = "CloseDateOldToNew"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 2,
                Name = "TitleAtoZ"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 3,
                Name = "TitleZtoA"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Status",
                Index = 4,
                Name = "StatusAtoZ"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Status",
                Index = 5,
                Name = "StatusZtoA"
            };
            ListingEndedOptions[listingQuery.Index] = listingQuery;

            //Set for ListingDrafts
            ListingDraftOptions = new ListingPageQuery[4];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 0,
                Name = "Newest"
            };
            ListingDraftOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 1,
                Name = "Oldest"
            };
            ListingDraftOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 2,
                Name = "TitleAtoZ"
            };
            ListingDraftOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 3,
                Name = "TitleZtoA"
            };
            ListingDraftOptions[listingQuery.Index] = listingQuery;

            //Set for BidWatching
            BidWatchOptions = new ListingPageQuery[4];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 0,
                Name = "EndingFirst"
            };
            BidWatchOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 1,
                Name = "EndingLast"
            };
            BidWatchOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            BidWatchOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            BidWatchOptions[listingQuery.Index] = listingQuery;

            //Set for BidActiveOptions
            BidActiveOptions = new ListingPageQuery[4];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 0,
                Name = "EndingFirst"
            };
            BidActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 1,
                Name = "EndingLast"
            };
            BidActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            BidActiveOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            BidActiveOptions[listingQuery.Index] = listingQuery;

            //set for BidWonOptions
            BidWonOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "DateStamp",
                Index = 0,
                Name = "NewestPurchases" // "NewestSales"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "DateStamp",
                Index = 1,
                Name = "OldestPurchases" // "OldestSales"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "TotalAmount",
                Index = 2,
                Name = "PriceHighToLow"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "TotalAmount",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Owner",
                Index = 4,
                Name = "SellerUserNameAToZ"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Owner",
                Index = 5,
                Name = "SellerUserNameZToA"
            };
            BidWonOptions[listingQuery.Index] = listingQuery;

            //set for BidNotWon
            BidNotWonOptions = new ListingPageQuery[4];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 0,
                Name = "CloseDateNewToOld"
            };
            BidNotWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 1,
                Name = "CloseDateOldToNew"
            };
            BidNotWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            BidNotWonOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            BidNotWonOptions[listingQuery.Index] = listingQuery;

            //set for SiteFeesReport
            SiteFeesReportOptions = new ListingPageQuery[8];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 0,
                Name = "IdLowToHigh"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 1,
                Name = "IdHighToLow"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "PaidDTTM",
                Index = 2,
                Name = "PaidDateNewToOld"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "PaidDTTM",
                Index = 3,
                Name = "PaidDateOldToNew"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Payer",
                Index = 4,
                Name = "PayerUserNameAToZ"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Payer",
                Index = 5,
                Name = "PayerUserNameZToA"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Total",
                Index = 6,
                Name = "TotalHighToLow"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Total",
                Index = 7,
                Name = "TotalLowToHigh"
            };
            SiteFeesReportOptions[listingQuery.Index] = listingQuery;

            //UserReportOptions
            UserListOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Id",
                Index = 0,
                Name = "IdLowToHigh"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Id",
                Index = 1,
                Name = "IdHighToLow"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "UserName",
                Index = 2,
                Name = "UserNameAtoZ"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "UserName",
                Index = 3,
                Name = "UserNameZtoA"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "LastLoginDate",
                Index = 4,
                Name = "LastLoginDateNewToOld"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "LastLoginDate",
                Index = 5,
                Name = "LastLoginDateOldToNew"
            };
            UserListOptions[listingQuery.Index] = listingQuery;

            //AttributeOptions
            AttributeOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Type",
                Index = 0,
                Name = "TypeAtoZ"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Type",
                Index = 1,
                Name = "TypeZtoA"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Context",
                Index = 2,
                Name = "ContextAtoZ"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Context",
                Index = 3,
                Name = "ContextZtoA"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Name",
                Index = 4,
                Name = "NameAtoZ"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Name",
                Index = 5,
                Name = "NameZtoA"
            };
            AttributeOptions[listingQuery.Index] = listingQuery;

            //Set for InvoiceSales
            InvoiceSalesOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CreatedDTTM",
                Index = 0,
                Name = "Newest"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CreatedDTTM",
                Index = 1,
                Name = "Oldest"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "PayerUN",
                Index = 2,
                Name = "BuyerAtoZ"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "PayerUN",
                Index = 3,
                Name = "BuyerZtoA"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Total",
                Index = 4,
                Name = "TotalLowToHigh"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Total",
                Index = 5,
                Name = "TotalHighToLow"
            };
            InvoiceSalesOptions[listingQuery.Index] = listingQuery;


            //Set for MyEventsOptions
            MyEventsOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CreatedOn",
                Index = 0,
                Name = "Newest"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CreatedOn",
                Index = 1,
                Name = "Oldest"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 2,
                Name = "EndingSoonest"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 3,
                Name = "EndingLast"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 4,
                Name = "TitleAtoZ"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 5,
                Name = "TitleZtoA"
            };
            MyEventsOptions[listingQuery.Index] = listingQuery;

            
            //Set for MyClosedEventsOptions
            MyClosedEventsOptions = new ListingPageQuery[6];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CreatedOn",
                Index = 0,
                Name = "Newest"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CreatedOn",
                Index = 1,
                Name = "Oldest"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 2,
                Name = "EndedEarliest"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 3,
                Name = "EndedRecently"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 4,
                Name = "TitleAtoZ"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 5,
                Name = "TitleZtoA"
            };
            MyClosedEventsOptions[listingQuery.Index] = listingQuery;

            //Set for LotsByEventOptions
            LotsByEventOptions = new ListingPageQuery[12];

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "EndDTTM",
                Index = 0,
                Name = "CloseDateNewToOld"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "EndDTTM",
                Index = 1,
                Name = "CloseDateOldToNew"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            //listingQuery = new ListingPageQuery()
            //{
            //    Descending = false,
            //    Sort = "StartDTTM",
            //    Index = -1,
            //    Name = "StartingFirst"
            //};
            //LotsByEventOptions[listingQuery.Index] = listingQuery;

            //listingQuery = new ListingPageQuery()
            //{
            //    Descending = true,
            //    Sort = "StartDTTM",
            //    Index = -1,
            //    Name = "StartingLast"
            //};
            //LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CurrentPrice",
                Index = 2,
                Name = "PriceHighToLow"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CurrentPrice",
                Index = 3,
                Name = "PriceLowToHigh"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            //listingQuery = new ListingPageQuery()
            //{
            //    Descending = false,
            //    Sort = "Id",
            //    Index = -1,
            //    Name = "ListingID0To9"
            //};
            //LotsByEventOptions[listingQuery.Index] = listingQuery;

            //listingQuery = new ListingPageQuery()
            //{
            //    Descending = true,
            //    Sort = "Id",
            //    Index = -1,
            //    Name = "ListingID9to0"
            //};
            //LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Title",
                Index = 4,
                Name = "TitleAtoZ"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Title",
                Index = 5,
                Name = "TitleZtoA"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Status",
                Index = 6,
                Name = "StatusAtoZ"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Status",
                Index = 7,
                Name = "StatusZtoA"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "LotOrder",
                Index = 8,
                Name = "LotOrderLowHigh"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "LotOrder",
                Index = 9,
                Name = "LotOrderHighLow"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "LotNumber",
                Index = 10,
                Name = "LotNumberAtoZ"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "LotNumber",
                Index = 11,
                Name = "LotNumberZtoA"
            };
            LotsByEventOptions[listingQuery.Index] = listingQuery;


            BiddingOffersOptions = new ListingPageQuery[8];
            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "CreatedOn",
                Index = 0,
                Name = "Newest"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "CreatedOn",
                Index = 1,
                Name = "Oldest"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "Amount",
                Index = 2,
                Name = "PriceLowHigh"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "Amount",
                Index = 3,
                Name = "PriceHighLow"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "UserName",
                Index = 4,
                Name = "UserNameAtoZ"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "UserName",
                Index = 5,
                Name = "UserNameZtoA"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = false,
                Sort = "ListingTitle",
                Index = 6,
                Name = "TitleAtoZ"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

            listingQuery = new ListingPageQuery()
            {
                Descending = true,
                Sort = "ListingTitle",
                Index = 7,
                Name = "TitleZtoA"
            };
            BiddingOffersOptions[listingQuery.Index] = listingQuery;

        }
    }
}
