using LoipvRemote.Domain.Connections;
using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Protocols.Putty;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Windows.Graphics;
using Windows.System;

namespace LoipvRemote.WinUI;

/// <summary>A two-pane local/remote file manager for SFTP transfers.</summary>
internal sealed class SftpBrowserDialog(Window owner, SshFileTransferSessionFactory sessionFactory)
{
    private readonly Window _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    private readonly SshFileTransferSessionFactory _sessionFactory =
        sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    public async Task ShowAsync(ConnectionDefinition connection, string? initialRemotePath)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using ISshFileTransferSession session = _sessionFactory.Create(connection);
        using var cancellation = new CancellationTokenSource();
        string localCurrentPath = GetInitialLocalPath();
        bool remoteReady = false;

        var localPath = CreatePathBox("Local path");
        var remotePath = CreatePathBox("Remote path");
        var localFiles = CreateFileList("SftpLocalFiles");
        var remoteFiles = CreateFileList("SftpRemoteFiles");
        var localUpButton = CreateToolbarButton("Up", Symbol.Up);
        var localRefreshButton = CreateToolbarButton("Refresh", Symbol.Refresh);
        var localNewFolderButton = CreateToolbarButton("New folder", Symbol.NewFolder);
        var remoteUpButton = CreateToolbarButton("Up", Symbol.Up, isEnabled: false);
        var remoteRefreshButton = CreateToolbarButton("Refresh", Symbol.Refresh, isEnabled: false);
        var remoteNewFolderButton = CreateToolbarButton("New folder", Symbol.NewFolder, isEnabled: false);
        var progress = new ProgressRing { Width = 18, Height = 18, IsActive = false };
        var status = new TextBlock
        {
            Text = "Connecting SFTP...",
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72
        };
        var closeContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        closeContent.Children.Add(new FontIcon { Glyph = "\uE711", FontSize = 14 });
        closeContent.Children.Add(new TextBlock { Text = "Close", VerticalAlignment = VerticalAlignment.Center });
        var closeButton = new Button
        {
            Content = closeContent,
            Height = 32,
            Padding = new Thickness(10, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(closeButton, "Close SFTP");
        ToolTipService.SetToolTip(closeButton, "Close");

        Grid localPane = CreateFilePane(
            "Local",
            "This computer",
            localPath,
            localUpButton,
            localRefreshButton,
            localNewFolderButton,
            localFiles);
        Grid remotePane = CreateFilePane(
            "Remote",
            connection.Name,
            remotePath,
            remoteUpButton,
            remoteRefreshButton,
            remoteNewFolderButton,
            remoteFiles);

        var panes = new Grid { ColumnSpacing = 16 };
        panes.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        panes.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        panes.Children.Add(localPane);
        Grid.SetColumn(remotePane, 1);
        panes.Children.Add(remotePane);
        Grid alertOverlay = CreateAlertOverlay();

        var activity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 660
        };
        activity.Children.Add(progress);
        activity.Children.Add(status);

        var header = new Grid { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = $"SFTP - {connection.Name} | {connection.Host}:{connection.Port}",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(activity, 1);
        header.Children.Add(activity);
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        XamlRoot? xamlRoot = (_owner.Content as FrameworkElement)?.XamlRoot;
        SftpDialogSize dialogSize = SftpDialogSizing.Fit(
            xamlRoot?.Size.Width ?? double.NaN,
            xamlRoot?.Size.Height ?? double.NaN);
        double rasterizationScale = xamlRoot?.RasterizationScale ?? 1d;
        var content = new Grid
        {
            Margin = new Thickness(24),
            RowSpacing = 14
        };
        content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(header);
        Grid.SetRow(panes, 1);
        content.Children.Add(panes);
        Grid.SetRowSpan(alertOverlay, 2);
        Canvas.SetZIndex(alertOverlay, 10);
        content.Children.Add(alertOverlay);

        var window = new Window
        {
            Title = $"SFTP - {connection.Name} | {connection.Host}:{connection.Port}",
            Content = content
        };
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "LoipvRemote.ico"));
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(true, false);
        closeButton.Click += (_, _) => window.Close();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void UpdateButtons(bool busy)
        {
            progress.IsActive = busy;
            localPath.IsEnabled = !busy;
            remotePath.IsEnabled = !busy && remoteReady;
            localFiles.IsEnabled = !busy;
            remoteFiles.IsEnabled = !busy && remoteReady;
            localUpButton.IsEnabled = !busy && Directory.GetParent(localCurrentPath) is not null;
            localRefreshButton.IsEnabled = !busy;
            localNewFolderButton.IsEnabled = !busy;
            remoteUpButton.IsEnabled = !busy && remoteReady && session.CurrentRemotePath != "/";
            remoteRefreshButton.IsEnabled = !busy && remoteReady;
            remoteNewFolderButton.IsEnabled = !busy && remoteReady;
        }

        void SetBusy(bool busy, string message)
        {
            status.Text = message;
            UpdateButtons(busy);
        }

        void RefreshLocal(string message)
        {
            try
            {
                LocalFileTransferEntry[] entries = ListLocalEntries(localCurrentPath);
                localPath.Text = localCurrentPath;
                localFiles.ItemsSource = entries;
                SetBusy(false, message.Length > 0 ? message : $"{entries.Length} local item(s)");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Local error: {exception.Message}");
            }
        }

        void NavigateLocal(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                if (!Directory.Exists(fullPath))
                    throw new DirectoryNotFoundException($"Local folder not found: {fullPath}");
                localCurrentPath = fullPath;
                RefreshLocal(string.Empty);
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Local error: {exception.Message}");
            }
        }

        void ShowRemoteEntries(IReadOnlyList<SshFileTransferEntry> entries, string message)
        {
            remoteReady = true;
            remotePath.Text = session.CurrentRemotePath;
            remoteFiles.ItemsSource = entries;
            SetBusy(false, message);
        }

        async Task RunRemoteAsync(
            Func<CancellationToken, Task<IReadOnlyList<SshFileTransferEntry>>> operation,
            string busyMessage)
        {
            try
            {
                SetBusy(true, busyMessage);
                IReadOnlyList<SshFileTransferEntry> entries = await operation(cancellation.Token);
                ShowRemoteEntries(entries, $"{entries.Count} remote item(s) in {session.CurrentRemotePath}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                status.Text = "Operation cancelled.";
            }
            catch (Exception exception)
            {
                SetBusy(false, $"SFTP error: {exception.Message}");
            }
        }

        async Task NavigateRemoteAsync(string path) =>
            await RunRemoteAsync(token => session.ChangeDirectoryAsync(path, token), $"Opening {path}...");

        async Task UploadAsync(LocalFileTransferEntry selected)
        {
            try
            {
                selected.TransferStatus.Begin(FileTransferDirection.Upload);
                status.Text = $"Uploading {selected.Name}...";
                var transferProgress = new Progress<FileTransferProgress>(selected.TransferStatus.Report);
                string destinationDirectory = session.CurrentRemotePath;
                string remoteName = selected.Name;
                string remoteTarget = SshRemotePath.Combine(destinationDirectory, remoteName);
                for (int suffix = 1; await session.FileExistsAsync(remoteTarget, cancellation.Token); suffix++)
                {
                    remoteName = FileTransferName.CreateCollisionName(selected.Name, suffix);
                    remoteTarget = SshRemotePath.Combine(destinationDirectory, remoteName);
                }

                await session.UploadFileAsync(
                    selected.FullPath,
                    remoteTarget,
                    transferProgress,
                    cancellation.Token);
                selected.TransferStatus.Report(new FileTransferProgress(selected.Length, selected.Length));
                IReadOnlyList<SshFileTransferEntry> entries = await session.RefreshAsync(cancellation.Token);
                ShowRemoteEntries(entries, $"Uploaded {selected.Name} → {remoteTarget}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                selected.TransferStatus.Fail();
                status.Text = $"Upload failed: {exception.Message}";
            }
        }

        async Task DownloadAsync(SshFileTransferEntry selected)
        {
            try
            {
                selected.TransferStatus.Begin(FileTransferDirection.Download);
                status.Text = $"Downloading {selected.Name}...";
                var transferProgress = new Progress<FileTransferProgress>(selected.TransferStatus.Report);
                string localName = selected.Name;
                string localTarget = Path.Combine(localCurrentPath, localName);
                for (int suffix = 1; File.Exists(localTarget) || Directory.Exists(localTarget); suffix++)
                {
                    localName = FileTransferName.CreateCollisionName(selected.Name, suffix);
                    localTarget = Path.Combine(localCurrentPath, localName);
                }

                await session.DownloadFileAsync(
                    selected.FullPath,
                    localTarget,
                    transferProgress,
                    cancellation.Token);
                selected.TransferStatus.Report(new FileTransferProgress(selected.Length, selected.Length));
                RefreshLocal($"Downloaded {selected.FullPath} → {localTarget}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                selected.TransferStatus.Fail();
                status.Text = $"Download failed: {exception.Message}";
            }
        }

        async Task CreateLocalFolderAsync()
        {
            string? name = await PromptForNameAsync(alertOverlay, "New local folder", string.Empty, "Create");
            if (name is null)
                return;

            try
            {
                Directory.CreateDirectory(Path.Combine(localCurrentPath, name));
                RefreshLocal($"Created local folder {name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Create folder failed: {exception.Message}");
            }
        }

        async Task CreateRemoteFolderAsync()
        {
            string? name = await PromptForNameAsync(alertOverlay, "New remote folder", string.Empty, "Create");
            if (name is null)
                return;

            try
            {
                SetBusy(true, $"Creating {name}...");
                await session.CreateDirectoryAsync(
                    SshRemotePath.Combine(session.CurrentRemotePath, name),
                    cancellation.Token);
                IReadOnlyList<SshFileTransferEntry> entries = await session.RefreshAsync(cancellation.Token);
                ShowRemoteEntries(entries, $"Created remote folder {name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Create folder failed: {exception.Message}");
            }
        }

        async Task RenameLocalAsync(LocalFileTransferEntry selected)
        {
            string? name = await PromptForNameAsync(alertOverlay, $"Rename {selected.Name}", selected.Name, "Rename");
            if (name is null || string.Equals(name, selected.Name, StringComparison.Ordinal))
                return;

            try
            {
                string target = Path.Combine(localCurrentPath, name);
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException($"{name} already exists.");
                if (selected.IsDirectory)
                    Directory.Move(selected.FullPath, target);
                else
                    File.Move(selected.FullPath, target);
                RefreshLocal($"Renamed {selected.Name} to {name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Rename failed: {exception.Message}");
            }
        }

        async Task RenameRemoteAsync(SshFileTransferEntry selected)
        {
            string? name = await PromptForNameAsync(alertOverlay, $"Rename {selected.Name}", selected.Name, "Rename");
            if (name is null || string.Equals(name, selected.Name, StringComparison.Ordinal))
                return;

            try
            {
                SetBusy(true, $"Renaming {selected.Name}...");
                await session.RenameAsync(
                    selected.FullPath,
                    SshRemotePath.Combine(session.CurrentRemotePath, name),
                    cancellation.Token);
                IReadOnlyList<SshFileTransferEntry> entries = await session.RefreshAsync(cancellation.Token);
                ShowRemoteEntries(entries, $"Renamed {selected.Name} to {name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Rename failed: {exception.Message}");
            }
        }

        async Task DeleteLocalAsync(LocalFileTransferEntry selected)
        {
            if (!await ConfirmAsync(
                    alertOverlay,
                    $"Delete {selected.Name}?",
                    selected.IsDirectory ? "Only an empty folder can be deleted." : "This file will be permanently deleted."))
                return;

            try
            {
                if (selected.IsDirectory)
                    Directory.Delete(selected.FullPath, recursive: false);
                else
                    File.Delete(selected.FullPath);
                RefreshLocal($"Deleted {selected.Name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Delete failed: {exception.Message}");
            }
        }

        async Task DeleteRemoteAsync(SshFileTransferEntry selected)
        {
            if (!await ConfirmAsync(
                    alertOverlay,
                    $"Delete {selected.Name}?",
                    selected.IsDirectory ? "Only an empty folder can be deleted." : "This file will be permanently deleted."))
                return;

            try
            {
                SetBusy(true, $"Deleting {selected.Name}...");
                if (selected.IsDirectory)
                    await session.DeleteDirectoryAsync(selected.FullPath, cancellation.Token);
                else
                    await session.DeleteFileAsync(selected.FullPath, cancellation.Token);
                IReadOnlyList<SshFileTransferEntry> entries = await session.RefreshAsync(cancellation.Token);
                ShowRemoteEntries(entries, $"Deleted {selected.Name}");
            }
            catch (Exception exception)
            {
                SetBusy(false, $"Delete failed: {exception.Message}");
            }
        }

        MenuFlyoutItem CreateMenuItem(string text, Symbol symbol, RoutedEventHandler handler)
        {
            var item = new MenuFlyoutItem
            {
                Text = text,
                Icon = new SymbolIcon(symbol)
            };
            item.Click += handler;
            return item;
        }

        void ShowLocalContextMenu(object sender, RightTappedRoutedEventArgs eventArgs)
        {
            if (progress.IsActive ||
                GetRightTappedItem(localFiles, eventArgs) is not LocalFileTransferEntry selected ||
                selected.TransferStatus.IsActive)
                return;

            var menu = new MenuFlyout();
            foreach (SftpEntryAction action in SftpContextMenuPolicy.For(SftpPaneSide.Local, selected.IsDirectory))
            {
                if (action == SftpEntryAction.Delete)
                    menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(action switch
                {
                    SftpEntryAction.Open => CreateMenuItem("Open", Symbol.OpenLocal, (_, _) => NavigateLocal(selected.FullPath)),
                    SftpEntryAction.Upload => CreateMenuItem("Upload", Symbol.Upload, async (_, _) => await UploadAsync(selected)),
                    SftpEntryAction.Rename => CreateMenuItem("Rename", Symbol.Edit, async (_, _) => await RenameLocalAsync(selected)),
                    SftpEntryAction.Delete => CreateMenuItem("Delete", Symbol.Delete, async (_, _) => await DeleteLocalAsync(selected)),
                    _ => throw new InvalidOperationException($"Unsupported local SFTP action: {action}")
                });
            }

            ShowContextMenu(menu, localFiles, eventArgs);
        }

        void ShowRemoteContextMenu(object sender, RightTappedRoutedEventArgs eventArgs)
        {
            if (progress.IsActive || !remoteReady ||
                GetRightTappedItem(remoteFiles, eventArgs) is not SshFileTransferEntry selected ||
                selected.TransferStatus.IsActive)
                return;

            var menu = new MenuFlyout();
            foreach (SftpEntryAction action in SftpContextMenuPolicy.For(SftpPaneSide.Remote, selected.IsDirectory))
            {
                if (action == SftpEntryAction.Delete)
                    menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(action switch
                {
                    SftpEntryAction.Open => CreateMenuItem("Open", Symbol.OpenLocal, async (_, _) => await NavigateRemoteAsync(selected.FullPath)),
                    SftpEntryAction.Download => CreateMenuItem("Download", Symbol.Download, async (_, _) => await DownloadAsync(selected)),
                    SftpEntryAction.Rename => CreateMenuItem("Rename", Symbol.Edit, async (_, _) => await RenameRemoteAsync(selected)),
                    SftpEntryAction.Delete => CreateMenuItem("Delete", Symbol.Delete, async (_, _) => await DeleteRemoteAsync(selected)),
                    _ => throw new InvalidOperationException($"Unsupported remote SFTP action: {action}")
                });
            }

            ShowContextMenu(menu, remoteFiles, eventArgs);
        }

        window.Closed += (_, _) =>
        {
            cancellation.Cancel();
            closed.TrySetResult();
        };

        localPath.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key != VirtualKey.Enter || string.IsNullOrWhiteSpace(localPath.Text))
                return;
            eventArgs.Handled = true;
            NavigateLocal(localPath.Text.Trim());
        };
        remotePath.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.Key != VirtualKey.Enter || string.IsNullOrWhiteSpace(remotePath.Text))
                return;
            eventArgs.Handled = true;
            await NavigateRemoteAsync(remotePath.Text.Trim());
        };
        localUpButton.Click += (_, _) =>
        {
            DirectoryInfo? parent = Directory.GetParent(localCurrentPath);
            if (parent is not null)
                NavigateLocal(parent.FullName);
        };
        localRefreshButton.Click += (_, _) => RefreshLocal(string.Empty);
        localNewFolderButton.Click += async (_, _) => await CreateLocalFolderAsync();
        remoteUpButton.Click += async (_, _) => await NavigateRemoteAsync(SshRemotePath.Parent(session.CurrentRemotePath));
        remoteRefreshButton.Click += async (_, _) =>
            await RunRemoteAsync(session.RefreshAsync, "Refreshing remote files...");
        remoteNewFolderButton.Click += async (_, _) => await CreateRemoteFolderAsync();

        localFiles.DoubleTapped += (_, _) =>
        {
            if (localFiles.SelectedItem is LocalFileTransferEntry { IsDirectory: true } directory)
                NavigateLocal(directory.FullPath);
        };
        remoteFiles.DoubleTapped += async (_, _) =>
        {
            if (remoteFiles.SelectedItem is SshFileTransferEntry { IsDirectory: true } directory)
                await NavigateRemoteAsync(directory.FullPath);
        };
        localFiles.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == VirtualKey.Enter &&
                localFiles.SelectedItem is LocalFileTransferEntry { IsDirectory: true } directory)
            {
                eventArgs.Handled = true;
                NavigateLocal(directory.FullPath);
            }
        };
        remoteFiles.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.Key == VirtualKey.Enter &&
                remoteFiles.SelectedItem is SshFileTransferEntry { IsDirectory: true } directory)
            {
                eventArgs.Handled = true;
                await NavigateRemoteAsync(directory.FullPath);
            }
        };
        localFiles.RightTapped += ShowLocalContextMenu;
        remoteFiles.RightTapped += ShowRemoteContextMenu;

        SetNativeOwner(window, _owner);
        KeepAboveEmbeddedSession(window, _owner);
        window.Activate();
        SizeAndCenterWindow(window, _owner, dialogSize, rasterizationScale);
        RefreshLocal(string.Empty);
        await RunRemoteAsync(
            token => session.ConnectAsync(initialRemotePath, token),
            "Connecting SFTP...");
        await closed.Task;
    }

    private static void SetNativeOwner(Window window, Window owner)
    {
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        IntPtr ownerHandle = WindowNative.GetWindowHandle(owner);
        if (windowHandle == IntPtr.Zero || ownerHandle == IntPtr.Zero)
            throw new InvalidOperationException("SFTP window ownership requires valid native window handles.");

        const int GwlHwndParent = -8;
        Marshal.SetLastPInvokeError(0);
        _ = NativeMethods.SetWindowLongPtr(windowHandle, GwlHwndParent, ownerHandle);
        int error = Marshal.GetLastPInvokeError();
        if (error != 0)
            throw new InvalidOperationException($"Could not assign the SFTP window owner (Win32 error {error}).");
    }

    private static void KeepAboveEmbeddedSession(Window window, Window owner)
    {
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        IntPtr ownerHandle = WindowNative.GetWindowHandle(owner);
        var topmostTimer = window.DispatcherQueue.CreateTimer();
        topmostTimer.Interval = TimeSpan.FromMilliseconds(100);
        topmostTimer.IsRepeating = true;

        void ApplyTopmostState()
        {
            if (!IsLoipvRemoteForeground(ownerHandle))
                return;

            const int SwpNoSize = 0x0001;
            const int SwpNoMove = 0x0002;
            const int SwpNoActivate = 0x0010;
            _ = NativeMethods.SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoSize | SwpNoMove | SwpNoActivate);
        }

        void RefreshTopmostState(object sender, WindowActivatedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            window.DispatcherQueue.TryEnqueue(ApplyTopmostState);
        }

        topmostTimer.Tick += (_, _) => ApplyTopmostState();
        window.Activated += RefreshTopmostState;
        owner.Activated += RefreshTopmostState;
        window.Closed += (_, _) =>
        {
            topmostTimer.Stop();
            window.Activated -= RefreshTopmostState;
            owner.Activated -= RefreshTopmostState;
        };

        ApplyTopmostState();
        topmostTimer.Start();
    }

    private static bool IsLoipvRemoteForeground(IntPtr ownerHandle)
    {
        IntPtr foregroundHandle = NativeMethods.GetForegroundWindow();
        if (foregroundHandle == IntPtr.Zero)
            return false;

        _ = NativeMethods.GetWindowThreadProcessId(foregroundHandle, out uint foregroundProcessId);
        bool foregroundUsesAppProcess = foregroundProcessId == (uint)Environment.ProcessId;
        bool foregroundIsInsideOwner = IsWindowOrDescendant(foregroundHandle, ownerHandle);
        return SftpWindowActivationPolicy.ShouldStayTopmost(
            foregroundUsesAppProcess,
            foregroundIsInsideOwner);
    }

    private static bool IsWindowOrDescendant(IntPtr windowHandle, IntPtr ownerHandle)
    {
        const int MaxParentDepth = 64;
        IntPtr current = windowHandle;
        for (int depth = 0; current != IntPtr.Zero && depth < MaxParentDepth; depth++)
        {
            if (current == ownerHandle)
                return true;

            IntPtr parent = NativeMethods.GetParent(current);
            if (parent == current)
                break;
            current = parent;
        }

        return false;
    }

    private static void SizeAndCenterWindow(
        Window window,
        Window owner,
        SftpDialogSize dialogSize,
        double rasterizationScale)
    {
        DisplayArea displayArea = DisplayArea.GetFromWindowId(owner.AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int width = Math.Min(
            workArea.Width,
            Math.Max(1, (int)Math.Ceiling(dialogSize.Width * rasterizationScale)));
        int height = Math.Min(
            workArea.Height,
            Math.Max(1, (int)Math.Ceiling(dialogSize.Height * rasterizationScale)));
        PointInt32 ownerPosition = owner.AppWindow.Position;
        SizeInt32 ownerSize = owner.AppWindow.Size;
        int x = Math.Clamp(
            ownerPosition.X + ((ownerSize.Width - width) / 2),
            workArea.X,
            workArea.X + workArea.Width - width);
        int y = Math.Clamp(
            ownerPosition.Y + ((ownerSize.Height - height) / 2),
            workArea.Y,
            workArea.Y + workArea.Height - height);
        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private static object? GetRightTappedItem(ListView list, RightTappedRoutedEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not DependencyObject source)
            return null;

        for (DependencyObject? current = source; current is not null && current != list;
             current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is not ListViewItem container)
                continue;

            list.SelectedItem = container.Content;
            return container.Content;
        }

        return null;
    }

    private static void ShowContextMenu(
        MenuFlyout menu,
        FrameworkElement target,
        RightTappedRoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        menu.ShowAt(target, new FlyoutShowOptions { Position = eventArgs.GetPosition(target) });
    }

    private static Grid CreateAlertOverlay()
    {
        var overlay = new Grid
        {
            Visibility = Visibility.Collapsed,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(96, 0, 0, 0))
        };
        AutomationProperties.SetAutomationId(overlay, "SftpConfirmationOverlay");
        return overlay;
    }

    private static Border CreateCenteredAlertCard(UIElement content) => new()
    {
        Child = content,
        MinWidth = 360,
        MaxWidth = 520,
        Padding = new Thickness(20),
        CornerRadius = new CornerRadius(8),
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
        BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static void ShowCenteredAlert(Grid overlay, Border card)
    {
        overlay.Children.Clear();
        overlay.Children.Add(card);
        overlay.Visibility = Visibility.Visible;
    }

    private static void HideCenteredAlert(Grid overlay)
    {
        overlay.Visibility = Visibility.Collapsed;
        overlay.Children.Clear();
    }

    private static async Task<string?> PromptForNameAsync(
        Grid alertOverlay,
        string title,
        string initialValue,
        string confirmText)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = new TextBox
        {
            Text = initialValue,
            MinWidth = 300,
            SelectionStart = 0,
            SelectionLength = initialValue.Length
        };
        var error = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Firebrick),
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };
        var confirm = new Button { Content = confirmText };
        var cancel = new Button { Content = "Cancel" };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(input);
        panel.Children.Add(error);
        panel.Children.Add(actions);

        Border card = CreateCenteredAlertCard(panel);

        void Submit()
        {
            string value = input.Text.Trim();
            if (!IsValidEntryName(value))
            {
                error.Text = "Enter a single valid file or folder name.";
                error.Visibility = Visibility.Visible;
                return;
            }

            if (completion.TrySetResult(value))
                HideCenteredAlert(alertOverlay);
        }

        confirm.Click += (_, _) => Submit();
        cancel.Click += (_, _) =>
        {
            if (completion.TrySetResult(null))
                HideCenteredAlert(alertOverlay);
        };
        input.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == VirtualKey.Enter)
            {
                eventArgs.Handled = true;
                Submit();
            }
            else if (eventArgs.Key == VirtualKey.Escape)
            {
                eventArgs.Handled = true;
                if (completion.TrySetResult(null))
                    HideCenteredAlert(alertOverlay);
            }
        };
        ShowCenteredAlert(alertOverlay, card);
        input.Focus(FocusState.Programmatic);
        return await completion.Task;
    }

    private static async Task<bool> ConfirmAsync(Grid alertOverlay, string title, string message)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delete = new Button { Content = "Delete" };
        var cancel = new Button { Content = "Cancel" };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actions.Children.Add(cancel);
        actions.Children.Add(delete);
        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(actions);

        Border card = CreateCenteredAlertCard(panel);
        delete.Click += (_, _) =>
        {
            if (completion.TrySetResult(true))
                HideCenteredAlert(alertOverlay);
        };
        cancel.Click += (_, _) =>
        {
            if (completion.TrySetResult(false))
                HideCenteredAlert(alertOverlay);
        };
        ShowCenteredAlert(alertOverlay, card);
        cancel.Focus(FocusState.Programmatic);
        return await completion.Task;
    }

    private static bool IsValidEntryName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name is not "." and not ".." &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.Contains('/') &&
        !name.Contains('\\');

    private static Button CreateToolbarButton(string accessibleName, Symbol symbol, bool isEnabled = true)
    {
        var button = new Button
        {
            Content = new Viewbox
            {
                Width = 14,
                Height = 14,
                Child = new SymbolIcon(symbol)
            },
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            IsEnabled = isEnabled
        };
        AutomationProperties.SetName(button, accessibleName);
        ToolTipService.SetToolTip(button, accessibleName);
        return button;
    }

    private static TextBox CreatePathBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static ListView CreateFileList(string automationId)
    {
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemTemplate = CreateEntryTemplate(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(list, automationId);
        return list;
    }

    private static DataTemplate CreateEntryTemplate() => (DataTemplate)XamlReader.Load("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Height="30" ColumnSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="20" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="72" />
                    <ColumnDefinition Width="82" />
                    <ColumnDefinition Width="128" />
                </Grid.ColumnDefinitions>
                <FontIcon Glyph="{Binding IconGlyph}" FontSize="14" VerticalAlignment="Center" />
                <TextBlock Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
                <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
                    <FontIcon Glyph="{Binding TransferStatus.Glyph}" FontSize="12" />
                    <TextBlock Text="{Binding TransferStatus.Text}" VerticalAlignment="Center" />
                </StackPanel>
                <TextBlock Grid.Column="3" Text="{Binding SizeText}" HorizontalAlignment="Right" VerticalAlignment="Center" />
                <TextBlock Grid.Column="4" Text="{Binding ModifiedText}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
            </Grid>
        </DataTemplate>
        """);

    private static Grid CreateFilePane(
        string title,
        string subtitle,
        TextBox path,
        Button upButton,
        Button refreshButton,
        Button newFolderButton,
        ListView files)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        heading.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = subtitle,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.65
        });

        var toolbar = new Grid { ColumnSpacing = 6 };
        toolbar.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.Children.Add(path);
        Grid.SetColumn(upButton, 1);
        toolbar.Children.Add(upButton);
        Grid.SetColumn(refreshButton, 2);
        toolbar.Children.Add(refreshButton);
        Grid.SetColumn(newFolderButton, 3);
        toolbar.Children.Add(newFolderButton);

        var columns = new Grid { Padding = new Thickness(8, 4, 8, 4), ColumnSpacing = 8 };
        columns.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new() { Width = new GridLength(72) });
        columns.ColumnDefinitions.Add(new() { Width = new GridLength(82) });
        columns.ColumnDefinitions.Add(new() { Width = new GridLength(128) });
        columns.Children.Add(new TextBlock { Text = "Name", Opacity = 0.7 });
        var transfer = new TextBlock { Text = "Transfer", Opacity = 0.7 };
        Grid.SetColumn(transfer, 1);
        columns.Children.Add(transfer);
        var size = new TextBlock { Text = "Size", Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(size, 2);
        columns.Children.Add(size);
        var modified = new TextBlock { Text = "Modified", Opacity = 0.7 };
        Grid.SetColumn(modified, 3);
        columns.Children.Add(modified);

        var pane = new Grid { RowSpacing = 6 };
        pane.RowDefinitions.Add(new() { Height = GridLength.Auto });
        pane.RowDefinitions.Add(new() { Height = GridLength.Auto });
        pane.RowDefinitions.Add(new() { Height = GridLength.Auto });
        pane.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        pane.Children.Add(heading);
        Grid.SetRow(toolbar, 1);
        pane.Children.Add(toolbar);
        Grid.SetRow(columns, 2);
        pane.Children.Add(columns);
        Grid.SetRow(files, 3);
        pane.Children.Add(files);
        return pane;
    }

    private static string GetInitialLocalPath()
    {
        string downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static LocalFileTransferEntry[] ListLocalEntries(string path)
    {
        var directory = new DirectoryInfo(path);
        IEnumerable<LocalFileTransferEntry> directories = directory.GetDirectories()
            .Select(item => new LocalFileTransferEntry(
                item.Name,
                item.FullName,
                IsDirectory: true,
                Length: 0,
                item.LastWriteTimeUtc));
        IEnumerable<LocalFileTransferEntry> files = directory.GetFiles()
            .Select(item => new LocalFileTransferEntry(
                item.Name,
                item.FullName,
                IsDirectory: false,
                item.Length,
                item.LastWriteTimeUtc));
        return directories.Concat(files)
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal sealed record LocalFileTransferEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Length,
    DateTime LastWriteTimeUtc)
{
    public FileTransferRowStatus TransferStatus { get; } = new();
    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5";
    public string SizeText => IsDirectory ? string.Empty : FileTransferDisplay.FormatSize(Length);
    public string ModifiedText => LastWriteTimeUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
}
