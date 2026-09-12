// net48's BCL has no IsExternalInit; the positional record in
// CategoryMap.cs (init-only setters) needs the type to exist at
// compile time. Kept out of RevitCompat.cs because a block-scoped
// namespace cannot share a file with a file-scoped one.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
