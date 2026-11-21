using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Session
{
    /// <summary>
    /// Synchronous typed event bus owned by one <see cref="GameSession"/>. No static state: a new session
    /// starts with no subscribers (R-STATIC). Handlers run in subscription order; an exception in one handler
    /// is collected and does not stop the others or corrupt the publisher.
    /// </summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly List<Exception> _handlerErrors = new List<Exception>();
        private readonly List<IEntityVisualSink> _visualSinks = new List<IEntityVisualSink>();

        public IReadOnlyList<Exception> HandlerErrors => _handlerErrors;

        public long PublishedCount { get; private set; }

        public void Subscribe<T>(Action<T> handler)
            where T : IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_handlers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                list = new List<Delegate>();
                _handlers.Add(typeof(T), list);
            }

            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
            where T : IGameEvent
        {
            if (handler != null && _handlers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        public void AddVisualSink(IEntityVisualSink sink)
        {
            if (sink != null && !_visualSinks.Contains(sink))
            {
                _visualSinks.Add(sink);
            }
        }

        public void RemoveVisualSink(IEntityVisualSink sink)
        {
            _visualSinks.Remove(sink);
        }

        public IReadOnlyList<IEntityVisualSink> VisualSinks => _visualSinks;

        public void Publish<T>(T gameEvent)
            where T : IGameEvent
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }

            PublishedCount++;
            if (_handlers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                // Copy: handlers may unsubscribe while being notified.
                Delegate[] snapshot = list.ToArray();
                foreach (Delegate handler in snapshot)
                {
                    try
                    {
                        ((Action<T>)handler)(gameEvent);
                    }
                    catch (Exception exception)
                    {
                        _handlerErrors.Add(exception);
                    }
                }
            }

            foreach (IEntityVisualSink sink in _visualSinks.ToArray())
            {
                try
                {
                    Dispatch(sink, gameEvent);
                }
                catch (Exception exception)
                {
                    _handlerErrors.Add(exception);
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
            _visualSinks.Clear();
            _handlerErrors.Clear();
        }

        private static void Dispatch(IEntityVisualSink sink, IGameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case EntitySpawned e:
                    sink.OnEntitySpawned(e);
                    break;
                case EntityDespawned e:
                    sink.OnEntityDespawned(e);
                    break;
                case EntityMoved e:
                    sink.OnEntityMoved(e);
                    break;
                case EquipmentVisualChanged e:
                    sink.OnEquipmentChanged(e);
                    break;
                case ActionStarted e:
                    sink.OnActionStarted(e);
                    break;
                case ActionPhaseChanged e:
                    sink.OnActionPhaseChanged(e);
                    break;
                case ActionResolved e:
                    sink.OnActionResolved(e);
                    break;
                case HitResolved e:
                    sink.OnHitResolved(e);
                    break;
                case EntityDied e:
                    sink.OnEntityDied(e);
                    break;
            }
        }
    }
}
