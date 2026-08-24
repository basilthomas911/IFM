using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Provides test-only repository helpers that derive globally identifiable command names
/// from the source test class and method.
/// </summary>
public static class ObjectRepositoryTestExtensions
{
    /// <summary>
    /// Creates a command-text context named after the calling test class and method.
    /// </summary>
    public static IObjectRepositoryContext UseTest(
        this IObjectRepository repository,
        string commandText,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "")
        => repository.Use(CreateCommandName(memberName, sourceFilePath), commandText);

    /// <summary>
    /// Creates a command-text context named after the calling test class, method, and operation.
    /// </summary>
    public static IObjectRepositoryContext UseTest(
        this IObjectRepository repository,
        string operationName,
        string commandText,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "")
        => repository.Use(
            $"{CreateCommandName(memberName, sourceFilePath)}.{operationName}",
            commandText);

    /// <summary>
    /// Creates a URI context named after the calling test class and method.
    /// </summary>
    public static IObjectUriContext UseTest(
        this IObjectRepository repository,
        Uri uri,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "")
        => repository.Use(CreateCommandName(memberName, sourceFilePath), uri);

    static string CreateCommandName(string memberName, string sourceFilePath)
        => $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}";
}
