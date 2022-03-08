using Microsoft.WindowsAPICodePack.Dialogs;
using OccupiedSpace.Domain.Models;
using OccupiedSpace.Infrastructure;
using OccupiedSpace.UI.ViewModels.Base;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OccupiedSpace.UI.ViewModels
{
    internal class FileSystemItemViewModel : BaseViewModel
    {
        private readonly IFileSystemItemService _fileSystemItemService;
        public AsyncObservableCollection<FileSystemItemViewModel> TreeItems { get; set; }

        public FileSystemItem FileSystemItem { get; set; } = new FileSystemItem();

        public string ImageName => FileSystemItem.Type == FileSystemItemType.Drive ? "DriveOutline" : (FileSystemItem.Type == FileSystemItemType.File ? "FileOutline" : (IsExpanded ? "FolderOpenOutline" : "FolderOutline"));

        public string DisplayProgressBar { get; set; }

        public ICommand OpenFolderCommand { get; set; }

        public ICommand StopScanCommand { get; set; }

        public ICommand ExpandCommand { get; set; }

        public ICommand RefreshCommand { get; set; }

        private bool _isExpanded;
        private readonly object _locker = new object();
        private double _parentSize;
        private double _parentAllocated;
        private int _parentCountFiles;
        private int _parentFolders;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value)
                {
                    Expand();
                }
                else
                {
                    ClearChildren();
                }
            }
        }

        public FileSystemItemViewModel(IFileSystemItemService fileSystemItemService)
        {
            OpenFolderCommand = new RelayCommand(OpenFolder);
            StopScanCommand = new RelayCommand(StopScan);
            ExpandCommand = new RelayCommand(Expand);
            RefreshCommand = new RelayCommand(Refresh);
            DisplayProgressBar = "Hidden";
            _fileSystemItemService = fileSystemItemService;
        }

        private void OpenFolder()
        {
            FileSystemItem = new FileSystemItem();
            var dlg = new CommonOpenFileDialog
            {
                Title = "Open folder",
                IsFolderPicker = true,
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };
            DisplayProgressBar = "Visible";

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                FileSystemItem.FullPath = dlg.FileName;
                Refresh();
            }
        }

        private async void Refresh()
        {
            DisplayProgressBar = "Visible";
            TreeItems = new AsyncObservableCollection<FileSystemItemViewModel>();
            var itemsWithSize = new AsyncObservableCollection<FileSystemItemViewModel>();
            try
            {
                await Task.Run(() =>
                {
                    _parentSize = 0.0;
                    _parentAllocated = 0.0;
                    _parentCountFiles = 0;
                    _parentFolders = 0;
                    _fileSystemItemService.ToCalculateSize = false;
                    TreeItems.Add(GetEmptyParentCatalog(FileSystemItem.FullPath));

                    var getSize = Parallel.ForEach(TreeItems[0].TreeItems, treeItem =>
                    {
                        var itemWithSize = new FileSystemItemViewModel(_fileSystemItemService) { };
                        itemWithSize = GetFileSystemItemWithSize(treeItem);
                        lock (_locker)
                        {
                            if (treeItem.FileSystemItem.Type == FileSystemItemType.Folder)
                            {
                                _parentFolders++;
                            }

                            _parentSize += itemWithSize.FileSystemItem.Size;
                            _parentAllocated += itemWithSize.FileSystemItem.AdditionalProperty.Allocated;
                            _parentCountFiles += itemWithSize.FileSystemItem.AdditionalProperty.CountFiles;
                            _parentFolders += itemWithSize.FileSystemItem.AdditionalProperty.CountFolders;
                            itemsWithSize.Add(itemWithSize);
                            TreeItems[0].TreeItems = itemsWithSize;
                        }
                    });

                    if (getSize.IsCompleted)
                    {
                        OnShowParentCatalogWithAllocated(SetAllocatedToItems(itemsWithSize));
                    }
                });
            }
            catch (Exception e)
            {
                Trace.WriteLine("Refresh method called exeption: " + e);
            }
            DisplayProgressBar = "Hidden";
        }

        private AsyncObservableCollection<FileSystemItemViewModel> SetAllocatedToItems(AsyncObservableCollection<FileSystemItemViewModel> itemsWithSize)
        {
            double percentFirstFolder = _fileSystemItemService.PercentFirstFolder;
            var itemsWithAllocated = new AsyncObservableCollection<FileSystemItemViewModel>();

            foreach (var itemWithSize in itemsWithSize)
            {
                itemWithSize.FileSystemItem.AdditionalProperty.PercentAllocated = Math.Round((double)(percentFirstFolder
                            * itemWithSize.FileSystemItem.AdditionalProperty.Allocated / _parentAllocated), 4);
                itemsWithAllocated.Add(itemWithSize);
            }

            var sortedItemsWithAllocated = new AsyncObservableCollection<FileSystemItemViewModel>();

            foreach (var sortedItem in itemsWithAllocated.OrderByDescending(x => x.FileSystemItem.AdditionalProperty.PercentAllocated))
            {
                sortedItemsWithAllocated.Add(sortedItem);
            }

            return sortedItemsWithAllocated;
        }

        private void OnShowParentCatalogWithAllocated(AsyncObservableCollection<FileSystemItemViewModel> sortedItemsWithAllocated)
        {
            var newParenFolder = new FileSystemItemViewModel(_fileSystemItemService)
            {
                _isExpanded = true,
                TreeItems = sortedItemsWithAllocated,
                FileSystemItem = new FileSystemItem
                {
                    FullPath = TreeItems[0].FileSystemItem.FullPath,
                    Name = TreeItems[0].FileSystemItem.FullPath,
                    Type = FileSystemItemType.Folder,
                    Size = _parentSize,
                    AdditionalProperty = new AdditionalProperty
                    {
                        Allocated = _parentAllocated,
                        CountFiles = _parentCountFiles,
                        CountFolders = _parentFolders,
                        PercentAllocated = _fileSystemItemService.PercentFirstFolder,
                    }
                }
            };
            TreeItems = new AsyncObservableCollection<FileSystemItemViewModel>() { newParenFolder };
        }

        #region empty parent catalog

        private FileSystemItemViewModel GetEmptyParentCatalog(string fullPath)
        {
            var firstFolder = new FileSystemItemViewModel(_fileSystemItemService)
            {
                _isExpanded = true,
                FileSystemItem = new FileSystemItem
                {
                    FullPath = FileSystemItem.FullPath,
                    Name = FileSystemItem.FullPath,
                    Type = FileSystemItemType.Folder,
                    AdditionalProperty = new AdditionalProperty
                    {
                        Allocated = default,
                        CountFiles = default,
                        CountFolders = default,
                        PercentAllocated = default
                    }
                },

                TreeItems = GetTreeItems(fullPath)
            };

            return firstFolder;
        }

        private AsyncObservableCollection<FileSystemItemViewModel> GetTreeItems(string path)
        {
            var treeItems = new AsyncObservableCollection<FileSystemItemViewModel>();
            var childrens = _fileSystemItemService.GetDirectoryContent(path).OrderByDescending(x => x.Size);

            var allocated = FileSystemItem.AdditionalProperty == null ? 1.0 : FileSystemItem.AdditionalProperty.Allocated;

            foreach (var children in childrens)
            {
                treeItems.Add(new FileSystemItemViewModel(_fileSystemItemService)
                {
                    FileSystemItem = new FileSystemItem
                    {
                        FullPath = children.FullPath,
                        Name = children.FullPath,
                        Type = children.Type,
                        Size = children.Size,
                        Modified = children.Modified,
                        AdditionalProperty = new AdditionalProperty
                        {
                            Allocated = children.AdditionalProperty.Allocated,
                            CountFiles = children.AdditionalProperty.CountFiles,
                            CountFolders = children.AdditionalProperty.CountFolders,
                            PercentAllocated = Math.Round((double)(_fileSystemItemService.PercentFirstFolder
                                    * children.AdditionalProperty.Allocated / allocated), 4)
                        }
                    }
                });
            };

            return treeItems;
        }

        #endregion empty parent catalog

        private FileSystemItemViewModel GetFileSystemItemWithSize(FileSystemItemViewModel item)
        {
            _fileSystemItemService.ToCalculateSize = true;
            if (item.FileSystemItem.Type == FileSystemItemType.File)
            {
                var file = _fileSystemItemService.GetFile(item.FileSystemItem.FullPath);
                item.FileSystemItem.AdditionalProperty.Allocated = file.AdditionalProperty.Allocated;
            }
            else
            {
                item.TreeItems = GetTreeItems(item.FileSystemItem.FullPath);
                GetSizeAndCounFolder(item);
            }

            return item;
        }

        private void GetSizeAndCounFolder(FileSystemItemViewModel directory)
        {
            if (_fileSystemItemService.ToCalculateSize)
            {
                foreach (var children in directory.TreeItems)
                {
                    if (children.FileSystemItem.Type == FileSystemItemType.Folder)
                    {
                        directory.FileSystemItem.AdditionalProperty.CountFolders++;
                    }
                    directory.FileSystemItem.Size += children.FileSystemItem.Size;
                    directory.FileSystemItem.AdditionalProperty.Allocated += children.FileSystemItem.AdditionalProperty.Allocated;
                    directory.FileSystemItem.AdditionalProperty.CountFiles += children.FileSystemItem.AdditionalProperty.CountFiles;
                    directory.FileSystemItem.AdditionalProperty.CountFolders += children.FileSystemItem.AdditionalProperty.CountFolders;
                };
            }
        }

        #region Commands

        private void StopScan()
        {
            _fileSystemItemService.ToCalculateSize = false;
            DisplayProgressBar = "Hidden";
        }

        private void Expand()
        {
            if (FileSystemItem.Type == FileSystemItemType.File)
            {
                return;
            }

            _isExpanded = true;
            _fileSystemItemService.ToCalculateSize = true;

            TreeItems = GetTreeItems(FileSystemItem.FullPath);
            foreach (var treeItem in TreeItems)
            {
                if (treeItem.FileSystemItem.Type == FileSystemItemType.Folder)
                {
                    treeItem.TreeItems = new AsyncObservableCollection<FileSystemItemViewModel>();
                    treeItem.TreeItems.Add(null);
                }
            }

            DisplayProgressBar = "Visible";
        }

        private void ClearChildren()
        {
            _isExpanded = false;
        }

        #endregion Commands
    }
}