# OPC UA Wrapper Tests

This directory contains unit tests and integration tests for the OPC UA Wrapper library.

## Test Categories

The tests are divided into two categories:

1. **Unit Tests**: These tests don't require a real OPC UA server and test the basic functionality of the library.
2. **Integration Tests**: These tests require a real OPC UA server and test the actual communication with the server.

## Running the Tests

### Running All Tests

```bash
dotnet test
```

### Running Only Unit Tests

```bash
dotnet test --filter Category!=Integration
```

### Running Only Integration Tests

```bash
dotnet test --filter Category=Integration
```

## Configuring Integration Tests

Integration tests require a real OPC UA server. You can configure the tests in one of two ways:

### Option 1: Using testsettings.json

1. Copy `testsettings.json.example` to `testsettings.json`
2. Edit `testsettings.json` to match your OPC UA server configuration:

```json
{
  "ServerUrl": "opc.tcp://your-server:4840",
  "TestNodeId": "ns=2;s=YourReadableNode",
  "TestWritableNodeId": "ns=2;s=YourWritableNode",
  "Username": "optional-username",
  "Password": "optional-password",
  "SkipIntegrationTests": false
}
```

### Option 2: Using Environment Variables

Set the following environment variables:

- `OPCUA_SERVER_URL`: The URL of your OPC UA server
- `OPCUA_TEST_NODE_ID`: A node ID that can be read for testing
- `OPCUA_TEST_WRITABLE_NODE_ID`: A node ID that can be written to for testing
- `OPCUA_USERNAME`: Optional username for authentication
- `OPCUA_PASSWORD`: Optional password for authentication
- `OPCUA_SKIP_INTEGRATION_TESTS`: Set to "true" to skip integration tests

## Test Server Recommendations

For testing, you can use one of the following OPC UA servers:

1. **OPC Foundation's Reference Implementation**: Available for testing
2. **Prosys OPC UA Simulation Server**: Free for testing
3. **Docker containers**: Several OPC UA servers are available as Docker images

## Troubleshooting

If integration tests are failing, check the following:

1. Ensure your OPC UA server is running and accessible
2. Verify the server URL is correct
3. Confirm the test node IDs exist on your server
4. Check if authentication is required by your server 