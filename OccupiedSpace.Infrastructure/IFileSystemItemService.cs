using OccupiedSpace.Domain.Models;
using System.Collections.Generic;

namespace OccupiedSpace.Infrastructure
{
    public interface IFileSystemItemService
    {
        public double AllocatedAll { get; }

        public bool ToCalculateSize { get; set; }

        public double PercentFirstFolder { get; }

        public List<FileSystemItem> GetDirectoryContent(string fullPath);

        public FileSystemItem GetFile(string fullPath);

        public List<FileSystemItem> GetFilesInDirectory(string fullPath);
    }
}