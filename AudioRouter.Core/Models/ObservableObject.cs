using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioRouter.Core.Models;

/// <summary>
/// 极简 INPC 基类。核心层保留它是有意的：
/// 未来 GUI（WPF / Avalonia）直接绑定核心模型时，不需要再包一层。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public void RaiseAllPropertiesChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
