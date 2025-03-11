using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Opc.Ua;
using Opc.Ua.Client;
using Moq;

namespace OpcUaWrapper.Tests
{
    public class OpcUaClientTests
    {
        #region Unit Tests

        [Fact]
        public void Constructor_WithValidEndpoint_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            var exception = Record.Exception(() => new OpcUaClient("opc.tcp://localhost:4840"));
            
            // Assert
            Assert.Null(exception);
        }
        
        [Fact]
        public void Constructor_WithNullEndpoint_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OpcUaClient(null));
        }
        
        [Fact]
        public void Constructor_WithEmptyEndpoint_ShouldThrowArgumentException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => new OpcUaClient(string.Empty));
        }
        
        [Fact]
        public void Constructor_WithInvalidEndpointFormat_ShouldNotThrow()
        {
            // Even with invalid format, constructor should not throw
            // Connection will fail later when ConnectAsync is called
            var exception = Record.Exception(() => new OpcUaClient("invalid-format"));
            
            Assert.Null(exception);
        }
        
        [Fact]
        public void IsConnected_WhenNotConnected_ShouldReturnFalse()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act
            bool isConnected = client.IsConnected;
            
            // Assert
            Assert.False(isConnected);
        }
        
        [Fact]
        public void Session_WhenNotConnected_ShouldNotBeNull()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            Assert.Null(client.Session);
        }
        
        [Fact]
        public async Task ReadValueAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.ReadValueAsync("ns=2;s=Demo.Static.Scalar.Double"));
        }
        
        [Fact]
        public async Task WriteValueAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.WriteValueAsync("ns=2;s=Demo.Static.Scalar.Double", 42.0));
        }
        
        [Fact]
        public async Task BrowseAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.BrowseAsync());
        }
        
        [Fact]
        public async Task CreateSubscriptionAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            var nodeIds = new List<string> { "ns=2;s=Demo.Dynamic.Scalar.Double" };
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.CreateSubscriptionAsync(
                    nodeIds, 1000, (sender, e) => { }));
        }
        
        [Fact]
        public async Task RemoveSubscriptionAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await client.RemoveSubscriptionAsync(1));
        }
        
        [Fact]
        public void Disconnect_WhenNotConnected_ShouldNotThrow()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            var exception = Record.Exception(() => client.Disconnect());
            Assert.Null(exception);
        }
        
        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var client = new OpcUaClient("opc.tcp://localhost:4840");
            
            // Act & Assert
            var exception = Record.Exception(() => client.Dispose());
            Assert.Null(exception);
        }

        #endregion
    }
} 