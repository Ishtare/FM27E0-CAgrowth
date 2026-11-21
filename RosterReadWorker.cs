// Modified build v1.1.22, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fm24Editor
{
    // One roster reader owns the current connection until it returns. Publication stays on the UI thread.
    sealed class RosterReadWorker
    {
        CancellationTokenSource active;
        Task running;
        bool closed;

        public bool Busy { get { return active != null; } }

        public async Task<T> Run<T>(Func<CancellationToken,T> read)
        {
            if(closed || Busy) throw new InvalidOperationException("已有名单读取任务正在进行。");
            var source=new CancellationTokenSource();
            active=source;
            var task=Task.Run(() => read(source.Token),source.Token);
            running=task;
            try
            {
                var result=await task;
                source.Token.ThrowIfCancellationRequested();
                return result;
            }
            catch
            {
                source.Token.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                running=null;
                active=null;
                source.Dispose();
            }
        }

        public void Cancel()
        {
            if(active!=null) active.Cancel();
        }

        public void Close(IDisposable connection)
        {
            closed=true;
            Cancel();
            if(connection==null) return;
            if(running==null || running.IsCompleted) connection.Dispose();
            else running.ContinueWith(_ => connection.Dispose(),TaskScheduler.Default);
        }
    }
}
