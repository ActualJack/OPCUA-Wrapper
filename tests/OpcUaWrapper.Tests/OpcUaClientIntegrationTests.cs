using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Opc.Ua;
using Opc.Ua.Client;

namespace OpcUaWrapper.Tests
{
    /// <summary>
    /// Integration tests for OpcUaClient that require a real OPC UA server.
    /// These tests are marked with [Trait("Category", "Integration")] and can be skipped
    /// if no server is available by using the --filter Category!=Integration option with dotnet test.
    /// </summary>
    public class OpcUaClientIntegrationTests : IDisposable
    {
        private readonly OpcUaClient _client;
        private readonly string _serverUrl;
        private readonly string _testNodeId;
        private readonly string _testWritableNodeId;
        private readonly bool _skipTests;
        
        public OpcUaClientIntegrationTests()
        {
            // Get settings from TestConfig
            _serverUrl = TestConfig.Settings.ServerUrl;
            _testNodeId = TestConfig.Settings.TestNodeId;
            _testWritableNodeId = TestConfig.Settings.TestWritableNodeId;
            _skipTests = TestConfig.Settings.SkipIntegrationTests;
            
            _client = new OpcUaClient(_serverUrl);
        }
        
        public void Dispose()
        {
            _client?.Dispose();
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task ConnectAsync_WithValidServer_ShouldConnect()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Act
            await _client.ConnectAsync();
            
            // Assert
            Assert.True(_client.IsConnected);
            Assert.NotNull(_client.Session);
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task ConnectAsync_WithInvalidServer_ShouldThrow()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            var invalidClient = new OpcUaClient("opc.tcp://invalid-server:4840");
            
            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => invalidClient.ConnectAsync());
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task ReadValueAsync_WithValidNodeId_ShouldReturnValue()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            
            // Act
            var (value, statusCode) = await _client.ReadValueAsync(_testNodeId);
            
            // Assert
            Assert.NotNull(value);
            Assert.True(StatusCode.IsGood(statusCode));
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task ReadValueAsync_WithInvalidNodeId_ShouldReturnBadStatusCode()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            
            // Act
            var (value, statusCode) = await _client.ReadValueAsync("ns=999;s=NonExistentNode");
            
            // Assert
            Assert.False(StatusCode.IsGood(statusCode));
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task WriteValueAsync_WithValidNodeId_ShouldSucceed()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            double testValue = 42.0;
            
            // Act
            var statusCode = await _client.WriteValueAsync(_testWritableNodeId, testValue);
            
            // Assert
            Assert.True(StatusCode.IsGood(statusCode));
            
            // Verify the write
            var (readValue, readStatus) = await _client.ReadValueAsync(_testWritableNodeId);
            Assert.True(StatusCode.IsGood(readStatus));
            Assert.Equal(testValue, Convert.ToDouble(readValue));
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task BrowseAsync_ShouldReturnNodes()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            
            // Act
            var nodes = await _client.BrowseAsync();
            
            // Assert
            Assert.NotNull(nodes);
            Assert.NotEmpty(nodes);
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task CreateSubscriptionAsync_ShouldCreateSubscription()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            var nodeIds = new List<string> { _testNodeId };
            var notificationReceived = new ManualResetEventSlim(false);
            object receivedValue = null;
            
            // Act
            uint subscriptionId = await _client.CreateSubscriptionAsync(
                nodeIds,
                100, // 100ms sampling interval
                (sender, e) =>
                {
                    if (e.NotificationValue is MonitoredItemNotification notification)
                    {
                        receivedValue = notification.Value.Value;
                        notificationReceived.Set();
                    }
                });
            
            // Wait for notification (timeout after 5 seconds)
            bool received = notificationReceived.Wait(5000);
            
            // Clean up
            await _client.RemoveSubscriptionAsync(subscriptionId);
            
            // Assert
            Assert.True(received, "Did not receive notification within timeout period");
            Assert.NotNull(receivedValue);
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task Disconnect_ShouldDisconnectFromServer()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Arrange
            await _client.ConnectAsync();
            Assert.True(_client.IsConnected);
            
            // Act
            _client.Disconnect();
            
            // Assert
            Assert.False(_client.IsConnected);
        }
        
        [SkippableFact]
        [Trait("Category", "Integration")]
        public async Task FullLifecycle_ShouldWorkCorrectly()
        {
            Skip.If(_skipTests, "Integration tests are disabled");
            
            // Connect
            await _client.ConnectAsync();
            Assert.True(_client.IsConnected);
            
            // Browse
            var nodes = await _client.BrowseAsync();
            Assert.NotEmpty(nodes);
            
            // Read
            var (readValue, readStatus) = await _client.ReadValueAsync(_testNodeId);
            Assert.True(StatusCode.IsGood(readStatus));
            
            // Write
            double testValue = 99.9;
            var writeStatus = await _client.WriteValueAsync(_testWritableNodeId, testValue);
            Assert.True(StatusCode.IsGood(writeStatus));
            
            // Verify write
            var (verifyValue, verifyStatus) = await _client.ReadValueAsync(_testWritableNodeId);
            Assert.True(StatusCode.IsGood(verifyStatus));
            Assert.Equal(testValue, Convert.ToDouble(verifyValue));
            
            // Create subscription
            var nodeIds = new List<string> { _testNodeId };
            uint subscriptionId = await _client.CreateSubscriptionAsync(
                nodeIds,
                100,
                (sender, e) => { });
            
            // Remove subscription
            await _client.RemoveSubscriptionAsync(subscriptionId);
            
            // Disconnect
            _client.Disconnect();
            Assert.False(_client.IsConnected);
        }
    }
} 