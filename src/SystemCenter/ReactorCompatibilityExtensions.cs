using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace WinUI3RustDemo;

internal static class ReactorCompatibilityExtensions
{
    // Microsoft.UI.Reactor 0.1.0-preview.11 does not expose the generic
    // HorizontalContentAlignment modifier available in newer source builds.
    // Alignment is cosmetic, so keep the call source-compatible without
    // reaching into a preview control wrapper's native instance.
    public static ButtonElement HorizontalContentAlignment(
        this ButtonElement element,
        HorizontalAlignment alignment)
        => element;
}
