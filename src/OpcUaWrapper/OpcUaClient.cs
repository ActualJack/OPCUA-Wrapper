using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Client;

namespace OpcUaWrapper
{
    /// <summary>
    /// A wrapper class for OPC UA client operations that simplifies interaction with OPC UA servers.
    /// This class encapsulates the complexity of the OPC UA SDK while providing a clean, intuitive API.
    /// </summary>
    public class OpcUaClient : IDisposable
    {
        #region Private Fields

        private ApplicationConfiguration _configuration;
        private Session _session;
        private SessionReconnectHandler _reconnectHandler;
        private readonly string _endpointUrl;
        private readonly string _applicationName;
        private readonly bool _autoAcceptUntrustedCertificates;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the OpcUaClient class.
        /// </summary>
        /// <param name="endpointUrl">The URL of the OPC UA server endpoint.</param>
        /// <param name="applicationName">The name of the client application.</param>
        /// <param name="autoAcceptUntrustedCertificates">Whether to automatically accept untrusted certificates.</param>
        public OpcUaClient(string endpointUrl, string applicationName = "OPC UA Client", bool autoAcceptUntrustedCertificates = true)
        {
            _endpointUrl = endpointUrl ?? throw new ArgumentNullException(nameof(endpointUrl));
            if (string.IsNullOrEmpty(endpointUrl))
            {
                throw new ArgumentException("Endpoint URL cannot be empty", nameof(endpointUrl));
            }
            
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _autoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when the client connects to the server.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Event raised when the client disconnects from the server.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Event raised when the client reconnects to the server after a connection loss.
        /// </summary>
        public event EventHandler Reconnected;

        /// <summary>
        /// Event raised when a keep alive operation fails.
        /// </summary>
        public event EventHandler<KeepAliveEventArgs> KeepAliveFailed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the client is connected to the server.
        /// </summary>
        public bool IsConnected => _session != null && _session.Connected;

        /// <summary>
        /// Gets the current session with the OPC UA server.
        /// </summary>
        public Session Session => _session;

        #endregion

        #region Public Methods

        /// <summary>
        /// Connects to the OPC UA server.
        /// </summary>
        /// <param name="userIdentity">The user identity to use for authentication. If null, anonymous authentication is used.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous connect operation.</returns>
        public async Task ConnectAsync(IUserIdentity userIdentity = null, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                return;
            }

            // Create and configure the application configuration
            _configuration = await CreateApplicationConfigurationAsync().ConfigureAwait(false);

            // Configure certificate validation
            ConfigureCertificateValidation();

            // Discover endpoints
            var selectedEndpoint = await DiscoverEndpointsAsync(cancellationToken).ConfigureAwait(false);
            if (selectedEndpoint == null)
            {
                throw new InvalidOperationException("No suitable endpoints found.");
            }

            // Create and connect the session
            _session = await Session.Create(
                _configuration,
                new ConfiguredEndpoint(null, selectedEndpoint),
                false,
                _applicationName,
                60000, // Session timeout
                userIdentity ?? new UserIdentity(),
                null).ConfigureAwait(false);

            // Set up the keep alive functionality
            _session.KeepAlive += Session_KeepAlive;

            // Raise connected event
            Connected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disconnects from the OPC UA server.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;

                _session.Close();
                _session.Dispose();
                _session = null;

                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from server: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads a value from the OPC UA server.
        /// </summary>
        /// <param name="nodeId">The node ID to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The read value and status code.</returns>
        public async Task<(object Value, StatusCode StatusCode)> ReadValueAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var node = new NodeId(nodeId);
            var valueToRead = new ReadValueId
            {
                NodeId = node,
                AttributeId = Attributes.Value
            };

            var request = new ReadRequest
            {
                NodesToRead = new[] { valueToRead },
                TimestampsToReturn = TimestampsToReturn.Both
            };

            var response = await _session.ReadAsync(request, cancellationToken).ConfigureAwait(false);

            var dataValue = response.Results[0];
            return (dataValue.Value, dataValue.StatusCode);
        }

        /// <summary>
        /// Writes a value to the OPC UA server.
        /// </summary>
        /// <param name="nodeId">The node ID to write to.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status code of the write operation.</returns>
        public async Task<StatusCode> WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var node = new NodeId(nodeId);
            var valueToWrite = new WriteValue
            {
                NodeId = node,
                AttributeId = Attributes.Value,
                Value = new DataValue
                {
                    Value = value
                }
            };

            var request = new WriteRequest
            {
                NodesToWrite = new[] { valueToWrite }
            };

            var response = await _session.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            return response.Results[0];
        }

        /// <summary>
        /// Browses the server for nodes.
        /// </summary>
        /// <param name="nodeId">The node ID to browse from. If null, the server's objects folder is used.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A collection of references.</returns>
        public async Task<IEnumerable<ReferenceDescription>> BrowseAsync(string nodeId = null, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var node = nodeId != null ? new NodeId(nodeId) : ObjectIds.ObjectsFolder;

            var request = new BrowseRequest
            {
                NodesToBrowse = new[]
                {
                    new BrowseDescription
                    {
                        NodeId = node,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                        IncludeSubtypes = true,
                        NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                        ResultMask = (uint)BrowseResultMask.All
                    }
                },
                RequestedMaxReferencesPerNode = 1000
            };

            var response = await _session.BrowseAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Results[0].StatusCode.IsGood())
            {
                return response.Results[0].References;
            }

            return Enumerable.Empty<ReferenceDescription>();
        }

        /// <summary>
        /// Creates a subscription for a list of monitored items.
        /// </summary>
        /// <param name="nodeIds">The node IDs to monitor.</param>
        /// <param name="samplingInterval">The sampling interval in milliseconds.</param>
        /// <param name="dataChangeHandler">The handler for data change notifications.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The subscription ID.</returns>
        public async Task<uint> CreateSubscriptionAsync(
            IEnumerable<string> nodeIds,
            int samplingInterval,
            MonitoredItemNotificationEventHandler dataChangeHandler,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = samplingInterval,
                PublishingEnabled = true,
                Priority = 100,
                KeepAliveCount = 10,
                LifetimeCount = 1000,
                MaxNotificationsPerPublish = 1000
            };

            _session.AddSubscription(subscription);
            await subscription.CreateAsync().ConfigureAwait(false);

            foreach (var nodeId in nodeIds)
            {
                var monitoredItem = new MonitoredItem
                {
                    StartNodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    SamplingInterval = samplingInterval,
                    QueueSize = 1,
                    DiscardOldest = true
                };

                monitoredItem.Notification += dataChangeHandler;
                subscription.AddItem(monitoredItem);
            }

            await subscription.ApplyChangesAsync().ConfigureAwait(false);
            return subscription.Id;
        }

        /// <summary>
        /// Removes a subscription.
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to remove.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RemoveSubscriptionAsync(uint subscriptionId, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server.");
            }

            var subscription = _session.Subscriptions.FirstOrDefault(s => s.Id == subscriptionId);
            if (subscription != null)
            {
                await subscription.DeleteAsync(true).ConfigureAwait(false);
                _session.RemoveSubscription(subscription);
                subscription.Dispose();
            }
        }

        #endregion

        #region Private Methods

        private async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync()
        {
            var appConfig = new ApplicationConfiguration
            {
                ApplicationName = _applicationName,
                ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:{_applicationName}",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier(),
                    TrustedPeerCertificates = new CertificateTrustList(),
                    TrustedIssuerCertificates = new CertificateTrustList(),
                    RejectedCertificateStore = new CertificateStoreIdentifier(),
                    AutoAcceptUntrustedCertificates = _autoAcceptUntrustedCertificates
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 600000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            // Create a temporary certificate if none exists
            var certificate = await CreateApplicationCertificateAsync(appConfig).ConfigureAwait(false);
            appConfig.SecurityConfiguration.ApplicationCertificate.Certificate = certificate;

            // Validate the configuration
            await appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);

            return appConfig;
        }

        private async Task<X509Certificate2> CreateApplicationCertificateAsync(ApplicationConfiguration appConfig)
        {
            // Check if a certificate already exists
            var certificateStore = new CertificateStoreIdentifier
            {
                StoreType = CertificateStoreType.X509Store,
                StorePath = "CurrentUser\\My"
            };

            var certificateIdentifier = new CertificateIdentifier
            {
                StoreType = certificateStore.StoreType,
                StorePath = certificateStore.StorePath,
                SubjectName = appConfig.ApplicationName
            };

            try
            {
                var certificate = await certificateIdentifier.Find().ConfigureAwait(false);
                if (certificate != null)
                {
                    return certificate;
                }
            }
            catch
            {
                // Ignore errors and create a new certificate
            }

            // Create a new certificate
            var subjectName = $"CN={appConfig.ApplicationName}, O={appConfig.ApplicationName}, DC={System.Net.Dns.GetHostName()}";
            var certificate = CertificateFactory.CreateCertificate(
                appConfig.ApplicationUri,
                appConfig.ApplicationName,
                subjectName,
                null);

            // Store the certificate
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();

            return certificate;
        }

        private void ConfigureCertificateValidation()
        {
            // Configure certificate validation here if needed
            if (_autoAcceptUntrustedCertificates)
            {
                CertificateValidator.CertificateValidation += (sender, args) =>
                {
                    args.Accept = true;
                };
            }
        }

        private async Task<EndpointDescription> DiscoverEndpointsAsync(CancellationToken cancellationToken)
        {
            // Discover endpoints
            var discoveryClient = DiscoveryClient.Create(new Uri(_endpointUrl));
            var endpoints = await discoveryClient.GetEndpointsAsync(null, cancellationToken).ConfigureAwait(false);

            // Select the endpoint with the highest security level
            return endpoints
                .OrderByDescending(e => e.SecurityLevel)
                .FirstOrDefault();
        }

        private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                KeepAliveFailed?.Invoke(this, e);

                if (_reconnectHandler == null)
                {
                    _reconnectHandler = new SessionReconnectHandler();
                    _reconnectHandler.BeginReconnect(_session, ReconnectPeriod, OnReconnected);
                }
            }
        }

        private void OnReconnected(object sender, EventArgs e)
        {
            // Clean up the reconnect handler
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;

            // Raise reconnected event
            Reconnected?.Invoke(this, EventArgs.Empty);
        }

        private const int ReconnectPeriod = 10000; // 10 seconds

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the resources used by the OpcUaClient.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resources used by the OpcUaClient.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Disconnect();
                _configuration = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
