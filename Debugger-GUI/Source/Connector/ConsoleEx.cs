/*
 * Copyright 2022 Marcel Bulla
 * 
 * This file is part of Debugger-GUI.
 * 
 * Debugger-GUI is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version.
 * 
 * Foobar is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more
 * details.
 * 
 * You should have received a copy of the GNU General Public License along
 * with Foobar. If not, see http://www.gnu.org/licenses/.
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Debugger.Source.Connector
{
    /// <summary>
    /// This class provides additional functionality for connected console applications.
    /// </summary>
    public static class ConsoleEx
    {
        #region Private Fields

        private static readonly Thread _threadCtrlEventSender;
        private static readonly Queue<Func<bool>> _queueCtrlJobs;
        private static readonly object _cndQueueCtrlJobs;
        private static bool _bRun;

        #endregion

        #region DLL Imports

        [DllImport("kernel32.dll")]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);

        #endregion

        #region Constructor

        static ConsoleEx()
        {
            _bRun = true;
            _queueCtrlJobs = new Queue<Func<bool>>();
            _cndQueueCtrlJobs = new object();
            _threadCtrlEventSender = new Thread(() => CtrlEventSender());
            _threadCtrlEventSender.Start();

            App.Instance.Exit += Instance_Exit;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends a CTRL event to a console application.
        /// </summary>
        /// <param name="enumEvent">The event that should be sent.</param>
        /// <param name="uiPid">The process ID of the target process.</param>
        /// <exception cref="AccessViolationException">A violation might occur of this process is not authorized to send
        /// the event.</exception>
        public static void SendConsoleCtrlEvent(EnumConsoleCtrlEvent enumEvent, uint uiPid)
        {
            object _cndWait = new();

            lock (_cndWait)
            {
                lock (_cndQueueCtrlJobs)
                {
                    _queueCtrlJobs.Enqueue(() =>
                    {
                        if (AttachConsole(uiPid))
                        {
                            if (!SetConsoleCtrlHandler(null, true))
                            {
                                throw new AccessViolationException();
                            }

                            try
                            {
                                GenerateConsoleCtrlEvent((uint)enumEvent, 0);

                                Thread.Sleep(500);
                            }
                            finally
                            {
                                SetConsoleCtrlHandler(null, false);
                                FreeConsole();
                            }
                        }

                        lock (_cndWait)
                        {
                            Monitor.Pulse(_cndWait);
                        }
                        return true;
                    });
                    Monitor.Pulse(_cndQueueCtrlJobs);
                }
                Monitor.Wait(_cndWait);
            }
        }

        #endregion

        #region Private Methods

        private static void Instance_Exit(object sender, System.Windows.ExitEventArgs e)
        {
            _bRun = false;
            _threadCtrlEventSender.Join();
        }

        private static void CtrlEventSender()
        {
            while (_bRun)
            {
                lock (_cndQueueCtrlJobs)
                {
                    while (_queueCtrlJobs.Count > 0)
                    {
                        _queueCtrlJobs.Dequeue()();
                    }

                    Thread.Sleep(100);
                }
            }
        }

        #endregion
    }
}
