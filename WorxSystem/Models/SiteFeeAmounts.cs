using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Utility;

namespace RainWorx.FrameWorx.MVC.Models
{
    public class SiteFeeAmounts
    {
        public List<SiteFee> Fees { get; private set; }
        public bool PayToProceed;

        public SiteFeeAmounts()
        {
            Fees = new List<SiteFee>();
        }

        public SiteFeeAmounts(UserInput input)
        {
            Fees = new List<SiteFee>();

            //first get a list of distinct Fee input groups
            Dictionary<string,string> feeNames = new Dictionary<string,string>();
            foreach(string key in input.Items.Keys)
            {
                if (key.EndsWith("_FeeName"))
                {
                    string feeFieldPrefix = key.Replace("FeeName", "");
                    string feeName = input.Items[key];
                    if (!feeNames.ContainsKey(key))
                    {
                        feeNames.Add(feeFieldPrefix, feeName);
                    }
                }
            }

            //next, convert these groups into the formal simple SiteFee structure
            foreach(string feeKey in feeNames.Keys)
            {
                decimal uBoundAmount = 0;
                decimal feeAmount;
                string feeType = input.Items[feeKey + "FeeType"];
                switch(feeType)
                {
                    case "TieredFlatFee":
                        TieredFlatFee tff = new TieredFlatFee()
                        {
                            Name = input.Items[feeKey + "FeeName"],
                            Description = input.Items[feeKey + "FeeDesc"]
                        };
                        foreach(string tierKey in input.Items.Keys.Where(
                            k => k.StartsWith(feeKey) && k.Contains(feeKey + "TierUbound_")))
                        {
                            //a blank/null upper bound "max value"
                            if (string.IsNullOrEmpty(input.Items[tierKey]))
                                uBoundAmount = Utilities.MaxMoneyValue();
                            else
                                uBoundAmount = decimal.Parse(input.Items[tierKey], NumberStyles.Currency, CultureInfo.GetCultureInfo(input.CultureName));
                            string amountKey = tierKey.Replace("TierUbound_", "TierFeeAmount_");
                            feeAmount = decimal.Parse(input.Items[amountKey], NumberStyles.Currency, CultureInfo.GetCultureInfo(input.CultureName));
                            tff.FeeAmountTiers.Add(new FlatFeeTier()
                            {
                                UpperBound = uBoundAmount,
                                FeeAmount = feeAmount
                            });
                        }
                        Fees.Add(tff);
                        break;
                    case "TieredPercentFee":
                        TieredPercentFee tpf = new TieredPercentFee()
                        {
                            Name = input.Items[feeKey + "FeeName"],
                            Description = input.Items[feeKey + "FeeDesc"]
                        };
                        foreach(string tierKey in input.Items.Keys.Where(
                            k => k.StartsWith(feeKey) && k.Contains(feeKey + "TierUbound_")))
                        {
                            //a blank/null upper bound "max value"
                            if (string.IsNullOrEmpty(input.Items[tierKey]))
                                uBoundAmount = Utilities.MaxMoneyValue();
                            else
                                uBoundAmount = decimal.Parse(input.Items[tierKey], NumberStyles.Currency, CultureInfo.GetCultureInfo(input.CultureName));
                            string amountKey = tierKey.Replace("TierUbound_", "TierFeeAmount_");
                            feeAmount = decimal.Parse(input.Items[amountKey], NumberStyles.Currency, CultureInfo.GetCultureInfo(input.CultureName));
                            tpf.FeePercentTiers.Add(new PercentFeeTier()
                            {
                                UpperBound = uBoundAmount,
                                FeePercent =feeAmount
                            });
                        }
                        Fees.Add(tpf);
                        break;
                    case "FlatFee":
                        FlatFee ff = new FlatFee()
                        {
                            Name = input.Items[feeKey + "FeeName"],
                            Description = input.Items[feeKey + "FeeDesc"],
                            FeeAmount = decimal.Parse(input.Items[feeKey + "FeeAmount"], NumberStyles.Currency, CultureInfo.GetCultureInfo(input.CultureName))
                        };
                        Fees.Add(ff);
                        break;
                    //case "PercentFee"
                    //    break;
                }
            }
        }

        public void AddTieredFlatFee(FeeSchedule tieredFee, string feeName)
        {
            TieredFlatFee siteFeeModel = new TieredFlatFee
            {
                Name = feeName,
                Description = tieredFee.Description
            };

            foreach (Tier t in tieredFee.Tiers)
            {
                siteFeeModel.FeeAmountTiers.Add(new FlatFeeTier
                {
                    FeeAmount = t.Value,
                    UpperBound = t.UpperBoundExclusive
                });
            }
            Fees.Add(siteFeeModel);
        }

        public void AddTieredPercentFee(FeeSchedule tieredFee, string feeName)
        {
            TieredPercentFee siteFeeModel = new TieredPercentFee
            {
                Name = feeName,
                Description = tieredFee.Description
            };

            foreach (Tier t in tieredFee.Tiers)
            {
                siteFeeModel.FeePercentTiers.Add(new PercentFeeTier
                {
                    FeePercent = t.Value,
                    UpperBound = t.UpperBoundExclusive
                });
            }
            Fees.Add(siteFeeModel);
        }

        public void AddFlatFees(List<FeeProperty> fees)
        {
            foreach(FeeProperty f in fees)
            {
                Fees.Add(new FlatFee 
                {
                    Name = f.Name,
                    Description = f.Description,
                    FeeAmount = f.Amount
                });
            }
        }

        public void AddFlatFees(List<Location> locations)
        {
            foreach(Location l in locations)
            {
                Fees.Add(new FlatFee 
                {
                    Name = l.Name,
                    Description = l.Description,
                    FeeAmount = l.Amount
                });
            }
        }

        public void AddFlatFees(List<Decoration> decorations)
        {
            foreach(Decoration d in decorations)
            {
                Fees.Add(new FlatFee
                {
                    Name = d.Name,
                    Description = d.Description,
                    FeeAmount = d.Amount
                });
            }
        }

    }

    public class SiteFee
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string FeeType { get; protected set; }
    }

    public class FlatFee : SiteFee
    {
        public decimal FeeAmount { get; set; }
        public FlatFee()
        {
            FeeType = "FlatFee";
        }
    }

    public class TieredFlatFee : SiteFee
    {
        public List<FlatFeeTier> FeeAmountTiers;
        public TieredFlatFee()
        {
            FeeAmountTiers = new List<FlatFeeTier>();
            FeeType = "TieredFlatFee";
        }
    }

    public class FlatFeeTier
    {
        public decimal FeeAmount { get; set; }
        public decimal? UpperBound { get; set; }
    }

    public class TieredPercentFee : SiteFee
    {
        public List<PercentFeeTier> FeePercentTiers;
        public TieredPercentFee()
        {
            FeePercentTiers = new List<PercentFeeTier>();
            FeeType = "TieredPercentFee";
        }
    }
    
    public class PercentFeeTier
    {
        public decimal FeePercent { get; set; }
        public decimal? UpperBound { get; set; }
    }

}