using System;
using System.Text.Json;
using System.Text;
using Xunit;
using FileCourier.Core.Models;

namespace FileCourier.Core.Tests
{
    public class PayloadSerializationTests
    {
        [Fact]
        public void CanSerializeAndDeserializeTransferRequest()
        {
            // Arrange
            var req = new TransferRequestHeader
            {
                SenderId = Guid.NewGuid(),
                Files = new()
                {
                    new TransferFile
                    {
                        FileName = "test_image.png",
                        RelativePath = "test_image.png",
                        FileSize = 1048576
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(req);
            var headerBytes = Encoding.UTF8.GetBytes(json);

            var deserialized = JsonSerializer.Deserialize<TransferRequestHeader>(Encoding.UTF8.GetString(headerBytes));

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(req.Files[0].FileName, deserialized.Files[0].FileName);
            Assert.Equal(req.Files[0].FileSize, deserialized.Files[0].FileSize);
            Assert.Equal(req.SenderId, deserialized.SenderId);
        }

        [Fact]
        public void PacketHeaderIsFormattedCorrectly()
        {
            // The protocol will prefix every header JSON with a 4-byte integer defining the length
            var req = new TransferRequestHeader
            {
                Files = new() { new TransferFile { FileName = "data.bin", RelativePath = "data.bin", FileSize = 10 } }
            };
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
