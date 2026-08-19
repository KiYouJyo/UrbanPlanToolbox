using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Views;

internal static class MarkdownDocumentView
{
    public static UIElement Build(string markdown)
    {
        var stack = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(2, 4, 10, 12)
        };

        foreach (var block in SimpleMarkdownParser.Parse(markdown))
        {
            switch (block.Kind)
            {
                case SimpleMarkdownBlockKind.Heading1:
                    stack.Children.Add(Heading(block.Text, 26, new Thickness(0, 8, 0, 2)));
                    break;
                case SimpleMarkdownBlockKind.Heading2:
                    stack.Children.Add(Heading(block.Text, 20, new Thickness(0, 14, 0, 2)));
                    break;
                case SimpleMarkdownBlockKind.Heading3:
                    stack.Children.Add(Heading(block.Text, 16, new Thickness(0, 10, 0, 0)));
                    break;
                case SimpleMarkdownBlockKind.UnorderedListItem:
                case SimpleMarkdownBlockKind.OrderedListItem:
                    stack.Children.Add(ListItem(block.Prefix ?? "•", block.Text));
                    break;
                case SimpleMarkdownBlockKind.CodeBlock:
                    stack.Children.Add(new Border
                    {
                        Padding = new Thickness(12, 10, 12, 10),
                        CornerRadius = new CornerRadius(6),
                        Background = ResourceBrush("CardBackgroundFillColorSecondaryBrush"),
                        Child = new TextBlock
                        {
                            Text = block.Text,
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new FontFamily("Consolas"),
                            IsTextSelectionEnabled = true
                        }
                    });
                    break;
                case SimpleMarkdownBlockKind.Separator:
                    stack.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(0, 8, 0, 8),
                        Background = ResourceBrush("DividerStrokeColorDefaultBrush")
                    });
                    break;
                default:
                    stack.Children.Add(new TextBlock
                    {
                        Text = block.Text,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        LineHeight = 24
                    });
                    break;
            }
        }

        return new ScrollViewer
        {
            Content = stack,
            MaxHeight = 720,
            MinWidth = 520,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Enabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static TextBlock Heading(string text, double fontSize, Thickness margin) => new()
    {
        Text = text,
        FontSize = fontSize,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
        Margin = margin
    };

    private static UIElement ListItem(string prefix, string text)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = prefix,
            MinWidth = 20,
            Opacity = .68,
            TextAlignment = TextAlignment.Right
        });
        var content = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            LineHeight = 23
        };
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private static Brush ResourceBrush(string key) => (Brush)Application.Current.Resources[key];
}
