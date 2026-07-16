using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace TerrariaSplit.WorldGuard.Payload
{
    internal sealed class PayloadCommandResult
    {
        public PayloadCommandResult(int code, string message, bool stopServer)
        {
            Code = code;
            Message = message ?? string.Empty;
            StopServer = stopServer;
        }

        public int Code { get; private set; }

        public string Message { get; private set; }

        public bool StopServer { get; private set; }
    }

    internal sealed class PayloadCommandServer
    {
        private const int MaximumEncodedCommandLength = 131072;
        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(20);
        private readonly string pipeName;
        private readonly Func<string, PayloadCommandResult> handler;
        private readonly Thread thread;
        private readonly Thread watchdogThread;
        private readonly object handlerSync = new object();
        private readonly object pipeSync = new object();
        private readonly ManualResetEvent started = new ManualResetEvent(false);
        private NamedPipeServerStream currentPipe;
        private Exception startupFailure;
        private long leaseDeadlineUtcTicks;
        private int stopRequested;

        public PayloadCommandServer(string pipeName, Func<string, PayloadCommandResult> handler)
        {
            this.pipeName = pipeName;
            this.handler = handler;
            thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "TerrariaSplit Race hook"
            };
            watchdogThread = new Thread(WatchLease)
            {
                IsBackground = true,
                Name = "TerrariaSplit Race hook lease"
            };
        }

        public string PipeName
        {
            get { return pipeName; }
        }

        public void Start()
        {
            thread.Start();
            if (!started.WaitOne(StartupTimeout))
            {
                RequestStop();
                throw new TimeoutException("The Race hook command server did not start in time.");
            }

            if (startupFailure != null)
            {
                throw new InvalidOperationException("The Race hook command server could not start.", startupFailure);
            }

            TouchLease();
            watchdogThread.Start();
        }

        private void Run()
        {
            bool firstAttempt = true;
            while (!IsStopRequested())
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None))
                    {
                        lock (pipeSync)
                        {
                            currentPipe = pipe;
                        }

                        if (firstAttempt)
                        {
                            firstAttempt = false;
                            started.Set();
                        }

                        pipe.WaitForConnection();
                        if (IsStopRequested())
                        {
                            return;
                        }

                        var reader = new StreamReader(pipe, new UTF8Encoding(false));
                        var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                        string encoded = ReadBoundedLine(reader);
                        PayloadCommandResult result = DecodeAndHandle(encoded);
                        string message = Convert.ToBase64String(Encoding.UTF8.GetBytes(result.Message));
                        writer.WriteLine(result.Code.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + message);
                        if (result.StopServer)
                        {
                            Interlocked.Exchange(ref stopRequested, 1);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ObjectDisposedException)
                {
                    if (firstAttempt)
                    {
                        firstAttempt = false;
                        startupFailure = ex;
                        started.Set();
                        return;
                    }
                }
                finally
                {
                    lock (pipeSync)
                    {
                        currentPipe = null;
                    }
                }
            }
        }

        private void WatchLease()
        {
            while (!IsStopRequested())
            {
                Thread.Sleep(1000);
                if (DateTime.UtcNow.Ticks <= Interlocked.Read(ref leaseDeadlineUtcTicks))
                {
                    continue;
                }

                try
                {
                    lock (handlerSync)
                    {
                        if (!IsStopRequested() &&
                            DateTime.UtcNow.Ticks > Interlocked.Read(ref leaseDeadlineUtcTicks))
                        {
                            handler("shutdown");
                        }
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    RequestStop();
                }
                return;
            }
        }

        private void TouchLease()
        {
            Interlocked.Exchange(ref leaseDeadlineUtcTicks, DateTime.UtcNow.Add(LeaseTimeout).Ticks);
        }

        private bool IsStopRequested()
        {
            return Interlocked.CompareExchange(ref stopRequested, 0, 0) != 0;
        }

        private void RequestStop()
        {
            Interlocked.Exchange(ref stopRequested, 1);
            lock (pipeSync)
            {
                if (currentPipe != null)
                {
                    try
                    {
                        currentPipe.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private static string ReadBoundedLine(StreamReader reader)
        {
            var value = new StringBuilder();
            while (value.Length <= MaximumEncodedCommandLength)
            {
                int character = reader.Read();
                if (character < 0)
                {
                    return value.Length == 0 ? null : value.ToString();
                }

                if (character == '\n')
                {
                    return value.ToString();
                }

                if (character != '\r')
                {
                    value.Append((char)character);
                }
            }

            return null;
        }

        private PayloadCommandResult DecodeAndHandle(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedCommandLength)
            {
                return new PayloadCommandResult(2, "Invalid hook command.", false);
            }

            try
            {
                string command = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                lock (handlerSync)
                {
                    PayloadCommandResult result = handler(command);
                    if (result.Code == 0 && !result.StopServer)
                    {
                        TouchLease();
                    }

                    return result;
                }
            }
            catch (FormatException)
            {
                return new PayloadCommandResult(2, "Invalid hook command.", false);
            }
            catch (Exception)
            {
                return new PayloadCommandResult(99, "The Race hook command failed.", false);
            }
        }
    }
}
