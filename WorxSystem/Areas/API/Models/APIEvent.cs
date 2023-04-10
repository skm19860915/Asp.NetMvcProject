using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using RainWorx.FrameWorx.DTO;
using Category = RainWorx.FrameWorx.DTO.Category;
using ShippingOption = RainWorx.FrameWorx.DTO.ShippingOption;

namespace RainWorx.FrameWorx.MVC.Areas.API.Models
{
    public class APIEvent
    {
        //public List<Category> Categories { get; set; }
        //public string CurrencyCode { get; set; }
        //public string WinningUser { get; set; }
        //public decimal? CurrentPrice { get; set; }
        //public int CurrentQuantity { get; set; }
        //public List<string> Decorations { get; set; }
        //public string Description { get; set; }
        //public DateTime? EndDTTM { get; set; }
        //public int? Hits { get; set; }
        //public int ID { get; set; }
        //public decimal? Increment { get; set; }
        //public int ActionCount { get; set; }
        //public List<string> Locations { get; set; }
        //public List<DTO.Media.Media> Media { get; set; }
        //public string OwnerUserName { get; set; }
        //public Category PrimaryCategory { get; set; }
        //public List<CustomProperty> Properties { get; set; }
        //public List<ShippingOption> ShippingOptions { get; set; }
        //public DateTime? StartDTTM { get; set; }
        //public string Status { get; set; }
        //public string Title { get; set; }
        //public string Subtitle { get; set; }
        //public int Version { get; set; }
        //public string TypeName { get; set; }
        //public bool? ReserveMet { get; set; }
        //public string ImageURI { get; set; }

        public int ID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool ProxyBidding { get; set; }
        public DateTime? StartDTTM { get; set; }
        public DateTime EndDTTM { get; set; }
        public int ClosingGroupIncrementSeconds { get; set; }
        public int SoftClosingGroupIncrementSeconds { get; set; }
        public decimal BuyersPremiumPercent { get; set; }
        public string LastUpdatedUser { get; set; }
        public bool Published { get; set; }
        public string Status { get; set; }
        public int CurrentClosingGroup { get; set; }
        public int CurrentSoftClosingGroup { get; set; }
        public int CurrentLotOrder { get; set; }
        public string TimeZone { get; set; }
        public string TermsAndConditions { get; set; }
        public string PrimaryImageURI { get; set; }
        public string SecondaryImageURI { get; set; }
        public List<DTO.Media.Media> Media { get; set; }
        public List<CustomProperty> Properties { get; set; }
        //public User Owner { get; set; }
        public string OwnerUserName { get; set; }
        //public bool IsMediaFilled { get; set; }
        //public bool IsPropertiesFilled { get; set; }
        //public bool IsOwnerFilled { get; set; }
        public int LotCount { get; set; }
        public int CategoryID { get; set; }
        //public Currency Currency { get; set; }
        public string CurrencyCode { get; set; }
        public string ManagedByName { get; set; }
        public string Subtitle { get; set; }
        public string ShippingInfo { get; set; }
        public bool PreviewLots { get; set; }
        public DateTime? EstimatedLastEndDTTM { get; set; }
        public bool LotsTaxable { get; set; }
        public bool FollowLiveEnabled { get; set; }

        static APIEvent()
        {
            //moved to global.asax.cs to to ensure only a single call
            //Mapper.CreateMap<DTO.Event, APIEvent>()
            //    /*.ForMember(d => d.WinningUser,
            //        o => o.MapFrom(s => s.CurrentListingAction != null ? s.CurrentListingAction.UserName : string.Empty))
            //    .ForMember(d => d.Decorations, o => o.MapFrom(s => s.Decorations.Select(x => x.Name).ToList()))
            //    .ForMember(d => d.ActionCount,
            //        o =>
            //            o.MapFrom(s => s.AcceptedActionCount))
            //    //.ForMember(d => d.Locations, o => o.MapFrom(s => s.Locations.Select(x => x.Name).ToList()))
            //    //.ForMember(d => d.ReserveMet, o => o.ResolveUsing<ReserveMetResolver>())
            //    .ForMember(d => d.ImageURI, o => o.Ignore())*/;

            //Mapper.AssertConfigurationIsValid();
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        //class ReserveMetResolver : ValueResolver<DTO.Listing, bool?>
        //{
        //    /// <summary>
        //    /// Implementors override this method to resolve the destination value based on the provided source value
        //    /// </summary>
        //    /// <param name="source">Source value</param>
        //    /// <returns>
        //    /// Destination
        //    /// </returns>
        //    protected override bool? ResolveCore(DTO.Listing source)
        //    {
        //        decimal? reservePrice = null;

        //        CustomProperty reservePriceProperty =
        //            source.Properties.Where(p => p.Field.Name.Equals("ReservePrice", StringComparison.OrdinalIgnoreCase))
        //                .SingleOrDefault();

        //        if (reservePriceProperty != null && !string.IsNullOrEmpty(reservePriceProperty.Value))
        //        {
        //            reservePrice = decimal.Parse(reservePriceProperty.Value);
        //        }

        //        if (reservePrice.HasValue)
        //        {
        //            return source.CurrentPrice >= reservePrice;
        //        }
        //        return null;
        //    }
        //}

        public static APIEvent FromDTOEvent(DTO.Event auctionEvent)
        {
            return Mapper.Map<APIEvent>(auctionEvent);
        }
    }
}
