using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;

namespace FileCourier.Core.ViewModels;

/// <summary>Exposes transfer history records for binding in HistoryPage.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly TransferHistoryStore _store;

    public ObservableCollection<TransferHistoryRecord> Records { get; } = new();

    public HistoryViewModel(TransferHistoryStore store)
    {
        _store = store;
        Refresh();
    }

    public void Refresh()
    {
        Records.Clear();
        foreach (var r in _store.GetAll())
            Records.Add(r);
    }
}
