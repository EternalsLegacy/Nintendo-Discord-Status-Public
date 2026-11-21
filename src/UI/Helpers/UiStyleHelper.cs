using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;

namespace NintendoDiscordStatus.UI.Helpers;

public static class UiStyleHelper
{
    public const string PresetsIconData = "M15,5H5C3.9,5 3,5.9 3,7V21L10,18L17,21V7C17,5.9 16.1,5 15,5M19,1H9V3H19V17H21V3C21,1.9 20.1,1 19,1Z";
    public const string SettingsIconData = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";
    public const string SaveIconData = "M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z";
    public const string UpdateIconData = "M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z";
    public const string TrashIconData = "M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z";

    public static IBrush ParseBrush(string HexColor)
    {
        return Brush.Parse(HexColor);
    }

    public static Path CreateVectorIcon(string PathData, string ColorHex, double Size = 18)
    {
        return new Path
        {
            Data = Geometry.Parse(PathData),
            Fill = ParseBrush(ColorHex),
            Stretch = Stretch.Uniform,
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Border WrapModalCard(Control Content, double Width)
    {
        return new Border
        {
            Background = Brush.Parse("#B0000000"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new Border
            {
                Width = Width,
                Background = ParseBrush(ThemeConstants.CardBackgroundHex),
                BorderBrush = ParseBrush("#3A3A44"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Content
            }
        };
    }

    public static TextBlock CreateLabel(string Text, double FontSize = 11, bool IsBold = false, string ColorHex = ThemeConstants.SubHeadingHex)
    {
        return new TextBlock
        {
            Text = Text,
            FontSize = FontSize,
            FontWeight = IsBold ? FontWeight.Bold : FontWeight.SemiBold,
            Foreground = ParseBrush(ColorHex)
        };
    }

    public static TextBox CreateStyledTextBox(string Watermark, string InitialText = "", int MaxLength = AppConstants.MaxDetailsLength)
    {
        return new TextBox
        {
            Watermark = Watermark,
            Text = InitialText,
            MaxLength = MaxLength,
            Background = ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7)
        };
    }

    public static Button CreatePrimaryButton(string ContentText, string AccentHex = ThemeConstants.DefaultAccentHex)
    {
        return new Button
        {
            Content = ContentText,
            Background = ParseBrush(AccentHex),
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 8),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }

    public static Button CreateSecondaryButton(string ContentText)
    {
        return new Button
        {
            Content = ContentText,
            Background = ParseBrush("#303036"),
            Foreground = ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
    }
}
