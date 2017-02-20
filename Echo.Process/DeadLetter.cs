﻿using LanguageExt;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// Dead letter message
    /// </summary>
    public class DeadLetter
    {
        private DeadLetter(ProcessId sender, ProcessId recipient, Exception ex, string reason, object message)
        {
            Sender = sender;
            Recipient = recipient;
            Exception = Optional(ex);
            Reason = Optional(reason);
            Message = Optional(message);
        }

        /// <summary>
        /// Create a new dead letter
        /// </summary>
        public static DeadLetter create(ProcessId sender, ProcessId recipient, Exception ex, string reason, object message) =>
            new DeadLetter(sender, recipient, ex, reason, message);

        /// <summary>
        /// Create a new dead letter
        /// </summary>
        public static DeadLetter create(ProcessId sender, ProcessId recipient, string reason, object message) =>
            new DeadLetter(sender, recipient, null, reason, message);

        /// <summary>
        /// Create a new dead letter
        /// </summary>
        public static DeadLetter create(ProcessId sender, ProcessId recipient, Exception ex, object message) =>
            new DeadLetter(sender, recipient, ex, null, message);

        /// <summary>
        /// Create a new dead letter
        /// </summary>
        public static DeadLetter create(ProcessId sender, ProcessId recipient, object message) =>
            new DeadLetter(sender, recipient, null, null, message);

        /// <summary>
        /// Sender of the letter that ended up 'dead'
        /// </summary>
        public readonly ProcessId Sender;

        /// <summary>
        /// Intended recipient of the message that ended up 'dead'
        /// </summary>
        public readonly ProcessId Recipient;

        /// <summary>
        /// Any exception that was thrown the cause the letter to die
        /// </summary>
        public readonly Option<Exception> Exception;

        /// <summary>
        /// An optional reason why the letter died
        /// </summary>
        public readonly Option<string> Reason;

        /// <summary>
        /// The content of the dead letter
        /// </summary>
        public Option<object> Message;

        /// <summary>
        /// Summary of the message content
        /// </summary>
        public string ContentDisplay =>
            Message.Match(
                Some: objmsg => map(objmsg.ToString(), msg =>
                                    msg.Length > 100
                                        ? msg.Substring(0, 100) + "..."
                                        : msg),
                None: ()     => "[null]"
            );

        /// <summary>
        /// Friendly type display
        /// </summary>
        public string ContentTypeDisplay =>
            Message.Match(
                Some: x => x.GetType().Name,
                None: () => "[null]"
            );

        private static string ProcessFmt(ProcessId pid) =>
            pid.IsValid
                ? pid.ToString()
                : "no-sender";


        /// <summary>
        /// Get a string representation of the dead letter
        /// </summary>
        public override string ToString() =>
            Exception.Match(
                Some: ex =>
                    Reason.Match(
                        Some: reason => $"Dead letter from: {ProcessFmt(Sender)} to: {Recipient}, failed because: {reason} {ex.Message}. Type: {ContentTypeDisplay} Content: {ContentDisplay}",
                        None: ()     => $"Dead letter from: {ProcessFmt(Sender)} to: {Recipient}, failed because: {ex.Message}. Type: {ContentTypeDisplay} Content: {ContentDisplay}"
                    ),
                None: () =>
                    Reason.Match(
                        Some: reason => $"Dead letter from: {ProcessFmt(Sender)} to: {Recipient}, failed because: {reason}.  Type: {ContentTypeDisplay} Content: {ContentDisplay}",
                        None: ()     => $"Dead letter from: {ProcessFmt(Sender)} to: {Recipient}.  Type: {ContentTypeDisplay} Content: {ContentDisplay}"
                    ));
    }
}
