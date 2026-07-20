using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public enum CommandErrorType
    {
        Unknown,
        CommandException,
        ValidationException,
        SystemException
    }

    public static class CommandErrorTypeExtensions
    {
        public static string ToStringFast(this CommandErrorType value) => value switch
        {
            CommandErrorType.Unknown => nameof(CommandErrorType.Unknown),
            CommandErrorType.CommandException => nameof(CommandErrorType.CommandException),
            CommandErrorType.ValidationException => nameof(CommandErrorType.ValidationException),
            CommandErrorType.SystemException => nameof(CommandErrorType.SystemException),
            _ => value.ToString()
        };
    }
}
