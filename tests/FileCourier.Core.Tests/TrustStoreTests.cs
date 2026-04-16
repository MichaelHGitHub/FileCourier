using System;
using System.Collections.Generic;
using Xunit;

namespace FileCourier.Core.Tests
{
    // Minimal mock for testability. In practice, this wraps SQLite or JSON.
    public class TrustStore
    {
        private readonly HashSet<Guid> _alwaysAgreeDevices = new();

        public void AddTrustedDevice(Guid deviceId)
        {
            _alwaysAgreeDevices.Add(deviceId);
        }

        public void RevokeTrustedDevice(Guid deviceId)
        {
            _alwaysAgreeDevices.Remove(deviceId);
        }

        public bool IsDeviceTrusted(Guid deviceId)
        {
            return _alwaysAgreeDevices.Contains(deviceId);
        }
    }

    public class TrustStoreTests
    {
        [Fact]
        public void DeviceIsUntrustedByDefault()
        {
            var store = new TrustStore();
            var arbitraryId = Guid.NewGuid();
            
            Assert.False(store.IsDeviceTrusted(arbitraryId));
        }

        [Fact]
        public void GrantingTrust_PersistsTrust()
        {
            var store = new TrustStore();
            var trustedId = Guid.NewGuid();
            
            store.AddTrustedDevice(trustedId);
            
            Assert.True(store.IsDeviceTrusted(trustedId));
        }

        [Fact]
        public void RevokingTrust_RemovesStatus()
        {
            var store = new TrustStore();
            var testId = Guid.NewGuid();
            
            store.AddTrustedDevice(testId);
            store.RevokeTrustedDevice(testId);
            
            Assert.False(store.IsDeviceTrusted(testId));
        }
    }
}
