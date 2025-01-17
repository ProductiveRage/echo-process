﻿using LanguageExt;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process registration - a kind of DNS for Processes
    /// </para>
    /// <para>
    ///     If the Process is visible to the cluster (PersistInbox) then the 
    ///     registration becomes a permanent named look-up until Process.deregister 
    ///     is called.
    /// </para>
    /// <para>
    ///     Multiple Processes can register under the same name.  You may use 
    ///     a dispatcher to work on them collectively (wherever they are in the 
    ///     cluster).  i.e. 
    /// </para>
    /// <para>
    ///         var regd = register("regd-name", pid);
    /// </para>
    /// <para>
    ///         tell(Dispatch.Broadcast[regd],  "Hello");
    ///         tell(Dispatch.First[regd],      "Hello");
    ///         tell(Dispatch.LeastBusy[regd],  "Hello");
    ///         tell(Dispatch.Random[regd],     "Hello");
    ///         tell(Dispatch.RoundRobin[regd], "Hello");
    /// </para>
    /// </summary>
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Find a process by its *registered* name (a kind of DNS for Processes).
        /// 
        /// Names are registered in roles.  This function will find registered 
        /// processes in the current role only.  Use the 'find' variant to find 
        /// registered processes in other roles.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Multiple Processes can register under the same name.  You may use 
        /// a dispatcher to work on them collectively (wherever they are in the 
        /// cluster).  i.e. 
        /// 
        ///     var regd = register("proc", pid);
        ///     tell(Dispatch.Broadcast[regd], "Hello");
        ///     tell(Dispatch.First[regd], "Hello");
        ///     tell(Dispatch.LeastBusy[regd], "Hello");
        ///     tell(Dispatch.Random[regd], "Hello");
        ///     tell(Dispatch.RoundRobin[regd], "Hello");
        ///     
        /// </remarks>
        /// <param name="name">Process name</param>
        /// <returns>A ProcessId that allows dispatching to the process(es).  The result
        /// would look like /disp/reg/name</returns>
        public static Eff<RT, ProcessId> find(ProcessName name) =>
            Eff(() => Process.find(name));

        /// <summary>
        /// Find a process by its *registered* name (a kind of DNS for Processes) in the
        /// role specified.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Multiple Processes can register under the same name.  You may use 
        /// a dispatcher to work on them collectively (wherever they are in the 
        /// cluster).  i.e. 
        /// 
        ///     var regd = register("proc", pid);
        ///     tell(Dispatch.Broadcast[regd], "Hello");
        ///     tell(Dispatch.First[regd], "Hello");
        ///     tell(Dispatch.LeastBusy[regd], "Hello");
        ///     tell(Dispatch.Random[regd], "Hello");
        ///     tell(Dispatch.RoundRobin[regd], "Hello");
        /// 
        /// </remarks>
        /// <param name="role">Process role</param>
        /// <param name="name">Process name</param>
        /// <returns>A ProcessId that allows dispatching to the process(es).  The result
        /// would look like /disp/reg/name</returns>
        public static Eff<RT, ProcessId> find(ProcessName role, ProcessName name) =>
            Eff(() => Process.find(role, name));

        /// <summary>
        /// Register a named process (a kind of DNS for Processes).  
        /// 
        /// If the Process is visible to the cluster (PersistInbox) then the 
        /// registration becomes a permanent named look-up until Process.deregister 
        /// is called.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Multiple Processes can register under the same name.  You may use 
        /// a dispatcher to work on them collectively (wherever they are in the 
        /// cluster).  i.e. 
        /// 
        ///     var regd = register("proc");
        ///     tell(Dispatch.Broadcast[regd], "Hello");
        ///     tell(Dispatch.First[regd], "Hello");
        ///     tell(Dispatch.LeastBusy[regd], "Hello");
        ///     tell(Dispatch.Random[regd], "Hello");
        ///     tell(Dispatch.RoundRobin[regd], "Hello");
        /// 
        ///     This should be used from within a process' message loop only
        /// </remarks>
        /// <param name="name">Name to register under</param>
        /// <returns>A ProcessId that allows dispatching to the process via the name.  The result
        /// would look like /disp/reg/name</returns>
        public static Aff<RT, ProcessId> register(ProcessName name) =>
            CurrentSystem.Map(sn => Process.register(name, sn));

        /// <summary>
        /// Register a named process (a kind of DNS for Processes).  
        /// 
        /// If the Process is visible to the cluster (PersistInbox) then the 
        /// registration becomes a permanent named look-up until Process.deregister 
        /// is called.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Multiple Processes can register under the same name.  You may use 
        /// a dispatcher to work on them collectively (wherever they are in the 
        /// cluster).  i.e. 
        /// 
        ///     var regd = register("proc", pid);
        ///     tell(Dispatch.Broadcast[regd], "Hello");
        ///     tell(Dispatch.First[regd], "Hello");
        ///     tell(Dispatch.LeastBusy[regd], "Hello");
        ///     tell(Dispatch.Random[regd], "Hello");
        ///     tell(Dispatch.RoundRobin[regd], "Hello");
        /// 
        /// </remarks>
        /// <param name="name">Name to register under</param>
        /// <param name="process">Process to be registered</param>
        /// <returns>A ProcessId that allows dispatching to the process(es).  The result
        /// would look like /disp/reg/name</returns>
        public static Aff<RT, ProcessId> register(ProcessName name, ProcessId process) =>
            Eff(() => Process.register(name, process));

        /// <summary>
        /// Deregister a Process from any names it's been registered as.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Any Process (or dispatcher, or role, etc.) can be registered by a name - 
        /// a kind of DNS for ProcessIds.  There can be multiple names associated
        /// with a single ProcessId.  
        /// 
        /// This function removes all registered names for a specific ProcessId.
        /// If you wish to deregister all ProcessIds registered under a name then
        /// use Process.deregisterByName(name)
        /// </remarks>
        /// <param name="process">Process to be deregistered</param>
        public static Aff<RT, Unit> deregisterById(ProcessId process) =>
            Eff(() => Process.deregisterById(process));

        /// <summary>
        /// Deregister all Processes associated with a name. NOTE: Be very careful
        /// with usage of this function if you didn't handle the registration you
        /// are potentially disconnecting many Processes from their registered name.
        /// 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// Any Process (or dispatcher, or role, etc.) can be registered by a name - 
        /// a kind of DNS for ProcessIds.  There can be multiple names associated
        /// with a single ProcessId and multiple ProcessIds associated with a name.
        /// 
        /// This function removes all registered ProcessIds for a specific name.
        /// If you wish to deregister all names registered for specific Process then
        /// use Process.deregisterById(pid)
        /// </remarks>
        /// <param name="name">Name of the process to deregister</param>
        public static Aff<RT, Unit> deregisterByName(ProcessName name) =>
            CurrentSystem.Map(sn => Process.deregisterByName(name, sn));
    }
}
