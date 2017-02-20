﻿using LanguageExt;
using Newtonsoft.Json;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    public enum ProcessLogItemType
    {
        Info       = 1,
        Warning    = 2,
        UserError  = 4,
        SysError   = 8,
        Error      = 12,
        Debug      = 16
    }

    public class ProcessLogItem
    {
        public readonly DateTime When;
        public readonly ProcessLogItemType Type;
        public readonly Option<string> Message;
        public readonly Option<Exception> Exception;

        public ProcessLogItem(ProcessLogItemType type, string message, Exception exception)
        {
            When = DateTime.UtcNow;
            Type = type;
            Message = message;
            Exception = exception;
        }

        [JsonConstructor]
        public ProcessLogItem(ProcessLogItemType type, string message)
            :
            this(type, message, null)
        {
        }

        public ProcessLogItem(ProcessLogItemType type, Exception exception)
            :
            this(type, null, exception)
        {
        }

        public string TypeDisplay =>
            Type == ProcessLogItemType.Info      ? "Info "
          : Type == ProcessLogItemType.Warning   ? "Warn "
          : Type == ProcessLogItemType.Debug     ? "Debug"
          : Type == ProcessLogItemType.SysError  ? "Error"
          : Type == ProcessLogItemType.UserError ? "Error"
          : Type == ProcessLogItemType.Error     ? "Error"
          : "     ";

        public string DateDisplay =>
            When.Date == DateTime.UtcNow.Date
                ? When.ToLocalTime().ToString("HH:mm.ss.fff")
                : When.ToLocalTime().ToString("dd/MM/yy HH:mm.ss");

        public override string ToString() =>
            Message.Match(
                Some: msg =>
                    Exception.Match(
                        Some: ex => $"{DateDisplay} {TypeDisplay} {msg}\n{ex.Message}\n\n{ex.StackTrace}",
                        None: () => $"{DateDisplay} {TypeDisplay} {msg}"),
                None: () =>
                    Exception.Match(
                        Some: ex => $"{DateDisplay} {TypeDisplay}\n{ex.Message}\n\n{ex.StackTrace}",
                        None: () => $"{DateDisplay} {TypeDisplay}"));
    }
}