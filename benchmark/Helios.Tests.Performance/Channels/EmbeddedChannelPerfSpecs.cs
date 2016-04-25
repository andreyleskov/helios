﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helios.Buffers;
using Helios.Channels;
using Helios.Channels.Embedded;
using Helios.Codecs;
using Helios.Concurrency;
using Helios.Logging;
using Helios.Util;
using NBench;

namespace Helios.Tests.Performance.Channels
{
    /// <summary>
    /// End-to-end performance benchmark for a realistic-ish pipeline built using the <see cref="EmbeddedChannel"/>
    /// 
    /// Contains a total of three handlers and runs on the same thread as the caller, so all calls made against the pipeline
    /// are totally synchronous. Tests the overhead of the following components working together:
    /// 
    /// 1. <see cref="IChannelPipeline"/> default implementation
    /// 2. <see cref="IChannelHandlerContext"/> default implementation
    /// 3. <see cref="IRecvByteBufAllocator"/> default implementation
    /// 4. <see cref="IByteBufAllocator"/> default implementation 
    /// 5. <see cref="ChannelOutboundBuffer"/>
    /// 6. <see cref="ObjectPool{T}"/> and <see cref="RecyclableArrayList"/>
    /// 7. <see cref="AbstractChannel"/> and its built-in <see cref="IChannelUnsafe"/> implementation
    /// 8. <see cref="LengthFieldPrepender"/> and <see cref="LengthFieldBasedFrameDecoder"/>
    /// 9. And finally, <see cref="AbstractEventExecutor"/>.
    /// 
    /// Buffer size for each individual written message is intentionally kept small, since this is a throughput and GC overhead test.
    /// </summary>
    public class EmbeddedChannelPerfSpecs
    {
        static EmbeddedChannelPerfSpecs()
        {
            // Disable the logging factory
            LoggingFactory.DefaultFactory = new NoOpLoggerFactory();
        }

        private EmbeddedChannel channel;
        private IByteBuf message;
        private const string InboundThroughputCounterName = "inbound ops";
        private Counter _inboundThroughputCounter;

        private const string OutboundThroughputCounterName = "outbound ops";
        private Counter _outboundThroughputCounter;

        private IChannelHandler _lengthFieldPrepender;
        private IChannelHandler _lengthFieldFrameDecoder;
        private IChannelHandler _counterHandlerInbound;
        private IChannelHandler _counterHandlerOutbound;

        private class CounterHandlerInbound : ChannelHandlerAdapter
        {
            private readonly Counter _throughput;

            public CounterHandlerInbound(Counter throughput)
            {
                _throughput = throughput;
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                _throughput.Increment();
            }
        }

        private class CounterHandlerOutbound : ChannelHandlerAdapter
        {
            private readonly Counter _throughput;

            public CounterHandlerOutbound(Counter throughput)
            {
                _throughput = throughput;
            }

            public override Task WriteAsync(IChannelHandlerContext context, object message)
            {
                _throughput.Increment();
                return TaskCompletionSource.Void.Task; // don't allocate any tasks
            }
        }

        [PerfSetup]
        public void SetUp(BenchmarkContext context)
        {
            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            byte[] charBytes = iso.GetBytes("ABC");
            message = Unpooled.WrappedBuffer(charBytes);
            _inboundThroughputCounter = context.GetCounter(InboundThroughputCounterName);
            _counterHandlerInbound = new CounterHandlerInbound(_inboundThroughputCounter);
            _outboundThroughputCounter = context.GetCounter(OutboundThroughputCounterName);
            _counterHandlerOutbound = new CounterHandlerOutbound(_outboundThroughputCounter);

            _lengthFieldFrameDecoder = new LengthFieldBasedFrameDecoder(20,0,4,0,4);
            _lengthFieldPrepender = new LengthFieldPrepender(4, true);

            channel = new EmbeddedChannel(_counterHandlerOutbound, _counterHandlerInbound, _lengthFieldFrameDecoder, _lengthFieldPrepender);
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead the EmbeddedChannel can decode / encode realistic messages",
            NumberOfIterations = 13, RunMode = RunMode.Throughput)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void EmbeddedChannel_Inbound_Throughput(BenchmarkContext context)
        {
            channel.WriteInbound(message);
        }

        [PerfBenchmark(Description = "Measures how quickly and with how much GC overhead the EmbeddedChannel can decode / encode realistic messages",
            NumberOfIterations = 13, RunMode = RunMode.Throughput)]
        [CounterMeasurement(InboundThroughputCounterName)]
        [CounterMeasurement(OutboundThroughputCounterName)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void EmbeddedChannel_Outbound_Throughput(BenchmarkContext context)
        {
            channel.WriteOutbound(message);
        }

        [PerfCleanup]
        public void CleanUp()
        {
            channel.Finish();
            channel = null;
        }
    }
}
