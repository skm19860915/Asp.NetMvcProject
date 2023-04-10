using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using RainWorx.FrameWorx.DTO;
using Category = RainWorx.FrameWorx.DTO.Category;
using ShippingOption = RainWorx.FrameWorx.DTO.ShippingOption;

namespace RainWorx.FrameWorx.MVC.Areas.API.Models
{
    public class APIListing
    {
        public List<Category> Categories { get; set; }
        public string CurrencyCode { get; set; }
        public string WinningUser { get; set; }
        public decimal? CurrentPrice { get; set; }
        public int CurrentQuantity { get; set; }
        public List<string> Decorations { get; set; }
        public string Description { get; set; }
        public DateTime? EndDTTM { get; set; }
        public int? Hits { get; set; }
        public int ID { get; set; }
        public decimal? Increment { get; set; }
        public int ActionCount { get; set; }
        public List<string> Locations { get; set; }
        public List<DTO.Media.Media> Media { get; set; }
        public string OwnerUserName { get; set; }
        public Category PrimaryCategory { get; set; }
        public List<CustomProperty> Properties { get; set; }
        public List<ShippingOption> ShippingOptions { get; set; }
        public DateTime? StartDTTM { get; set; }
        public string Status { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public int Version { get; set; }
        public string TypeName { get; set; }
        public bool? ReserveMet { get; set; }
        public string ImageURI { get; set; }

        public string LotNumber { get; set; }
        public int? EventID { get; set; }

        static APIListing()
        {
            //moved to global.asax.cs to to ensure only a single call
            //Mapper.CreateMap<DTO.Listing, APIListing>()
            //    .ForMember(d => d.WinningUser,
            //        o => o.MapFrom(s => s.CurrentListingAction != null ? s.CurrentListingAction.UserName : string.Empty))
            //    .ForMember(d => d.Decorations, o => o.MapFrom(s => s.Decorations.Select(x => x.Name).ToList()))
            //    .ForMember(d => d.ActionCount,
            //        o =>
            //            o.MapFrom(s => s.AcceptedActionCount))
            //    .ForMember(d => d.Locations, o => o.MapFrom(s => s.Locations.Select(x => x.Name).ToList()))
            //    .ForMember(d => d.ReserveMet, o => o.ResolveUsing<ReserveMetResolver>())
            //    .ForMember(d => d.ImageURI, o => o.Ignore())
            //    .ForMember(d => d.LotNumber, o => o.MapFrom(s => s.Lot != null ? s.Lot.LotNumber :  null))
            //    .ForMember(d => d.EventID, o => o.MapFrom(s => s.Lot != null ? (int?)s.Lot.Event.ID : null));
            //Mapper.AssertConfigurationIsValid();
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        public class ReserveMetResolver : ValueResolver<DTO.Listing, bool?>
        {
            /// <summary>
            /// Implementors override this method to resolve the destination value based on the provided source value
            /// </summary>
            /// <param name="source">Source value</param>
            /// <returns>
            /// Destination
            /// </returns>
            protected override bool? ResolveCore(DTO.Listing source)
            {
                decimal? reservePrice = null;

                CustomProperty reservePriceProperty =
                    source.Properties.Where(p => p.Field.Name.Equals("ReservePrice", StringComparison.OrdinalIgnoreCase))
                        .SingleOrDefault();

                if (reservePriceProperty != null && !string.IsNullOrEmpty(reservePriceProperty.Value))
                {
                    reservePrice = decimal.Parse(reservePriceProperty.Value);
                }

                if (reservePrice.HasValue)
                {
                    return source.CurrentPrice >= reservePrice;
                }
                return null;
            }
        }

        public static APIListing FromDTOListing(DTO.Listing fullListing)
        {
            return Mapper.Map<APIListing>(fullListing);
        }
    }
}