namespace RainWorx.FrameWorx.MVC.Models
{
    public class TaxRate
    {
        //TODO: use LINQ and select as new anonymous type instead?
        public object ID { get; set; }
        public string Country { get; set;}
        public string State { get; set; }
        public decimal Rate { get; set; }
        public string TaxableShipping { get; set; }
    }
}
