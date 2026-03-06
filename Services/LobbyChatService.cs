using System;
using System.Collections.Generic;
using System.Linq;

namespace Drafts.Services;

public sealed class LobbyChatService
{
    private readonly object _lock = new();
    private readonly List<ChatMessage> _messages = new();

    public event Action? ChatUpdated;

    public sealed record ChatMessage(DateTime Utc, int SenderUserId, string SenderName, string Text);

    public IReadOnlyList<ChatMessage> GetMessages()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    public bool AddMessage(int senderUserId, string senderName, string text)
    {
        if (senderUserId < 0) return false;

        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_lock)
        {
            _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, text));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
        return true;
    }

    public void AddSystemMessage(string text)
    {
        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        lock (_lock)
        {
            _messages.Add(new ChatMessage(DateTime.UtcNow, 0, "System", text));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }

        ChatUpdated?.Invoke();
    }

    private void TrimIfNeeded()
    {
        const int max = 200;
        if (_messages.Count <= max) return;
        _messages.RemoveRange(0, _messages.Count - max);
    }
}
