using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quark.Xaml.Messengers;

public abstract class MessageBase
{
    /// <summary>
    /// メッセージ名
    /// </summary>
    public string? MessageName { get; set; }
}
