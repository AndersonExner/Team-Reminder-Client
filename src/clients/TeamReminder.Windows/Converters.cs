using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TeamReminder.Windows;

// {Binding} does not convert bool to Visibility on its own; x:Bind is not used for these view-model paths.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is true) != Invert ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        (value is Visibility.Visible) != Invert;
}

// Collapses an empty message so it does not leave a gap in stacked layouts.
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
