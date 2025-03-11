using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Security.Certificates;

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
        private CertificateValidator _certificateValidator;

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

            try
            {
                // Create and configure the application configuration
                _configuration = await CreateApplicationConfigurationAsync().ConfigureAwait(false);

                // Configure certificate validation to accept all certificates
                ConfigureCertificateValidation();

                // Discover endpoints
                var selectedEndpoint = await DiscoverEndpointsAsync(cancellationToken).ConfigureAwait(false);
                if (selectedEndpoint == null)
                {
                    throw new InvalidOperationException("No suitable endpoints found.");
                }

                // Create a configured endpoint with no security
                ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(_configuration));

                // Create and connect the session using Session.Create for version 1.4.x
                // Use anonymous identity if none provided
                _session = await Task.Run(() => 
                {
                    return Session.Create(
                        _configuration,
                        endpoint,
                        false,
                        _applicationName,
                        60000, // Session timeout
                        userIdentity ?? new UserIdentity(new AnonymousIdentityToken()), // Use anonymous identity by default
                        null).Result;
                }).ConfigureAwait(false);

                // Set up the keep alive functionality
                _session.KeepAlive += Session_KeepAlive;

                // Raise connected event
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to OPC UA server: {ex.Message}");
                throw new InvalidOperationException($"Failed to connect to OPC UA server: {ex.Message}", ex);
            }
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

            try
            {
                // Create a ReadValueId for the node
                ReadValueId readValueId = new ReadValueId
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value
                };

                // Create a collection with the single ReadValueId
                ReadValueIdCollection nodesToRead = new ReadValueIdCollection { readValueId };

                // Execute the read operation
                DataValueCollection results;
                DiagnosticInfoCollection diagnosticInfos;

                // Use Task.Run to make the synchronous call asynchronous
                return await Task.Run(() => 
                {
                    _session.Read(
                        null,                  // requestHeader
                        0,                     // maxAge
                        TimestampsToReturn.Both,
                        nodesToRead,
                        out results,
                        out diagnosticInfos);

                    // Return the value and status code
                    return (results[0].Value, results[0].StatusCode);
                });
            }
            catch (ServiceResultException ex)
            {
                Console.WriteLine($"Error reading value: {ex.Message}");
                return (null, StatusCodes.Bad);
            }
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

            try
            {
                // Create a WriteValue for the node
                WriteValue writeValue = new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };

                // Create a collection with the single WriteValue
                WriteValueCollection nodesToWrite = new WriteValueCollection { writeValue };

                // Execute the write operation
                StatusCodeCollection results;
                DiagnosticInfoCollection diagnosticInfos;

                // Use Task.Run to make the synchronous call asynchronous
                return await Task.Run(() => 
                {
                    _session.Write(
                        null,           // requestHeader
                        nodesToWrite,
                        out results,
                        out diagnosticInfos);

                    // Return the status code
                    return results[0];
                });
            }
            catch (ServiceResultException ex)
            {
                Console.WriteLine($"Error writing value: {ex.Message}");
                return StatusCodes.Bad;
            }
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

            try
            {
                // Create a BrowseDescription for the node
                BrowseDescription nodeToBrowse = new BrowseDescription
                {
                    NodeId = nodeId != null ? new NodeId(nodeId) : ObjectIds.ObjectsFolder,
                    BrowseDirection = BrowseDirection.Forward,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)NodeClass.Variable | (uint)NodeClass.Object,
                    ResultMask = (uint)BrowseResultMask.All
                };

                // Create a collection with the single BrowseDescription
                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection { nodeToBrowse };

                // Execute the browse operation
                BrowseResultCollection results;
                DiagnosticInfoCollection diagnosticInfos;

                // Use Task.Run to make the synchronous call asynchronous
                return await Task.Run(() => 
                {
                    _session.Browse(
                        null,           // requestHeader
                        null,           // view
                        0,              // requestedMaxReferencesPerNode
                        nodesToBrowse,
                        out results,
                        out diagnosticInfos);

                    // Return the references
                    return results[0].References;
                });
            }
            catch (ServiceResultException ex)
            {
                Console.WriteLine($"Error browsing: {ex.Message}");
                return Enumerable.Empty<ReferenceDescription>();
            }
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

            try
            {
                // Create a new subscription
                var subscription = new Subscription(_session.DefaultSubscription)
                {
                    PublishingInterval = samplingInterval,
                    PublishingEnabled = true,
                    Priority = 100,
                    KeepAliveCount = 10,
                    LifetimeCount = 1000,
                    MaxNotificationsPerPublish = 1000
                };

                // Add the subscription to the session
                _session.AddSubscription(subscription);
                
                // Create the subscription on the server
                subscription.Create();

                // Add monitored items to the subscription
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

                // Apply the changes to the subscription
                subscription.ApplyChanges();
                
                return subscription.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating subscription: {ex.Message}");
                throw new InvalidOperationException($"Failed to create subscription: {ex.Message}", ex);
            }
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

            try
            {
                // Find the subscription with the specified ID
                var subscription = _session.Subscriptions.FirstOrDefault(s => s.Id == subscriptionId);
                if (subscription != null)
                {
                    // Delete the subscription from the server
                    subscription.Delete(true);
                    
                    // Remove the subscription from the session
                    _session.RemoveSubscription(subscription);
                    
                    // Dispose the subscription
                    subscription.Dispose();
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing subscription: {ex.Message}");
                throw new InvalidOperationException($"Failed to remove subscription: {ex.Message}", ex);
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
                    AutoAcceptUntrustedCertificates = true // Always accept untrusted certificates for simplicity
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 600000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            // Skip certificate creation for simplicity
            await appConfig.Validate(ApplicationType.Client).ConfigureAwait(false);

            return appConfig;
        }

        private async Task<X509Certificate2> CreateApplicationCertificateAsync(ApplicationConfiguration appConfig)
        {
            // This method is simplified for now - community can enhance security later
            return null;
        }

        private void ConfigureCertificateValidation()
        {
            // Always accept certificates for simplicity
            _certificateValidator = new CertificateValidator();
            _certificateValidator.CertificateValidation += (sender, args) =>
            {
                args.Accept = true;
            };
        }

        private async Task<EndpointDescription> DiscoverEndpointsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Use CoreClientUtils for endpoint discovery in version 1.4.x
                // Always use no security for simplicity
                EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(_endpointUrl, false, 15000);
                
                // Return the discovered endpoint
                return endpointDescription;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error discovering endpoints: {ex.Message}");
                throw new InvalidOperationException($"Failed to discover endpoints: {ex.Message}", ex);
            }
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
                _certificateValidator = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
