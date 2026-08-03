using Forma.XamlSpike;
using Forma.XamlSpike.Generated;

var root = GeneratedView.Build(null);
if (root.Children.Count != 1 || root.Children[0] is not SpikeLeaf { Text: "Cecil" })
    throw new InvalidOperationException("NativeAOT generated view did not build the expected tree.");

Console.WriteLine("Forma XAML generated NativeAOT view: PASS");