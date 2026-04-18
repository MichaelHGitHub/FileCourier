using System;
using Xunit;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.Core.Tests
{
    public class ReceiverViewModelTests
    {
        private ReceiverViewModel CreateVm()
        {
            var tcp = new TcpTransferService(0);
            var trust = new TrustStore();
            var settings = new SettingsStore();
            var vm = new ReceiverViewModel(tcp, trust, settings);
            vm.Dispatcher = action => action();
            return vm;
        }

        [Fact]
        public void DefaultState_IsIdle()
        {
            using var vm = CreateVm();
            Assert.Equal(ReceiverState.Idle, vm.State);
        }

        [Fact]
        public void DefaultSavePath_MatchesSettings()
        {
            using var vm = CreateVm();
            Assert.Contains("FileCourier", vm.DefaultSavePath);
        }

        [Fact]
        public void AcceptOnce_TransitionsToReceiving()
        {
            using var vm = CreateVm();
            var args = new IncomingTransferEventArgs
            {
                TransferId = Guid.NewGuid(),
                Header = new Core.Models.TransferRequestHeader
                {
                    SenderId = Guid.NewGuid(),
                    Files = new() { new Core.Models.TransferFile { FileName = "test.txt", RelativePath = "test.txt", FileSize = 100 } }
                },
                SenderIp = "192.168.1.1"
            };

            vm.AcceptOnce(args, @"D:\CustomDownloads");

            Assert.Equal(ReceiverState.Receiving, vm.State);
            Assert.True(args.Accepted);
            Assert.Equal(@"D:\CustomDownloads", vm.DefaultSavePath);
        }

        [Fact]
        public void Deny_ResetsStateToIdle()
        {
            using var vm = CreateVm();
            var args = new IncomingTransferEventArgs
            {
                TransferId = Guid.NewGuid(),
                Header = new Core.Models.TransferRequestHeader
                {
                    SenderId = Guid.NewGuid(),
                    Files = new() { new Core.Models.TransferFile { FileName = "virus.exe", RelativePath = "virus.exe", FileSize = 1000000 } }
                },
                SenderIp = "192.168.1.1"
            };

            vm.Deny(args);

            Assert.Equal(ReceiverState.Idle, vm.State);
            Assert.False(args.Accepted);
        }
    }
}
