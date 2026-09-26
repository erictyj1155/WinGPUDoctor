using System.Windows.Markup;

namespace WinGPUDoctor.Desktop.Views;

// XAML access to resource copy: Text="{views:Text Welcome.Title}".
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
    public TextExtension() { }
    public TextExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(Key);
}
