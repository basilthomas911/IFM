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

    extension(IObjectRepository repository)
    {
        /// <summary>
        /// Creates a command-text context named after the calling test class and method.
        /// </summary>
        public IObjectRepositoryContext UseTest(
            string commandText,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            => repository.Use(CreateCommandName(memberName, sourceFilePath), commandText);

        /// <summary>
        /// Creates a command-text context named after the calling test class, method, and operation.
        /// </summary>
        public IObjectRepositoryContext UseTest(
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
        public IObjectUriContext UseTest(
            Uri uri,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "")
            => repository.Use(CreateCommandName(memberName, sourceFilePath), uri);
    }

    static string CreateCommandName(string memberName, string sourceFilePath)
        => $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}";
}
