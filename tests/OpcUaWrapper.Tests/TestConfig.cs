using System;
using System.IO;
using System.Text.Json;

namespace OpcUaWrapper.Tests
{
    /// <summary>
    /// Helper class for test configuration.
    /// </summary>
    public static class TestConfig
    {
        private static readonly Lazy<TestSettings> _settings = new Lazy<TestSettings>(LoadSettings);
        
        /// <summary>
        /// Gets the test settings.
        /// </summary>
        public static TestSettings Settings => _settings.Value;
        
        private static TestSettings LoadSettings()
        {
            // First try to load from testsettings.json
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<TestSettings>(json) ?? new TestSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading test settings from file: {ex.Message}");
            }
            
            // Fall back to environment variables
            return new TestSettings
            {
                ServerUrl = Environment.GetEnvironmentVariable("OPCUA_SERVER_URL") ?? "opc.tcp://localhost:4840",
                TestNodeId = Environment.GetEnvironmentVariable("OPCUA_TEST_NODE_ID") ?? "ns=2;s=Demo.Static.Scalar.Double",
                TestWritableNodeId = Environment.GetEnvironmentVariable("OPCUA_TEST_WRITABLE_NODE_ID") ?? "ns=2;s=Demo.Static.Scalar.Double",
                Username = Environment.GetEnvironmentVariable("OPCUA_USERNAME"),
                Password = Environment.GetEnvironmentVariable("OPCUA_PASSWORD"),
                SkipIntegrationTests = bool.TryParse(Environment.GetEnvironmentVariable("OPCUA_SKIP_INTEGRATION_TESTS"), out bool skip) && skip
            };
        }
    }
    
    /// <summary>
    /// Settings for OPC UA tests.
    /// </summary>
    public class TestSettings
    {
        /// <summary>
        /// Gets or sets the OPC UA server URL.
        /// </summary>
        public string ServerUrl { get; set; } = "opc.tcp://localhost:4840";
        
        /// <summary>
        /// Gets or sets a node ID that can be read for testing.
        /// </summary>
        public string TestNodeId { get; set; } = "ns=2;s=Demo.Static.Scalar.Double";
        
        /// <summary>
        /// Gets or sets a node ID that can be written to for testing.
        /// </summary>
        public string TestWritableNodeId { get; set; } = "ns=2;s=Demo.Static.Scalar.Double";
        
        /// <summary>
        /// Gets or sets the username for authentication (if required).
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// Gets or sets the password for authentication (if required).
        /// </summary>
        public string Password { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to skip integration tests.
        /// </summary>
        public bool SkipIntegrationTests { get; set; }
    }
} 