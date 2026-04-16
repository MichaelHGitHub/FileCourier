using System;
using System.Text.Json;
using System.Text;
using Xunit;

namespace FileCourier.Core.Tests
{
    // Minimal mock model representing what will eventually be in FileCourier.Core
    public class TransferRequest
    {
        public Guid SenderId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    public class PayloadSerializationTests
    {
        [Fact]
        public void CanSerializeAndDeserializeTransferRequest()
        {
            // Arrange
            var req = new TransferRequest
            {
                SenderId = Guid.NewGuid(),
                FileName = "test_image.png",
                FileSize = 1048576,
            };

            // Act
            var json = JsonSerializer.Serialize(req);
            var headerBytes = Encoding.UTF8.GetBytes(json);
            
            var deserialized = JsonSerializer.Deserialize<TransferRequest>(Encoding.UTF8.GetString(headerBytes));

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(req.FileName, deserialized.FileName);
            Assert.Equal(req.FileSize, deserialized.FileSize);
            Assert.Equal(req.SenderId, deserialized.SenderId);
        }

        [Fact]
        public void PacketHeaderIsFormattedCorrectly()
        {
            // The protocol will prefix every header JSON with a 4-byte integer defining the length
            var req = new TransferRequest { FileName = "data.bin", FileSize = 10 };
            var json = JsonSerializer.Serialize(req);
            var headerBytes = Encoding.UTF8.GetBytes(json);

            var lengthPrefixBytes = BitConverter.GetBytes(headerBytes.Length);

            Assert.Equal(4, lengthPrefixBytes.Length);
            
            // In a real network, we will read the 4 bytes, convert them to int, and then read that many bytes
            int lengthFromPrefix = BitConverter.ToInt32(lengthPrefixBytes, 0);
            Assert.Equal(headerBytes.Length, lengthFromPrefix);
        }
    }
}
