using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUaWrapper;
using System.Linq;

namespace OpcUaWrapper.Samples
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("OPC UA Client Wrapper Sample");
            Console.WriteLine("============================");
            
            // Get server details and build the endpoint URL
            string serverIp = args.Length > 0 ? args[0] : PromptForInput("Enter OPC UA server IP address", "localhost");
            string serverPort = args.Length > 1 ? args[1] : PromptForInput("Enter OPC UA server port", "4840");
            
            // Format the endpoint URL with the correct protocol prefix
            string endpointUrl = $"opc.tcp://{serverIp}:{serverPort}";
            Console.WriteLine($"\nUsing OPC UA endpoint: {endpointUrl}");
            
            try
            {
                // Create the client
                using (var client = new OpcUaClient(endpointUrl))
                {
                    // Set up event handlers
                    client.Connected += (sender, e) => Console.WriteLine("Connected to server");
                    client.Disconnected += (sender, e) => Console.WriteLine("Disconnected from server");
                    client.Reconnected += (sender, e) => Console.WriteLine("Reconnected to server");
                    client.KeepAliveFailed += (sender, e) => Console.WriteLine($"Keep alive failed: {e.Status}");
                    
                    // Connect to the server
                    Console.WriteLine($"Connecting to: {endpointUrl}");
                    try
                    {
                        await client.ConnectAsync();
                        
                        if (client.IsConnected)
                        {
                            Console.WriteLine("Successfully connected to the server");
                            
                            bool exit = false;
                            while (!exit && client.IsConnected)
                            {
                                exit = await ShowMenuAndProcessSelection(client);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Failed to connect to the server");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Connection error: {ex.Message}");
                        Console.WriteLine("Common issues:");
                        Console.WriteLine("1. Server is not running or not accessible");
                        Console.WriteLine("2. Incorrect IP address or port");
                        Console.WriteLine("3. Firewall blocking the connection");
                        Console.WriteLine("4. Server requires authentication");
                    }
                    
                    // Disconnect from the server if still connected
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                        Console.WriteLine("Disconnected from server");
                    }
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

        private static async Task<bool> ShowMenuAndProcessSelection(OpcUaClient client)
        {
            Console.WriteLine("\nOPC UA Client Operations:");
            Console.WriteLine("1. Browse server address space");
            Console.WriteLine("2. Read a value");
            Console.WriteLine("3. Write a value");
            Console.WriteLine("4. Create a subscription");
            Console.WriteLine("5. Disconnect and exit");
            
            string choice = PromptForInput("Enter your choice (1-5)", "1");
            
            switch (choice)
            {
                case "1":
                    await BrowseAddressSpace(client);
                    return false;
                
                case "2":
                    await ReadValue(client);
                    return false;
                
                case "3":
                    await WriteValue(client);
                    return false;
                
                case "4":
                    await CreateSubscription(client);
                    return false;
                
                case "5":
                    return true;
                
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    return false;
            }
        }

        private static async Task BrowseAddressSpace(OpcUaClient client)
        {
            Console.WriteLine("\nNode ID format examples:");
            Console.WriteLine("- Objects folder: ns=0;i=85");
            Console.WriteLine("- By string identifier: ns=2;s=MyVariable");
            Console.WriteLine("- By numeric identifier: ns=2;i=1234");
            Console.WriteLine("- Leave empty to browse the root Objects folder");
            
            string nodeId = PromptForInput("Enter node ID to browse (leave empty for root)", "");
            
            try
            {
                var nodes = string.IsNullOrEmpty(nodeId) 
                    ? await client.BrowseAsync() 
                    : await client.BrowseAsync(nodeId);
                
                Console.WriteLine("\nNodes found:");
                if (!nodes.Any())
                {
                    Console.WriteLine("No nodes found. The node might not exist or might not have any children.");
                }
                else
                {
                    foreach (var node in nodes)
                    {
                        Console.WriteLine($"- {node.DisplayName}: {node.NodeId}");
                    }
                    Console.WriteLine("\nTip: Copy a node ID from above to read or write its value");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error browsing address space: {ex.Message}");
                Console.WriteLine("Make sure the node ID format is correct (e.g., ns=2;s=MyVariable)");
            }
        }

        private static async Task ReadValue(OpcUaClient client)
        {
            Console.WriteLine("\nNode ID format examples:");
            Console.WriteLine("- By string identifier: ns=2;s=MyVariable");
            Console.WriteLine("- By numeric identifier: ns=2;i=1234");
            
            string nodeId = PromptForInput("Enter node ID to read", "ns=2;s=Demo.Static.Scalar.Double");
            
            try
            {
                var (value, statusCode) = await client.ReadValueAsync(nodeId);
                Console.WriteLine($"Value: {value}, Status: {statusCode}");
                
                if (value != null)
                {
                    Console.WriteLine($"Data type: {value.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading value: {ex.Message}");
                Console.WriteLine("Common issues:");
                Console.WriteLine("1. Node ID doesn't exist");
                Console.WriteLine("2. Incorrect node ID format");
                Console.WriteLine("3. Node is not readable");
            }
        }

        private static async Task WriteValue(OpcUaClient client)
        {
            Console.WriteLine("\nNode ID format examples:");
            Console.WriteLine("- By string identifier: ns=2;s=MyVariable");
            Console.WriteLine("- By numeric identifier: ns=2;i=1234");
            
            string nodeId = PromptForInput("Enter node ID to write to", "ns=2;s=Demo.Static.Scalar.Double");
            
            Console.WriteLine("\nValue format examples:");
            Console.WriteLine("- Number: 42.5");
            Console.WriteLine("- Boolean: true or false");
            Console.WriteLine("- String: Hello World");
            
            string valueStr = PromptForInput("Enter value to write", "42.0");
            
            try
            {
                // Try to parse the value based on common types
                object value;
                if (double.TryParse(valueStr, out double doubleValue))
                {
                    value = doubleValue;
                    Console.WriteLine("Interpreted as a double value");
                }
                else if (int.TryParse(valueStr, out int intValue))
                {
                    value = intValue;
                    Console.WriteLine("Interpreted as an integer value");
                }
                else if (bool.TryParse(valueStr, out bool boolValue))
                {
                    value = boolValue;
                    Console.WriteLine("Interpreted as a boolean value");
                }
                else
                {
                    value = valueStr; // Default to string
                    Console.WriteLine("Interpreted as a string value");
                }
                
                var statusCode = await client.WriteValueAsync(nodeId, value);
                Console.WriteLine($"Write status: {statusCode}");
                
                // Check if the status code is good
                if (StatusCode.IsGood(statusCode))
                {
                    // Verify the write by reading back the value
                    var (readValue, readStatus) = await client.ReadValueAsync(nodeId);
                    Console.WriteLine($"Read back value: {readValue}, Status: {readStatus}");
                }
                else
                {
                    Console.WriteLine("Write operation failed. The node might not be writable or the data type might be incorrect.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing value: {ex.Message}");
                Console.WriteLine("Common issues:");
                Console.WriteLine("1. Node ID doesn't exist");
                Console.WriteLine("2. Incorrect node ID format");
                Console.WriteLine("3. Node is not writable");
                Console.WriteLine("4. Value type doesn't match the node's data type");
            }
        }

        private static async Task CreateSubscription(OpcUaClient client)
        {
            Console.WriteLine("\nNode ID format examples:");
            Console.WriteLine("- By string identifier: ns=2;s=MyVariable");
            Console.WriteLine("- By numeric identifier: ns=2;i=1234");
            Console.WriteLine("- For multiple nodes, separate with commas");
            Console.WriteLine("- Example: ns=2;s=Demo.Dynamic.Scalar.Double,ns=2;s=Demo.Dynamic.Scalar.Int32");
            
            string nodeIdInput = PromptForInput("Enter node ID(s) to monitor (separate multiple IDs with commas)", "ns=2;s=Demo.Dynamic.Scalar.Double");
            
            Console.WriteLine("\nSampling interval determines how often the server checks for changes");
            Console.WriteLine("- Recommended: 100-1000 ms");
            Console.WriteLine("- Faster intervals (e.g., 100ms) detect changes more quickly but increase server load");
            Console.WriteLine("- Slower intervals (e.g., 1000ms) reduce server load but may miss rapid changes");
            
            string samplingIntervalStr = PromptForInput("Enter sampling interval in milliseconds", "1000");
            
            if (!int.TryParse(samplingIntervalStr, out int samplingInterval) || samplingInterval < 50)
            {
                Console.WriteLine("Invalid sampling interval. Using default of 1000ms.");
                samplingInterval = 1000; // Default to 1 second
            }
            
            var nodeIds = new List<string>(nodeIdInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            
            if (nodeIds.Count == 0)
            {
                Console.WriteLine("No valid node IDs provided. Subscription canceled.");
                return;
            }
            
            Console.WriteLine($"\nCreating subscription for {nodeIds.Count} node(s) with sampling interval {samplingInterval}ms");
            
            try
            {
                // Create a counter for received notifications
                int notificationCount = 0;
                
                uint subscriptionId = await client.CreateSubscriptionAsync(
                    nodeIds,
                    samplingInterval,
                    (sender, e) =>
                    {
                        if (e.NotificationValue is MonitoredItemNotification notification)
                        {
                            notificationCount++;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Notification #{notificationCount}:");
                            // Access the node ID from the sender (which is a MonitoredItem)
                            Console.WriteLine($"  Node: {((MonitoredItem)sender).DisplayName}");
                            Console.WriteLine($"  Value: {notification.Value.Value}");
                            Console.WriteLine($"  Status: {notification.Value.StatusCode}");
                            Console.WriteLine($"  Timestamp: {notification.Value.SourceTimestamp}");
                        }
                    });
                
                Console.WriteLine($"Created subscription with ID: {subscriptionId}");
                Console.WriteLine("\nWaiting for data changes...");
                Console.WriteLine("(If no changes appear, try writing to the monitored node in another session)");
                Console.WriteLine("Monitoring for 30 seconds... (Press Enter to stop sooner)");
                
                // Create a task that completes when Enter is pressed
                var userInputTask = Task.Run(() => Console.ReadLine());
                
                // Wait for either 30 seconds or user input
                await Task.WhenAny(userInputTask, Task.Delay(30000));
                
                // Remove the subscription
                await client.RemoveSubscriptionAsync(subscriptionId);
                Console.WriteLine("Subscription removed");
                
                if (notificationCount == 0)
                {
                    Console.WriteLine("\nNo data changes were detected. Possible reasons:");
                    Console.WriteLine("1. The monitored node's value didn't change during the subscription period");
                    Console.WriteLine("2. The node doesn't support monitoring");
                    Console.WriteLine("3. The node ID might be incorrect");
                    Console.WriteLine("\nTip: Try monitoring a node that changes frequently, like a counter or timestamp");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with subscription: {ex.Message}");
                Console.WriteLine("Common issues:");
                Console.WriteLine("1. One or more node IDs don't exist");
                Console.WriteLine("2. Incorrect node ID format");
                Console.WriteLine("3. Server doesn't support subscriptions");
                Console.WriteLine("4. Sampling interval too small for server capabilities");
            }
        }

        private static string PromptForInput(string prompt, string defaultValue)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            string input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }
    }
} 