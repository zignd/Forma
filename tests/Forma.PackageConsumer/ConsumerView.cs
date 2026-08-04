// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Forma.Xaml;

namespace Forma.PackageConsumer;

public sealed class ConsumerView : Control
{
    public int AttachedHandlerCalls { get; private set; }

    public ConsumerView()
    {
        FormaXamlLoader.Load(this);
    }

    private void OnStyleTargetAttached(object? sender, EventArgs args) => AttachedHandlerCalls++;
}

public sealed class ConsumerButton : BaseButton
{
}

public sealed class ConsumerResourceValue
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ConsumerTarget : Control
{
    private EventHandler? _stopRequested;

    public int StopRequestedSubscriberCount { get; private set; }
    public ConsumerResourceValue Value { get; set; } = new ConsumerResourceValue { Name = "Underlying" };

    public event EventHandler? StopRequested
    {
        add { _stopRequested += value; StopRequestedSubscriberCount++; }
        remove { _stopRequested -= value; StopRequestedSubscriberCount--; }
    }

    public void RaiseStopRequested() => _stopRequested?.Invoke(this, EventArgs.Empty);
}

public sealed class ConsumerViewModel : INotifyPropertyChanged
{
    private string _message = string.Empty;
    private bool _isActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            _message = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
}