using System;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : Singleton<EventManager>
{
    // --- Parameterless events ---
    private readonly Dictionary<string, Action> _events = new();

    public void On(string eventName, Action listener)
    {
        if (_events.TryGetValue(eventName, out Action existing))
            _events[eventName] = existing + listener;
        else
            _events[eventName] = listener;
    }

    public void Off(string eventName, Action listener)
    {
        if (_events.TryGetValue(eventName, out Action existing))
            _events[eventName] = existing - listener;
    }

    public void Emit(string eventName)
    {
        if (_events.TryGetValue(eventName, out Action action))
            action?.Invoke();
        else
            Debug.LogWarning($"[EventManager] No subscribers for event: '{eventName}'");
    }

    // --- Typed events (single argument) ---
    private readonly Dictionary<string, Delegate> _typedEvents = new();

    public void On<T>(string eventName, Action<T> listener)
    {
        if (_typedEvents.TryGetValue(eventName, out Delegate existing))
            _typedEvents[eventName] = Delegate.Combine(existing, listener);
        else
            _typedEvents[eventName] = listener;
    }

    public void Off<T>(string eventName, Action<T> listener)
    {
        if (_typedEvents.TryGetValue(eventName, out Delegate existing))
            _typedEvents[eventName] = Delegate.Remove(existing, listener);
    }

    public void Emit<T>(string eventName, T arg)
    {
        if (_typedEvents.TryGetValue(eventName, out Delegate del))
            (del as Action<T>)?.Invoke(arg);
        else
            Debug.LogWarning($"[EventManager] No typed subscribers for event: '{eventName}'");
    }
}
