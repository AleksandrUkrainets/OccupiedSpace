using Microsoft.Extensions.DependencyInjection;
using OccupiedSpace.Infrastructure;
using OccupiedSpace.Program.Services;
using System.Windows;

namespace OccupiedSpace.UI
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            ServiceCollection services = new();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddTransient<IFileSystemItemService, FileSystemItemService>();
            services.AddSingleton<MainWindow>();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow.Show();
        }
    }
}