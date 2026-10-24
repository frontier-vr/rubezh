using Rubezh.Core;
using UnityEngine;

namespace Rubezh.Gameplay
{
    // Время игры для Core. В тестах подменяется своим IClock.
    public sealed class UnityClock : IClock
    {
        public double Now => Time.timeAsDouble;
    }
}
