using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using OpcUaWrapper;

namespace OpcUaWrapper.Samples
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("OPC UA Client Wrapper Sample");
            Console.WriteLine("============================");
            
            // Replace with your OPC UA server endpoint
            string endpointUrl = "opc.tcp://localhost:4840";
            
            if (args.Length > 0)
            {
                endpointUrl = args[0];
            }
            
            Console.WriteLine($"Connecting to: {endpointUrl}");
            
            try
            {
                // Create and connect the client
                using (var client = new OpcUaClient(endpointUrl))
                {
                    // Set up event handlers
                    client.Connected += (sender, e) => Console.WriteLine("Connected to server");
                    client.Disconnected += (sender, e) => Console.WriteLine("Disconnected from server");
                    client.Reconnected += (sender, e) => Console.WriteLine("Reconnected to server");
                    client.KeepAliveFailed += (sender, e) => Console.WriteLine($"Keep alive failed: {e.Status}");
                    
                    // Connect to the server
                    await client.ConnectAsync();
                    
                    if (client.IsConnected)
                    {
                        Console.WriteLine("Successfully connected to the server");
                        
                        // Browse the server's address space
                        Console.WriteLine("\nBrowsing the server's address space:");
                        var nodes = await client.BrowseAsync();
                        foreach (var node in nodes)
                        {
                            Console.WriteLine($"- {node.DisplayName}: {node.NodeId}");
                        }
                        
                        // Read a value (replace with a valid node ID from your server)
                        string nodeIdToRead = "ns=2;s=Demo.Static.Scalar.Double";
                        Console.WriteLine($"\nReading value from {nodeIdToRead}:");
                        try
                        {
                            var (value, statusCode) = await client.ReadValueAsync(nodeIdToRead);
                            Console.WriteLine($"Value: {value}, Status: {statusCode}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading value: {ex.Message}");
                        }
                        
                        // Create a subscription (replace with valid node IDs from your server)
                        Console.WriteLine("\nCreating a subscription:");
                        try
                        {
                            var nodeIds = new List<string> { "ns=2;s=Demo.Dynamic.Scalar.Double" };
                            uint subscriptionId = await client.CreateSubscriptionAsync(
                                nodeIds,
                                1000, // 1 second sampling interval
                                (sender, e) =>
                                {
                                    if (e.NotificationValue is MonitoredItemNotification notification)
                                    {
                                        Console.WriteLine($"Subscription update: {notification.Value.Value}, " +
                                                         $"Status: {notification.Value.StatusCode}, " +
                                                         $"Timestamp: {notification.Value.SourceTimestamp}");
                                    }
                                });
                            
                            Console.WriteLine($"Created subscription with ID: {subscriptionId}");
                            
                            // Wait for some subscription updates
                            Console.WriteLine("Waiting for subscription updates (press Enter to continue)...");
                            Console.ReadLine();
                            
                            // Remove the subscription
                            await client.RemoveSubscriptionAsync(subscriptionId);
                            Console.WriteLine("Subscription removed");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error with subscription: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to connect to the server");
                    }
                    
                    // Disconnect from the server
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }
} 