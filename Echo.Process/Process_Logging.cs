﻿using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using LanguageExt;

namespace Echo
{
    public static partial class Process
    {
#if DEBUG
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static Unit logInfo(object message)
        {
            Debug.WriteLine(new ProcessLogItem(ProcessLogItemType.Info, (message ?? "").ToString()));
            return default;
        }
#else
        /// <summary>
        /// Log info - Internal 
        /// </summary>
        internal static Unit logInfo(object message)
        {
            return default;
        }
#endif 

        private static Unit IfNotNull<T>(T value, Action<T> action)
            where T : class
        {
            if (value != null) action(value);
            return default;
        }

        /// <summary>
        /// Log warning - Internal 
        /// </summary>
        public static Unit logWarn(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Warning, (message ?? "").ToString())));

        /// <summary>
        /// Log system error - Internal 
        /// </summary>
        internal static Unit logSysErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (message ?? "").ToString())));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static Unit logSysErr(Exception ex) =>
            IfNotNull(ex, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, ex)));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        internal static Unit logSysErr(string message, Exception ex) =>
            IfNotNull(message, _ => IfNotNull(ex, __ => log.OnNext(new ProcessLogItem(ProcessLogItemType.SysError, (message ?? "").ToString(), ex))));

        /// <summary>
        /// Log user error - Internal 
        /// </summary>
        public static Unit logUserErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.UserError, (message ?? "").ToString())));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Unit logErr(Exception ex) =>
            IfNotNull(ex, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, ex)));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Unit logErr(string message, Exception ex) =>
            IfNotNull(message, _ => IfNotNull(ex, __ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (message ?? "").ToString(), ex))));

        /// <summary>
        /// Log user or system error - Internal 
        /// </summary>
        public static Unit logErr(string message) =>
            IfNotNull(message, _ => log.OnNext(new ProcessLogItem(ProcessLogItemType.Error, (message ?? "").ToString())));

        /// <summary>
        /// Log subject - Internal
        /// </summary>
        private static readonly Subject<ProcessLogItem> log = new Subject<ProcessLogItem>();
    }
}
