using System;
using Xunit;

namespace FileCourier.Core.Tests
{
    public enum ReceiverState
    {
        Idle,
        PromptingUser,
        Receiving,
        Completed
    }

    public class ReceiverViewModel
    {
        public ReceiverState State { get; private set; } = ReceiverState.Idle;
        public string DefaultSavePath { get; set; } = @"C:\Downloads\FileCourier";
        
        // Mock method simulating an incoming network request
        public void OnIncomingRequestReceived(string filename, long size)
        {
            // Pops the "Accept?" dialog
            State = ReceiverState.PromptingUser;
        }

        public void HandleUserDecision(bool accepted, string? newPath = null)
        {
            if (newPath != null)
                DefaultSavePath = newPath;

            if (accepted)
                State = ReceiverState.Receiving;
            else
                State = ReceiverState.Idle;
        }
    }

    public class ReceiverViewModelTests
    {
        [Fact]
        public void IncomingRequest_TriggersPromptState()
        {
            var vm = new ReceiverViewModel();
            vm.OnIncomingRequestReceived("vacation.jpg", 5000);
            
            Assert.Equal(ReceiverState.PromptingUser, vm.State);
        }

        [Fact]
        public void UserAccepts_TransitionsToReceivingAndUpdatesPath()
        {
            var vm = new ReceiverViewModel();
            vm.OnIncomingRequestReceived("test.txt", 100);
            
            string customPath = @"D:\CustomDownloads";
            vm.HandleUserDecision(true, customPath);
            
            Assert.Equal(ReceiverState.Receiving, vm.State);
            Assert.Equal(customPath, vm.DefaultSavePath);
        }

        [Fact]
        public void UserDenies_ResetsStateToIdle()
        {
            var vm = new ReceiverViewModel();
            vm.OnIncomingRequestReceived("virus.exe", 1000000);
            
            vm.HandleUserDecision(false);
            
            Assert.Equal(ReceiverState.Idle, vm.State);
        }
    }
}
