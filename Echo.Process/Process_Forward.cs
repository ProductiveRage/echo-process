﻿using System;
using System.Linq;
using LanguageExt;
using System.Reactive.Linq;
using static LanguageExt.Map;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process:  Forward functions
    /// </para>
    /// <para>
    ///     'fwd' is used to forward a message onto another process whilst maintaining the original 
    ///     sender context (for 'ask' responses to go back to the right place).
    /// </para>
    /// </summary>
    public static partial class Process
    {
        /// <summary>
        /// Forward a message
        /// </summary>
        /// <param name="pid">Process ID to send to</param>
        /// <param name="message">Message to send</param>
        public static Unit fwd<T>(ProcessId pid, T message)
        {
            try
            {
                return ActorContext.Request.CurrentRequest == null
                           ? tell(pid, message, Sender)
                           : tell(pid,
                                  new ActorRequest(
                                      message,
                                      pid,
                                      ActorContext.Request.CurrentRequest.ReplyTo,
                                      ActorContext.Request.CurrentRequest.RequestId),
                                  Sender);
            }
            catch (Exception e)
            {
                return dead(message, e);
            }
        }

        /// <summary>
        /// Forward a message
        /// </summary>
        /// <param name="pid">Process ID to send to</param>
        public static Unit fwd(ProcessId pid)
        {
            try
            {
                return tell(pid, ActorContext.Request.CurrentRequest ?? ActorContext.Request.CurrentMsg, Sender);
            }
            catch (Exception e)
            {
                return dead(ActorContext.Request.CurrentRequest ?? ActorContext.Request.CurrentMsg, e);
            }
        }

        /// <summary>
        /// Forward a message to a named child process
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="name">Name of the child process</param>
        public static Unit fwdChild<T>(ProcessName name, T message) =>
            fwd(Self.Child(name), message);

        /// <summary>
        /// Forward a message to a child process (found by index)
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="message">Message to send</param>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Unit fwdChild<T>(int index, T message) =>
            fwd(child(index), message);

        /// <summary>
        /// Forward a message to a named child process
        /// </summary>
        /// <param name="name">Name of the child process</param>
        public static Unit fwdChild(ProcessName name) =>
             fwd(Self.Child(name));

        /// <summary>
        /// Forward a message to a child process (found by index)
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Unit fwdChild(int index) =>
            fwd(child(index));
    }
}
