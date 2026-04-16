using System;
using Xunit;

namespace FileCourier.Core.Tests
{
    public enum SenderState 
    {
        Idle,
        WaitingForReceiver,
        Transferring,
        Completed,
        Rejected
    }

    // Mock representation of the UI ViewModel
    public class SenderViewModel
    {
        public SenderState State { get; private set; } = SenderState.Idle;
        public string? SelectedFile { get; private set; }
        public SystemDevice? TargetDevice { get; private set; }

        public void SelectFileAndTarget(string file, SystemDevice device)
        {
            SelectedFile = file;
            TargetDevice = device;
        }

        public void InitiateTransfer()
        {
            if (string.IsNullOrEmpty(SelectedFile) || TargetDevice == null)
                throw new InvalidOperationException("Missing file or target");
                
            // When send is clicked, transition to waiting
            State = SenderState.WaitingForReceiver;
        }

        public void OnReceiverResponded(bool accepted)
        {
            State = accepted ? SenderState.Transferring : SenderState.Rejected;
        }
    }

    public class SenderViewModelTests
    {
        [Fact]
        public void InitiatingTransfer_ChangesStateToWaiting()
        {
            var vm = new SenderViewModel();
            vm.SelectFileAndTarget("document.pdf", new SystemDevice());
            
            vm.InitiateTransfer();
            
            Assert.Equal(SenderState.WaitingForReceiver, vm.State);
        }

        [Fact]
        public void ValidationFails_WhenNoFileSelected()
        {
            var vm = new SenderViewModel();
            Assert.Throws<InvalidOperationException>(() => vm.InitiateTransfer());
        }

        [Fact]
        public void ReceiverResponds_ChangesStateAccordingly()
        {
            var vm = new SenderViewModel();
            vm.SelectFileAndTarget("data.zip", new SystemDevice());
            vm.InitiateTransfer();
            
            vm.OnReceiverResponded(accepted: true);
            Assert.Equal(SenderState.Transferring, vm.State);

            vm.OnReceiverResponded(accepted: false);
            Assert.Equal(SenderState.Rejected, vm.State);
        }
    }
}
