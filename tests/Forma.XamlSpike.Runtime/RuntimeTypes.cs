// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

[assembly: Forma.XamlSpike.XmlnsDefinition("https://forma.dev/xaml", "Forma.XamlSpike")]

namespace Forma.XamlSpike;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class XmlnsDefinitionAttribute : Attribute
{
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
    {
        XmlNamespace = xmlNamespace;
        ClrNamespace = clrNamespace;
    }

    public string XmlNamespace { get; }
    public string ClrNamespace { get; }
}

public interface IAddChild
{
    void AddChild(object child);
}

public interface IAddChild<in T> : IAddChild
{
    void AddChild(T child);
}

public class SpikeControl : IAddChild<SpikeControl>
{
    private readonly List<SpikeControl> _children = new();

    public IReadOnlyList<SpikeControl> Children => _children;

    public void AddChild(SpikeControl child) => _children.Add(child);

    void IAddChild.AddChild(object child) => AddChild((SpikeControl)child);
}

public sealed class SpikeRoot : SpikeControl
{
    public string? Title { get; set; }
    public string? ConstructorState { get; set; }
    public int ActivationCount { get; private set; }
    public event EventHandler? Activated;

    public void OnActivated(object? sender, EventArgs eventArgs) => ActivationCount++;
    public void RaiseActivated() => Activated?.Invoke(this, EventArgs.Empty);
}

public sealed class SpikeLeaf : SpikeControl
{
    public string? Text { get; set; }
}

public sealed class EchoExtension
{
    public string? Value { get; set; }
    public object? ProvideValue(IServiceProvider serviceProvider) => Value;
}