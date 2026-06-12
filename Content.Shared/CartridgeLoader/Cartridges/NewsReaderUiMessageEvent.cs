using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class NewsReaderUiMessageEvent : CartridgeMessageEvent
{
    public readonly NewsReaderUiAction Action;

    public NewsReaderUiMessageEvent(NewsReaderUiAction action)
    {
        Action = action;
    }
}

[Serializable, NetSerializable]
public sealed class NewsReaderPostCommentMessage : CartridgeMessageEvent
{
    public readonly string CommentText;

    public NewsReaderPostCommentMessage(string commentText)
    {
        CommentText = commentText;
    }
}

[Serializable, NetSerializable]
public enum NewsReaderUiAction
{
    Next,
    Prev,
    NotificationSwitch
}
