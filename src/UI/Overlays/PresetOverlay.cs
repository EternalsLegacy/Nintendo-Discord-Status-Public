using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Overlays;

public static class PresetOverlay
{
    public static Control Build(
        ObservableCollection<PresetModel> Presets,
        string SuggestedName,
        string AccentHex,
        Action<PresetModel> OnLoadPreset,
        Func<string, bool> OnSaveCurrentPreset,
        Func<string, bool> OnDeletePreset,
        Action OnClose)
    {
        TextBox PresetNameBox = UiStyleHelper.CreateStyledTextBox("Custom preset name...", SuggestedName, 60);
        TextBlock FeedbackText = new TextBlock
        {
            FontSize = 11,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex),
            IsVisible = false
        };

        StackPanel ListPanel = new StackPanel { Spacing = 8 };

        void RebuildList()
        {
            ListPanel.Children.Clear();

            if (Presets.Count == 0)
            {
                ListPanel.Children.Add(new TextBlock
                {
                    Text = "No saved presets yet. Enter a name above and save your current setup!",
                    FontSize = 12,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (PresetModel Item in Presets)
            {
                string Subtitle = string.IsNullOrWhiteSpace(Item.Details) && string.IsNullOrWhiteSpace(Item.State)
                    ? $"{Item.ConsoleName} • {Item.GameName}"
                    : $"{Item.ConsoleName} • {Item.GameName} ({Item.Details} {Item.State})".Trim();

                Button LoadButton = new Button
                {
                    Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8),
                    Content = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = Item.PresetName,
                                FontSize = 13,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex)
                            },
                            new TextBlock
                            {
                                Text = Subtitle,
                                FontSize = 11,
                                Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        }
                    }
                };
                LoadButton.Click += (Sender, Args) =>
                {
                    OnLoadPreset(Item);
                    OnClose();
                };

                Button DeleteButton = UiStyleHelper.CreateSecondaryButton("Delete");
                DeleteButton.Margin = new Thickness(6, 0, 0, 0);
                DeleteButton.Click += (Sender, Args) =>
                {
                    OnDeletePreset(Item.PresetName);
                    RebuildList();
                };

                Grid RowGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto")
                };
                RowGrid.Children.Add(LoadButton);
                Grid.SetColumn(LoadButton, 0);
                RowGrid.Children.Add(DeleteButton);
                Grid.SetColumn(DeleteButton, 1);

                ListPanel.Children.Add(RowGrid);
            }
        }

        RebuildList();

        Button SaveCurrentButton = UiStyleHelper.CreatePrimaryButton("Save Current", AccentHex);
        SaveCurrentButton.Margin = new Thickness(8, 0, 0, 0);
        SaveCurrentButton.Click += (Sender, Args) =>
        {
            string EnteredName = PresetNameBox.Text?.Trim() ?? string.Empty;
            if (!OnSaveCurrentPreset(EnteredName))
            {
                FeedbackText.Text = "Please enter a preset name first.";
                FeedbackText.IsVisible = true;
                return;
            }

            FeedbackText.IsVisible = false;
            PresetNameBox.Text = string.Empty;
            RebuildList();
        };

        Grid SaveRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        SaveRow.Children.Add(PresetNameBox);
        Grid.SetColumn(PresetNameBox, 0);
        SaveRow.Children.Add(SaveCurrentButton);
        Grid.SetColumn(SaveCurrentButton, 1);

        ScrollViewer Scroller = new ScrollViewer
        {
            MaxHeight = 250,
            Content = ListPanel
        };

        Button CloseButton = UiStyleHelper.CreateSecondaryButton("Close");
        CloseButton.HorizontalAlignment = HorizontalAlignment.Right;
        CloseButton.Click += (Sender, Args) => OnClose();

        StackPanel CardContent = new StackPanel { Spacing = 12 };
        CardContent.Children.Add(UiStyleHelper.CreateLabel("PRESETS", 14, true, ThemeConstants.TextWhiteHex));
        CardContent.Children.Add(SaveRow);
        CardContent.Children.Add(FeedbackText);
        CardContent.Children.Add(Scroller);
        CardContent.Children.Add(CloseButton);

        return UiStyleHelper.WrapModalCard(CardContent, 430);
    }
}
