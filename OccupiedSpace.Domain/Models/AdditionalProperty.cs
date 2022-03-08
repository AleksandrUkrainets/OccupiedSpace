namespace OccupiedSpace.Domain.Models
{
    public class AdditionalProperty
    {
        public double Allocated { get; set; }

        public int CountFiles { get; set; }

        public int CountFolders { get; set; }

        public double PercentAllocated { get; set; }
    }
}