using LoipvRemote.App.Composition;
using LoipvRemote.Connection;
using LoipvRemote.Credential;
using LoipvRemote.Credential.Repositories;
using LoipvRemote.Messages;
using LoipvRemote.Tools;
using LoipvRemote.UI;
using LoipvRemote.Domain.Events;
using LoipvRemote.Connectors.Abstractions;
using LoipvRemote.UseCases.Sessions;
using LoipvRemote.UseCases.Credentials;
using System;
using System.Security;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;

namespace LoipvRemote.App
{
    [SupportedOSPlatform("windows")]
    public static class Runtime
    {
        private static readonly object ServiceProviderLock = new();
        private static IServiceProvider? _serviceProvider;
        private static ConnectionSessionOrchestrator? _observedSessionOrchestrator;

        public static WindowList WindowList
        {
            get => State.WindowList!;
            set => State.WindowList = value;
        }

        public static MessageCollector MessageCollector => GetRequiredService<MessageCollector>();

        public static NotificationAreaIcon NotificationAreaIcon
        {
            get => State.NotificationAreaIcon!;
            set => State.NotificationAreaIcon = value;
        }

        public static ExternalToolsService ExternalToolsService => GetRequiredService<ExternalToolsService>();

        public static IStringSecretStore UserSecretStore => GetRequiredService<IStringSecretStore>();

        public static ICredentialRepositoryList CredentialProviderCatalog =>
            GetRequiredService<ICredentialRepositoryList>();

        public static ConnectionInitiator ConnectionInitiator => GetRequiredService<ConnectionInitiator>();

        public static ConnectionsService ConnectionsService => GetRequiredService<ConnectionsService>();

        internal static void Initialize(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            lock (ServiceProviderLock)
            {
                if (_serviceProvider is not null)
                    throw new InvalidOperationException("Runtime has already been initialized.");

                _serviceProvider = serviceProvider;
                _observedSessionOrchestrator = GetRequiredService<ConnectionSessionOrchestrator>();
                _observedSessionOrchestrator.StateChanged += OnConnectionSessionStateChanged;
            }
        }

        internal static void Uninitialize()
        {
            lock (ServiceProviderLock)
            {
                if (_observedSessionOrchestrator is not null)
                    _observedSessionOrchestrator.StateChanged -= OnConnectionSessionStateChanged;

                _observedSessionOrchestrator = null;
                _serviceProvider = null;
            }
        }

        private static RuntimeState State => GetRequiredService<RuntimeState>();

        private static T GetRequiredService<T>() where T : notnull
        {
            IServiceProvider serviceProvider = Volatile.Read(ref _serviceProvider)
                ?? throw new InvalidOperationException("Runtime has not been initialized from the desktop host.");
            return serviceProvider.GetRequiredService<T>();
        }

        private static void OnConnectionSessionStateChanged(ConnectionSessionStateChanged stateChanged)
        {
            MessageCollector.AddMessage(
                MessageClass.DebugMsg,
                $"Connection session {stateChanged.ConnectionId} transitioned to {stateChanged.State}.");
        }

        public static void LoadConnectionsAsync() =>
            GetRequiredService<ConnectionLoadingService>().LoadConnectionsAsync();

        public static void LoadConnections(bool withDialog = false) =>
            GetRequiredService<ConnectionLoadingService>().LoadConnections(withDialog);
    }
}
