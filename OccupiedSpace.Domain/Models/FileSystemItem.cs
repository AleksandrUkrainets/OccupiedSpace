using System;

namespace OccupiedSpace.Domain.Models
{
    public class FileSystemItem
    {
        public FileSystemItemType Type { get; set; }

        public string FullPath { get; set; }

        public string Name { get; set; }

        public AdditionalProperty AdditionalProperty { get; set; }

        public DateTime Modified { get; set; }

        public double Size { get; set; }
    }
}