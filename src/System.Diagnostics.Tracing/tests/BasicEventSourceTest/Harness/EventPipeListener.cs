using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using static BasicEventSourceTests.EtwListener;

namespace BasicEventSourceTests
{
    class EventPipeListener : Listener
    {
        Dictionary<string, Provider> _providers = new Dictionary<string, Provider>();
        Task _processSession;
        CancellationTokenSource _cts;

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void EventSourceCommand(string eventSourceName, EventCommand command, FilteringOptions options = null)
        {
            if(_cts != null)
            {
                _cts.Cancel();
            }
            if(_processSession != null)
            {
                _processSession.Wait();
            }
            _cts = new CancellationTokenSource();
            _processSession = null;

            if (command == EventCommand.Enable)
            {
                if (options == null)
                    options = new FilteringOptions();
                string args = null;
                _providers[eventSourceName] = new Provider(eventSourceName, (ulong)options.Keywords, options.Level, args);
            }
            else if(command == EventCommand.Disable)
            {
                _providers.Remove(eventSourceName);
            }
            int processId = Process.GetCurrentProcess().Id;
            SessionConfigurationV2 config = new SessionConfigurationV2(10, EventPipeSerializationFormat.NetTrace, false, _providers.Values);
            Stream s = EventPipeClient.CollectTracing2(processId, config, out ulong sessionId);
            EventPipeEventSource source = null;
            _cts.Token.Register(() =>
            {
                source?.StopProcessing();
                EventPipeClient.StopTracing(processId, sessionId);
            });
            _processSession = Task.Run(() =>
            {
                using (source = new EventPipeEventSource(s))
                {
                    source.Dynamic.All += Dynamic_All;
                    source.Process();
                }
            },
            _cts.Token);
        }

        private void Dynamic_All(TraceEvent obj)
        {
            if (obj.ProviderName == "Microsoft-DotNETCore-EventPipe")
                return;

            this.OnEvent?.Invoke(new EtwEvent(obj));
        }
    }
}
