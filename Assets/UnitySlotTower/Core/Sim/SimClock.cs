using System;

namespace UnitySlotTower.Core.Sim
{
    /// <summary>1/60 秒固定ステップのシミュレーション時計。一時停止中は進まない（FR-012）。</summary>
    public sealed class SimClock
    {
        public const int StepsPerSecond = 60;
        public const float StepSeconds = 1f / StepsPerSecond;
        public const int MaxStepsPerAdvance = 10;

        private double accumulator;

        public long Step { get; private set; }
        public bool Paused { get; set; }
        public float ElapsedSeconds => Step * StepSeconds;

        /// <summary>実時間 deltaSeconds を与え、進めるべき固定ステップごとに onStep を呼ぶ。進めたステップ数を返す。</summary>
        public int Advance(float deltaSeconds, Action onStep)
        {
            if (Paused) return 0;
            accumulator += deltaSeconds;
            var steps = 0;
            while (accumulator >= StepSeconds && steps < MaxStepsPerAdvance)
            {
                accumulator -= StepSeconds;
                Step++;
                steps++;
                onStep?.Invoke();
            }
            if (steps == MaxStepsPerAdvance) accumulator = 0;
            return steps;
        }
    }
}
