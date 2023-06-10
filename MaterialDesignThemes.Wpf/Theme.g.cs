//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by mdresgen.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable
using System.Windows.Media;

namespace MaterialDesignThemes.Wpf;

partial class Theme
{
    public Color Background { get; set; }

    public Color Foreground { get; set; }

    public Color ForegroundLight { get; set; }

    public Color ValidationError { get; set; }

    public Button Buttons { get; set; } = new();

    public SnackBar SnackBars { get; set; } = new();

    public Card Cards { get; set; } = new();

    public CheckBox CheckBoxes { get; set; } = new();

    public Chip Chips { get; set; } = new();

    public DataGrid DataGrids { get; set; } = new();

    public TextBox TextBoxes { get; set; } = new();

    public GridSplitter GridSplitters { get; set; } = new();

    public Header Headers { get; set; } = new();

    public ListView ListViews { get; set; } = new();

    public ScrollBar ScrollBars { get; set; } = new();

    public Separator Separators { get; set; } = new();

    public ToolBar ToolBars { get; set; } = new();

    public ToolTip ToolTips { get; set; } = new();

    public class Button
    {
        public Color FlatClick { get; set; }

        public Color Ripple { get; set; }

        public Color FlatRipple { get; set; }

    }

    public class SnackBar
    {
        public Color Ripple { get; set; }

        public Color Background { get; set; }

        public Color MouseOver { get; set; }

    }

    public class Card
    {
        public Color Background { get; set; }

    }

    public class CheckBox
    {
        public Color Disabled { get; set; }

        public Color Off { get; set; }

    }

    public class Chip
    {
        public Color Background { get; set; }

    }

    public class DataGrid
    {
        public Color ButtonPressed { get; set; }

        public Color ComboBoxSelected { get; set; }

        public Color RowHoverBackground { get; set; }

        public Color Selected { get; set; }

        public Color ColumnHeaderForeground { get; set; }

    }

    public class TextBox
    {
        public Color Border { get; set; }

        public Color OutlineBorder { get; set; }

        public Color DisabledBackground { get; set; }

        public Color FilledBackground { get; set; }

        public Color HoverBackground { get; set; }

        public Color OutlineInactiveBorder { get; set; }

    }

    public class GridSplitter
    {
        public Color Background { get; set; }

    }

    public class Header
    {
        public Color Foreground { get; set; }

    }

    public class ListView
    {
        public Color Selected { get; set; }

    }

    public class ScrollBar
    {
        public Color Foreground { get; set; }

        public Color RepeatButtonBackground { get; set; }

    }

    public class Separator
    {
        public Color Background { get; set; }

    }

    public class ToolBar
    {
        public Color Background { get; set; }

        public Item Items { get; set; } = new();

        public class Item
        {
            public Color Background { get; set; }

            public Color Foreground { get; set; }

        }

    }

    public class ToolTip
    {
        public Color Background { get; set; }

    }

}