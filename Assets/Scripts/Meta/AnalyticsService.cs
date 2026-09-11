using System;
using System.Collections.Generic;
using MergeWater.Core;
using UnityEngine;

namespace MergeWater.Meta
{
    public readonly struct AnalyticsRecord
    {
        public readonly string Name;
        public readonly IReadOnlyList<AnalyticsParam> Parameters;
        public readonly DateTime At;

        public AnalyticsRecord(string name, IReadOnlyList<AnalyticsParam> parameters, DateTime at)
        {
            Name = name;
            Parameters = parameters;
            At = at;
        }
    }

    public interface IAnalyticsSink
    {
        void Write(string eventName, IReadOnlyList<AnalyticsParam> parameters, DateTime at);
    }

    /// <summary>测试与本地校验用：把事件留在内存里。</summary>
    public sealed class InMemoryAnalyticsSink : IAnalyticsSink
    {
        private readonly List<AnalyticsRecord> _records = new List<AnalyticsRecord>();

        public bool ThrowOnWrite { get; set; }

        public IReadOnlyList<AnalyticsRecord> Records => _records;

        public void Write(string eventName, IReadOnlyList<AnalyticsParam> parameters, DateTime at)
        {
            if (ThrowOnWrite)
                throw new InvalidOperationException("sink failure");

            _records.Add(new AnalyticsRecord(eventName, parameters, at));
        }

        public bool HasEvent(string eventName)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Name == eventName)
                    return true;
            }

            return false;
        }

        public int CountOf(string eventName)
        {
            var count = 0;
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Name == eventName)
                    count++;
            }

            return count;
        }
    }

    /// <summary>生产占位实现：结构化输出到编辑器日志，便于人工核对 R24 事件。</summary>
    public sealed class UnityDebugSink : IAnalyticsSink
    {
        public void Write(string eventName, IReadOnlyList<AnalyticsParam> parameters, DateTime at)
        {
            if (parameters == null || parameters.Count == 0)
            {
                Debug.Log($"[Analytics] {eventName}");
                return;
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("[Analytics] ").Append(eventName);
            for (var i = 0; i < parameters.Count; i++)
                builder.Append(' ').Append(parameters[i].Key).Append('=').Append(parameters[i].Value);

            Debug.Log(builder.ToString());
        }
    }

    /// <summary>
    /// 埋点门面。隐私同意前 <see cref="IsInitialized"/> 为 false，所有上报静默丢弃（R20）；
    /// Sink 抛异常时被吞掉并降级为一次告警，绝不影响游戏流程。
    /// </summary>
    public sealed class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsSink _sink;
        private readonly IClock _clock;
        private bool _sinkFailureLogged;

        public AnalyticsService(IAnalyticsSink sink, IClock clock)
        {
            _sink = sink;
            _clock = clock ?? new SystemClock();
        }

        public bool IsInitialized { get; private set; }

        public void Initialize() => IsInitialized = true;

        public void Track(string eventName, params AnalyticsParam[] parameters)
        {
            if (!IsInitialized || string.IsNullOrEmpty(eventName) || _sink == null)
                return;

            try
            {
                _sink.Write(eventName, parameters, _clock.Now);
            }
            catch (Exception e)
            {
                if (_sinkFailureLogged)
                    return;

                _sinkFailureLogged = true;
                Debug.LogWarning($"[Analytics] Sink 写入失败，已忽略后续错误：{e.Message}");
            }
        }
    }

    /// <summary>同意隐私前的空实现：永不初始化、永不上报。</summary>
    public sealed class NullAnalyticsService : IAnalyticsService
    {
        public bool IsInitialized => false;

        public void Initialize()
        {
        }

        public void Track(string eventName, params AnalyticsParam[] parameters)
        {
        }
    }
}
