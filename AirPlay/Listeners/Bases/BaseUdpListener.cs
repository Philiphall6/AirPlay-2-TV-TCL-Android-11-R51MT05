using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AirPlay.Listeners
{
    public class BaseUdpListener : BaseListener
    {
        public const int CloseTimeout = 1000;

        private readonly Socket _cSocket;
        private readonly Socket _dSocket;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public bool IsStopped => _cancellationTokenSource.IsCancellationRequested;

        public BaseUdpListener(ushort cPort, ushort dPort)
        {
            _cSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            _dSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);

            // AirPlay sends audio in short bursts. A larger kernel queue keeps
            // packets available while ALAC decoding and AudioTrack writes run.
            _cSocket.ReceiveBufferSize = 1024 * 1024;
            _dSocket.ReceiveBufferSize = 4 * 1024 * 1024;

            _cSocket.Bind(new IPEndPoint(IPAddress.Any, cPort));
            _dSocket.Bind(new IPEndPoint(IPAddress.Any, dPort));

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            _ = Task.Run(() => RunWorkerAsync("control", _cSocket, OnRawCSocketAsync, source.Token), source.Token);
            _ = Task.Run(() => RunWorkerAsync("data", _dSocket, OnRawDSocketAsync, source.Token), source.Token);

            return Task.CompletedTask;
        }

        public override Task StopAsync()
        {
            _cancellationTokenSource.Cancel();

            _cSocket.Close(CloseTimeout);
            _dSocket.Close(CloseTimeout);

            return Task.CompletedTask;
        }

        public virtual Task OnRawCSocketAsync(Socket cSocket, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnRawDSocketAsync(Socket dSocket, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected virtual void OnWorkerFailed(string worker, Exception exception)
        {
            Console.WriteLine($"UDP {worker} worker stopped: {exception}");
        }

        private async Task RunWorkerAsync(
            string worker,
            Socket socket,
            Func<Socket, CancellationToken, Task> action,
            CancellationToken cancellationToken)
        {
            try
            {
                await action(socket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                OnWorkerFailed(worker, exception);
            }
        }
    }
}
