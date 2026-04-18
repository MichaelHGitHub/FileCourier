using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FileCourier.Core.Models;

namespace FileCourier.Core.Tests
{
    public class UdpDiscoveryTests
    {
        [Fact]
        public async Task CanBroadcastAndReceiveDiscoveryMessage()
        {
            // Arrange
            int port = 45000; // Arbitrary high port for local test
            var device = new SystemDevice
            {
                DeviceId = Guid.NewGuid(),
                DeviceName = "Test Windows PC",
                OS = "Windows"
            };

            var json = JsonSerializer.Serialize(device);
            var payload = Encoding.UTF8.GetBytes(json);

            // Act - Receiver
            var receiverTask = Task.Run(async () =>
            {
                using var udpClient = new UdpClient(port);
                // Listen indefinitely (in practice, a short timeout or cancellation token is used)
                udpClient.Client.ReceiveTimeout = 5000;
                var result = await udpClient.ReceiveAsync();

                return Encoding.UTF8.GetString(result.Buffer);
            });

            // Act - Sender
            // Give receiver a tiny bit of time to start listening
            await Task.Delay(100);
            using (var senderClient = new UdpClient())
            {
                // Send to loopback broadcast for testing
                await senderClient.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));
            }

            var receivedJson = await receiverTask;
            var receivedDevice = JsonSerializer.Deserialize<SystemDevice>(receivedJson);

            // Assert
            Assert.NotNull(receivedDevice);
            Assert.Equal(device.DeviceId, receivedDevice.DeviceId);
            Assert.Equal(device.DeviceName, receivedDevice.DeviceName);
        }
    }
}
