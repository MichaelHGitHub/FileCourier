using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FileCourier.Core.Tests
{
    public class TcpTransferTests
    {
        [Fact]
        public async Task LocalhostServerCanReceiveBytesFromClient()
        {
            // Arrange: Setup a local TCP listener on any available port
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int localPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            string expectedMessage = "HELLO_FILECOURIER";
            byte[] bytesToSend = Encoding.UTF8.GetBytes(expectedMessage);

            // Act: Fire up a background receiver Task
            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            });

            // Act: Client connects and sends
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, localPort);
                using var stream = client.GetStream();
                await stream.WriteAsync(bytesToSend, 0, bytesToSend.Length);
            }

            string receivedMessage = await serverTask;
            listener.Stop();

            // Assert
            Assert.Equal(expectedMessage, receivedMessage);
        }
    }
}
