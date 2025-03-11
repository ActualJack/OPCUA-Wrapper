# OPC UA Wrapper

A clean, modern wrapper for OPC UA client operations in .NET that simplifies interaction with OPC UA servers.

<!-- Badges will be enabled once the package is published and CI is set up -->
<!-- 
[![Build and Test](https://github.com/ActualAI/OPCUA-Wrapper/actions/workflows/build.yml/badge.svg)](https://github.com/ActualAI/OPCUA-Wrapper/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/OpcUaWrapper.svg)](https://www.nuget.org/packages/OpcUaWrapper/)
-->

## Why This Wrapper?

The official OPC UA .NET library is powerful but notoriously complex and difficult to use. This wrapper was created to address these challenges by providing:

- **Simplified API**: Reduces hundreds of lines of boilerplate code to just a few intuitive method calls
- **Modern C# Patterns**: Uses async/await, tuples, and other modern C# features for a cleaner developer experience
- **Error Handling**: Provides consistent error handling instead of the complex error hierarchies in the base library
- **Resource Management**: Automatically handles the complex lifecycle of OPC UA resources
- **Reconnection Logic**: Built-in handling of connection losses and automatic reconnection
- **Reduced Learning Curve**: Eliminates the need to understand the complex OPC UA object model to perform basic operations

What would typically require 50-100 lines of code with the base OPC UA library can often be accomplished in 5-10 lines with this wrapper.

## Features

- Simple, intuitive API for OPC UA client operations
- Asynchronous methods for non-blocking operations
- Proper resource management with IDisposable implementation
- Event-based notification system for connection state changes
- Support for secure connections with certificate handling
- Auto-reconnection for resilient server connections

## Key Benefits

| Without OpcUaWrapper | With OpcUaWrapper |
|----------------------|-------------------|
| Complex session and channel management | Simple Connect/Disconnect methods |
| Manual certificate handling | Automatic certificate management |
| Complicated subscription setup | One-line subscription creation |
| Manual type conversion for reads/writes | Automatic value handling |
| Complex error handling hierarchies | Consistent, simplified error handling |
| Steep learning curve | Intuitive, discoverable API |
| Hundreds of lines of boilerplate code | Concise, focused implementation |

## Version Compatibility

This wrapper is built for OPC UA SDK version 1.4.371.60. A version compatible with OPC UA SDK 1.5.x is planned for future development.

## Security Considerations

The current implementation focuses on core functionality and simplifies security aspects for ease of use. For production environments, consider the following security enhancements that the community can contribute:

1. **Certificate Management**: Implement proper certificate creation, validation, and storage
2. **Secure Connections**: Enable and configure secure communication channels
3. **Authentication**: Enhance user authentication mechanisms beyond anonymous access
4. **Encryption**: Configure proper encryption for data transmission
5. **Vulnerability Mitigation**: Address known vulnerabilities in the OPC UA SDK

These security enhancements are open for community contributions.

## Installation

Install the package via NuGet:

```
dotnet add package OpcUaWrapper
```

## Quick Start

```csharp
using System;
using System.Threading.Tasks;
using OpcUaWrapper;

// Create a client
using var client = new OpcUaClient("opc.tcp://localhost:4840");

// Connect to the server
await client.ConnectAsync();

// Read a value
var (value, statusCode) = await client.ReadValueAsync("ns=2;s=Demo.Static.Scalar.Double");
Console.WriteLine($"Value: {value}, Status: {statusCode}");

// Write a value
var writeStatus = await client.WriteValueAsync("ns=2;s=Demo.Static.Scalar.Double", 42.0);
Console.WriteLine($"Write status: {writeStatus}");

// Browse the server's address space
var nodes = await client.BrowseAsync();
foreach (var node in nodes)
{
    Console.WriteLine($"{node.DisplayName}: {node.NodeId}");
}

// Disconnect when done
client.Disconnect();
```

## Subscriptions Example

```csharp
// Create a subscription
var nodeIds = new List<string> { "ns=2;s=Demo.Dynamic.Scalar.Double" };
uint subscriptionId = await client.CreateSubscriptionAsync(
    nodeIds,
    1000, // 1 second sampling interval
    (sender, e) =>
    {
        if (e.NotificationValue is MonitoredItemNotification notification)
        {
            Console.WriteLine($"Value: {notification.Value.Value}");
        }
    });

// Remove the subscription when done
await client.RemoveSubscriptionAsync(subscriptionId);
```

## API Reference

### OpcUaClient Class

The main class for interacting with OPC UA servers.

#### Constructor

```csharp
public OpcUaClient(string endpointUrl, string applicationName = "OPC UA Client", bool autoAcceptUntrustedCertificates = true)
```

- **endpointUrl**: The URL of the OPC UA server endpoint (e.g., "opc.tcp://localhost:4840")
- **applicationName**: The name of the client application (optional)
- **autoAcceptUntrustedCertificates**: Whether to automatically accept untrusted certificates (optional)

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Gets a value indicating whether the client is connected to the server |
| `Session` | `Session` | Gets the current session with the OPC UA server |

#### Events

| Event | Type | Description |
|-------|------|-------------|
| `Connected` | `EventHandler` | Raised when the client connects to the server |
| `Disconnected` | `EventHandler` | Raised when the client disconnects from the server |
| `Reconnected` | `EventHandler` | Raised when the client reconnects to the server after a connection loss |
| `KeepAliveFailed` | `EventHandler<KeepAliveEventArgs>` | Raised when a keep alive operation fails |

#### Methods

##### ConnectAsync

Connects to the OPC UA server.

```csharp
public async Task ConnectAsync(IUserIdentity userIdentity = null, CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `userIdentity`: The user identity for authentication (optional, defaults to anonymous)
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: A task representing the asynchronous operation
- **Exceptions**:
  - `InvalidOperationException`: Thrown when the connection fails
  - `ArgumentException`: Thrown when the endpoint URL is invalid

##### Disconnect

Disconnects from the OPC UA server.

```csharp
public void Disconnect()
```

- **Parameters**: None
- **Returns**: None

##### ReadValueAsync

Reads a value from the OPC UA server.

```csharp
public async Task<(object Value, StatusCode StatusCode)> ReadValueAsync(string nodeId, CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `nodeId`: The node ID to read from (e.g., "ns=2;s=Demo.Static.Scalar.Double")
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: A tuple containing:
  - `Value`: The value read from the server (can be null)
  - `StatusCode`: The status code of the read operation
- **Exceptions**:
  - `InvalidOperationException`: Thrown when not connected to the server
  - `ServiceResultException`: Thrown when the read operation fails

##### WriteValueAsync

Writes a value to the OPC UA server.

```csharp
public async Task<StatusCode> WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `nodeId`: The node ID to write to (e.g., "ns=2;s=Demo.Static.Scalar.Double")
  - `value`: The value to write
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: The status code of the write operation
- **Exceptions**:
  - `InvalidOperationException`: Thrown when not connected to the server
  - `ServiceResultException`: Thrown when the write operation fails

##### BrowseAsync

Browses the server for nodes.

```csharp
public async Task<IEnumerable<ReferenceDescription>> BrowseAsync(string nodeId = null, CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `nodeId`: The node ID to browse from. If null, the server's objects folder is used (optional)
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: A collection of references describing the nodes found
- **Exceptions**:
  - `InvalidOperationException`: Thrown when not connected to the server
  - `ServiceResultException`: Thrown when the browse operation fails

##### CreateSubscriptionAsync

Creates a subscription for a list of monitored items.

```csharp
public async Task<uint> CreateSubscriptionAsync(
    IEnumerable<string> nodeIds,
    int samplingInterval,
    MonitoredItemNotificationEventHandler dataChangeHandler,
    CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `nodeIds`: The node IDs to monitor
  - `samplingInterval`: The sampling interval in milliseconds
  - `dataChangeHandler`: The handler for data change notifications
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: The subscription ID
- **Exceptions**:
  - `InvalidOperationException`: Thrown when not connected to the server
  - `ServiceResultException`: Thrown when the subscription creation fails

##### RemoveSubscriptionAsync

Removes a subscription.

```csharp
public async Task RemoveSubscriptionAsync(uint subscriptionId, CancellationToken cancellationToken = default)
```

- **Parameters**:
  - `subscriptionId`: The subscription ID to remove
  - `cancellationToken`: A cancellation token that can be used to cancel the operation (optional)
- **Returns**: A task representing the asynchronous operation
- **Exceptions**:
  - `InvalidOperationException`: Thrown when not connected to the server
  - `ServiceResultException`: Thrown when the subscription removal fails

## Node ID Format

Node IDs in OPC UA are used to uniquely identify nodes in the address space. The format is:

```
ns=<namespace index>;<identifier type>=<identifier>
```

Common formats:
- String identifier: `ns=2;s=Demo.Static.Scalar.Double`
- Numeric identifier: `ns=2;i=1234`
- GUID identifier: `ns=2;g=A123456789ABCDEF0123456789ABCDEF`
- Opaque identifier: `ns=2;b=base64encodeddata`

The namespace index (`ns`) is a numeric value that identifies the namespace. The identifier type can be:
- `s`: String
- `i`: Numeric
- `g`: GUID
- `b`: Opaque (binary)

## Running Tests

The project includes both unit tests and integration tests:

### Unit Tests

Unit tests can be run without any external dependencies:

```bash
dotnet test --filter Category!=Integration
```

### Integration Tests

Integration tests require a running OPC UA server. You can use one of the following options:

1. **OPC UA Reference Server**: Download and run the [OPC Foundation's Reference Server](https://github.com/OPCFoundation/UA-.NETStandard-Samples)
2. **Prosys OPC UA Simulation Server**: A free simulation server available at [Prosys OPC UA](https://www.prosysopc.com/products/opc-ua-simulation-server/)
3. **Docker Container**: Use a containerized OPC UA server like `open62541/open62541`

Before running integration tests, configure the server connection in `tests/OpcUaWrapper.Tests/testsettings.json`:

```json
{
  "OpcUaServer": {
    "Url": "opc.tcp://localhost:4840",
    "TestNodeId": "ns=2;s=Demo.Static.Scalar.Double",
    "TestWritableNodeId": "ns=2;s=Demo.Static.Scalar.Double"
  }
}
```

Then run the integration tests:

```bash
dotnet test --filter Category=Integration
```

## Troubleshooting

### Common Connection Issues

1. **Server not reachable**: Ensure the server is running and accessible from your network
2. **Incorrect endpoint URL**: Verify the protocol, IP address, and port (e.g., "opc.tcp://localhost:4840")
3. **Security configuration**: If the server requires secure connections, ensure certificates are properly configured
4. **Authentication failure**: If the server requires authentication, provide valid credentials

### Common Operation Issues

1. **Invalid node ID**: Ensure the node ID format is correct and the node exists on the server
2. **Permission denied**: Verify you have the necessary permissions to read/write the node
3. **Data type mismatch**: When writing values, ensure the data type matches what the server expects
4. **Subscription failures**: Some servers limit the number of subscriptions or have specific requirements for monitoring

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 