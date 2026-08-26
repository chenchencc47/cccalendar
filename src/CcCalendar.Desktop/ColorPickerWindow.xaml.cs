using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CcCalendar.Desktop;

public partial class ColorPickerWindow : Window
{
    private bool updating;

    public ColorPickerWindow(string initialColor)
    {
        InitializeComponent();
        SetInitialColor(initialColor);
        UpdatePreview();
    }

    public string SelectedColor { get; private set; } = "#FFFFFF";

    private void SetInitialColor(string color)
    {
        if (color.Length != 7
            || color[0] != '#'
            || !byte.TryParse(color[1..3], NumberStyles.HexNumber, null, out byte red)
            || !byte.TryParse(color[3..5], NumberStyles.HexNumber, null, out byte green)
            || !byte.TryParse(color[5..7], NumberStyles.HexNumber, null, out byte blue))
        {
            red = 255;
            green = 255;
            blue = 255;
        }

        updating = true;
        RedSlider.Value = red;
        GreenSlider.Value = green;
        BlueSlider.Value = blue;
        RedBox.Text = red.ToString(CultureInfo.InvariantCulture);
        GreenBox.Text = green.ToString(CultureInfo.InvariantCulture);
        BlueBox.Text = blue.ToString(CultureInfo.InvariantCulture);
        updating = false;
        SelectedColor = FormatColor();
    }

    private void ChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!updating && IsLoaded)
        {
            updating = true;
            RedBox.Text = ((int)Math.Round(RedSlider.Value)).ToString(CultureInfo.InvariantCulture);
            GreenBox.Text = ((int)Math.Round(GreenSlider.Value)).ToString(CultureInfo.InvariantCulture);
            BlueBox.Text = ((int)Math.Round(BlueSlider.Value)).ToString(CultureInfo.InvariantCulture);
            updating = false;
            UpdatePreview();
        }
    }

    private void ChannelBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            value = 0;
        }

        value = Math.Clamp(value, 0, 255);
        updating = true;
        if (ReferenceEquals(box, RedBox)) RedSlider.Value = value;
        if (ReferenceEquals(box, GreenBox)) GreenSlider.Value = value;
        if (ReferenceEquals(box, BlueBox)) BlueSlider.Value = value;
        box.Text = value.ToString(CultureInfo.InvariantCulture);
        updating = false;
        UpdatePreview();
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        SelectedColor = FormatColor();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdatePreview()
    {
        SelectedColor = FormatColor();
        ColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedColor));
    }

    private string FormatColor() =>
        $"#{(int)Math.Round(RedSlider.Value):X2}{(int)Math.Round(GreenSlider.Value):X2}{(int)Math.Round(BlueSlider.Value):X2}";
}
