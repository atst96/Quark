using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Quark.ViewModels;

public partial class ValueSelectorViewModel<T> : ObservableObject
    where T : notnull
{
    private T _selectedValue;

    /// <summary>選択中の値</summary>
    public T SelectedValue
    {
        get => _selectedValue;
        set
        {
            if (this.SetProperty(ref this._selectedValue, value))
                this.SetProperty(ref this._selectedIndex, this.GetIndex(value), nameof(this.SelectedIndex));
        }
    }


    private int _selectedIndex = -1;

    public int SelectedIndex
    {
        get => this._selectedIndex;
        set
        {
            if (this.SetProperty(ref this._selectedIndex, value) && value > 0)
                this.SetProperty(ref this._selectedValue, this._valuePairs[value].Key, nameof(this.SelectedValue));
        }
    }

    private KeyValuePair<T, string>[] _valuePairs;

    private FrozenDictionary<T, string> _dict;

    public IList<string> Texts { get; }

    public ValueSelectorViewModel(T value, KeyValuePair<T, string>[] valuePairs)
    {
        this._selectedValue = value;
        this._valuePairs = valuePairs;
        this._selectedIndex = this.GetIndex(value);
        this._dict = valuePairs.ToFrozenDictionary();
        this.Texts = valuePairs.Select(i => i.Value).ToArray();
    }

    protected void OnSelectedIndexChanged(int index)
    {
        this.SelectedValue = this._valuePairs[index].Key;
    }

    private int GetIndex(T value)
    {
        // HACK: もう少し良い実装に直す
        var values = this._valuePairs;

        for (int i = 0; i < values.Length; ++i)
        {
            if (Equals(values[i], value))
                return i;
        }

        return -1;
    }
}
