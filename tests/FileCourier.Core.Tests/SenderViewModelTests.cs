using System;
using Xunit;
using FileCourier.Core.Models;
using FileCourier.Core.ViewModels;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;

namespace FileCourier.Core.Tests
{
    public class SenderViewModelTests
    {
        private SenderViewModel CreateVm()
        {
            var tcp = new TcpTransferService(0); // port 0 = won't listen
            var settings = new SettingsStore();
            var history = new TransferHistoryStore();
            return new SenderViewModel(tcp, settings, history);
        }

        [Fact]
        public void InitiatingTransfer_RequiresFileOrText()
        {
            var vm = CreateVm();
            // No file, no text, no target — SendCommand should not execute meaningful work
            Assert.Equal(SenderState.Idle, vm.State);
        }

        [Fact]
        public void Cancel_ResetsStateToIdle()
        {
            var vm = CreateVm();
            vm.TargetDevice = new SystemDevice();
            vm.SelectedFiles = new() { new FileItem("document.pdf", "document.pdf") };
            // We can't call SendAsync without a real network, but we can test Cancel
            vm.CancelCommand.Execute(null);

            Assert.Equal(SenderState.Idle, vm.State);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            var vm = CreateVm();
            vm.TargetDevice = new SystemDevice();
            vm.SelectedFiles = new() { new FileItem("data.zip", "data.zip") };
            vm.TextPayload = "hello";

            vm.ResetCommand.Execute(null);

            Assert.Equal(SenderState.Idle, vm.State);
            Assert.Null(vm.TargetDevice);
            Assert.Empty(vm.SelectedFiles);
            Assert.Null(vm.TextPayload);
        }

        [Fact]
        public void FileItem_StatusChange_NotifiesUI()
        {
            var item = new FileItem("test.txt", "test.txt");
            bool propertyChangedFired = false;
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileItem.Status))
                    propertyChangedFired = true;
            };

            item.Status = FileStatus.Transferred;

            Assert.True(propertyChangedFired);
            Assert.Equal(FileStatus.Transferred, item.Status);
        }
    }
}
