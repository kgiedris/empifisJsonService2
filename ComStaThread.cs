using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace empifisJsonAPI2
{
    /// <summary>
    /// A dedicated STA thread with a Windows message loop for an apartment-threaded COM object.
    /// Creating and calling the object here keeps every call on the thread that owns it (instead of
    /// COM marshalling through one shared hidden host thread), so a call that hangs blocks only this
    /// thread and the service can move on to a fresh one.
    /// </summary>
    public sealed class ComStaThread
    {
        private readonly SynchronizationContext _context;

        public ComStaThread(string name)
        {
            var ready = new TaskCompletionSource<SynchronizationContext>();
            var thread = new Thread(() =>
            {
                var context = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);
                ready.SetResult(context);
                Application.Run();
            })
            {
                Name = name,
                // A thread abandoned in a hung COM call must not keep the process alive.
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _context = ready.Task.GetAwaiter().GetResult();
        }

        /// <summary>Queues <paramref name="func"/> to run on the STA thread, after any call already queued.</summary>
        public Task<T> Invoke<T>(Func<T> func)
        {
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                _context.Post(_ =>
                {
                    try { result.SetResult(func()); }
                    catch (Exception ex) { result.SetException(ex); }
                }, null);
            }
            catch (Exception ex)
            {
                // The message loop has already ended.
                result.SetException(ex);
            }
            return result.Task;
        }

        /// <summary>
        /// Queues release of the COM object (looked up on the thread itself) and the end of the message
        /// loop. If the thread is stuck in a hung call, this runs only when (if ever) that call returns.
        /// </summary>
        public Task Shutdown(Func<object?> comObjectToRelease) => Invoke(() =>
        {
            try
            {
                var comObject = comObjectToRelease();
                if (comObject != null)
                {
                    Marshal.ReleaseComObject(comObject);
                }
            }
            finally
            {
                Application.ExitThread();
            }
            return true;
        });
    }
}
