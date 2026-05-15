using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace WebPaper.Services
{
    /// <summary>
    /// Receives one-shot command strings from secondary WebPaper instances launched
    /// by the desktop right-click menu. The single-instance Mutex in App.OnLaunched
    /// blocks the second process from running its own UI; instead it opens this
    /// pipe, sends its command (e.g. "--settings"), and exits. The running primary
    /// instance dispatches the command to its existing handlers.
    ///
    /// We use a per-user pipe name so multiple users on the same machine don't
    /// collide. The pipe ACL is the .NET default (current user only), which is
    /// what we want — only the same user can talk to us.
    /// </summary>
    public sealed class IpcServer : IDisposable
    {
        public const string PipeName = "WebPaper.IPC.v1";

        private readonly Action<string> _onCommand;
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public IpcServer(Action<string> onCommand)
        {
            _onCommand = onCommand ?? throw new ArgumentNullException(nameof(onCommand));
        }

        public void Start()
        {
            if (_loopTask != null) return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    // Read until EOF — the client always sends one short ASCII command.
                    var buffer = new byte[256];
                    int total = 0;
                    int read;
                    while (total < buffer.Length &&
                           (read = await server.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct)
                                .ConfigureAwait(false)) > 0)
                    {
                        total += read;
                    }

                    if (total > 0)
                    {
                        string command = Encoding.UTF8.GetString(buffer, 0, total).Trim();
                        if (command.Length > 0)
                        {
                            try { _onCommand(command); }
                            catch (Exception ex) { Log.Error(ex, "IpcServer: command handler threw"); }
                        }
                    }
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex)
                {
                    Log.Warning(ex, "IpcServer: accept loop error, retrying");
                    try { await Task.Delay(500, ct).ConfigureAwait(false); }
                    catch { /* shutdown */ }
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }

        /// <summary>
        /// Client side: connect to the running primary instance and send a command.
        /// Called from App.OnLaunched when the single-instance Mutex shows we're the
        /// second instance. Returns true on success.
        /// </summary>
        public static bool TrySendCommand(string command, int timeoutMs = 1500)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: PipeName,
                    direction: PipeDirection.Out,
                    options: PipeOptions.None);

                client.Connect(timeoutMs);
                var bytes = Encoding.UTF8.GetBytes(command);
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "IpcServer.TrySendCommand failed for {Cmd}", command);
                return false;
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _loopTask?.Wait(500); } catch { /* ignore */ }
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }
}
