using System.IO.Abstractions;
using System.Linq.Expressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testably.Abstractions.Testing;
using WheelWizard.ApplicationData;
using WheelWizard.Settings;
using WheelWizard.Shared;
using WheelWizard.Shared.Services;
using WheelWizard.Views;
using WheelWizard.Views.Navigation;
using WheelWizard.Views.Pages;
using WheelWizard.Views.Pages.Settings;
using WheelWizard.Views.Patterns;

namespace WheelWizard.UI.Test;

public class ApplicationCompositionTests
{
    [AvaloniaFact]
    public async Task MainWindowAndPages_ConstructWithoutStaticServiceInitialization_AndRefreshSafely()
    {
        var fileSystem = new MockFileSystem();
        var directory = Path.Combine(Path.GetTempPath(), "wheelwizard-ui-composition");
        fileSystem.Directory.CreateDirectory(directory);
        var location = Substitute.For<IApplicationDataLocation>();
        location.DirectoryPath.Returns(directory);
        var registrations = new ServiceCollection();
        registrations.AddWheelWizardServices(location);
        registrations.AddSingleton<IFileSystem>(fileSystem);
        registrations.AddTransient(typeof(IApiCaller<>), typeof(OfflineApiCaller<>));
        using var services = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        services.GetRequiredService<ISettingsStartupInitializer>().Initialize();
        // The memory filesystem rejects empty Exists paths; supply explicit optional Dolphin paths.
        var settings = services.GetRequiredService<ISettingsManager>();
        settings.NAND_ROOT_PATH.Set(directory, skipSave: true);
        settings.LOAD_PATH.Set(directory, skipSave: true);
        services.GetRequiredService<MiiControlThemes>().Install(Application.Current!.Resources);
        services.GetRequiredService<WindowAppearance>().Install(Application.Current.Resources);
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var windows = services.GetRequiredService<IMainWindowService>();
        try
        {
            windows.Show(desktop);
            var original = Assert.IsType<Layout>(desktop.MainWindow);
            original.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var navigation = services.GetRequiredService<INavigationService>();
            Assert.IsType<HomePage>(navigation.CurrentPage);

            Type[] pages =
            [
                typeof(FriendsPage),
                typeof(RoomsPage),
                typeof(ModsPage),
                typeof(MiiListPage),
                typeof(LeaderboardPage),
                typeof(UserProfilePage),
                typeof(TestingPage),
                typeof(SettingsPage),
            ];
            foreach (var page in pages)
            {
                navigation.NavigateTo(page);
                original.UpdateLayout();
                Assert.IsType(page, navigation.CurrentPage);
            }
            navigation.NavigateTo<ThemesPage>();
            var themesPage = Assert.IsType<ThemesPage>(navigation.CurrentPage);
            settings.LAUNCHER_THEME_COLOR.Set("#FFFFFF", skipSave: true);
            settings.LAUNCHER_TEXT_COLOR.Set("#FFFFFF", skipSave: true);
            settings.LAUNCHER_BODY_TEXT_COLOR.Set("#FFFFFF", skipSave: true);
            settings.LAUNCHER_BACKGROUND_COLOR.Set("#000000", skipSave: true);
            original.UpdateLayout();
            Assert.Same(original, desktop.MainWindow);
            Assert.Same(themesPage, navigation.CurrentPage);
            var layoutGrid = original.FindControl<Grid>("CompleteGrid")!;
            var titleContainer = original.FindControl<Border>("TitleBrandContainer")!;
            var titleRow = original.FindControl<StackPanel>("TitleBrandRow")!;
            var titleWheel = original.FindControl<PathIcon>("TitleWheelIcon")!;
            var titleText = original.FindControl<TextBlock>("TitleLabel")!;
            Assert.Equal(new GridLength(222), layoutGrid.ColumnDefinitions[0].Width);
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(titleWheel.Foreground).Color);
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(titleText.Foreground).Color);
            Assert.Equal(Colors.White, Assert.IsType<Color>(Application.Current!.FindResource("Neutral400")));
            Assert.True(titleWheel.Bounds.Right < titleText.Bounds.Left);
            Assert.InRange(titleText.Bounds.Left - titleWheel.Bounds.Right, 3, 5);
            Assert.InRange(Math.Abs(titleWheel.Bounds.Center.Y - titleText.Bounds.Center.Y), 0, 1);
            Assert.InRange(Math.Abs(titleRow.Bounds.Center.X - (titleContainer.Bounds.Width / 2)), 0, 1);
            navigation.NavigateTo<HomePage>();
            var homePage = Assert.IsType<HomePage>(navigation.CurrentPage);
            original.UpdateLayout();
            var homeWheel = homePage.FindControl<PathIcon>("WheelIcon")!;
            Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(homeWheel.Foreground).Color);
            navigation.NavigateTo<ModsPage>();
            var modsPage = Assert.IsType<ModsPage>(navigation.CurrentPage);
            original.UpdateLayout();
            var browseButton = modsPage.FindControl<WheelWizard.Views.Components.Button>("BrowseModsButton")!;
            var importButton = modsPage
                .GetVisualDescendants()
                .OfType<WheelWizard.Views.Components.Button>()
                .First(button => button.IconData == Application.Current!.FindResource("FileImport"));
            Assert.Equal(importButton.Background, browseButton.Background);
            Assert.Equal(importButton.Foreground, browseButton.Foreground);
            var room = new WheelWizard.Models.RRInfo.RrRoom
            {
                Id = "room-test",
                Created = DateTime.UtcNow,
                Type = "private",
                Suspend = false,
                Players = [],
            };
            navigation.NavigateTo<RoomDetailsPage>(room);
            original.UpdateLayout();
            Assert.Same(room, Assert.IsType<RoomDetailsPage>(navigation.CurrentPage).Room);
            Type[] settingsPages =
            [
                typeof(WhWzSettings),
                typeof(VideoSettings),
                typeof(OtherSettings),
                typeof(RecompSettings),
                typeof(AppInfo),
            ];
            foreach (var page in settingsPages)
            {
                navigation.NavigateTo<SettingsPage>(page);
                original.UpdateLayout();
                Assert.IsType<SettingsPage>(navigation.CurrentPage);
            }
            windows.Refresh();
            Assert.NotSame(original, desktop.MainWindow);
            Assert.False(original.IsVisible);
            Assert.True(desktop.MainWindow!.IsVisible);
            Assert.Equal(Avalonia.Controls.ShutdownMode.OnMainWindowClose, desktop.ShutdownMode);
        }
        finally
        {
            desktop.MainWindow?.Close();
            desktop.MainWindow = null;
        }
    }

    public sealed class OfflineApiCaller<TApi> : IApiCaller<TApi>
        where TApi : class
    {
        public Task<OperationResult<TResult>> CallApiAsync<TResult>(Expression<Func<TApi, Task<TResult>>> apiCall) =>
            Task.FromResult((OperationResult<TResult>)new OperationError { Message = "Offline UI test" });
    }
}
