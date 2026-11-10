using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

internal static class BarLayout
{
    public static StackElement Along(Orientation orientation, double spacing, params Element?[] children) =>
        orientation == Orientation.Vertical ? VStack(spacing, children) : HStack(spacing, children);

    public static string Key(object value) =>
        RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
}
