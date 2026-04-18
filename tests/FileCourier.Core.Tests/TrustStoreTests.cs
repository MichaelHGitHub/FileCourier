using System;
using Xunit;
using FileCourier.Core.Storage;

namespace FileCourier.Core.Tests
{
    public class TrustStoreTests
    {
        [Fact]
        public void DeviceIsUntrustedByDefault()
        {
            using var store = new TrustStore(); // in-memory
            var arbitraryId = Guid.NewGuid();

            Assert.False(store.IsDeviceTrusted(arbitraryId));
        }

        [Fact]
        public void GrantingTrust_PersistsTrust()
        {
            using var store = new TrustStore();
            var trustedId = Guid.NewGuid();

            store.AddTrustedDevice(trustedId);

            Assert.True(store.IsDeviceTrusted(trustedId));
        }

        [Fact]
        public void RevokingTrust_RemovesStatus()
        {
            using var store = new TrustStore();
            var testId = Guid.NewGuid();

            store.AddTrustedDevice(testId);
            store.RevokeTrustedDevice(testId);

            Assert.False(store.IsDeviceTrusted(testId));
        }
    }
}
