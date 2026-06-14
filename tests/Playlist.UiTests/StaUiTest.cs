using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Playlist.UiTests;

/// <summary>
/// Runs UI assertions on a single persistent STA dispatcher thread so WPF/COM
/// input services are not torn down per test (which triggers RCW shutdown errors).
/// </summary>
internal static class StaUiTest
{
    private static readonly object Gate = new object();
    private static Thread staThread;
    private static Dispatcher dispatcher;
    private static int initialized;

    static StaUiTest()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, __) => Shutdown();
    }

    public static void Run(Action test)
    {
        if (test == null)
        {
            throw new ArgumentNullException(nameof(test));
        }

        EnsureStaThread();
        Exception caught = null;
        dispatcher.Invoke(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        if (caught != null)
        {
            throw caught;
        }
    }

    private static void EnsureStaThread()
    {
        if (Volatile.Read(ref initialized) == 1)
        {
            return;
        }

        lock (Gate)
        {
            if (Volatile.Read(ref initialized) == 1)
            {
                return;
            }

            using (var ready = new ManualResetEventSlim(false))
            {
                staThread = new Thread(() =>
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }

                    dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.IsBackground = true;
                staThread.Name = "Playlist.UiTests.STA";
                staThread.Start();
                ready.Wait();
            }

            Volatile.Write(ref initialized, 1);
        }
    }

    private static void Shutdown()
    {
        if (dispatcher == null)
        {
            return;
        }

        try
        {
            dispatcher.Invoke(() =>
            {
                if (Application.Current != null)
                {
                    Application.Current.Shutdown();
                }
            });
        }
        catch
        {
            // Process is exiting; best-effort cleanup only.
        }

        try
        {
            dispatcher.InvokeShutdown();
        }
        catch
        {
        }

        staThread?.Join(TimeSpan.FromSeconds(2));
    }
}
