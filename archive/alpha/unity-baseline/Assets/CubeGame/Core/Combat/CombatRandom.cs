using System;

namespace CubeGame.Core.Combat
{
    /// <summary>
    /// Источник случайных чисел для боевых расчётов.
    ///
    /// Core не использует <c>UnityEngine.Random</c> и любое глобальное состояние: вызывающий код
    /// передаёт реализацию явно. Это делает бой воспроизводимым и позволяет детерминированно
    /// проверять границы и статистику.
    /// </summary>
    public interface ICombatRandom
    {
        /// <summary>Бросок в диапазоне 0…32767 (Alpha: <c>rand()</c> в бою).</summary>
        int NextRoll();
    }

    /// <summary>
    /// Детерминированный источник: xorshift32, приведённый к диапазону 0…32767.
    /// Один и тот же seed всегда даёт одну и ту же последовательность бросков.
    /// </summary>
    public sealed class SeededCombatRandom : ICombatRandom
    {
        private uint _state;

        public SeededCombatRandom(int seed)
        {
            // Ноль — вырожденное состояние xorshift, поэтому заменяется константой.
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public int NextRoll()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;

            return (int)(_state >> 16) & 0x7FFF;
        }
    }

    /// <summary>
    /// Источник с заранее заданной последовательностью бросков — только для тестов.
    /// Исчерпание последовательности является ошибкой теста, а не «случайным» поведением.
    /// </summary>
    public sealed class FixedCombatRandom : ICombatRandom
    {
        private readonly int[] _rolls;
        private int _index;

        public FixedCombatRandom(params int[] rolls)
        {
            _rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));
        }

        public int RemainingRolls => _rolls.Length - _index;

        public int NextRoll()
        {
            if (_index >= _rolls.Length)
            {
                throw new InvalidOperationException("Последовательность бросков исчерпана.");
            }

            return _rolls[_index++];
        }
    }
}
