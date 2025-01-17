﻿using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using static Echo.Process;

namespace Echo
{
    internal class ActorDispatchLocal : IActorDispatch
    {
        public readonly ILocalActorInbox Inbox;
        public readonly IActor Actor;
        public readonly Option<SessionId> SessionId;
        public readonly long ConversationId;

        public ActorDispatchLocal(ActorItem actor, Option<SessionId> sessionId)
        {
            SessionId      = sessionId;
            ConversationId = ActorContext.NextOrCurrentConversationId();
            Inbox          = actor.Inbox as ILocalActorInbox;
            if (Inbox == null) throw new ArgumentException("Invalid (not local) ActorItem passed to LocalActorDispatch.");
            Actor          = actor.Actor;
        }

        public IObservable<T> Observe<T>() =>
            from x in Actor.PublishStream
            where x is T
            select (T)x;

        public IObservable<T> ObserveState<T>() =>
            from x in Actor.StateStream
            where x is T
            select (T)x;

        public Either<string, bool> HasStateTypeOf<T>() =>
            Inbox.HasStateTypeOf<T>();

        public Either<string, bool> CanAccept<T>() =>
            Inbox.CanAcceptMessageType<T>();

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) =>
            LocalScheduler.Push(schedule, Actor.Id, m => Inbox.Tell(Inbox.ValidateMessageType(m, sender), sender, SessionId, ConversationId), message);

        public Unit TellSystem(SystemMessage message, ProcessId sender)
        {
            message.ConversationId = ConversationId;
            message.SessionId      = SessionId.Map(static x => x.Value).IfNoneUnsafe(message.SessionId);
            return Inbox.TellSystem(message);
        }

        public Unit TellUserControl(UserControlMessage message, ProcessId sender)
        {
            message.ConversationId = ConversationId;
            message.SessionId      = SessionId.Map(static x => x.Value).IfNoneUnsafe(message.SessionId);
            return Inbox.TellUserControl(message);
        }

        public Unit Ask(object message, ProcessId sender) =>
            Inbox.Ask(message, sender, SessionId, ConversationId);

        public Unit Kill() =>
            TellSystem(new ShutdownProcessMessage(false), ProcessId.NoSender);

        public Unit Shutdown() =>
            TellSystem(new ShutdownProcessMessage(false), ProcessId.NoSender);

        ValueTask<Unit> ShutdownProcess(bool maintainState) =>
            
            ActorContext.System(Actor.Id).WithContext(
                new ActorItem(
                    Actor,
                    (IActorInbox)Inbox,
                    Actor.Flags
                    ),
                Actor.Parent,
                ProcessId.NoSender,
                null,
                SystemMessage.ShutdownProcess(maintainState),
                None,
                ConversationId,
                () => Actor.Shutdown(maintainState)
            );

        public HashMap<string, ProcessId> GetChildren() =>
            Actor.Children.Map(a => a.Actor.Id);

        public Unit Publish(object message) =>
            Actor.Publish(message);

        public int GetInboxCount() =>
            Inbox.Count;

        public Unit Watch(ProcessId pid) =>
            Actor.AddWatcher(pid);

        public Unit UnWatch(ProcessId pid) =>
            Actor.RemoveWatcher(pid);

        public Unit DispatchWatch(ProcessId watching) =>
            Actor.DispatchWatch(watching);

        public Unit DispatchUnWatch(ProcessId watching) =>
            Actor.DispatchUnWatch(watching);

        public bool IsLocal => 
            true;

        public bool Exists =>
            true;

        public IEnumerable<Type> GetValidMessageTypes() =>
            Inbox.GetValidMessageTypes();

        public bool Ping() =>
            Exists;
    }
}
