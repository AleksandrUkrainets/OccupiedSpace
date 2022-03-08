using OccupiedSpace.Domain.Models;
using OccupiedSpace.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading.Tasks;

namespace OccupiedSpace.Program.Services
{
    public class FileSystemItemService : IFileSystemItemService
    {
        public double AllocatedAll { get; private set; } = 100;

        public double PercentFirstFolder { get; private set; } = 100;

        public bool ToCalculateSize { get; set; }

        private const double BYTEinMB = 1073741824;

        private readonly object _locker = new object();

        public List<FileSystemItem> GetDirectoryContent(string fullPath)
        {
            var readPermision = new FileIOPermission(PermissionState.None);
            readPermision.AllFiles = FileIOPermissionAccess.Read;
            readPermision.AllLocalFiles = FileIOPermissionAccess.Read;
            try
            {
                readPermision.Demand();
            }
            catch (SecurityException s)
            {
                Trace.WriteLine("readPermision called exepsion: " + s.Message);
            }

            var directoryContent = new List<FileSystemItem>();

            directoryContent.AddRange(GetFoldersInDirectory(fullPath));

            directoryContent.AddRange(GetFilesInDirectory(fullPath));

            return directoryContent;
        }

        private List<FileSystemItem> GetFoldersInDirectory(string fullPath)
        {
            var folders = new List<FileSystemItem>();
            try
            {
                var dirsInfo = new DirectoryInfo(fullPath);
                var dirs = dirsInfo.GetDirectories("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false
                });

                foreach (var dir in dirs)
                {
                    var (catalogSize, catalogsCount, filesCount) = (0.0, 0, 0);
                    double allocatedItemSize = 0;

                    if (dir.Exists)
                    {
                        if (ToCalculateSize)
                        {
                            (catalogSize, catalogsCount, filesCount) = GetSizeAndCountItems(dir.FullName);
                            allocatedItemSize = GetItemsSizeOnDisk(dir.FullName, FileSystemItemType.Folder);
                        }

                        folders.Add(new FileSystemItem
                        {
                            FullPath = dir.FullName,
                            Type = FileSystemItemType.Folder,
                            Name = dir.Name,
                            Modified = dir.LastWriteTime,
                            Size = (double)catalogSize / BYTEinMB,
                            AdditionalProperty =
                            new AdditionalProperty
                            {
                                Allocated = allocatedItemSize / BYTEinMB,
                                CountFiles = filesCount,
                                CountFolders = catalogsCount,
                                PercentAllocated = 0,
                            },
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("GetFoldersInDirectory method called exeption: " + e);
            }

            return folders;
        }

        public List<FileSystemItem> GetFilesInDirectory(string fullPath)
        {
            var filesInDirectory = new List<FileSystemItem>();
            try
            {
                var dirsInfo = new DirectoryInfo(fullPath);
                var files = dirsInfo.GetFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true
                });

                foreach (var file in files)
                {
                    if (file.Exists)
                    {
                        var newFile = GetFile(file.FullName);
                        filesInDirectory.Add(newFile);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("GetFilesInDirectory method called exeption: " + e);
            }

            return filesInDirectory;
        }

        public FileSystemItem GetFile(string fullPath)
        {
            double allocatedItemSize = 0;
            var file = new FileInfo(fullPath);
            var newFile = new FileSystemItem();
            if (file.Exists)
            {
                if (ToCalculateSize)
                {
                    allocatedItemSize = GetItemsSizeOnDisk(file.FullName, FileSystemItemType.File);
                }

                newFile.FullPath = file.FullName;
                newFile.Type = FileSystemItemType.File;
                newFile.Name = file.Name;
                newFile.Modified = file.LastWriteTime;
                newFile.Size = (double)file.Length / BYTEinMB;
                newFile.AdditionalProperty =
                new AdditionalProperty
                {
                    Allocated = allocatedItemSize / BYTEinMB,
                    CountFiles = 1,
                    CountFolders = 0,
                    PercentAllocated = 0
                };
            }

            return newFile;
        }

        private (long catalogSize, int catalogsCount, int filesCount) GetSizeAndCountItems(string folder)
        {
            var result = (catalogSize: (long)0, catalogsCount: 0, filesCount: 0);

            try
            {
                if (ToCalculateSize)
                {
                    var di = new DirectoryInfo(folder);
                    var directories = di.GetDirectories("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false
                    });
                    var files = di.GetFiles("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true
                    });
                    Parallel.ForEach(files, file =>
                    {
                        if (file.Exists)
                        {
                            lock (_locker)
                            {
                                result.catalogSize += file.Length;
                                result.filesCount++;
                            }
                        }
                    });

                    foreach (var directory in directories)
                    {
                        if (directory.Exists)
                        {
                            var (catalogSize, catalogsCount, filesCount) = GetSizeAndCountItems(directory.FullName);

                            result.catalogsCount++;
                            result.catalogsCount += catalogsCount;
                            result.catalogSize += catalogSize;
                            result.filesCount += filesCount;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("GetSizeAndCountItems method called exeption: " + e);
            }

            return result;
        }

        private double GetItemsSizeOnDisk(string path, FileSystemItemType typeItem)
        {
            double allocatedItemSize = 0;
            try
            {
                FileInfo[] files;

                if (typeItem == FileSystemItemType.File)
                {
                    files = new FileInfo[] { new FileInfo(path) };
                }
                else
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var directories = directoryInfo.GetDirectories("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false
                    });
                    files = directoryInfo.GetFiles("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true
                    });

                    foreach (var directory in directories)
                    {
                        allocatedItemSize += GetItemsSizeOnDisk(directory.FullName, FileSystemItemType.Folder);
                    }
                }
                Parallel.ForEach(files, file =>
                {
                    lock (_locker)
                    {
                        allocatedItemSize += GetFileSizeOnDisk(file.FullName);
                    }
                });
            }
            catch (Exception e)
            {
                Trace.WriteLine("GetItemsSizeOnDisk method called exeption: " + e);
            }

            return allocatedItemSize;
        }

        public static double GetFileSizeOnDisk(string file)
        {
            var info = new FileInfo(file);
            int result = GetDiskFreeSpaceW(info.Directory.Root.FullName, out uint sectorsPerCluster, out uint bytesPerSector, out _, out _);
            if (result == 0)
            {
                throw new Win32Exception();
            }
            uint clusterSize = sectorsPerCluster * bytesPerSector;
            uint realSizeLowRank = GetCompressedFileSizeW(file, out uint realSizeHighRank);
            double size = (long)realSizeHighRank << 32 | realSizeLowRank;
            return (size + clusterSize - 1) / clusterSize * clusterSize;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        private static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
            out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
            out uint lpTotalNumberOfClusters);
    }
}