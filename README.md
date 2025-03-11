# OPC UA Wrapper

A clean, modern wrapper for OPC UA client operations in .NET that simplifies interaction with OPC UA servers.

[![Build and Test](https://github.com/ActualAI/OPCUA-Wrapper/actions/workflows/build.yml/badge.svg)](https://github.com/ActualAI/OPCUA-Wrapper/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/OpcUaWrapper.svg)](https://www.nuget.org/packages/OpcUaWrapper/)

## Features

- Simple, intuitive API for OPC UA client operations
- Asynchronous methods for non-blocking operations
- Proper resource management with IDisposable implementation
- Event-based notification system for connection state changes
- Support for secure connections with certificate handling
- Auto-reconnection for resilient server connections

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

### OpcUaClient

The main class for interacting with OPC UA servers.

#### Constructor

```csharp
public OpcUaClient(string endpointUrl, string applicationName = "OPC UA Client", bool autoAcceptUntrustedCertificates = true)
```

#### Properties

- `bool IsConnected` - Gets a value indicating whether the client is connected to the server.
- `Session Session` - Gets the current session with the OPC UA server.

#### Methods

- `Task ConnectAsync(IUserIdentity userIdentity = null, CancellationToken cancellationToken = default)` - Connects to the OPC UA server.
- `void Disconnect()` - Disconnects from the OPC UA server.
- `Task<(object Value, StatusCode StatusCode)> ReadValueAsync(string nodeId, CancellationToken cancellationToken = default)` - Reads a value from the OPC UA server.
- `Task<StatusCode> WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken = default)` - Writes a value to the OPC UA server.
- `Task<IEnumerable<ReferenceDescription>> BrowseAsync(string nodeId = null, CancellationToken cancellationToken = default)` - Browses the server for nodes.
- `Task<uint> CreateSubscriptionAsync(IEnumerable<string> nodeIds, int samplingInterval, MonitoredItemNotificationEventHandler dataChangeHandler, CancellationToken cancellationToken = default)` - Creates a subscription for a list of monitored items.
- `Task RemoveSubscriptionAsync(uint subscriptionId, CancellationToken cancellationToken = default)` - Removes a subscription.

#### Events

- `event EventHandler Connected` - Event raised when the client connects to the server.
- `event EventHandler Disconnected` - Event raised when the client disconnects from the server.
- `event EventHandler Reconnected` - Event raised when the client reconnects to the server after a connection loss.
- `event EventHandler<KeepAliveEventArgs> KeepAliveFailed` - Event raised when a keep alive operation fails.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. 