﻿using LanguageExt;
using Newtonsoft.Json;
using System;

using static LanguageExt.Prelude;

namespace Echo
{
    public abstract class UserControlMessage : Message
    {
        public override Type MessageType => Type.UserControl;
        public readonly static UserControlMessage GetChildren = new GetChildrenMessage();
        public readonly static UserControlMessage Null = new UserControlNullMessage();

        public override string ToString() => $"{MessageType} {Tag}";
    }

    class UserControlNullMessage : UserControlMessage
    {
        public override TagSpec Tag => TagSpec.Null;
    }

    class GetChildrenMessage : UserControlMessage
    {
        public override TagSpec Tag => TagSpec.GetChildren;
        public override string ToString() => "GetChildren";
    }

    public class UserMessage : UserControlMessage
    {
        public override Type MessageType => Type.User;
        public override TagSpec Tag      => TagSpec.User;

        public UserMessage(object message, ProcessId sender, ProcessId replyTo)
        {
            Content = message;
            Sender = sender;
            ReplyTo = replyTo;
        }

        public ProcessId Sender { get; }
        public ProcessId ReplyTo { get; }
        public object Content { get; internal set; }

        public UserMessage SetSystem(SystemName sys) =>
            new UserMessage(Content, Sender.SetSystem(sys), ReplyTo.SetSystem(sys))
            {
                ConversationId = ConversationId,
                SessionId = SessionId
            };

        public override string ToString() => $"UserMessage: {Content}";
    }

    public class TerminatedMessage : UserControlMessage
    {
        public override Type MessageType => Type.UserControl;
        public override TagSpec Tag      => TagSpec.UserTerminated;

        public ProcessId Id;

        [JsonConstructor]
        public TerminatedMessage(ProcessId id)
        {
            Id = id;
        }

        public TerminatedMessage SetSystem(SystemName sys) =>
            new TerminatedMessage(Id.SetSystem(sys));
    }

    public class ClientMessageDTO
    {
        public string connectionId;
        public string tag;
        public string type;
        public string contentType;
        public object content;
        public string to;
        public string replyTo;
        public string sender;
        public string sessionId;
        public long conversationId;
        public long requestId;
    }

    public class RemoteMessageDTO
    {
        public int Type;
        public int Tag;
        public string Exception;
        public string Child;
        public string To;
        public string Sender;
        public string ReplyTo;
        public long RequestId;
        public string ContentType;
        public string Content;
        public Guid MessageId;
        public string SessionId;
        public long ConversationId;
        public long Due;

        internal static RemoteMessageDTO Create(object message, ProcessId to, ProcessId sender, Message.Type type, Message.TagSpec tag, Option<SessionId> sessionId, long conversationId, long due) =>
            map(message as ActorRequest, req =>
                req == null
                    ? map(message as ActorResponse, res =>
                        res == null
                            ? CreateMessage(message, to, sender, type, tag, sessionId, conversationId, due)
                            : CreateResponse(res, to, sender, sessionId, conversationId))
                    : CreateRequest(req, to, sender, sessionId, conversationId));

        internal static RemoteMessageDTO CreateMessage(object message, ProcessId to, ProcessId sender, Message.Type type, Message.TagSpec tag, Option<SessionId> sessionId, long conversationId, long due) =>
            new RemoteMessageDTO
            {
                Type        = (int) type,
                Tag         = (int) tag,
                To          = to.ToString(),
                RequestId   = -1,
                MessageId   = Guid.NewGuid(),
                Sender      = sender.ToString(),
                ReplyTo     = sender.ToString(),
                ContentType = message?.GetType()?.AssemblyQualifiedName,
                Content = message == null
                              ? null
                              : JsonConvert.SerializeObject(message, ActorSystemConfig.Default.JsonSerializerSettings),
                SessionId      = sessionId.Map(s => s.Value).IfNoneUnsafe(() => null),
                ConversationId = conversationId,
                Due            = due
            };

        internal static RemoteMessageDTO CreateRequest(ActorRequest req, ProcessId to, ProcessId sender, Option<SessionId> sessionId, long conversationId) =>
            new RemoteMessageDTO
            {
                Type           = (int)Message.Type.User,
                Tag            = (int)Message.TagSpec.UserAsk,
                Child          = null,
                Exception      = null,
                To             = to.ToString(),
                RequestId      = req.RequestId,
                MessageId      = Guid.NewGuid(),
                Sender         = sender.ToString(),
                ReplyTo        = req.ReplyTo.ToString(),
                ContentType    = req.Message.GetType().AssemblyQualifiedName,
                Content        = JsonConvert.SerializeObject(req.Message, ActorSystemConfig.Default.JsonSerializerSettings),
                ConversationId = conversationId,
                SessionId      = sessionId.Map(s => s.Value).IfNoneUnsafe(() => null)
            };

        internal static RemoteMessageDTO CreateResponse(ActorResponse res, ProcessId to, ProcessId sender, Option<SessionId> sessionId, long conversationId) =>
            new RemoteMessageDTO
            {
                Type           = (int)Message.Type.User,
                Tag            = (int)Message.TagSpec.UserReply,
                Child          = null,
                Exception      = res.IsFaulted
                                  ? "RESPERR"
                                  : null,
                To             = to.ToString(),
                RequestId      = res.RequestId,
                MessageId      = Guid.NewGuid(),
                Sender         = res.ReplyFrom.ToString(),
                ReplyTo        = res.ReplyTo.ToString(),
                ContentType    = res.Message.GetType().AssemblyQualifiedName,
                Content        = JsonConvert.SerializeObject(res.Message, ActorSystemConfig.Default.JsonSerializerSettings),
                ConversationId = conversationId,
                SessionId      = sessionId.Map(s => s.Value).IfNoneUnsafe(() => null)
            };
    }
}
