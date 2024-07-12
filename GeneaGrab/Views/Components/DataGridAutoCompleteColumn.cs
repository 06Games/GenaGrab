using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.Threading;

namespace GeneaGrab.Views.Components;

public class DataGridAutoCompleteColumn : DataGridTextColumn
{
    public static readonly StyledProperty<Func<string?, CancellationToken, Task<IEnumerable<object>>>?> AsyncPopulatorProperty =
        AutoCompleteBox.AsyncPopulatorProperty.AddOwner<DataGridAutoCompleteColumn>();
    public Func<string?, CancellationToken, Task<IEnumerable<object>>>? AsyncPopulator
    {
        get => GetValue(AsyncPopulatorProperty)
               ?? (KeyedAsyncPopulator == null ? null : new Func<string?, CancellationToken, Task<IEnumerable<object>>>((s, token) => KeyedAsyncPopulator(Key, s).WithCancellation(token)));
        set => SetValue(AsyncPopulatorProperty, value);
    }

    public static readonly StyledProperty<string> KeyProperty = AvaloniaProperty.Register<DataGridAutoCompleteColumn, string>(nameof(Key));
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    public static readonly StyledProperty<Func<string, string?, Task<IEnumerable<object>>>?> KeyedAsyncPopulatorProperty =
        AvaloniaProperty.Register<DataGridAutoCompleteColumn, Func<string, string?, Task<IEnumerable<object>>>?>(nameof(KeyedAsyncPopulator));
    public Func<string, string?, Task<IEnumerable<object>>>? KeyedAsyncPopulator
    {
        get => GetValue(KeyedAsyncPopulatorProperty);
        set => SetValue(KeyedAsyncPopulatorProperty, value);
    }

    protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
    {
        var textBox = new AutoCompleteBox
        {
            Name = "CellTextBox",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FilterMode = AutoCompleteFilterMode.None,
            AsyncPopulator = AsyncPopulator
        };

        SyncProperties(textBox);
        return textBox;
    }

    protected override void CancelCellEdit(Control editingElement, object uneditedValue) => base.CancelCellEdit(GetTextBox(editingElement), uneditedValue);

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs) => base.PrepareCellForEdit(GetTextBox(editingElement), editingEventArgs);

    private static TextBox? GetTextBox(Control control)
    {
        return control switch
        {
            AutoCompleteBox autoCompleteBox => (TextBox?)autoCompleteBox.GetVisualDescendants().FirstOrDefault(x => x is TextBox),
            TextBox textBox => textBox,
            _ => null
        };
    }


    /// <summary>Copy of the parent private method <see cref="DataGridTextColumn.SyncProperties"/></summary>
    private void SyncProperties(AvaloniaObject content)
    {
        SyncColumnProperty(this, content, FontFamilyProperty);
        SyncColumnProperty(this, content, FontSizeProperty);
        SyncColumnProperty(this, content, FontStyleProperty);
        SyncColumnProperty(this, content, FontWeightProperty);
        SyncColumnProperty(this, content, ForegroundProperty);

        static void SyncColumnProperty<T>(AvaloniaObject column, AvaloniaObject content, AvaloniaProperty<T> contentProperty)
        {
            if (!column.IsSet(contentProperty)) content.ClearValue(contentProperty);
            else content.SetValue(contentProperty, column.GetValue(contentProperty));
        }
    }
}
